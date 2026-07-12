using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HATAGONG.Phase1
{
    public sealed class Phase1GradeAssigner
    {
        private static bool fallbackWarned,rangeWarned;
        private readonly Phase1GameConfig config;
        public Phase1GradeAssigner(Phase1GameConfig config){this.config=config;}
        public bool TryAssign(Phase1Difficulty difficulty,List<Phase1TilePlacement> tiles,System.Random random,out string error)
        {
            error=null;List<Phase1TilePlacement> best=null;int bestDistance=int.MaxValue;config.GetModifierRange(difficulty,out int min,out int max);
            for(int attempt=0;attempt<config.GradeAssignmentAttempts;attempt++)
            {
                var copy=tiles.Select(CloneBase).ToList();if(!AssignOnce(difficulty,copy,random,out error))return false;int sum=copy.Sum(x=>x.GradeHpModifier);int distance=sum<min?min-sum:sum>max?sum-max:0;if(distance<bestDistance){best=copy;bestDistance=distance;}if(distance==0)break;
            }
            if(best==null){error="No valid grade assignment.";return false;}if(bestDistance>0&&!rangeWarned){rangeWarned=true;Debug.LogWarning($"[Phase1] Grade modifier range could not be met exactly; nearest valid assignment used (distance={bestDistance}).");}
            for(int i=0;i<tiles.Count;i++)CopyGrade(best[i],tiles[i]);return true;
        }
        private bool AssignOnce(Phase1Difficulty difficulty,List<Phase1TilePlacement> tiles,System.Random random,out string error)
        {
            error=null;var counts=new Dictionary<Phase1TileGrade,int>();
            foreach(var tile in tiles.OrderBy(x=>x.TileId))
            {
                var candidates=config.GradeDefinitions.Where(g=>g.Enabled&&tile.BaseHp+g.HpModifier>=config.MinimumFinalTileHp&&(g.MaxCount(difficulty)==0||!counts.TryGetValue(g.Grade,out int count)||count<g.MaxCount(difficulty))).ToList();
                if(candidates.Count==0){error=$"No valid grade for tile {tile.TileId}, baseHp={tile.BaseHp}.";return false;}
                var preferred=candidates.Where(g=>!WouldCrowd(g.Grade,tile,tiles,counts)).ToList();if(preferred.Count>0)candidates=preferred;
                var selected=Weighted(candidates,difficulty,random)??Fallback(candidates);
                if(selected==null){error=$"No weighted or fallback grade for tile {tile.TileId}.";return false;}
                if(selected.Weight(difficulty)<=0&&!fallbackWarned){fallbackWarned=true;Debug.LogWarning("[Phase1] Grade fallback was used because weighted candidates were unavailable.");}
                tile.Grade=selected.Grade;tile.GradeId=selected.GradeId;tile.BaseHp=Math.Max(0,tile.BaseHp);tile.GradeHpModifier=selected.HpModifier;tile.MaxHp=tile.BaseHp+tile.GradeHpModifier;tile.MinimumHpValid=tile.MaxHp>=config.MinimumFinalTileHp;tile.VisualSetId=config.GetVisualSetId(tile.Grade);tile.UsedSpriteName=string.Empty;tile.VisualFallbackUsed=true;
                if(!tile.MinimumHpValid){error=$"Final HP violation on tile {tile.TileId}.";return false;}counts[tile.Grade]=counts.TryGetValue(tile.Grade,out int n)?n+1:1;
            }
            return true;
        }
        private static Phase1TileGradeDefinition Weighted(List<Phase1TileGradeDefinition> list,Phase1Difficulty d,System.Random random){int total=list.Sum(x=>Math.Max(0,x.Weight(d)));if(total<=0)return null;int roll=random.Next(total);foreach(var x in list){roll-=Math.Max(0,x.Weight(d));if(roll<0)return x;}return list[^1];}
        private Phase1TileGradeDefinition Fallback(List<Phase1TileGradeDefinition> list)=>list.FirstOrDefault(x=>x.Grade==config.DefaultFallbackGrade&&x.HpModifier==0)??list.OrderBy(x=>Math.Abs(x.HpModifier)).ThenBy(x=>x.HpModifier<0?1:0).ThenBy(x=>x.HpModifier).FirstOrDefault();
        private static bool WouldCrowd(Phase1TileGrade grade,Phase1TilePlacement tile,List<Phase1TilePlacement> tiles,Dictionary<Phase1TileGrade,int> counts){int assigned=counts.Values.Sum();if(assigned>0&&counts.TryGetValue(grade,out int n)&&(n+1)*2>assigned+1)return true;var adjacent=tiles.Where(x=>x.TileId<tile.TileId&&x.Grade==grade&&Phase1PlacementValidator.EdgeTouch(x,tile)).ToList();if(grade==Phase1TileGrade.Marble&&adjacent.Count>0)return true;return adjacent.Count>=2||adjacent.Any(x=>x.Shape==tile.Shape);}
        private static Phase1TilePlacement CloneBase(Phase1TilePlacement t)=>new(){TileId=t.TileId,GridX=t.GridX,GridY=t.GridY,GridWidth=t.GridWidth,GridHeight=t.GridHeight,Shape=t.Shape,Role=t.Role,BaseHp=t.BaseHp,MaxHp=t.BaseHp};
        private static void CopyGrade(Phase1TilePlacement a,Phase1TilePlacement b){b.Grade=a.Grade;b.GradeId=a.GradeId;b.GradeHpModifier=a.GradeHpModifier;b.MaxHp=a.MaxHp;b.VisualSetId=a.VisualSetId;b.UsedSpriteName=a.UsedSpriteName;b.VisualFallbackUsed=a.VisualFallbackUsed;b.MinimumHpValid=a.MinimumHpValid;}
    }
}
