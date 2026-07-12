using System;
namespace HATAGONG.GameFlow
{
    public sealed class GameSessionModel
    {
        public GameSessionState CurrentState{get;private set;}=GameSessionState.Preparing;
        public bool CanAcceptGameplayInput=>CurrentState==GameSessionState.Playing;
        public bool CanAddScore=>CurrentState==GameSessionState.Playing;
        public bool IsTransitioning=>CurrentState==GameSessionState.Transitioning;
        public bool IsExpired=>CurrentState==GameSessionState.Expired;
        public bool IsCompleted=>CurrentState==GameSessionState.Completed;
        public event Action<GameSessionState> SessionStateChanged;
        public event Action GameExpired;
        public event Action GameCompleted;
        public bool SetState(GameSessionState next)
        {
            if(CurrentState==next||CurrentState==GameSessionState.Expired||CurrentState==GameSessionState.Completed)return false;
            bool valid=(CurrentState==GameSessionState.Preparing&&next==GameSessionState.Ready)||(CurrentState==GameSessionState.Ready&&next==GameSessionState.Playing)||(CurrentState==GameSessionState.Playing&&(next==GameSessionState.Transitioning||next==GameSessionState.Expired||next==GameSessionState.Completed))||(CurrentState==GameSessionState.Transitioning&&(next==GameSessionState.Playing||next==GameSessionState.Expired||next==GameSessionState.Completed));
            if(!valid)return false;CurrentState=next;SessionStateChanged?.Invoke(next);if(next==GameSessionState.Expired)GameExpired?.Invoke();if(next==GameSessionState.Completed)GameCompleted?.Invoke();return true;
        }
    }
}
