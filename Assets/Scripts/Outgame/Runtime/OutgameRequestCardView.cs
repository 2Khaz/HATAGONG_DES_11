using System;
using HATAGONG.GameFlow;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HATAGONG.Outgame
{
    public sealed class OutgameRequestCardView : MonoBehaviour
    {
        [Serializable]
        private sealed class SpriteKeyBinding
        {
            [SerializeField] private string key;
            [SerializeField] private Sprite sprite;

            public string Key => key;
            public Sprite Sprite => sprite;
        }

        [Serializable]
        private sealed class EffectSlotView
        {
            [SerializeField] private Image background;
            [SerializeField] private Image iconPlaceholder;
            [SerializeField] private TextMeshProUGUI effectNameLabel;

            public bool IsFilled { get; private set; }
            public string EffectName => effectNameLabel == null ? string.Empty : effectNameLabel.text;
            public RectTransform GetRoot() => background == null ? null : background.rectTransform.parent as RectTransform;

            public void ApplyReferenceLayout(Vector2 anchorMin, Vector2 anchorMax)
            {
                SetNormalizedRect(background == null ? null : background.rectTransform, anchorMin, anchorMax);
                SetNormalizedRect(iconPlaceholder == null ? null : iconPlaceholder.rectTransform,
                    new Vector2(0.05f, 0.02f), new Vector2(0.95f, 0.76f));
                SetNormalizedRect(effectNameLabel == null ? null : effectNameLabel.rectTransform,
                    new Vector2(0f, 0.76f), Vector2.one);

                if (iconPlaceholder != null) iconPlaceholder.preserveAspect = true;
                if (effectNameLabel != null)
                {
                    effectNameLabel.alignment = TextAlignmentOptions.Center;
                    effectNameLabel.textWrappingMode = TextWrappingModes.NoWrap;
                    effectNameLabel.enableAutoSizing = true;
                }
            }

            public void ApplyAccentStyle(TMP_FontAsset font, float titleFontSize)
            {
                if (effectNameLabel == null || font == null) return;
                effectNameLabel.font = font;
                effectNameLabel.fontStyle = FontStyles.Bold;
                effectNameLabel.fontSize = titleFontSize;
                effectNameLabel.fontSizeMin = Mathf.Max(8f, titleFontSize * 0.65f);
                effectNameLabel.fontSizeMax = titleFontSize;
            }

            public void ResetDisplay(Sprite emptySprite)
            {
                IsFilled = false;
                if (background != null)
                {
                    background.color = Color.clear;
                    background.raycastTarget = false;
                }
                if (iconPlaceholder != null)
                {
                    iconPlaceholder.sprite = emptySprite;
                    iconPlaceholder.color = Color.white;
                    iconPlaceholder.preserveAspect = true;
                    iconPlaceholder.raycastTarget = false;
                    iconPlaceholder.gameObject.SetActive(true);
                }
                if (effectNameLabel != null)
                {
                    effectNameLabel.text = string.Empty;
                    effectNameLabel.gameObject.SetActive(false);
                }
            }

            public void Bind(OutgameRequestEffectDefinition definition, Sprite iconSprite, Sprite emptySprite)
            {
                ResetDisplay(emptySprite);
                if (definition == null) return;
                IsFilled = true;
                if (iconPlaceholder != null)
                {
                    iconPlaceholder.sprite = iconSprite;
                    iconPlaceholder.color = Color.white;
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
        [SerializeField] private SpriteKeyBinding[] portraitSpriteBindings = Array.Empty<SpriteKeyBinding>();
        [SerializeField] private SpriteKeyBinding[] effectSpriteBindings = Array.Empty<SpriteKeyBinding>();
        [SerializeField] private Sprite emptyEffectSprite;
        [SerializeField] private Texture2D performButtonTexture;
        [SerializeField] private TMP_FontAsset accentFont;
        [SerializeField] private Button performButton;
        [SerializeField] private TextMeshProUGUI performButtonLabel;

        private RectTransform cardRect;
        private RectTransform[] layoutRects = Array.Empty<RectTransform>();
        private Vector2[] baseAnchoredPositions = Array.Empty<Vector2>();
        private Vector2[] baseSizeDeltas = Array.Empty<Vector2>();
        private TextMeshProUGUI[] layoutTexts = Array.Empty<TextMeshProUGUI>();
        private float[] baseFontSizes = Array.Empty<float>();
        private float[] baseFontSizeMins = Array.Empty<float>();
        private float[] baseFontSizeMaxes = Array.Empty<float>();
        private Vector2 baseCardSize;
        private Sprite runtimeButtonSprite;

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
            ConfigureButtonSprite();
            ConfigurePortraitFrame();
            ApplyReferenceLayout();
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
            if (runtimeButtonSprite != null)
            {
                if (Application.isPlaying) Destroy(runtimeButtonSprite);
                else DestroyImmediate(runtimeButtonSprite);
            }
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

            portraitPlaceholder.sprite = ResolveSprite(
                portraitSpriteBindings,
                definition.PortraitKey,
                "portrait");
            portraitPlaceholder.color = Color.white;
            portraitPlaceholder.preserveAspect = true;

            int activeStars = GetActiveStarCount(definition.Difficulty);
            for (int i = 0; i < difficultyStars.Length; i++)
                difficultyStars[i].sprite = i < activeStars ? activeStarSprite : inactiveStarSprite;

            int effectCount = Math.Min(effectSlots.Length, definition.Effects.Count);
            for (int i = 0; i < effectCount; i++)
            {
                OutgameRequestEffectDefinition effect = definition.Effects[i];
                Sprite effectSprite = ResolveSprite(effectSpriteBindings, effect.EffectIconKey, "effect");
                effectSlots[i].Bind(effect, effectSprite, emptyEffectSprite);
            }
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

        public void ApplyReferenceLayout()
        {
            RectTransform root = transform as RectTransform;
            if (root == null) throw new InvalidOperationException("Request Card requires a RectTransform.");
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;

            SetNormalizedRect(clipboardBackground == null ? null : clipboardBackground.rectTransform,
                Vector2.zero, Vector2.one);
            SetNormalizedRect(requestTypeLabel == null ? null : requestTypeLabel.rectTransform,
                new Vector2(0.12f, 0.825f), new Vector2(0.88f, 0.915f));
            SetNormalizedRect(portraitPlaceholder == null ? null : portraitPlaceholder.rectTransform,
                new Vector2(0.075f, 0.575f), new Vector2(0.365f, 0.835f));
            SetNormalizedRect(requesterNameLabel == null ? null : requesterNameLabel.rectTransform,
                new Vector2(0.39f, 0.755f), new Vector2(0.92f, 0.83f));
            SetNormalizedRect(titleLabel == null ? null : titleLabel.rectTransform,
                new Vector2(0.39f, 0.655f), new Vector2(0.92f, 0.755f));
            SetNormalizedRect(difficultyLabel == null ? null : difficultyLabel.rectTransform,
                new Vector2(0.39f, 0.585f), new Vector2(0.56f, 0.655f));
            SetNormalizedRect(descriptionTitleLabel == null ? null : descriptionTitleLabel.rectTransform,
                new Vector2(0.075f, 0.49f), new Vector2(0.35f, 0.555f));
            SetNormalizedRect(descriptionLabel == null ? null : descriptionLabel.rectTransform,
                new Vector2(0.075f, 0.355f), new Vector2(0.925f, 0.495f));

            RectTransform starsRoot = difficultyStars.Length == 0 || difficultyStars[0] == null
                ? null
                : difficultyStars[0].rectTransform.parent as RectTransform;
            SetNormalizedRect(starsRoot, new Vector2(0.56f, 0.565f), new Vector2(0.92f, 0.66f));
            for (int i = 0; i < difficultyStars.Length; i++)
            {
                float left = i / 3f + 0.03f;
                float right = (i + 1f) / 3f - 0.03f;
                SetNormalizedRect(difficultyStars[i] == null ? null : difficultyStars[i].rectTransform,
                    new Vector2(left, 0.08f), new Vector2(right, 0.92f));
                if (difficultyStars[i] != null) difficultyStars[i].preserveAspect = true;
            }

            RectTransform effectsRoot = effectSlots.Length == 0 || effectSlots[0] == null
                ? null
                : effectSlots[0].GetRoot();
            SetNormalizedRect(effectsRoot, new Vector2(0.075f, 0.16f), new Vector2(0.925f, 0.35f));
            Vector2[] slotMins =
            {
                new Vector2(0f, 0f), new Vector2(0.35f, 0f), new Vector2(0.70f, 0f)
            };
            Vector2[] slotMaxes =
            {
                new Vector2(0.30f, 1f), new Vector2(0.65f, 1f), new Vector2(1f, 1f)
            };
            for (int i = 0; i < effectSlots.Length && i < 3; i++)
                effectSlots[i]?.ApplyReferenceLayout(slotMins[i], slotMaxes[i]);

            SetNormalizedRect(performButton == null ? null : performButton.transform as RectTransform,
                new Vector2(0.18f, 0.07f), new Vector2(0.82f, 0.155f));
            SetNormalizedRect(performButtonLabel == null ? null : performButtonLabel.rectTransform,
                Vector2.zero, Vector2.one);

            if (clipboardBackground != null)
            {
                clipboardBackground.preserveAspect = true;
                clipboardBackground.raycastTarget = false;
            }
            if (portraitPlaceholder != null)
            {
                portraitPlaceholder.preserveAspect = true;
                portraitPlaceholder.raycastTarget = false;
            }
            if (performButton != null && performButton.targetGraphic is Image buttonImage)
                buttonImage.preserveAspect = true;

            ConfigureText(requestTypeLabel, TextAlignmentOptions.Center, false);
            ConfigureText(requesterNameLabel, TextAlignmentOptions.Left, false);
            ConfigureText(titleLabel, TextAlignmentOptions.Left, true);
            ConfigureText(difficultyLabel, TextAlignmentOptions.Left, false);
            ConfigureText(descriptionTitleLabel, TextAlignmentOptions.Left, false);
            ConfigureText(descriptionLabel, TextAlignmentOptions.TopLeft, true);
            ConfigureText(performButtonLabel, TextAlignmentOptions.Center, false);

            ApplyAccentStyle(requestTypeLabel);
            ApplyAccentStyle(titleLabel);
            float titleFontSize = titleLabel == null ? 20f : titleLabel.fontSize;
            for (int i = 0; i < effectSlots.Length; i++)
                effectSlots[i]?.ApplyAccentStyle(accentFont, titleFontSize);
        }

        private void ApplyAccentStyle(TextMeshProUGUI text)
        {
            if (text == null || accentFont == null) return;
            text.font = accentFont;
            text.fontStyle = FontStyles.Bold;
        }

        private static void ConfigureText(TextMeshProUGUI text, TextAlignmentOptions alignment, bool wrap)
        {
            if (text == null) return;
            text.alignment = alignment;
            text.enableAutoSizing = true;
            text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
        }

        private static void SetNormalizedRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            if (rect == null) return;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
        }

        private static Sprite ResolveSprite(SpriteKeyBinding[] bindings, string key, string role)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new InvalidOperationException($"Request {role} key is empty.");
            for (int i = 0; i < bindings.Length; i++)
            {
                SpriteKeyBinding binding = bindings[i];
                if (binding != null && string.Equals(binding.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    if (binding.Sprite == null)
                        throw new InvalidOperationException($"Request {role} sprite is missing for key '{key}'.");
                    return binding.Sprite;
                }
            }
            if (string.Equals(role, "effect", StringComparison.Ordinal))
            {
                Sprite resourceSprite = Resources.Load<Sprite>("Outgame/Request/" + key);
                if (resourceSprite != null) return resourceSprite;
            }
            throw new InvalidOperationException($"Request {role} sprite binding was not found for key '{key}'.");
        }

        private void ConfigureButtonSprite()
        {
            if (performButton == null || !(performButton.targetGraphic is Image buttonImage)) return;
            if (buttonImage.sprite != null || performButtonTexture == null) return;
            runtimeButtonSprite = Sprite.Create(
                performButtonTexture,
                new Rect(0f, 0f, performButtonTexture.width, performButtonTexture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            runtimeButtonSprite.name = performButtonTexture.name + "_RuntimeSprite";
            buttonImage.sprite = runtimeButtonSprite;
        }

        private void ConfigurePortraitFrame()
        {
            if (portraitPlaceholder == null) return;
            Outline outline = portraitPlaceholder.GetComponent<Outline>();
            if (outline == null) outline = portraitPlaceholder.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = true;
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
            performButtonLabel.text = "의뢰 수락";
            performButton.interactable = false;
            portraitPlaceholder.sprite = null;
            portraitPlaceholder.color = Color.white;
            portraitPlaceholder.preserveAspect = true;
            portraitPlaceholder.raycastTarget = false;
            clipboardBackground.raycastTarget = false;

            for (int i = 0; i < difficultyStars.Length; i++)
            {
                difficultyStars[i].sprite = inactiveStarSprite;
                difficultyStars[i].raycastTarget = false;
            }
            for (int i = 0; i < effectSlots.Length; i++)
                effectSlots[i]?.ResetDisplay(emptyEffectSprite);
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
