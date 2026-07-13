using System;

namespace HATAGONG.GameFlow
{
    public static class GamePhaseTransitionCompletion
    {
        public static bool ShouldResume(PhaseTransitionResult result, bool midpointSucceeded, bool currentIsNext)
        {
            return result == PhaseTransitionResult.Succeeded ||
                   (result == PhaseTransitionResult.Interrupted && midpointSucceeded && currentIsNext);
        }
    }

    public sealed class GamePhaseExitReadyGate
    {
        private readonly System.Collections.Generic.HashSet<GamePhaseId> _handled = new System.Collections.Generic.HashSet<GamePhaseId>();

        public bool TryAccept(IGamePhase signaledPhase, IGamePhase currentPhase, GameSessionState sessionState)
        {
            return signaledPhase != null &&
                   signaledPhase == currentPhase &&
                   signaledPhase.IsExitReady &&
                   sessionState == GameSessionState.Playing &&
                   _handled.Add(signaledPhase.PhaseId);
        }
    }

    public sealed class GamePhaseTransaction
    {
        private readonly GamePhaseRegistry _registry;

        public GamePhaseTransaction(GamePhaseRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public IGamePhase CurrentPhase { get; private set; }

        public bool TryInitialize(GamePhaseId initialPhaseId, GameRunContext context, out string error)
        {
            error = null;
            _registry.DisableAllInput();
            if (!context.IsValid)
            {
                error = "Game run context is invalid.";
                return false;
            }
            if (!_registry.TryGet(initialPhaseId, out IGamePhase initial))
            {
                error = $"Initial phase is missing: {initialPhaseId}.";
                return false;
            }
            if (!TryPrepare(initial, context, out error) || !TryActivate(initial, out error))
            {
                SafeDeactivate(initial);
                _registry.DisableAllInput();
                return false;
            }

            CurrentPhase = initial;
            return true;
        }

        public bool TryPrepareNext(IGamePhase next, GameRunContext context, out string error)
        {
            error = null;
            _registry.DisableAllInput();
            if (next != null && TryPrepare(next, context, out error)) return true;
            SafeDeactivate(next);
            _registry.DisableAllInput();
            return false;
        }

        public bool TryCommitPreparedNext(IGamePhase next, out string error)
        {
            error = null;
            if (next == null || !next.IsPrepared || next.IsRunning)
            {
                error = "Next phase is not in the prepared inactive state.";
                return false;
            }

            IGamePhase previous = CurrentPhase;
            if (!TryActivate(next, out error))
            {
                SafeDeactivate(next);
                _registry.DisableAllInput();
                return false;
            }

            previous?.Deactivate();
            CurrentPhase = next;
            _registry.DisableAllInput();
            return true;
        }

        public void SetCurrentInputEnabled(bool enabled)
        {
            _registry.DisableAllInput();
            if (enabled)
            {
                CurrentPhase?.SetInputEnabled(true);
            }
        }

        private static bool TryPrepare(IGamePhase phase, GameRunContext context, out string error)
        {
            error = null;
            try
            {
                phase.SetInputEnabled(false);
                if (!phase.Prepare(context) || !phase.IsPrepared || phase.IsRunning)
                {
                    error = $"Phase prepare failed: {phase.PhaseId}.";
                    return false;
                }
                phase.SetInputEnabled(false);
                return true;
            }
            catch (Exception exception)
            {
                error = $"Phase prepare threw for {phase.PhaseId}: {exception}";
                return false;
            }
        }

        private static bool TryActivate(IGamePhase phase, out string error)
        {
            error = null;
            try
            {
                if (!phase.Activate() || !phase.IsRunning)
                {
                    error = $"Phase activate failed: {phase.PhaseId}.";
                    return false;
                }
                phase.SetInputEnabled(false);
                return true;
            }
            catch (Exception exception)
            {
                error = $"Phase activate threw for {phase.PhaseId}: {exception}";
                return false;
            }
        }

        private static void SafeDeactivate(IGamePhase phase)
        {
            if (phase == null) return;
            try { phase.Deactivate(); }
            catch (Exception) { }
            try { phase.SetInputEnabled(false); }
            catch (Exception) { }
        }
    }
}
