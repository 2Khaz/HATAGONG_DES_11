using System;

namespace HATAGONG.Phase2
{
    public static class Phase2PaintGeometry
    {
        public static bool IsCircleIntersectingUnitBoard(float centerU, float centerV, float radiusRatio)
        {
            if (!IsFinite(centerU) || !IsFinite(centerV) || !IsFinite(radiusRatio) || radiusRatio <= 0f)
            {
                return false;
            }

            double u = centerU;
            double v = centerV;
            double nearestU = Math.Max(0d, Math.Min(1d, u));
            double nearestV = Math.Max(0d, Math.Min(1d, v));
            double deltaU = u - nearestU;
            double deltaV = v - nearestV;
            double radius = radiusRatio;
            return deltaU * deltaU + deltaV * deltaV < radius * radius;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
