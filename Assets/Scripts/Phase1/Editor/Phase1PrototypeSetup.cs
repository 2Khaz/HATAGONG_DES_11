#if UNITY_EDITOR
using HATAGONG.Phase1;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using System.Collections.Generic;
using HATAGONG.GameFlow;

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
            var timeContent=Require("Canvas/Game_UI_General/Top_HUD/Time_Content");
            var timeValue=Require("Canvas/Game_UI_General/Top_HUD/Time_Content/Time_Value");
            var diffText=Require("Canvas/Game_UI_General/Top_HUD/Difficulty_Content/Difficulty_Text");
            var star1=Require("Canvas/Game_UI_General/Top_HUD/Difficulty_Content/Difficulty_StarArea/Diff01_Star");
            var star2=Require("Canvas/Game_UI_General/Top_HUD/Difficulty_Content/Difficulty_StarArea/Diff02_Star");
            var star3=Require("Canvas/Game_UI_General/Top_HUD/Difficulty_Content/Difficulty_StarArea/Diff03_Star");
            var board=Add<Phase1BoardController>(field);var input=Add<Phase1InputController>(field);var scoreCtrl=Add<Phase1ScoreController>(field);var hud=Add<Phase1HUDPresenter>(field);var feedback=Add<Phase1FeedbackController>(field);var audio=Add<AudioSource>(field);
            audio.playOnAwake=false;audio.loop=false;audio.spatialBlend=0;audio.volume=1;
            Set(board,("fieldRoot",field.GetComponent<RectTransform>()),("tileContainer",tiles.GetComponent<RectTransform>()),("effectRoot",effects.GetComponent<RectTransform>()),("scorePopupRoot",popups.GetComponent<RectTransform>()),("tilePrefab",prefab.GetComponent<Phase1TileView>()),("config",config),("inputController",input),("scoreController",scoreCtrl),("feedbackController",feedback),("hudPresenter",hud));
            Set(input,("boardController",board));Set(scoreCtrl,("scoreValueText",score.GetComponent<TextMeshProUGUI>()));
            Set(hud,("difficultyText",diffText.GetComponent<TextMeshProUGUI>()));var hso=new SerializedObject(hud);var stars=hso.FindProperty("difficultyStars");stars.arraySize=3;stars.GetArrayElementAtIndex(0).objectReferenceValue=star1.GetComponent<Image>();stars.GetArrayElementAtIndex(1).objectReferenceValue=star2.GetComponent<Image>();stars.GetArrayElementAtIndex(2).objectReferenceValue=star3.GetComponent<Image>();hso.ApplyModifiedProperties();
            var filledStarSprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Ingame/ICON/LevelICON/Img_icon_star2.png");var emptyStarSprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Ingame/ICON/LevelICON/Img_icon_star1.png");if(!filledStarSprite||!emptyStarSprite)throw new System.InvalidOperationException("Required Phase 1 star Sprite asset is missing.");
            hso=new SerializedObject(hud);hso.FindProperty("filledStarSprite").objectReferenceValue=filledStarSprite;hso.FindProperty("emptyStarSprite").objectReferenceValue=emptyStarSprite;hso.ApplyModifiedProperties();
            Set(feedback,("audioSource",audio),("config",config));
            var timer=Add<GameTimerController>(GameObject.Find("Canvas/Game_UI_General"));
            var timerPresenter=Add<GameTimerPresenter>(timeContent);
            Set(timer,("config",config));Set(timerPresenter,("controller",timer),("timeValueText",timeValue.GetComponent<TextMeshProUGUI>()));
            EditorUtility.SetDirty(field);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(field.scene);EditorSceneManager.SaveScene(field.scene);Debug.Log("[Phase1] Prototype setup and scene save completed.");
        }
        [MenuItem("Tools/HATAGONG/Phase1/Validate Generation Matrix")]
        public static void ValidateGenerationMatrix(){ValidateGenerationMatrix(10,"Matrix");}
        [MenuItem("Tools/HATAGONG/Phase1/Validate Generation Stress 1200")]
        public static void ValidateGenerationStress(){ValidateGenerationMatrix(100,"Stress");}
        [MenuItem("Tools/HATAGONG/Phase1/Validate Grade Distribution 3000")]
        public static void ValidateGradeDistribution()
        {
            var config=AssetDatabase.LoadAssetAtPath<Phase1GameConfig>("Assets/Settings/Phase1/Phase1GameConfig.asset");
            if(!config||!config.ValidateAllBags())throw new System.InvalidOperationException("Phase1 config or bag validation failed.");
            var generator=new Phase1BoardGenerator(config);int allBoards=0,allTiles=0,allFailures=0,allMinimum=0,allMaximum=0,allMissing=0,allGeometry=0,allDeterminism=0,allBase=0,allRange=0,allNull=0,allFallbacks=0;
            foreach(Phase1Difficulty difficulty in System.Enum.GetValues(typeof(Phase1Difficulty)))
            {
                var bags=config.Bags.Where(x=>x.Difficulty==difficulty).ToArray();var gradeTotals=new int[4];var fallbackGrades=new int[4];int boards=0,tiles=0,failures=0,generationRetries=0,gradeAssignmentFailures=0,minimumViolations=0,maximumViolations=0,missingGrades=0,geometryMismatch=0,determinism=0,baseMismatch=0,hpRange=0,nullGrades=0,fallbacks=0,modifierRange=0;int marbleMinimum=int.MaxValue,marbleMaximum=0;
                long sameEdges=0,marbleEdges=0,largestComponents=0,sameShapeEdges=0;int maxSameEdges=0,maxMarbleEdges=0,maxLargest=0,sameThreeBoards=0,marbleThreeBoards=0,improved=0,same=0,worse=0;
                for(int i=0;i<1000;i++)
                {
                    Phase1TileBagDefinition bag=bags[i%bags.Length];int seed=100000+(int)difficulty*10000+i,usedSeed=seed;Phase1BoardState board=null;
                    for(int attempt=0;attempt<20&&board==null;attempt++){usedSeed=seed+attempt*37;if(!generator.TryGenerate(difficulty,bag,usedSeed,true,out board)){generationRetries++;if(generator.TryGenerateGeometry(difficulty,bag,usedSeed,true,out _))gradeAssignmentFailures++;}}
                    if(board==null){failures++;Debug.LogError($"[Phase1][Distribution] generation failed difficulty={difficulty}, bag={bag.Id}, seed={seed}, attempts=20");continue;}
                    int boardFallbacks=generator.LastGradeFallbackCount;int[] boardFallbackGrades=new int[4];foreach(var pair in generator.LastGradeFallbackGrades)boardFallbackGrades[(int)pair.Key]=pair.Value;Phase1GradeDispersionScore preScore=generator.LastPreOptimizationScore,finalScore=generator.LastGradeDispersionScore;
                    if(!generator.TryGenerateGeometry(difficulty,bag,usedSeed,true,out Phase1BoardState geometry)||!GeometryMatches(board,geometry))geometryMismatch++;
                    if(!generator.TryGenerate(difficulty,bag,usedSeed,true,out Phase1BoardState repeat)||board.LayoutHash!=repeat.LayoutHash||board.VariantHash!=repeat.VariantHash)determinism++;
                    boards++;tiles+=board.Tiles.Count;fallbacks+=boardFallbacks;for(int grade=0;grade<4;grade++)fallbackGrades[grade]+=boardFallbackGrades[grade];
                    var counts=board.Tiles.GroupBy(x=>x.Grade).ToDictionary(x=>x.Key,x=>x.Count());
                    foreach(Phase1TileGradeDefinition definition in config.GradeDefinitions.Where(x=>x.Enabled))
                    {
                        int count=counts.TryGetValue(definition.Grade,out int value)?value:0;gradeTotals[(int)definition.Grade]+=count;if(count<definition.MinCount(difficulty))minimumViolations++;if(definition.MaxCount(difficulty)>0&&count>definition.MaxCount(difficulty))maximumViolations++;if(count==0)missingGrades++;
                    }
                    int marble=counts.TryGetValue(Phase1TileGrade.Marble,out int marbleCount)?marbleCount:0;marbleMinimum=System.Math.Min(marbleMinimum,marble);marbleMaximum=System.Math.Max(marbleMaximum,marble);
                    foreach(Phase1TilePlacement tile in board.Tiles){if(string.IsNullOrEmpty(tile.GradeId))nullGrades++;if(tile.BaseHp!=config.GetHp(difficulty,tile.Shape))baseMismatch++;if(tile.MaxHp!=tile.BaseHp+tile.GradeHpModifier||tile.MaxHp<8||tile.MaxHp>47)hpRange++;}
                    config.GetModifierRange(difficulty,out int modifierMin,out int modifierMax);int modifierTotal=board.Tiles.Sum(x=>x.GradeHpModifier);if(modifierTotal<modifierMin||modifierTotal>modifierMax)modifierRange++;
                    Phase1GradeDispersionScore score=Phase1GradeAssigner.EvaluateDispersion(board.Tiles);sameEdges+=score.SameGradeEdges;marbleEdges+=score.MarbleEdges;largestComponents+=score.LargestComponent;sameShapeEdges+=score.SameShapeEdges;maxSameEdges=System.Math.Max(maxSameEdges,score.SameGradeEdges);maxMarbleEdges=System.Math.Max(maxMarbleEdges,score.MarbleEdges);maxLargest=System.Math.Max(maxLargest,score.LargestComponent);if(score.LargestComponent>=3)sameThreeBoards++;if(Phase1GradeAssigner.LargestComponent(board.Tiles,Phase1TileGrade.Marble)>=3)marbleThreeBoards++;
                    int comparison=finalScore.CompareTo(preScore);if(comparison<0)improved++;else if(comparison==0)same++;else worse++;
                }
                Debug.Log($"[Phase1][Distribution] difficulty={difficulty}, boards={boards}/1000, failures={failures}, generationRetries={generationRetries}, gradeAssignmentFailures={gradeAssignmentFailures}, tiles={tiles}, averages={gradeTotals[0]/(float)System.Math.Max(1,boards):F3}/{gradeTotals[1]/(float)System.Math.Max(1,boards):F3}/{gradeTotals[2]/(float)System.Math.Max(1,boards):F3}/{gradeTotals[3]/(float)System.Math.Max(1,boards):F3}, marbleMinMax={marbleMinimum}..{marbleMaximum}, minViolations={minimumViolations}, maxViolations={maximumViolations}, missingGrades={missingGrades}, geometryMismatch={geometryMismatch}, determinism={determinism}, baseMismatch={baseMismatch}, hpRange={hpRange}, nullGrades={nullGrades}, modifierRange={modifierRange}, fallback={fallbacks}, fallbackGrades={fallbackGrades[0]}/{fallbackGrades[1]}/{fallbackGrades[2]}/{fallbackGrades[3]}");
                Debug.Log($"[Phase1][Dispersion] difficulty={difficulty}, averageSameEdges={sameEdges/(float)System.Math.Max(1,boards):F3}, maxSameEdges={maxSameEdges}, averageMarbleEdges={marbleEdges/(float)System.Math.Max(1,boards):F3}, maxMarbleEdges={maxMarbleEdges}, averageLargest={largestComponents/(float)System.Math.Max(1,boards):F3}, maxLargest={maxLargest}, sameGradeThreeBoards={sameThreeBoards}, marbleThreeBoards={marbleThreeBoards}, sameShapeEdges={sameShapeEdges}, improved/same/worse={improved}/{same}/{worse}");
                allBoards+=boards;allTiles+=tiles;allFailures+=failures;allMinimum+=minimumViolations;allMaximum+=maximumViolations;allMissing+=missingGrades;allGeometry+=geometryMismatch;allDeterminism+=determinism;allBase+=baseMismatch;allRange+=hpRange+modifierRange;allNull+=nullGrades;allFallbacks+=fallbacks;
                if(boards!=1000||failures!=0||gradeAssignmentFailures!=0||minimumViolations!=0||maximumViolations!=0||missingGrades!=0||geometryMismatch!=0||determinism!=0||baseMismatch!=0||hpRange!=0||nullGrades!=0||modifierRange!=0||worse!=0)throw new System.InvalidOperationException($"Phase1 distribution validation failed for {difficulty}.");
            }
            Debug.Log($"[Phase1][Distribution] PASS boards={allBoards}/3000, failures={allFailures}, tiles={allTiles}, minViolations={allMinimum}, maxViolations={allMaximum}, missingGrades={allMissing}, geometryMismatch={allGeometry}, determinism={allDeterminism}, baseMismatch={allBase}, hpRange={allRange}, nullGrades={allNull}, fallback={allFallbacks}");
        }
        private static bool GeometryMatches(Phase1BoardState board,Phase1BoardState geometry)
        {
            if(board==null||geometry==null||board.Difficulty!=geometry.Difficulty||board.BagId!=geometry.BagId||board.Seed!=geometry.Seed||board.LayoutHash!=geometry.LayoutHash||board.Tiles.Count!=geometry.Tiles.Count)return false;
            var expected=geometry.Tiles.OrderBy(x=>x.TileId).ToArray();var actual=board.Tiles.OrderBy(x=>x.TileId).ToArray();
            for(int i=0;i<actual.Length;i++)if(actual[i].TileId!=expected[i].TileId||actual[i].Shape!=expected[i].Shape||actual[i].GridX!=expected[i].GridX||actual[i].GridY!=expected[i].GridY||actual[i].GridWidth!=expected[i].GridWidth||actual[i].GridHeight!=expected[i].GridHeight||actual[i].IsRotated!=expected[i].IsRotated)return false;return true;
        }
        [MenuItem("Tools/HATAGONG/Phase1/Regenerate Active Board")]
        public static void RegenerateActiveBoard(){if(!Application.isPlaying){Debug.LogError("[Phase1][HistoryTest] Play Mode required.");return;}var board=Object.FindFirstObjectByType<Phase1BoardController>();if(!board){Debug.LogError("[Phase1][HistoryTest] Board controller missing.");return;}board.RegenerateBoard();}
        private static void ValidateGenerationMatrix(int seedsPerBag,string label)
        {
            var config=AssetDatabase.LoadAssetAtPath<Phase1GameConfig>("Assets/Settings/Phase1/Phase1GameConfig.asset");if(!config||!config.ValidateAllBags())throw new System.InvalidOperationException("Phase1 config or bag validation failed.");
            var generator=new Phase1BoardGenerator(config);int success=0,total=0,testedTiles=0,minimumHpViolations=0,hpMismatches=0;
            foreach(var bag in config.Bags)for(int i=0;i<seedsPerBag;i++)
            {
                total++;Phase1BoardState board=null;string error="generation failed";int usedSeed=0;
                for(int attempt=0;attempt<20;attempt++){usedSeed=1000+(total*370)+(attempt*37);if(generator.TryGenerate(bag.Difficulty,bag,usedSeed,true,out var candidate)&&Phase1PlacementValidator.ValidateFinal(candidate.Tiles,config.BoardSize,out error)){board=candidate;break;}}
                if(board!=null){testedTiles+=board.Tiles.Count;minimumHpViolations+=board.Tiles.Count(x=>!x.MinimumHpValid||x.MaxHp<config.MinimumFinalTileHp);hpMismatches+=board.Tiles.Count(x=>x.MaxHp!=x.BaseHp+x.GradeHpModifier);}
                if(board!=null&&board.Tiles.All(x=>x.MinimumHpValid&&x.MaxHp>=config.MinimumFinalTileHp&&x.MaxHp==x.BaseHp+x.GradeHpModifier)){success++;if(seedsPerBag<=10)Debug.Log($"[Phase1][{label}] {bag.Id} seed={usedSeed} tiles={board.Tiles.Count} area={board.Tiles.Sum(x=>x.GridWidth*x.GridHeight)} baseHp={board.Tiles.Sum(x=>x.BaseHp)} modifier={board.Tiles.Sum(x=>x.GradeHpModifier)} finalHp={board.Tiles.Sum(x=>x.MaxHp)} layout={board.LayoutHash} variant={board.VariantHash}");}
                else Debug.LogError($"[Phase1][{label}] FAILED {bag.Id} after 20 attempts error={error}");
            }
            Debug.Log($"[Phase1][{label}] result={success}/{total}, tiles={testedTiles}, minimumHpViolations={minimumHpViolations}, hpMismatches={hpMismatches}");
            if(success!=total||minimumHpViolations!=0||hpMismatches!=0)throw new System.InvalidOperationException($"Phase1 {label} validation failed: result={success}/{total}, minimumHpViolations={minimumHpViolations}, hpMismatches={hpMismatches}.");
            foreach(var bag in config.Bags.GroupBy(x=>x.Difficulty).Select(x=>x.First())){int seed=24680+(int)bag.Difficulty;generator.TryGenerate(bag.Difficulty,bag,seed,true,out var a);generator.TryGenerate(bag.Difficulty,bag,seed,true,out var b);Debug.Log($"[Phase1][FixedSeed] difficulty={bag.Difficulty}, bag={bag.Id}, seed={seed}, layoutMatch={a?.LayoutHash==b?.LayoutHash}, variantMatch={a?.VariantHash==b?.VariantHash}");}
            ValidateDamageStates();
        }
        private static void ValidateDamageStates(){int checks=0,failures=0;for(int max=8;max<=47;max++){var previous=Phase1DamageState.Normal;for(int current=max;current>=0;current--){var state=Phase1TileView.CalculateState(max,current);checks++;if(current==max&&state!=Phase1DamageState.Normal)failures++;if(current==0&&state!=Phase1DamageState.Destroyed)failures++;if((int)state<(int)previous)failures++;previous=state;}}if(failures>0)Debug.LogError($"[Phase1][DamageState] failures={failures}/{checks}");else Debug.Log($"[Phase1][DamageState] result={checks}/{checks}, hpRange=8..47");}
        private static GameObject CreatePrefab(string path){var root=new GameObject("Phase1_TilePrefab",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(Phase1TileView));var rootImage=root.GetComponent<Image>();rootImage.color=new Color(1,1,1,0);rootImage.raycastTarget=true;rootImage.alphaHitTestMinimumThreshold=0;var child=new GameObject("Tile_Visual",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));child.transform.SetParent(root.transform,false);var rt=child.GetComponent<RectTransform>();rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;var image=child.GetComponent<Image>();image.raycastTarget=false;image.color=new Color(.72f,.58f,.38f,1);Set(root.GetComponent<Phase1TileView>(),("hitArea",rootImage),("visualImage",image),("visualRoot",rt));var prefab=PrefabUtility.SaveAsPrefabAsset(root,path);Object.DestroyImmediate(root);return prefab;}
        private static void EnsureFolder(string path){if(AssetDatabase.IsValidFolder(path))return;int slash=path.LastIndexOf('/');AssetDatabase.CreateFolder(path[..slash],path[(slash+1)..]);}
        private static GameObject Require(string path){var go=GameObject.Find(path);if(!go)throw new System.InvalidOperationException("Required object missing: "+path);return go;}
        private static T Add<T>(GameObject go) where T:Component {return go.GetComponent<T>()?go.GetComponent<T>():Undo.AddComponent<T>(go);}
        private static void Set(Component target,params (string name,Object value)[] values){var so=new SerializedObject(target);foreach(var item in values){var p=so.FindProperty(item.name);if(p==null)throw new System.InvalidOperationException(target.GetType().Name+"."+item.name+" missing");p.objectReferenceValue=item.value;}so.ApplyModifiedProperties();}
    }
}
#endif
