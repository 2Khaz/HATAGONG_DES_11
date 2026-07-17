using System;
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
            Phase1Seed = offer.Phase1Seed;
            Phase2Seed = offer.Phase2Seed;
            Phase3Seed = offer.Phase3Seed;
        }

        public OutgameRequestOffer OfferSnapshot { get; }
        public string RequestId { get; }
        public GameDifficulty Difficulty { get; }
        public RequestType RequestType { get; }
        public int PermanentSeed { get; }
        public int Phase1Seed { get; }
        public int Phase2Seed { get; }
        public int Phase3Seed { get; }
    }
}
