using System;
using HATAGONG.GameFlow;

namespace HATAGONG.Phase3
{
    public static class Phase3ScoreRules
    {
        public const int PiecePickScore = 0;
        public const int PieceRotateScore = 0;
        public const int LooseDropScore = 0;
        public const int WrongPlacementScore = 0;
        public const int TileGrinderScore = 0;
        public const int ManualSnapScore = 200;
        public const int TileCutterAutoPlaceScore = 100;
        public const int PhaseClearScore = 1000;
        public const int ConsecutiveSnapBonus = 0;

        public static int GetManualSnapScore() => ManualSnapScore;
        public static int GetTileCutterAutoPlaceScore() => TileCutterAutoPlaceScore;
        public static int GetPhaseClearScore() => PhaseClearScore;

        public static int CalculateMaximumDirectPlayScore(int pieceCount)
        {
            if (pieceCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pieceCount), pieceCount, "Piece count cannot be negative.");
            }

            return checked(pieceCount * ManualSnapScore + PhaseClearScore);
        }

        public static int GetMaximumDirectPlayScore(GameDifficulty difficulty)
        {
            return CalculateMaximumDirectPlayScore(Phase3DifficultyRules.For(difficulty).TargetPieceCount);
        }
    }
}
