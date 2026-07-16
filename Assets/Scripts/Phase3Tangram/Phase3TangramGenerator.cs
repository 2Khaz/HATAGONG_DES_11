using System;
using System.Collections.Generic;
using HATAGONG.GameFlow;
using UnityEngine;

namespace HATAGONG.Phase3Tangram
{
    public sealed class TangramGeneratedPiece
    {
        public TangramGeneratedPiece(int id, IReadOnlyList<Vector2> absolutePolygon, int initialRotationStep)
        {
            Id = id;
            AbsolutePolygon = absolutePolygon ?? throw new ArgumentNullException(nameof(absolutePolygon));
            InitialRotationStep = NormalizeStep(initialRotationStep);
        }

        public int Id { get; }
        public IReadOnlyList<Vector2> AbsolutePolygon { get; }
        public int InitialRotationStep { get; }
        private static int NormalizeStep(int value) => ((value % 8) + 8) % 8;
    }

    public sealed class TangramGenerationResult
    {
        private TangramGenerationResult(bool success, IReadOnlyList<TangramGeneratedPiece> pieces, string reason, int attempts)
        {
            Success = success;
            Pieces = pieces ?? Array.Empty<TangramGeneratedPiece>();
            FailureReason = reason ?? string.Empty;
            Attempts = attempts;
        }

        public bool Success { get; }
        public IReadOnlyList<TangramGeneratedPiece> Pieces { get; }
        public string FailureReason { get; }
        public int Attempts { get; }
        public static TangramGenerationResult Succeeded(IReadOnlyList<TangramGeneratedPiece> pieces, int attempts) => new TangramGenerationResult(true, pieces, string.Empty, attempts);
        public static TangramGenerationResult Failed(string reason, int attempts) => new TangramGenerationResult(false, null, reason, attempts);
    }

    public static class Phase3TangramGenerator
    {
        public const float BoardSize = 16f;
        public const int MaximumAttempts = 1500;
        public const int MaximumRestarts = 12;
        public const float MinimumPieceArea = 16f;
        private const float Epsilon = 0.0001f;

        private readonly struct Line
        {
            public Line(float a, float b, float c) { A = a; B = b; C = c; }
            public float A { get; }
            public float B { get; }
            public float C { get; }
        }

        public static TangramGenerationResult Generate(GameDifficulty difficulty, long requestedSeed)
        {
            TangramGenerationResult lastFailure = null;
            for (int restart = 0; restart < MaximumRestarts; restart++)
            {
                TangramGenerationResult result = GenerateAttempt(difficulty, requestedSeed, restart);
                if (result.Success) return result;
                lastFailure = result;
            }
            return TangramGenerationResult.Failed(lastFailure?.FailureReason ?? "GenerationBudgetExhausted", MaximumAttempts * MaximumRestarts);
        }

        private static TangramGenerationResult GenerateAttempt(GameDifficulty difficulty, long requestedSeed, int restart)
        {
            int targetCount = PieceCount(difficulty);
            var polygons = new List<List<Vector2>>
            {
                new List<Vector2> { new Vector2(0f, 0f), new Vector2(BoardSize, 0f), new Vector2(BoardSize, BoardSize), new Vector2(0f, BoardSize) }
            };
            var random = new TangramRandom(Mix(unchecked((ulong)requestedSeed) ^ ((ulong)restart * 0xD6E8FEB86659FD93UL), (ulong)((int)difficulty + 1)));
            int attempts = 0;

            while (polygons.Count < targetCount && attempts < MaximumAttempts)
            {
                attempts++;
                int polygonIndex = SelectByArea(polygons, random);
                List<Vector2> polygon = polygons[polygonIndex];
                List<Line> lines = BuildCandidateLines();
                Shuffle(lines, random);
                bool splitApplied = false;

                for (int lineIndex = 0; lineIndex < lines.Count; lineIndex++)
                {
                    if (!SplitConvexPolygon(polygon, lines[lineIndex], out List<Vector2> first, out List<Vector2> second)) continue;
                    if (!IsValidChild(first) || !IsValidChild(second)) continue;
                    polygons.RemoveAt(polygonIndex);
                    polygons.Add(first);
                    polygons.Add(second);
                    splitApplied = true;
                    break;
                }

                if (!splitApplied) continue;
            }

            int totalAttempts = restart * MaximumAttempts + attempts;
            if (polygons.Count != targetCount) return TangramGenerationResult.Failed("TargetPieceCountNotReached", totalAttempts);
            if (!ValidatePartition(polygons, targetCount, out string failure)) return TangramGenerationResult.Failed(failure, totalAttempts);

            var pieces = new List<TangramGeneratedPiece>(targetCount);
            for (int i = 0; i < polygons.Count; i++) pieces.Add(new TangramGeneratedPiece(i, polygons[i].AsReadOnly(), random.Next(8)));
            return TangramGenerationResult.Succeeded(pieces.AsReadOnly(), totalAttempts);
        }

