using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HATAGONG.Phase1
{
    [CreateAssetMenu(fileName="Phase1GameConfig", menuName="HATAGONG/Phase1/Game Config")]
    public sealed class Phase1GameConfig : ScriptableObject
    {
        [Header("Board")][SerializeField] private int boardSize=5;
        [SerializeField] private bool generateOnStart=true;
        [SerializeField] private float tileDebounce=0.04f;
        [SerializeField] private int currentBagAttempts=20;
        [SerializeField] private int alternativeBagAttempts=20;
        [Header("Score")][SerializeField] private int hitScore=10, destroyScore=50, clearScore=300;
        [Header("Time record only")][SerializeField] private float overallGameDurationSeconds=90f;
        [Header("Recent hashes")][SerializeField] private int easyHashCapacity=30, normalHashCapacity=50, hardHashCapacity=50;
        [Header("Punch")][SerializeField] private float punchScale=0.97f, punchDownDuration=0.04f, punchReturnDuration=0.06f;
        [Header("Sound")][SerializeField] private bool enableSound=true;
        [SerializeField] private AudioClip normalHitClip, damageStateChangeClip, destroyClip, coreHitClip;
        [SerializeField, Range(0,1)] private float normalHitVolume=1, damageStateChangeVolume=1, destroyVolume=1, coreHitVolume=1;
        [Header("Vibration")][SerializeField] private bool enableVibration=true;
        [SerializeField] private bool vibrateOnNormalHit=false, vibrateOnDamageStateChange=true, vibrateOnDestroy=true;
        [Header("Definitions")][SerializeField] private List<Phase1TileBagDefinition> bags = new();
        [SerializeField] private List<Phase1TileVisualDefinition> visuals = new();
        [Header("Tile Grades")][SerializeField] private List<Phase1TileGradeDefinition> gradeDefinitions = new();
        [SerializeField] private List<Phase1TileGradeVisualSet> gradeVisualSets = new();
        [SerializeField] private int minimumFinalTileHp=2,gradeAssignmentAttempts=20;
        [SerializeField] private Phase1TileGrade defaultFallbackGrade=Phase1TileGrade.Brown;
        [SerializeField] private int easyModifierMin=0,easyModifierMax=30,normalModifierMin=0,normalModifierMax=41,hardModifierMin=0,hardModifierMax=55;

        public int BoardSize=>boardSize; public bool GenerateOnStart=>generateOnStart; public float TileDebounce=>tileDebounce;
        public int CurrentBagAttempts=>currentBagAttempts; public int AlternativeBagAttempts=>alternativeBagAttempts;
        public int HitScore=>hitScore; public int DestroyScore=>destroyScore; public int ClearScore=>clearScore;
        public float OverallGameDurationSeconds=>overallGameDurationSeconds; public float PunchScale=>punchScale;
        public float PunchDownDuration=>punchDownDuration; public float PunchReturnDuration=>punchReturnDuration;
        public bool EnableSound=>enableSound; public AudioClip NormalHitClip=>normalHitClip; public AudioClip DamageStateChangeClip=>damageStateChangeClip; public AudioClip DestroyClip=>destroyClip; public AudioClip CoreHitClip=>coreHitClip;
        public float NormalHitVolume=>normalHitVolume; public float DamageStateChangeVolume=>damageStateChangeVolume; public float DestroyVolume=>destroyVolume; public float CoreHitVolume=>coreHitVolume;
        public bool EnableVibration=>enableVibration; public bool VibrateOnNormalHit=>vibrateOnNormalHit; public bool VibrateOnDamageStateChange=>vibrateOnDamageStateChange; public bool VibrateOnDestroy=>vibrateOnDestroy;
        public IReadOnlyList<Phase1TileBagDefinition> Bags=>bags;
        public IReadOnlyList<Phase1TileGradeDefinition> GradeDefinitions=>gradeDefinitions; public IReadOnlyList<Phase1TileGradeVisualSet> GradeVisualSets=>gradeVisualSets;
        public int MinimumFinalTileHp=>minimumFinalTileHp; public int GradeAssignmentAttempts=>gradeAssignmentAttempts; public Phase1TileGrade DefaultFallbackGrade=>defaultFallbackGrade;
        public int HashCapacity(Phase1Difficulty d)=>d==Phase1Difficulty.Easy?easyHashCapacity:d==Phase1Difficulty.Normal?normalHashCapacity:hardHashCapacity;

        public int GetHp(Phase1Difficulty d, Phase1TileShape s) => d switch
        {
            Phase1Difficulty.Easy => s switch { Phase1TileShape.OneByTwo=>8, Phase1TileShape.OneByThree=>14, Phase1TileShape.TwoByTwo=>20, Phase1TileShape.TwoByThree=>28, Phase1TileShape.ThreeByThree=>36, _=>0 },
            Phase1Difficulty.Normal => s switch { Phase1TileShape.OneByOne=>22, Phase1TileShape.OneByTwo=>10, Phase1TileShape.OneByThree=>18, Phase1TileShape.TwoByTwo=>26, Phase1TileShape.TwoByThree=>34, Phase1TileShape.ThreeByThree=>40, _=>0 },
            _ => s switch { Phase1TileShape.OneByOne=>28, Phase1TileShape.OneByTwo=>14, Phase1TileShape.OneByThree=>22, Phase1TileShape.TwoByTwo=>30, Phase1TileShape.TwoByThree=>40, _=>0 }
        };
        public Sprite GetSprite(Phase1TileGrade grade,Phase1TileShape shape,Phase1DamageState state,out bool fallback){var v=gradeVisualSets.Find(x=>x.Grade==grade);if(v!=null)return v.Get(shape,state,out fallback);fallback=true;return null;}
        public string GetVisualSetId(Phase1TileGrade grade)=>gradeVisualSets.Find(x=>x.Grade==grade)?.VisualSetId??grade.ToString().ToUpperInvariant();
        public Phase1TileGradeVisualSet GetVisualSet(Phase1TileGrade grade)=>gradeVisualSets.Find(x=>x.Grade==grade);
        public Color GetFallbackColor(Phase1TileGrade grade)=>GetVisualSet(grade)?.FallbackColor??Color.white;
        public void GetModifierRange(Phase1Difficulty d,out int min,out int max){if(d==Phase1Difficulty.Easy){min=easyModifierMin;max=easyModifierMax;}else if(d==Phase1Difficulty.Normal){min=normalModifierMin;max=normalModifierMax;}else{min=hardModifierMin;max=hardModifierMax;}}

        public void EnsureDefaults()
        {
            if (bags.Count!=12) bags = new List<Phase1TileBagDefinition> {
                B("EASY_A",0,0,2,1,1,1,5,112), B("EASY_B",0,1,0,2,1,1,5,112), B("EASY_C",0,1,1,2,2,0,6,118), B("EASY_D",0,2,0,3,0,1,6,112),
                B("NORMAL_A",2,1,1,3,1,0,8,184), B("NORMAL_B",2,0,1,2,2,0,7,182), B("NORMAL_C",2,1,0,3,0,1,7,172), B("NORMAL_D",2,2,1,1,2,0,8,176),
                B("HARD_A",3,3,2,1,1,0,10,240), B("HARD_B",4,1,1,1,2,0,9,258), B("HARD_C",3,2,4,0,1,0,10,240), B("HARD_D",3,2,0,3,1,0,9,242) };
            if(visuals.Count==0){visuals = new List<Phase1TileVisualDefinition>(); foreach(Phase1TileShape s in System.Enum.GetValues(typeof(Phase1TileShape))) visuals.Add(new Phase1TileVisualDefinition(s));}
            if(gradeDefinitions.Count!=4)gradeDefinitions=new List<Phase1TileGradeDefinition>{new(Phase1TileGrade.Beige,"BEIGE","Beige",0,20,10,5,0,0,0),new(Phase1TileGrade.Brown,"BROWN","Brown",2,30,25,15,0,0,0),new(Phase1TileGrade.Gray,"GRAY","Gray",4,25,30,35,0,0,0),new(Phase1TileGrade.Marble,"MARBLE","Marble",7,25,35,45,2,3,5)};
            if(gradeVisualSets.Count!=4){gradeVisualSets=new List<Phase1TileGradeVisualSet>();foreach(Phase1TileGrade g in System.Enum.GetValues(typeof(Phase1TileGrade)))gradeVisualSets.Add(new Phase1TileGradeVisualSet(g));}
            foreach(var set in gradeVisualSets)set.EnsureFallbackColor();
        }
        private Phase1TileBagDefinition B(string id,int a,int b,int c,int d,int e,int f,int tiles,int hp) => new(id,id.StartsWith("EASY")?Phase1Difficulty.Easy:id.StartsWith("NORMAL")?Phase1Difficulty.Normal:Phase1Difficulty.Hard,a,b,c,d,e,f,tiles,25,hp);
        public bool ValidateAllBags(bool log=true)
        {
            EnsureDefaults(); bool ok=true;
            foreach(var bag in bags) { int tiles=0,area=0,hp=0; foreach(Phase1TileShape s in System.Enum.GetValues(typeof(Phase1TileShape))) { int n=bag.Count(s); tiles+=n; area+=n*Phase1BoardGenerator.ShapeArea(s); hp+=n*GetHp(bag.Difficulty,s); }
                bool valid=tiles==bag.ExpectedTileCount&&area==bag.ExpectedArea&&hp==bag.ExpectedHp&&bag.Count(Phase1TileShape.ThreeByThree)<=1&&!(bag.Difficulty==Phase1Difficulty.Easy&&bag.Count(Phase1TileShape.OneByOne)>0)&&!(bag.Difficulty==Phase1Difficulty.Hard&&bag.Count(Phase1TileShape.ThreeByThree)>0);
                if(log) Debug.Log($"[Phase1][Bag] {bag.Id}: tiles={tiles}, area={area}, hp={hp}, valid={valid}"); ok &= valid; }
            ok&=ValidateGradeSettings(log);if(!ok) Debug.LogError("[Phase1] Config validation failed. Board generation is blocked."); return ok;
        }
        public bool ValidateGradeSettings(bool log=true)
        {
            EnsureDefaults();bool ok=true;var enabled=gradeDefinitions.FindAll(x=>x.Enabled);ok&=enabled.Count>0;ok&=gradeDefinitions.Select(x=>x.GradeId).Distinct().Count()==gradeDefinitions.Count;ok&=gradeDefinitions.All(x=>x.HpModifier>=0&&x.HpModifier<=7&&x.Weight(Phase1Difficulty.Easy)>0&&x.Weight(Phase1Difficulty.Normal)>0&&x.Weight(Phase1Difficulty.Hard)>0&&x.MaxCount(Phase1Difficulty.Easy)>=0&&x.MaxCount(Phase1Difficulty.Normal)>=0&&x.MaxCount(Phase1Difficulty.Hard)>=0);ok&=enabled.Any(x=>x.Grade==defaultFallbackGrade);ok&=minimumFinalTileHp>=2&&gradeAssignmentAttempts>0;ok&=easyModifierMin==0&&easyModifierMax==30&&normalModifierMin==0&&normalModifierMax==41&&hardModifierMin==0&&hardModifierMax==55;ok&=gradeVisualSets.Select(x=>x.Grade).Distinct().Count()==gradeVisualSets.Count;
            ok&=enabled.Sum(x=>x.Weight(Phase1Difficulty.Easy))==100&&enabled.Sum(x=>x.Weight(Phase1Difficulty.Normal))==100&&enabled.Sum(x=>x.Weight(Phase1Difficulty.Hard))==100;
            ok&=GradeMatches(Phase1TileGrade.Beige,0,20,10,5,0,0,0)&&GradeMatches(Phase1TileGrade.Brown,2,30,25,15,0,0,0)&&GradeMatches(Phase1TileGrade.Gray,4,25,30,35,0,0,0)&&GradeMatches(Phase1TileGrade.Marble,7,25,35,45,2,3,5);
            ok&=GetHp(Phase1Difficulty.Easy,Phase1TileShape.OneByTwo)==8&&GetHp(Phase1Difficulty.Easy,Phase1TileShape.OneByThree)==14&&GetHp(Phase1Difficulty.Easy,Phase1TileShape.TwoByTwo)==20&&GetHp(Phase1Difficulty.Easy,Phase1TileShape.TwoByThree)==28&&GetHp(Phase1Difficulty.Easy,Phase1TileShape.ThreeByThree)==36;
            ok&=GetHp(Phase1Difficulty.Normal,Phase1TileShape.OneByOne)==22&&GetHp(Phase1Difficulty.Normal,Phase1TileShape.OneByTwo)==10&&GetHp(Phase1Difficulty.Normal,Phase1TileShape.OneByThree)==18&&GetHp(Phase1Difficulty.Normal,Phase1TileShape.TwoByTwo)==26&&GetHp(Phase1Difficulty.Normal,Phase1TileShape.TwoByThree)==34&&GetHp(Phase1Difficulty.Normal,Phase1TileShape.ThreeByThree)==40;
            ok&=GetHp(Phase1Difficulty.Hard,Phase1TileShape.OneByOne)==28&&GetHp(Phase1Difficulty.Hard,Phase1TileShape.OneByTwo)==14&&GetHp(Phase1Difficulty.Hard,Phase1TileShape.OneByThree)==22&&GetHp(Phase1Difficulty.Hard,Phase1TileShape.TwoByTwo)==30&&GetHp(Phase1Difficulty.Hard,Phase1TileShape.TwoByThree)==40;
            foreach(Phase1Difficulty d in System.Enum.GetValues(typeof(Phase1Difficulty))){ok&=enabled.Sum(x=>x.Weight(d))>0;foreach(Phase1TileShape s in System.Enum.GetValues(typeof(Phase1TileShape))){int hp=GetHp(d,s);if(hp>0)ok&=enabled.Any(x=>hp+x.HpModifier>=minimumFinalTileHp);}}
            if(log)Debug.Log($"[Phase1][GradeConfig] grades={gradeDefinitions.Count}, visuals={gradeVisualSets.Count}, minimumFinalHp={minimumFinalTileHp}, valid={ok}");return ok;
        }
        private bool GradeMatches(Phase1TileGrade grade,int modifier,int ew,int nw,int hw,int em,int nm,int hm){var value=gradeDefinitions.Find(x=>x.Grade==grade);return value!=null&&value.HpModifier==modifier&&value.Weight(Phase1Difficulty.Easy)==ew&&value.Weight(Phase1Difficulty.Normal)==nw&&value.Weight(Phase1Difficulty.Hard)==hw&&value.MaxCount(Phase1Difficulty.Easy)==em&&value.MaxCount(Phase1Difficulty.Normal)==nm&&value.MaxCount(Phase1Difficulty.Hard)==hm;}
        private void OnValidate(){ EnsureDefaults(); ValidateAllBags(false); }
    }
}
