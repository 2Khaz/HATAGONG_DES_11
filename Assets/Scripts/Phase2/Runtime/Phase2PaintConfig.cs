using System;

namespace HATAGONG.Phase2
{
    public sealed class Phase2PaintConfig
    {
        public int Width { get; }
        public int Height { get; }
        public int TotalCellCount => Width * Height;
        public double ClearRatio { get; }
        public int RequiredClearCells { get; }
        public double EasyRadiusRatio { get; }
        public double NormalRadiusRatio { get; }
        public double HardRadiusRatio { get; }
        public double StampSpacingRatio { get; }
        public int CoverageScoreBudget { get; }
        public int QuarterMilestoneBonus { get; }
        public int HalfMilestoneBonus { get; }
        public int ThreeQuarterMilestoneBonus { get; }
        public int ClearBonus { get; }

        public Phase2PaintConfig(
            int width,
            int height,
            double clearRatio,
            double easyRadiusRatio,
            double normalRadiusRatio,
            double hardRadiusRatio,
            double stampSpacingRatio,
            int coverageScoreBudget,
            int quarterMilestoneBonus,
            int halfMilestoneBonus,
            int threeQuarterMilestoneBonus,
            int clearBonus)
        {
            Width = width;
            Height = height;
            ClearRatio = clearRatio;
            EasyRadiusRatio = easyRadiusRatio;
            NormalRadiusRatio = normalRadiusRatio;
            HardRadiusRatio = hardRadiusRatio;
            StampSpacingRatio = stampSpacingRatio;
            CoverageScoreBudget = coverageScoreBudget;
            QuarterMilestoneBonus = quarterMilestoneBonus;
            HalfMilestoneBonus = halfMilestoneBonus;
            ThreeQuarterMilestoneBonus = threeQuarterMilestoneBonus;
            ClearBonus = clearBonus;
            RequiredClearCells = (int)Math.Ceiling(TotalCellCount * ClearRatio);
        }

        public static Phase2PaintConfig CreateProduction()
        {
            return new Phase2PaintConfig(
                width: 128,
                height: 128,
                clearRatio: 0.99d,
                easyRadiusRatio: 0.085d,
                normalRadiusRatio: 0.075d,
                hardRadiusRatio: 0.065d,
                stampSpacingRatio: 0.4d,
                coverageScoreBudget: 500,
                quarterMilestoneBonus: 100,
                halfMilestoneBonus: 150,
                threeQuarterMilestoneBonus: 200,
                clearBonus: 500);
        }

        public bool IsValid(out string reason)
        {
            if (Width <= 0 || Height <= 0)
            {
                reason = "invalid grid size";
                return false;
            }

            if (double.IsNaN(ClearRatio) || double.IsInfinity(ClearRatio) || ClearRatio <= 0d || ClearRatio > 1d)
            {
                reason = "invalid clear ratio";
                return false;
            }

            if (double.IsNaN(EasyRadiusRatio) || double.IsInfinity(EasyRadiusRatio) || EasyRadiusRatio <= 0d ||
                double.IsNaN(NormalRadiusRatio) || double.IsInfinity(NormalRadiusRatio) || NormalRadiusRatio <= 0d ||
                double.IsNaN(HardRadiusRatio) || double.IsInfinity(HardRadiusRatio) || HardRadiusRatio <= 0d)
            {
                reason = "invalid radius ratio";
                return false;
            }

            if (double.IsNaN(StampSpacingRatio) || double.IsInfinity(StampSpacingRatio) || StampSpacingRatio <= 0d)
            {
                reason = "invalid stamp spacing";
                return false;
            }

            if (CoverageScoreBudget < 0 || QuarterMilestoneBonus < 0 || HalfMilestoneBonus < 0 || ThreeQuarterMilestoneBonus < 0 || ClearBonus < 0)
            {
                reason = "invalid score configuration";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }

    [Flags]
    public enum Phase2MilestoneFlags
    {
        None = 0,
        Quarter = 1,
        Half = 2,
        ThreeQuarter = 4
    }

    public enum Phase2PaintSessionState
    {
        Ready,
        Running,
        Completing,
        Cleared
    }

    public enum Phase2PaintMutationRejectionReason
    {
        None,
        SessionNotPlaying,
        InputDisabled,
        PhaseNotRunning,
        InvalidStamp,
        NoBoardIntersection,
        AlreadyCompleting
    }
}
