using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HATAGONG.Phase1
{
    public readonly struct Phase1GradeDispersionScore : IComparable<Phase1GradeDispersionScore>
    {
        public Phase1GradeDispersionScore(int marbleEdges,int sameGradeEdges,int largestComponent,int sameShapeEdges)
        {
            MarbleEdges=marbleEdges;SameGradeEdges=sameGradeEdges;LargestComponent=largestComponent;SameShapeEdges=sameShapeEdges;
        }
        public int MarbleEdges { get; }
        public int SameGradeEdges { get; }
        public int LargestComponent { get; }
        public int SameShapeEdges { get; }
        public int CompareTo(Phase1GradeDispersionScore other)
        {
            int result=MarbleEdges.CompareTo(other.MarbleEdges);if(result!=0)return result;
            result=SameGradeEdges.CompareTo(other.SameGradeEdges);if(result!=0)return result;
            result=LargestComponent.CompareTo(other.LargestComponent);if(result!=0)return result;
            return SameShapeEdges.CompareTo(other.SameShapeEdges);
        }
        public override string ToString()=>$"marbleEdges={MarbleEdges}, sameEdges={SameGradeEdges}, largest={LargestComponent}, sameShapeEdges={SameShapeEdges}";
    }

    public sealed class Phase1GradeAssigner
    {
        private const int MaximumSwapPasses=32;
        private static bool fallbackWarned,rangeWarned;
        private readonly Phase1GameConfig config;
        private readonly Dictionary<Phase1TileGrade,int> lastFallbackGrades=new();

        public Phase1GradeAssigner(Phase1GameConfig config){this.config=config;}
        public int LastFallbackCount { get; private set; }
        public IReadOnlyDictionary<Phase1TileGrade,int> LastFallbackGrades=>lastFallbackGrades;
        public Phase1GradeDispersionScore LastPreOptimizationScore { get; private set; }
        public Phase1GradeDispersionScore LastDispersionScore { get; private set; }

        public bool TryAssign(Phase1Difficulty difficulty,List<Phase1TilePlacement> tiles,System.Random random,out string error)
        {
            error=null;List<Phase1TilePlacement> best=null;int bestDistance=int.MaxValue;Phase1GradeDispersionScore bestScore=default,bestPreScore=default;int bestFallbacks=0;Dictionary<Phase1TileGrade,int> bestFallbackGrades=null;
            config.GetModifierRange(difficulty,out int min,out int max);
            for(int attempt=0;attempt<config.GradeAssignmentAttempts;attempt++)
            {
                var copy=tiles.Select(CloneBase).ToList();
                if(!AssignOnce(difficulty,copy,random,out int fallbackCount,out Dictionary<Phase1TileGrade,int> fallbackGrades,out Phase1GradeDispersionScore preScore,out Phase1GradeDispersionScore score,out error))return false;
                int sum=copy.Sum(x=>x.GradeHpModifier);int distance=sum<min?min-sum:sum>max?sum-max:0;
                if(best==null||distance<bestDistance||(distance==bestDistance&&score.CompareTo(bestScore)<0))
                {
                    best=copy;bestDistance=distance;bestScore=score;bestPreScore=preScore;bestFallbacks=fallbackCount;bestFallbackGrades=fallbackGrades;
                }
            }
            if(best==null){error="No valid grade assignment.";return false;}
            if(bestDistance>0&&!rangeWarned){rangeWarned=true;Debug.LogWarning($"[Phase1] Grade modifier range could not be met exactly; nearest valid assignment used (distance={bestDistance}).");}
            if(bestFallbacks>0&&!fallbackWarned){fallbackWarned=true;Debug.LogWarning($"[Phase1] Grade fallback was used {bestFallbacks} time(s) because weighted candidates were unavailable.");}
            for(int i=0;i<tiles.Count;i++)CopyGrade(best[i],tiles[i]);
            LastFallbackCount=bestFallbacks;lastFallbackGrades.Clear();if(bestFallbackGrades!=null)foreach(var pair in bestFallbackGrades)lastFallbackGrades[pair.Key]=pair.Value;
            LastPreOptimizationScore=bestPreScore;LastDispersionScore=bestScore;return true;
        }

        private bool AssignOnce(Phase1Difficulty difficulty,List<Phase1TilePlacement> tiles,System.Random random,out int fallbackCount,out Dictionary<Phase1TileGrade,int> fallbackGrades,out Phase1GradeDispersionScore preScore,out Phase1GradeDispersionScore score,out string error)
        {
            fallbackCount=0;fallbackGrades=new Dictionary<Phase1TileGrade,int>();preScore=default;score=default;error=null;
            if(!BuildGradePool(difficulty,tiles.Count,random,out List<Phase1TileGradeDefinition> pool,out fallbackCount,out fallbackGrades,out error))return false;
            List<int>[] adjacency=BuildAdjacency(tiles);
            if(!AssignSpatially(tiles,pool,adjacency,random,out Phase1TileGradeDefinition[] assigned,out error))return false;
            preScore=EvaluateAssigned(tiles,assigned,adjacency);
            OptimizeSwaps(tiles,assigned,adjacency,random);
            score=EvaluateAssigned(tiles,assigned,adjacency);
            for(int i=0;i<tiles.Count;i++)
            {
                ApplyGrade(tiles[i],assigned[i]);
                if(!tiles[i].MinimumHpValid){error=$"Final HP violation on tile {tiles[i].TileId}.";return false;}
            }
            return true;
        }

        private bool BuildGradePool(Phase1Difficulty difficulty,int tileCount,System.Random random,out List<Phase1TileGradeDefinition> pool,out int fallbackCount,out Dictionary<Phase1TileGrade,int> fallbackGrades,out string error)
        {
            pool=new List<Phase1TileGradeDefinition>(tileCount);fallbackCount=0;fallbackGrades=new Dictionary<Phase1TileGrade,int>();error=null;
            List<Phase1TileGradeDefinition> enabled=config.GradeDefinitions.Where(x=>x.Enabled).OrderBy(x=>x.Grade).ToList();
            foreach(Phase1TileGradeDefinition definition in enabled)
            {
                int minimum=definition.MinCount(difficulty),maximum=definition.MaxCount(difficulty);
                if(minimum<0||(maximum>0&&minimum>maximum)){error=$"Invalid Min/Max for grade {definition.Grade}.";return false;}
                for(int i=0;i<minimum;i++)pool.Add(definition);
            }
            if(pool.Count>tileCount){error=$"Grade minimum total {pool.Count} exceeds tile count {tileCount}.";return false;}
            while(pool.Count<tileCount)
            {
                var counts=pool.GroupBy(x=>x.Grade).ToDictionary(x=>x.Key,x=>x.Count());
                List<Phase1TileGradeDefinition> candidates=enabled.Where(x=>x.MaxCount(difficulty)==0||!counts.TryGetValue(x.Grade,out int count)||count<x.MaxCount(difficulty)).ToList();
                if(candidates.Count==0){error=$"No grade remains below MaxCount for slot {pool.Count}.";return false;}
                Phase1TileGradeDefinition selected=Weighted(candidates,difficulty,random);
                if(selected==null)
                {
                    selected=Fallback(candidates,counts,random);fallbackCount++;
                    if(selected!=null)fallbackGrades[selected.Grade]=fallbackGrades.TryGetValue(selected.Grade,out int count)?count+1:1;
                }
                if(selected==null){error=$"No weighted or fallback grade for slot {pool.Count}.";return false;}
                pool.Add(selected);
            }
            return true;
        }

        private bool AssignSpatially(List<Phase1TilePlacement> tiles,List<Phase1TileGradeDefinition> pool,List<int>[] adjacency,System.Random random,out Phase1TileGradeDefinition[] assigned,out string error)
        {
            error=null;assigned=new Phase1TileGradeDefinition[tiles.Count];
            var remaining=pool.GroupBy(x=>x.Grade).ToDictionary(x=>x.Key,x=>x.Count());
            var definitions=pool.GroupBy(x=>x.Grade).ToDictionary(x=>x.Key,x=>x.First());
            int[] tieKeys=Enumerable.Range(0,tiles.Count).Select(_=>random.Next()).ToArray();
            int[] order=Enumerable.Range(0,tiles.Count).OrderByDescending(x=>adjacency[x].Count).ThenBy(x=>tieKeys[x]).ThenBy(x=>tiles[x].TileId).ToArray();
            foreach(int tileIndex in order)
            {
                var candidates=remaining.Where(x=>x.Value>0).Select(x=>definitions[x.Key]).Where(x=>tiles[tileIndex].BaseHp+x.HpModifier>=config.MinimumFinalTileHp).ToList();
                if(candidates.Count==0){error=$"No spatial grade candidate for tile {tiles[tileIndex].TileId}.";return false;}
                Phase1GradeDispersionScore bestScore=default;var best=new List<Phase1TileGradeDefinition>();bool hasBest=false;
                foreach(Phase1TileGradeDefinition candidate in candidates)
                {
                    assigned[tileIndex]=candidate;Phase1GradeDispersionScore candidateScore=EvaluateAssigned(tiles,assigned,adjacency);assigned[tileIndex]=null;
                    int comparison=hasBest?candidateScore.CompareTo(bestScore):-1;
                    if(!hasBest||comparison<0){hasBest=true;bestScore=candidateScore;best.Clear();best.Add(candidate);}
                    else if(comparison==0)best.Add(candidate);
                }
                Phase1TileGradeDefinition selected=best[random.Next(best.Count)];assigned[tileIndex]=selected;remaining[selected.Grade]--;
            }
            return true;
        }

        private void OptimizeSwaps(List<Phase1TilePlacement> tiles,Phase1TileGradeDefinition[] assigned,List<int>[] adjacency,System.Random random)
        {
            var pairs=new List<(int a,int b,int tie)>();for(int a=0;a<assigned.Length;a++)for(int b=a+1;b<assigned.Length;b++)pairs.Add((a,b,random.Next()));pairs=pairs.OrderBy(x=>x.tie).ThenBy(x=>x.a).ThenBy(x=>x.b).ToList();
            for(int pass=0;pass<MaximumSwapPasses;pass++)
            {
                Phase1GradeDispersionScore current=EvaluateAssigned(tiles,assigned,adjacency),best=current;int bestA=-1,bestB=-1;
                foreach(var pair in pairs)
                {
                    if(assigned[pair.a].Grade==assigned[pair.b].Grade)continue;
                    if(tiles[pair.a].BaseHp+assigned[pair.b].HpModifier<config.MinimumFinalTileHp||tiles[pair.b].BaseHp+assigned[pair.a].HpModifier<config.MinimumFinalTileHp)continue;
                    (assigned[pair.a],assigned[pair.b])=(assigned[pair.b],assigned[pair.a]);Phase1GradeDispersionScore candidate=EvaluateAssigned(tiles,assigned,adjacency);(assigned[pair.a],assigned[pair.b])=(assigned[pair.b],assigned[pair.a]);
                    if(candidate.CompareTo(best)<0){best=candidate;bestA=pair.a;bestB=pair.b;}
                }
                if(bestA<0)break;(assigned[bestA],assigned[bestB])=(assigned[bestB],assigned[bestA]);
            }
        }

        public static Phase1GradeDispersionScore EvaluateDispersion(IReadOnlyList<Phase1TilePlacement> tiles)
        {
            List<int>[] adjacency=BuildAdjacency(tiles);Phase1TileGrade?[] grades=tiles.Select(x=>(Phase1TileGrade?)x.Grade).ToArray();return EvaluateGrades(tiles,grades,adjacency);
        }
        public static int LargestComponent(IReadOnlyList<Phase1TilePlacement> tiles,Phase1TileGrade grade)
        {
            List<int>[] adjacency=BuildAdjacency(tiles);Phase1TileGrade?[] grades=tiles.Select(x=>(Phase1TileGrade?)x.Grade).ToArray();return LargestComponent(grades,adjacency,grade);
        }
        private static Phase1GradeDispersionScore EvaluateAssigned(IReadOnlyList<Phase1TilePlacement> tiles,Phase1TileGradeDefinition[] assigned,List<int>[] adjacency)
        {
            Phase1TileGrade?[] grades=assigned.Select(x=>x==null?(Phase1TileGrade?)null:x.Grade).ToArray();return EvaluateGrades(tiles,grades,adjacency);
        }
        private static Phase1GradeDispersionScore EvaluateGrades(IReadOnlyList<Phase1TilePlacement> tiles,Phase1TileGrade?[] grades,List<int>[] adjacency)
        {
            int marble=0,same=0,sameShape=0,largest=0;
            for(int i=0;i<tiles.Count;i++)
            {
                if(!grades[i].HasValue)continue;
                for(int n=0;n<adjacency[i].Count;n++){int j=adjacency[i][n];if(j<=i||!grades[j].HasValue||grades[j].Value!=grades[i].Value)continue;same++;if(grades[i].Value==Phase1TileGrade.Marble)marble++;if(tiles[i].Shape==tiles[j].Shape)sameShape++;}
            }
            foreach(Phase1TileGrade grade in Enum.GetValues(typeof(Phase1TileGrade)))largest=Math.Max(largest,LargestComponent(grades,adjacency,grade));
            return new Phase1GradeDispersionScore(marble,same,largest,sameShape);
        }
        private static int LargestComponent(Phase1TileGrade?[] grades,List<int>[] adjacency,Phase1TileGrade grade)
        {
            int largest=0;var seen=new HashSet<int>();
            for(int i=0;i<grades.Length;i++)
            {
                if(seen.Contains(i)||grades[i]!=grade)continue;int count=0;var queue=new Queue<int>();queue.Enqueue(i);seen.Add(i);
                while(queue.Count>0){int current=queue.Dequeue();count++;foreach(int next in adjacency[current])if(!seen.Contains(next)&&grades[next]==grade){seen.Add(next);queue.Enqueue(next);}}
                largest=Math.Max(largest,count);
            }
            return largest;
        }
        private static List<int>[] BuildAdjacency(IReadOnlyList<Phase1TilePlacement> tiles)
        {
            var result=Enumerable.Range(0,tiles.Count).Select(_=>new List<int>()).ToArray();
            for(int i=0;i<tiles.Count;i++)for(int j=i+1;j<tiles.Count;j++)if(Phase1PlacementValidator.EdgeTouch(tiles[i],tiles[j])){result[i].Add(j);result[j].Add(i);}return result;
        }
        private static Phase1TileGradeDefinition Weighted(List<Phase1TileGradeDefinition> list,Phase1Difficulty difficulty,System.Random random)
        {
            int total=list.Sum(x=>Math.Max(0,x.Weight(difficulty)));if(total<=0)return null;int roll=random.Next(total);foreach(var value in list){roll-=Math.Max(0,value.Weight(difficulty));if(roll<0)return value;}return list[^1];
        }
        private static Phase1TileGradeDefinition Fallback(List<Phase1TileGradeDefinition> list,Dictionary<Phase1TileGrade,int> counts,System.Random random)
        {
            int minimum=list.Min(x=>counts.TryGetValue(x.Grade,out int count)?count:0);var least=list.Where(x=>(counts.TryGetValue(x.Grade,out int count)?count:0)==minimum).OrderBy(x=>x.HpModifier).ThenBy(x=>x.Grade).ToList();return least.Count==0?null:least[random.Next(least.Count)];
        }
        private void ApplyGrade(Phase1TilePlacement tile,Phase1TileGradeDefinition selected)
        {
            tile.Grade=selected.Grade;tile.GradeId=selected.GradeId;tile.BaseHp=Math.Max(0,tile.BaseHp);tile.GradeHpModifier=selected.HpModifier;tile.MaxHp=tile.BaseHp+tile.GradeHpModifier;tile.MinimumHpValid=tile.MaxHp>=config.MinimumFinalTileHp;tile.VisualSetId=config.GetVisualSetId(tile.Grade);tile.UsedSpriteName=string.Empty;tile.VisualFallbackUsed=true;
        }
        private static Phase1TilePlacement CloneBase(Phase1TilePlacement tile)=>new(){TileId=tile.TileId,GridX=tile.GridX,GridY=tile.GridY,GridWidth=tile.GridWidth,GridHeight=tile.GridHeight,Shape=tile.Shape,Role=tile.Role,BaseHp=tile.BaseHp,MaxHp=tile.BaseHp};
        private static void CopyGrade(Phase1TilePlacement source,Phase1TilePlacement target){target.Grade=source.Grade;target.GradeId=source.GradeId;target.GradeHpModifier=source.GradeHpModifier;target.MaxHp=source.MaxHp;target.VisualSetId=source.VisualSetId;target.UsedSpriteName=source.UsedSpriteName;target.VisualFallbackUsed=source.VisualFallbackUsed;target.MinimumHpValid=source.MinimumHpValid;}
    }
}
