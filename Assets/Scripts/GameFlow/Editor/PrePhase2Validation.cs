#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using HATAGONG.Phase1;
using System.Reflection;
namespace HATAGONG.GameFlow.Editor
{
    public static class PrePhase2Validation
    {
        [MenuItem("Tools/HATAGONG/Game Flow/Validate Pre-Phase2 Models")]
        public static void Validate()
        {
            int pass=0,total=0;void Check(bool value,string name){total++;if(!value)throw new InvalidOperationException("[PrePhase2][Test] "+name);pass++;}
            var model=new GameSessionModel();int changes=0,expired=0,completed=0;model.SessionStateChanged+=_=>changes++;model.GameExpired+=()=>expired++;model.GameCompleted+=()=>completed++;
            Check(model.CurrentState==GameSessionState.Preparing&&!model.CanAcceptGameplayInput,"preparing");
            Check(!model.SetState(GameSessionState.Playing)&&changes==0,"invalid preparing to playing");
            Check(model.SetState(GameSessionState.Ready)&&!model.CanAddScore,"ready");
            Check(!model.SetState(GameSessionState.Ready)&&changes==1,"duplicate state");
            Check(model.SetState(GameSessionState.Playing)&&model.CanAcceptGameplayInput&&model.CanAddScore,"playing");
            Check(model.SetState(GameSessionState.Transitioning)&&model.IsTransitioning&&!model.CanAddScore,"transitioning");
            Check(model.SetState(GameSessionState.Playing),"transition complete");
            Check(model.SetState(GameSessionState.Expired)&&expired==1&&model.IsExpired,"expired");
            Check(!model.SetState(GameSessionState.Playing)&&expired==1,"expired terminal");
            var complete=new GameSessionModel();complete.SetState(GameSessionState.Ready);complete.SetState(GameSessionState.Playing);complete.GameCompleted+=()=>completed++;Check(complete.SetState(GameSessionState.Completed)&&complete.IsCompleted&&completed==1,"completed");Check(!complete.CanAddScore&&!complete.CanAcceptGameplayInput&&!complete.SetState(GameSessionState.Playing),"completed terminal");
            Debug.Log($"[PrePhase2][Test] result={pass}/{total}, failures=0");
        }
        [MenuItem("Tools/HATAGONG/Game Flow/Validate Request Icons")]
        public static void ValidateRequestIcons()
        {
            int pass=0,total=0;void Check(bool value,string name){total++;if(!value)throw new InvalidOperationException("[RequestIcon][Test] "+name);pass++;}
            var presenter=UnityEngine.Object.FindFirstObjectByType<RequestPresenter>();var context=UnityEngine.Object.FindFirstObjectByType<GameRequestContext>();var image=GameObject.Find("Canvas/Game_UI_General/Top_HUD/Request_Content/Request_Icon")?.GetComponent<Image>();var text=GameObject.Find("Canvas/Game_UI_General/Top_HUD/Request_Content/Request_Text")?.GetComponent<TextMeshProUGUI>();
            Check(presenter&&context&&image&&text,"references");Check(presenter.NormalIcon,"normal slot");Check(presenter.SuddenIcon,"sudden slot");Check(presenter.NormalIcon!=presenter.SuddenIcon,"distinct sprites");
            var sequence=new[]{RequestType.Normal,RequestType.Sudden,RequestType.Normal,RequestType.Sudden,RequestType.Normal};foreach(var type in sequence){context.SetRequest(type);presenter.Present(type);var expected=type==RequestType.Normal?presenter.NormalIcon:presenter.SuddenIcon;Check(image.sprite==expected&&image.sprite&&image.color==Color.white&&text.text==(type==RequestType.Normal?"NORMAL REQUEST":"SUDDEN REQUEST"),"transition "+type);}
            presenter.enabled=false;presenter.enabled=true;Check(image.sprite==presenter.NormalIcon&&text.text=="NORMAL REQUEST","reenable restore");Check(image.sprite.name=="Img_icon_normal","normal sprite name");presenter.Present(RequestType.Sudden);Check(image.sprite.name=="Img_icon_sudden","sudden sprite name");context.SetRequest(RequestType.Normal);presenter.Present(RequestType.Normal);
            Debug.Log($"[RequestIcon][Test] result={pass}/{total}, failures=0, normal={presenter.NormalIcon.name}, sudden={presenter.SuddenIcon.name}");
        }
        [MenuItem("Tools/HATAGONG/Game Flow/Validate Pre-Phase2 Risk Boundaries")]
        public static void ValidateRiskBoundaries()
        {
            int pass=0,total=0;void Check(bool value,string name){total++;if(!value)throw new InvalidOperationException("[RiskReview][Test] "+name);pass++;}
            var starts=UnityEngine.Object.FindObjectsByType<GameStartOverlay>(FindObjectsInactive.Include,FindObjectsSortMode.None);var transitions=UnityEngine.Object.FindObjectsByType<PhaseTransitionOverlay>(FindObjectsInactive.Include,FindObjectsSortMode.None);var sessions=UnityEngine.Object.FindObjectsByType<GameSessionController>(FindObjectsInactive.Include,FindObjectsSortMode.None);var scores=UnityEngine.Object.FindObjectsByType<GameScoreController>(FindObjectsInactive.Include,FindObjectsSortMode.None);var timers=UnityEngine.Object.FindObjectsByType<GameTimerController>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            Check(starts.Length==1,"single start overlay");Check(transitions.Length==1,"single transition overlay");Check(sessions.Length==1,"single session");Check(scores.Length==1,"single score");Check(timers.Length==1,"single timer");var start=starts[0];Check(Mathf.Approximately(start.ReadyDuration,.85f),"ready duration");Check(Mathf.Approximately(start.ReadyToGoGap,.15f),"ready gap");Check(Mathf.Approximately(start.GoDuration,.45f),"go duration");Check(Mathf.Approximately(start.FadeDuration,.2f),"fade duration");
            var request=UnityEngine.Object.FindFirstObjectByType<RequestPresenter>();Check(request&&request.NormalIcon&&request.SuddenIcon&&request.NormalIcon!=request.SuddenIcon,"request references");var model=new GameSessionModel();model.SetState(GameSessionState.Ready);Check(!model.SetState(GameSessionState.Transitioning),"ready transition blocked");model.SetState(GameSessionState.Playing);model.SetState(GameSessionState.Expired);Check(!model.SetState(GameSessionState.Transitioning)&&!model.SetState(GameSessionState.Playing),"expired terminal");
            Debug.Log($"[RiskReview][Test] result={pass}/{total}, failures=0");
        }
        [MenuItem("Tools/HATAGONG/Game Flow/Validate Final Safety Contracts")]
        public static void ValidateFinalSafetyContracts()
        {
            int pass=0,total=0;void Check(bool value,string name){total++;if(!value)throw new InvalidOperationException("[FinalSafety][Test] "+name);pass++;}
            var board=UnityEngine.Object.FindFirstObjectByType<Phase1BoardController>();var boardSo=new SerializedObject(board);Check(boardSo.FindProperty("sessionController").objectReferenceValue,"board session gate");Check(boardSo.FindProperty("inputController").objectReferenceValue,"board input gate");Check(typeof(Phase1BoardController).GetMethod("TryHitDetailed")!=null,"detailed hit result");Check(Enum.GetValues(typeof(Phase1HitResult)).Length==5,"hit result states");Check(Enum.GetValues(typeof(PhaseTransitionResult)).Length==5,"transition result states");
            var execute=typeof(PhaseTransitionOverlay).GetMethod("TryExecuteMidpoint",BindingFlags.Static|BindingFlags.NonPublic);object[] failed={new Func<bool>(()=>false),null};Check(!(bool)execute.Invoke(null,failed)&&failed[1]==null,"explicit midpoint failure");object[] thrown={new Func<bool>(()=>throw new InvalidOperationException("validation")),null};Check(!(bool)execute.Invoke(null,thrown)&&thrown[1] is InvalidOperationException,"midpoint exception");Check(typeof(PhaseTransitionController).GetProperty("LastResult")!=null&&typeof(PhaseTransitionController).GetProperty("LastError")!=null,"transition diagnostics");
            Debug.Log($"[FinalSafety][Test] result={pass}/{total}, failures=0");
        }
    }
}
#endif
