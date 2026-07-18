using System;

namespace HATAGONG.Outgame
{
    public sealed class OutgameRequestOffer
    {
        internal OutgameRequestOffer(OutgameRequestDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }

        public OutgameRequestDefinition Definition { get; }
        public int Phase1Seed => Definition.Phase1Seed;
        public int Phase3Seed => Definition.Phase3Seed;
        public string Phase3ImageKey => Definition.Phase3ImageKey;
    }
}
