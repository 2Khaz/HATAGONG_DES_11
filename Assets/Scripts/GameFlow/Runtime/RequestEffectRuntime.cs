using System;
using System.Collections.Generic;

namespace HATAGONG.GameFlow
{
    public static class RequestEffectRuntime
    {
        public const string Hard = "EFFECT_HARD";
        public const string Slippery = "EFFECT_SLIPPERY";
        public const string NoItem = "EFFECT_NOITEM";
        public const string Fatigue = "EFFECT_FATIGUE";
        public const string Super = "EFFECT_SUPER";
        public const string TimeDown = "EFFECT_TIMEDOWN";
        public const string Chemical = "EFFECT_CHEMICAL";

        public const int HardHpBonus = 2;
        public const int SuperTileHpBonus = 10;
        public const float FatigueHitIntervalSeconds = 0.18f;
        public const float DefaultSnapRadiusRatio = 0.20f;
        public const float SlipperySnapRadiusRatio = 0.12f;
        public const double TimeDownDurationSeconds = 80d;
        public const int ChemicalCellPercent = 25;

        private static readonly IReadOnlyDictionary<string, (string iconKey, string scriptKey)> Contracts =
            new Dictionary<string, (string, string)>(StringComparer.Ordinal)
            {
                { Hard, ("Img_icon_quest1", "effect_hard") },
                { Slippery, ("Img_icon_quest2", "effect_slippery") },
                { NoItem, ("Img_icon_quest3", "effect_noitem") },
                { Fatigue, ("Img_icon_quest4", "effect_fatigue") },
                { Super, ("Img_icon_quest5", "effect_super") },
                { TimeDown, ("Img_icon_quest6", "effect_timedown") },
                { Chemical, ("Img_icon_quest7", "effect_chemical") }
            };

        public static readonly IReadOnlyList<string> SupportedEffectIds = Array.AsReadOnly(new[]
        {
            Hard, Slippery, NoItem, Fatigue, Super, TimeDown, Chemical
        });

        public static int SupportedEffectCount => Contracts.Count;

        public static int BalancePhase1Hp(int unbalancedHp)
        {
            if (unbalancedHp < 30) return Math.Max(1, unbalancedHp);
            int scaled = (int)Math.Round(unbalancedHp * (43d / 47d), MidpointRounding.AwayFromZero);
            if (unbalancedHp >= 42 && unbalancedHp < 47) scaled = Math.Max(scaled, unbalancedHp - 3);
            return Math.Max(1, Math.Min(43, scaled));
        }

        public static int CalculatePhase1Hp(int baseHp, int gradeModifier, bool hard, bool super)
        {
            return BalancePhase1Hp(baseHp + gradeModifier) + (hard ? HardHpBonus : 0) + (super ? SuperTileHpBonus : 0);
        }

        public static int SelectSuperTileId(int permanentSeed, IReadOnlyList<int> tileIds)
        {
            if (permanentSeed <= 0 || tileIds == null || tileIds.Count == 0) return -1;
            int selectedId = tileIds[0];
            uint selectedKey = SuperOrderKey(permanentSeed, selectedId);
            for (int i = 1; i < tileIds.Count; i++)
            {
                int candidateId = tileIds[i];
                uint candidateKey = SuperOrderKey(permanentSeed, candidateId);
                if (candidateKey < selectedKey || (candidateKey == selectedKey && candidateId < selectedId))
                {
                    selectedId = candidateId;
                    selectedKey = candidateKey;
                }
            }
            return selectedId;
        }

        public static int SelectLargestAreaSuperTileId(int permanentSeed, IReadOnlyList<int> tileIds, IReadOnlyList<int> tileAreas)
        {
            if (tileIds == null || tileAreas == null || tileIds.Count == 0 || tileIds.Count != tileAreas.Count) return -1;
            int largestArea = tileAreas[0];
            for (int i = 1; i < tileAreas.Count; i++) largestArea = Math.Max(largestArea, tileAreas[i]);
            var candidates = new List<int>();
            for (int i = 0; i < tileIds.Count; i++)
                if (tileAreas[i] == largestArea) candidates.Add(tileIds[i]);
            return SelectSuperTileId(permanentSeed, candidates);
        }

        public static float GetPhase1HitInterval(bool fatigue) => fatigue ? FatigueHitIntervalSeconds : 0f;
        public static float GetPhase3SnapRadiusRatio(bool slippery) => slippery ? SlipperySnapRadiusRatio : DefaultSnapRadiusRatio;
        public static double GetGameDuration(bool timeDown, double normalDuration) => timeDown ? TimeDownDurationSeconds : normalDuration;

        public static bool TryGetContract(string effectId, out string iconKey, out string scriptKey)
        {
            if (effectId != null && Contracts.TryGetValue(effectId, out var contract))
            {
                iconKey = contract.iconKey;
                scriptKey = contract.scriptKey;
                return true;
            }
            iconKey = string.Empty;
            scriptKey = string.Empty;
            return false;
        }

        private static uint SuperOrderKey(int permanentSeed, int tileId)
        {
            unchecked
            {
                return (uint)((permanentSeed * 397) ^ ((tileId + 1) * 1103515245) ^ 0x53555045);
            }
        }
    }
}
