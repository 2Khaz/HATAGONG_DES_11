#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using HATAGONG.GameFlow;
using UnityEditor;
using UnityEngine;

namespace HATAGONG.Phase3.Editor
{
    public static class Phase3StageP3_5PuzzleGeneratorValidation
    {
        [MenuItem("Tools/HATAGONG/Phase 3/Stage P3-5 Puzzle Generator Validation")]
        public static void Validate()
        {
            var validation = new ValidationContext();
            validation.RunSection("Determinism", () => ValidateDeterminism(validation));
            validation.RunSection("Easy", () => ValidateDifficulty(validation, GameDifficulty.Easy, 8));
            validation.RunSection("Normal", () => ValidateDifficulty(validation, GameDifficulty.Normal, 10));
            validation.RunSection("Hard", () => ValidateDifficulty(validation, GameDifficulty.Hard, 12));
            validation.RunSection("Shape catalog and symmetry", () => ValidateShapeCatalogAndSymmetry(validation));
            validation.RunSection("Core geometry contracts", () => ValidateCoreGeometryContracts(validation));
            validation.RunSection("Canonical hash normalization", () => ValidateHashNormalization(validation));
            validation.RunSection("Difficulty diversity", () => ValidateDifficultyDiversity(validation));
            validation.RunSection("Seed diversity", () => ValidateSeedDiversity(validation));
            validation.RunSection("History and bounded attempts", () => ValidateHistory(validation));
            validation.RunSection("Sequential accumulated history", () => ValidateSequentialHistory(validation));
            validation.RunSection("Failure contract", () => ValidateFailureContract(validation));
            validation.RunSection("256-seed quality audit", () => ValidateQuality256(validation));

            for (int i = 0; i < validation.Failures.Count; i++) Debug.LogError(validation.Failures[i]);
            Debug.Log($"[Phase3][P3-5] result={validation.Passed}/{validation.Total}, failures={validation.Failures.Count}");
        }

        private static void ValidateDeterminism(ValidationContext validation)
        {
            Phase3PuzzleGenerationResult first = Generate(20260714L, GameDifficulty.Hard);
            Phase3PuzzleGenerationResult second = Generate(20260714L, GameDifficulty.Hard);
            validation.Check(first.Succeeded && second.Succeeded, "Determinism.GenerationSucceeds", true, $"{first.Failure}/{second.Failure}");
            validation.Equal(Phase3PuzzleGenerator.GeneratorVersion, "phase3-geometric-partition-v3", "Determinism.VersionChangedForNewOutput");
            validation.Equal(first.CanonicalHash, second.CanonicalHash, "Determinism.CanonicalHash");
            validation.Equal(first.PuzzleId, second.PuzzleId, "Determinism.PuzzleId");
            validation.Equal(first.Signature.Value, second.Signature.Value, "Determinism.Signature");
            validation.Equal(first.AttemptsUsed, second.AttemptsUsed, "Determinism.Attempts");
            validation.Equal(first.EffectiveSeed, second.EffectiveSeed, "Determinism.EffectiveSeed");
            validation.Equal(first.AttemptIndex, second.AttemptIndex, "Determinism.AttemptIndex");
            validation.Check(EqualGeneratedPieces(first.GeneratedPieces, second.GeneratedPieces), "Determinism.FullPieceData", true, false);
            var reversed = new List<Phase3GeneratedPieceData>(first.GeneratedPieces);
            reversed.Reverse();
            validation.Equal(first.CanonicalHash, Phase3PuzzleGenerator.ComputeCanonicalHash(first.Difficulty, reversed), "Determinism.HashIgnoresPieceOrder");
        }

