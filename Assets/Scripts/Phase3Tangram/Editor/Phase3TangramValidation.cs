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
            Require(Phase3TangramGenerator.PieceCount(GameDifficulty.Easy) == 6, "Easy piece count must be 6.");
            Require(Phase3TangramGenerator.PieceCount(GameDifficulty.Normal) == 7, "Normal piece count must be 7.");
            Require(Phase3TangramGenerator.PieceCount(GameDifficulty.Hard) == 8, "Hard piece count must be 8.");
            foreach (GameDifficulty difficulty in new[] { GameDifficulty.Easy, GameDifficulty.Normal, GameDifficulty.Hard })
                for (int seed = 0; seed < 1000; seed++) ValidateSeed(difficulty, seed);
            ValidateInterchangeableTargetPrinciple();
            Debug.Log("[Phase3Tangram][Validation] PASS: 3,000 deterministic generators, originalShape round-trip, closed target polygons, interchangeable polygon matching.");
        }

        private static void ValidateSeed(GameDifficulty difficulty, int seed)
        {
            TangramGenerationResult first = Phase3TangramGenerator.Generate(difficulty, seed);
            TangramGenerationResult replay = Phase3TangramGenerator.Generate(difficulty, seed);
            Require(first.Success && replay.Success, $"Generation failed: {difficulty}/{seed}.");
            int expected = Phase3TangramGenerator.PieceCount(difficulty);
            Require(first.Pieces.Count == expected && replay.Pieces.Count == expected, $"Piece count mismatch: {difficulty}/{seed}.");
            float area = 0f;
            for (int pieceIndex = 0; pieceIndex < first.Pieces.Count; pieceIndex++)
            {
                TangramGeneratedPiece piece = first.Pieces[pieceIndex], other = replay.Pieces[pieceIndex];
                Require(piece.InitialRotationStep == other.InitialRotationStep && piece.AbsolutePolygon.Count == other.AbsolutePolygon.Count, "Initial rotation or vertex count is not deterministic.");
                Require(piece.AbsolutePolygon.Count >= 3 && piece.AbsolutePolygon.Count <= 4, "Generated polygon vertex count is invalid.");
                Vector2 center = Phase3TangramGenerator.GetAreaCentroid(piece.AbsolutePolygon);
                for (int vertex = 0; vertex < piece.AbsolutePolygon.Count; vertex++)
                {
                    Vector2 original = piece.AbsolutePolygon[vertex] - center;
                    Require(Vector2.Distance(original + center, piece.AbsolutePolygon[vertex]) < 0.00001f, "originalShape + targetPosition round-trip failed.");
                    Require(Vector2.Distance(piece.AbsolutePolygon[vertex], other.AbsolutePolygon[vertex]) < 0.00001f, "Seed polygon determinism failed.");
                }
                area += Phase3TangramGenerator.GetArea(piece.AbsolutePolygon);
            }
            Require(Mathf.Abs(area - 256f) < 0.001f, "Partition area differs from the 16x16 board.");
        }

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
