using System;
using UnityEngine;
namespace HATAGONG.GameFlow
{
    public sealed class GameSessionController:MonoBehaviour
    {
        [SerializeField]private GameTimerController timer;
        [SerializeField]private GameScoreController score;
        [SerializeField]private GameStartOverlay startOverlay;
        [SerializeField]private Phase1PhaseAdapter phase1;
        private readonly GameSessionModel model=new();
        public GameSessionState CurrentState=>model.CurrentState;public bool CanAcceptGameplayInput=>model.CanAcceptGameplayInput;public bool CanAddScore=>model.CanAddScore;public bool IsTransitioning=>model.IsTransitioning;public bool IsExpired=>model.IsExpired;public bool IsCompleted=>model.IsCompleted;
        public event Action<GameSessionState> SessionStateChanged{add=>model.SessionStateChanged+=value;remove=>model.SessionStateChanged-=value;}
        public event Action GameExpired{add=>model.GameExpired+=value;remove=>model.GameExpired-=value;}
        public event Action GameCompleted{add=>model.GameCompleted+=value;remove=>model.GameCompleted-=value;}
        private void Start(){timer.Timer.TimerExpired+=OnTimerExpired;score.ResetForNewSession();timer.ResetTimer();phase1.SetInputEnabled(false);model.SetState(GameSessionState.Ready);startOverlay.Play(BeginPlaying);}
        private void OnDestroy(){if(timer&&timer.Timer!=null)timer.Timer.TimerExpired-=OnTimerExpired;}
        private void BeginPlaying(){if(!model.SetState(GameSessionState.Playing))return;timer.StartTimer();phase1.StartPhase();phase1.SetInputEnabled(true);}
        private void OnTimerExpired(){if(!model.SetState(GameSessionState.Expired))return;phase1.SetInputEnabled(false);score.LockScore();}
        public bool BeginTransition(){if(!model.SetState(GameSessionState.Transitioning))return false;phase1.SetInputEnabled(false);timer.PauseTimer();return true;}
        public void FinishTransition(){if(!model.SetState(GameSessionState.Playing))return;timer.ResumeTimer();phase1.SetInputEnabled(true);}
        public void CompleteGame(){if(!model.SetState(GameSessionState.Completed))return;phase1.SetInputEnabled(false);timer.StopTimer();score.LockScore();}
    }
}
