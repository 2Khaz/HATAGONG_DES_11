using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HATAGONG.Outgame
{
    public sealed class OutgameRequestCarouselController : MonoBehaviour, IBeginDragHandler, IEndDragHandler
    {
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform content;
        [SerializeField] private Image[] pageIndicators = Array.Empty<Image>();
        [SerializeField] private Color selectedPageColor = new Color32(255, 200, 48, 255);
        [SerializeField] private Color unselectedPageColor = new Color32(145, 145, 145, 180);
        [SerializeField] private float snapDuration = 0.18f;

        private Coroutine snapRoutine;
        private int pageCount = 3;
        private float dragStartPointerX;
        private int dragStartPage;

        private const float CardWidthRatio = 0.88f;
        private const float CardHeightLimitRatio = 0.80f;
        private const float CardAspectRatio = 1512f / 1015f;
        private const float CardVerticalOffsetRatio = 0.045f;

        public int CurrentPage { get; private set; }
        public int CurrentPageIndex => CurrentPage;
        public int PageCount => pageCount;
        public RectTransform Viewport => viewport;
        public RectTransform Content => content;
        public Vector2 CardDisplaySize { get; private set; }
        public event Action<int> PageChanged;

        public void Initialize(int count)
        {
            if (count != 3) throw new ArgumentOutOfRangeException(nameof(count), "The carousel requires exactly three pages.");
            if (scrollRect == null || viewport == null || content == null || pageIndicators.Length != 3)
                throw new InvalidOperationException("Carousel references are not configured.");

            pageCount = count;
            Canvas.ForceUpdateCanvases();
            ApplyViewportRelativeLayout();
            ResetToFirstPage();
        }

        public void RefreshLayout()
        {
            if (scrollRect == null || viewport == null || content == null || pageIndicators.Length != 3)
                return;
            Canvas.ForceUpdateCanvases();
            ApplyViewportRelativeLayout();
            SetContentPosition(CurrentPage);
        }

        private void ApplyViewportRelativeLayout()
        {
            float pageWidth = viewport.rect.width;
            float viewportHeight = viewport.rect.height;
            float cardWidth = pageWidth * CardWidthRatio;
            float cardHeight = cardWidth * CardAspectRatio;
            float maximumHeight = viewportHeight * CardHeightLimitRatio;
            if (cardHeight > maximumHeight)
            {
                cardHeight = maximumHeight;
                cardWidth = cardHeight / CardAspectRatio;
            }
            CardDisplaySize = new Vector2(cardWidth, cardHeight);

            for (int i = 0; i < content.childCount; i++)
            {
                OutgameRequestCardView card = content.GetChild(i).GetComponent<OutgameRequestCardView>();
                if (card != null) card.ApplyDisplaySize(CardDisplaySize);
            }

            HorizontalLayoutGroup layout = content.GetComponent<HorizontalLayoutGroup>();
            if (layout == null) throw new InvalidOperationException("RequestContent requires a HorizontalLayoutGroup.");
            int sidePadding = Mathf.RoundToInt((pageWidth - cardWidth) * 0.5f);
            layout.padding = new RectOffset(sidePadding, sidePadding, 0, 0);
            layout.spacing = Mathf.Max(0f, pageWidth - cardWidth);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            content.anchorMin = new Vector2(0f, 0f);
            content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 0.5f);
            content.sizeDelta = new Vector2(pageWidth * pageCount, 0f);
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, viewportHeight * CardVerticalOffsetRatio);
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }

        public void ResetToFirstPage()
        {
            StopSnap();
            bool changed = CurrentPage != 0;
            CurrentPage = 0;
            SetContentPosition(0);
            UpdateIndicators();
            if (scrollRect != null) scrollRect.StopMovement();
            if (changed) PageChanged?.Invoke(CurrentPage);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            StopSnap();
            dragStartPointerX = eventData.position.x;
            dragStartPage = CurrentPage;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (viewport == null || content == null) return;
            float viewportWidthInPixels = viewport.rect.width * viewport.GetComponentInParent<Canvas>().scaleFactor;
            float threshold = Mathf.Max(60f, viewportWidthInPixels * 0.12f);
            float dragDeltaX = eventData.position.x - dragStartPointerX;
            int targetPage = dragStartPage;
            if (dragDeltaX <= -threshold) targetPage++;
            else if (dragDeltaX >= threshold) targetPage--;
            SnapToPage(Mathf.Clamp(targetPage, 0, pageCount - 1));
        }

        public void SnapToPage(int page)
        {
            int clamped = Mathf.Clamp(page, 0, pageCount - 1);
            bool changed = CurrentPage != clamped;
            CurrentPage = clamped;
            UpdateIndicators();
            if (changed) PageChanged?.Invoke(CurrentPage);
            StopSnap();
            if (!isActiveAndEnabled || snapDuration <= 0f)
            {
                SetContentPosition(clamped);
                return;
            }
            snapRoutine = StartCoroutine(SnapRoutine(clamped));
        }

        public bool IsPageAligned(float tolerance = 1f)
        {
            if (viewport == null || content == null) return false;
            float expected = -CurrentPage * viewport.rect.width;
            return Mathf.Abs(content.anchoredPosition.x - expected) <= tolerance;
        }

        private IEnumerator SnapRoutine(int page)
        {
            if (scrollRect != null) scrollRect.StopMovement();
            Vector2 start = content.anchoredPosition;
            Vector2 target = new Vector2(-page * viewport.rect.width, start.y);
            float elapsed = 0f;
            while (elapsed < snapDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / snapDuration);
                content.anchoredPosition = Vector2.Lerp(start, target, 1f - Mathf.Pow(1f - t, 3f));
                yield return null;
            }
            content.anchoredPosition = target;
            snapRoutine = null;
        }

        private void SetContentPosition(int page)
        {
            Vector2 position = content.anchoredPosition;
            position.x = -page * viewport.rect.width;
            content.anchoredPosition = position;
        }

        private void UpdateIndicators()
        {
            for (int i = 0; i < pageIndicators.Length; i++)
            {
                if (pageIndicators[i] == null) continue;
                pageIndicators[i].color = i == CurrentPage ? selectedPageColor : unselectedPageColor;
                pageIndicators[i].raycastTarget = false;
            }
        }

        private void StopSnap()
        {
            if (snapRoutine == null) return;
            StopCoroutine(snapRoutine);
            snapRoutine = null;
        }
    }
}
