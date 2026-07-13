using System;

namespace HATAGONG.Phase2
{
    public sealed class Phase2PaintSessionModel
    {
        private bool _clearThresholdReached;
        private int _score;
        private Phase2MilestoneFlags _milestones;

        public Phase2PaintSessionModel(Phase2PaintConfig config)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            Grid = new Phase2PaintGrid(config);
            Reset();
        }

        public Phase2PaintConfig Config { get; }
        public Phase2PaintGrid Grid { get; }
        public Phase2PaintSessionState CurrentState { get; private set; }
        public int Score => _score;
        public Phase2MilestoneFlags Milestones => _milestones;
        public bool IsRunning => CurrentState == Phase2PaintSessionState.Running;
        public bool IsCompleting => CurrentState == Phase2PaintSessionState.Completing;
        public bool IsCleared => CurrentState == Phase2PaintSessionState.Cleared;

        public void Start()
        {
            if (CurrentState == Phase2PaintSessionState.Ready)
            {
                CurrentState = Phase2PaintSessionState.Running;
            }
        }

        public void Reset()
        {
            CurrentState = Phase2PaintSessionState.Ready;
            _clearThresholdReached = false;
            _score = 0;
            _milestones = Phase2MilestoneFlags.None;
            Grid.Reset();
        }

        public Phase2StampResult ApplyStamp(float centerU, float centerV, float radiusRatio, bool sessionPlaying, bool inputEnabled)
        {
            if (!Config.IsValid(out string reason))
            {
                return Phase2StampResult.Rejected(Phase2PaintMutationRejectionReason.InvalidStamp, CurrentState, CurrentState, 0, Grid.PaintedCellCount, Config.TotalCellCount, 0d, 0d, 0, 0, 0, 0, Phase2MilestoneFlags.None, false);
            }

            Phase2PaintSessionState stateBefore = CurrentState;
            if (!sessionPlaying)
            {
                return Phase2StampResult.Rejected(Phase2PaintMutationRejectionReason.SessionNotPlaying, stateBefore, stateBefore, 0, Grid.PaintedCellCount, Config.TotalCellCount, CalculateCoverage(Grid.PaintedCellCount), CalculateCoverage(Grid.PaintedCellCount), 0, 0, 0, 0, _milestones, false);
            }

            if (!inputEnabled)
            {
                return Phase2StampResult.Rejected(Phase2PaintMutationRejectionReason.InputDisabled, stateBefore, stateBefore, 0, Grid.PaintedCellCount, Config.TotalCellCount, CalculateCoverage(Grid.PaintedCellCount), CalculateCoverage(Grid.PaintedCellCount), 0, 0, 0, 0, _milestones, false);
            }

            if (CurrentState == Phase2PaintSessionState.Completing || CurrentState == Phase2PaintSessionState.Cleared)
            {
                return Phase2StampResult.Rejected(Phase2PaintMutationRejectionReason.AlreadyCompleting, stateBefore, stateBefore, 0, Grid.PaintedCellCount, Config.TotalCellCount, CalculateCoverage(Grid.PaintedCellCount), CalculateCoverage(Grid.PaintedCellCount), 0, 0, 0, 0, _milestones, false);
            }

            if (CurrentState != Phase2PaintSessionState.Running)
            {
                return Phase2StampResult.Rejected(Phase2PaintMutationRejectionReason.PhaseNotRunning, stateBefore, stateBefore, 0, Grid.PaintedCellCount, Config.TotalCellCount, CalculateCoverage(Grid.PaintedCellCount), CalculateCoverage(Grid.PaintedCellCount), 0, 0, 0, 0, _milestones, false);
            }

            if (float.IsNaN(centerU) || float.IsNaN(centerV) || float.IsInfinity(centerU) || float.IsInfinity(centerV) || float.IsNaN(radiusRatio) || float.IsInfinity(radiusRatio) || radiusRatio <= 0f)
            {
                return Phase2StampResult.Rejected(Phase2PaintMutationRejectionReason.InvalidStamp, stateBefore, stateBefore, 0, Grid.PaintedCellCount, Config.TotalCellCount, CalculateCoverage(Grid.PaintedCellCount), CalculateCoverage(Grid.PaintedCellCount), 0, 0, 0, 0, _milestones, false);
            }

            if (!Phase2PaintGeometry.IsCircleIntersectingUnitBoard(centerU, centerV, radiusRatio))
            {
                return Phase2StampResult.Rejected(Phase2PaintMutationRejectionReason.NoBoardIntersection, stateBefore, stateBefore, 0, Grid.PaintedCellCount, Config.TotalCellCount, CalculateCoverage(Grid.PaintedCellCount), CalculateCoverage(Grid.PaintedCellCount), 0, 0, 0, 0, _milestones, false);
            }

            int paintedBefore = Grid.PaintedCellCount;
            int newlyPainted = Grid.ApplyStamp(centerU, centerV, radiusRatio, Config);
            int paintedAfter = paintedBefore + newlyPainted;
            double coverageBefore = CalculateCoverage(paintedBefore);
            double coverageAfter = CalculateCoverage(paintedAfter);
            Phase2MilestoneFlags flagsBefore = CalculateMilestones(paintedBefore);
            Phase2MilestoneFlags flagsAfter = CalculateMilestones(paintedAfter);
            Phase2MilestoneFlags newlyReachedMilestones = flagsAfter & ~flagsBefore;

            int coverageScoreBefore = CalculateCoverageScore(paintedBefore);
            int coverageScoreAfter = CalculateCoverageScore(paintedAfter);
            int coverageScoreDelta = coverageScoreAfter - coverageScoreBefore;
            int milestoneScoreDelta = CalculateMilestoneScoreDelta(newlyReachedMilestones);
            int clearScoreDelta = 0;
            bool clearThresholdReached = false;

            if (!_clearThresholdReached && paintedAfter >= Config.RequiredClearCells)
            {
                clearThresholdReached = true;
                clearScoreDelta = Config.ClearBonus;
                _clearThresholdReached = true;
                CurrentState = Phase2PaintSessionState.Completing;
            }

            _score += coverageScoreDelta + milestoneScoreDelta + clearScoreDelta;
            _milestones = flagsAfter;

            return new Phase2StampResult(
                accepted: true,
                rejectionReason: Phase2PaintMutationRejectionReason.None,
                newlyPaintedCellCount: newlyPainted,
                totalPaintedCellCount: paintedAfter,
                totalCellCount: Config.TotalCellCount,
                coverageBefore: coverageBefore,
                coverageAfter: coverageAfter,
                coverageScoreDelta: coverageScoreDelta,
                milestoneScoreDelta: milestoneScoreDelta,
                clearScoreDelta: clearScoreDelta,
                totalScoreDelta: coverageScoreDelta + milestoneScoreDelta + clearScoreDelta,
                reachedMilestones: newlyReachedMilestones,
                clearThresholdReached: clearThresholdReached,
                stateBefore: stateBefore,
                stateAfter: CurrentState);
        }

