using System;
using System.Collections.Generic;

namespace HATAGONG.Phase3
{
    public sealed class Phase3ShapeDefinition
    {
        public Phase3ShapeDefinition(
            string shapeId,
            IEnumerable<Phase3GridPoint> vertices,
            int rotationalSymmetryPeriodSteps)
        {
            if (string.IsNullOrWhiteSpace(shapeId))
            {
                throw new ArgumentException("Shape ID cannot be null or whitespace.", nameof(shapeId));
            }

            if (vertices == null)
            {
                throw new ArgumentNullException(nameof(vertices));
            }

            Phase3RotationStep.ValidateSymmetryPeriod(rotationalSymmetryPeriodSteps);
            var copiedVertices = new List<Phase3GridPoint>(vertices);
            IReadOnlyList<Phase3GridPoint> canonical = Phase3Geometry.CanonicalizeVertices(copiedVertices);
            if (!Phase3Geometry.TryGetCentroid(canonical, out Phase3Point2D centroid))
            {
                throw new ArgumentException("Shape centroid cannot be calculated.", nameof(vertices));
            }

            var immutableVertices = new Phase3GridPoint[canonical.Count];
            for (int i = 0; i < canonical.Count; i++)
            {
                immutableVertices[i] = canonical[i];
            }

            ShapeId = shapeId;
            Vertices = Array.AsReadOnly(immutableVertices);
            RotationalSymmetryPeriodSteps = rotationalSymmetryPeriodSteps;
            Area = Phase3Geometry.AbsoluteArea(Vertices);
            Centroid = centroid;
            Bounds = Phase3Geometry.GetBounds(Vertices);
            AspectRatio = Bounds.AspectRatio;
        }

        public string ShapeId { get; }
        public IReadOnlyList<Phase3GridPoint> Vertices { get; }
        public int RotationalSymmetryPeriodSteps { get; }
        public double Area { get; }
        public Phase3Point2D Centroid { get; }
        public Phase3Bounds2D Bounds { get; }
        public double AspectRatio { get; }

        public bool IsRotationEquivalent(Phase3RotationStep actual, Phase3RotationStep required)
        {
            return actual.IsEquivalentTo(required, RotationalSymmetryPeriodSteps);
        }
    }
}
