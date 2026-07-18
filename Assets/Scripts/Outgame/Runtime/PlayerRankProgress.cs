using System;

namespace HATAGONG.Outgame
{
    public readonly struct PlayerRankProgress
    {
        private PlayerRankProgress(string rankName, int clearedStageCount, int nextPromotionThreshold)
        {
            RankName = rankName;
            ClearedStageCount = clearedStageCount;
            NextPromotionThreshold = nextPromotionThreshold;
        }

        public string RankName { get; }
        public int ClearedStageCount { get; }
        public int NextPromotionThreshold { get; }
        public bool IsMaxRank => NextPromotionThreshold < 0;
        public string ProgressDisplayText => IsMaxRank
            ? $"{ClearedStageCount}/MAX"
            : $"{ClearedStageCount}/{NextPromotionThreshold}";

        public static PlayerRankProgress Evaluate(int clearedStageCount)
        {
            int count = Math.Max(0, clearedStageCount);
            if (count < 100) return new PlayerRankProgress("사원", count, 100);
            if (count < 250) return new PlayerRankProgress("대리", count, 250);
            if (count < 400) return new PlayerRankProgress("과장", count, 400);
            if (count < 600) return new PlayerRankProgress("팀장", count, 600);
            if (count < 850) return new PlayerRankProgress("이사", count, 850);
            return new PlayerRankProgress("사장", count, -1);
        }
    }
}
