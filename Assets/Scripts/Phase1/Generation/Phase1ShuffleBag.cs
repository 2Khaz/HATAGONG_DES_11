using System;
using System.Collections.Generic;
using System.Linq;

namespace HATAGONG.Phase1
{
    public sealed class Phase1ShuffleBag
    {
        private readonly Dictionary<Phase1Difficulty,List<string>> remaining=new();
        public IEnumerable<string> OrderedCandidates(Phase1Difficulty difficulty, IEnumerable<Phase1TileBagDefinition> all, Random random)
        {
            var ids=all.Where(x=>x.Difficulty==difficulty).Select(x=>x.Id).ToList();
            if(!remaining.TryGetValue(difficulty,out var list)||list.Count==0) { list=ids.OrderBy(_=>random.Next()).ToList(); remaining[difficulty]=list; }
            foreach(var id in list.ToArray()) yield return id;
            foreach(var id in ids.Where(x=>!list.Contains(x)).OrderBy(_=>random.Next())) yield return id;
        }
        public void MarkSuccessful(Phase1Difficulty difficulty,string id) { if(remaining.TryGetValue(difficulty,out var list)) list.Remove(id); }
    }
}