        public static int PieceCount(GameDifficulty difficulty) => difficulty == GameDifficulty.Easy ? 7 : difficulty == GameDifficulty.Normal ? 9 : 11;

        public static float GetArea(IReadOnlyList<Vector2> polygon)
        {
            float area = 0f;
            for (int i = 0; i < polygon.Count; i++) { Vector2 next = polygon[(i + 1) % polygon.Count]; area += polygon[i].x * next.y - next.x * polygon[i].y; }
            return Mathf.Abs(area * 0.5f);
        }

        public static Vector2 GetAreaCentroid(IReadOnlyList<Vector2> polygon)
        {
            double twiceArea = 0d, x = 0d, y = 0d;
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 current = polygon[i], next = polygon[(i + 1) % polygon.Count];
                double cross = current.x * next.y - next.x * current.y;
                twiceArea += cross;
                x += (current.x + next.x) * cross;
                y += (current.y + next.y) * cross;
            }
            if (Math.Abs(twiceArea) < Epsilon) throw new ArgumentException("Polygon area is zero.", nameof(polygon));
            return new Vector2((float)(x / (3d * twiceArea)), (float)(y / (3d * twiceArea)));
        }

        private static int SelectByArea(IReadOnlyList<List<Vector2>> polygons, TangramRandom random)
        {
            double total = 0d;
            for (int i = 0; i < polygons.Count; i++) total += GetArea(polygons[i]);
            double selected = random.NextDouble() * total;
            for (int i = 0; i < polygons.Count; i++) { selected -= GetArea(polygons[i]); if (selected <= 0d) return i; }
            return polygons.Count - 1;
        }

        private static List<Line> BuildCandidateLines()
        {
            var lines = new List<Line>(128);
            for (int i = 1; i < 16; i++) { lines.Add(new Line(1f, 0f, -i)); lines.Add(new Line(0f, 1f, -i)); }
            for (int i = -16; i <= 32; i++) { lines.Add(new Line(1f, -1f, -i)); lines.Add(new Line(1f, 1f, -i)); }
            return lines;
        }

        private static void Shuffle<T>(IList<T> values, TangramRandom random)
        {
            for (int i = values.Count - 1; i > 0; i--) { int other = random.Next(i + 1); T value = values[i]; values[i] = values[other]; values[other] = value; }
        }