        private static void ValidateDifficulty(ValidationContext validation, GameDifficulty difficulty, int expectedCount)
        {
            Phase3PuzzleGenerationResult result = Generate(73129L + (int)difficulty, difficulty);
            validation.Check(result.Succeeded, $"{difficulty}.GenerationSucceeds", true, result.FailureReason);
            if (!result.Succeeded) return;

            Phase3PuzzleGeneratorDifficultyConfig config = Phase3PuzzleGeneratorDifficultyConfig.For(difficulty);
            validation.Equal(expectedCount, config.PieceCount, $"{difficulty}.ConfigPieceCount");
            validation.Equal(expectedCount, result.Puzzle.Pieces.Count, $"{difficulty}.PieceCount");
            validation.Equal(expectedCount, result.Puzzle.Slots.Count, $"{difficulty}.SlotCount");
            validation.Equal(expectedCount, result.GeneratedPieces.Count, $"{difficulty}.GeneratedDataCount");
            validation.Equal(64, result.CanonicalHash.Length, $"{difficulty}.Sha256Length");

            var pieceIds = new HashSet<string>(StringComparer.Ordinal);
            var slotIds = new HashSet<string>(StringComparer.Ordinal);
            var shapeKinds = new HashSet<Phase3GeneratedShapeKind>();
            double areaSum = 0d;
            bool allGeometryValid = true;
            bool allInsideField = true;
            bool allQualityValid = true;
            bool allTargetsValid = true;
            bool allInitialRotationsDistinct = true;
            for (int i = 0; i < result.GeneratedPieces.Count; i++)
            {
                Phase3GeneratedPieceData generated = result.GeneratedPieces[i];
                pieceIds.Add(generated.PieceId);
                slotIds.Add(generated.SlotId);
                shapeKinds.Add(generated.ShapeKind);
                areaSum += generated.Area;
                Phase3PieceDefinition piece = FindPiece(result.Puzzle.Pieces, generated.PieceId);
                Phase3SlotDefinition slot = FindSlot(result.Puzzle.Slots, generated.SlotId);
                Phase3PolygonValidationResult polygon = Phase3Geometry.ValidatePolygon(generated.Vertices);
                allGeometryValid &= polygon.IsValid && Phase3Geometry.IsConvex(generated.Vertices) &&
                    !Phase3Geometry.HasSelfIntersection(generated.Vertices) && generated.Area > 0d &&
                    Phase3PuzzleGenerator.ClassifyShape(generated.Vertices) == generated.ShapeKind;
                Phase3Bounds2D bounds = Phase3Geometry.GetBounds(generated.Vertices);
                allInsideField &= bounds.MinX >= 0d && bounds.MinY >= 0d &&
                    bounds.MaxX <= Phase3CoreConstants.LogicalGridSize && bounds.MaxY <= Phase3CoreConstants.LogicalGridSize;
                allQualityValid &= Phase3PuzzleGenerator.MeetsQualityLimits(generated.Vertices, config, out _);
                allTargetsValid &= piece != null && slot != null && piece.AllowedTargets.Count == 1 &&
                    string.Equals(piece.AllowedTargets[0].SlotId, generated.SlotId, StringComparison.Ordinal) &&
                    piece.AllowedTargets[0].RequiredRotationStep == generated.TargetRotation &&
                    piece.ShapeDefinition.Vertices.Count == generated.Vertices.Count;
                allInitialRotationsDistinct &= generated.InitialRotation.Value >= 0 &&
                    generated.InitialRotation.Value < Phase3CoreConstants.FullRotationStepCount &&
                    !generated.InitialRotation.IsEquivalentTo(generated.TargetRotation, generated.RotationalSymmetryPeriodSteps);
            }

            validation.Equal(expectedCount, pieceIds.Count, $"{difficulty}.UniquePieceIds");
            validation.Equal(expectedCount, slotIds.Count, $"{difficulty}.UniqueSlotIds");
            validation.Near(Phase3CoreConstants.LogicalFieldArea, areaSum, $"{difficulty}.AreaSum");
            validation.Check(allGeometryValid, $"{difficulty}.GeometryValidAndConnected", true, false);
            validation.Check(allInsideField, $"{difficulty}.NoOutsideArea", true, false);
            validation.Check(allQualityValid, $"{difficulty}.MinimumAreaAngleWidth", true, false);
            validation.Check(allTargetsValid, $"{difficulty}.OneToOneTargets", true, false);
            validation.Check(allInitialRotationsDistinct, $"{difficulty}.InitialRotationNotSymmetryEquivalent", true, false);
            validation.Check(shapeKinds.Contains(Phase3GeneratedShapeKind.Triangle), $"{difficulty}.TriangleGenerated", true, shapeKinds.Count);
            validation.Check(shapeKinds.Contains(Phase3GeneratedShapeKind.Parallelogram), $"{difficulty}.ParallelogramGenerated", true, shapeKinds.Count);
            validation.Check(ContainsQuadrilateral(shapeKinds), $"{difficulty}.QuadrilateralGenerated", true, shapeKinds.Count);
            Phase3PartitionValidationResult partition = Phase3PartitionValidator.Validate(result.Puzzle, difficulty, config.PartitionRules);
            validation.Check(partition.IsValid, $"{difficulty}.NoGapOverlapAndCoreValid", true, partition.Issues.Count > 0 ? partition.Issues[0].ToString() : "valid");
            validation.Near(Phase3CoreConstants.LogicalFieldArea, partition.PieceAreaSum, $"{difficulty}.PolygonAreaSum");
        }

        private static void ValidateShapeCatalogAndSymmetry(ValidationContext validation)
        {
            Phase3GridPoint[] triangle = { P(0, 0), P(6, 0), P(0, 6) };
            Phase3GridPoint[] rectangle = { P(0, 0), P(8, 0), P(8, 4), P(0, 4) };
            Phase3GridPoint[] square = { P(0, 0), P(4, 0), P(4, 4), P(0, 4) };
            Phase3GridPoint[] parallelogram = { P(0, 0), P(8, 0), P(14, 6), P(6, 6) };
            Phase3GridPoint[] quadrilateral = { P(0, 0), P(2, 0), P(8, 6), P(0, 6) };
            validation.Equal(Phase3GeneratedShapeKind.Triangle, Phase3PuzzleGenerator.ClassifyShape(triangle), "Catalog.Triangle");
            validation.Equal(Phase3GeneratedShapeKind.Rectangle, Phase3PuzzleGenerator.ClassifyShape(rectangle), "Catalog.Rectangle");
            validation.Equal(Phase3GeneratedShapeKind.Square, Phase3PuzzleGenerator.ClassifyShape(square), "Catalog.Square");
            validation.Equal(Phase3GeneratedShapeKind.Parallelogram, Phase3PuzzleGenerator.ClassifyShape(parallelogram), "Catalog.Parallelogram");
            validation.Equal(Phase3GeneratedShapeKind.Quadrilateral, Phase3PuzzleGenerator.ClassifyShape(quadrilateral), "Catalog.GeneralQuadrilateral");
            validation.Equal(8, Phase3PuzzleGenerator.DetermineRotationalSymmetryPeriod(triangle), "Symmetry.Triangle360");
            validation.Equal(4, Phase3PuzzleGenerator.DetermineRotationalSymmetryPeriod(rectangle), "Symmetry.Rectangle180");
            validation.Equal(2, Phase3PuzzleGenerator.DetermineRotationalSymmetryPeriod(square), "Symmetry.Square90");
            validation.Equal(4, Phase3PuzzleGenerator.DetermineRotationalSymmetryPeriod(parallelogram), "Symmetry.Parallelogram180");
            validation.Equal(8, Phase3PuzzleGenerator.DetermineRotationalSymmetryPeriod(quadrilateral), "Symmetry.Quadrilateral360");
        }

