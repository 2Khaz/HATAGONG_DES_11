using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HATAGONG.GameFlow
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class IngameSettingsMenuController : MonoBehaviour
    {
        private const float ButtonSpacingMultiplier = 1.1f;
        private const float ItemDuration = 0.22f;
        private const float ItemDelay = 0.06f;

        private enum MenuState { Closed, Opening, Open, Closing, Loading }

        private sealed class OptionView
        {
            public RectTransform Rect;
            public CanvasGroup Group;
            public Button Button;
            public GameObject OffMark;
            public Vector2 BasePosition;
            public float TargetOffset;
        }

        [SerializeField] private GameSessionController session;
        [SerializeField] private IdleGameplayGuidePresenter idleGuide;
        [SerializeField] private AudioSource[] bgmSources = System.Array.Empty<AudioSource>();
        [SerializeField] private Sprite buttonBackground;
        [SerializeField] private Sprite settingsIcon;
        [SerializeField] private Sprite exitIcon;
        [SerializeField] private Sprite bgmIcon;
        [SerializeField] private Sprite sfxIcon;
        [SerializeField] private Sprite vibrationIcon;
        [SerializeField] private Sprite offIcon;
        [SerializeField] private TMP_FontAsset pauseFont;

        private readonly List<OptionView> options = new List<OptionView>(4);
        private Button settingsButton;
        private Image settingsIconImage;
        private GameObject runtimeRoot;
        private GameObject blocker;
        private GameObject pauseMessage;
        private OptionView exitView;
        private OptionView bgmView;
        private OptionView sfxView;
        private OptionView vibrationView;
        private Coroutine animationRoutine;
        private MenuState state;
        private float timeScaleBeforeOpen = 1f;
        private bool ownsTimeScalePause;

        public bool IsOpen => state == MenuState.Open || state == MenuState.Opening;
        public bool IsAnimating => state == MenuState.Opening || state == MenuState.Closing;

        private void Awake()
        {
            if (!session) session = FindAnyObjectByType<GameSessionController>();
            if (!idleGuide) idleGuide = FindAnyObjectByType<IdleGameplayGuidePresenter>();
            DiscoverLoopingBgmSources();
            if (!BuildRuntimeHierarchy())
            {
                enabled = false;
                return;
            }

            settingsButton.onClick.AddListener(OnSettingsClicked);
            exitView.Button.onClick.AddListener(OnExitClicked);
            bgmView.Button.onClick.AddListener(OnBgmClicked);
            sfxView.Button.onClick.AddListener(OnSfxClicked);
            vibrationView.Button.onClick.AddListener(OnVibrationClicked);
            IngameOptionPreferences.Changed += RefreshOptionState;
            ApplyBgmState();
            RefreshOptionState();
            SetClosedImmediate();
        }

        private void Update()
        {
            if (state == MenuState.Closed && settingsButton)
                SetSettingsInteractable(session && session.CanOpenSettings);
        }

        private void OnDestroy()
        {
            IngameOptionPreferences.Changed -= RefreshOptionState;
            RestoreTimeScaleIfOwned();
        }

        private void OnSettingsClicked()
        {
            if (state == MenuState.Closed) { GameSfxPlayer.Play(GameSfxId.Click); Open(); }
            else if (state == MenuState.Open) { GameSfxPlayer.Play(GameSfxId.Click); Close(); }
        }

        private void Open()
        {
            if (!session || !session.TryPauseForSettings()) return;
            timeScaleBeforeOpen = Time.timeScale;
            ownsTimeScalePause = true;
            Time.timeScale = 0f;
            idleGuide?.SetExternalUiBlocked(true);
            blocker.SetActive(true);
            pauseMessage.SetActive(true);
            SetSettingsInteractable(false);
            SetOptionButtonsInteractable(false);
            state = MenuState.Opening;
            animationRoutine = StartCoroutine(Animate(opening: true));
        }

        private void Close()
        {
            state = MenuState.Closing;
            SetSettingsInteractable(false);
            SetOptionButtonsInteractable(false);
            animationRoutine = StartCoroutine(Animate(opening: false));
        }

        private IEnumerator Animate(bool opening)
        {
            float elapsed = 0f;
            float total = ItemDuration + ItemDelay * (options.Count - 1);
            while (elapsed < total)
            {
                elapsed += Time.unscaledDeltaTime;
                for (int i = 0; i < options.Count; i++)
                {
                    int order = opening ? i : options.Count - 1 - i;
                    float local = Mathf.Clamp01((elapsed - ItemDelay * order) / ItemDuration);
                    float progress = opening ? EaseOutBack(local) : 1f - EaseInCubic(local);
                    ApplyOptionProgress(options[i], progress);
                }
                yield return null;
            }

            for (int i = 0; i < options.Count; i++) ApplyOptionProgress(options[i], opening ? 1f : 0f);
            animationRoutine = null;
            if (opening)
            {
                state = MenuState.Open;
                SetSettingsInteractable(true);
                SetOptionButtonsInteractable(true);
                yield break;
            }

            if (!session || !session.TryResumeFromSettings())
            {
                Debug.LogError("[GameFlow][Settings] Session refused to resume; keeping the settings lock active.", this);
                for (int i = 0; i < options.Count; i++) ApplyOptionProgress(options[i], 1f);
                state = MenuState.Open;
                SetSettingsInteractable(true);
                SetOptionButtonsInteractable(true);
                yield break;
            }

            RestoreTimeScaleIfOwned();
            idleGuide?.SetExternalUiBlocked(false);
            blocker.SetActive(false);
            pauseMessage.SetActive(false);
            for (int i = 0; i < options.Count; i++) options[i].Rect.gameObject.SetActive(false);
            state = MenuState.Closed;
            SetSettingsInteractable(session.CanOpenSettings);
        }

        private void OnExitClicked()
        {
            if (state != MenuState.Open || !session) return;
            GameSfxPlayer.Play(GameSfxId.Click);
            state = MenuState.Loading;
            SetSettingsInteractable(false);
            SetOptionButtonsInteractable(false);
            RestoreTimeScaleIfOwned();
            if (session.TryExitToOutgameFromSettings()) return;

            timeScaleBeforeOpen = Time.timeScale;
            ownsTimeScalePause = true;
            Time.timeScale = 0f;
            state = MenuState.Open;
            SetSettingsInteractable(true);
            SetOptionButtonsInteractable(true);
            Debug.LogError("[GameFlow][Settings] OUTGAME_LOBBY load was rejected; settings remain open and gameplay remains locked.", this);
        }

        private void OnBgmClicked()
        {
            if (state != MenuState.Open) return;
            GameSfxPlayer.Play(GameSfxId.Click);
            IngameOptionPreferences.SetBgmEnabled(!IngameOptionPreferences.BgmEnabled);
            ApplyBgmState();
        }

        private void OnSfxClicked()
        {
            if (state != MenuState.Open) return;
            bool enabling = !IngameOptionPreferences.SfxEnabled;
            IngameOptionPreferences.SetSfxEnabled(enabling);
            if (enabling) GameSfxPlayer.Play(GameSfxId.Click);
        }

        private void OnVibrationClicked()
        {
            if (state != MenuState.Open) return;
            GameSfxPlayer.Play(GameSfxId.Click);
            IngameOptionPreferences.SetVibrationEnabled(!IngameOptionPreferences.VibrationEnabled);
        }

        private bool BuildRuntimeHierarchy()
        {
            settingsButton = GetComponent<Button>();
            RectTransform settingsRect = transform as RectTransform;
            RectTransform parent = settingsRect ? settingsRect.parent as RectTransform : null;
            if (!buttonBackground) buttonBackground = Resources.Load<Sprite>("Ingame/UI/Button/Img_button_option 1");
            if (!settingsIcon) settingsIcon = Resources.Load<Sprite>("Ingame/ICON/Img_icon_option");
            if (!exitIcon) exitIcon = Resources.Load<Sprite>("Ingame/ICON/Img_icon_option_out");
            if (!bgmIcon) bgmIcon = Resources.Load<Sprite>("Ingame/ICON/Img_icon_option_bgm");
            if (!sfxIcon) sfxIcon = Resources.Load<Sprite>("Ingame/ICON/Img_icon_option_se");
            if (!vibrationIcon) vibrationIcon = Resources.Load<Sprite>("Ingame/ICON/Img_icon_option_zindong");
            if (!offIcon) offIcon = Resources.Load<Sprite>("Ingame/ICON/Img_icon_opuix");
            if (!pauseFont) pauseFont = Resources.Load<TMP_FontAsset>("Fonts/KERISKEDU_B SDF");
            if (!settingsButton || !settingsRect || !parent || !buttonBackground || !settingsIcon || !exitIcon ||
                !bgmIcon || !sfxIcon || !vibrationIcon || !offIcon || !pauseFont)
            {
                Debug.LogError("[GameFlow][Settings] Required UI references or one of the seven option sprites are missing.", this);
                return false;
            }

            gameObject.name = "SettingsButton";
            Image legacyImage = GetComponent<Image>();
            if (legacyImage) legacyImage.enabled = false;
            FixedImageSprite fixedImage = GetComponent<FixedImageSprite>();
            if (fixedImage) fixedImage.enabled = false;
            Image settingsBackground = CreateImage("Background", settingsRect, buttonBackground, stretch: true, 1f);
            settingsBackground.transform.SetAsFirstSibling();
            settingsBackground.raycastTarget = true;
            settingsButton.targetGraphic = settingsBackground;
            settingsIconImage = ConfigureOrCreateIcon(settingsRect, settingsIcon);

            runtimeRoot = new GameObject("SettingsMenuRuntimeRoot", typeof(RectTransform));
            RectTransform runtimeRect = runtimeRoot.GetComponent<RectTransform>();
            runtimeRect.SetParent(parent, false);
            runtimeRect.SetSiblingIndex(settingsRect.GetSiblingIndex());
            Stretch(runtimeRect);

            blocker = new GameObject("InputBlocker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform blockerRect = blocker.GetComponent<RectTransform>();
            blockerRect.SetParent(runtimeRect, false);
            Stretch(blockerRect);
            Image blockerImage = blocker.GetComponent<Image>();
            blockerImage.color = new Color(0f, 0f, 0f, 0.5f);
            blockerImage.raycastTarget = true;

            pauseMessage = new GameObject("PauseMessage", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            RectTransform pauseRect = pauseMessage.GetComponent<RectTransform>();
            pauseRect.SetParent(runtimeRect, false);
            Stretch(pauseRect);
            TextMeshProUGUI pauseText = pauseMessage.GetComponent<TextMeshProUGUI>();
            pauseText.text = "- PAUSE -";
            pauseText.font = pauseFont;
            pauseText.fontSharedMaterial = pauseFont.material;
            pauseText.fontSize = 120f;
            pauseText.enableAutoSizing = false;
            pauseText.fontStyle = FontStyles.Normal;
            pauseText.color = Color.black;
            pauseText.alignment = TextAlignmentOptions.Center;
            pauseText.textWrappingMode = TextWrappingModes.NoWrap;
            pauseText.raycastTarget = false;

            float spacing = settingsRect.rect.height * ButtonSpacingMultiplier;
            exitView = CreateOption("ExitButton", runtimeRect, settingsRect, buttonBackground, exitIcon, offIcon, spacing * 4f);
            bgmView = CreateOption("BgmButton", runtimeRect, settingsRect, buttonBackground, bgmIcon, offIcon, spacing * 3f);
            sfxView = CreateOption("SfxButton", runtimeRect, settingsRect, buttonBackground, sfxIcon, offIcon, spacing * 2f);
            vibrationView = CreateOption("VibrationButton", runtimeRect, settingsRect, buttonBackground, vibrationIcon, offIcon, spacing);
            options.Add(exitView);
            options.Add(bgmView);
            options.Add(sfxView);
            options.Add(vibrationView);
            return true;
        }

        private static OptionView CreateOption(string name, RectTransform parent, RectTransform template, Sprite background,
            Sprite icon, Sprite offIcon, float targetOffset)
        {
            GameObject root = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup), typeof(Button));
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = template.anchorMin;
            rect.anchorMax = template.anchorMax;
            rect.pivot = template.pivot;
            rect.sizeDelta = template.sizeDelta;
            rect.anchoredPosition = template.anchoredPosition;
            Image backgroundImage = CreateImage("Background", rect, background, stretch: true, 1f);
            backgroundImage.raycastTarget = true;
            CreateImage("Icon", rect, icon, stretch: false, 0.56f);
            Image offMark = CreateImage("OffMark", rect, offIcon, stretch: false, 0.48f);
            offMark.raycastTarget = false;
            Button button = root.GetComponent<Button>();
            button.targetGraphic = backgroundImage;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            return new OptionView
            {
                Rect = rect,
                Group = root.GetComponent<CanvasGroup>(),
                Button = button,
                OffMark = offMark.gameObject,
                BasePosition = template.anchoredPosition,
                TargetOffset = targetOffset
            };
        }

        private static Image CreateImage(string name, RectTransform parent, Sprite sprite, bool stretch, float sizeRatio)
        {
            GameObject child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rect = child.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            if (stretch) Stretch(rect);
            else
            {
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = Vector2.Scale(parent.sizeDelta, new Vector2(sizeRatio, sizeRatio));
                rect.anchoredPosition = Vector2.zero;
            }
            Image image = child.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = !stretch;
            image.raycastTarget = false;
            return image;
        }

        private static Image ConfigureOrCreateIcon(RectTransform parent, Sprite sprite)
        {
            Transform existing = parent.Find("Icon");
            Image image = existing ? existing.GetComponent<Image>() : null;
            if (!image)
            {
                return CreateImage("Icon", parent, sprite, stretch: false, 0.56f);
            }

            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.Scale(parent.sizeDelta, new Vector2(0.56f, 0.56f));
            rect.anchoredPosition = Vector2.zero;
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void SetClosedImmediate()
        {
            state = MenuState.Closed;
            blocker.SetActive(false);
            pauseMessage.SetActive(false);
            for (int i = 0; i < options.Count; i++)
            {
                ApplyOptionProgress(options[i], 0f);
                options[i].Rect.gameObject.SetActive(false);
            }
            SetSettingsInteractable(session && session.CanOpenSettings);
        }

        private void SetSettingsInteractable(bool interactable)
        {
            if (!settingsButton) return;
            settingsButton.interactable = interactable;
            if (!settingsIconImage) return;
            ColorBlock colors = settingsButton.colors;
            settingsIconImage.color = interactable ? colors.normalColor : colors.disabledColor;
        }

        private static void ApplyOptionProgress(OptionView view, float progress)
        {
            if (!view.Rect.gameObject.activeSelf) view.Rect.gameObject.SetActive(true);
            view.Rect.anchoredPosition = view.BasePosition + Vector2.up * (view.TargetOffset * progress);
            view.Rect.localScale = Vector3.one * Mathf.Lerp(0.82f, 1f, progress);
            view.Group.alpha = Mathf.Clamp01(progress);
            view.Group.interactable = progress >= 0.999f;
            view.Group.blocksRaycasts = progress >= 0.999f;
        }

        private void SetOptionButtonsInteractable(bool interactable)
        {
            for (int i = 0; i < options.Count; i++)
            {
                options[i].Button.interactable = interactable;
                options[i].Group.interactable = interactable;
                options[i].Group.blocksRaycasts = interactable;
            }
        }

        private void RefreshOptionState()
        {
            if (bgmView != null) bgmView.OffMark.SetActive(!IngameOptionPreferences.BgmEnabled);
            if (sfxView != null) sfxView.OffMark.SetActive(!IngameOptionPreferences.SfxEnabled);
            if (vibrationView != null) vibrationView.OffMark.SetActive(!IngameOptionPreferences.VibrationEnabled);
            if (exitView != null) exitView.OffMark.SetActive(false);
        }

        private void DiscoverLoopingBgmSources()
        {
            if (bgmSources != null && bgmSources.Length > 0) return;
            AudioSource[] allSources = FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var looping = new List<AudioSource>();
            for (int i = 0; i < allSources.Length; i++) if (allSources[i] && allSources[i].loop) looping.Add(allSources[i]);
            bgmSources = looping.ToArray();
        }

        private void ApplyBgmState()
        {
            bool muted = !IngameOptionPreferences.BgmEnabled;
            if (bgmSources == null) return;
            for (int i = 0; i < bgmSources.Length; i++) if (bgmSources[i]) bgmSources[i].mute = muted;
        }

        private void RestoreTimeScaleIfOwned()
        {
            if (!ownsTimeScalePause) return;
            Time.timeScale = timeScaleBeforeOpen;
            ownsTimeScalePause = false;
        }

        private static float EaseOutBack(float value)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float x = value - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }

        private static float EaseInCubic(float value) => value * value * value;
    }
}
