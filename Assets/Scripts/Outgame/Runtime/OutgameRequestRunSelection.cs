using System;
using System.Collections.Generic;
using System.Linq;
using HATAGONG.GameFlow;

namespace HATAGONG.Outgame
{
    public sealed class OutgameRequestRunSelection
    {
        internal OutgameRequestRunSelection(OutgameRequestOffer offer)
        {
            if (offer == null) throw new ArgumentNullException(nameof(offer));

            OfferSnapshot = offer;
            OutgameRequestDefinition definition = offer.Definition;
            RequestId = definition.RequestId;
            Difficulty = definition.Difficulty;
            RequestType = definition.RequestType;
            PermanentSeed = definition.PermanentSeed;
            Phase1Seed = definition.Phase1Seed;
            Phase3Seed = definition.Phase3Seed;
            Phase3ImageKey = definition.Phase3ImageKey;
            EffectIds = Array.AsReadOnly(definition.Effects.Select(effect => effect.EffectId).ToArray());
        }

        public OutgameRequestOffer OfferSnapshot { get; }
        public string RequestId { get; }
        public GameDifficulty Difficulty { get; }
        public RequestType RequestType { get; }
        public int PermanentSeed { get; }
        public int Phase1Seed { get; }
        public int Phase3Seed { get; }
        public string Phase3ImageKey { get; }
        public IReadOnlyList<string> EffectIds { get; }
    }
}
