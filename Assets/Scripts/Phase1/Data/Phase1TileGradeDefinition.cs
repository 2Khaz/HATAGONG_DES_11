using System;
using UnityEngine;

namespace HATAGONG.Phase1
{
    [Serializable]
    public sealed class Phase1TileGradeDefinition
    {
        [SerializeField] private Phase1TileGrade grade;
        [SerializeField] private string gradeId, displayName;
        [SerializeField, Range(0,7)] private int hpModifier;
        [SerializeField] private int easyWeight, normalWeight, hardWeight;
        [SerializeField] private int easyMinCount, normalMinCount, hardMinCount;
        [SerializeField] private int easyMaxCount, normalMaxCount, hardMaxCount;
        [SerializeField] private bool enabled=true;
        public Phase1TileGrade Grade=>grade; public string GradeId=>gradeId; public string DisplayName=>displayName; public int HpModifier=>hpModifier; public bool Enabled=>enabled;
        public int Weight(Phase1Difficulty d)=>d==Phase1Difficulty.Easy?easyWeight:d==Phase1Difficulty.Normal?normalWeight:hardWeight;
        public int MinCount(Phase1Difficulty d)=>d==Phase1Difficulty.Easy?easyMinCount:d==Phase1Difficulty.Normal?normalMinCount:hardMinCount;
        public int MaxCount(Phase1Difficulty d)=>d==Phase1Difficulty.Easy?easyMaxCount:d==Phase1Difficulty.Normal?normalMaxCount:hardMaxCount;
        public Phase1TileGradeDefinition(Phase1TileGrade grade,string id,string name,int modifier,int ew,int nw,int hw,int emin,int nmin,int hmin,int emax,int nmax,int hmax){this.grade=grade;gradeId=id;displayName=name;hpModifier=modifier;easyWeight=ew;normalWeight=nw;hardWeight=hw;easyMinCount=emin;normalMinCount=nmin;hardMinCount=hmin;easyMaxCount=emax;normalMaxCount=nmax;hardMaxCount=hmax;enabled=true;}
        public void EnsureMinimumCounts(int easy,int normal,int hard){easyMinCount=easy;normalMinCount=normal;hardMinCount=hard;}
    }
}
