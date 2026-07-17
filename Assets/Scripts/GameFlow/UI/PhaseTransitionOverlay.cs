using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HATAGONG.GameFlow
{
    public readonly struct GameCompletionSummary
    {
        public GameCompletionSummary(int remainingSeconds, int? acquiredScore, int finalScore)
        {
            RemainingSeconds = Mathf.Max(0, remainingSeconds);
            AcquiredScore = acquiredScore;
            FinalScore = Mathf.Max(0, finalScore);
        }

        public int RemainingSeconds { get; }
        public int? AcquiredScore { get; }
        public int FinalScore { get; }
    }

    public sealed class PhaseTransitionOverlay : MonoBehaviour, IPointerClickHandler
    {
        private const float SuccessEntranceDuration = .52f;
        private const float SuccessClipboardBaseWidth = 760f;
        private const float SuccessClipboardWidth = 1040f;
        private static readonly Vector2 SuccessClipboardStart = new Vector2(0f, -1600f);
        private static readonly Vector2 SuccessClipboardOvershoot = new Vector2(0f, 45f);
        private static readonly Vector2 SuccessClipboardTarget = new Vector2(0f, -20f);

        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform banner;
        [SerializeField] private TextMeshProUGUI messageText;
        [SerializeField] private float enterDuration = .2f;
        [SerializeField] private float holdDuration = .25f;
        [SerializeField] private float exitDuration = .2f;
        [SerializeField] private float completionDelay = .1f;
        [SerializeField] private Sprite defeatCharacterSprite;
        [SerializeField] private Sprite retryButtonSprite;
        [SerializeField] private TMP_FontAsset defeatFont;
        [SerializeField] private Sprite successQuestPanelSprite;
        [SerializeField] private Sprite successGoldPanelSprite;
        [SerializeField] private Sprite successGoldIconSprite;
        [SerializeField] private Sprite successTimeIconSprite;
        [SerializeField] private Sprite successStarIconSprite;

        private Coroutine sequence;
        private Action<PhaseTransitionResult, bool, Exception> finished;
        private bool midpointSucceeded;
        private bool gameCompletionVisible;
        private bool sceneLoadRequested;
        private GameObject defeatResultRoot;
        private Button defeatDimButton;
        private Button defeatRetryButton;
        private Func<bool> retryRequested;
        private Func<bool> lobbyRequested;
        private bool defeatVisible;
        private bool resultNavigationStarted;
        private GameObject successResultRoot;
        private RectTransform successClipboard;
        private Image successInputCatcher;
        private TextMeshProUGUI successRemainingTimeText;
        private TextMeshProUGUI successAcquiredScoreText;
        private TextMeshProUGUI successTotalScoreText;
        private TextMeshProUGUI successGoldRewardText;
        private Coroutine successEntrance;
        private Func<bool> successLobbyRequested;
        private bool successInputEnabled;

        public bool IsPlaying => sequence != null;
        public bool MidpointSucceeded => midpointSucceeded;
        public float EnterDuration => enterDuration;
        public float HoldDuration => holdDuration;
        public float ExitDuration => exitDuration;
        public float CompletionDelay => completionDelay;
        public float TotalConfiguredDuration => enterDuration + holdDuration + exitDuration + completionDelay;
        public int DefeatShowCount { get; private set; }
        public int RetryActionCount { get; private set; }
        public int LobbyActionCount { get; private set; }

        public bool Play(GamePhaseId phase, Func<bool> midpoint, Action<PhaseTransitionResult, bool, Exception> completion)
        {
            if (sequence != null || gameCompletionVisible || defeatVisible) return false;
            gameObject.SetActive(true);
            finished = completion;
            midpointSucceeded = false;
            sequence = StartCoroutine(Run(phase, midpoint));
            return true;
        }

        public bool ShowGameCompleted(GameCompletionSummary summary, Func<bool> onLobbyRequested)
        {
            if (sequence != null || gameCompletionVisible || defeatVisible || !canvasGroup || !defeatFont ||
                !successQuestPanelSprite || !successGoldPanelSprite || !successGoldIconSprite ||
                !successTimeIconSprite || !successStarIconSprite || onLobbyRequested == null) return false;

            try
            {
                gameObject.SetActive(true);
                EnsureSuccessResultBuilt();
                ApplySuccessSummary(summary);
                gameCompletionVisible = true;
                resultNavigationStarted = false;
                sceneLoadRequested = false;
                successInputEnabled = false;
                successLobbyRequested = onLobbyRequested;
                if (banner) banner.gameObject.SetActive(false);
                successResultRoot.SetActive(true);
                successResultRoot.transform.SetAsLastSibling();
                successInputCatcher.raycastTarget = true;
                successClipboard.anchoredPosition = SuccessClipboardStart;
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
                successEntrance = StartCoroutine(PlaySuccessEntrance());
                if (successEntrance == null) throw new InvalidOperationException("Success entrance coroutine did not start.");
                Debug.Log($"[GameFlow][Completion] Success result scheduled after shine. remaining={summary.RemainingSeconds}, finalScore={summary.FinalScore}", this);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("[GameFlow][Completion] Success result construction failed: " + exception, this);
                HideSuccessResult();
                return false;
            }
        }

        public bool ShowGameDefeated(Func<bool> onRetryRequested, Func<bool> onLobbyRequested)
        {
            if (sequence != null || gameCompletionVisible || defeatVisible || !canvasGroup ||
                !defeatCharacterSprite || !retryButtonSprite || !defeatFont ||
                onRetryRequested == null || onLobbyRequested == null) return false;

            try
            {
                gameObject.SetActive(true);
                EnsureDefeatResultBuilt();
                retryRequested = onRetryRequested;
                lobbyRequested = onLobbyRequested;
                defeatVisible = true;
                resultNavigationStarted = false;
                sceneLoadRequested = false;
                DefeatShowCount++;
                if (banner) banner.gameObject.SetActive(false);
                defeatResultRoot.SetActive(true);
                defeatResultRoot.transform.SetAsLastSibling();
                defeatDimButton.interactable = true;
                defeatRetryButton.interactable = true;
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
                Debug.Log("[GameFlow][Defeat] Result overlay shown once.", this);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("[GameFlow][Defeat] Result overlay construction failed: " + exception, this);
                return false;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!gameCompletionVisible || !successInputEnabled || resultNavigationStarted || sceneLoadRequested ||
                eventData == null || eventData.button != PointerEventData.InputButton.Left) return;

            resultNavigationStarted = true;
            sceneLoadRequested = true;
            successInputEnabled = false;
            successInputCatcher.raycastTarget = false;
            bool accepted = false;
            try { accepted = successLobbyRequested?.Invoke() == true; }
            catch (Exception exception) { Debug.LogError("[GameFlow][Completion] Lobby request failed: " + exception, this); }
            if (!accepted)
                Debug.LogError("[GameFlow][Completion] Lobby request was rejected. Success result remains locked.", this);
        }

        private IEnumerator Run(GamePhaseId phase, Func<bool> midpoint)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            messageText.text = $"PHASE {(int)phase} CLEAR!!";
            float width = ((RectTransform)transform).rect.width;
            float distance = (width + banner.rect.width) * .5f;
            banner.anchoredPosition = new Vector2(distance, 0f);
            yield return Move(distance, 0f, enterDuration, false);
            midpointSucceeded = TryExecuteMidpoint(midpoint, out Exception failure);
            if (!midpointSucceeded)
            {
                Finish(PhaseTransitionResult.Failed, failure ?? new InvalidOperationException("Midpoint reported failure."));
                yield break;
            }
            yield return Wait(holdDuration);
            yield return Move(0f, -distance, exitDuration, true);
            yield return Wait(completionDelay);
            Finish(PhaseTransitionResult.Succeeded, null);
        }

        private static bool TryExecuteMidpoint(Func<bool> midpoint, out Exception failure)
        {
            failure = null;
            try { return midpoint == null || midpoint(); }
            catch (Exception exception) { failure = exception; return false; }
        }

        private void Finish(PhaseTransitionResult result, Exception error)
        {
            if (sequence == null) return;
            sequence = null;
            Action<PhaseTransitionResult, bool, Exception> callback = finished;
            finished = null;
            if (canvasGroup) canvasGroup.blocksRaycasts = false;
            gameObject.SetActive(false);
            callback?.Invoke(result, midpointSucceeded, error);
        }

        private void OnDisable()
        {
            if (successEntrance != null)
            {
                StopCoroutine(successEntrance);
                successEntrance = null;
            }
            if (sequence != null)
            {
                StopCoroutine(sequence);
                sequence = null;
                Action<PhaseTransitionResult, bool, Exception> callback = finished;
                finished = null;
                if (canvasGroup) canvasGroup.blocksRaycasts = false;
                callback?.Invoke(PhaseTransitionResult.Interrupted, midpointSucceeded, null);
            }
            gameCompletionVisible = false;
            sceneLoadRequested = false;
            defeatVisible = false;
            resultNavigationStarted = false;
            retryRequested = null;
            lobbyRequested = null;
            successLobbyRequested = null;
            successInputEnabled = false;
            if (defeatResultRoot) defeatResultRoot.SetActive(false);
            if (successResultRoot) successResultRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            if (defeatDimButton) defeatDimButton.onClick.RemoveListener(HandleLobbyClicked);
            if (defeatRetryButton) defeatRetryButton.onClick.RemoveListener(HandleRetryClicked);
        }

        private void EnsureSuccessResultBuilt()
        {
            if (successResultRoot) return;

            RectTransform root = CreateStretch("SuccessResultRoot", transform);
            successResultRoot = root.gameObject;

            RectTransform dimRect = CreateStretch("Dim", root);
            Image dim = dimRect.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, .68f);
            dim.raycastTarget = false;

            RectTransform inputRect = CreateStretch("InputCatcher", root);
            successInputCatcher = inputRect.gameObject.AddComponent<Image>();
            successInputCatcher.color = Color.clear;
            successInputCatcher.raycastTarget = true;

            successClipboard = CreateRect("ClipboardRoot", root, SuccessClipboardStart,
                AspectSize(successQuestPanelSprite, SuccessClipboardWidth));
            Image questPanel = successClipboard.gameObject.AddComponent<Image>();
            questPanel.sprite = successQuestPanelSprite;
            questPanel.color = Color.white;
            questPanel.preserveAspect = true;
            questPanel.raycastTarget = false;

            TextMeshProUGUI clearTitle = CreateText("Title_Clear", successClipboard, ScaleSuccessLayout(new Vector2(0f, 390f)), ScaleSuccessLayout(new Vector2(620f, 140f)),
                "CLEAR!", ScaleSuccessLayout(92f), new Color32(255, 198, 42, 255), TextAlignmentOptions.Center);
            clearTitle.fontStyle = FontStyles.Bold;
            clearTitle.outlineColor = new Color32(25, 31, 39, 255);
            clearTitle.outlineWidth = .18f;

            RectTransform scoreSection = CreateRect("ScoreSection", successClipboard, ScaleSuccessLayout(new Vector2(0f, 170f)), ScaleSuccessLayout(new Vector2(650f, 130f)));
            CreateImage("Img_StarIcon", scoreSection, ScaleSuccessLayout(new Vector2(-235f, 0f)), ScaleSuccessLayout(new Vector2(88f, 84f)), successStarIconSprite);
            successAcquiredScoreText = CreateText("Text_AcquiredScore", scoreSection, ScaleSuccessLayout(new Vector2(45f, 0f)), ScaleSuccessLayout(new Vector2(470f, 88f)),
                string.Empty, ScaleSuccessLayout(46f), new Color32(48, 54, 62, 255), TextAlignmentOptions.Center);
            successAcquiredScoreText.fontStyle = FontStyles.Bold;

            RectTransform timeSection = CreateRect("TimeSection", successClipboard, ScaleSuccessLayout(new Vector2(0f, 20f)), ScaleSuccessLayout(new Vector2(650f, 130f)));
            CreateImage("Img_TimeIcon", timeSection, ScaleSuccessLayout(new Vector2(-235f, 0f)), AspectSize(successTimeIconSprite, ScaleSuccessLayout(82f)), successTimeIconSprite);
            successRemainingTimeText = CreateText("Text_RemainingTime", timeSection, ScaleSuccessLayout(new Vector2(45f, 0f)), ScaleSuccessLayout(new Vector2(470f, 88f)),
                string.Empty, ScaleSuccessLayout(46f), new Color32(48, 54, 62, 255), TextAlignmentOptions.Center);
            successRemainingTimeText.fontStyle = FontStyles.Bold;

            RectTransform totalSection = CreateRect("TotalScoreSection", successClipboard, ScaleSuccessLayout(new Vector2(0f, -135f)), ScaleSuccessLayout(new Vector2(650f, 145f)));
            successTotalScoreText = CreateText("Text_TotalScore", totalSection, Vector2.zero, ScaleSuccessLayout(new Vector2(620f, 110f)),
                string.Empty, ScaleSuccessLayout(58f), new Color32(24, 72, 145, 255), TextAlignmentOptions.Center);
            successTotalScoreText.fontStyle = FontStyles.Bold;

            RectTransform goldPanel = CreateRect("GoldPanel", successClipboard, ScaleSuccessLayout(new Vector2(0f, -370f)),
                AspectSize(successGoldPanelSprite, ScaleSuccessLayout(610f)));
            CreateImage("Img_GoldPanel", goldPanel, Vector2.zero, goldPanel.sizeDelta, successGoldPanelSprite);
            CreateImage("Img_GoldIcon", goldPanel, ScaleSuccessLayout(new Vector2(-180f, 0f)), ScaleSuccessLayout(new Vector2(98f, 98f)), successGoldIconSprite);
            successGoldRewardText = CreateText("Text_GoldReward", goldPanel, ScaleSuccessLayout(new Vector2(85f, 0f)), ScaleSuccessLayout(new Vector2(380f, 100f)),
                string.Empty, ScaleSuccessLayout(50f), new Color32(220, 153, 18, 255), TextAlignmentOptions.Center);
            successGoldRewardText.fontStyle = FontStyles.Bold;

            successResultRoot.SetActive(false);
        }

        private void ApplySuccessSummary(GameCompletionSummary summary)
        {
            successAcquiredScoreText.text = summary.AcquiredScore.HasValue
                ? $"획득 점수  <color=#184B91>{summary.AcquiredScore.Value:N0}</color>"
                : "획득 점수  <color=#184B91>0</color>";
            successRemainingTimeText.text = $"남은 시간  <color=#184B91>{summary.RemainingSeconds:N0}초</color>";
            successTotalScoreText.text = $"총 점수  <color=#144E9C>{summary.FinalScore:N0}</color>";
            successGoldRewardText.text = "<color=#8A6424>획득 골드</color>  0 골드";
        }

        private IEnumerator PlaySuccessEntrance()
        {
            float riseDuration = SuccessEntranceDuration - .1f;
            float elapsed = 0f;
            while (elapsed < riseDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / riseDuration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                successClipboard.anchoredPosition = Vector2.LerpUnclamped(SuccessClipboardStart, SuccessClipboardOvershoot, eased);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < .1f)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / .1f);
                float eased = t * t * (3f - 2f * t);
                successClipboard.anchoredPosition = Vector2.LerpUnclamped(SuccessClipboardOvershoot, SuccessClipboardTarget, eased);
                yield return null;
            }

            successClipboard.anchoredPosition = SuccessClipboardTarget;
            while (IsAnyPointerPressed()) yield return null;
            yield return null;
            successEntrance = null;
            if (gameCompletionVisible && !resultNavigationStarted) successInputEnabled = true;
        }

        private static bool IsAnyPointerPressed()
        {
            if (Pointer.current != null && Pointer.current.press.isPressed) return true;
            if (Touchscreen.current == null) return false;
            foreach (var touch in Touchscreen.current.touches)
            {
                if (touch.press.isPressed) return true;
            }
            return false;
        }

        private void HideSuccessResult()
        {
            if (successEntrance != null)
            {
                StopCoroutine(successEntrance);
                successEntrance = null;
            }
            gameCompletionVisible = false;
            successInputEnabled = false;
            successLobbyRequested = null;
            if (successResultRoot) successResultRoot.SetActive(false);
        }

        private void EnsureDefeatResultBuilt()
        {
            if (defeatResultRoot) return;

            RectTransform root = CreateStretch("DefeatResultRoot", transform);
            defeatResultRoot = root.gameObject;
            Image dimImage = root.gameObject.AddComponent<Image>();
            dimImage.color = new Color(0f, 0f, 0f, .7f);
            dimImage.raycastTarget = true;
            defeatDimButton = root.gameObject.AddComponent<Button>();
            defeatDimButton.targetGraphic = dimImage;
            defeatDimButton.transition = Selectable.Transition.None;
            defeatDimButton.onClick.AddListener(HandleLobbyClicked);

            RectTransform content = CreateStretch("ContentRoot", root);
            RectTransform characterRect = CreateRect("Image_PlayerDown", content, new Vector2(0f, 300f),
                AspectSize(defeatCharacterSprite, 960f));
            Image character = characterRect.gameObject.AddComponent<Image>();
            character.sprite = defeatCharacterSprite;
            character.color = Color.white;
            character.preserveAspect = true;
            character.raycastTarget = false;

            RectTransform messageRect = CreateRect("Text_RequestFailed", content, new Vector2(0f, -385f), new Vector2(660f, 210f));
            TextMeshProUGUI message = messageRect.gameObject.AddComponent<TextMeshProUGUI>();
            message.font = defeatFont;
            message.text = "의뢰 실패...";
            message.fontSize = 190f;
            message.color = new Color32(215, 62, 62, 255);
            message.outlineColor = new Color32(255, 255, 255, 255);
            message.outlineWidth = .14f;
            message.alignment = TextAlignmentOptions.Center;
            message.textWrappingMode = TextWrappingModes.NoWrap;
            message.overflowMode = TextOverflowModes.Overflow;
            message.raycastTarget = false;

            RectTransform retryRect = CreateRect("Button_Retry", content, new Vector2(0f, -620f),
                AspectSize(retryButtonSprite, 410f));
            Image retryImage = retryRect.gameObject.AddComponent<Image>();
            retryImage.sprite = retryButtonSprite;
            retryImage.color = Color.white;
            retryImage.preserveAspect = true;
            retryImage.raycastTarget = true;
            defeatRetryButton = retryRect.gameObject.AddComponent<Button>();
            defeatRetryButton.targetGraphic = retryImage;
            Navigation navigation = defeatRetryButton.navigation;
            navigation.mode = Navigation.Mode.None;
            defeatRetryButton.navigation = navigation;
            defeatRetryButton.onClick.AddListener(HandleRetryClicked);

            defeatResultRoot.SetActive(false);
        }

        private void HandleRetryClicked()
        {
            if (!TryBeginResultNavigation()) return;
            bool accepted = false;
            try { accepted = retryRequested?.Invoke() == true; }
            catch (Exception exception) { Debug.LogError("[GameFlow][Defeat] Retry request failed: " + exception, this); }
            if (accepted)
            {
                RetryActionCount++;
            }
        }

        private void HandleLobbyClicked()
        {
            if (!TryBeginResultNavigation()) return;
            bool accepted = false;
            try { accepted = lobbyRequested?.Invoke() == true; }
            catch (Exception exception) { Debug.LogError("[GameFlow][Defeat] Lobby request failed: " + exception, this); }
            if (accepted)
            {
                LobbyActionCount++;
            }
        }

        private bool TryBeginResultNavigation()
        {
            if (!defeatVisible || resultNavigationStarted) return false;
            resultNavigationStarted = true;
            defeatRetryButton.interactable = false;
            defeatDimButton.interactable = false;
            return true;
        }

        private static RectTransform CreateStretch(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.layer = parent.gameObject.layer;
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            return rect;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.layer = parent.gameObject.layer;
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            return rect;
        }

        private static Image CreateImage(string name, Transform parent, Vector2 position, Vector2 size, Sprite sprite)
        {
            RectTransform rect = CreateRect(name, parent, position, size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        private TextMeshProUGUI CreateText(string name, Transform parent, Vector2 position, Vector2 size,
            string value, float fontSize, Color color, TextAlignmentOptions alignment)
        {
            RectTransform rect = CreateRect(name, parent, position, size);
            TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = defeatFont;
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static Vector2 AspectSize(Sprite sprite, float width)
        {
            float height = sprite && sprite.rect.width > 0f ? width * sprite.rect.height / sprite.rect.width : width;
            return new Vector2(width, height);
        }

        private static float ScaleSuccessLayout(float value) => value * (SuccessClipboardWidth / SuccessClipboardBaseWidth);
        private static Vector2 ScaleSuccessLayout(Vector2 value) => value * (SuccessClipboardWidth / SuccessClipboardBaseWidth);

        private IEnumerator Move(float from, float to, float duration, bool easeIn)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(.001f, duration));
                float eased = easeIn ? t * t * t : 1f - Mathf.Pow(1f - t, 3f);
                banner.anchoredPosition = new Vector2(Mathf.LerpUnclamped(from, to, eased), 0f);
                yield return null;
            }
            banner.anchoredPosition = new Vector2(to, 0f);
        }

        private static IEnumerator Wait(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }
    }
}