        private static void ValidateCoreGeometryContracts(ValidationContext validation)
        {
            Phase3GridPoint[] concave = { P(0, 0), P(6, 0), P(2, 2), P(0, 6) };
            Phase3GridPoint[] bowTie = { P(0, 0), P(6, 6), P(0, 6), P(6, 0) };
            Phase3GridPoint[] collinear = { P(0, 0), P(3, 0), P(6, 0) };
            validation.Check(!Phase3Geometry.ValidatePolygon(concave).IsValid, "Convexity.ConcaveRejected", true, false);
            validation.Check(!Phase3Geometry.ValidatePolygon(bowTie).IsValid, "Convexity.SelfIntersectionRejected", true, false);
            validation.Check(!Phase3Geometry.ValidatePolygon(collinear).IsValid, "Convexity.ZeroAreaRejected", true, false);

            Phase3PuzzleGeneratorDifficultyConfig easy = Phase3PuzzleGeneratorDifficultyConfig.For(GameDifficulty.Easy);
            Phase3GridPoint[] thinRectangle = { P(0, 0), P(16, 0), P(16, 1), P(0, 1) };
            Phase3GridPoint[] diagonalThin = { P(0, 0), P(2, 0), P(10, 8), P(8, 8) };
            validation.Near(1d, Phase3PuzzleGenerator.CalculateMinimumThickness(thinRectangle), "Thickness.AxisAlignedValue");
            validation.Check(!Phase3PuzzleGenerator.MeetsQualityLimits(thinRectangle, easy, out _), "Thickness.AxisAlignedThinRejected", true, false);
            double diagonalThickness = Phase3PuzzleGenerator.CalculateMinimumThickness(diagonalThin);
            validation.Check(diagonalThickness < 2d, "Thickness.DiagonalActualValue", "<2", diagonalThickness);
            validation.Check(!Phase3PuzzleGenerator.MeetsQualityLimits(diagonalThin, easy, out _), "Thickness.DiagonalThinRejectedDespiteLargeAabb", true, false);
        }

        private static void ValidateHashNormalization(ValidationContext validation)
        {
            Phase3GridPoint[] canonical = { P(0, 0), P(6, 0), P(0, 6) };
            Phase3GridPoint[] shiftedStart = { P(6, 0), P(0, 6), P(0, 0) };
            Phase3GridPoint[] reversed = { P(0, 0), P(0, 6), P(6, 0) };
            Phase3GridPoint[] explicitlyClosed = { P(0, 0), P(6, 0), P(0, 6), P(0, 0) };
            var basePieces = new[] { HashPiece("a", "s-a", canonical), HashPiece("b", "s-b", new[] { P(8, 0), P(14, 0), P(8, 6) }) };
            string baseline = Phase3PuzzleGenerator.ComputeCanonicalHash(GameDifficulty.Easy, basePieces);
            validation.Equal(baseline, Phase3PuzzleGenerator.ComputeCanonicalHash(GameDifficulty.Easy,
                new[] { HashPiece("renamed", "renamed-slot", shiftedStart), basePieces[1] }), "Hash.StartVertexAndIdsIgnored");
            validation.Equal(baseline, Phase3PuzzleGenerator.ComputeCanonicalHash(GameDifficulty.Easy,
                new[] { HashPiece("a", "s-a", reversed), basePieces[1] }), "Hash.WindingIgnored");
            validation.Equal(baseline, Phase3PuzzleGenerator.ComputeCanonicalHash(GameDifficulty.Easy,
                new[] { HashPiece("a", "s-a", explicitlyClosed), basePieces[1] }), "Hash.ClosingVertexIgnored");
            validation.Equal(baseline, Phase3PuzzleGenerator.ComputeCanonicalHash(GameDifficulty.Easy,
                new[] { basePieces[1], basePieces[0] }), "Hash.PieceOrderIgnored");
            string changedGeometry = Phase3PuzzleGenerator.ComputeCanonicalHash(GameDifficulty.Easy,
                new[] { HashPiece("a", "s-a", new[] { P(0, 0), P(7, 0), P(0, 7) }), basePieces[1] });
            validation.Check(!string.Equals(baseline, changedGeometry, StringComparison.Ordinal), "Hash.GeometryChangeDetected", true, changedGeometry);
            string changedVersion = Phase3PuzzleGenerator.ComputeCanonicalHashForVersion(
                "phase3-geometric-partition-v3-contract-test", GameDifficulty.Easy, basePieces);
            validation.Check(!string.Equals(baseline, changedVersion, StringComparison.Ordinal), "Hash.VersionChangeDetected", true, changedVersion);
        }

