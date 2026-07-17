using System;
using System.Collections.Generic;
using HATAGONG.Outgame;
using HATAGONG.Phase3;
using HATAGONG.Phase3Tangram;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HATAGONG.GameFlow
{
    public sealed class TerminalPhaseClearCommitGate
    {
        public bool IsCommitted { get; private set; }

        public bool TryCommit(GameSessionState sessionState, GamePhaseId requestedPhase, GamePhaseId currentPhase, Action stopTimer)
        {
            if (sessionState != GameSessionState.Playing || requestedPhase != GamePhaseId.Phase3 || currentPhase != GamePhaseId.Phase3) return false;
            if (IsCommitted) return true;
            IsCommitted = true;
            stopTimer?.Invoke();
            return true;
        }

        public bool ShouldIgnoreTimerExpiration => IsCommitted;
        public void Reset() => IsCommitted = false;
    }

    public sealed class GameSessionController : MonoBehaviour
    {
        [SerializeField] private GameTimerController timer;
        [SerializeField] private GameScoreController score;
        [SerializeField] private GameStartOverlay startOverlay;
        [SerializeField] private PhaseTransitionController transition;
        [SerializeField] private GameDifficulty difficulty = GameDifficulty.Hard;
        [SerializeField] private GamePhaseId initialPhase = GamePhaseId.Phase1;
        [SerializeField] private MonoBehaviour[] phases = Array.Empty<MonoBehaviour>();

        private readonly GameSessionModel _model = new GameSessionModel();
        private readonly Dictionary<IGamePhase, Action> _exitReadyHandlers = new Dictionary<IGamePhase, Action>();
        private readonly GamePhaseExitReadyGate _exitReadyGate = new GamePhaseExitReadyGate();
        private readonly TerminalPhaseClearCommitGate _terminalPhaseClearGate = new TerminalPhaseClearCommitGate();
        private GamePhaseRegistry _registry;
        private GamePhaseTransaction _phaseTransaction;
        private GameRunContext _runContext;
        private string _contextInitializationError;
        private IGamePhase _pendingNextPhase;
        private double _transitionStartedAt;
        private bool _resultSceneLoadRequested;

        public GameSessionState CurrentState => _model.CurrentState;
        public bool CanAcceptGameplayInput => _model.CanAcceptGameplayInput;
        public bool CanAddScore => _model.CanAddScore;
        public bool IsTransitioning => _model.IsTransitioning;
        public bool IsExpired => _model.IsExpired;
        public bool IsCompleted => _model.IsCompleted;
        public GameRunContext RunContext => _runContext;
        public IGamePhase CurrentPhase => _phaseTransaction?.CurrentPhase;
        public double LastTransitionElapsedSeconds { get; private set; }
        public int ResultSceneLoadRequestCount { get; private set; }

        public event Action<GameSessionState> SessionStateChanged { add => _model.SessionStateChanged += value; remove => _model.SessionStateChanged -= value; }
        public event Action GameExpired { add => _model.GameExpired += value; remove => _model.GameExpired -= value; }
        public event Action GameCompleted { add => _model.GameCompleted += value; remove => _model.GameCompleted -= value; }

        private void Awake()
        {
            if (!TryCreateRunContext(out GameRunContext runContext, out _contextInitializationError)) return;
            _runContext = runContext;
            if (_runContext.HasSelectedRequest && !OutgameRequestSelectionStore.ActivatePending())
            {
                _contextInitializationError = "Pending request selection could not become the active retry snapshot.";
                _runContext = default;
                return;
            }
            GameRequestContext requestContext = GetComponent<GameRequestContext>();
            if (requestContext) requestContext.SetRequest(_runContext.RequestType);
        }

        private void Start()
        {
            DisableSerializedPhaseInputs();
            if (!timer || timer.Timer == null || !score || !startOverlay || !transition)
            {
                FailInitialization("Required GameSessionController references are missing.");
                return;
            }

            timer.Timer.TimerExpired += OnTimerExpired;
            score.ResetForNewSession();
            timer.ResetTimer();
            _terminalPhaseClearGate.Reset();

            if (!GamePhaseRegistry.TryCreate(phases, initialPhase, out _registry, out string registryError))
            {
                FailInitialization(registryError);
                return;
            }

            _registry.DisableAllInput();
            if (_registry.TryGet(GamePhaseId.Phase3, out IGamePhase terminalPhase) && terminalPhase is Phase3TangramManager phase3)
                phase3.BindTerminalClearCommit(TryCommitTerminalPhaseClear);
            if (!_runContext.IsValid)
            {
                FailInitialization(string.IsNullOrEmpty(_contextInitializationError)
                    ? "GameRunContext was not initialized."
                    : _contextInitializationError);
                return;
            }

            _phaseTransaction = new GamePhaseTransaction(_registry);
            if (!_phaseTransaction.TryInitialize(initialPhase, _runContext, out string initializationError))
            {
                FailInitialization(initializationError);
                return;
            }

            SubscribePhaseExitReady();
            if (!_model.SetState(GameSessionState.Ready))
            {
                FailInitialization("Session could not enter Ready after phase preparation.");
                return;
            }
            if (!startOverlay.Play(BeginPlaying))
            {
                Debug.LogError("[GameFlow][Session] READY overlay could not start. Session remains Ready with all input disabled.", this);
                _phaseTransaction.SetCurrentInputEnabled(false);
            }
        }

        private bool TryCreateRunContext(out GameRunContext context, out string error)
        {
            if (!OutgameRequestSelectionStore.TryGetPending(out OutgameRequestRunSelection selection))
            {
                GameRequestContext requestContext = GetComponent<GameRequestContext>();
                RequestType fallbackRequestType = requestContext ? requestContext.CurrentRequestType : RequestType.Normal;
                context = new GameRunContext(difficulty, fallbackRequestType);
                error = context.IsValid ? string.Empty : $"Invalid direct INGAME fallback context: difficulty={difficulty}, requestType={fallbackRequestType}.";
                return context.IsValid;
            }

            context = new GameRunContext(selection.RequestId, selection.Difficulty, selection.RequestType,
                selection.PermanentSeed, selection.Phase1Seed, selection.Phase2Seed, selection.Phase3Seed);
            if (context.IsValid)
            {
                error = string.Empty;
                return true;
            }

            OutgameRequestSelectionStore.Clear();
            error = $"Invalid pending OUTGAME request selection: requestId='{selection.RequestId}', difficulty={selection.Difficulty}, requestType={selection.RequestType}.";
            return false;
        }

        private void OnDestroy()
        {
            if (timer && timer.Timer != null) timer.Timer.TimerExpired -= OnTimerExpired;
            foreach (KeyValuePair<IGamePhase, Action> pair in _exitReadyHandlers)
            {
                pair.Key.PhaseExitReady -= pair.Value;
            }
            _exitReadyHandlers.Clear();
        }

        private void BeginPlaying()
        {
            if (!_model.SetState(GameSessionState.Playing)) return;
            timer.StartTimer();
            _phaseTransaction.SetCurrentInputEnabled(true);
        }

        private void OnTimerExpired()
        {
            TryCommitTerminalDefeat();
        }

        public bool TryCommitTerminalDefeat()
        {
            if (_terminalPhaseClearGate.ShouldIgnoreTimerExpiration || !_model.SetState(GameSessionState.Expired)) return false;
            timer?.StopTimer();
            _registry?.DisableAllInput();
            _phaseTransaction?.SetCurrentInputEnabled(false);
            score?.LockScore();
            if (!transition || !transition.ShowGameDefeated(RestartSameRequest, ReturnToOutgameLobby))
                Debug.LogError("[GameFlow][Defeat] Result overlay could not be shown. Session remains Expired with all gameplay locked.", this);
            return true;
        }

        public bool RetryPendingTransition()
        {
            if (!_model.IsTransitioning || CurrentPhase == null || !CurrentPhase.IsExitReady || (transition && transition.IsTransitioning))
            {
                return false;
            }
            _phaseTransaction.SetCurrentInputEnabled(false);
            return PrepareAndStartNextTransition();
        }

        public void CompleteGame()
        {
            if (!_model.SetState(GameSessionState.Completed)) return;
            _phaseTransaction?.SetCurrentInputEnabled(false);
            timer.StopTimer();
            score.LockScore();
            var summary = new GameCompletionSummary(
                timer.DisplayedSeconds,
                acquiredScore: Phase3ScoreRules.PhaseClearScore,
                finalScore: score.CurrentScore);
            if (!transition.ShowGameCompleted(summary, ReturnToOutgameLobbyAfterSuccess))
                Debug.LogError("[GameFlow][Completion] Result overlay could not be shown. Session remains Completed.", this);
        }

        public bool TryCommitTerminalPhaseClear(GamePhaseId phaseId)
        {
            GamePhaseId currentPhaseId = CurrentPhase != null ? CurrentPhase.PhaseId : default;
            return _terminalPhaseClearGate.TryCommit(_model.CurrentState, phaseId, currentPhaseId, timer ? timer.StopTimer : null);
        }

        private bool RestartSameRequest()
        {
            const string sceneName = "INGAME";
            if (_resultSceneLoadRequested || !_model.IsExpired || !_runContext.IsValid ||
                !Application.CanStreamedLevelBeLoaded(sceneName)) return false;
            if (!OutgameRequestSelectionStore.TryPrepareRetry(_runContext))
            {
                Debug.LogError("[GameFlow][Defeat] Active request snapshot no longer matches the running context.", this);
                return false;
            }

            bool started = TryLoadResultScene(sceneName, "Defeat");
            if (!started) OutgameRequestSelectionStore.CancelPreparedRetry(_runContext);
            return started;
        }

        private bool ReturnToOutgameLobby()
        {
            const string sceneName = "OUTGAME_LOBBY";
            if (_resultSceneLoadRequested || !_model.IsExpired || !Application.CanStreamedLevelBeLoaded(sceneName)) return false;
            if (!TryLoadResultScene(sceneName, "Defeat")) return false;
            OutgameRequestSelectionStore.Clear();
            return true;
        }

        private bool ReturnToOutgameLobbyAfterSuccess()
        {
            const string sceneName = "OUTGAME_LOBBY";
            if (_resultSceneLoadRequested || !_model.IsCompleted || !Application.CanStreamedLevelBeLoaded(sceneName)) return false;
            if (!TryLoadResultScene(sceneName, "Completion")) return false;
            OutgameRequestSelectionStore.Clear();
            return true;
        }

        private bool TryLoadResultScene(string sceneName, string resultKind)
        {
            if (_resultSceneLoadRequested) return false;
            _resultSceneLoadRequested = true;
            ResultSceneLoadRequestCount++;
            try
            {
                AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
                if (operation != null)
                {
                    Debug.Log($"[GameFlow][{resultKind}] Loading scene once: {sceneName}.", this);
                    return true;
                }
                Debug.LogError($"[GameFlow][{resultKind}] LoadSceneAsync returned null: {sceneName}.", this);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[GameFlow][{resultKind}] LoadSceneAsync failed once for {sceneName}: {exception}", this);
            }
            return false;
        }

        private void SubscribePhaseExitReady()
        {
            for (int i = 0; i < _registry.Phases.Count; i++)
            {
                IGamePhase phase = _registry.Phases[i];
                Action handler = () => OnPhaseExitReady(phase);
                _exitReadyHandlers.Add(phase, handler);
                phase.PhaseExitReady += handler;
            }
        }

        private void OnPhaseExitReady(IGamePhase phase)
        {
            if (!_exitReadyGate.TryAccept(phase, CurrentPhase, _model.CurrentState))
            {
                return;
            }

            if (phase.PhaseId == GamePhaseId.Phase3)
            {
                CompleteGame();
                return;
            }

            _phaseTransaction.SetCurrentInputEnabled(false);
            if (!_model.SetState(GameSessionState.Transitioning))
            {
                Debug.LogError($"[GameFlow][Transition] Could not lock session after {phase.PhaseId} exit-ready.", this);
                return;
            }
            timer.PauseTimer();
            _transitionStartedAt = Time.realtimeSinceStartupAsDouble;
            PrepareAndStartNextTransition();
        }

        private bool PrepareAndStartNextTransition()
        {
            GamePhaseId nextId = (GamePhaseId)((int)CurrentPhase.PhaseId + 1);
            if (!_registry.TryGet(nextId, out _pendingNextPhase))
            {
                return FailLockedTransition($"Next phase is not registered: {nextId}.");
            }

            if (!_pendingNextPhase.IsPrepared && !_phaseTransaction.TryPrepareNext(_pendingNextPhase, _runContext, out string prepareError))
            {
                return FailLockedTransition(prepareError);
            }
            _pendingNextPhase.SetInputEnabled(false);
            if (!_pendingNextPhase.IsPrepared || _pendingNextPhase.IsRunning)
            {
                return FailLockedTransition($"Next phase did not remain prepared and inactive: {nextId}.");
            }

            bool started = transition.PlayPreparedTransition(
                CurrentPhase.PhaseId,
                nextId,
                CommitNextPhaseAtMidpoint,
                OnTransitionOverlayFinished);
            if (!started)
            {
                return FailLockedTransition("Transition overlay rejected the prepared transition.");
            }
            return true;
        }

        private bool CommitNextPhaseAtMidpoint()
        {
            if (_pendingNextPhase == null || !_pendingNextPhase.IsPrepared)
            {
                return FailLockedTransition("Prepared next phase was lost before midpoint commit.");
            }
            if (!_phaseTransaction.TryCommitPreparedNext(_pendingNextPhase, out string commitError))
            {
                return FailLockedTransition(commitError);
            }
            return true;
        }

        private void OnTransitionOverlayFinished(PhaseTransitionResult result, bool midpointSucceeded, Exception error)
        {
            bool committed = CurrentPhase == _pendingNextPhase;
            if (GamePhaseTransitionCompletion.ShouldResume(result, midpointSucceeded, committed))
            {
                FinishCommittedTransition();
                return;
            }

            _phaseTransaction.SetCurrentInputEnabled(false);
            Debug.LogError($"[GameFlow][Transition] Transition stopped in locked state. result={result}, midpoint={midpointSucceeded}, error={error}", this);
        }

        private void FinishCommittedTransition()
        {
            if (!_model.SetState(GameSessionState.Playing))
            {
                FailLockedTransition("Session could not leave Transitioning after midpoint commit.");
                return;
            }
            timer.ResumeTimer();
            _phaseTransaction.SetCurrentInputEnabled(true);
            LastTransitionElapsedSeconds = Time.realtimeSinceStartupAsDouble - _transitionStartedAt;
            if (LastTransitionElapsedSeconds > 2d)
            {
                Debug.LogError($"[GameFlow][Transition] Transition exceeded 2.0s: {LastTransitionElapsedSeconds:F3}s.", this);
            }
            else if (LastTransitionElapsedSeconds > 1.4d)
            {
                Debug.LogWarning($"[GameFlow][Transition] Transition exceeded the 1.4s target: {LastTransitionElapsedSeconds:F3}s.", this);
            }
        }

        private bool FailLockedTransition(string reason)
        {
            _phaseTransaction?.SetCurrentInputEnabled(false);
            Debug.LogError($"[GameFlow][Transition] {reason} Session remains Transitioning, timer paused, and all phase input disabled.", this);
            return false;
        }

        private void FailInitialization(string reason)
        {
            DisableSerializedPhaseInputs();
            _registry?.DisableAllInput();
            _phaseTransaction?.SetCurrentInputEnabled(false);
            Debug.LogError($"[GameFlow][Session] Initialization failed while Preparing: {reason}", this);
        }

        private void DisableSerializedPhaseInputs()
        {
            if (phases == null) return;
            for (int i = 0; i < phases.Length; i++)
            {
                if (!(phases[i] is IGamePhase phase)) continue;
                try { phase.SetInputEnabled(false); }
                catch (Exception) { }
            }
        }
    }
}