        private static bool SplitConvexPolygon(IReadOnlyList<Vector2> polygon, Line line, out List<Vector2> first, out List<Vector2> second)
        {
            first = new List<Vector2>();
            second = new List<Vector2>();
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 current = polygon[i], next = polygon[(i + 1) % polygon.Count];
                int currentSide = Classify(current, line), nextSide = Classify(next, line);
                if (currentSide >= 0) first.Add(current);
                if (currentSide <= 0) second.Add(current);
                if (currentSide * nextSide >= 0) continue;
                if (!TryIntersection(current, next, line, out Vector2 intersection)) continue;
                first.Add(intersection);
                second.Add(intersection);
            }
            first = CleanPolygon(first);
            second = CleanPolygon(second);
            return first.Count >= 3 && second.Count >= 3;
        }

        private static int Classify(Vector2 point, Line line)
        {
            float value = line.A * point.x + line.B * point.y + line.C;
            return Mathf.Abs(value) < 0.00001f ? 0 : value > 0f ? 1 : -1;
        }

        private static bool TryIntersection(Vector2 first, Vector2 second, Line line, out Vector2 intersection)
        {
            float firstDistance = line.A * first.x + line.B * first.y + line.C;
            Vector2 edge = second - first;
            float denominator = line.A * edge.x + line.B * edge.y;
            if (Mathf.Abs(denominator) < 0.000000001f) { intersection = default; return false; }
            float t = -firstDistance / denominator;
            intersection = first + edge * t;
            return true;
        }

        private static List<Vector2> CleanPolygon(IReadOnlyList<Vector2> polygon)
        {
            var result = new List<Vector2>();
            for (int i = 0; i < polygon.Count; i++) if (result.Count == 0 || Vector2.Distance(result[result.Count - 1], polygon[i]) > Epsilon) result.Add(polygon[i]);
            if (result.Count > 1 && Vector2.Distance(result[0], result[result.Count - 1]) < Epsilon) result.RemoveAt(result.Count - 1);
            if (SignedDoubleArea(result) < 0f) result.Reverse();
            return result;
        }

        private static bool IsValidChild(IReadOnlyList<Vector2> polygon)
        {
            return polygon.Count >= 3 && polygon.Count <= 4 && GetArea(polygon) + Epsilon >= MinimumPieceArea && HasValidAngles(polygon) && IsCompact(polygon) && IsConvex(polygon);
        }

        private static bool HasValidAngles(IReadOnlyList<Vector2> polygon)
        {
            float diagonal = Mathf.Sqrt(0.5f);
            float[] valid = { 0f, 1f, -1f, diagonal, -diagonal };
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 first = polygon[(i - 1 + polygon.Count) % polygon.Count] - polygon[i];
                Vector2 second = polygon[(i + 1) % polygon.Count] - polygon[i];
                if (first.sqrMagnitude < Epsilon * Epsilon || second.sqrMagnitude < Epsilon * Epsilon) return false;
                float cosine = Vector2.Dot(first, second) / (first.magnitude * second.magnitude);
                bool accepted = false;
                for (int value = 0; value < valid.Length; value++) if (Mathf.Abs(cosine - valid[value]) < 0.001f) { accepted = true; break; }
                if (!accepted) return false;
            }
            return true;
        }

        private static bool IsCompact(IReadOnlyList<Vector2> polygon)
        {
            Bounds(polygon, out Vector2 min, out Vector2 max);
            float width = max.x - min.x, height = max.y - min.y;
            return width > Epsilon && height > Epsilon && width / height <= 2.0001f && height / width <= 2.0001f;
        }

        private static bool IsConvex(IReadOnlyList<Vector2> polygon)
        {
            float sign = 0f;
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 a = polygon[i], b = polygon[(i + 1) % polygon.Count], c = polygon[(i + 2) % polygon.Count];
                float cross = Cross(b - a, c - b);
                if (Mathf.Abs(cross) < Epsilon) continue;
                if (sign == 0f) sign = Mathf.Sign(cross); else if (Mathf.Sign(cross) != sign) return false;
            }
            return sign != 0f;
        }

        private static bool ValidatePartition(IReadOnlyList<List<Vector2>> polygons, int expectedCount, out string failure)
        {
            if (polygons.Count != expectedCount) { failure = "PieceCountMismatch"; return false; }
            float area = 0f;
            for (int i = 0; i < polygons.Count; i++)
            {
                if (!IsValidChild(polygons[i])) { failure = "InvalidFinalPolygon"; return false; }
                for (int vertex = 0; vertex < polygons[i].Count; vertex++)
                {
                    Vector2 point = polygons[i][vertex];
                    if (point.x < -Epsilon || point.y < -Epsilon || point.x > BoardSize + Epsilon || point.y > BoardSize + Epsilon) { failure = "VertexOutsideBoard"; return false; }
                }
                area += GetArea(polygons[i]);
            }
            if (Mathf.Abs(area - BoardSize * BoardSize) > 0.001f) { failure = "PartitionAreaMismatch"; return false; }
            failure = string.Empty;
            return true;
        }

        private static void Bounds(IReadOnlyList<Vector2> polygon, out Vector2 min, out Vector2 max)
        {
            min = new Vector2(float.MaxValue, float.MaxValue); max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < polygon.Count; i++) { min = Vector2.Min(min, polygon[i]); max = Vector2.Max(max, polygon[i]); }
        }

        private static float SignedDoubleArea(IReadOnlyList<Vector2> polygon)
        {
            float area = 0f;
            for (int i = 0; i < polygon.Count; i++) { Vector2 next = polygon[(i + 1) % polygon.Count]; area += polygon[i].x * next.y - next.x * polygon[i].y; }
            return area;
        }

        private static float Cross(Vector2 first, Vector2 second) => first.x * second.y - first.y * second.x;
        private static ulong Mix(ulong seed, ulong difficulty) { ulong value = seed ^ 0x9E3779B97F4A7C15UL ^ (difficulty << 48); value ^= value >> 30; value *= 0xBF58476D1CE4E5B9UL; value ^= value >> 27; value *= 0x94D049BB133111EBUL; return value ^ (value >> 31); }

        private sealed class TangramRandom
        {
            private ulong state;
            public TangramRandom(ulong seed) { state = seed == 0UL ? 1UL : seed; }
            public int Next(int maximum) => (int)(NextUInt64() % (uint)maximum);
            public double NextDouble() => (NextUInt64() >> 11) * (1d / 9007199254740992d);
            private ulong NextUInt64() { state += 0x9E3779B97F4A7C15UL; ulong value = state; value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL; value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL; return value ^ (value >> 31); }
        }
    }
}