        private static void ValidateDifficultyDiversity(ValidationContext validation)
        {
            Phase3PuzzleGenerationResult easy = Generate(44001L, GameDifficulty.Easy);
            Phase3PuzzleGenerationResult normal = Generate(44001L, GameDifficulty.Normal);
            Phase3PuzzleGenerationResult hard = Generate(44001L, GameDifficulty.Hard);
            validation.Check(easy.Succeeded && normal.Succeeded && hard.Succeeded, "Complexity.AllGenerate", true, $"{easy.Failure}/{normal.Failure}/{hard.Failure}");
            if (!easy.Succeeded || !normal.Succeeded || !hard.Succeeded) return;
            validation.Check(normal.Signature.InternalBoundaryLength >= easy.Signature.InternalBoundaryLength, "Complexity.NormalBoundaryAtLeastEasy", true, $"{easy.Signature.InternalBoundaryLength:R}/{normal.Signature.InternalBoundaryLength:R}");
            validation.Check(hard.Signature.InternalBoundaryLength >= normal.Signature.InternalBoundaryLength, "Complexity.HardBoundaryAtLeastNormal", true, $"{normal.Signature.InternalBoundaryLength:R}/{hard.Signature.InternalBoundaryLength:R}");
            validation.Check(CountUsedShapeKinds(hard) >= CountUsedShapeKinds(easy), "Complexity.HardShapeDiversityAtLeastEasy", true, $"{CountUsedShapeKinds(easy)}/{CountUsedShapeKinds(hard)}");
        }

        private static void ValidateSeedDiversity(ValidationContext validation)
        {
            foreach (GameDifficulty difficulty in new[] { GameDifficulty.Easy, GameDifficulty.Normal, GameDifficulty.Hard })
            {
                var hashes = new HashSet<string>(StringComparer.Ordinal);
                int successes = 0;
                for (int seed = 1; seed <= 20; seed++)
                {
                    Phase3PuzzleGenerationResult result = Generate(seed, difficulty);
                    if (!result.Succeeded) continue;
                    successes++;
                    hashes.Add(result.CanonicalHash);
                }
                validation.Equal(20, successes, $"Diversity.{difficulty}.SuccessfulSeeds");
                validation.Check(hashes.Count >= 8, $"Diversity.{difficulty}.AtLeastFortyPercentDistinct", ">=8", hashes.Count);
            }
        }

        private static void ValidateHistory(ValidationContext validation)
        {
            Phase3PuzzleGenerationResult initial = Generate(99173L, GameDifficulty.Normal);
            validation.Check(initial.Succeeded, "History.InitialGeneration", true, initial.FailureReason);
            if (!initial.Succeeded) return;
            Phase3PuzzleGenerationResult retry = Phase3PuzzleGenerator.Generate(new Phase3PuzzleGenerationRequest(
                99173L, GameDifficulty.Normal, new[] { initial.CanonicalHash }, Phase3PuzzleGenerator.DefaultMaximumAttempts));
            validation.Check(retry.Succeeded, "History.DuplicateRegenerated", true, retry.FailureReason);
            if (retry.Succeeded)
            {
                validation.Check(!string.Equals(initial.CanonicalHash, retry.CanonicalHash, StringComparison.OrdinalIgnoreCase), "History.NewHash", true, retry.CanonicalHash);
                validation.Equal(99173L, retry.RequestedSeed, "History.RequestedSeedPreserved");
                validation.Check(retry.AttemptIndex >= 1, "History.RetryAttemptIndex", ">=1", retry.AttemptIndex);
                validation.Check(retry.EffectiveSeed != initial.EffectiveSeed, "History.EffectiveSeedChanges", true, retry.EffectiveSeed);
                Phase3PuzzleGenerationResult replay = Phase3PuzzleGenerator.RegenerateCandidate(
                    retry.RequestedSeed, retry.Difficulty, retry.AttemptIndex);
                validation.Check(replay.Succeeded, "History.ReplaySucceeds", true, replay.FailureReason);
                validation.Equal(retry.EffectiveSeed, replay.EffectiveSeed, "History.ReplayEffectiveSeed");
                validation.Equal(retry.AttemptIndex, replay.AttemptIndex, "History.ReplayAttemptIndex");
                validation.Equal(retry.CanonicalHash, replay.CanonicalHash, "History.ReplayHash");
                validation.Equal(retry.Signature.Value, replay.Signature.Value, "History.ReplaySignature");
                validation.Check(EqualGeneratedPieces(retry.GeneratedPieces, replay.GeneratedPieces), "History.ReplayFullPieceData", true, false);
            }
            Phase3PuzzleGenerationResult bounded = Phase3PuzzleGenerator.Generate(new Phase3PuzzleGenerationRequest(
                99173L, GameDifficulty.Normal, new[] { initial.CanonicalHash }, 1));
            validation.Check(!bounded.Succeeded && bounded.Failure == Phase3PuzzleGenerationFailure.DuplicateHistoryExhausted, "History.BoundedFailure", Phase3PuzzleGenerationFailure.DuplicateHistoryExhausted, bounded.Failure);
            validation.Equal(1, bounded.AttemptsUsed, "History.SingleAttemptStops");
        }

