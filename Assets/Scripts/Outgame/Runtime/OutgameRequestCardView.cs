using System;
using System.Collections.Generic;
using HATAGONG.GameFlow;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HATAGONG.Outgame
{
    public sealed class OutgameRequestCardView : MonoBehaviour
    {
        [Serializable]
        private sealed class EffectSlotView
        {
            [SerializeField] private Image background;
            [SerializeField] private Image iconPlaceholder;
            [SerializeField] private TextMeshProUGUI effectNameLabel;

            public bool IsFilled { get; private set; }
            public string EffectName => effectNameLabel == null ? string.Empty : effectNameLabel.text;

            public void ResetDisplay()
            {
                IsFilled = false;
                if (background != null)
                {
                    background.color = new Color32(142, 142, 142, 255);
                    background.raycastTarget = false;
                }
                if (iconPlaceholder != null)
                {
                    iconPlaceholder.sprite = null;
                    iconPlaceholder.color = Color.clear;
                    iconPlaceholder.raycastTarget = false;
                    iconPlaceholder.gameObject.SetActive(false);
                }
                if (effectNameLabel != null)
                {
                    effectNameLabel.text = string.Empty;
                    effectNameLabel.gameObject.SetActive(false);
                }
            }

            public void Bind(OutgameRequestEffectDefinition definition, Color32 iconColor)
            {
                ResetDisplay();
                if (definition == null) return;
                IsFilled = true;
                if (background != null) background.color = new Color32(238, 232, 220, 255);
                if (iconPlaceholder != null)
                {
                    iconPlaceholder.sprite = null;
                    iconPlaceholder.color = iconColor;
                    iconPlaceholder.gameObject.SetActive(true);
                }
                if (effectNameLabel != null)
                {
                    effectNameLabel.text = definition.EffectName;
                    effectNameLabel.gameObject.SetActive(true);
                }
            }
        }

        [Header("Card")]
        [SerializeField] private Image clipboardBackground;
        [SerializeField] private Image portraitPlaceholder;
        [SerializeField] private TextMeshProUGUI requestTypeLabel;
        [SerializeField] private TextMeshProUGUI requesterNameLabel;
        [SerializeField] private TextMeshProUGUI titleLabel;
        [SerializeField] private TextMeshProUGUI difficultyLabel;
        [SerializeField] private Image[] difficultyStars = Array.Empty<Image>();
        [SerializeField] private Sprite activeStarSprite;
        [SerializeField] private Sprite inactiveStarSprite;
        [SerializeField] private TextMeshProUGUI descriptionTitleLabel;
        [SerializeField] private TextMeshProUGUI descriptionLabel;
        [SerializeField] private EffectSlotView[] effectSlots = Array.Empty<EffectSlotView>();
        [SerializeField] private Button performButton;
        [SerializeField] private TextMeshProUGUI performButtonLabel;

        private static readonly Color32[] EffectColors =
        {
            new Color32(71, 151, 230, 255),
            new Color32(244, 179, 62, 255),
            new Color32(107, 190, 119, 255)
        };

        private RectTransform cardRect;
        private RectTransform[] layoutRects = Array.Empty<RectTransform>();
        private Vector2[] baseAnchoredPositions = Array.Empty<Vector2>();
        private Vector2[] baseSizeDeltas = Array.Empty<Vector2>();
        private TextMeshProUGUI[] layoutTexts = Array.Empty<TextMeshProUGUI>();
        private float[] baseFontSizes = Array.Empty<float>();
        private float[] baseFontSizeMins = Array.Empty<float>();
        private float[] baseFontSizeMaxes = Array.Empty<float>();
        private Vector2 baseCardSize;

        public OutgameRequestOffer BoundOffer { get; private set; }
        public event Action<OutgameRequestOffer> PerformRequested;
        public string RequestTypeText => requestTypeLabel == null ? string.Empty : requestTypeLabel.text;
        public string RequesterNameText => requesterNameLabel == null ? string.Empty : requesterNameLabel.text;
        public string TitleText => titleLabel == null ? string.Empty : titleLabel.text;
        public string DescriptionText => descriptionLabel == null ? string.Empty : descriptionLabel.text;
        public int FilledEffectCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < effectSlots.Length; i++)
                    if (effectSlots[i] != null && effectSlots[i].IsFilled) count++;
                return count;
            }
        }
        public bool IsPerformButtonInteractable => performButton != null && performButton.interactable;
        public Vector2 DisplaySize => cardRect == null ? Vector2.zero : cardRect.rect.size;

        private void Awake()
        {
            CaptureBaseLayout();
            if (performButton != null)
            {
                if (performButton.targetGraphic != null) performButton.targetGraphic.raycastTarget = true;
                performButton.onClick.AddListener(HandlePerformClicked);
            }
        }

        private void OnDestroy()
        {
            if (performButton != null) performButton.onClick.RemoveListener(HandlePerformClicked);
        }

        public void Bind(OutgameRequestOffer offer)
        {
            if (offer == null) throw new ArgumentNullException(nameof(offer));
            ResetDisplay();
            BoundOffer = offer;
            OutgameRequestDefinition definition = offer.Definition;

            requestTypeLabel.text = definition.RequestType == RequestType.Sudden
                ? "특수 의뢰 발생!"
                : "일반 의뢰 발생!";
            requesterNameLabel.text = "의뢰주: " + definition.RequesterName;
            titleLabel.text = definition.Title;
            descriptionLabel.text = definition.Description;

            int activeStars = GetActiveStarCount(definition.Difficulty);
            for (int i = 0; i < difficultyStars.Length; i++)
                difficultyStars[i].sprite = i < activeStars ? activeStarSprite : inactiveStarSprite;

            int effectCount = Math.Min(effectSlots.Length, definition.Effects.Count);
            for (int i = 0; i < effectCount; i++)
                effectSlots[i].Bind(definition.Effects[i], EffectColors[i % EffectColors.Length]);
        }

        public void SetPerformInteractable(bool interactable)
        {
            if (performButton != null) performButton.interactable = interactable;
        }

        public void ApplyDisplaySize(Vector2 size)
        {
            if (cardRect == null || baseCardSize.x <= 0f) CaptureBaseLayout();
            if (size.x <= 0f || size.y <= 0f)
                throw new ArgumentOutOfRangeException(nameof(size), "Card display size must be positive.");

            float scale = size.x / baseCardSize.x;
            cardRect.sizeDelta = size;
            cardRect.localScale = Vector3.one;
            for (int i = 0; i < layoutRects.Length; i++)
            {
                RectTransform rect = layoutRects[i];
                if (rect == null) continue;
                rect.anchoredPosition = baseAnchoredPositions[i] * scale;
                rect.sizeDelta = baseSizeDeltas[i] * scale;
                rect.localScale = Vector3.one;
            }
            for (int i = 0; i < layoutTexts.Length; i++)
            {
                TextMeshProUGUI text = layoutTexts[i];
                if (text == null) continue;
                text.fontSize = baseFontSizes[i] * scale;
                text.fontSizeMin = baseFontSizeMins[i] * scale;
                text.fontSizeMax = baseFontSizeMaxes[i] * scale;
            }
        }

        private void CaptureBaseLayout()
        {
            cardRect = transform as RectTransform;
            if (cardRect == null) throw new InvalidOperationException("Request Card requires a RectTransform.");
            baseCardSize = cardRect.rect.size;

            RectTransform[] allRects = GetComponentsInChildren<RectTransform>(true);
            layoutRects = new RectTransform[Math.Max(0, allRects.Length - 1)];
            baseAnchoredPositions = new Vector2[layoutRects.Length];
            baseSizeDeltas = new Vector2[layoutRects.Length];
            int rectIndex = 0;
            for (int i = 0; i < allRects.Length; i++)
            {
                if (allRects[i] == cardRect) continue;
                layoutRects[rectIndex] = allRects[i];
                baseAnchoredPositions[rectIndex] = allRects[i].anchoredPosition;
                baseSizeDeltas[rectIndex] = allRects[i].sizeDelta;
                rectIndex++;
            }

            layoutTexts = GetComponentsInChildren<TextMeshProUGUI>(true);
            baseFontSizes = new float[layoutTexts.Length];
            baseFontSizeMins = new float[layoutTexts.Length];
            baseFontSizeMaxes = new float[layoutTexts.Length];
            for (int i = 0; i < layoutTexts.Length; i++)
            {
                baseFontSizes[i] = layoutTexts[i].fontSize;
                baseFontSizeMins[i] = layoutTexts[i].fontSizeMin;
                baseFontSizeMaxes[i] = layoutTexts[i].fontSizeMax;
            }
        }

        private void HandlePerformClicked()
        {
            OutgameRequestOffer offer = BoundOffer;
            if (offer != null) PerformRequested?.Invoke(offer);
        }

        private void ResetDisplay()
        {
            BoundOffer = null;
            requestTypeLabel.text = string.Empty;
            requesterNameLabel.text = string.Empty;
            titleLabel.text = string.Empty;
            difficultyLabel.text = "난이도:";
            descriptionTitleLabel.text = "의뢰 내용:";
            descriptionLabel.text = string.Empty;
            performButtonLabel.text = "수행하기";
            performButton.interactable = false;
            portraitPlaceholder.sprite = null;
            portraitPlaceholder.color = new Color32(208, 192, 168, 255);
            portraitPlaceholder.raycastTarget = false;
            clipboardBackground.raycastTarget = false;

            for (int i = 0; i < difficultyStars.Length; i++)
            {
                difficultyStars[i].sprite = inactiveStarSprite;
                difficultyStars[i].raycastTarget = false;
            }
            for (int i = 0; i < effectSlots.Length; i++)
                effectSlots[i]?.ResetDisplay();
        }

        private static int GetActiveStarCount(GameDifficulty difficulty)
        {
            switch (difficulty)
            {
                case GameDifficulty.Easy: return 1;
                case GameDifficulty.Normal: return 2;
                case GameDifficulty.Hard: return 3;
                default: return 0;
            }
        }
    }
}
