using System;
using System.Collections.Generic;

namespace HATAGONG.GameFlow
{
    public readonly struct GameRunContext
    {
        public GameRunContext(GameDifficulty difficulty) : this(difficulty, RequestType.Normal) { }

        public GameRunContext(GameDifficulty difficulty, RequestType requestType)
        {
            HasSelectedRequest = false;
            RequestId = string.Empty;
            Difficulty = difficulty;
            RequestType = requestType;
            PermanentSeed = Phase1Seed = Phase3Seed = 0;
            Phase3ImageKey = string.Empty;
            EffectIds = Array.Empty<string>();
        }

        public GameRunContext(string requestId, GameDifficulty difficulty, RequestType requestType,
            int permanentSeed, int phase1Seed, int phase3Seed, string phase3ImageKey,
            IEnumerable<string> effectIds = null)
        {
            HasSelectedRequest = true;
            RequestId = requestId ?? string.Empty;
            Difficulty = difficulty;
            RequestType = requestType;
            PermanentSeed = permanentSeed;
            Phase1Seed = phase1Seed;
            Phase3Seed = phase3Seed;
            Phase3ImageKey = phase3ImageKey ?? string.Empty;
            EffectIds = NormalizeEffects(effectIds);
        }

        public bool HasSelectedRequest { get; }
        public string RequestId { get; }
        public GameDifficulty Difficulty { get; }
        public RequestType RequestType { get; }
        public int PermanentSeed { get; }
        public int Phase1Seed { get; }
        public int Phase3Seed { get; }
        public string Phase3ImageKey { get; }
        public IReadOnlyList<string> EffectIds { get; }

        public bool HasEffect(string effectId)
        {
            if (string.IsNullOrEmpty(effectId) || EffectIds == null) return false;
            for (int i = 0; i < EffectIds.Count; i++)
                if (string.Equals(EffectIds[i], effectId, StringComparison.Ordinal)) return true;
            return false;
        }

        public bool HasSameEffects(IReadOnlyList<string> other)
        {
            int count = EffectIds?.Count ?? 0;
            if ((other?.Count ?? 0) != count) return false;
            for (int i = 0; i < count; i++)
                if (!string.Equals(EffectIds[i], other[i], StringComparison.Ordinal)) return false;
            return true;
        }

        public bool IsValid =>
            (Difficulty == GameDifficulty.Easy || Difficulty == GameDifficulty.Normal || Difficulty == GameDifficulty.Hard) &&
            (RequestType == RequestType.Normal || RequestType == RequestType.Sudden) &&
            (!HasSelectedRequest || (!string.IsNullOrWhiteSpace(RequestId) && PermanentSeed > 0 &&
                Phase1Seed > 0 && Phase3Seed > 0 && !string.IsNullOrWhiteSpace(Phase3ImageKey)));

        private static IReadOnlyList<string> NormalizeEffects(IEnumerable<string> effectIds)
        {
            if (effectIds == null) return Array.Empty<string>();
            var result = new List<string>(3);
            foreach (string effectId in effectIds)
            {
                if (string.IsNullOrWhiteSpace(effectId) || result.Contains(effectId)) continue;
                if (result.Count == 3) throw new ArgumentException("A request can contain at most three effects.", nameof(effectIds));
                result.Add(effectId);
            }
            return result.AsReadOnly();
        }
    }
}
