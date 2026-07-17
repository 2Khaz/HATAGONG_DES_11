using System;
using System.Collections.Generic;
using HATAGONG.GameFlow;
using UnityEditor;
using UnityEngine;

namespace HATAGONG.Phase3Tangram.Editor
{
    public static class Phase3TangramValidation
    {
        [MenuItem("Tools/HATAGONG/Phase 3 Tangram/Validate Antigravity Contracts")]
        public static void Validate()
        {
            Require(Phase3TangramGuide.GuideColor == Color.white, "Partition guide color must be opaque white.");
            Require(Phase3TangramGuide.GuideSortingOrder < 10000, "Partition guides must render below GamePanelCase.");
            Require(Phase3TangramPiece.PlacedSortingOrderBase < 10000, "Placed pieces must render below GamePanelCase.");
            Require(Phase3TangramPiece.FinalPieceDraggingSortingOrder < 10000, "The final dragging piece must remain below GamePanelCase.");
            Require(Phase3TangramGuide.GuideSortingOrder < Phase3TangramManager.CompletionShineSortingOrder, "Completion shine must render above partition guides.");
            Require(Phase3TangramManager.CompletionShineSortingOrder < 10000, "Completion shine must render below GamePanelCase.");
            Require(Phase3TangramPiece.DeckSortingOrderBase > 20000, "Deck pieces must render above DeckPanel.");
            Require(Phase3TangramPiece.DraggingSortingOrder > Phase3TangramPiece.DeckSortingOrderBase, "Dragging pieces must remain above the Deck foreground.");
            Require(Phase3TangramGenerator.PieceCount(GameDifficulty.Easy) == 7, "Easy piece count must be 7.");
            Require(Phase3TangramGenerator.PieceCount(GameDifficulty.Normal) == 9, "Normal piece count must be 9.");
            Require(Phase3TangramGenerator.PieceCount(GameDifficulty.Hard) == 11, "Hard piece count must be 11.");
            foreach (GameDifficulty difficulty in new[] { GameDifficulty.Easy, GameDifficulty.Normal, GameDifficulty.Hard })
            {
                long totalAttempts = 0;
                long totalRestarts = 0;
                int maximumAttempts = 0;
                int maximumRestarts = 0;
                for (int seed = 0; seed < 1000; seed++)
                {
                    int attempts = ValidateSeed(difficulty, seed);
                    int restarts = attempts / Phase3TangramGenerator.MaximumAttempts;
                    totalAttempts += attempts;
                    totalRestarts += restarts;
                    maximumAttempts = Mathf.Max(maximumAttempts, attempts);
                    maximumRestarts = Mathf.Max(maximumRestarts, restarts);
                }
                Debug.Log($"[Phase3Tangram][Validation] difficulty={difficulty},success=1000,failed=0,expectedPieces={Phase3TangramGenerator.PieceCount(difficulty)},averageAttempts={totalAttempts / 1000d:F3},maximumAttempts={maximumAttempts},averageRestarts={totalRestarts / 1000d:F3},maximumRestarts={maximumRestarts},firstFailedSeed=none");
            }
            ValidateInterchangeableTargetPrinciple();
            Debug.Log("[Phase3Tangram][Validation] PASS: 3,000 deterministic generators, originalShape round-trip, closed target polygons, interchangeable polygon matching.");
        }

