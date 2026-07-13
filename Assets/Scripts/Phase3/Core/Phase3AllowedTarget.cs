using System;

namespace HATAGONG.Phase3
{
    public readonly struct Phase3AllowedTarget : IEquatable<Phase3AllowedTarget>, IComparable<Phase3AllowedTarget>
    {
        public Phase3AllowedTarget(string slotId, Phase3RotationStep requiredRotationStep)
            : this(slotId, requiredRotationStep, new Phase3RotationStep(0))
        {
        }

        public Phase3AllowedTarget(
            string slotId,
            Phase3RotationStep requiredRotationStep,
            Phase3RotationStep rotationCorrectionStep)
        {
            if (string.IsNullOrWhiteSpace(slotId))
            {
                throw new ArgumentException("Slot ID cannot be null or whitespace.", nameof(slotId));
            }

            SlotId = slotId;
            RequiredRotationStep = requiredRotationStep;
            RotationCorrectionStep = rotationCorrectionStep;
        }

        public string SlotId { get; }
        public Phase3RotationStep RequiredRotationStep { get; }
        public Phase3RotationStep RotationCorrectionStep { get; }

        public int CompareTo(Phase3AllowedTarget other) => string.CompareOrdinal(SlotId, other.SlotId);

        public bool Equals(Phase3AllowedTarget other) =>
            string.Equals(SlotId, other.SlotId, StringComparison.Ordinal) &&
            RequiredRotationStep == other.RequiredRotationStep &&
            RotationCorrectionStep == other.RotationCorrectionStep;

        public override bool Equals(object obj) => obj is Phase3AllowedTarget other && Equals(other);
        public override int GetHashCode() => unchecked((((SlotId != null ? SlotId.GetHashCode() : 0) * 397) ^ RequiredRotationStep.GetHashCode()) * 397 ^ RotationCorrectionStep.GetHashCode());
        public override string ToString() => $"{SlotId}: required={RequiredRotationStep}, correction={RotationCorrectionStep}";
    }
}
