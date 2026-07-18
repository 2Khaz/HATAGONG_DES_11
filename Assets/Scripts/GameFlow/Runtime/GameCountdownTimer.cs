using System;

namespace HATAGONG.GameFlow
{
    public sealed class GameCountdownTimer
    {
        private double durationSeconds;
        private bool expirationRaised;

        public GameCountdownTimer(double durationSeconds)
        {
            this.durationSeconds = IsValidDuration(durationSeconds) ? durationSeconds : 0d;
            RemainingSeconds = this.durationSeconds;
            CurrentState = this.durationSeconds > 0d ? GameTimerState.Idle : GameTimerState.Expired;
        }

        public event Action<int> DisplayedSecondChanged;
        public event Action TimerExpired;
        public event Action<GameTimerState> TimerStateChanged;

        public double RemainingSeconds { get; private set; }
        public int DisplayedSeconds => ToDisplayedSeconds(RemainingSeconds);
        public GameTimerState CurrentState { get; private set; }
        public bool IsRunning => CurrentState == GameTimerState.Running;
        public bool IsPaused => CurrentState == GameTimerState.Paused;
        public bool IsExpired => CurrentState == GameTimerState.Expired;
        public bool HasValidDuration => durationSeconds > 0d;
        public double DurationSeconds => durationSeconds;

        public bool TrySetDuration(double seconds)
        {
            if (!IsValidDuration(seconds)) return false;
            durationSeconds = seconds;
            ResetTimer();
            return true;
        }

        public void StartTimer()
        {
            if ((CurrentState == GameTimerState.Idle || CurrentState == GameTimerState.Paused) && RemainingSeconds > 0d)
                ChangeState(GameTimerState.Running);
        }

        public void PauseTimer()
        {
            if (CurrentState == GameTimerState.Running) ChangeState(GameTimerState.Paused);
        }

        public void ResumeTimer()
        {
            if (CurrentState == GameTimerState.Paused) ChangeState(GameTimerState.Running);
        }

        public void ResetTimer()
        {
            int previousDisplay = DisplayedSeconds;
            RemainingSeconds = durationSeconds;
            expirationRaised = false;
            ChangeState(durationSeconds > 0d ? GameTimerState.Idle : GameTimerState.Expired);
            RaiseDisplayIfChanged(previousDisplay);
        }

        public void StopTimer()
        {
            if (CurrentState == GameTimerState.Running || CurrentState == GameTimerState.Paused)
                ChangeState(GameTimerState.Idle);
        }

        public void Tick(double deltaSeconds)
        {
            if (CurrentState != GameTimerState.Running || double.IsNaN(deltaSeconds) || double.IsInfinity(deltaSeconds) || deltaSeconds <= 0d) return;
            int previousDisplay = DisplayedSeconds;
            RemainingSeconds = Math.Max(0d, RemainingSeconds - deltaSeconds);
            RaiseDisplayIfChanged(previousDisplay);
            if (RemainingSeconds <= 0d) Expire();
        }

        public bool TryAddSeconds(double seconds, double maximumSeconds)
        {
            if (CurrentState != GameTimerState.Running || seconds <= 0d || maximumSeconds <= 0d ||
                double.IsNaN(seconds) || double.IsInfinity(seconds) ||
                double.IsNaN(maximumSeconds) || double.IsInfinity(maximumSeconds) ||
                RemainingSeconds >= maximumSeconds) return false;
            int previousDisplay = DisplayedSeconds;
            double next = Math.Min(maximumSeconds, RemainingSeconds + seconds);
            if (next <= RemainingSeconds) return false;
            RemainingSeconds = next;
            RaiseDisplayIfChanged(previousDisplay);
            return true;
        }

        public static int ToDisplayedSeconds(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsNegativeInfinity(seconds) || seconds <= 0d) return 0;
            if (double.IsPositiveInfinity(seconds)) return int.MaxValue;
            return seconds >= int.MaxValue ? int.MaxValue : (int)Math.Ceiling(seconds);
        }

        public static string FormatSeconds(int seconds)
        {
            return Math.Max(0, seconds).ToString();
        }

        private static bool IsValidDuration(double seconds) => !double.IsNaN(seconds) && !double.IsInfinity(seconds) && seconds > 0d;
        private void RaiseDisplayIfChanged(int previousDisplay) { if (previousDisplay != DisplayedSeconds) DisplayedSecondChanged?.Invoke(DisplayedSeconds); }
        private void ChangeState(GameTimerState state) { if (CurrentState == state) return; CurrentState = state; TimerStateChanged?.Invoke(state); }
        private void Expire()
        {
            RemainingSeconds = 0d;
            ChangeState(GameTimerState.Expired);
            if (expirationRaised) return;
            expirationRaised = true;
            TimerExpired?.Invoke();
        }
    }
}
