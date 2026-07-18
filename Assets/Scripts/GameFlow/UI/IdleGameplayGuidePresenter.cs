using TMPro;
using UnityEngine;

namespace HATAGONG.GameFlow
{
    public sealed class IdleGameplayGuidePresenter : MonoBehaviour
    {
        public const float DefaultIdleThresholdSeconds = 3f;
        public const float DefaultPulsePeriodSeconds = 1.5f;
        public const float DefaultMinimumAlpha = 0.35f;
        public const float DefaultMaximumAlpha = 0.9f;
        public const string Phase1Message = "타일을 파괴하세요!";
        public const string Phase2Message = "드래그해서 칠하세요!";
        public const string Phase3Message = "조각을 맞추세요!";

        [SerializeField] private GameSessionController session;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TextMeshProUGUI guideText;
        [SerializeField] private GameObject[] blockingUiRoots = System.Array.Empty<GameObject>();
        [SerializeField] private float idleThresholdSeconds = DefaultIdleThresholdSeconds;
        [SerializeField] private float pulsePeriodSeconds = DefaultPulsePeriodSeconds;
        [SerializeField] private float minimumAlpha = DefaultMinimumAlpha;
        [SerializeField] private float maximumAlpha = DefaultMaximumAlpha;

        private float idleElapsed;
        private float pulseElapsed;
        private GamePhaseId trackedPhase;
        private bool hasTrackedPhase;
        private bool wasEligible;
        private bool applicationSuspended;
        private bool externalUiBlocked;
        private bool initialPromptDismissed;

        public float IdleElapsed => idleElapsed;
        public bool IsVisible => canvasGroup && canvasGroup.alpha > 0f;
        public float IdleThresholdSeconds => idleThresholdSeconds;
        public float PulsePeriodSeconds => pulsePeriodSeconds;
        public float MinimumAlpha => minimumAlpha;
        public float MaximumAlpha => maximumAlpha;
        public string CurrentMessage => guideText ? guideText.text : string.Empty;

        private void Awake()
        {
            NormalizeConfiguration();
            HideImmediate();
        }

        private void OnEnable()
        {
            GameplayInputActivity.ValidGameplayInput += OnValidGameplayInput;
            if (session) session.SessionStateChanged += OnSessionStateChanged;
            ResetIdleMeasurement();
        }

        private void OnDisable()
        {
            GameplayInputActivity.ValidGameplayInput -= OnValidGameplayInput;
            if (session) session.SessionStateChanged -= OnSessionStateChanged;
            ResetIdleMeasurement();
        }

        private void Update()
        {
            Advance(Time.unscaledDeltaTime);
        }

        public void SetExternalUiBlocked(bool blocked)
        {
            if (externalUiBlocked == blocked) return;
            externalUiBlocked = blocked;
            idleElapsed = 0f;
            pulseElapsed = 0f;
            HideImmediate();
        }

        public void NotifyValidGameplayInput(GamePhaseId phaseId)
        {
            OnValidGameplayInput(phaseId);
        }

        public void Advance(float unscaledDeltaTime)
        {
            if (!TryGetEligiblePhase(out GamePhaseId phaseId))
            {
                if ((applicationSuspended || externalUiBlocked || IsBlockingUiActive()) && hasTrackedPhase)
                {
                    HideImmediate();
                    return;
                }
                if (wasEligible || IsVisible || idleElapsed > 0f) ResetIdleMeasurement();
                return;
            }

            if (!wasEligible || !hasTrackedPhase || trackedPhase != phaseId)
            {
                trackedPhase = phaseId;
                hasTrackedPhase = true;
                wasEligible = true;
                idleElapsed = 0f;
                pulseElapsed = 0f;
                initialPromptDismissed = false;
                SetMessage(phaseId);
                SetVisibleAlpha(maximumAlpha);
            }

            if (!initialPromptDismissed)
            {
                SetVisibleAlpha(maximumAlpha);
                return;
            }

            idleElapsed += Mathf.Max(0f, unscaledDeltaTime);
            if (idleElapsed < idleThresholdSeconds)
            {
                HideImmediate();
                return;
            }

            pulseElapsed += Mathf.Max(0f, unscaledDeltaTime);
            float normalized = Mathf.Repeat(pulseElapsed, pulsePeriodSeconds) / pulsePeriodSeconds;
            float wave = 0.5f - 0.5f * Mathf.Cos(normalized * Mathf.PI * 2f);
            SetVisibleAlpha(Mathf.Lerp(minimumAlpha, maximumAlpha, wave));
        }

