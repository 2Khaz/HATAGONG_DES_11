#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Stopwatch = System.Diagnostics.Stopwatch;
using HATAGONG.GameFlow;
using UnityEditor;
using UnityEngine;

namespace HATAGONG.Phase3.Editor
{
    public static class Phase3StageP3_5PuzzleGeneratorValidation
    {
        private const int SamplesPerDifficulty = 1000;

        [MenuItem("Tools/HATAGONG/Phase 3/Stage P3-5 Puzzle Generator Validation")]
        public static void Validate()
        {
            int passed = 0;
            int total = 0;
            Check(
                string.Equals(
                    Phase3PuzzleGenerator.GeneratorVersion,
                    "phase3-recursive-grid-split-v4",
                    StringComparison.Ordinal),
                "Generator version", ref passed, ref total);
            ValidateDeterminism(ref passed, ref total);
            ValidateShapeSignature(ref passed, ref total);
            ValidateForcedHistoryCollision(ref passed, ref total);
            ValidateImmediateShapeHistory(ref passed, ref total);
            Audit(GameDifficulty.Easy, 8, ref passed, ref total);
            Audit(GameDifficulty.Normal, 10, ref passed, ref total);
            Audit(GameDifficulty.Hard, 12, ref passed, ref total);
            Debug.Log($"[Phase3][P3-5 v4] result={passed}/{total}");
        }

        private static void ValidateDeterminism(ref int passed, ref int total)
        {
            Phase3PuzzleGenerationResult first = Generate(20260714L, GameDifficulty.Hard);
            Phase3PuzzleGenerationResult second = Generate(20260714L, GameDifficulty.Hard);
            Check(first.Succeeded && second.Succeeded, "Determinism generation", ref passed, ref total);
            Check(first.CanonicalHash == second.CanonicalHash, "Determinism hash", ref passed, ref total);
            Check(first.EffectiveSeed == second.EffectiveSeed, "Determinism effective seed", ref passed, ref total);
            Check(first.AttemptIndex == second.AttemptIndex, "Determinism attempt", ref passed, ref total);
            Check(EqualPieces(first.GeneratedPieces, second.GeneratedPieces), "Determinism vertices", ref passed, ref total);
        }

        private static void ValidateShapeSignature(ref int passed, ref int total)
        {
            Phase3GridPoint[] first =
            {
                new Phase3GridPoint(0, 0), new Phase3GridPoint(4, 0),
                new Phase3GridPoint(4, 2), new Phase3GridPoint(0, 2)
            };
            Phase3GridPoint[] scaledReflected =
            {
                new Phase3GridPoint(6, 6), new Phase3GridPoint(6, 0),
                new Phase3GridPoint(3, 0), new Phase3GridPoint(3, 6)
            };
            Check(
                Phase3PuzzleGenerator.ComputeShapeSignature(first) ==
                Phase3PuzzleGenerator.ComputeShapeSignature(scaledReflected),
                "Shape signature ignores scale/rotation/reflection",
                ref passed,
                ref total);
        }

        private static void ValidateForcedHistoryCollision(ref int passed, ref int total)
        {
            const long seed = 741852963L;
            Phase3PuzzleGenerationResult first = Generate(seed, GameDifficulty.Normal);
            Phase3PuzzleGenerationResult second = Phase3PuzzleGenerator.Generate(
                new Phase3PuzzleGenerationRequest(
                    seed,
                    GameDifficulty.Normal,
                    new[] { first.CanonicalHash },
                    Phase3PuzzleGenerator.DefaultMaximumAttempts));
            Check(first.Succeeded && second.Succeeded, "Forced exact history generation", ref passed, ref total);
            Check(second.AttemptIndex >= 1, "Forced exact history advances attempt", ref passed, ref total);
            Check(second.ExactHistoryRejectionCount == 1, "Forced exact history rejection count", ref passed, ref total);
            Check(second.RequestedSeed == seed, "Forced exact history preserves requested seed", ref passed, ref total);
            Phase3PuzzleGenerationResult regenerated = Phase3PuzzleGenerator.RegenerateCandidate(
                seed, GameDifficulty.Normal, second.AttemptIndex);
            Check(
                regenerated.Succeeded &&
                regenerated.CanonicalHash == second.CanonicalHash &&
                regenerated.EffectiveSeed == second.EffectiveSeed &&
                EqualPieces(regenerated.GeneratedPieces, second.GeneratedPieces),
                "Forced exact history regeneration",
                ref passed,
                ref total);
        }

