using System;

namespace HATAGONG.Outgame
{
    [Serializable]
    public sealed class PlayerProgressData
    {
        public const int CurrentVersion = 2;

        public int Version = CurrentVersion;
        public int ClearedStageCount;
        public bool ItemInventoryInitialized;
        public int Stopwatch;
        public int Hammer;
        public int TileGrinder;
        public int TileCutter;
        public int CementBasket;
        public int Trowel;
        public int Scraper;

        public PlayerProgressData()
        {
        }

        public PlayerProgressData(int clearedStageCount)
        {
            Version = CurrentVersion;
            ClearedStageCount = Math.Max(0, clearedStageCount);
            ItemInventoryInitialized = true;
            Stopwatch = Hammer = TileGrinder = TileCutter = CementBasket = Trowel = Scraper = 5;
        }
    }
}
