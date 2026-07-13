using System;

namespace HATAGONG.Phase2
{
    public static class Phase2PaintProgressRules
    {
        public static int CalculateRequiredClearCells(int totalCellCount, double clearRatio)
        {
            return (int)Math.Ceiling(totalCellCount * clearRatio);
        }

        public static double CalculateCoverage(int paintedCellCount, int totalCellCount)
        {
            if (totalCellCount <= 0) return 0d;
            double coverage = (double)paintedCellCount / totalCellCount;
            if (coverage < 0d) return 0d;
            if (coverage > 1d) return 1d;
            return coverage;
        }

        public static int CalculateCoverageScore(int paintedCellCount, int requiredClearCells, int coverageScoreBudget)
        {
            if (coverageScoreBudget <= 0 || requiredClearCells <= 0) return 0;
            int cappedPainted = paintedCellCount < requiredClearCells ? paintedCellCount : requiredClearCells;
            return (int)Math.Floor((double)cappedPainted / requiredClearCells * coverageScoreBudget);
        }

        public static Phase2MilestoneFlags CalculateMilestones(int paintedCellCount, int totalCellCount)
        {
            if (totalCellCount <= 0) return Phase2MilestoneFlags.None;
            double coverage = CalculateCoverage(paintedCellCount, totalCellCount);
            Phase2MilestoneFlags flags = Phase2MilestoneFlags.None;
            if (coverage >= 0.25d) flags |= Phase2MilestoneFlags.Quarter;
            if (coverage >= 0.50d) flags |= Phase2MilestoneFlags.Half;
            if (coverage >= 0.75d) flags |= Phase2MilestoneFlags.ThreeQuarter;
            return flags;
        }

        public static bool IsClearThresholdReached(int paintedCellCount, int requiredClearCells)
        {
            return paintedCellCount >= requiredClearCells;
        }
    }
}
