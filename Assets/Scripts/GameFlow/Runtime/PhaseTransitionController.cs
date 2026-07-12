using System;
using UnityEngine;
namespace HATAGONG.GameFlow
{
    public sealed class PhaseTransitionController:MonoBehaviour
    {
        [SerializeField]private GameSessionController session;[SerializeField]private PhaseTransitionOverlay overlay;[SerializeField]private PhaseHUDPresenter phaseHud;
        public bool IsTransitioning=>overlay&&overlay.IsPlaying;public PhaseTransitionResult LastResult{get;private set;}public Exception LastError{get;private set;}
        public bool PlayPhaseClearTransition(GamePhaseId cleared,GamePhaseId next,Action midpointAction=null,Action completedAction=null)
            =>PlayPhaseClearTransitionChecked(cleared,next,()=>{midpointAction?.Invoke();return true;},completedAction);
        public bool PlayPhaseClearTransitionChecked(GamePhaseId cleared,GamePhaseId next,Func<bool> midpointAction,Action completedAction=null)
        {
            LastError=null;if(IsTransitioning||!session||!overlay||!phaseHud||!session.BeginTransition()){LastResult=PhaseTransitionResult.Rejected;return false;}LastResult=PhaseTransitionResult.None;bool midpointCalled=false;
            bool started=overlay.Play(cleared,()=>{if(midpointCalled)return false;midpointCalled=true;if(midpointAction!=null&&!midpointAction())return false;phaseHud.SetPhase(next);return true;},(result,midpointSucceeded,error)=>HandleFinished(result,error,completedAction));
            if(!started){LastResult=PhaseTransitionResult.Rejected;session.FinishTransition();}return started;
        }
        private void HandleFinished(PhaseTransitionResult result,Exception error,Action completedAction){LastResult=result;LastError=error;if(result==PhaseTransitionResult.Failed){Debug.LogError($"[GameFlow][Transition] Midpoint failed. Session remains locked: {error}");return;}session.FinishTransition();if(result!=PhaseTransitionResult.Succeeded||completedAction==null)return;try{completedAction();}catch(Exception e){LastError=e;Debug.LogError($"[GameFlow][Transition] Completed callback failed after successful resume: {e}");}}
    }
}