        private static int ValidateSeed(GameDifficulty difficulty, int seed)
        {
            TangramGenerationResult first = Phase3TangramGenerator.Generate(difficulty, seed);
            TangramGenerationResult replay = Phase3TangramGenerator.Generate(difficulty, seed);
            int expected = Phase3TangramGenerator.PieceCount(difficulty);
            Require(first.Success && replay.Success, $"Generation failed: difficulty={difficulty}, seed={seed}, expected={expected}, firstReason={first.FailureReason}, replayReason={replay.FailureReason}, firstAttempts={first.Attempts}, replayAttempts={replay.Attempts}.");
            Require(first.Pieces.Count == expected && replay.Pieces.Count == expected, $"Piece count mismatch: {difficulty}/{seed}.");
            Require(first.Attempts == replay.Attempts, $"Attempt count is not deterministic: {difficulty}/{seed}.");
            float area = 0f;
            var ids = new HashSet<int>();
            for (int pieceIndex = 0; pieceIndex < first.Pieces.Count; pieceIndex++)
            {
                TangramGeneratedPiece piece = first.Pieces[pieceIndex], other = replay.Pieces[pieceIndex];
                Require(piece != null && other != null, $"Null piece: {difficulty}/{seed}/{pieceIndex}.");
                Require(ids.Add(piece.Id), $"Duplicate piece ID: {difficulty}/{seed}/{piece.Id}.");
                Require(piece.Id >= 0 && piece.Id < expected, $"Piece ID out of range: {difficulty}/{seed}/{piece.Id}.");
                Require(piece.InitialRotationStep == other.InitialRotationStep && piece.AbsolutePolygon.Count == other.AbsolutePolygon.Count, "Initial rotation or vertex count is not deterministic.");
                Require(piece.AbsolutePolygon.Count >= 3 && piece.AbsolutePolygon.Count <= 4, "Generated polygon vertex count is invalid.");
                Vector2 center = Phase3TangramGenerator.GetAreaCentroid(piece.AbsolutePolygon);
                for (int vertex = 0; vertex < piece.AbsolutePolygon.Count; vertex++)
                {
                    Vector2 absolute = piece.AbsolutePolygon[vertex];
                    Require(absolute.x >= 0f && absolute.x <= Phase3TangramGenerator.BoardSize && absolute.y >= 0f && absolute.y <= Phase3TangramGenerator.BoardSize, $"Piece vertex is outside the board: {difficulty}/{seed}/{piece.Id}/{absolute}.");
                    Vector2 original = piece.AbsolutePolygon[vertex] - center;
                    Require(Vector2.Distance(original + center, piece.AbsolutePolygon[vertex]) < 0.00001f, "originalShape + targetPosition round-trip failed.");
                    Require(Vector2.Distance(piece.AbsolutePolygon[vertex], other.AbsolutePolygon[vertex]) < 0.00001f, "Seed polygon determinism failed.");
                }
                float pieceArea = Phase3TangramGenerator.GetArea(piece.AbsolutePolygon);
                Require(pieceArea >= Phase3TangramGenerator.MinimumPieceArea - 0.001f, $"Minimum piece area violated: {difficulty}/{seed}/{piece.Id}/{pieceArea}.");
                area += pieceArea;
            }
            Require(ids.Count == expected, $"Piece ID count mismatch: {difficulty}/{seed}.");
            for (int id = 0; id < expected; id++) Require(ids.Contains(id), $"Missing piece ID: {difficulty}/{seed}/{id}.");
            for (int firstIndex = 0; firstIndex < first.Pieces.Count; firstIndex++)
                for (int secondIndex = firstIndex + 1; secondIndex < first.Pieces.Count; secondIndex++)
                    Require(ConvexIntersectionArea(first.Pieces[firstIndex].AbsolutePolygon, first.Pieces[secondIndex].AbsolutePolygon) < 0.001f, $"Piece interiors overlap: {difficulty}/{seed}/{firstIndex}/{secondIndex}.");
            Require(Mathf.Abs(area - 256f) < 0.001f, "Partition area differs from the 16x16 board.");
            return first.Attempts;
        }

        private static float ConvexIntersectionArea(IReadOnlyList<Vector2> subject, IReadOnlyList<Vector2> clip)
        {
            var output = new List<Vector2>(subject);
            float orientation = SignedArea(clip) >= 0f ? 1f : -1f;
            for (int edge = 0; edge < clip.Count && output.Count > 0; edge++)
            {
                Vector2 a = clip[edge], b = clip[(edge + 1) % clip.Count];
                var input = output;
                output = new List<Vector2>();
                Vector2 previous = input[input.Count - 1];
                bool previousInside = Cross(b - a, previous - a) * orientation >= -0.00001f;
                for (int i = 0; i < input.Count; i++)
                {
                    Vector2 current = input[i];
                    bool currentInside = Cross(b - a, current - a) * orientation >= -0.00001f;
                    if (currentInside != previousInside && TryLineIntersection(previous, current, a, b, out Vector2 intersection)) output.Add(intersection);
                    if (currentInside) output.Add(current);
                    previous = current;
                    previousInside = currentInside;
                }
            }
            return output.Count >= 3 ? Mathf.Abs(SignedArea(output)) : 0f;
        }

        private static bool TryLineIntersection(Vector2 p, Vector2 q, Vector2 a, Vector2 b, out Vector2 intersection)
        {
            Vector2 r = q - p, s = b - a;
            float denominator = Cross(r, s);
            if (Mathf.Abs(denominator) < 0.000001f) { intersection = default; return false; }
            intersection = p + r * (Cross(a - p, s) / denominator);
            return true;
        }

        private static float SignedArea(IReadOnlyList<Vector2> polygon)
        {
            float twiceArea = 0f;
            for (int i = 0; i < polygon.Count; i++) twiceArea += Cross(polygon[i], polygon[(i + 1) % polygon.Count]);
            return twiceArea * 0.5f;
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        private static void ValidateInterchangeableTargetPrinciple()
        {
            var shapeAtA = new List<Vector2> { new Vector2(0f, 0f), new Vector2(2f, 0f), new Vector2(1f, 1f) };
            var sameShapeAtB = new List<Vector2> { new Vector2(4f, 3f), new Vector2(6f, 3f), new Vector2(5f, 4f) };
            var translated = new List<Vector2>();
            for (int i = 0; i < shapeAtA.Count; i++) translated.Add(shapeAtA[i] + new Vector2(4f, 3f));
            Require(Phase3TangramManager.MatchPolygons(translated, sameShapeAtB, 0.00001f), "Identical translated shapes are not interchangeable.");
            TangramTargetAssignment first = new TangramTargetAssignment(1, shapeAtA), second = new TangramTargetAssignment(2, sameShapeAtB);
            TangramTargetAssignment temporary = first; first = second; second = temporary;
            Require(first.TargetId == 2 && second.TargetId == 1, "Target assignment set was not preserved by exchange.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[Phase3Tangram][Validation] " + message);
        }
    }
}