        private static void ValidateFailureContract(ValidationContext validation)
        {
            Phase3PuzzleGenerationResult invalid = Phase3PuzzleGenerator.Generate(new Phase3PuzzleGenerationRequest(1L, GameDifficulty.Unspecified));
            validation.Check(!invalid.Succeeded && invalid.Failure == Phase3PuzzleGenerationFailure.InvalidDifficulty, "Failure.InvalidDifficulty", Phase3PuzzleGenerationFailure.InvalidDifficulty, invalid.Failure);
            validation.Check(!string.IsNullOrWhiteSpace(invalid.FailureReason), "Failure.ReasonPresent", true, invalid.FailureReason);
            validation.Throws<ArgumentOutOfRangeException>(() => new Phase3PuzzleGenerationRequest(1L, GameDifficulty.Easy, null, 0), "Failure.ZeroAttemptsRejected");
            validation.Throws<ArgumentOutOfRangeException>(() => new Phase3PuzzleGenerationRequest(1L, GameDifficulty.Easy, null, 65), "Failure.ExcessAttemptsRejected");
            validation.Throws<ArgumentOutOfRangeException>(() => Phase3PuzzleGenerator.RegenerateCandidate(1L, GameDifficulty.Easy, -1), "Failure.NegativeAttemptIndexRejected");
            validation.Throws<ArgumentOutOfRangeException>(() => Phase3PuzzleGenerator.RegenerateCandidate(1L, GameDifficulty.Easy, 64), "Failure.ExcessAttemptIndexRejected");
        }

        private static void ValidateQuality256(ValidationContext validation)
        {
            foreach (GameDifficulty difficulty in new[] { GameDifficulty.Easy, GameDifficulty.Normal, GameDifficulty.Hard })
            {
                QualitySummary summary = AuditQuality(difficulty, 256);
                Debug.Log(summary.ToLogLine());
                for (int sample = 0; sample < summary.RepresentativeStructures.Count; sample++)
                    Debug.Log($"[Phase3][P3-5][SAMPLE] difficulty={difficulty}, index={sample + 1}, vertices={summary.RepresentativeStructures[sample]}");
                validation.Equal(256, summary.SuccessCount, $"Quality256.{difficulty}.SuccessCount");
                validation.Equal(0, summary.FailureCount, $"Quality256.{difficulty}.FailureCount");
                validation.Equal(0, summary.ConvexityViolations, $"Quality256.{difficulty}.ConvexityViolations");
                validation.Equal(0, summary.SelfIntersectionViolations, $"Quality256.{difficulty}.SelfIntersectionViolations");
                validation.Equal(0, summary.OutsideFieldViolations, $"Quality256.{difficulty}.OutsideFieldViolations");
                validation.Equal(0, summary.OverlapViolations, $"Quality256.{difficulty}.OverlapViolations");
                validation.Equal(0, summary.ShapeKindViolations, $"Quality256.{difficulty}.ShapeKindViolations");
                validation.Equal(0, summary.MinimumAreaViolations, $"Quality256.{difficulty}.MinimumAreaViolations");
                validation.Equal(0, summary.MinimumThicknessViolations, $"Quality256.{difficulty}.MinimumThicknessViolations");
                validation.Equal(0, summary.MinimumAngleViolations, $"Quality256.{difficulty}.MinimumAngleViolations");
                validation.Check(summary.Hashes.Count >= 128, $"Quality256.{difficulty}.AtLeastHalfDistinctGeometry", ">=128", summary.Hashes.Count);
                validation.Check(summary.RepresentativeStructures.Count >= 3, $"Quality256.{difficulty}.ThreeRepresentativeStructures", ">=3", summary.RepresentativeStructures.Count);
            }
        }

        private static void ValidateSequentialHistory(ValidationContext validation)
        {
            foreach (GameDifficulty difficulty in new[] { GameDifficulty.Easy, GameDifficulty.Normal, GameDifficulty.Hard })
            {
                HistorySummary summary = AuditSequentialHistory(difficulty, 64);
                Debug.Log(summary.ToLogLine());
                validation.Equal(64, summary.SuccessCount, $"History64.{difficulty}.SuccessCount");
                validation.Equal(64, summary.UniqueHashes.Count, $"History64.{difficulty}.UniqueHashes");
                validation.Equal(0, summary.ExhaustedCount, $"History64.{difficulty}.ExhaustedCount");
                validation.Equal(0, summary.ReplayMismatchCount, $"History64.{difficulty}.ReplayMismatchCount");
                validation.Equal(0, summary.RequestedSeedMismatchCount, $"History64.{difficulty}.RequestedSeedMismatchCount");
            }
        }

