using System;

namespace HATAGONG.Phase3
{
    public readonly struct Phase3RotationStep : IEquatable<Phase3RotationStep>, IComparable<Phase3RotationStep>
    {
        public Phase3RotationStep(int rawStep)
        {
            Value = Normalize(rawStep);
        }

        public int Value { get; }
        public int Degrees => Value * Phase3CoreConstants.RotationStepDegrees;
        public Phase3RotationStep Clockwise => Add(1);
        public Phase3RotationStep CounterClockwise => Add(-1);

        public Phase3RotationStep Add(int stepDelta) => new Phase3RotationStep(Value + stepDelta);

        public int NormalizedDeltaTo(Phase3RotationStep other)
        {
            return Normalize(other.Value - Value);
        }

        public bool IsEquivalentTo(Phase3RotationStep required, int symmetryPeriodSteps)
        {
            ValidateSymmetryPeriod(symmetryPeriodSteps);
            return required.NormalizedDeltaTo(this) % symmetryPeriodSteps == 0;
        }

        public static bool IsValidSymmetryPeriod(int periodSteps)
        {
            return periodSteps == 1 || periodSteps == 2 || periodSteps == 4 || periodSteps == 8;
        }

        public static void ValidateSymmetryPeriod(int periodSteps)
        {
            if (!IsValidSymmetryPeriod(periodSteps))
            {
                throw new ArgumentOutOfRangeException(nameof(periodSteps), periodSteps, "Symmetry period must be 1, 2, 4, or 8 steps.");
            }
        }

        public int CompareTo(Phase3RotationStep other) => Value.CompareTo(other.Value);
        public bool Equals(Phase3RotationStep other) => Value == other.Value;
        public override bool Equals(object obj) => obj is Phase3RotationStep other && Equals(other);
        public override int GetHashCode() => Value;
        public override string ToString() => $"Step {Value} ({Degrees} deg)";

        public static bool operator ==(Phase3RotationStep left, Phase3RotationStep right) => left.Equals(right);
        public static bool operator !=(Phase3RotationStep left, Phase3RotationStep right) => !left.Equals(right);

        private static int Normalize(int rawStep)
        {
            int normalized = rawStep % Phase3CoreConstants.FullRotationStepCount;
            return normalized < 0 ? normalized + Phase3CoreConstants.FullRotationStepCount : normalized;
        }
    }
}
