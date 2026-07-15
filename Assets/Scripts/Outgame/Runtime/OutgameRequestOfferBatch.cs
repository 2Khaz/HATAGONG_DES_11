using System;
using System.Collections.Generic;

namespace HATAGONG.Outgame
{
    public sealed class OutgameRequestOfferBatch
    {
        private readonly IReadOnlyList<OutgameRequestOffer> offers;

        internal OutgameRequestOfferBatch(int batchSeed, IEnumerable<OutgameRequestOffer> offers)
        {
            BatchSeed = batchSeed;
            var copy = offers == null
                ? Array.Empty<OutgameRequestOffer>()
                : new List<OutgameRequestOffer>(offers).ToArray();
            this.offers = Array.AsReadOnly(copy);
        }

        public int BatchSeed { get; }
        public IReadOnlyList<OutgameRequestOffer> Offers => offers;
    }
}
