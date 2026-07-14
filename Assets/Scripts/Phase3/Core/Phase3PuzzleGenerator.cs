using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using HATAGONG.GameFlow;

namespace HATAGONG.Phase3
{
    public static class Phase3PuzzleGenerator
    {
        public const string GeneratorVersion = "phase3-geometric-partition-v3";
        public const int DefaultMaximumAttempts = 16;
        public const int MaximumAllowedAttempts = 64;

        public static string ComputeCanonicalHash(GameDifficulty difficulty, IEnumerable<Phase3GeneratedPieceData> generatedPieces)
            => ComputeCanonicalHashForVersion(GeneratorVersion, difficulty, generatedPieces);

        public static string ComputeCanonicalHashForVersion(
            string generatorVersion,
            GameDifficulty difficulty,
            IEnumerable<Phase3GeneratedPieceData> generatedPieces)
        {
            if (string.IsNullOrWhiteSpace(generatorVersion)) throw new ArgumentException("Generator version is required.", nameof(generatorVersion));
            Phase3PuzzleGeneratorDifficultyConfig.For(difficulty);
            if (generatedPieces == null) throw new ArgumentNullException(nameof(generatedPieces));
            var pieces = new List<Phase3GeneratedPieceData>(generatedPieces);
            if (pieces.Count == 0 || pieces.Exists(piece => piece == null))
                throw new ArgumentException("Canonical hashing requires non-null generated pieces.", nameof(generatedPieces));
            return ComputeSha256(BuildCanonicalStructureText(generatorVersion, difficulty, pieces));
        }

        public static Phase3PuzzleGenerationResult Generate(Phase3PuzzleGenerationRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            Phase3PuzzleGeneratorDifficultyConfig config;
            try
            {
                config = Phase3PuzzleGeneratorDifficultyConfig.For(request.Difficulty);
            }
            catch (ArgumentOutOfRangeException)
            {
                return Phase3PuzzleGenerationResult.Failed(
                    Phase3PuzzleGenerationFailure.InvalidDifficulty,
                    "Difficulty must be Easy, Normal, or Hard.",
                    request.Seed,
                    request.Difficulty,
                    0);
            }

            var recentHashes = new HashSet<string>(request.RecentCanonicalHashes, StringComparer.OrdinalIgnoreCase);
            string lastInvariantFailure = string.Empty;
            bool rejectedDuplicate = false;
            for (int attempt = 0; attempt < request.MaximumAttempts; attempt++)
            {
                ulong effectiveSeed = CreateAttemptSeed(request.Seed, request.Difficulty, attempt);
                var random = new DeterministicRandom(effectiveSeed);
                List<Region> regions = BuildRegions(request.Difficulty, random);
                if (!TryBuildCandidate(config, regions, random, out Candidate candidate, out lastInvariantFailure)) continue;

                if (recentHashes.Contains(candidate.CanonicalHash))
                {
                    rejectedDuplicate = true;
                    continue;
                }

                return Phase3PuzzleGenerationResult.Success(
                    request.Seed,
                    effectiveSeed,
                    attempt,
                    request.Difficulty,
                    attempt + 1,
                    candidate.PuzzleId,
                    candidate.CanonicalHash,
                    candidate.Signature,
                    candidate.Puzzle,
                    candidate.GeneratedPieces,
                    candidate.InitialRotations);
            }

            Phase3PuzzleGenerationFailure failure = rejectedDuplicate && string.IsNullOrEmpty(lastInvariantFailure)
                ? Phase3PuzzleGenerationFailure.DuplicateHistoryExhausted
                : Phase3PuzzleGenerationFailure.AttemptsExhausted;
            string reason = failure == Phase3PuzzleGenerationFailure.DuplicateHistoryExhausted
                ? $"All {request.MaximumAttempts} deterministic candidates matched recent canonical hashes."
                : $"No valid puzzle was produced in {request.MaximumAttempts} attempts. Last invariant failure: {lastInvariantFailure}";
            return Phase3PuzzleGenerationResult.Failed(failure, reason, request.Seed, request.Difficulty, request.MaximumAttempts);
        }