        public static string MessageFor(GamePhaseId phaseId)
        {
            switch (phaseId)
            {
                case GamePhaseId.Phase1: return Phase1Message;
                case GamePhaseId.Phase2: return Phase2Message;
                case GamePhaseId.Phase3: return Phase3Message;
                default: return string.Empty;
            }
        }

        private bool TryGetEligiblePhase(out GamePhaseId phaseId)
        {
            phaseId = default;
            if (!session || applicationSuspended || externalUiBlocked || IsBlockingUiActive() ||
                session.CurrentState != GameSessionState.Playing || !session.CanAcceptGameplayInput ||
                session.IsExpired || session.IsCompleted || session.IsSceneLoadRequested || session.IsTimerPaused)
            {
                return false;
            }

            IGamePhase phase = session.CurrentPhase;
            if (phase == null || !phase.IsPrepared || !phase.IsRunning || phase.IsCleared || phase.IsExitReady ||
                !(phase is IGameplayInputStatus inputStatus) || !inputStatus.IsGameplayInputEnabled)
            {
                return false;
            }

            phaseId = phase.PhaseId;
            return MessageFor(phaseId).Length > 0;
        }

        private bool IsBlockingUiActive()
        {
            if (blockingUiRoots == null) return false;
            for (int i = 0; i < blockingUiRoots.Length; i++)
            {
                if (blockingUiRoots[i] && blockingUiRoots[i].activeInHierarchy) return true;
            }
            return false;
        }

        private void OnValidGameplayInput(GamePhaseId phaseId)
        {
            if (!TryGetEligiblePhase(out GamePhaseId currentPhase) || currentPhase != phaseId) return;
            initialPromptDismissed = true;
            idleElapsed = 0f;
            pulseElapsed = 0f;
            HideImmediate();
        }

        private void OnSessionStateChanged(GameSessionState _)
        {
            ResetIdleMeasurement();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            applicationSuspended = !hasFocus;
            ResetIdleMeasurement();
        }

        private void OnApplicationPause(bool paused)
        {
            applicationSuspended = paused;
            ResetIdleMeasurement();
        }

        private void ResetIdleMeasurement()
        {
            idleElapsed = 0f;
            pulseElapsed = 0f;
            hasTrackedPhase = false;
            wasEligible = false;
            initialPromptDismissed = false;
            HideImmediate();
        }

        private void SetMessage(GamePhaseId phaseId)
        {
            if (guideText) guideText.text = MessageFor(phaseId);
        }

        private void HideImmediate()
        {
            if (!canvasGroup) return;
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        private void SetVisibleAlpha(float alpha)
        {
            if (!canvasGroup) return;
            canvasGroup.alpha = Mathf.Clamp(alpha, minimumAlpha, maximumAlpha);
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        private void NormalizeConfiguration()
        {
            idleThresholdSeconds = DefaultIdleThresholdSeconds;
            pulsePeriodSeconds = Mathf.Max(0.01f, pulsePeriodSeconds);
            minimumAlpha = Mathf.Clamp01(minimumAlpha);
            maximumAlpha = Mathf.Clamp(maximumAlpha, minimumAlpha, 1f);
            if (guideText)
            {
                guideText.raycastTarget = false;
                guideText.textWrappingMode = TextWrappingModes.NoWrap;
            }
            if (canvasGroup)
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }
    }
}
