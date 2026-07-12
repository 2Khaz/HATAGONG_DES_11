#if UNITY_EDITOR
using HATAGONG.Phase1;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

namespace HATAGONG.Phase1Editor
{
    public static class Phase1PrototypeSetup
    {
        [InitializeOnLoadMethod]
        private static void SetupOnceAfterReload()
        {
            if (AssetDatabase.LoadAssetAtPath<Phase1GameConfig>("Assets/Settings/Phase1/Phase1GameConfig.asset")) return;
            EditorApplication.delayCall += Setup;
        }

        [MenuItem("Tools/HATAGONG/Phase1/Setup Prototype")]
        public static void Setup()
        {
            EnsureFolder("Assets/Prefabs"); EnsureFolder("Assets/Prefabs/Phase1"); EnsureFolder("Assets/Settings"); EnsureFolder("Assets/Settings/Phase1");
            const string configPath="Assets/Settings/Phase1/Phase1GameConfig.asset";
            var config=AssetDatabase.LoadAssetAtPath<Phase1GameConfig>(configPath);
            if(!config){config=ScriptableObject.CreateInstance<Phase1GameConfig>();config.EnsureDefaults();AssetDatabase.CreateAsset(config,configPath);}config.EnsureDefaults();EditorUtility.SetDirty(config);
            const string prefabPath="Assets/Prefabs/Phase1/Phase1_TilePrefab.prefab";
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if(!prefab)prefab=CreatePrefab(prefabPath);

            var field=Require("Canvas/Game_UI_General/Middle_GamePanel/Phase1_FieldRoot");
            var tiles=Require("Canvas/Game_UI_General/Middle_GamePanel/Phase1_FieldRoot/Phase1_TileContainer");
            var effects=Require("Canvas/Game_UI_General/Middle_GamePanel/Phase1_FieldRoot/Phase1_EffectRoot");
            var popups=Require("Canvas/Game_UI_General/Middle_GamePanel/Phase1_FieldRoot/Phase1_ScorePopupRoot");
            var score=Require("Canvas/Game_UI_General/Top_HUD/Score_Content/Score_Value");
            var diffText=Require("Canvas/Game_UI_General/Top_HUD/Difficulty_Content/Difficulty_Text");
            var star1=Require("Canvas/Game_UI_General/Top_HUD/Difficulty_Content/Difficulty_StarArea/Diff01_Star");
            var star2=Require("Canvas/Game_UI_General/Top_HUD/Difficulty_Content/Difficulty_StarArea/Diff02_Star");
            var star3=Require("Canvas/Game_UI_General/Top_HUD/Difficulty_Content/Difficulty_StarArea/Diff03_Star");
            var board=Add<Phase1BoardController>(field);var input=Add<Phase1InputController>(field);var scoreCtrl=Add<Phase1ScoreController>(field);var hud=Add<Phase1HUDPresenter>(field);var feedback=Add<Phase1FeedbackController>(field);var audio=Add<AudioSource>(field);
            audio.playOnAwake=false;audio.loop=false;audio.spatialBlend=0;audio.volume=1;
            Set(board,("fieldRoot",field.GetComponent<RectTransform>()),("tileContainer",tiles.GetComponent<RectTransform>()),("effectRoot",effects.GetComponent<RectTransform>()),("scorePopupRoot",popups.GetComponent<RectTransform>()),("tilePrefab",prefab.GetComponent<Phase1TileView>()),("config",config),("inputController",input),("scoreController",scoreCtrl),("feedbackController",feedback),("hudPresenter",hud));
            Set(input,("boardController",board));Set(scoreCtrl,("scoreValueText",score.GetComponent<TextMeshProUGUI>()));
            Set(hud,("difficultyText",diffText.GetComponent<TextMeshProUGUI>()));var hso=new SerializedObject(hud);var stars=hso.FindProperty("difficultyStars");stars.arraySize=3;stars.GetArrayElementAtIndex(0).objectReferenceValue=star1.GetComponent<Image>();stars.GetArrayElementAtIndex(1).objectReferenceValue=star2.GetComponent<Image>();stars.GetArrayElementAtIndex(2).objectReferenceValue=star3.GetComponent<Image>();hso.ApplyModifiedProperties();
            hso=new SerializedObject(hud);hso.FindProperty("filledStarSprite").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/resource/Img_icon_star2.png");hso.FindProperty("emptyStarSprite").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/resource/Img_icon_star1.png");hso.ApplyModifiedProperties();
            Set(feedback,("audioSource",audio),("config",config));
            EditorUtility.SetDirty(field);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(field.scene);EditorSceneManager.SaveScene(field.scene);Debug.Log("[Phase1] Prototype setup and scene save completed.");
        }
        [MenuItem("Tools/HATAGONG/Phase1/Validate Generation Matrix")]
        public static void ValidateGenerationMatrix()
        {
            var config=AssetDatabase.LoadAssetAtPath<Phase1GameConfig>("Assets/Settings/Phase1/Phase1GameConfig.asset");if(!config||!config.ValidateAllBags())throw new System.InvalidOperationException("Phase1 config or bag validation failed.");
            var generator=new Phase1BoardGenerator(config);int success=0,total=0;
            foreach(var bag in config.Bags)for(int i=0;i<10;i++)
            {
                total++;Phase1BoardState board=null;string error="generation failed";int usedSeed=0;
                for(int attempt=0;attempt<20;attempt++){usedSeed=1000+(total*370)+(attempt*37);if(generator.TryGenerate(bag.Difficulty,bag,usedSeed,true,out var candidate)&&Phase1PlacementValidator.ValidateFinal(candidate.Tiles,config.BoardSize,out error)){board=candidate;break;}}
                if(board!=null&&board.Tiles.All(x=>x.MinimumHpValid&&x.MaxHp>=config.MinimumFinalTileHp&&x.MaxHp==x.BaseHp+x.GradeHpModifier)){success++;Debug.Log($"[Phase1][Matrix] {bag.Id} seed={usedSeed} tiles={board.Tiles.Count} area={board.Tiles.Sum(x=>x.GridWidth*x.GridHeight)} baseHp={board.Tiles.Sum(x=>x.BaseHp)} modifier={board.Tiles.Sum(x=>x.GradeHpModifier)} finalHp={board.Tiles.Sum(x=>x.MaxHp)} layout={board.LayoutHash} variant={board.VariantHash}");}
                else Debug.LogError($"[Phase1][Matrix] FAILED {bag.Id} after 20 attempts error={error}");
            }
            Debug.Log($"[Phase1][Matrix] result={success}/{total}");
            foreach(var bag in config.Bags.GroupBy(x=>x.Difficulty).Select(x=>x.First())){int seed=24680+(int)bag.Difficulty;generator.TryGenerate(bag.Difficulty,bag,seed,true,out var a);generator.TryGenerate(bag.Difficulty,bag,seed,true,out var b);Debug.Log($"[Phase1][FixedSeed] difficulty={bag.Difficulty}, bag={bag.Id}, seed={seed}, layoutMatch={a?.LayoutHash==b?.LayoutHash}, variantMatch={a?.VariantHash==b?.VariantHash}");}
        }
        private static GameObject CreatePrefab(string path){var root=new GameObject("Phase1_TilePrefab",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(Phase1TileView));var rootImage=root.GetComponent<Image>();rootImage.color=new Color(1,1,1,0);rootImage.raycastTarget=true;rootImage.alphaHitTestMinimumThreshold=0;var child=new GameObject("Tile_Visual",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));child.transform.SetParent(root.transform,false);var rt=child.GetComponent<RectTransform>();rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;var image=child.GetComponent<Image>();image.raycastTarget=false;image.color=new Color(.72f,.58f,.38f,1);Set(root.GetComponent<Phase1TileView>(),("hitArea",rootImage),("visualImage",image),("visualRoot",rt));var prefab=PrefabUtility.SaveAsPrefabAsset(root,path);Object.DestroyImmediate(root);return prefab;}
        private static void EnsureFolder(string path){if(AssetDatabase.IsValidFolder(path))return;int slash=path.LastIndexOf('/');AssetDatabase.CreateFolder(path[..slash],path[(slash+1)..]);}
        private static GameObject Require(string path){var go=GameObject.Find(path);if(!go)throw new System.InvalidOperationException("Required object missing: "+path);return go;}
        private static T Add<T>(GameObject go) where T:Component {return go.GetComponent<T>()?go.GetComponent<T>():Undo.AddComponent<T>(go);}
        private static void Set(Component target,params (string name,Object value)[] values){var so=new SerializedObject(target);foreach(var item in values){var p=so.FindProperty(item.name);if(p==null)throw new System.InvalidOperationException(target.GetType().Name+"."+item.name+" missing");p.objectReferenceValue=item.value;}so.ApplyModifiedProperties();}
    }
}
#endif
