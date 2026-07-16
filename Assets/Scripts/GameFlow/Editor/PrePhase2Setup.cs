#if UNITY_EDITOR
using HATAGONG.GameFlow;
using HATAGONG.Phase1;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
namespace HATAGONG.GameFlowEditor
{
    public static class PrePhase2Setup
    {
        [MenuItem("Tools/HATAGONG/Game Flow/Setup Pre-Phase2 Framework")]
        public static void Setup()
        {
            var canvas=Require("Canvas");var general=Require("Canvas/Game_UI_General");var field=Require("Canvas/Game_UI_General/Middle_GamePanel/Phase1_FieldRoot");
            var scoreText=Require("Canvas/Game_UI_General/Top_HUD/Score_Content/Score_Value").GetComponent<TextMeshProUGUI>();
            var requestContent=Require("Canvas/Game_UI_General/Top_HUD/Request_Content");var requestText=Require("Canvas/Game_UI_General/Top_HUD/Request_Content/Request_Text").GetComponent<TextMeshProUGUI>();var requestIcon=Require("Canvas/Game_UI_General/Top_HUD/Request_Content/Request_Icon").GetComponent<Image>();
            var phaseContent=Require("Canvas/Game_UI_General/Top_HUD/Phase_Content");var phaseText=Require("Canvas/Game_UI_General/Top_HUD/Phase_Content/Phase_TitleArea/Phase_Text").GetComponent<TextMeshProUGUI>();var phaseDialog=Require("Canvas/Game_UI_General/Top_HUD/Phase_Content/Phase_Dialog").GetComponent<TextMeshProUGUI>();
            var dots=new[]{Require("Canvas/Game_UI_General/Top_HUD/Phase_Content/Phase_DotArea/Phase1_Dot").GetComponent<Image>(),Require("Canvas/Game_UI_General/Top_HUD/Phase_Content/Phase_DotArea/Phase2_Dot").GetComponent<Image>(),Require("Canvas/Game_UI_General/Top_HUD/Phase_Content/Phase_DotArea/Phase3_Dot").GetComponent<Image>()};
            var board=field.GetComponent<Phase1BoardController>();var input=field.GetComponent<Phase1InputController>();var legacyScore=field.GetComponent<Phase1ScoreController>();var timer=general.GetComponent<GameTimerController>();
            var adapter=Add<Phase1PhaseAdapter>(field);var session=Add<GameSessionController>(general);var score=Add<GameScoreController>(general);var request=Add<GameRequestContext>(general);var transition=Add<PhaseTransitionController>(general);
            var requestPresenter=Add<RequestPresenter>(requestContent);var phasePresenter=Add<PhaseHUDPresenter>(phaseContent);
            Set(adapter,("board",board),("input",input));Set(legacyScore,("gameScore",score));Set(board,("sessionController",session));
            var normalIcon=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/resource/Img_icon_normal.png");var suddenIcon=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/resource/Img_icon_sudden.png");if(!normalIcon||!suddenIcon)throw new System.InvalidOperationException("Required Request icon Sprite asset is missing.");
            Set(score,("scoreValueText",scoreText),("session",session));Set(requestPresenter,("context",request),("requestText",requestText),("requestIcon",requestIcon),("normalIcon",normalIcon),("suddenIcon",suddenIcon));
            var activeDot=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Ingame/ICON/PhaseICON/Img_icon_phaseOn.png");var inactiveDot=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Ingame/ICON/PhaseICON/Img_icon_phaseOff.png");
            Set(phasePresenter,("phaseText",phaseText),("phaseDescription",phaseDialog),("activeDotSprite",activeDot),("inactiveDotSprite",inactiveDot));SetArray(phasePresenter,"dots",dots);SetStringArray(phasePresenter,"descriptions",new[]{"철 거","도 포","시 공"});
            var transitionRoot=EnsureOverlayRoot(canvas,phaseText.font);
            var startOverlay=transitionRoot.transform.Find("StartOverlay").GetComponent<GameStartOverlay>();var phaseOverlay=transitionRoot.transform.Find("PhaseTransitionOverlay").GetComponent<PhaseTransitionOverlay>();
            Set(session,("timer",timer),("score",score),("startOverlay",startOverlay),("transition",transition));SetArray(session,"phases",new Object[]{adapter});SetInt(session,"difficulty",(int)GameDifficulty.Hard);SetInt(session,"initialPhase",(int)GamePhaseId.Phase1);Set(transition,("overlay",phaseOverlay),("phaseHud",phasePresenter));
            SetBool(timer,"startOnStart",false);SetBool(board,"generateOnStart",false);
            phasePresenter.SetPhase(GamePhaseId.Phase1);requestPresenter.Present(RequestType.Normal);
            EditorUtility.SetDirty(general);EditorUtility.SetDirty(field);EditorSceneManager.MarkSceneDirty(canvas.scene);EditorSceneManager.SaveScene(canvas.scene);Debug.Log("[GameFlow] Pre-Phase2 framework setup completed.");
        }
        private static GameObject EnsureOverlayRoot(GameObject canvas,TMP_FontAsset font)
        {
            var root=FindOrCreate(canvas,"Game_UI_Transition");Stretch(root.GetComponent<RectTransform>());root.transform.SetAsLastSibling();
            var start=FindOrCreate(root,"StartOverlay");Stretch(start.GetComponent<RectTransform>());var startGroup=Add<CanvasGroup>(start);startGroup.alpha=1;startGroup.blocksRaycasts=true;
            var startDim=FindOrCreate(start,"BackgroundDim");Stretch(startDim.GetComponent<RectTransform>());var startImage=Add<Image>(startDim);startImage.color=new Color(0,0,0,.5f);startImage.raycastTarget=true;
            var startMessage=FindOrCreate(start,"StartMessage");Stretch(startMessage.GetComponent<RectTransform>());var startText=Add<TextMeshProUGUI>(startMessage);ConfigureText(startText,font,72);
            var startOverlay=Add<GameStartOverlay>(start);Set(startOverlay,("canvasGroup",startGroup),("messageText",startText));
            SetFloat(startOverlay,"readyDuration",.85f);SetFloat(startOverlay,"readyToGoGap",.15f);SetFloat(startOverlay,"goDuration",.45f);SetFloat(startOverlay,"fadeDuration",.2f);
            var phase=FindOrCreate(root,"PhaseTransitionOverlay");Stretch(phase.GetComponent<RectTransform>());var phaseGroup=Add<CanvasGroup>(phase);phaseGroup.alpha=0;phaseGroup.blocksRaycasts=false;
            var phaseDim=FindOrCreate(phase,"BackgroundDim");Stretch(phaseDim.GetComponent<RectTransform>());var phaseImage=Add<Image>(phaseDim);phaseImage.color=new Color(0,0,0,.2f);phaseImage.raycastTarget=true;
            var banner=FindOrCreate(phase,"TransitionBanner");var bannerRt=banner.GetComponent<RectTransform>();bannerRt.anchorMin=bannerRt.anchorMax=bannerRt.pivot=new Vector2(.5f,.5f);bannerRt.sizeDelta=new Vector2(1000,180);
            var bannerImage=Add<Image>(banner);bannerImage.color=new Color(.08f,.2f,.42f,.95f);bannerImage.raycastTarget=false;
            var message=FindOrCreate(banner,"MessageText");Stretch(message.GetComponent<RectTransform>());var messageText=Add<TextMeshProUGUI>(message);ConfigureText(messageText,font,58);
            var phaseOverlay=Add<PhaseTransitionOverlay>(phase);Set(phaseOverlay,("canvasGroup",phaseGroup),("banner",bannerRt),("messageText",messageText));return root;
        }
        private static void ConfigureText(TextMeshProUGUI text,TMP_FontAsset font,float size){text.font=font;text.fontSize=size;text.alignment=TextAlignmentOptions.Center;text.color=Color.white;text.raycastTarget=false;}
        private static GameObject FindOrCreate(GameObject parent,string name){var child=parent.transform.Find(name);if(child)return child.gameObject;var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent.transform,false);return go;}
        private static void Stretch(RectTransform rt){rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;rt.localScale=Vector3.one;}
        private static GameObject Require(string path){var go=GameObject.Find(path);if(!go)throw new System.InvalidOperationException("Required object missing: "+path);return go;}
        private static T Add<T>(GameObject go)where T:Component{return go.GetComponent<T>()?go.GetComponent<T>():Undo.AddComponent<T>(go);}
        private static void Set(Component target,params(string,Object)[] values){var so=new SerializedObject(target);foreach(var item in values){var p=so.FindProperty(item.Item1);if(p==null)throw new System.InvalidOperationException(target.GetType().Name+"."+item.Item1+" missing");p.objectReferenceValue=item.Item2;}so.ApplyModifiedProperties();}
        private static void SetArray(Component target,string name,Object[] values){var so=new SerializedObject(target);var p=so.FindProperty(name);p.arraySize=values.Length;for(int i=0;i<values.Length;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=values[i];so.ApplyModifiedProperties();}
        private static void SetStringArray(Component target,string name,string[] values){var so=new SerializedObject(target);var p=so.FindProperty(name);p.arraySize=values.Length;for(int i=0;i<values.Length;i++)p.GetArrayElementAtIndex(i).stringValue=values[i];so.ApplyModifiedProperties();}
        private static void SetColor(Component target,string name,Color value){var so=new SerializedObject(target);so.FindProperty(name).colorValue=value;so.ApplyModifiedProperties();}
        private static void SetBool(Component target,string name,bool value){var so=new SerializedObject(target);so.FindProperty(name).boolValue=value;so.ApplyModifiedProperties();}
        private static void SetInt(Component target,string name,int value){var so=new SerializedObject(target);so.FindProperty(name).intValue=value;so.ApplyModifiedProperties();}
        private static void SetFloat(Component target,string name,float value){var so=new SerializedObject(target);so.FindProperty(name).floatValue=value;so.ApplyModifiedProperties();}
    }
}
#endif