        public static Phase3PuzzleGenerationResult RegenerateCandidate(long requestedSeed, GameDifficulty difficulty, int attemptIndex)
        {
            if (attemptIndex < 0 || attemptIndex >= MaximumAllowedAttempts)
                throw new ArgumentOutOfRangeException(nameof(attemptIndex), attemptIndex, "Attempt index must be between 0 and 63.");
            Phase3PuzzleGeneratorDifficultyConfig config = Phase3PuzzleGeneratorDifficultyConfig.For(difficulty);
            ulong effectiveSeed = CreateAttemptSeed(requestedSeed, difficulty, attemptIndex);
            var random = new DeterministicRandom(effectiveSeed);
            List<Region> regions = BuildRegions(difficulty, random);
            if (!TryBuildCandidate(config, regions, random, out Candidate candidate, out string failureReason))
                return Phase3PuzzleGenerationResult.Failed(
                    Phase3PuzzleGenerationFailure.AttemptsExhausted,
                    $"Deterministic candidate at attempt index {attemptIndex} failed invariants: {failureReason}",
                    requestedSeed,
                    difficulty,
                    attemptIndex + 1);

            return Phase3PuzzleGenerationResult.Success(
                requestedSeed,
                effectiveSeed,
                attemptIndex,
                difficulty,
                attemptIndex + 1,
                candidate.PuzzleId,
                candidate.CanonicalHash,
                candidate.Signature,
                candidate.Puzzle,
                candidate.GeneratedPieces,
                candidate.InitialRotations);
        }

        private static List<Region> BuildRegions(GameDifficulty difficulty, DeterministicRandom random)
        {
            var regions = new List<Region>();
            Phase3PuzzleGeneratorDifficultyConfig config = Phase3PuzzleGeneratorDifficultyConfig.For(difficulty);
            int geometricHeight;
            int minimumRowHeight;
            int firstRowCount;
            int secondRowCount;
            int firstSplitCount;
            int secondSplitCount;
            switch (difficulty)
            {
                case GameDifficulty.Easy:
                    geometricHeight = 6 + random.NextInt(2);
                    minimumRowHeight = 4;
                    firstRowCount = 2;
                    secondRowCount = 3;
                    firstSplitCount = 0;
                    secondSplitCount = 0;
                    break;
                case GameDifficulty.Normal:
                    geometricHeight = 5 + random.NextInt(2);
                    minimumRowHeight = 5;
                    firstRowCount = 3;
                    secondRowCount = 3;
                    firstSplitCount = 0;
                    secondSplitCount = 1;
                    break;
                case GameDifficulty.Hard:
                    geometricHeight = 5 + random.NextInt(2);
                    minimumRowHeight = 5;
                    firstRowCount = 3;
                    secondRowCount = 3;
                    firstSplitCount = 1;
                    secondSplitCount = 2;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(difficulty));
            }

            int remainingHeight = Phase3CoreConstants.LogicalGridSize - geometricHeight;
            int firstRowHeight = minimumRowHeight + random.NextInt(remainingHeight - minimumRowHeight * 2 + 1);
            int secondRowHeight = remainingHeight - firstRowHeight;
            if (random.NextInt(2) == 1)
            {
                Swap(ref firstRowCount, ref secondRowCount);
                Swap(ref firstSplitCount, ref secondSplitCount);
            }

            int geometricBandIndex = random.NextInt(3);
            int rowIndex = 0;
            int y = 0;
            for (int bandIndex = 0; bandIndex < 3; bandIndex++)
            {
                if (bandIndex == geometricBandIndex)
                {
                    AddGeometricBand(regions, y, geometricHeight, config, random);
                    y += geometricHeight;
                    continue;
                }

                bool firstRow = rowIndex++ == 0;
                int height = firstRow ? firstRowHeight : secondRowHeight;
                int count = firstRow ? firstRowCount : secondRowCount;
                int splitCount = firstRow ? firstSplitCount : secondSplitCount;
                AddRectangularRow(regions, y, height, CreateRowWidths(count, height, splitCount, config, random), splitCount, random);
                y += height;
            }

            bool mirrorX = random.NextInt(2) == 1;
            bool mirrorY = random.NextInt(2) == 1;
            if (mirrorX || mirrorY)
            {
                for (int i = 0; i < regions.Count; i++) regions[i] = regions[i].Transform(mirrorX, mirrorY);
            }
            regions.Sort();
            return regions;
        }

