using System;
using System.Collections.Generic;

namespace HATAGONG.Phase1
{
    [Serializable]
    public sealed class Phase1TilePlacement
    {
        public int TileId, GridX, GridY, GridWidth, GridHeight, BaseHp, GradeHpModifier, MaxHp;
        public Phase1TileShape Shape; public Phase1TileRole Role;
        public Phase1TileGrade Grade; public string GradeId,VisualSetId,UsedSpriteName; public bool VisualFallbackUsed,MinimumHpValid;
        public bool IsRotated => GridWidth < GridHeight && GridWidth != GridHeight;
    }

    public sealed class Phase1BoardState
    {
        public Phase1Difficulty Difficulty; public string BagId, LayoutHash, VariantHash; public int Seed;
        public readonly List<Phase1TilePlacement> Tiles = new();
    }
}
