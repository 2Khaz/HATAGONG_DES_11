using System;
using System.Collections.Generic;

namespace HATAGONG.Phase3
{
    public readonly struct Phase3Bounds2D : IEquatable<Phase3Bounds2D>
    {
        public Phase3Bounds2D(double minX, double minY, double maxX, double maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        public double MinX { get; }
        public double MinY { get; }
        public double MaxX { get; }
        public double MaxY { get; }
        public double Width => MaxX - MinX;
        public double Height => MaxY - MinY;

        public double AspectRatio
        {
            get
            {
                double shorter = Math.Min(Width, Height);
                return shorter <= 0d ? double.PositiveInfinity : Math.Max(Width, Height) / shorter;
            }
        }

        public bool Equals(Phase3Bounds2D other) =>
            MinX.Equals(other.MinX) && MinY.Equals(other.MinY) &&
            MaxX.Equals(other.MaxX) && MaxY.Equals(other.MaxY);

        public override bool Equals(object obj) => obj is Phase3Bounds2D other && Equals(other);
        public override int GetHashCode() => unchecked((((MinX.GetHashCode() * 397) ^ MinY.GetHashCode()) * 397 ^ MaxX.GetHashCode()) * 397 ^ MaxY.GetHashCode());
        public override string ToString() => $"[{MinX:R}, {MinY:R}]..[{MaxX:R}, {MaxY:R}]";
    }

    public enum Phase3PolygonFailure
    {
        None = 0,
        NullVertices,
        TooFewVertices,
        TooManyVertices,
        PointOutsideGrid,
        DuplicateVertex,
        ZeroLengthEdge,
        RedundantCollinearVertex,
        UnsupportedEdgeDirection,
        ZeroArea,
        SelfIntersection,
        Concave
    }

    public readonly struct Phase3PolygonValidationResult
    {
        private Phase3PolygonValidationResult(bool isValid, Phase3PolygonFailure failure, string message)
        {
            IsValid = isValid;
            Failure = failure;
            Message = message ?? string.Empty;
        }

        public bool IsValid { get; }
        public Phase3PolygonFailure Failure { get; }
        public string Message { get; }

        public static Phase3PolygonValidationResult Valid() =>
            new Phase3PolygonValidationResult(true, Phase3PolygonFailure.None, string.Empty);

        public static Phase3PolygonValidationResult Invalid(Phase3PolygonFailure failure, string message) =>
            new Phase3PolygonValidationResult(false, failure, message);
    }

    public static class Phase3Geometry
    {
        public const int MinimumVertexCount = 3;
        public const int MaximumVertexCount = 5;

        public static long SignedDoubleArea(IReadOnlyList<Phase3GridPoint> vertices)
        {
            RequireVertices(vertices);
            long area = 0L;
            for (int i = 0; i < vertices.Count; i++)
            {
                Phase3GridPoint current = vertices[i];
                Phase3GridPoint next = vertices[(i + 1) % vertices.Count];
                area += (long)current.X * next.Y - (long)next.X * current.Y;
            }

            return area;
        }

        public static double AbsoluteArea(IReadOnlyList<Phase3GridPoint> vertices)
        {
            return Math.Abs((double)SignedDoubleArea(vertices)) * 0.5d;
        }

        public static bool TryGetCentroid(IReadOnlyList<Phase3GridPoint> vertices, out Phase3Point2D centroid)
        {
            centroid = default;
            if (vertices == null || vertices.Count < MinimumVertexCount)
            {
                return false;
            }

            long signedDoubleArea = SignedDoubleArea(vertices);
            if (signedDoubleArea == 0L)
            {
                return false;
            }

            double xNumerator = 0d;
            double yNumerator = 0d;
            for (int i = 0; i < vertices.Count; i++)
            {
                Phase3GridPoint current = vertices[i];
                Phase3GridPoint next = vertices[(i + 1) % vertices.Count];
                long cross = (long)current.X * next.Y - (long)next.X * current.Y;
                xNumerator += (current.X + next.X) * (double)cross;
                yNumerator += (current.Y + next.Y) * (double)cross;
            }

            double divisor = 3d * signedDoubleArea;
            centroid = new Phase3Point2D(xNumerator / divisor, yNumerator / divisor);
            return centroid.IsFinite;
        }

        public static Phase3Bounds2D GetBounds(IReadOnlyList<Phase3GridPoint> vertices)
        {
            RequireVertices(vertices);
            if (vertices.Count == 0)
            {
                throw new ArgumentException("At least one vertex is required.", nameof(vertices));
            }

            int minX = vertices[0].X;
            int minY = vertices[0].Y;
            int maxX = minX;
            int maxY = minY;
            for (int i = 1; i < vertices.Count; i++)
            {
                Phase3GridPoint point = vertices[i];
                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
            }

            return new Phase3Bounds2D(minX, minY, maxX, maxY);
        }

        public static double GetBoundsAspectRatio(IReadOnlyList<Phase3GridPoint> vertices) => GetBounds(vertices).AspectRatio;
        public static bool IsClockwise(IReadOnlyList<Phase3GridPoint> vertices) => SignedDoubleArea(vertices) < 0L;
        public static bool IsCounterClockwise(IReadOnlyList<Phase3GridPoint> vertices) => SignedDoubleArea(vertices) > 0L;

        public static IReadOnlyList<Phase3GridPoint> NormalizeCounterClockwise(IReadOnlyList<Phase3GridPoint> vertices)
        {
            RequireVertices(vertices);
            if (SignedDoubleArea(vertices) == 0L)
            {
                throw new ArgumentException("A zero-area polygon has no winding.", nameof(vertices));
            }

            var normalized = new Phase3GridPoint[vertices.Count];
            if (IsCounterClockwise(vertices))
            {
                for (int i = 0; i < vertices.Count; i++)
                {
                    normalized[i] = vertices[i];
                }
            }
            else
            {
                for (int i = 0; i < vertices.Count; i++)
                {
                    normalized[i] = vertices[vertices.Count - 1 - i];
                }
            }

            return Array.AsReadOnly(normalized);
        }

        public static bool HasDuplicateVertex(IReadOnlyList<Phase3GridPoint> vertices)
        {
            if (vertices == null)
            {
                return false;
            }

            for (int i = 0; i < vertices.Count; i++)
            {
                for (int j = i + 1; j < vertices.Count; j++)
                {
                    if (vertices[i] == vertices[j])
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool HasZeroLengthEdge(IReadOnlyList<Phase3GridPoint> vertices)
        {
            if (vertices == null || vertices.Count < 2)
            {
                return false;
            }

            for (int i = 0; i < vertices.Count; i++)
            {
                if (vertices[i] == vertices[(i + 1) % vertices.Count])
                {
                    return true;
                }
            }

            return false;
        }

        public static bool HasRedundantCollinearVertex(IReadOnlyList<Phase3GridPoint> vertices)
        {
            if (vertices == null || vertices.Count < MinimumVertexCount)
            {
                return false;
            }

            for (int i = 0; i < vertices.Count; i++)
            {
                Phase3GridPoint previous = vertices[(i - 1 + vertices.Count) % vertices.Count];
                Phase3GridPoint current = vertices[i];
                Phase3GridPoint next = vertices[(i + 1) % vertices.Count];
                if (Cross(previous, current, next) == 0L)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsAllowedEdgeDirection(Phase3GridPoint from, Phase3GridPoint to)
        {
            int dx = to.X - from.X;
            int dy = to.Y - from.Y;
            if (dx == 0 && dy == 0)
            {
                return false;
            }

            return dx == 0 || dy == 0 || Math.Abs(dx) == Math.Abs(dy);
        }

        public static bool AreAllEdgesAllowed(IReadOnlyList<Phase3GridPoint> vertices)
        {
            if (vertices == null || vertices.Count < 2)
            {
                return false;
            }

            for (int i = 0; i < vertices.Count; i++)
            {
                if (!IsAllowedEdgeDirection(vertices[i], vertices[(i + 1) % vertices.Count]))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool AreAllVerticesWithinLogicalGrid(IReadOnlyList<Phase3GridPoint> vertices)
        {
            if (vertices == null)
            {
                return false;
            }

            for (int i = 0; i < vertices.Count; i++)
            {
                if (!vertices[i].IsWithinLogicalGrid)
                {
                    return false;
                }
            }

            return true;
        }

        public static bool PointOnSegment(Phase3GridPoint point, Phase3GridPoint start, Phase3GridPoint end)
        {
            if (Cross(start, end, point) != 0L)
            {
                return false;
            }

            return point.X >= Math.Min(start.X, end.X) && point.X <= Math.Max(start.X, end.X) &&
                   point.Y >= Math.Min(start.Y, end.Y) && point.Y <= Math.Max(start.Y, end.Y);
        }

        public static bool SegmentsIntersect(
            Phase3GridPoint firstStart,
            Phase3GridPoint firstEnd,
            Phase3GridPoint secondStart,
            Phase3GridPoint secondEnd)
        {
            long d1 = Cross(firstStart, firstEnd, secondStart);
            long d2 = Cross(firstStart, firstEnd, secondEnd);
            long d3 = Cross(secondStart, secondEnd, firstStart);
            long d4 = Cross(secondStart, secondEnd, firstEnd);

            if (((d1 > 0L && d2 < 0L) || (d1 < 0L && d2 > 0L)) &&
                ((d3 > 0L && d4 < 0L) || (d3 < 0L && d4 > 0L)))
            {
                return true;
            }

            return (d1 == 0L && PointOnSegment(secondStart, firstStart, firstEnd)) ||
                   (d2 == 0L && PointOnSegment(secondEnd, firstStart, firstEnd)) ||
                   (d3 == 0L && PointOnSegment(firstStart, secondStart, secondEnd)) ||
                   (d4 == 0L && PointOnSegment(firstEnd, secondStart, secondEnd));
        }

        public static bool HasSelfIntersection(IReadOnlyList<Phase3GridPoint> vertices)
        {
            if (vertices == null || vertices.Count < 4)
            {
                return false;
            }

            int count = vertices.Count;
            for (int first = 0; first < count; first++)
            {
                int firstNext = (first + 1) % count;
                for (int second = first + 1; second < count; second++)
                {
                    int secondNext = (second + 1) % count;
                    bool adjacent = first == second || firstNext == second || secondNext == first;
                    if (adjacent)
                    {
                        continue;
                    }

                    if (SegmentsIntersect(vertices[first], vertices[firstNext], vertices[second], vertices[secondNext]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool IsConvex(IReadOnlyList<Phase3GridPoint> vertices)
        {
            if (vertices == null || vertices.Count < MinimumVertexCount)
            {
                return false;
            }

            long expectedSign = 0L;
            for (int i = 0; i < vertices.Count; i++)
            {
                long cross = Cross(vertices[i], vertices[(i + 1) % vertices.Count], vertices[(i + 2) % vertices.Count]);
                if (cross == 0L)
                {
                    return false;
                }

                long sign = cross > 0L ? 1L : -1L;
                if (expectedSign == 0L)
                {
                    expectedSign = sign;
                }
                else if (expectedSign != sign)
                {
                    return false;
                }
            }

            return true;
        }

        public static bool PointInPolygon(Phase3Point2D point, IReadOnlyList<Phase3GridPoint> vertices, bool includeBoundary = true)
        {
            if (!point.IsFinite || vertices == null || vertices.Count < MinimumVertexCount)
            {
                return false;
            }

            bool inside = false;
            for (int i = 0, j = vertices.Count - 1; i < vertices.Count; j = i++)
            {
                Phase3GridPoint current = vertices[i];
                Phase3GridPoint previous = vertices[j];
                if (IsPointOnSegment(point, previous, current))
                {
                    return includeBoundary;
                }

                bool crosses = (current.Y > point.Y) != (previous.Y > point.Y);
                if (crosses)
                {
                    double intersectionX = (previous.X - current.X) * (point.Y - current.Y) /
                                           (previous.Y - current.Y) + current.X;
                    if (point.X < intersectionX)
                    {
                        inside = !inside;
                    }
                }
            }

            return inside;
        }

        public static Phase3PolygonValidationResult ValidatePolygon(IReadOnlyList<Phase3GridPoint> vertices)
        {
            if (vertices == null)
            {
                return Phase3PolygonValidationResult.Invalid(Phase3PolygonFailure.NullVertices, "Vertices cannot be null.");
            }

            if (vertices.Count < MinimumVertexCount)
            {
                return Phase3PolygonValidationResult.Invalid(Phase3PolygonFailure.TooFewVertices, "A polygon requires at least three vertices.");
            }

            if (vertices.Count > MaximumVertexCount)
            {
                return Phase3PolygonValidationResult.Invalid(Phase3PolygonFailure.TooManyVertices, "A Phase 3 polygon supports at most five vertices.");
            }

            if (!AreAllVerticesWithinLogicalGrid(vertices))
            {
                return Phase3PolygonValidationResult.Invalid(Phase3PolygonFailure.PointOutsideGrid, "Every vertex must be inside the inclusive 0..16 grid boundary.");
            }

            if (HasZeroLengthEdge(vertices))
            {
                return Phase3PolygonValidationResult.Invalid(Phase3PolygonFailure.ZeroLengthEdge, "Zero-length edges are not allowed.");
            }

            if (HasDuplicateVertex(vertices))
            {
                return Phase3PolygonValidationResult.Invalid(Phase3PolygonFailure.DuplicateVertex, "Duplicate vertices are not allowed.");
            }

            if (HasRedundantCollinearVertex(vertices))
            {
                return Phase3PolygonValidationResult.Invalid(Phase3PolygonFailure.RedundantCollinearVertex, "Redundant consecutive collinear vertices are not allowed.");
            }

            if (!AreAllEdgesAllowed(vertices))
            {
                return Phase3PolygonValidationResult.Invalid(Phase3PolygonFailure.UnsupportedEdgeDirection, "Edges must be horizontal, vertical, or at 45 degrees.");
            }

            if (SignedDoubleArea(vertices) == 0L)
            {
                return Phase3PolygonValidationResult.Invalid(Phase3PolygonFailure.ZeroArea, "Polygon area must be greater than zero.");
            }

            if (HasSelfIntersection(vertices))
            {
                return Phase3PolygonValidationResult.Invalid(Phase3PolygonFailure.SelfIntersection, "Self-intersecting polygons are not allowed.");
            }

            if (!IsConvex(vertices))
            {
                return Phase3PolygonValidationResult.Invalid(Phase3PolygonFailure.Concave, "Only convex polygons are allowed.");
            }

            return Phase3PolygonValidationResult.Valid();
        }

        public static IReadOnlyList<Phase3GridPoint> CanonicalizeVertices(IReadOnlyList<Phase3GridPoint> vertices)
        {
            Phase3PolygonValidationResult validation = ValidatePolygon(vertices);
            if (!validation.IsValid)
            {
                throw new ArgumentException(validation.Message, nameof(vertices));
            }

            int count = vertices.Count;
            IReadOnlyList<Phase3GridPoint> ccw = NormalizeCounterClockwise(vertices);

            int minimumIndex = 0;
            for (int i = 1; i < count; i++)
            {
                if (ccw[i].CompareTo(ccw[minimumIndex]) < 0)
                {
                    minimumIndex = i;
                }
            }

            var canonical = new Phase3GridPoint[count];
            for (int i = 0; i < count; i++)
            {
                canonical[i] = ccw[(minimumIndex + i) % count];
            }

            return Array.AsReadOnly(canonical);
        }

        private static long Cross(Phase3GridPoint origin, Phase3GridPoint first, Phase3GridPoint second)
        {
            long firstX = (long)first.X - origin.X;
            long firstY = (long)first.Y - origin.Y;
            long secondX = (long)second.X - origin.X;
            long secondY = (long)second.Y - origin.Y;
            return firstX * secondY - firstY * secondX;
        }

        private static bool IsPointOnSegment(Phase3Point2D point, Phase3GridPoint start, Phase3GridPoint end)
        {
            double cross = (end.X - start.X) * (point.Y - start.Y) - (end.Y - start.Y) * (point.X - start.X);
            if (Math.Abs(cross) > Phase3CoreConstants.ComparisonEpsilon)
            {
                return false;
            }

            return point.X >= Math.Min(start.X, end.X) - Phase3CoreConstants.ComparisonEpsilon &&
                   point.X <= Math.Max(start.X, end.X) + Phase3CoreConstants.ComparisonEpsilon &&
                   point.Y >= Math.Min(start.Y, end.Y) - Phase3CoreConstants.ComparisonEpsilon &&
                   point.Y <= Math.Max(start.Y, end.Y) + Phase3CoreConstants.ComparisonEpsilon;
        }

        private static void RequireVertices(IReadOnlyList<Phase3GridPoint> vertices)
        {
            if (vertices == null)
            {
                throw new ArgumentNullException(nameof(vertices));
            }
        }
    }
}