        private static void AddGeometricBand(
            ICollection<Region> regions,
            int y,
            int height,
            Phase3PuzzleGeneratorDifficultyConfig config,
            DeterministicRandom random)
        {
            int minimumEnd = (int)Math.Ceiling(Math.Max(
                height + Math.Max(config.MinimumPieceArea / height, config.MinimumThickness * Math.Sqrt(2d)),
                Phase3CoreConstants.LogicalGridSize + height - config.PartitionRules.MaximumAspectRatio * height));
            int maximumEnd = (int)Math.Floor(Math.Min(
                Phase3CoreConstants.LogicalGridSize - 1d,
                config.PartitionRules.MaximumAspectRatio * height));
            int topRightStart = minimumEnd + random.NextInt(maximumEnd - minimumEnd + 1);
            regions.Add(new Region(new[]
            {
                new Phase3GridPoint(0, y), new Phase3GridPoint(height, y), new Phase3GridPoint(0, y + height)
            }));
            regions.Add(new Region(new[]
            {
                new Phase3GridPoint(height, y), new Phase3GridPoint(topRightStart, y),
                new Phase3GridPoint(topRightStart - height, y + height), new Phase3GridPoint(0, y + height)
            }));
            regions.Add(new Region(new[]
            {
                new Phase3GridPoint(topRightStart, y), new Phase3GridPoint(Phase3CoreConstants.LogicalGridSize, y),
                new Phase3GridPoint(Phase3CoreConstants.LogicalGridSize, y + height), new Phase3GridPoint(topRightStart - height, y + height)
            }));
        }

        private static int[] CreateRowWidths(
            int count,
            int height,
            int splitCount,
            Phase3PuzzleGeneratorDifficultyConfig config,
            DeterministicRandom random)
        {
            int minimumWidth = Math.Max(
                (int)Math.Ceiling(config.MinimumThickness),
                (int)Math.Ceiling(config.MinimumPieceArea / height));
            int maximumWidth = (int)Math.Floor(Math.Min(
                config.PartitionRules.MaximumAspectRatio * height,
                config.PartitionRules.MaximumPieceAreaRatio * Phase3CoreConstants.LogicalFieldArea / height));
            var widths = new int[count];
            for (int i = 0; i < widths.Length; i++) widths[i] = minimumWidth;
            for (int i = 0; i < splitCount; i++) widths[i] = Math.Max(widths[i], height);

            int assigned = 0;
            for (int i = 0; i < widths.Length; i++) assigned += widths[i];
            int remaining = Phase3CoreConstants.LogicalGridSize - assigned;
            if (remaining < 0) throw new InvalidOperationException("The selected row cannot satisfy the generator quality limits.");
            for (int unit = 0; unit < remaining; unit++)
            {
                int start = random.NextInt(widths.Length);
                int selected = -1;
                for (int offset = 0; offset < widths.Length; offset++)
                {
                    int candidate = (start + offset) % widths.Length;
                    if (widths[candidate] >= maximumWidth) continue;
                    selected = candidate;
                    break;
                }
                if (selected < 0) throw new InvalidOperationException("The selected row exceeds its bounded aspect-ratio capacity.");
                widths[selected]++;
            }
            Shuffle(widths, random);
            return widths;
        }

        private static void Shuffle(int[] values, DeterministicRandom random)
        {
            for (int i = values.Length - 1; i > 0; i--)
            {
                int swap = random.NextInt(i + 1);
                int temporary = values[i];
                values[i] = values[swap];
                values[swap] = temporary;
            }
        }

        private static void Swap(ref int first, ref int second)
        {
            int temporary = first;
            first = second;
            second = temporary;
        }