        private double CalculateCoverage(int paintedCellCount)
        {
            return Phase2PaintProgressRules.CalculateCoverage(paintedCellCount, Config.TotalCellCount);
        }

        private int CalculateCoverageScore(int paintedCellCount)
        {
            return Phase2PaintProgressRules.CalculateCoverageScore(paintedCellCount, Config.RequiredClearCells, Config.CoverageScoreBudget);
        }

        private Phase2MilestoneFlags CalculateMilestones(int paintedCellCount)
        {
            return Phase2PaintProgressRules.CalculateMilestones(paintedCellCount, Config.TotalCellCount);
        }

        private int CalculateMilestoneScoreDelta(Phase2MilestoneFlags flags)
        {
            int total = 0;
            if ((flags & Phase2MilestoneFlags.Quarter) != 0) total += Config.QuarterMilestoneBonus;
            if ((flags & Phase2MilestoneFlags.Half) != 0) total += Config.HalfMilestoneBonus;
            if ((flags & Phase2MilestoneFlags.ThreeQuarter) != 0) total += Config.ThreeQuarterMilestoneBonus;
            return total;
        }
    }

    public readonly struct Phase2StampResult
    {
        public Phase2StampResult(
            bool accepted,
            Phase2PaintMutationRejectionReason rejectionReason,
            int newlyPaintedCellCount,
            int totalPaintedCellCount,
            int totalCellCount,
            double coverageBefore,
            double coverageAfter,
            int coverageScoreDelta,
            int milestoneScoreDelta,
            int clearScoreDelta,
            int totalScoreDelta,
            Phase2MilestoneFlags reachedMilestones,
            bool clearThresholdReached,
            Phase2PaintSessionState stateBefore,
            Phase2PaintSessionState stateAfter)
        {
            Accepted = accepted;
            RejectionReason = rejectionReason;
            NewlyPaintedCellCount = newlyPaintedCellCount;
            TotalPaintedCellCount = totalPaintedCellCount;
            TotalCellCount = totalCellCount;
            CoverageBefore = coverageBefore;
            CoverageAfter = coverageAfter;
            CoverageScoreDelta = coverageScoreDelta;
            MilestoneScoreDelta = milestoneScoreDelta;
            ClearScoreDelta = clearScoreDelta;
            TotalScoreDelta = totalScoreDelta;
            ReachedMilestones = reachedMilestones;
            ClearThresholdReached = clearThresholdReached;
            StateBefore = stateBefore;
            StateAfter = stateAfter;
        }

        public bool Accepted { get; }
        public Phase2PaintMutationRejectionReason RejectionReason { get; }
        public int NewlyPaintedCellCount { get; }
        public int TotalPaintedCellCount { get; }
        public int TotalCellCount { get; }
        public double CoverageBefore { get; }
        public double CoverageAfter { get; }
        public int CoverageScoreDelta { get; }
        public int MilestoneScoreDelta { get; }
        public int ClearScoreDelta { get; }
        public int TotalScoreDelta { get; }
        public Phase2MilestoneFlags ReachedMilestones { get; }
        public bool ClearThresholdReached { get; }
        public Phase2PaintSessionState StateBefore { get; }
        public Phase2PaintSessionState StateAfter { get; }

        public static Phase2StampResult Rejected(
            Phase2PaintMutationRejectionReason rejectionReason,
            Phase2PaintSessionState stateBefore,
            Phase2PaintSessionState stateAfter,
            int newlyPaintedCellCount,
            int totalPaintedCellCount,
            int totalCellCount,
            double coverageBefore,
            double coverageAfter,
            int coverageScoreDelta,
            int milestoneScoreDelta,
            int clearScoreDelta,
            int totalScoreDelta,
            Phase2MilestoneFlags reachedMilestones,
            bool clearThresholdReached)
        {
            return new Phase2StampResult(false, rejectionReason, newlyPaintedCellCount, totalPaintedCellCount, totalCellCount, coverageBefore, coverageAfter, coverageScoreDelta, milestoneScoreDelta, clearScoreDelta, totalScoreDelta, reachedMilestones, clearThresholdReached, stateBefore, stateAfter);
        }
    }
}