        private static void ValidateImmediateShapeHistory(ref int passed, ref int total)
        {
            Phase3PuzzleGenerationResult first = Generate(918273645L, GameDifficulty.Hard);
            var signatures = new List<string>();
            for (int i = 0; i < first.GeneratedPieces.Count; i++)
                signatures.Add(first.GeneratedPieces[i].ShapeSignature);
            Phase3PuzzleGenerationResult next = Phase3PuzzleGenerator.Generate(
                new Phase3PuzzleGenerationRequest(
                    918273646L,
                    GameDifficulty.Hard,
                    null,
                    Phase3PuzzleGenerator.DefaultMaximumAttempts,
                    new[] { signatures }));
            var overlap = new HashSet<string>(signatures, StringComparer.Ordinal);
            bool disjoint = next.Succeeded;
            for (int i = 0; i < next.GeneratedPieces.Count; i++)
                disjoint &= !overlap.Contains(next.GeneratedPieces[i].ShapeSignature);
            Check(disjoint, "Immediate previous shape signatures forbidden", ref passed, ref total);
        }

        private static void Audit(
            GameDifficulty difficulty,
            int expectedPieceCount,
            ref int passed,
            ref int total)
        {
            int succeeded = 0;
            int partitionFailures = 0;
            int contractFailures = 0;
            long totalMilliseconds = 0;
            long maximumMilliseconds = 0;
            long totalCycles = 0;
            long totalBacktracking = 0;
            long backtrackingBudgetExhaustions = 0;
            long candidateExhaustions = 0;
            long currentShapeDuplicateRejections = 0;
            long exactHistoryRejections = 0;
            var timings = new List<long>(SamplesPerDifficulty);
            var hashes = new HashSet<string>(StringComparer.Ordinal);
            var shapeDistribution = new Dictionary<Phase3GeneratedShapeKind, int>();
            for (int sample = 0; sample < SamplesPerDifficulty; sample++)
            {
                var stopwatch = Stopwatch.StartNew();
                Phase3PuzzleGenerationResult result = Generate(202607140000L + sample, difficulty);
                stopwatch.Stop();
                timings.Add(stopwatch.ElapsedMilliseconds);
                totalMilliseconds += stopwatch.ElapsedMilliseconds;
                maximumMilliseconds = Math.Max(maximumMilliseconds, stopwatch.ElapsedMilliseconds);
                if (!result.Succeeded) continue;
                succeeded++;
                hashes.Add(result.CanonicalHash);
                totalCycles += result.GenerationCycles;
                totalBacktracking += result.BacktrackingCount;
                backtrackingBudgetExhaustions += result.BacktrackingBudgetExhaustionCount;
                candidateExhaustions += result.ExhaustedCandidateCount;
                currentShapeDuplicateRejections += result.CurrentShapeDuplicateRejectionCount;
                exactHistoryRejections += result.ExactHistoryRejectionCount;
                Phase3PartitionValidationResult partition = Phase3PartitionValidator.Validate(
                    result.Puzzle,
                    difficulty,
                    Phase3PuzzleGeneratorDifficultyConfig.For(difficulty).PartitionRules);
                if (!partition.IsValid) partitionFailures++;
                if (!ValidateResultContract(result, expectedPieceCount, difficulty)) contractFailures++;
                for (int piece = 0; piece < result.GeneratedPieces.Count; piece++)
                {
                    Phase3GeneratedShapeKind kind = result.GeneratedPieces[piece].ShapeKind;
                    shapeDistribution.TryGetValue(kind, out int count);
                    shapeDistribution[kind] = count + 1;
                }
            }
            timings.Sort();
            long p95 = timings[(int)Math.Ceiling(timings.Count * 0.95d) - 1];
            Check(succeeded == SamplesPerDifficulty, $"{difficulty} 1000-seed success", ref passed, ref total);
            Check(partitionFailures == 0, $"{difficulty} partition", ref passed, ref total);
            Check(contractFailures == 0, $"{difficulty} contracts", ref passed, ref total);
            Check(hashes.Count >= 999, $"{difficulty} exact hash diversity", ref passed, ref total);
            Debug.Log(
                $"[Phase3][P3-5 Audit] difficulty={difficulty},samples={SamplesPerDifficulty}," +
                $"grid=Integer,failed={SamplesPerDifficulty - succeeded}," +
                $"uniqueHashes={hashes.Count},avgMs={(double)totalMilliseconds / SamplesPerDifficulty:F2}," +
                $"p95Ms={p95},maxMs={maximumMilliseconds},avgCycles={(double)totalCycles / Math.Max(1, succeeded):F2}," +
                $"avgBacktracking={(double)totalBacktracking / Math.Max(1, succeeded):F2}," +
                $"backtrackingBudgetExhaustions={backtrackingBudgetExhaustions},candidateExhaustions={candidateExhaustions}," +
                $"currentShapeDuplicateRejections={currentShapeDuplicateRejections},exactHistoryRejections={exactHistoryRejections}," +
                $"shapeDistribution={FormatDistribution(shapeDistribution)}");
        }