        private static void AddRectangularRow(
            ICollection<Region> regions,
            int y,
            int height,
            IReadOnlyList<int> widths,
            int splitCount,
            DeterministicRandom random)
        {
            var splitIndices = new HashSet<int>();
            for (int split = 0; split < splitCount; split++)
            {
                int selected = -1;
                for (int i = 0; i < widths.Count; i++)
                {
                    if (splitIndices.Contains(i) || widths[i] < height) continue;
                    if (selected < 0 || widths[i] > widths[selected] ||
                        (widths[i] == widths[selected] && random.NextInt(2) == 1)) selected = i;
                }
                if (selected < 0) throw new InvalidOperationException("A diagonal split requires enough width for a 45-degree edge.");
                splitIndices.Add(selected);
            }

            int x = 0;
            for (int i = 0; i < widths.Count; i++)
            {
                int width = widths[i];
                if (!splitIndices.Contains(i))
                {
                    regions.Add(Rectangle(x, y, width, height));
                }
                else if (width == height && random.NextInt(2) == 0)
                {
                    regions.Add(new Region(new[] { P(x, y), P(x + width, y), P(x + width, y + height) }));
                    regions.Add(new Region(new[] { P(x, y), P(x + width, y + height), P(x, y + height) }));
                }
                else if (width == height)
                {
                    regions.Add(new Region(new[] { P(x, y), P(x + width, y), P(x, y + height) }));
                    regions.Add(new Region(new[] { P(x + width, y), P(x + width, y + height), P(x, y + height) }));
                }
                else if (random.NextInt(2) == 0)
                {
                    regions.Add(new Region(new[] { P(x, y), P(x + height, y + height), P(x, y + height) }));
                    regions.Add(new Region(new[] { P(x, y), P(x + width, y), P(x + width, y + height), P(x + height, y + height) }));
                }
                else
                {
                    int cutX = x + width - height;
                    regions.Add(new Region(new[] { P(cutX, y), P(x + width, y), P(x + width, y + height) }));
                    regions.Add(new Region(new[] { P(x, y), P(cutX, y), P(x + width, y + height), P(x, y + height) }));
                }
                x += width;
            }
        }

        private static Region Rectangle(int x, int y, int width, int height) => new Region(new[]
        {
            P(x, y), P(x + width, y), P(x + width, y + height), P(x, y + height)
        });

        private static bool TryBuildCandidate(
            Phase3PuzzleGeneratorDifficultyConfig config,
            List<Region> regions,
            DeterministicRandom random,
            out Candidate candidate,
            out string failureReason)
        {
            if (regions.Count != config.PieceCount)
            {
                candidate = default;
                failureReason = $"Expected {config.PieceCount} regions but built {regions.Count}.";
                return false;
            }

            var pieces = new List<Phase3PieceDefinition>(regions.Count);
            var slots = new List<Phase3SlotDefinition>(regions.Count);
            var generated = new List<Phase3GeneratedPieceData>(regions.Count);
            var initialRotations = new List<Phase3InitialPieceRotation>(regions.Count);
            for (int i = 0; i < regions.Count; i++)
            {
                Region region = regions[i];
                string difficultyName = config.Difficulty.ToString().ToLowerInvariant();
                string pieceId = $"p3-{difficultyName}-piece-{i + 1:D2}";
                string slotId = $"p3-{difficultyName}-slot-{i + 1:D2}";
                string deckSlotId = $"p3-{difficultyName}-deck-{i + 1:D2}";
                int symmetryPeriod = DetermineRotationalSymmetryPeriod(region.Vertices);
                Phase3GeneratedShapeKind shapeKind = ClassifyShape(region.Vertices);
                var shape = new Phase3ShapeDefinition($"{pieceId}-shape", region.Vertices, symmetryPeriod);
                var targetRotation = new Phase3RotationStep(0);
                Phase3RotationStep initialRotation = CreateInitialRotation(random, targetRotation, symmetryPeriod);
                Phase3Point2D canonicalCentroid = shape.Centroid * (Phase3CoreConstants.CanvasFieldSize / (double)Phase3CoreConstants.LogicalGridSize);

                slots.Add(new Phase3SlotDefinition(slotId, shape, canonicalCentroid, targetRotation));
                pieces.Add(new Phase3PieceDefinition(pieceId, deckSlotId, shape, new[] { new Phase3AllowedTarget(slotId, targetRotation) }));
                generated.Add(new Phase3GeneratedPieceData(pieceId, slotId, region.Vertices, shapeKind, symmetryPeriod, targetRotation, initialRotation));
                initialRotations.Add(new Phase3InitialPieceRotation(pieceId, initialRotation));
            }

            Phase3PuzzleDefinition puzzle;
            try
            {
                puzzle = new Phase3PuzzleDefinition(pieces, slots);
            }
            catch (Exception exception)
            {
                candidate = default;
                failureReason = $"Definition construction failed: {exception.Message}";
                return false;
            }

            if (!ValidateGeneratedData(puzzle, generated, config, out failureReason))
            {
                candidate = default;
                return false;
            }

            string canonicalHash = ComputeCanonicalHash(config.Difficulty, generated);
            Phase3PuzzleStructureSignature signature = BuildSignature(config.Difficulty, generated);
            string puzzleId = $"p3-{config.Difficulty.ToString().ToLowerInvariant()}-{canonicalHash.Substring(0, 16)}";
            candidate = new Candidate(puzzleId, canonicalHash, signature, puzzle, generated, initialRotations);
            return true;
        }

