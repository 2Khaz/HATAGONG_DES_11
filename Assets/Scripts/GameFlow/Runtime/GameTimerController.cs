using HATAGONG.Phase1;
using UnityEngine;

namespace HATAGONG.GameFlow
{
    public sealed class GameTimerController : MonoBehaviour
    {
        [SerializeField] private Phase1GameConfig config;
        [SerializeField] private bool startOnStart = true;
        private GameCountdownTimer timer;

        public GameCountdownTimer Timer => timer;
        public double RemainingSeconds => timer?.RemainingSeconds ?? 0d;
        public int DisplayedSeconds => timer?.DisplayedSeconds ?? 0;
        public GameTimerState CurrentState => timer?.CurrentState ?? GameTimerState.Expired;
        public bool IsRunning => timer?.IsRunning == true;
        public bool IsPaused => timer?.IsPaused == true;
        public bool IsExpired => timer?.IsExpired != false;

        private void Awake()
        {
            double duration = config ? config.OverallGameDurationSeconds : 0d;
            timer = new GameCountdownTimer(duration);
            if (!config || !timer.HasValidDuration) Debug.LogError("[GameTimer] Missing config or invalid overall game duration. Timer entered 0 Expired state.", this);
        }

        private void Start() { if (startOnStart) StartTimer(); }
        private void Update() { timer?.Tick(Time.deltaTime); }

        public void StartTimer() => timer?.StartTimer();
        public void PauseTimer() => timer?.PauseTimer();
        public void ResumeTimer() => timer?.ResumeTimer();
        public void ResetTimer() => timer?.ResetTimer();
        public void ResetAndStartTimer() { timer?.ResetTimer(); timer?.StartTimer(); }
        public void StopTimer() => timer?.StopTimer();
    }
}
