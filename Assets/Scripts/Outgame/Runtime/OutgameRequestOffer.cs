using System;

namespace HATAGONG.Outgame
{
    public sealed class OutgameRequestOffer
    {
        internal OutgameRequestOffer(
            OutgameRequestDefinition definition,
            int phase1Seed,
            int phase2Seed,
            int phase3Seed)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Phase1Seed = phase1Seed;
            Phase2Seed = phase2Seed;
            Phase3Seed = phase3Seed;
        }

        public OutgameRequestDefinition Definition { get; }
        public int Phase1Seed { get; }
        public int Phase2Seed { get; }
        public int Phase3Seed { get; }
    }
}