        private static bool ValidateGeneratedData(
            Phase3PuzzleDefinition puzzle,
            IReadOnlyList<Phase3GeneratedPieceData> generated,
            Phase3PuzzleGeneratorDifficultyConfig config,
            out string failureReason)
        {
            var pieceIds = new HashSet<string>(StringComparer.Ordinal);
            var slotIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < generated.Count; i++)
            {
                Phase3GeneratedPieceData piece = generated[i];
                if (!pieceIds.Add(piece.PieceId) || !slotIds.Add(piece.SlotId))
                {
                    failureReason = "Generated Piece and Slot IDs must be unique.";
                    return false;
                }
                Phase3PolygonValidationResult polygon = Phase3Geometry.ValidatePolygon(piece.Vertices);
                if (!polygon.IsValid || !Phase3Geometry.IsConvex(piece.Vertices))
                {
                    failureReason = $"Piece '{piece.PieceId}' has invalid connected polygon geometry: {polygon.Message}";
                    return false;
                }
                if (!MeetsQualityLimits(piece.Vertices, config, out string qualityFailure))
                {
                    failureReason = $"Piece '{piece.PieceId}' failed quality limits: {qualityFailure}";
                    return false;
                }
                if (ClassifyShape(piece.Vertices) != piece.ShapeKind)
                {
                    failureReason = $"Piece '{piece.PieceId}' has an unsupported or inconsistent shape classification.";
                    return false;
                }
                if (piece.InitialRotation.IsEquivalentTo(piece.TargetRotation, piece.RotationalSymmetryPeriodSteps))
                {
                    failureReason = $"Piece '{piece.PieceId}' starts in a rotation visually equivalent to its target.";
                    return false;
                }
            }

