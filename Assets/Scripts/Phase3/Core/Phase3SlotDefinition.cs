using System;

namespace HATAGONG.Phase3
{
    public sealed class Phase3SlotDefinition
    {
        public Phase3SlotDefinition(
            string slotId,
            Phase3ShapeDefinition shapeDefinition,
            Phase3Point2D correctCentroid,
            Phase3RotationStep correctBaseRotationStep)
        {
            if (string.IsNullOrWhiteSpace(slotId))
            {
                throw new ArgumentException("Slot ID cannot be null or whitespace.", nameof(slotId));
            }

            if (!correctCentroid.IsFinite)
            {
                throw new ArgumentOutOfRangeException(nameof(correctCentroid), "Slot centroid must be finite.");
            }

            SlotId = slotId;
            ShapeDefinition = shapeDefinition ?? throw new ArgumentNullException(nameof(shapeDefinition));
            CorrectCentroid = correctCentroid;
            CorrectBaseRotationStep = correctBaseRotationStep;
        }

        public string SlotId { get; }
        public Phase3ShapeDefinition ShapeDefinition { get; }
        public Phase3Point2D CorrectCentroid { get; }
        public Phase3RotationStep CorrectBaseRotationStep { get; }
    }
}
