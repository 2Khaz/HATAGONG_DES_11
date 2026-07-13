namespace HATAGONG.Phase2
{
    public sealed class Phase2PaintGrid
    {
        private readonly bool[] _paintedCells;

        public Phase2PaintGrid(Phase2PaintConfig config)
        {
            _paintedCells = new bool[config.TotalCellCount];
        }

        public int PaintedCellCount { get; private set; }
        public int TotalCellCount => _paintedCells.Length;

        public int ApplyStamp(float centerU, float centerV, float radiusRatio, Phase2PaintConfig config)
        {
            int newlyPainted = 0;
            double radiusSquared = radiusRatio * radiusRatio;
            double minU = centerU - radiusRatio;
            double maxU = centerU + radiusRatio;
            double minV = centerV - radiusRatio;
            double maxV = centerV + radiusRatio;

            int minX = (int)(minU * config.Width);
            int maxX = (int)(maxU * config.Width);
            int minY = (int)(minV * config.Height);
            int maxY = (int)(maxV * config.Height);

            minX = ClampIndex(minX, 0, config.Width - 1);
            maxX = ClampIndex(maxX, 0, config.Width - 1);
            minY = ClampIndex(minY, 0, config.Height - 1);
            maxY = ClampIndex(maxY, 0, config.Height - 1);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    double cellCenterU = (x + 0.5d) / config.Width;
                    double cellCenterV = (y + 0.5d) / config.Height;
                    double distanceSquared = (cellCenterU - centerU) * (cellCenterU - centerU) + (cellCenterV - centerV) * (cellCenterV - centerV);
                    if (distanceSquared > radiusSquared)
                    {
                        continue;
                    }

                    int index = y * config.Width + x;
                    if (_paintedCells[index])
                    {
                        continue;
                    }

                    _paintedCells[index] = true;
                    PaintedCellCount++;
                    newlyPainted++;
                }
            }

            return newlyPainted;
        }

        public void Reset()
        {
            for (int i = 0; i < _paintedCells.Length; i++)
            {
                _paintedCells[i] = false;
            }
            PaintedCellCount = 0;
        }

        private static int ClampIndex(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