            Phase3PartitionValidationResult validation = Phase3PartitionValidator.Validate(puzzle, config.Difficulty, config.PartitionRules);
            if (!validation.IsValid)
            {
                failureReason = validation.Issues[0].ToString();
                return false;
            }
            if (Math.Abs(validation.PieceAreaSum - Phase3CoreConstants.LogicalFieldArea) > Phase3CoreConstants.ComparisonEpsilon)
            {
                failureReason = $"Piece area sum {validation.PieceAreaSum:R} does not equal {Phase3CoreConstants.LogicalFieldArea}.";
                return false;
            }
            failureReason = string.Empty;
            return true;
        }

        public static bool MeetsQualityLimits(
            IReadOnlyList<Phase3GridPoint> vertices,
            Phase3PuzzleGeneratorDifficultyConfig config,
            out string failureReason)
        {
            if (vertices == null) throw new ArgumentNullException(nameof(vertices));
            if (config == null) throw new ArgumentNullException(nameof(config));
            double area = Phase3Geometry.AbsoluteArea(vertices);
            if (area < config.MinimumPieceArea - Phase3CoreConstants.ComparisonEpsilon)
            {
                failureReason = $"Area {area:R} is below {config.MinimumPieceArea:R}.";
                return false;
            }
            double minimumThickness = CalculateMinimumThickness(vertices);
            if (minimumThickness < config.MinimumThickness - Phase3CoreConstants.ComparisonEpsilon)
            {
                failureReason = $"Minimum support-line thickness {minimumThickness:R} is below {config.MinimumThickness:R}.";
                return false;
            }
            double minimumAngle = CalculateMinimumInteriorAngleDegrees(vertices);
            if (minimumAngle < config.MinimumInteriorAngleDegrees - Phase3CoreConstants.ComparisonEpsilon)
            {
                failureReason = $"Interior angle {minimumAngle:R} is below {config.MinimumInteriorAngleDegrees:R}.";
                return false;
            }
            failureReason = string.Empty;
            return true;
        }

        public static Phase3GeneratedShapeKind ClassifyShape(IReadOnlyList<Phase3GridPoint> vertices)
        {
            Phase3PolygonValidationResult validation = Phase3Geometry.ValidatePolygon(vertices);
            if (!validation.IsValid) throw new ArgumentException(validation.Message, nameof(vertices));
            if (vertices.Count == 3) return Phase3GeneratedShapeKind.Triangle;
            if (vertices.Count != 4) throw new ArgumentException("The generator catalog supports only triangles and quadrilaterals.", nameof(vertices));

            bool parallelogram = vertices[0].X + vertices[2].X == vertices[1].X + vertices[3].X &&
                                 vertices[0].Y + vertices[2].Y == vertices[1].Y + vertices[3].Y;
            if (!parallelogram) return Phase3GeneratedShapeKind.Quadrilateral;
            long firstX = (long)vertices[1].X - vertices[0].X;
            long firstY = (long)vertices[1].Y - vertices[0].Y;
            long secondX = (long)vertices[2].X - vertices[1].X;
            long secondY = (long)vertices[2].Y - vertices[1].Y;
            bool rightAngle = firstX * secondX + firstY * secondY == 0L;
            if (!rightAngle) return Phase3GeneratedShapeKind.Parallelogram;
            long firstLength = firstX * firstX + firstY * firstY;
            long secondLength = secondX * secondX + secondY * secondY;
            return firstLength == secondLength ? Phase3GeneratedShapeKind.Square : Phase3GeneratedShapeKind.Rectangle;
        }

        public static int DetermineRotationalSymmetryPeriod(IReadOnlyList<Phase3GridPoint> vertices)
        {
            if (!Phase3Geometry.TryGetCentroid(vertices, out Phase3Point2D centroid))
                throw new ArgumentException("A valid polygon centroid is required.", nameof(vertices));
            for (int step = 1; step <= Phase3CoreConstants.FullRotationStepCount; step++)
            {
                if (MatchesAfterRotation(vertices, centroid, step) && Phase3RotationStep.IsValidSymmetryPeriod(step)) return step;
            }
            return Phase3CoreConstants.FullRotationStepCount;
        }

        private static bool MatchesAfterRotation(IReadOnlyList<Phase3GridPoint> vertices, Phase3Point2D centroid, int step)
        {
            double radians = -step * Phase3CoreConstants.RotationStepDegrees * Math.PI / 180d;
            double cosine = Math.Cos(radians);
            double sine = Math.Sin(radians);
            for (int i = 0; i < vertices.Count; i++)
            {
                double x = vertices[i].X - centroid.X;
                double y = vertices[i].Y - centroid.Y;
                double rotatedX = x * cosine - y * sine + centroid.X;
                double rotatedY = x * sine + y * cosine + centroid.Y;
                bool matched = false;
                for (int candidate = 0; candidate < vertices.Count; candidate++)
                {
                    if (Math.Abs(vertices[candidate].X - rotatedX) <= Phase3CoreConstants.ComparisonEpsilon &&
                        Math.Abs(vertices[candidate].Y - rotatedY) <= Phase3CoreConstants.ComparisonEpsilon)
                    {
                        matched = true;
                        break;
                    }
                }
                if (!matched) return false;
            }
            return true;
        }

        private static Phase3RotationStep CreateInitialRotation(DeterministicRandom random, Phase3RotationStep target, int symmetryPeriod)
        {
            int firstStep = random.NextInt(Phase3CoreConstants.FullRotationStepCount);
            for (int offset = 0; offset < Phase3CoreConstants.FullRotationStepCount; offset++)
            {
                var candidate = new Phase3RotationStep(firstStep + offset);
                if (!candidate.IsEquivalentTo(target, symmetryPeriod)) return candidate;
            }
            throw new InvalidOperationException("No non-equivalent initial rotation exists for the generated shape.");
        }

        public static double CalculateMinimumThickness(IReadOnlyList<Phase3GridPoint> vertices)
        {
            Phase3PolygonValidationResult validation = Phase3Geometry.ValidatePolygon(vertices);
            if (!validation.IsValid) throw new ArgumentException(validation.Message, nameof(vertices));
            double minimum = double.PositiveInfinity;
            for (int edge = 0; edge < vertices.Count; edge++)
            {
                Phase3GridPoint start = vertices[edge];
                Phase3GridPoint end = vertices[(edge + 1) % vertices.Count];
                double dx = end.X - start.X;
                double dy = end.Y - start.Y;
                double length = Math.Sqrt(dx * dx + dy * dy);
                double normalX = -dy / length;
                double normalY = dx / length;
                double minimumProjection = double.PositiveInfinity;
                double maximumProjection = double.NegativeInfinity;
                for (int vertex = 0; vertex < vertices.Count; vertex++)
                {
                    double projection = vertices[vertex].X * normalX + vertices[vertex].Y * normalY;
                    minimumProjection = Math.Min(minimumProjection, projection);
                    maximumProjection = Math.Max(maximumProjection, projection);
                }
                minimum = Math.Min(minimum, maximumProjection - minimumProjection);
            }
            return minimum;
        }

        public static double CalculateMinimumInteriorAngleDegrees(IReadOnlyList<Phase3GridPoint> vertices)
        {
            Phase3PolygonValidationResult validation = Phase3Geometry.ValidatePolygon(vertices);
            if (!validation.IsValid) throw new ArgumentException(validation.Message, nameof(vertices));
            double minimum = 180d;
            for (int i = 0; i < vertices.Count; i++)
            {
                Phase3GridPoint previous = vertices[(i - 1 + vertices.Count) % vertices.Count];
                Phase3GridPoint current = vertices[i];
                Phase3GridPoint next = vertices[(i + 1) % vertices.Count];
                double ax = previous.X - current.X;
                double ay = previous.Y - current.Y;
                double bx = next.X - current.X;
                double by = next.Y - current.Y;
                double denominator = Math.Sqrt((ax * ax + ay * ay) * (bx * bx + by * by));
                if (denominator <= Phase3CoreConstants.ComparisonEpsilon) return 0d;
                double cosine = Math.Max(-1d, Math.Min(1d, (ax * bx + ay * by) / denominator));
                minimum = Math.Min(minimum, Math.Acos(cosine) * 180d / Math.PI);
            }
            return minimum;
        }

        private static string BuildCanonicalStructureText(string generatorVersion, GameDifficulty difficulty, IReadOnlyList<Phase3GeneratedPieceData> pieces)
        {
            var descriptors = new List<string>(pieces.Count);
            for (int i = 0; i < pieces.Count; i++)
            {
                var builder = new StringBuilder();
                for (int vertex = 0; vertex < pieces[i].Vertices.Count; vertex++)
                {
                    Phase3GridPoint point = pieces[i].Vertices[vertex];
                    builder.Append(point.X.ToString(CultureInfo.InvariantCulture)).Append(',')
                        .Append(point.Y.ToString(CultureInfo.InvariantCulture)).Append(';');
                }
                builder.Append("r:").Append(pieces[i].TargetRotation.Value.ToString(CultureInfo.InvariantCulture));
                descriptors.Add(builder.ToString());
            }
            descriptors.Sort(StringComparer.Ordinal);
            return $"{generatorVersion}|grid:{Phase3CoreConstants.LogicalGridSize}|difficulty:{(int)difficulty}|{string.Join("|", descriptors)}";
        }

        private static Phase3PuzzleStructureSignature BuildSignature(GameDifficulty difficulty, IReadOnlyList<Phase3GeneratedPieceData> pieces)
        {
            var areas = new List<double>(pieces.Count);
            var perimeters = new List<double>(pieces.Count);
            int[] kinds = new int[Enum.GetValues(typeof(Phase3GeneratedShapeKind)).Length];
            double perimeterSum = 0d;
            for (int i = 0; i < pieces.Count; i++)
            {
                double perimeter = PolygonPerimeter(pieces[i].Vertices);
                areas.Add(pieces[i].Area);
                perimeters.Add(perimeter);
                perimeterSum += perimeter;
                kinds[(int)pieces[i].ShapeKind]++;
            }
            double outerPerimeter = Phase3CoreConstants.LogicalGridSize * 4d;
            return new Phase3PuzzleStructureSignature(difficulty, pieces.Count, areas, perimeters, (perimeterSum - outerPerimeter) * 0.5d, kinds);
        }

        private static double PolygonPerimeter(IReadOnlyList<Phase3GridPoint> vertices)
        {
            double perimeter = 0d;
            for (int i = 0; i < vertices.Count; i++)
            {
                Phase3GridPoint current = vertices[i];
                Phase3GridPoint next = vertices[(i + 1) % vertices.Count];
                long dx = (long)next.X - current.X;
                long dy = (long)next.Y - current.Y;
                perimeter += Math.Sqrt(dx * dx + dy * dy);
            }
            return perimeter;
        }

        private static string ComputeSha256(string value)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(value));
                var builder = new StringBuilder(bytes.Length * 2);
                for (int i = 0; i < bytes.Length; i++) builder.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture));
                return builder.ToString();
            }
        }

        private static ulong CreateAttemptSeed(long seed, GameDifficulty difficulty, int attempt)
        {
            ulong value = unchecked((ulong)seed) ^ 0x9E3779B97F4A7C15UL;
            value ^= (ulong)(int)difficulty * 0xBF58476D1CE4E5B9UL;
            value ^= (ulong)(attempt + 1) * 0x94D049BB133111EBUL;
            value ^= value >> 30;
            value *= 0xBF58476D1CE4E5B9UL;
            value ^= value >> 27;
            value *= 0x94D049BB133111EBUL;
            value ^= value >> 31;
            return value == 0UL ? 0xD1B54A32D192ED03UL : value;
        }

        private static Phase3GridPoint P(int x, int y) => new Phase3GridPoint(x, y);

        private readonly struct Region : IComparable<Region>
        {
            public Region(IEnumerable<Phase3GridPoint> vertices)
            {
                IReadOnlyList<Phase3GridPoint> canonical = Phase3Geometry.CanonicalizeVertices(new List<Phase3GridPoint>(vertices));
                var copied = new Phase3GridPoint[canonical.Count];
                for (int i = 0; i < canonical.Count; i++) copied[i] = canonical[i];
                Vertices = Array.AsReadOnly(copied);
                Bounds = Phase3Geometry.GetBounds(Vertices);
                Phase3Geometry.TryGetCentroid(Vertices, out Phase3Point2D centroid);
                Centroid = centroid;
            }
            public IReadOnlyList<Phase3GridPoint> Vertices { get; }
            private Phase3Bounds2D Bounds { get; }
            private Phase3Point2D Centroid { get; }
            public int CompareTo(Region other)
            {
                int y = Centroid.Y.CompareTo(other.Centroid.Y);
                if (y != 0) return y;
                int x = Centroid.X.CompareTo(other.Centroid.X);
                if (x != 0) return x;
                return Phase3Geometry.SignedDoubleArea(Vertices).CompareTo(Phase3Geometry.SignedDoubleArea(other.Vertices));
            }
            public Region Transform(bool mirrorX, bool mirrorY)
            {
                var transformed = new Phase3GridPoint[Vertices.Count];
                for (int i = 0; i < Vertices.Count; i++)
                {
                    int x = mirrorX ? Phase3CoreConstants.LogicalGridSize - Vertices[i].X : Vertices[i].X;
                    int y = mirrorY ? Phase3CoreConstants.LogicalGridSize - Vertices[i].Y : Vertices[i].Y;
                    transformed[i] = new Phase3GridPoint(x, y);
                }
                return new Region(transformed);
            }
        }

        private readonly struct Candidate
        {
            public Candidate(string puzzleId, string canonicalHash, Phase3PuzzleStructureSignature signature,
                Phase3PuzzleDefinition puzzle, IReadOnlyList<Phase3GeneratedPieceData> generatedPieces,
                IReadOnlyList<Phase3InitialPieceRotation> initialRotations)
            {
                PuzzleId = puzzleId;
                CanonicalHash = canonicalHash;
                Signature = signature;
                Puzzle = puzzle;
                GeneratedPieces = generatedPieces;
                InitialRotations = initialRotations;
            }
            public string PuzzleId { get; }
            public string CanonicalHash { get; }
            public Phase3PuzzleStructureSignature Signature { get; }
            public Phase3PuzzleDefinition Puzzle { get; }
            public IReadOnlyList<Phase3GeneratedPieceData> GeneratedPieces { get; }
            public IReadOnlyList<Phase3InitialPieceRotation> InitialRotations { get; }
        }

        private sealed class DeterministicRandom
        {
            private ulong state;
            public DeterministicRandom(ulong seed) => state = seed;
            public int NextInt(int exclusiveMaximum)
            {
                if (exclusiveMaximum <= 0) throw new ArgumentOutOfRangeException(nameof(exclusiveMaximum));
                uint bound = (uint)exclusiveMaximum;
                uint threshold = unchecked((uint)(0u - bound)) % bound;
                for (int retry = 0; retry < 8; retry++)
                {
                    uint value = NextUInt32();
                    if (value >= threshold) return (int)(value % bound);
                }
                return (int)(NextUInt32() % bound);
            }
            private uint NextUInt32()
            {
                state ^= state >> 12;
                state ^= state << 25;
                state ^= state >> 27;
                return (uint)((state * 2685821657736338717UL) >> 32);
            }
        }
    }
}
