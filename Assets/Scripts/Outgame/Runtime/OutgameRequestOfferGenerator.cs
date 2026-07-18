using System;
using System.Collections.Generic;

namespace HATAGONG.Outgame
{
    public static class OutgameRequestOfferGenerator
    {
        public const int MaximumOfferCount = 3;

        private const uint ShuffleDomain = 0xA511E9B3u;
        private const uint DuplicateDomain = 0x63D83595u;

        public static OutgameRequestOfferGenerationResult Generate(
            OutgameRequestCatalog catalog,
            int batchSeed)
        {
            var errors = new List<string>();
            ValidateCatalog(catalog, errors);
            if (errors.Count > 0)
                return OutgameRequestOfferGenerationResult.Failed(errors);

            var candidates = new List<OutgameRequestDefinition>(catalog.EnabledRequests);
            var random = new DeterministicRandom(unchecked((uint)batchSeed) ^ ShuffleDomain);
            FisherYatesShuffle(candidates, ref random);

            var selected = new List<OutgameRequestDefinition>(MaximumOfferCount);
            if (candidates.Count >= MaximumOfferCount)
            {
                for (int i = 0; i < MaximumOfferCount; i++) selected.Add(candidates[i]);
            }
            else if (candidates.Count == 2)
            {
                int duplicateIndex = unchecked((int)(
                    Mix32(unchecked((uint)batchSeed) ^ DuplicateDomain) % 2u));
                selected.Add(candidates[0]);
                selected.Add(candidates[1]);
                selected.Add(catalog.EnabledRequests[duplicateIndex]);
                FisherYatesShuffle(selected, ref random);
            }
            else
            {
                selected.Add(candidates[0]);
                selected.Add(candidates[0]);
                selected.Add(candidates[0]);
            }

            var offers = new OutgameRequestOffer[MaximumOfferCount];
            for (int i = 0; i < offers.Length; i++)
                offers[i] = CreateOffer(selected[i]);

            return OutgameRequestOfferGenerationResult.Succeeded(
                new OutgameRequestOfferBatch(batchSeed, offers));
        }

        private static void ValidateCatalog(
            OutgameRequestCatalog catalog,
            ICollection<string> errors)
        {
            if (catalog == null)
            {
                errors.Add("Catalog is required.");
                return;
            }

            IReadOnlyList<OutgameRequestDefinition> requests = catalog.Requests;
            IReadOnlyList<OutgameRequestDefinition> enabled = catalog.EnabledRequests;
            if (requests == null)
            {
                errors.Add("Catalog request collection is required.");
                return;
            }
            if (enabled == null)
            {
                errors.Add("Catalog enabled request collection is required.");
                return;
            }

            var requestIds = new HashSet<string>(StringComparer.Ordinal);
            var permanentSeeds = new HashSet<int>();
            var seedTuples = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < requests.Count; i++)
            {
                OutgameRequestDefinition request = requests[i];
                if (request == null)
                {
                    errors.Add($"Catalog request at index {i} is null.");
                    continue;
                }
                if (!requestIds.Add(request.RequestId))
                    errors.Add($"Duplicate RequestId '{request.RequestId}'.");
                if (request.PermanentSeed <= 0)
                    errors.Add($"Request '{request.RequestId}' has a non-positive PermanentSeed.");
                else if (!permanentSeeds.Add(request.PermanentSeed))
                    errors.Add($"Duplicate PermanentSeed '{request.PermanentSeed}'.");
                if (request.Phase1Seed <= 0)
                    errors.Add($"Request '{request.RequestId}' has a non-positive Phase1Seed.");
                if (request.Phase3Seed <= 0)
                    errors.Add($"Request '{request.RequestId}' has a non-positive Phase3Seed.");
                if (request.Phase3ImageKey != "Img_bigtiles1" && request.Phase3ImageKey != "Img_bigtiles2" && request.Phase3ImageKey != "Img_bigtiles3")
                    errors.Add($"Request '{request.RequestId}' has an unsupported Phase3ImageKey '{request.Phase3ImageKey}'.");
                string seedTuple = request.PermanentSeed + ":" + request.Phase1Seed + ":" + request.Phase3Seed;
                if (!seedTuples.Add(seedTuple))
                    errors.Add($"Duplicate request seed tuple '{seedTuple}'.");
            }

            if (enabled.Count == 0)
                errors.Add("Catalog has no enabled requests.");
            for (int i = 0; i < enabled.Count; i++)
            {
                if (enabled[i] == null)
                    errors.Add($"Enabled request at index {i} is null.");
            }
        }

        private static OutgameRequestOffer CreateOffer(OutgameRequestDefinition definition)
        {
            return new OutgameRequestOffer(definition);
        }

        private static uint Mix32(uint value)
        {
            unchecked
            {
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                value *= 0x846CA68Bu;
                value ^= value >> 16;
                return value;
            }
        }

        // Fisher-Yates driven by a fixed xorshift32 stream; rejection sampling removes bounded modulo bias.
        private static void FisherYatesShuffle<T>(IList<T> values, ref DeterministicRandom random)
        {
            for (int i = values.Count - 1; i > 0; i--)
            {
                int selected = random.NextBounded(i + 1);
                T temporary = values[i];
                values[i] = values[selected];
                values[selected] = temporary;
            }
        }

        private struct DeterministicRandom
        {
            private uint state;

            public DeterministicRandom(uint seed)
            {
                state = seed == 0u ? 0x6D2B79F5u : seed;
            }

            public int NextBounded(int exclusiveUpperBound)
            {
                uint bound = unchecked((uint)exclusiveUpperBound);
                uint threshold = unchecked(0u - bound) % bound;
                uint value;
                do { value = NextUInt32(); }
                while (value < threshold);
                return unchecked((int)(value % bound));
            }

            private uint NextUInt32()
            {
                unchecked
                {
                    uint value = state;
                    value ^= value << 13;
                    value ^= value >> 17;
                    value ^= value << 5;
                    state = value;
                    return value;
                }
            }
        }
    }
}