        private static bool ValidateResultContract(
            Phase3PuzzleGenerationResult result,
            int expectedPieceCount,
            GameDifficulty difficulty)
        {
            if (result.GeneratedPieces.Count != expectedPieceCount || result.CanonicalHash.Length != 64) return false;
            var signatures = new HashSet<string>(StringComparer.Ordinal);
            var kindCounts = new Dictionary<Phase3GeneratedShapeKind, int>();
            int complex = 0;
            int sameRotation = 0;
            for (int i = 0; i < result.GeneratedPieces.Count; i++)
            {
                Phase3GeneratedPieceData piece = result.GeneratedPieces[i];
                if (!signatures.Add(piece.ShapeSignature)) return false;
                if (!Phase3PuzzleGenerator.MeetsQualityLimits(
                    piece.Vertices,
                    Phase3PuzzleGeneratorDifficultyConfig.For(difficulty),
                    out _)) return false;
                kindCounts.TryGetValue(piece.ShapeKind, out int count);
                kindCounts[piece.ShapeKind] = count + 1;
                if (piece.ShapeKind == Phase3GeneratedShapeKind.Rhombus ||
                    piece.ShapeKind == Phase3GeneratedShapeKind.Parallelogram ||
                    piece.ShapeKind == Phase3GeneratedShapeKind.Trapezoid) complex++;
                if (piece.InitialRotation == piece.TargetRotation) sameRotation++;
            }
            foreach (KeyValuePair<Phase3GeneratedShapeKind, int> pair in kindCounts)
                if (pair.Value > expectedPieceCount / 2) return false;
            if (Count(kindCounts, Phase3GeneratedShapeKind.Square) > 1 ||
                Count(kindCounts, Phase3GeneratedShapeKind.RightTriangle) > 1 ||
                Count(kindCounts, Phase3GeneratedShapeKind.Rhombus) > 1) return false;
            if (difficulty == GameDifficulty.Normal &&
                Count(kindCounts, Phase3GeneratedShapeKind.Rhombus) +
                Count(kindCounts, Phase3GeneratedShapeKind.Parallelogram) < 1) return false;
            if (difficulty == GameDifficulty.Hard && (complex < 2 || sameRotation != 0)) return false;
            return difficulty != GameDifficulty.Easy || sameRotation <= 3;
        }

        private static int Count(
            IDictionary<Phase3GeneratedShapeKind, int> values,
            Phase3GeneratedShapeKind kind) =>
            values.TryGetValue(kind, out int count) ? count : 0;

        private static Phase3PuzzleGenerationResult Generate(long seed, GameDifficulty difficulty) =>
            Phase3PuzzleGenerator.Generate(
                new Phase3PuzzleGenerationRequest(
                    seed,
                    difficulty,
                    null,
                    Phase3PuzzleGenerator.DefaultMaximumAttempts));

        private static bool EqualPieces(
            IReadOnlyList<Phase3GeneratedPieceData> first,
            IReadOnlyList<Phase3GeneratedPieceData> second)
        {
            if (first.Count != second.Count) return false;
            for (int i = 0; i < first.Count; i++)
            {
                if (first[i].PieceId != second[i].PieceId ||
                    first[i].Vertices.Count != second[i].Vertices.Count) return false;
                for (int vertex = 0; vertex < first[i].Vertices.Count; vertex++)
                    if (first[i].Vertices[vertex] != second[i].Vertices[vertex]) return false;
            }
            return true;
        }

        private static string FormatDistribution(
            IReadOnlyDictionary<Phase3GeneratedShapeKind, int> distribution)
        {
            var values = new List<string>();
            foreach (KeyValuePair<Phase3GeneratedShapeKind, int> pair in distribution)
                values.Add($"{pair.Key}:{pair.Value}");
            values.Sort(StringComparer.Ordinal);
            return string.Join(",", values);
        }

        private static void Check(bool condition, string name, ref int passed, ref int total)
        {
            total++;
            if (condition) passed++;
            else Debug.LogError($"[Phase3][P3-5 v4] FAIL {name}");
        }
    }
}
#endif