        private static HistorySummary AuditSequentialHistory(GameDifficulty difficulty, int count)
        {
            var summary = new HistorySummary(difficulty);
            var history = new List<string>();
            for (int index = 0; index < count; index++)
            {
                long requestedSeed = 640000L + (long)(int)difficulty * 10000L + index;
                Phase3PuzzleGenerationResult result = Phase3PuzzleGenerator.Generate(new Phase3PuzzleGenerationRequest(
                    requestedSeed, difficulty, history, Phase3PuzzleGenerator.MaximumAllowedAttempts));
                if (!result.Succeeded)
                {
                    summary.ExhaustedCount++;
                    continue;
                }
                summary.SuccessCount++;
                summary.UniqueHashes.Add(result.CanonicalHash);
                history.Add(result.CanonicalHash);
                summary.MaximumAttemptIndex = Math.Max(summary.MaximumAttemptIndex, result.AttemptIndex);
                summary.AttemptIndexSum += result.AttemptIndex;
                if (result.RequestedSeed != requestedSeed) summary.RequestedSeedMismatchCount++;
                Phase3PuzzleGenerationResult replay = Phase3PuzzleGenerator.RegenerateCandidate(
                    result.RequestedSeed, result.Difficulty, result.AttemptIndex);
                if (!replay.Succeeded || replay.EffectiveSeed != result.EffectiveSeed ||
                    !string.Equals(replay.CanonicalHash, result.CanonicalHash, StringComparison.Ordinal) ||
                    !string.Equals(replay.Signature.Value, result.Signature.Value, StringComparison.Ordinal) ||
                    !EqualGeneratedPieces(replay.GeneratedPieces, result.GeneratedPieces)) summary.ReplayMismatchCount++;
            }
            return summary;
        }

        private static QualitySummary AuditQuality(GameDifficulty difficulty, int seedCount)
        {
            var summary = new QualitySummary(difficulty);
            Phase3PuzzleGeneratorDifficultyConfig config = Phase3PuzzleGeneratorDifficultyConfig.For(difficulty);
            for (int seed = 0; seed < seedCount; seed++)
            {
                Phase3PuzzleGenerationResult result = Generate(seed, difficulty);
                if (!result.Succeeded)
                {
                    summary.RecordFailure(result.Failure);
                    continue;
                }
                summary.SuccessCount++;
                bool newStructure = summary.Hashes.Add(result.CanonicalHash);
                summary.ShapeKindCombinations.Add(JoinIntegers(result.Signature.ShapeKindCounts));
                summary.AreaDistributions.Add(JoinDoubles(result.Signature.PieceAreas));
                if (newStructure && summary.RepresentativeStructures.Count < 3)
                    summary.RepresentativeStructures.Add(FormatVertices(result.GeneratedPieces));
                summary.AttemptIndexSum += result.AttemptIndex;
                summary.MaximumAttemptIndex = Math.Max(summary.MaximumAttemptIndex, result.AttemptIndex);
                for (int pieceIndex = 0; pieceIndex < result.GeneratedPieces.Count; pieceIndex++)
                {
                    Phase3GeneratedPieceData piece = result.GeneratedPieces[pieceIndex];
                    Phase3PolygonValidationResult polygon = Phase3Geometry.ValidatePolygon(piece.Vertices);
                    if (!polygon.IsValid || !Phase3Geometry.IsConvex(piece.Vertices)) summary.ConvexityViolations++;
                    if (Phase3Geometry.HasSelfIntersection(piece.Vertices)) summary.SelfIntersectionViolations++;
                    Phase3Bounds2D bounds = Phase3Geometry.GetBounds(piece.Vertices);
                    if (bounds.MinX < 0d || bounds.MinY < 0d || bounds.MaxX > Phase3CoreConstants.LogicalGridSize || bounds.MaxY > Phase3CoreConstants.LogicalGridSize)
                        summary.OutsideFieldViolations++;
                    if (polygon.IsValid)
                    {
                        double area = Phase3Geometry.AbsoluteArea(piece.Vertices);
                        double thickness = Phase3PuzzleGenerator.CalculateMinimumThickness(piece.Vertices);
                        double angle = Phase3PuzzleGenerator.CalculateMinimumInteriorAngleDegrees(piece.Vertices);
                        summary.MinimumObservedArea = Math.Min(summary.MinimumObservedArea, area);
                        summary.MinimumObservedThickness = Math.Min(summary.MinimumObservedThickness, thickness);
                        summary.MinimumObservedAngle = Math.Min(summary.MinimumObservedAngle, angle);
                        if (area < config.MinimumPieceArea - Phase3CoreConstants.ComparisonEpsilon) summary.MinimumAreaViolations++;
                        if (thickness < config.MinimumThickness - Phase3CoreConstants.ComparisonEpsilon) summary.MinimumThicknessViolations++;
                        if (angle < config.MinimumInteriorAngleDegrees - Phase3CoreConstants.ComparisonEpsilon) summary.MinimumAngleViolations++;
                        if (Phase3PuzzleGenerator.ClassifyShape(piece.Vertices) != piece.ShapeKind) summary.ShapeKindViolations++;
                    }
                    summary.ShapeKindCounts[(int)piece.ShapeKind]++;
                }
                Phase3PartitionValidationResult partition = Phase3PartitionValidator.Validate(result.Puzzle, difficulty, config.PartitionRules);
                if (partition.HasFailure(Phase3PartitionFailure.InteriorOverlap) || partition.HasFailure(Phase3PartitionFailure.ProperEdgeCrossing))
                    summary.OverlapViolations++;
            }
            return summary;
        }

