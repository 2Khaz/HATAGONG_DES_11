using System;
using System.Collections.Generic;

namespace HATAGONG.Phase2
{
    public sealed class Phase2PaintGrid
    {
        private readonly bool[] _paintedCells;
        private readonly byte[] _coatCounts;
        private readonly byte[] _requiredCoats;
        private readonly int[] _overlayRemovedStrokeIds;
        private int _implicitStrokeId;

        public Phase2PaintGrid(Phase2PaintConfig config, int? chemicalSeed = null)
        {
            _paintedCells = new bool[config.TotalCellCount];
            _coatCounts = new byte[config.TotalCellCount];
            _requiredCoats = new byte[config.TotalCellCount];
            _overlayRemovedStrokeIds = new int[config.TotalCellCount];
            for (int i = 0; i < _requiredCoats.Length; i++) _requiredCoats[i] = 1;
            if (chemicalSeed.HasValue) ConfigureChemicalCells(chemicalSeed.Value, config);
        }

        public int PaintedCellCount { get; private set; }
        public int TotalCellCount => _paintedCells.Length;
        public bool IsPainted(int index) => index >= 0 && index < _paintedCells.Length && _paintedCells[index];
        public int ChemicalCellCount { get; private set; }
        public int RequiredCoats(int index) => index >= 0 && index < _requiredCoats.Length ? _requiredCoats[index] : 0;
        public int CoatCount(int index) => index >= 0 && index < _coatCounts.Length ? _coatCounts[index] : 0;
        public int RemainingChemicalOverlayCount { get; private set; }
        public int UnfinishedChemicalCellCount { get; private set; }
        public int LastOverlayRemovedCount { get; private set; }
        public int LastChemicalBaseRemovedCount { get; private set; }
        public int LastSameStrokeBaseRemovedCount { get; private set; }

        public int ApplyStamp(float centerU, float centerV, float radiusRatio, Phase2PaintConfig config)
        {
            return ApplyStamp(centerU, centerV, radiusRatio, config, NextImplicitStrokeId());
        }

        public int ApplyStamp(float centerU, float centerV, float radiusRatio, Phase2PaintConfig config, int strokeId)
        {
            if (strokeId == 0) strokeId = NextImplicitStrokeId();
            int newlyPainted = 0;
            LastOverlayRemovedCount = 0;
            LastChemicalBaseRemovedCount = 0;
            LastSameStrokeBaseRemovedCount = 0;
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

                    if (_requiredCoats[index] == 2)
                    {
                        if (_coatCounts[index] == 0)
                        {
                            _coatCounts[index] = 1;
                            _overlayRemovedStrokeIds[index] = strokeId;
                            RemainingChemicalOverlayCount--;
                            LastOverlayRemovedCount++;
                            continue;
                        }
                        if (_overlayRemovedStrokeIds[index] == strokeId)
                        {
                            continue;
                        }
                        _coatCounts[index] = 2;
                        LastChemicalBaseRemovedCount++;
                        UnfinishedChemicalCellCount--;
                    }
                    else
                    {
                        _coatCounts[index] = 1;
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
                _coatCounts[i] = 0;
                _overlayRemovedStrokeIds[i] = 0;
            }
            PaintedCellCount = 0;
            RemainingChemicalOverlayCount = ChemicalCellCount;
            UnfinishedChemicalCellCount = ChemicalCellCount;
            LastOverlayRemovedCount = 0;
            LastChemicalBaseRemovedCount = 0;
            LastSameStrokeBaseRemovedCount = 0;
        }

        private void ConfigureChemicalCells(int seed, Phase2PaintConfig config)
        {
            int count = Math.Max(1, (int)Math.Ceiling(_requiredCoats.Length * HATAGONG.GameFlow.RequestEffectRuntime.ChemicalCellPercent / 100d));
            var frontier = new Queue<int>(count * 2);
            var queued = new bool[_requiredCoats.Length];
            const int patchCount = 8;
            for (int patch = 0; patch < patchCount; patch++)
            {
                int start = (int)(ChemicalOrderKey(seed + patch * 104729, patch) % (uint)_requiredCoats.Length);
                while (queued[start]) start = (start + 1) % _requiredCoats.Length;
                queued[start] = true;
                frontier.Enqueue(start);
            }

            int selected = 0;
            while (selected < count && frontier.Count > 0)
            {
                int index = frontier.Dequeue();
                if (_requiredCoats[index] == 2) continue;
                _requiredCoats[index] = 2;
                selected++;
                int x = index % config.Width;
                int y = index / config.Width;
                int rotation = (int)(ChemicalOrderKey(seed, index) & 3u);
                for (int direction = 0; direction < 4; direction++)
                {
                    int rotated = (direction + rotation) & 3;
                    int nx = x + (rotated == 0 ? 1 : rotated == 1 ? -1 : 0);
                    int ny = y + (rotated == 2 ? 1 : rotated == 3 ? -1 : 0);
                    if (nx < 0 || nx >= config.Width || ny < 0 || ny >= config.Height) continue;
                    int neighbor = ny * config.Width + nx;
                    if (queued[neighbor]) continue;
                    queued[neighbor] = true;
                    frontier.Enqueue(neighbor);
                }
            }
            ChemicalCellCount = count;
            RemainingChemicalOverlayCount = count;
            UnfinishedChemicalCellCount = count;
        }

        private int NextImplicitStrokeId()
        {
            _implicitStrokeId = _implicitStrokeId == int.MaxValue ? 1 : _implicitStrokeId + 1;
            return _implicitStrokeId;
        }

        private static uint ChemicalOrderKey(int seed, int index)
        {
            unchecked
            {
                uint value = (uint)(seed ^ (index * 747796405));
                value = (value ^ (value >> 16)) * 2246822519u;
                value = (value ^ (value >> 13)) * 3266489917u;
                return value ^ (value >> 16);
            }
        }

        private static int ClampIndex(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
