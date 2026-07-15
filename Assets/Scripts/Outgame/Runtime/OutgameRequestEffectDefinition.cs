using System;

namespace HATAGONG.Outgame
{
    public sealed class OutgameRequestEffectDefinition
    {
        public OutgameRequestEffectDefinition(
            string effectId,
            bool enabled,
            string effectName,
            string effectIconKey,
            string description)
        {
            EffectId = effectId ?? throw new ArgumentNullException(nameof(effectId));
            Enabled = enabled;
            EffectName = effectName ?? throw new ArgumentNullException(nameof(effectName));
            EffectIconKey = effectIconKey ?? throw new ArgumentNullException(nameof(effectIconKey));
            Description = description ?? throw new ArgumentNullException(nameof(description));
        }

        public string EffectId { get; }
        public bool Enabled { get; }
        public string EffectName { get; }
        public string EffectIconKey { get; }
        public string Description { get; }
    }
}
