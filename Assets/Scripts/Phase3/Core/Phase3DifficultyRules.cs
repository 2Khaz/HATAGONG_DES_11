using System;
using HATAGONG.GameFlow;

namespace HATAGONG.Phase3
{
    public readonly struct Phase3DifficultyRuleSet
    {
        public Phase3DifficultyRuleSet(
            int targetPieceCount,
            double snapDistance,
            double minimumPieceAreaRatio,
            double maximumPieceAreaRatio,
            double maximumAspectRatio,
            int maximumVertexCount)
        {
            TargetPieceCount = targetPieceCount;
            SnapDistance = snapDistance;
            MinimumPieceAreaRatio = minimumPieceAreaRatio;
            MaximumPieceAreaRatio = maximumPieceAreaRatio;
            MaximumAspectRatio = maximumAspectRatio;
            MaximumVertexCount = maximumVertexCount;
        }

        public int TargetPieceCount { get; }
        public double SnapDistance { get; }
        public double MinimumPieceAreaRatio { get; }
        public double MaximumPieceAreaRatio { get; }
        public double MaximumAspectRatio { get; }
        public int MaximumVertexCount { get; }
    }

    public static class Phase3DifficultyRules
    {
        public static Phase3DifficultyRuleSet For(GameDifficulty difficulty)
        {
            switch (difficulty)
            {
                case GameDifficulty.Easy:
                    return new Phase3DifficultyRuleSet(6, 45d, 0.10d, 0.35d, 2.2d, 4);
                case GameDifficulty.Normal:
                    return new Phase3DifficultyRuleSet(7, 40d, 0.07d, 0.30d, 2.6d, 4);
                case GameDifficulty.Hard:
                    return new Phase3DifficultyRuleSet(8, 35d, 0.05d, 0.28d, 3.0d, 5);
                default:
                    throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, "Phase 3 requires Easy, Normal, or Hard difficulty.");
            }
        }
    }
}
