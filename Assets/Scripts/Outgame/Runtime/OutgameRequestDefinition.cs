using System;
using System.Collections.Generic;
using HATAGONG.GameFlow;

namespace HATAGONG.Outgame
{
    public sealed class OutgameRequestDefinition
    {
        private readonly IReadOnlyList<OutgameRequestEffectDefinition> effects;

        public OutgameRequestDefinition(
            string requestId,
            bool enabled,
            RequestType requestType,
            GameDifficulty difficulty,
            int permanentSeed,
            int phase1Seed,
            int phase3Seed,
            string phase3ImageKey,
            string requesterName,
            string portraitKey,
            string title,
            string description,
            IEnumerable<OutgameRequestEffectDefinition> effects)
        {
            RequestId = requestId ?? throw new ArgumentNullException(nameof(requestId));
            Enabled = enabled;
            RequestType = requestType;
            Difficulty = difficulty;
            PermanentSeed = permanentSeed;
            Phase1Seed = phase1Seed;
            Phase3Seed = phase3Seed;
            Phase3ImageKey = phase3ImageKey ?? throw new ArgumentNullException(nameof(phase3ImageKey));
            RequesterName = requesterName ?? throw new ArgumentNullException(nameof(requesterName));
            PortraitKey = portraitKey ?? throw new ArgumentNullException(nameof(portraitKey));
            Title = title ?? throw new ArgumentNullException(nameof(title));
            Description = description ?? throw new ArgumentNullException(nameof(description));
            var copy = effects == null
                ? Array.Empty<OutgameRequestEffectDefinition>()
                : new List<OutgameRequestEffectDefinition>(effects).ToArray();
            this.effects = Array.AsReadOnly(copy);
        }

        public string RequestId { get; }
        public bool Enabled { get; }
        public RequestType RequestType { get; }
        public GameDifficulty Difficulty { get; }
        public int PermanentSeed { get; }
        public int Phase1Seed { get; }
        public int Phase3Seed { get; }
        public string Phase3ImageKey { get; }
        public string RequesterName { get; }
        public string PortraitKey { get; }
        public string Title { get; }
        public string Description { get; }
        public IReadOnlyList<OutgameRequestEffectDefinition> Effects => effects;
    }
}
