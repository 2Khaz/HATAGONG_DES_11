using System;
using UnityEngine;

namespace HATAGONG.GameFlow
{
    public sealed class PhaseTransitionController : MonoBehaviour
    {
        [SerializeField] private PhaseTransitionOverlay overlay;
        [SerializeField] private PhaseHUDPresenter phaseHud;

        public bool IsTransitioning => overlay && overlay.IsPlaying;
        public PhaseTransitionResult LastResult { get; private set; }
        public Exception LastError { get; private set; }
        public float ConfiguredDuration => overlay ? overlay.TotalConfiguredDuration : 0f;

        public bool PlayPreparedTransition(
            GamePhaseId cleared,
            GamePhaseId next,
            Func<bool> midpointCommit,
            Action<PhaseTransitionResult, bool, Exception> completed)
        {
            LastError = null;
            if (IsTransitioning || !overlay || !phaseHud)
            {
                LastResult = PhaseTransitionResult.Rejected;
                return false;
            }

            LastResult = PhaseTransitionResult.None;
            bool midpointCalled = false;
            bool started = overlay.Play(
                cleared,
                () =>
                {
                    if (midpointCalled) return false;
                    midpointCalled = true;
                    if (midpointCommit != null && !midpointCommit()) return false;
                    phaseHud.SetPhase(next);
                    return true;
                },
                (result, midpointSucceeded, error) =>
                {
                    LastResult = result;
                    LastError = error;
                    completed?.Invoke(result, midpointSucceeded, error);
                });

            if (!started)
            {
                LastResult = PhaseTransitionResult.Rejected;
            }
            return started;
        }
    }
}
