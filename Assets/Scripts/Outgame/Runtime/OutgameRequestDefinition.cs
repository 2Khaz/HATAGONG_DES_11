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
        public string RequesterName { get; }
        public string PortraitKey { get; }
        public string Title { get; }
        public string Description { get; }
        public IReadOnlyList<OutgameRequestEffectDefinition> Effects => effects;
    }
}
