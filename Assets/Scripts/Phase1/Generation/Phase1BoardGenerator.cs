using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace HATAGONG.Phase1
{
    public sealed class Phase1BoardGenerator
    {
        private sealed class Spec { public Phase1TileShape Shape; public int Area; public int Id; }
        private readonly Phase1GameConfig config;
        private readonly Dictionary<Phase1TileGrade,int> lastFallbackGrades=new();
        public Phase1BoardGenerator(Phase1GameConfig config){this.config=config;}
        public int LastGradeFallbackCount { get; private set; }
        public IReadOnlyDictionary<Phase1TileGrade,int> LastGradeFallbackGrades=>lastFallbackGrades;
        public Phase1GradeDispersionScore LastPreOptimizationScore { get; private set; }
        public Phase1GradeDispersionScore LastGradeDispersionScore { get; private set; }
        public static int ShapeArea(Phase1TileShape s)=>s switch{Phase1TileShape.OneByOne=>1,Phase1TileShape.OneByTwo=>2,Phase1TileShape.OneByThree=>3,Phase1TileShape.TwoByTwo=>4,Phase1TileShape.TwoByThree=>6,Phase1TileShape.ThreeByThree=>9,_=>0};
        public static Phase1TileRole Role(Phase1TileShape s)=>s switch{Phase1TileShape.OneByOne=>Phase1TileRole.Core,Phase1TileShape.OneByTwo=>Phase1TileRole.Pop,Phase1TileShape.TwoByThree or Phase1TileShape.ThreeByThree=>Phase1TileRole.Anchor,_=>Phase1TileRole.Standard};
        public bool TryGenerate(Phase1Difficulty difficulty,Phase1TileBagDefinition bag,int seed,bool prioritize,out Phase1BoardState state)
        {
            LastGradeFallbackCount=0;lastFallbackGrades.Clear();LastPreOptimizationScore=default;LastGradeDispersionScore=default;
            if(!TryGenerateGeometry(difficulty,bag,seed,prioritize,out state))return false;
            var assigner=new Phase1GradeAssigner(config);
            var gradeRandom=new Random(DeriveGradeSeed(seed,bag.Id,difficulty));
            if(!assigner.TryAssign(difficulty,state.Tiles,gradeRandom,out _)){state=null;return false;}
            LastGradeFallbackCount=assigner.LastFallbackCount;
            foreach(var pair in assigner.LastFallbackGrades)lastFallbackGrades[pair.Key]=pair.Value;
            LastPreOptimizationScore=assigner.LastPreOptimizationScore;
            LastGradeDispersionScore=assigner.LastDispersionScore;
            state.VariantHash=Phase1VariantHash.Compute(state);return true;
        }
        public bool TryGenerateGeometry(Phase1Difficulty difficulty,Phase1TileBagDefinition bag,int seed,bool prioritize,out Phase1BoardState state)
        {
            state=null;var random=new Random(seed);var specs=new List<Spec>();int id=0;
            foreach(Phase1TileShape s in Enum.GetValues(typeof(Phase1TileShape))) for(int i=0;i<bag.Count(s);i++) specs.Add(new Spec{Shape=s,Area=ShapeArea(s),Id=id++});
            specs=specs.OrderByDescending(x=>x.Area).ThenBy(_=>random.Next()).ToList(); var placed=new List<Phase1TilePlacement>();var occupied=new bool[config.BoardSize,config.BoardSize];
            if(!Place(0,specs,placed,occupied,difficulty,random,prioritize))return false;
            if(!Phase1PlacementValidator.ValidateFinal(placed,config.BoardSize,out _))return false;
            state=new Phase1BoardState{Difficulty=difficulty,BagId=bag.Id,Seed=seed};state.Tiles.AddRange(placed.OrderBy(x=>x.TileId));state.LayoutHash=Phase1LayoutHash.Compute(difficulty,bag.Id,state.Tiles);
            return true;
        }
        public static int DeriveGradeSeed(int boardSeed,string bagId,Phase1Difficulty difficulty)
        {
            string value=boardSeed+"|"+bagId+"|"+(int)difficulty+"|PHASE1_GRADE";
            using var sha=SHA256.Create();byte[] hash=sha.ComputeHash(Encoding.UTF8.GetBytes(value));
            int result=((hash[0]&0x7f)<<24)|(hash[1]<<16)|(hash[2]<<8)|hash[3];return result==0?1:result;
        }
        private bool Place(int index,List<Spec> specs,List<Phase1TilePlacement> placed,bool[,] occupied,Phase1Difficulty difficulty,Random random,bool prioritize)
        {
            if(index==specs.Count)return true;var s=specs[index];var dims=Dimensions(s.Shape);var candidates=new List<(Phase1TilePlacement p,int edge,int diag,int near,int orientation,int anchorDistance)>();
            foreach(var d in dims.OrderBy(_=>random.Next()))for(int y=0;y<=config.BoardSize-d.h;y++)for(int x=0;x<=config.BoardSize-d.w;x++){
                if(!Fits(x,y,d.w,d.h,occupied))continue;int baseHp=config.GetHp(difficulty,s.Shape);var p=new Phase1TilePlacement{TileId=s.Id,Shape=s.Shape,Role=Role(s.Shape),GridX=x,GridY=y,GridWidth=d.w,GridHeight=d.h,BaseHp=baseHp,MaxHp=baseHp};
                if(p.Role==Phase1TileRole.Core&&placed.Any(t=>t.Role==Phase1TileRole.Core&&Phase1PlacementValidator.EdgeTouch(t,p)))continue;
                var same=placed.Where(t=>t.Shape==p.Shape).ToList();int edge=same.Count(t=>Phase1PlacementValidator.EdgeTouch(t,p));int diag=same.Count(t=>Diagonal(t,p));int near=same.Count(t=>Near(t,p));int orientation=same.Count(t=>t.GridWidth==p.GridWidth&&t.GridHeight==p.GridHeight);int anchorDistance=p.Role==Phase1TileRole.Anchor?placed.Where(t=>t.Role==Phase1TileRole.Anchor).Select(t=>Distance2(t,p)).DefaultIfEmpty(999).Min():999;
                candidates.Add((p,edge,diag,near,orientation,anchorDistance));}
            IEnumerable<(Phase1TilePlacement p,int edge,int diag,int near,int orientation,int anchorDistance)> ordered=prioritize?candidates.OrderBy(c=>c.edge).ThenBy(c=>c.diag).ThenBy(c=>c.near).ThenBy(c=>c.orientation).ThenByDescending(c=>c.anchorDistance):candidates.OrderBy(_=>random.Next());
            foreach(var c in ordered.GroupBy(c=>prioritize?(c.edge,c.diag,c.near,c.orientation,c.anchorDistance):(0,0,0,0,0)).SelectMany(g=>g.OrderBy(_=>random.Next()))){Mark(c.p,occupied,true);placed.Add(c.p);if(Place(index+1,specs,placed,occupied,difficulty,random,prioritize))return true;placed.RemoveAt(placed.Count-1);Mark(c.p,occupied,false);}return false;
        }
        private static (int w,int h)[] Dimensions(Phase1TileShape s)=>s switch{Phase1TileShape.OneByOne=>new[]{(1,1)},Phase1TileShape.OneByTwo=>new[]{(2,1),(1,2)},Phase1TileShape.OneByThree=>new[]{(3,1),(1,3)},Phase1TileShape.TwoByTwo=>new[]{(2,2)},Phase1TileShape.TwoByThree=>new[]{(3,2),(2,3)},Phase1TileShape.ThreeByThree=>new[]{(3,3)},_=>Array.Empty<(int,int)>()};
        private static bool Fits(int x,int y,int w,int h,bool[,] o){for(int yy=y;yy<y+h;yy++)for(int xx=x;xx<x+w;xx++)if(o[xx,yy])return false;return true;}
        private static void Mark(Phase1TilePlacement p,bool[,] o,bool v){for(int y=p.GridY;y<p.GridY+p.GridHeight;y++)for(int x=p.GridX;x<p.GridX+p.GridWidth;x++)o[x,y]=v;}
        private static bool Diagonal(Phase1TilePlacement a,Phase1TilePlacement b)=>Math.Abs((a.GridX+a.GridWidth/2f)-(b.GridX+b.GridWidth/2f))<=Math.Max(a.GridWidth,b.GridWidth)&&Math.Abs((a.GridY+a.GridHeight/2f)-(b.GridY+b.GridHeight/2f))<=Math.Max(a.GridHeight,b.GridHeight)&&!Phase1PlacementValidator.EdgeTouch(a,b);
        private static bool Near(Phase1TilePlacement a,Phase1TilePlacement b)=>Math.Abs((a.GridX+a.GridWidth/2f)-(b.GridX+b.GridWidth/2f))<=Math.Max(a.GridWidth,b.GridWidth)+1&&Math.Abs((a.GridY+a.GridHeight/2f)-(b.GridY+b.GridHeight/2f))<=Math.Max(a.GridHeight,b.GridHeight)+1;
        private static int Distance2(Phase1TilePlacement a,Phase1TilePlacement b){int x=(a.GridX*2+a.GridWidth)-(b.GridX*2+b.GridWidth),y=(a.GridY*2+a.GridHeight)-(b.GridY*2+b.GridHeight);return x*x+y*y;}
    }
}
