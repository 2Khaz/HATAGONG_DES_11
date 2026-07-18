using HATAGONG.GameFlow;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace HATAGONG.Outgame
{
    [DisallowMultipleComponent]
    public sealed class OutgameLobbyProgressQuitController : MonoBehaviour
    {
        private const float DefaultQuitAlpha = 0.45f;
        private const float ActiveQuitAlpha = 1f;

        [SerializeField] private Sprite topUiSprite = null;
        [SerializeField] private Sprite logoutIconSprite = null;
        [SerializeField] private Sprite logoutConfirmSprite = null;
        [SerializeField] private TMP_FontAsset lobbyFont = null;

        private RectTransform _canvasRect;
        private RectTransform _safeAreaRoot;
        private RectTransform _progressRoot;
        private RectTransform _topUiBackground;
        private RectTransform _quitRoot;
        private RectTransform _quitConfirmRoot;
        private RectTransform _confirmPanel;
        private GameObject _inputBlocker;
        private Button _quitButton;
        private Button _yesButton;
        private Button _noButton;
        private LobbyTextButtonStateRelay _yesTextState;
        private LobbyTextButtonStateRelay _noTextState;
        private CanvasGroup _quitCanvasGroup;
        private TMP_Text _rankText;
        private TMP_Text _stageProgressText;
        private bool _hierarchyBuilt;
        private bool _popupOpen;
        private bool _popupActionConsumed;
        private bool _quitRequested;
        private int _lastScreenWidth = -1;
        private int _lastScreenHeight = -1;
        private Rect _lastSafeArea = new Rect(float.NaN, float.NaN, float.NaN, float.NaN);
        private bool _layoutDirty = true;

        public bool IsQuitPopupOpen => _popupOpen;

        private void Awake()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (!canvas || !topUiSprite || !logoutIconSprite || !logoutConfirmSprite || !lobbyFont)
            {
                Debug.LogError("[Outgame][Lobby] Progress/Quit UI references are incomplete.", this);
                enabled = false;
                return;
            }

            _canvasRect = canvas.GetComponent<RectTransform>();
            BuildHierarchy();
            RefreshProgress();
            CloseQuitPopup();
        }

        private void OnEnable()
        {
            if (!_hierarchyBuilt) return;
            RefreshProgress();
            _layoutDirty = true;
        }

        private void Start()
        {
            ApplyResponsiveLayout(true);
        }

        private void Update()
        {
            Rect safeArea = Screen.safeArea;
            if (_lastScreenWidth != Screen.width || _lastScreenHeight != Screen.height || safeArea != _lastSafeArea)
                _layoutDirty = true;

            if (_layoutDirty) ApplyResponsiveLayout(false);
        }

        private void OnRectTransformDimensionsChange()
        {
            if (_hierarchyBuilt) _layoutDirty = true;
        }

        public void OpenQuitPopup()
        {
            if (!_hierarchyBuilt || _popupOpen || _quitRequested) return;
            _popupOpen = true;
            _popupActionConsumed = false;
            _inputBlocker.SetActive(true);
            _quitConfirmRoot.gameObject.SetActive(true);
            _quitButton.interactable = false;
            _quitCanvasGroup.alpha = ActiveQuitAlpha;
            GameSfxPlayer.Play(GameSfxId.Logout);
        }

        public void NotifyQuitPointerActive()
        {
            if (!_hierarchyBuilt) return;
            _quitCanvasGroup.alpha = ActiveQuitAlpha;
        }

        public void NotifyQuitPointerInactive()
        {
            if (!_hierarchyBuilt) return;
            _quitCanvasGroup.alpha = _popupOpen ? ActiveQuitAlpha : DefaultQuitAlpha;
        }

        private void ConfirmQuit()
        {
            if (!_popupOpen || _popupActionConsumed || _quitRequested) return;
            _popupActionConsumed = true;
            _quitRequested = true;
            _yesButton.interactable = false;
            _noButton.interactable = false;
            _yesTextState.SetInteractable(false);
            _noTextState.SetInteractable(false);
            ExecuteQuit();
        }

        private static void ExecuteQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void CancelQuit()
        {
            if (!_popupOpen || _popupActionConsumed || _quitRequested) return;
            GameSfxPlayer.Play(GameSfxId.Click);
            _popupActionConsumed = true;
            CloseQuitPopup();
        }

        private void CloseQuitPopup()
        {
            if (!_hierarchyBuilt) return;
            _popupOpen = false;
            _popupActionConsumed = false;
            _inputBlocker.SetActive(false);
            _quitConfirmRoot.gameObject.SetActive(false);
            _yesButton.interactable = true;
            _noButton.interactable = true;
            _yesTextState.SetInteractable(true);
            _noTextState.SetInteractable(true);
            _quitButton.interactable = !_quitRequested;
            _quitCanvasGroup.alpha = DefaultQuitAlpha;
        }

        private void RefreshProgress()
        {
            PlayerRankProgress progress = PlayerRankProgress.Evaluate(PlayerProgressRepository.GetClearedStageCount());
            _rankText.text = progress.RankName;
            _stageProgressText.text = progress.ProgressDisplayText;
        }

        private void BuildHierarchy()
        {
            _inputBlocker = CreateRect("QuitInputBlocker", _canvasRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f)).gameObject;
            Image blockerImage = _inputBlocker.AddComponent<Image>();
            blockerImage.color = Color.clear;
            blockerImage.raycastTarget = true;

            _safeAreaRoot = CreateRect("LobbySafeAreaRoot", _canvasRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));

            _progressRoot = CreateRect("LobbyProgressRoot", _safeAreaRoot, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            _topUiBackground = CreateRect("TopUiBackground", _progressRoot, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            Image topImage = _topUiBackground.gameObject.AddComponent<Image>();
            topImage.sprite = topUiSprite;
            topImage.type = Image.Type.Simple;
            topImage.preserveAspect = true;
            topImage.raycastTarget = false;

            // Reference-aligned text bounds converted from the non-transparent artwork area
            // of Img_topui (1774x887, alpha bounds approximately x=48..1758, y=166..698).
            _rankText = CreateText("RankText", _topUiBackground, new Vector2(0.54f, 0.66f), new Vector2(0.61f, 0.745f));
            _stageProgressText = CreateText("StageProgressText", _topUiBackground, new Vector2(0.56f, 0.35f), new Vector2(0.91f, 0.55f));
            _stageProgressText.fontSizeMax = 160f;

            _quitRoot = CreateRect("LobbyQuitRoot", _safeAreaRoot, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f));
            Image quitHitTarget = _quitRoot.gameObject.AddComponent<Image>();
            quitHitTarget.color = Color.clear;
            quitHitTarget.raycastTarget = true;
            _quitButton = _quitRoot.gameObject.AddComponent<Button>();
            _quitButton.transition = Selectable.Transition.None;
            _quitButton.targetGraphic = quitHitTarget;
            _quitButton.onClick.AddListener(OpenQuitPopup);
            _quitCanvasGroup = _quitRoot.gameObject.AddComponent<CanvasGroup>();
            _quitCanvasGroup.alpha = DefaultQuitAlpha;
            LobbyQuitPointerRelay relay = _quitRoot.gameObject.AddComponent<LobbyQuitPointerRelay>();
            relay.Configure(this);

            RectTransform quitIcon = CreateRect("QuitIcon", _quitRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            Image quitIconImage = quitIcon.gameObject.AddComponent<Image>();
            quitIconImage.sprite = logoutIconSprite;
            quitIconImage.type = Image.Type.Simple;
            quitIconImage.preserveAspect = true;
            quitIconImage.raycastTarget = false;
            quitIcon.anchorMin = new Vector2(0.05f, 0.05f);
            quitIcon.anchorMax = new Vector2(0.95f, 0.95f);
            quitIcon.offsetMin = quitIcon.offsetMax = Vector2.zero;

            _quitConfirmRoot = CreateRect("QuitConfirmRoot", _safeAreaRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            _confirmPanel = CreateRect("ConfirmPanel", _quitConfirmRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            Image panelImage = _confirmPanel.gameObject.AddComponent<Image>();
            panelImage.sprite = logoutConfirmSprite;
            panelImage.type = Image.Type.Simple;
            panelImage.preserveAspect = true;
            panelImage.raycastTarget = false;

            // Img_logoutyesno has a transparent header (alpha begins near source y=102).
            // These anchors map the reference text bounds back into the full 1410x645 sprite.
            TMP_Text message = CreateText("MessageText", _confirmPanel, new Vector2(0.24f, 0.485f), new Vector2(0.75f, 0.64f));
            message.text = "종료하시겠습니까?";
            _yesButton = CreateTextButton(
                "YesButton", "예", _confirmPanel,
                new Vector2(0.07f, 0.09f), new Vector2(0.48f, 0.45f),
                new Vector2(0.537f, 0.389f), new Vector2(0.683f, 0.722f),
                out _yesTextState);
            _noButton = CreateTextButton(
                "NoButton", "아니오", _confirmPanel,
                new Vector2(0.52f, 0.09f), new Vector2(0.93f, 0.45f),
                new Vector2(0.195f, 0.389f), new Vector2(0.610f, 0.722f),
                out _noTextState);
            _yesButton.onClick.AddListener(ConfirmQuit);
            _noButton.onClick.AddListener(CancelQuit);

            _inputBlocker.transform.SetSiblingIndex(_safeAreaRoot.GetSiblingIndex());
            _safeAreaRoot.SetAsLastSibling();
            _hierarchyBuilt = true;
        }

        private TMP_Text CreateText(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            RectTransform rect = CreateRect(name, parent, anchorMin, anchorMax, new Vector2(0.5f, 0.5f));
            TMP_Text text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = lobbyFont;
            text.fontStyle = FontStyles.Normal;
            text.color = new Color32(20, 34, 54, 255);
            text.alignment = TextAlignmentOptions.Center;
            text.enableAutoSizing = true;
            text.fontSizeMin = 12f;
            text.fontSizeMax = 96f;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private Button CreateTextButton(
            string name,
            string label,
            RectTransform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 textAnchorMin,
            Vector2 textAnchorMax,
            out LobbyTextButtonStateRelay textState)
        {
            RectTransform rect = CreateRect(name, parent, anchorMin, anchorMax, new Vector2(0.5f, 0.5f));
            Image hitTarget = rect.gameObject.AddComponent<Image>();
            hitTarget.color = Color.clear;
            hitTarget.raycastTarget = true;
            Button button = rect.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = hitTarget;
            TMP_Text text = CreateText(name == "YesButton" ? "YesText" : "NoText", rect, textAnchorMin, textAnchorMax);
            text.text = label;
            textState = rect.gameObject.AddComponent<LobbyTextButtonStateRelay>();
            textState.Configure(button, text);
            return button;
        }

        private void ApplyResponsiveLayout(bool force)
        {
            if (!_hierarchyBuilt || !_canvasRect) return;

            Rect safeArea = Screen.safeArea;
            int screenWidth = Mathf.Max(1, Screen.width);
            int screenHeight = Mathf.Max(1, Screen.height);
            if (!force && !_layoutDirty && _lastScreenWidth == screenWidth && _lastScreenHeight == screenHeight && safeArea == _lastSafeArea) return;

            _lastScreenWidth = screenWidth;
            _lastScreenHeight = screenHeight;
            _lastSafeArea = safeArea;
            _layoutDirty = false;

            _safeAreaRoot.anchorMin = new Vector2(safeArea.xMin / screenWidth, safeArea.yMin / screenHeight);
            _safeAreaRoot.anchorMax = new Vector2(safeArea.xMax / screenWidth, safeArea.yMax / screenHeight);
            _safeAreaRoot.offsetMin = _safeAreaRoot.offsetMax = Vector2.zero;
            Canvas.ForceUpdateCanvases();

            float safeWidth = _safeAreaRoot.rect.width;
            float safeHeight = _safeAreaRoot.rect.height;
            if (safeWidth <= 0f || safeHeight <= 0f) return;

            float topAspect = Mathf.Max(0.01f, topUiSprite.rect.width / topUiSprite.rect.height);
            float topWidth = safeWidth * 0.54f;
            float topHeight = topWidth / topAspect;
            if (topHeight > safeHeight * 0.18f)
            {
                topHeight = safeHeight * 0.18f;
                topWidth = topHeight * topAspect;
            }
            _progressRoot.anchoredPosition = new Vector2(safeWidth * 0.03f, -safeHeight * 0.025f);
            _progressRoot.sizeDelta = new Vector2(topWidth, topHeight);
            _topUiBackground.sizeDelta = _progressRoot.sizeDelta;

            float quitSize = Mathf.Min(safeWidth * 0.11f, safeHeight * 0.08f);
            _quitRoot.anchoredPosition = new Vector2(-safeWidth * 0.035f, safeHeight * 0.025f);
            _quitRoot.sizeDelta = new Vector2(quitSize, quitSize);

            float popupAspect = Mathf.Max(0.01f, logoutConfirmSprite.rect.width / logoutConfirmSprite.rect.height);
            float popupWidth = safeWidth * 0.76f;
            float popupHeight = popupWidth / popupAspect;
            float maximumHeight = safeHeight * 0.40f;
            if (popupHeight > maximumHeight)
            {
                popupHeight = maximumHeight;
                popupWidth = popupHeight * popupAspect;
            }
            _quitConfirmRoot.sizeDelta = new Vector2(popupWidth, popupHeight);
            _confirmPanel.sizeDelta = _quitConfirmRoot.sizeDelta;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.layer = parent.gameObject.layer;
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            return rect;
        }
    }

    internal sealed class LobbyQuitPointerRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private OutgameLobbyProgressQuitController _owner;

        public void Configure(OutgameLobbyProgressQuitController owner)
        {
            _owner = owner;
        }

        public void OnPointerEnter(PointerEventData eventData) => _owner?.NotifyQuitPointerActive();
        public void OnPointerExit(PointerEventData eventData) => _owner?.NotifyQuitPointerInactive();
        public void OnPointerDown(PointerEventData eventData) => _owner?.NotifyQuitPointerActive();
        public void OnPointerUp(PointerEventData eventData) => _owner?.NotifyQuitPointerInactive();

        private void OnDisable()
        {
            _owner?.NotifyQuitPointerInactive();
        }
    }

    internal sealed class LobbyTextButtonStateRelay : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        ISelectHandler,
        IDeselectHandler
    {
        private static readonly Color32 NormalColor = new Color32(0x77, 0x77, 0x77, 0xFF);
        private static readonly Color32 FocusedColor = new Color32(0x22, 0x22, 0x22, 0xFF);
        private static readonly Color32 PressedColor = new Color32(0xD9, 0x43, 0x43, 0xFF);
        private static readonly Color32 DisabledColor = new Color32(0xAA, 0xAA, 0xAA, 0xFF);

        private Button _button;
        private TMP_Text _text;
        private bool _mouseHover;
        private bool _focused;
        private bool _pressed;
        private bool _lastPointerWasTouch;

        public void Configure(Button button, TMP_Text text)
        {
            _button = button;
            _text = text;
            ResetState();
        }

        public void SetInteractable(bool interactable)
        {
            if (_button) _button.interactable = interactable;
            RefreshColor();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (IsTouch(eventData)) return;
            _mouseHover = true;
            RefreshColor();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!IsTouch(eventData)) _mouseHover = false;
            if (!_pressed) RefreshColor();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _lastPointerWasTouch = IsTouch(eventData);
            _pressed = true;
            RefreshColor();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pressed = false;
            if (IsTouch(eventData)) _mouseHover = false;
            RefreshColor();
        }

        public void OnSelect(BaseEventData eventData)
        {
            _focused = true;
            RefreshColor();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            _focused = false;
            RefreshColor();
        }

        private void OnDisable()
        {
            ResetState();
        }

        private void ResetState()
        {
            _mouseHover = false;
            _focused = false;
            _pressed = false;
            _lastPointerWasTouch = false;
            RefreshColor();
        }

        private void RefreshColor()
        {
            if (!_text) return;
            if (!_button || !_button.IsInteractable()) _text.color = DisabledColor;
            else if (_pressed) _text.color = PressedColor;
            else if (_mouseHover || (_focused && !_lastPointerWasTouch)) _text.color = FocusedColor;
            else _text.color = NormalColor;
        }

        private static bool IsTouch(PointerEventData eventData)
        {
            if (eventData == null) return false;
#if ENABLE_INPUT_SYSTEM
            if (eventData is ExtendedPointerEventData extended)
                return extended.pointerType == UIPointerType.Touch;
#endif
            return eventData.pointerId >= 0;
        }
    }
}