        private static Phase3PuzzleGenerationResult Generate(long seed, GameDifficulty difficulty) =>
            Phase3PuzzleGenerator.Generate(new Phase3PuzzleGenerationRequest(seed, difficulty));

        private static string JoinIntegers(IReadOnlyList<int> values)
        {
            var text = new string[values.Count];
            for (int i = 0; i < values.Count; i++) text[i] = values[i].ToString(CultureInfo.InvariantCulture);
            return string.Join(",", text);
        }

        private static string JoinDoubles(IReadOnlyList<double> values)
        {
            var text = new string[values.Count];
            for (int i = 0; i < values.Count; i++) text[i] = values[i].ToString("R", CultureInfo.InvariantCulture);
            return string.Join(",", text);
        }

        private static string FormatVertices(IReadOnlyList<Phase3GeneratedPieceData> pieces)
        {
            var pieceText = new string[pieces.Count];
            for (int piece = 0; piece < pieces.Count; piece++)
            {
                var vertices = new string[pieces[piece].Vertices.Count];
                for (int vertex = 0; vertex < vertices.Length; vertex++)
                {
                    Phase3GridPoint point = pieces[piece].Vertices[vertex];
                    vertices[vertex] = $"{point.X},{point.Y}";
                }
                pieceText[piece] = $"[{string.Join(";", vertices)}]";
            }
            return string.Join("/", pieceText);
        }

        private static Phase3GeneratedPieceData HashPiece(string pieceId, string slotId, IEnumerable<Phase3GridPoint> vertices) =>
            new Phase3GeneratedPieceData(pieceId, slotId, vertices, Phase3GeneratedShapeKind.Triangle, 8,
                new Phase3RotationStep(0), new Phase3RotationStep(1));

        private static bool ContainsQuadrilateral(ISet<Phase3GeneratedShapeKind> kinds) =>
            kinds.Contains(Phase3GeneratedShapeKind.Quadrilateral) || kinds.Contains(Phase3GeneratedShapeKind.Rectangle) ||
            kinds.Contains(Phase3GeneratedShapeKind.Square) || kinds.Contains(Phase3GeneratedShapeKind.Parallelogram);

        private static int CountUsedShapeKinds(Phase3PuzzleGenerationResult result)
        {
            int count = 0;
            for (int i = 0; i < result.Signature.ShapeKindCounts.Count; i++) if (result.Signature.ShapeKindCounts[i] > 0) count++;
            return count;
        }

        private static Phase3PieceDefinition FindPiece(IReadOnlyList<Phase3PieceDefinition> pieces, string pieceId)
        {
            for (int i = 0; i < pieces.Count; i++) if (string.Equals(pieces[i].PieceId, pieceId, StringComparison.Ordinal)) return pieces[i];
            return null;
        }

        private static Phase3SlotDefinition FindSlot(IReadOnlyList<Phase3SlotDefinition> slots, string slotId)
        {
            for (int i = 0; i < slots.Count; i++) if (string.Equals(slots[i].SlotId, slotId, StringComparison.Ordinal)) return slots[i];
            return null;
        }

        private static bool EqualGeneratedPieces(IReadOnlyList<Phase3GeneratedPieceData> first, IReadOnlyList<Phase3GeneratedPieceData> second)
        {
            if (first.Count != second.Count) return false;
            for (int i = 0; i < first.Count; i++)
            {
                if (!string.Equals(first[i].PieceId, second[i].PieceId, StringComparison.Ordinal) ||
                    !string.Equals(first[i].SlotId, second[i].SlotId, StringComparison.Ordinal) ||
                    first[i].ShapeKind != second[i].ShapeKind ||
                    first[i].RotationalSymmetryPeriodSteps != second[i].RotationalSymmetryPeriodSteps ||
                    first[i].TargetRotation != second[i].TargetRotation || first[i].InitialRotation != second[i].InitialRotation ||
                    first[i].Vertices.Count != second[i].Vertices.Count) return false;
                for (int vertex = 0; vertex < first[i].Vertices.Count; vertex++) if (first[i].Vertices[vertex] != second[i].Vertices[vertex]) return false;
            }
            return true;
        }

        private static Phase3GridPoint P(int x, int y) => new Phase3GridPoint(x, y);

