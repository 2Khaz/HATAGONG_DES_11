using System;
using System.Collections.Generic;
using UnityEngine;

namespace HATAGONG.GameFlow
{
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
        private GamePhaseRegistry _registry;
        private GamePhaseTransaction _phaseTransaction;
        private GameRunContext _runContext;
        private IGamePhase _pendingNextPhase;
        private double _transitionStartedAt;

        public GameSessionState CurrentState => _model.CurrentState;
        public bool CanAcceptGameplayInput => _model.CanAcceptGameplayInput;
        public bool CanAddScore => _model.CanAddScore;
        public bool IsTransitioning => _model.IsTransitioning;
        public bool IsExpired => _model.IsExpired;
        public bool IsCompleted => _model.IsCompleted;
        public GameRunContext RunContext => _runContext;
        public IGamePhase CurrentPhase => _phaseTransaction?.CurrentPhase;
        public double LastTransitionElapsedSeconds { get; private set; }

        public event Action<GameSessionState> SessionStateChanged { add => _model.SessionStateChanged += value; remove => _model.SessionStateChanged -= value; }
        public event Action GameExpired { add => _model.GameExpired += value; remove => _model.GameExpired -= value; }
        public event Action GameCompleted { add => _model.GameCompleted += value; remove => _model.GameCompleted -= value; }

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

            if (!GamePhaseRegistry.TryCreate(phases, initialPhase, out _registry, out string registryError))
            {
                FailInitialization(registryError);
                return;
            }

            _registry.DisableAllInput();
            _runContext = new GameRunContext(difficulty);
            if (!_runContext.IsValid)
            {
                FailInitialization($"Invalid game difficulty: {difficulty}.");
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
            if (!_model.SetState(GameSessionState.Expired)) return;
            _phaseTransaction?.SetCurrentInputEnabled(false);
            score.LockScore();
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