        private sealed class QualitySummary
        {
            private readonly Dictionary<Phase3PuzzleGenerationFailure, int> failureReasons = new Dictionary<Phase3PuzzleGenerationFailure, int>();
            public QualitySummary(GameDifficulty difficulty)
            {
                Difficulty = difficulty;
                MinimumObservedArea = double.PositiveInfinity;
                MinimumObservedThickness = double.PositiveInfinity;
                MinimumObservedAngle = double.PositiveInfinity;
                ShapeKindCounts = new int[Enum.GetValues(typeof(Phase3GeneratedShapeKind)).Length];
                Hashes = new HashSet<string>(StringComparer.Ordinal);
            }
            public GameDifficulty Difficulty { get; }
            public int SuccessCount { get; set; }
            public int FailureCount { get; private set; }
            public int ConvexityViolations { get; set; }
            public int SelfIntersectionViolations { get; set; }
            public int OutsideFieldViolations { get; set; }
            public int OverlapViolations { get; set; }
            public int ShapeKindViolations { get; set; }
            public int MinimumAreaViolations { get; set; }
            public int MinimumThicknessViolations { get; set; }
            public int MinimumAngleViolations { get; set; }
            public double MinimumObservedArea { get; set; }
            public double MinimumObservedThickness { get; set; }
            public double MinimumObservedAngle { get; set; }
            public int MaximumAttemptIndex { get; set; }
            public long AttemptIndexSum { get; set; }
            public int[] ShapeKindCounts { get; }
            public HashSet<string> Hashes { get; }
            public HashSet<string> ShapeKindCombinations { get; } = new HashSet<string>(StringComparer.Ordinal);
            public HashSet<string> AreaDistributions { get; } = new HashSet<string>(StringComparer.Ordinal);
            public List<string> RepresentativeStructures { get; } = new List<string>();
            public void RecordFailure(Phase3PuzzleGenerationFailure failure)
            {
                FailureCount++;
                failureReasons.TryGetValue(failure, out int count);
                failureReasons[failure] = count + 1;
            }
            public string ToLogLine()
            {
                var reasons = new List<string>();
                foreach (KeyValuePair<Phase3PuzzleGenerationFailure, int> pair in failureReasons) reasons.Add($"{pair.Key}:{pair.Value}");
                string shapes = string.Join(",", ShapeKindCounts);
                double averageAttempt = SuccessCount == 0 ? 0d : (double)AttemptIndexSum / SuccessCount;
                return $"[Phase3][P3-5][QUALITY256] difficulty={Difficulty}, success={SuccessCount}, failure={FailureCount}, failureReasons={string.Join(";", reasons)}, minArea={MinimumObservedArea:R}, minThickness={MinimumObservedThickness:R}, minAngle={MinimumObservedAngle:R}, convex={ConvexityViolations}, selfIntersection={SelfIntersectionViolations}, outside={OutsideFieldViolations}, overlap={OverlapViolations}, shapeKind={ShapeKindViolations}, areaLimit={MinimumAreaViolations}, thicknessLimit={MinimumThicknessViolations}, angleLimit={MinimumAngleViolations}, shapeCounts={shapes}, shapeCombinations={ShapeKindCombinations.Count}, areaDistributions={AreaDistributions.Count}, uniqueHashes={Hashes.Count}, duplicateHashes={SuccessCount - Hashes.Count}, uniqueRatio={(SuccessCount == 0 ? 0d : (double)Hashes.Count / SuccessCount):R}, maxAttemptIndex={MaximumAttemptIndex}, averageAttemptIndex={averageAttempt:R}, exhausted={FailureCount}";
            }
        }

        private sealed class HistorySummary
        {
            public HistorySummary(GameDifficulty difficulty)
            {
                Difficulty = difficulty;
                UniqueHashes = new HashSet<string>(StringComparer.Ordinal);
            }
            public GameDifficulty Difficulty { get; }
            public int SuccessCount { get; set; }
            public int ExhaustedCount { get; set; }
            public int ReplayMismatchCount { get; set; }
            public int RequestedSeedMismatchCount { get; set; }
            public int MaximumAttemptIndex { get; set; }
            public long AttemptIndexSum { get; set; }
            public HashSet<string> UniqueHashes { get; }
            public string ToLogLine()
            {
                double averageAttempt = SuccessCount == 0 ? 0d : (double)AttemptIndexSum / SuccessCount;
                return $"[Phase3][P3-5][HISTORY64] difficulty={Difficulty}, success={SuccessCount}, unique={UniqueHashes.Count}, exhausted={ExhaustedCount}, replayMismatch={ReplayMismatchCount}, requestedSeedMismatch={RequestedSeedMismatchCount}, maxAttemptIndex={MaximumAttemptIndex}, averageAttemptIndex={averageAttempt:R}";
            }
        }

        private sealed class ValidationContext
        {
            private readonly List<string> failures = new List<string>();
            public int Passed { get; private set; }
            public int Total { get; private set; }
            public IReadOnlyList<string> Failures => failures;
            public void RunSection(string section, Action action)
            {
                try { action(); }
                catch (Exception exception)
                {
                    Total++;
                    failures.Add($"[Phase3][P3-5][FAIL] section={section}, unexpected={exception.GetType().FullName}, message={exception.Message}");
                }
            }
            public void Check(bool condition, string name, object expected, object actual)
            {
                Total++;
                if (condition) { Passed++; return; }
                failures.Add($"[Phase3][P3-5][FAIL] assertion={name}, expected={expected}, actual={actual}");
            }
            public void Equal<T>(T expected, T actual, string name) => Check(EqualityComparer<T>.Default.Equals(expected, actual), name, expected, actual);
            public void Near(double expected, double actual, string name) => Check(Math.Abs(expected - actual) <= Phase3CoreConstants.ComparisonEpsilon, name, expected, actual);
            public void Throws<TException>(Action action, string name) where TException : Exception
            {
                Total++;
                try { action(); failures.Add($"[Phase3][P3-5][FAIL] assertion={name}, expectedException={typeof(TException).FullName}, actual=no exception"); }
                catch (TException) { Passed++; }
                catch (Exception exception) { failures.Add($"[Phase3][P3-5][FAIL] assertion={name}, expectedException={typeof(TException).FullName}, actualException={exception.GetType().FullName}"); }
            }
        }
    }
}
#endif
