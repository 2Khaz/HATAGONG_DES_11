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
        public const string GeneratorVersion = "phase3-recursive-grid-split-v4";
        public const int DefaultMaximumAttempts = 16;
        public const int MaximumAllowedAttempts = 64;

        public static string ComputeCanonicalHash(
            GameDifficulty difficulty,
            IEnumerable<Phase3GeneratedPieceData> generatedPieces) =>
            ComputeCanonicalHashForVersion(GeneratorVersion, difficulty, generatedPieces);

        public static string ComputeCanonicalHashForVersion(
            string generatorVersion,
            GameDifficulty difficulty,
            IEnumerable<Phase3GeneratedPieceData> generatedPieces)
        {
            if (string.IsNullOrWhiteSpace(generatorVersion)) throw new ArgumentException("Generator version is required.", nameof(generatorVersion));
            Phase3PuzzleGeneratorDifficultyConfig.For(difficulty);
            if (generatedPieces == null) throw new ArgumentNullException(nameof(generatedPieces));
            var polygons = new List<string>();
            foreach (Phase3GeneratedPieceData piece in generatedPieces)
            {
                if (piece == null) throw new ArgumentException("Canonical hashing requires non-null pieces.", nameof(generatedPieces));
                polygons.Add(BuildAbsolutePolygonKey(piece.Vertices));
            }
            if (polygons.Count == 0) throw new ArgumentException("Canonical hashing requires at least one piece.", nameof(generatedPieces));
            polygons.Sort(StringComparer.Ordinal);
            return ComputeSha256($"{generatorVersion}|{(int)difficulty}|{string.Join("/", polygons)}");
        }

        public static Phase3PuzzleGenerationResult Generate(Phase3PuzzleGenerationRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            Phase3PuzzleGeneratorDifficultyConfig config;
            try { config = Phase3PuzzleGeneratorDifficultyConfig.For(request.Difficulty); }
            catch (ArgumentOutOfRangeException)
            {
                return Phase3PuzzleGenerationResult.Failed(
                    Phase3PuzzleGenerationFailure.InvalidDifficulty,
                    "Difficulty must be Easy, Normal, or Hard.", request.Seed, request.Difficulty, 0);
            }

            var recentHashes = new HashSet<string>(request.RecentCanonicalHashes, StringComparer.OrdinalIgnoreCase);
            var shapeHistory = new ShapeHistoryProfile(request.RecentShapeSignaturePuzzles);
            bool duplicateRejected = false;
            int exactHistoryRejections = 0;
            string lastFailure = string.Empty;
            for (int attemptIndex = 0; attemptIndex < request.MaximumAttempts; attemptIndex++)
            {
                GenerationAttempt attempt = BuildAttempt(request.Seed, request.Difficulty, attemptIndex, config, shapeHistory);
                if (!attempt.Succeeded)
                {
                    lastFailure = attempt.FailureReason;
                    continue;
                }
                if (recentHashes.Contains(attempt.Candidate.CanonicalHash))
                {
                    duplicateRejected = true;
                    exactHistoryRejections++;
                    lastFailure = string.Empty;
                    continue;
                }
                return ToResult(request.Seed, request.Difficulty, attemptIndex, attempt, exactHistoryRejections);
            }

            Phase3PuzzleGenerationFailure failure = duplicateRejected && string.IsNullOrEmpty(lastFailure)
                ? Phase3PuzzleGenerationFailure.DuplicateHistoryExhausted
                : Phase3PuzzleGenerationFailure.AttemptsExhausted;
            string reason = failure == Phase3PuzzleGenerationFailure.DuplicateHistoryExhausted
                ? $"All {request.MaximumAttempts} deterministic candidates matched recent canonical hashes."
                : $"No recursive split candidate passed within {request.MaximumAttempts} attempts. Last failure: {lastFailure}";
            return Phase3PuzzleGenerationResult.Failed(failure, reason, request.Seed, request.Difficulty, request.MaximumAttempts);
        }

        public static Phase3PuzzleGenerationResult RegenerateCandidate(
            long requestedSeed,
            GameDifficulty difficulty,
            int attemptIndex)
        {
            if (attemptIndex < 0 || attemptIndex >= MaximumAllowedAttempts)
                throw new ArgumentOutOfRangeException(nameof(attemptIndex), attemptIndex, "Attempt index must be between 0 and 63.");
            Phase3PuzzleGeneratorDifficultyConfig config = Phase3PuzzleGeneratorDifficultyConfig.For(difficulty);
            GenerationAttempt attempt = BuildAttempt(
                requestedSeed, difficulty, attemptIndex, config, ShapeHistoryProfile.Empty);
            if (!attempt.Succeeded)
                return Phase3PuzzleGenerationResult.Failed(
                    Phase3PuzzleGenerationFailure.AttemptsExhausted,
                    attempt.FailureReason, requestedSeed, difficulty, attemptIndex + 1);
            return ToResult(requestedSeed, difficulty, attemptIndex, attempt);
        }

        private static Phase3PuzzleGenerationResult ToResult(
            long requestedSeed,
            GameDifficulty difficulty,
            int attemptIndex,
            GenerationAttempt attempt,
            int exactHistoryRejections = 0) =>
            Phase3PuzzleGenerationResult.Success(
                requestedSeed,
                attempt.EffectiveSeed,
                attemptIndex,
                difficulty,
                attemptIndex + 1,
                attempt.Candidate.PuzzleId,
                attempt.Candidate.CanonicalHash,
                attempt.Candidate.Signature,
                attempt.Candidate.Puzzle,
                attempt.Candidate.GeneratedPieces,
                attempt.Candidate.InitialRotations,
                attempt.GenerationCycles,
                attempt.BacktrackingCount,
                attempt.ExhaustedCandidateCount,
                attempt.BacktrackingBudgetExhaustionCount,
                attempt.CurrentShapeDuplicateRejectionCount,
                exactHistoryRejections);

        private static GenerationAttempt BuildAttempt(
            long requestedSeed,
            GameDifficulty difficulty,
            int attemptIndex,
            Phase3PuzzleGeneratorDifficultyConfig config,
            ShapeHistoryProfile shapeHistory)
        {
            ulong effectiveSeed = CreateAttemptSeed(requestedSeed, difficulty, attemptIndex);
            string lastFailure = string.Empty;
            int totalBacktracking = 0;
            int exhausted = 0;
            int budgetExhaustions = 0;
            int shapeDuplicateRejections = 0;
            for (int cycle = 0; cycle < config.CycleBudget; cycle++)
            {
                ulong cycleSeed = Mix(effectiveSeed ^ (1UL << 56) ^ (ulong)(cycle + 1) * 0x9E3779B97F4A7C15UL);
                var random = new DeterministicRandom(cycleSeed);
                var search = new SearchState(config.BacktrackingBudget);
                var regions = new List<Region>
                {
                    new Region(new[]
                    {
                        new Phase3GridPoint(0, 0),
                        new Phase3GridPoint(16, 0),
                        new Phase3GridPoint(16, 16),
                        new Phase3GridPoint(0, 16)
                    })
                };
                if (TrySearch(regions, config, random, search, shapeHistory, out List<Region> completed))
                {
                    if (TryBuildCandidate(config, completed, random, out Candidate candidate, out lastFailure))
                        return GenerationAttempt.Success(
                            effectiveSeed, cycle + 1, totalBacktracking + search.BacktrackingCount,
                            exhausted + search.ExhaustedCandidateCount,
                            budgetExhaustions + search.BacktrackingBudgetExhaustionCount,
                            shapeDuplicateRejections + search.CurrentShapeDuplicateRejectionCount,
                            candidate);
                }
                else lastFailure = search.LastFailure;
                totalBacktracking += search.BacktrackingCount;
                exhausted += search.ExhaustedCandidateCount;
                budgetExhaustions += search.BacktrackingBudgetExhaustionCount;
                shapeDuplicateRejections += search.CurrentShapeDuplicateRejectionCount;
            }
            return GenerationAttempt.Failed(
                effectiveSeed, lastFailure, totalBacktracking, exhausted,
                budgetExhaustions, shapeDuplicateRejections,
                config.CycleBudget);
        }

        private static bool TrySearch(
            List<Region> regions,
            Phase3PuzzleGeneratorDifficultyConfig config,
            DeterministicRandom random,
            SearchState state,
            ShapeHistoryProfile shapeHistory,
            out List<Region> completed)
        {
            if (regions.Count == config.PieceCount)
            {
                if (ValidateFinalSet(regions, config, shapeHistory, out string failure))
                {
                    completed = regions;
                    return true;
                }
                state.LastFailure = failure;
                if (string.Equals(failure, "Duplicate shape signature in current puzzle.", StringComparison.Ordinal))
                    state.CurrentShapeDuplicateRejectionCount++;
                completed = null;
                return false;
            }

            List<SplitOption> options = EnumerateOptions(regions, config, random, shapeHistory);
            if (options.Count == 0)
            {
                state.ExhaustedCandidateCount++;
                state.LastFailure = "No valid finite grid split remained.";
                completed = null;
                return false;
            }
            int remainingAfterThis = config.PieceCount - (regions.Count + 1);
            for (int optionIndex = 0; optionIndex < options.Count; optionIndex++)
            {
                SplitOption option = options[optionIndex];
                var next = new List<Region>(regions.Count + 1);
                for (int i = 0; i < regions.Count; i++)
                {
                    if (i != option.RegionIndex) next.Add(regions[i]);
                }
                next.Add(option.First);
                next.Add(option.Second);
                next.Sort();
                if (remainingAfterThis == 0 && !ValidateFinalSet(next, config, shapeHistory, out string finalFailure))
                {
                    if (string.Equals(finalFailure, "Duplicate shape signature in current puzzle.", StringComparison.Ordinal))
                        state.CurrentShapeDuplicateRejectionCount++;
                    continue;
                }
                if (TrySearch(next, config, random, state, shapeHistory, out completed)) return true;
                state.BacktrackingCount++;
                if (state.BacktrackingCount >= state.BacktrackingBudget)
                {
                    state.BacktrackingBudgetExhaustionCount++;
                    state.LastFailure = "Backtracking budget exhausted.";
                    completed = null;
                    return false;
                }
            }
            state.ExhaustedCandidateCount++;
            completed = null;
            return false;
        }

        private static List<SplitOption> EnumerateOptions(
            IReadOnlyList<Region> regions,
            Phase3PuzzleGeneratorDifficultyConfig config,
            DeterministicRandom random,
            ShapeHistoryProfile shapeHistory)
        {
            var options = new List<SplitOption>();
            for (int regionIndex = 0; regionIndex < regions.Count; regionIndex++)
            {
                Region region = regions[regionIndex];
                Phase3Bounds2D bounds = Phase3Geometry.GetBounds(region.Vertices);
                int minX = (int)bounds.MinX;
                int maxX = (int)bounds.MaxX;
                int minY = (int)bounds.MinY;
                int maxY = (int)bounds.MaxY;
                AddLines(options, regions, regionIndex, region, 1, 0, minX + 1, maxX - 1, config, random, shapeHistory);
                AddLines(options, regions, regionIndex, region, 0, 1, minY + 1, maxY - 1, config, random, shapeHistory);
                int minDifference = int.MaxValue;
                int maxDifference = int.MinValue;
                int minSum = int.MaxValue;
                int maxSum = int.MinValue;
                for (int i = 0; i < region.Vertices.Count; i++)
                {
                    int difference = region.Vertices[i].X - region.Vertices[i].Y;
                    int sum = region.Vertices[i].X + region.Vertices[i].Y;
                    minDifference = Math.Min(minDifference, difference);
                    maxDifference = Math.Max(maxDifference, difference);
                    minSum = Math.Min(minSum, sum);
                    maxSum = Math.Max(maxSum, sum);
                }
                AddLines(options, regions, regionIndex, region, 1, -1, minDifference + 1, maxDifference - 1, config, random, shapeHistory);
                AddLines(options, regions, regionIndex, region, 1, 1, minSum + 1, maxSum - 1, config, random, shapeHistory);
            }
            options.Sort((left, right) =>
            {
                int score = right.Score.CompareTo(left.Score);
                return score != 0 ? score : left.Order.CompareTo(right.Order);
            });
            return options;
        }

        private static void AddLines(
            ICollection<SplitOption> output,
            IReadOnlyList<Region> currentRegions,
            int regionIndex,
            Region region,
            int a,
            int b,
            int minimumC,
            int maximumC,
            Phase3PuzzleGeneratorDifficultyConfig config,
            DeterministicRandom random,
            ShapeHistoryProfile shapeHistory)
        {
            for (int c = minimumC; c <= maximumC; c++)
            {
                if (!TrySplit(region, a, b, c, out Region first, out Region second)) continue;
                if (!MeetsIntermediateLimits(first, config) || !MeetsIntermediateLimits(second, config)) continue;
                double balance = Math.Abs(first.Area - second.Area);
                int balanceWeight;
                switch (config.Difficulty)
                {
                    case GameDifficulty.Easy: balanceWeight = (int)Math.Round(1000d - balance * 18d); break;
                    case GameDifficulty.Normal: balanceWeight = (int)Math.Round(700d - balance * 6d); break;
                    default: balanceWeight = (int)Math.Round(500d + balance * 4d); break;
                }
                int areaWeight = (int)Math.Round(region.Area * 8d);
                int jitter = random.NextInt(8001);
                int completionWeight = EvaluateCompletionWeight(currentRegions, regionIndex, first, second, config, shapeHistory);
                output.Add(new SplitOption(
                    regionIndex,
                    first,
                    second,
                    completionWeight + areaWeight + balanceWeight + jitter,
                    random.NextUInt64()));
            }
        }

        private static int EvaluateCompletionWeight(
            IReadOnlyList<Region> currentRegions,
            int replacedRegionIndex,
            Region first,
            Region second,
            Phase3PuzzleGeneratorDifficultyConfig config,
            ShapeHistoryProfile shapeHistory)
        {
            var signatures = new HashSet<string>(StringComparer.Ordinal);
            var kindCounts = new Dictionary<Phase3GeneratedShapeKind, int>();
            int validUniqueLeaves = 0;
            int invalidLeaves = 0;
            int duplicateLeaves = 0;
            int complexLeaves = 0;
            for (int i = 0; i < currentRegions.Count + 1; i++)
            {
                Region candidate;
                if (i < currentRegions.Count)
                {
                    if (i == replacedRegionIndex) candidate = first;
                    else candidate = currentRegions[i];
                }
                else candidate = second;

                Phase3GeneratedShapeKind kind;
                try { kind = ClassifyShape(candidate.Vertices); }
                catch (ArgumentException)
                {
                    invalidLeaves++;
                    continue;
                }
                if (!IsAllowedFinalKind(candidate.Vertices, kind, config.Difficulty) ||
                    !MeetsQualityLimits(candidate.Vertices, config, out _))
                {
                    invalidLeaves++;
                    continue;
                }
                string signature = ComputeShapeSignature(candidate.Vertices);
                if (shapeHistory.IsForbiddenByPreviousPuzzle(signature))
                {
                    invalidLeaves++;
                    continue;
                }
                if (!signatures.Add(signature))
                {
                    duplicateLeaves++;
                    continue;
                }
                validUniqueLeaves++;
                validUniqueLeaves += shapeHistory.WeightFor(signature) / 25;
                kindCounts.TryGetValue(kind, out int count);
                kindCounts[kind] = count + 1;
                if (kind == Phase3GeneratedShapeKind.Rhombus ||
                    kind == Phase3GeneratedShapeKind.Parallelogram ||
                    kind == Phase3GeneratedShapeKind.Trapezoid && !IsEasyTrapezoid(candidate.Vertices))
                    complexLeaves++;
            }

            int score = validUniqueLeaves * 20000 - invalidLeaves * 2500 - duplicateLeaves * 12000;
            int maximumKind = 0;
            foreach (KeyValuePair<Phase3GeneratedShapeKind, int> pair in kindCounts)
                maximumKind = Math.Max(maximumKind, pair.Value);
            score -= Math.Max(0, maximumKind - config.PieceCount / 2) * 30000;
            if (config.Difficulty == GameDifficulty.Normal) score += Math.Min(1, complexLeaves) * 18000;
            if (config.Difficulty == GameDifficulty.Hard) score += Math.Min(2, complexLeaves) * 18000;
            return score;
        }

        private static bool TrySplit(Region region, int a, int b, int c, out Region first, out Region second)
        {
            var positive = new List<Phase3GridPoint>();
            var negative = new List<Phase3GridPoint>();
            bool hasPositive = false;
            bool hasNegative = false;
            for (int i = 0; i < region.Vertices.Count; i++)
            {
                Phase3GridPoint current = region.Vertices[i];
                Phase3GridPoint next = region.Vertices[(i + 1) % region.Vertices.Count];
                int currentValue = a * current.X + b * current.Y - c;
                int nextValue = a * next.X + b * next.Y - c;
                if (currentValue >= 0) AddDistinct(positive, current);
                if (currentValue <= 0) AddDistinct(negative, current);
                hasPositive |= currentValue > 0;
                hasNegative |= currentValue < 0;
                if (currentValue * nextValue < 0)
                {
                    int denominator = currentValue - nextValue;
                    long numeratorX = (long)current.X * denominator + (long)(next.X - current.X) * currentValue;
                    long numeratorY = (long)current.Y * denominator + (long)(next.Y - current.Y) * currentValue;
                    if (numeratorX % denominator != 0 || numeratorY % denominator != 0)
                    {
                        first = default;
                        second = default;
                        return false;
                    }
                    var intersection = new Phase3GridPoint((int)(numeratorX / denominator), (int)(numeratorY / denominator));
                    AddDistinct(positive, intersection);
                    AddDistinct(negative, intersection);
                }
            }
            if (!hasPositive || !hasNegative)
            {
                first = default;
                second = default;
                return false;
            }
            positive = CleanPolygon(positive);
            negative = CleanPolygon(negative);
            if (positive.Count < 3 || negative.Count < 3)
            {
                first = default;
                second = default;
                return false;
            }
            first = new Region(positive);
            second = new Region(negative);
            return true;
        }

        private static List<Phase3GridPoint> CleanPolygon(List<Phase3GridPoint> source)
        {
            if (source.Count > 1 && source[0] == source[source.Count - 1]) source.RemoveAt(source.Count - 1);
            bool removed;
            do
            {
                removed = false;
                for (int i = 0; i < source.Count && source.Count >= 3; i++)
                {
                    Phase3GridPoint previous = source[(i - 1 + source.Count) % source.Count];
                    Phase3GridPoint current = source[i];
                    Phase3GridPoint next = source[(i + 1) % source.Count];
                    long cross = (long)(current.X - previous.X) * (next.Y - current.Y) -
                                 (long)(current.Y - previous.Y) * (next.X - current.X);
                    if (cross != 0) continue;
                    source.RemoveAt(i);
                    removed = true;
                    break;
                }
            } while (removed);
            return source;
        }

        private static void AddDistinct(ICollection<Phase3GridPoint> target, Phase3GridPoint point)
        {
            if (target is List<Phase3GridPoint> list && list.Count > 0 && list[list.Count - 1] == point) return;
            target.Add(point);
        }

        private static bool MeetsIntermediateLimits(Region region, Phase3PuzzleGeneratorDifficultyConfig config)
        {
            if (region.Vertices.Count < 3 || region.Vertices.Count > 8) return false;
            if (region.Area < config.MinimumPieceArea - Phase3CoreConstants.ComparisonEpsilon) return false;
            if (!Phase3Geometry.IsConvex(region.Vertices) || Phase3Geometry.HasSelfIntersection(region.Vertices)) return false;
            if (!Phase3Geometry.AreAllVerticesWithinLogicalGrid(region.Vertices) || !Phase3Geometry.AreAllEdgesAllowed(region.Vertices)) return false;
            if (CalculateMinimumEdgeLength(region.Vertices) < config.MinimumEdgeLength - Phase3CoreConstants.ComparisonEpsilon) return false;
            return CalculateMinimumThickness(region.Vertices) >= config.MinimumThickness - Phase3CoreConstants.ComparisonEpsilon;
        }

        private static bool ValidateFinalSet(
            IReadOnlyList<Region> regions,
            Phase3PuzzleGeneratorDifficultyConfig config,
            ShapeHistoryProfile shapeHistory,
            out string failureReason)
        {
            if (regions.Count != config.PieceCount)
            {
                failureReason = "Target piece count mismatch.";
                return false;
            }
            var signatures = new HashSet<string>(StringComparer.Ordinal);
            var kindCounts = new Dictionary<Phase3GeneratedShapeKind, int>();
            int complexCount = 0;
            for (int i = 0; i < regions.Count; i++)
            {
                Region region = regions[i];
                if (region.Vertices.Count != 3 && region.Vertices.Count != 4)
                {
                    failureReason = "Final polygon is not a triangle or quadrilateral.";
                    return false;
                }
                Phase3GeneratedShapeKind kind;
                try { kind = ClassifyShape(region.Vertices); }
                catch (ArgumentException)
                {
                    failureReason = "Final polygon is outside the allowed shape catalog.";
                    return false;
                }
                if (!IsAllowedFinalKind(region.Vertices, kind, config.Difficulty))
                {
                    failureReason = $"Shape {kind} is not allowed for {config.Difficulty}.";
                    return false;
                }
                if (!MeetsQualityLimits(region.Vertices, config, out failureReason)) return false;
                string signature = ComputeShapeSignature(region.Vertices);
                if (shapeHistory.IsForbiddenByPreviousPuzzle(signature))
                {
                    failureReason = "Shape signature appeared in the immediately previous puzzle.";
                    return false;
                }
                if (!signatures.Add(signature))
                {
                    failureReason = "Duplicate shape signature in current puzzle.";
                    return false;
                }
                kindCounts.TryGetValue(kind, out int count);
                kindCounts[kind] = count + 1;
                if (kind == Phase3GeneratedShapeKind.Rhombus || kind == Phase3GeneratedShapeKind.Parallelogram ||
                    kind == Phase3GeneratedShapeKind.Trapezoid && !IsEasyTrapezoid(region.Vertices)) complexCount++;
            }
            foreach (KeyValuePair<Phase3GeneratedShapeKind, int> count in kindCounts)
                if (count.Value > config.PieceCount / 2)
                {
                    failureReason = "One shape kind exceeds half of the puzzle.";
                    return false;
                }
            if (Count(kindCounts, Phase3GeneratedShapeKind.Square) > 1 ||
                Count(kindCounts, Phase3GeneratedShapeKind.RightTriangle) > 1 ||
                Count(kindCounts, Phase3GeneratedShapeKind.Rhombus) > 1)
            {
                failureReason = "Square, right triangle, or rhombus maximum count exceeded.";
                return false;
            }
            if (config.Difficulty == GameDifficulty.Normal &&
                Count(kindCounts, Phase3GeneratedShapeKind.Rhombus) + Count(kindCounts, Phase3GeneratedShapeKind.Parallelogram) < 1)
            {
                failureReason = "Normal requires a rhombus or parallelogram.";
                return false;
            }
            if (config.Difficulty == GameDifficulty.Hard && complexCount < 2)
            {
                failureReason = "Hard requires at least two complex quadrilaterals.";
                return false;
            }
            failureReason = string.Empty;
            return true;
        }

        private static int Count(IDictionary<Phase3GeneratedShapeKind, int> counts, Phase3GeneratedShapeKind kind) =>
            counts.TryGetValue(kind, out int value) ? value : 0;

        private static bool IsAllowedFinalKind(
            IReadOnlyList<Phase3GridPoint> vertices,
            Phase3GeneratedShapeKind kind,
            GameDifficulty difficulty)
        {
            if (kind == Phase3GeneratedShapeKind.RightTriangle || kind == Phase3GeneratedShapeKind.Square ||
                kind == Phase3GeneratedShapeKind.Rectangle) return true;
            if (kind == Phase3GeneratedShapeKind.Trapezoid)
                return difficulty != GameDifficulty.Easy || IsEasyTrapezoid(vertices);
            return difficulty != GameDifficulty.Easy &&
                   (kind == Phase3GeneratedShapeKind.Rhombus || kind == Phase3GeneratedShapeKind.Parallelogram);
        }

        private static bool IsEasyTrapezoid(IReadOnlyList<Phase3GridPoint> vertices)
        {
            if (vertices.Count != 4) return false;
            bool firstParallel = Parallel(Edge(vertices, 0), Edge(vertices, 2));
            int firstLeg = firstParallel ? 1 : 0;
            int secondLeg = firstParallel ? 3 : 2;
            Vector legA = Edge(vertices, firstLeg);
            Vector legB = Edge(vertices, secondLeg);
            Vector baseEdge = Edge(vertices, firstParallel ? 0 : 1);
            return Perpendicular(legA, baseEdge) || Perpendicular(legB, baseEdge) || legA.LengthSquared == legB.LengthSquared;
        }

        private static bool TryBuildCandidate(
            Phase3PuzzleGeneratorDifficultyConfig config,
            List<Region> regions,
            DeterministicRandom random,
            out Candidate candidate,
            out string failureReason)
        {
            regions.Sort();
            var pieces = new List<Phase3PieceDefinition>(regions.Count);
            var slots = new List<Phase3SlotDefinition>(regions.Count);
            var generated = new List<Phase3GeneratedPieceData>(regions.Count);
            var initialRotations = new List<Phase3InitialPieceRotation>(regions.Count);
            int sameAsTarget = 0;
            for (int i = 0; i < regions.Count; i++)
            {
                Region region = regions[i];
                string prefix = config.Difficulty.ToString().ToLowerInvariant();
                string pieceId = $"p3-{prefix}-piece-{i + 1:D2}";
                string slotId = $"p3-{prefix}-slot-{i + 1:D2}";
                string deckSlotId = $"p3-{prefix}-deck-{i + 1:D2}";
                int symmetry = DetermineRotationalSymmetryPeriod(region.Vertices);
                Phase3GeneratedShapeKind kind = ClassifyShape(region.Vertices);
                var targetRotation = new Phase3RotationStep(random.NextInt(8));
                Phase3RotationStep initialRotation = CreateInitialRotation(random, targetRotation, config.Difficulty, ref sameAsTarget);
                var shape = new Phase3ShapeDefinition($"{pieceId}-shape", region.Vertices, symmetry);
                double logicalScale = Phase3CoreConstants.CanvasFieldSize / (double)Phase3CoreConstants.LogicalGridSize;
                Phase3Point2D canonicalCentroid = shape.Centroid * logicalScale;
                slots.Add(new Phase3SlotDefinition(slotId, shape, canonicalCentroid, default));
                pieces.Add(new Phase3PieceDefinition(pieceId, deckSlotId, shape,
                    new[]
                    {
                        new Phase3AllowedTarget(
                            slotId,
                            targetRotation,
                            new Phase3RotationStep(-targetRotation.Value))
                    }));
                generated.Add(new Phase3GeneratedPieceData(
                    pieceId, slotId, region.Vertices, kind, symmetry, targetRotation, initialRotation));
                initialRotations.Add(new Phase3InitialPieceRotation(pieceId, initialRotation));
            }
            Phase3PuzzleDefinition puzzle;
            try { puzzle = new Phase3PuzzleDefinition(pieces, slots); }
            catch (ArgumentException exception)
            {
                candidate = default;
                failureReason = exception.Message;
                return false;
            }
            Phase3PartitionValidationResult partition = Phase3PartitionValidator.Validate(puzzle, config.Difficulty, config.PartitionRules);
            if (!partition.IsValid)
            {
                candidate = default;
                failureReason = partition.Issues[0].ToString();
                return false;
            }
            string hash = ComputeCanonicalHash(config.Difficulty, generated);
            string puzzleId = $"p3-v4-{config.Difficulty.ToString().ToLowerInvariant()}-{hash.Substring(0, 12)}";
            candidate = new Candidate(
                puzzleId, hash, BuildStructureSignature(config.Difficulty, generated), puzzle, generated, initialRotations);
            failureReason = string.Empty;
            return true;
        }

        private static Phase3RotationStep CreateInitialRotation(
            DeterministicRandom random,
            Phase3RotationStep target,
            GameDifficulty difficulty,
            ref int sameAsTarget)
        {
            int[] steps = difficulty == GameDifficulty.Easy ? new[] { 0, 2, 4, 6 } : new[] { 0, 1, 2, 3, 4, 5, 6, 7 };
            int maximumSame = difficulty == GameDifficulty.Easy ? 3 : difficulty == GameDifficulty.Normal ? 2 : 0;
            int start = random.NextInt(steps.Length);
            for (int offset = 0; offset < steps.Length; offset++)
            {
                var rotation = new Phase3RotationStep(steps[(start + offset) % steps.Length]);
                if (rotation == target && sameAsTarget >= maximumSame) continue;
                if (rotation == target) sameAsTarget++;
                return rotation;
            }
            return new Phase3RotationStep(target.Value + 1);
        }

        public static bool MeetsQualityLimits(
            IReadOnlyList<Phase3GridPoint> vertices,
            Phase3PuzzleGeneratorDifficultyConfig config,
            out string failureReason)
        {
            if (vertices == null) throw new ArgumentNullException(nameof(vertices));
            if (config == null) throw new ArgumentNullException(nameof(config));
            Phase3PolygonValidationResult polygon = Phase3Geometry.ValidatePolygon(vertices);
            if (!polygon.IsValid)
            {
                failureReason = polygon.Message;
                return false;
            }
            if (vertices.Count != 3 && vertices.Count != 4)
            {
                failureReason = "Final piece requires three or four vertices.";
                return false;
            }
            if (Phase3Geometry.AbsoluteArea(vertices) < config.MinimumPieceArea - Phase3CoreConstants.ComparisonEpsilon)
            {
                failureReason = "Minimum area failed.";
                return false;
            }
            if (CalculateMinimumEdgeLength(vertices) < config.MinimumEdgeLength - Phase3CoreConstants.ComparisonEpsilon)
            {
                failureReason = "Minimum edge length failed.";
                return false;
            }
            if (CalculateMinimumThickness(vertices) < config.MinimumThickness - Phase3CoreConstants.ComparisonEpsilon)
            {
                failureReason = "Minimum thickness failed.";
                return false;
            }
            if (Phase3Geometry.GetBounds(vertices).AspectRatio >
                config.PartitionRules.MaximumAspectRatio + Phase3CoreConstants.ComparisonEpsilon)
            {
                failureReason = "Maximum aspect ratio failed.";
                return false;
            }
            if (CalculateMinimumInteriorAngleDegrees(vertices) < config.MinimumInteriorAngleDegrees - Phase3CoreConstants.ComparisonEpsilon)
            {
                failureReason = "Minimum angle failed.";
                return false;
            }
            Phase3GeneratedShapeKind kind;
            try { kind = ClassifyShape(vertices); }
            catch (ArgumentException exception)
            {
                failureReason = exception.Message;
                return false;
            }
            if (!IsAllowedFinalKind(vertices, kind, config.Difficulty))
            {
                failureReason = "Final shape kind failed.";
                return false;
            }
            failureReason = string.Empty;
            return true;
        }

        public static Phase3GeneratedShapeKind ClassifyShape(IReadOnlyList<Phase3GridPoint> vertices)
        {
            if (vertices == null) throw new ArgumentNullException(nameof(vertices));
            IReadOnlyList<Phase3GridPoint> normalized = Phase3Geometry.NormalizeCounterClockwise(vertices);
            if (normalized.Count == 3)
            {
                for (int i = 0; i < 3; i++)
                    if (Perpendicular(Edge(normalized, i), Edge(normalized, (i + 1) % 3)))
                        return Phase3GeneratedShapeKind.RightTriangle;
                throw new ArgumentException("Only right triangles are supported.", nameof(vertices));
            }
            if (normalized.Count != 4) throw new ArgumentException("Only triangles and quadrilaterals are supported.", nameof(vertices));
            Vector e0 = Edge(normalized, 0);
            Vector e1 = Edge(normalized, 1);
            Vector e2 = Edge(normalized, 2);
            Vector e3 = Edge(normalized, 3);
            bool oppositeA = Parallel(e0, e2);
            bool oppositeB = Parallel(e1, e3);
            bool allEqual = e0.LengthSquared == e1.LengthSquared && e1.LengthSquared == e2.LengthSquared && e2.LengthSquared == e3.LengthSquared;
            bool right = Perpendicular(e0, e1);
            if (oppositeA && oppositeB && right && allEqual) return Phase3GeneratedShapeKind.Square;
            if (oppositeA && oppositeB && right) return Phase3GeneratedShapeKind.Rectangle;
            if (oppositeA && oppositeB && allEqual) return Phase3GeneratedShapeKind.Rhombus;
            if (oppositeA && oppositeB) return Phase3GeneratedShapeKind.Parallelogram;
            if (oppositeA ^ oppositeB) return Phase3GeneratedShapeKind.Trapezoid;
            throw new ArgumentException("Quadrilateral is not an allowed trapezoid or parallelogram family shape.", nameof(vertices));
        }

        public static int DetermineRotationalSymmetryPeriod(IReadOnlyList<Phase3GridPoint> vertices)
        {
            Phase3GeneratedShapeKind kind = ClassifyShape(vertices);
            if (kind == Phase3GeneratedShapeKind.Square) return 2;
            if (kind == Phase3GeneratedShapeKind.Rectangle || kind == Phase3GeneratedShapeKind.Rhombus ||
                kind == Phase3GeneratedShapeKind.Parallelogram) return 4;
            return 8;
        }

        public static string ComputeShapeSignature(IReadOnlyList<Phase3GridPoint> vertices)
        {
            if (vertices == null) throw new ArgumentNullException(nameof(vertices));
            var distances = new List<long>();
            for (int i = 0; i < vertices.Count; i++)
                for (int j = i + 1; j < vertices.Count; j++)
                    distances.Add(vertices[i].DistanceSquaredTo(vertices[j]));
            long divisor = 0;
            for (int i = 0; i < distances.Count; i++) divisor = GreatestCommonDivisor(divisor, distances[i]);
            if (divisor <= 0) throw new ArgumentException("Shape signature requires non-degenerate vertices.", nameof(vertices));
            for (int i = 0; i < distances.Count; i++) distances[i] /= divisor;
            distances.Sort();
            return $"{vertices.Count}:{string.Join(",", distances)}";
        }

        public static double CalculateMinimumEdgeLength(IReadOnlyList<Phase3GridPoint> vertices)
        {
            double minimum = double.PositiveInfinity;
            for (int i = 0; i < vertices.Count; i++)
                minimum = Math.Min(minimum, Math.Sqrt(vertices[i].DistanceSquaredTo(vertices[(i + 1) % vertices.Count])));
            return minimum;
        }

        public static double CalculateMinimumThickness(IReadOnlyList<Phase3GridPoint> vertices)
        {
            double minimum = double.PositiveInfinity;
            for (int edgeIndex = 0; edgeIndex < vertices.Count; edgeIndex++)
            {
                Phase3GridPoint start = vertices[edgeIndex];
                Phase3GridPoint end = vertices[(edgeIndex + 1) % vertices.Count];
                double dx = end.X - start.X;
                double dy = end.Y - start.Y;
                double length = Math.Sqrt(dx * dx + dy * dy);
                double minimumProjection = double.PositiveInfinity;
                double maximumProjection = double.NegativeInfinity;
                for (int i = 0; i < vertices.Count; i++)
                {
                    double projection = (-dy * vertices[i].X + dx * vertices[i].Y) / length;
                    minimumProjection = Math.Min(minimumProjection, projection);
                    maximumProjection = Math.Max(maximumProjection, projection);
                }
                minimum = Math.Min(minimum, maximumProjection - minimumProjection);
            }
            return minimum;
        }

        public static double CalculateMinimumInteriorAngleDegrees(IReadOnlyList<Phase3GridPoint> vertices)
        {
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
                double cosine = (ax * bx + ay * by) /
                    (Math.Sqrt(ax * ax + ay * ay) * Math.Sqrt(bx * bx + by * by));
                cosine = Math.Max(-1d, Math.Min(1d, cosine));
                minimum = Math.Min(minimum, Math.Acos(cosine) * 180d / Math.PI);
            }
            return minimum;
        }

        private static Phase3PuzzleStructureSignature BuildStructureSignature(
            GameDifficulty difficulty,
            IReadOnlyList<Phase3GeneratedPieceData> pieces)
        {
            var areas = new List<double>();
            var perimeters = new List<double>();
            var counts = new int[Enum.GetValues(typeof(Phase3GeneratedShapeKind)).Length];
            double totalPerimeter = 0d;
            for (int i = 0; i < pieces.Count; i++)
            {
                areas.Add(pieces[i].Area);
                double perimeter = 0d;
                for (int v = 0; v < pieces[i].Vertices.Count; v++)
                    perimeter += Math.Sqrt(pieces[i].Vertices[v].DistanceSquaredTo(
                        pieces[i].Vertices[(v + 1) % pieces[i].Vertices.Count]));
                perimeters.Add(perimeter);
                totalPerimeter += perimeter;
                counts[(int)pieces[i].ShapeKind]++;
            }
            double internalBoundary = (totalPerimeter - Phase3CoreConstants.LogicalGridSize * 4d) * 0.5d;
            return new Phase3PuzzleStructureSignature(difficulty, pieces.Count, areas, perimeters, internalBoundary, counts);
        }

        private static Vector Edge(IReadOnlyList<Phase3GridPoint> vertices, int index)
        {
            Phase3GridPoint start = vertices[index];
            Phase3GridPoint end = vertices[(index + 1) % vertices.Count];
            return new Vector(end.X - start.X, end.Y - start.Y);
        }

        private static bool Parallel(Vector first, Vector second) => first.X * second.Y - first.Y * second.X == 0;
        private static bool Perpendicular(Vector first, Vector second) => first.X * second.X + first.Y * second.Y == 0;

        private static string BuildAbsolutePolygonKey(IReadOnlyList<Phase3GridPoint> vertices)
        {
            IReadOnlyList<Phase3GridPoint> canonical = CanonicalizeRegionVertices(vertices);
            var values = new string[canonical.Count];
            for (int i = 0; i < canonical.Count; i++) values[i] = $"{canonical[i].X},{canonical[i].Y}";
            return string.Join(";", values);
        }

        private static string ComputeSha256(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
                var builder = new StringBuilder(bytes.Length * 2);
                for (int i = 0; i < bytes.Length; i++) builder.Append(bytes[i].ToString("x2", CultureInfo.InvariantCulture));
                return builder.ToString();
            }
        }

        private static ulong CreateAttemptSeed(long requestedSeed, GameDifficulty difficulty, int attemptIndex) =>
            Mix(unchecked((ulong)requestedSeed) ^ ((ulong)(int)difficulty << 48) ^ (ulong)(attemptIndex + 1) * 0xD6E8FEB86659FD93UL);

        private static ulong Mix(ulong value)
        {
            value ^= value >> 30;
            value *= 0xBF58476D1CE4E5B9UL;
            value ^= value >> 27;
            value *= 0x94D049BB133111EBUL;
            return value ^ value >> 31;
        }

        private static long GreatestCommonDivisor(long left, long right)
        {
            left = Math.Abs(left);
            right = Math.Abs(right);
            while (right != 0)
            {
                long temporary = left % right;
                left = right;
                right = temporary;
            }
            return left;
        }

        private static IReadOnlyList<Phase3GridPoint> CanonicalizeRegionVertices(
            IReadOnlyList<Phase3GridPoint> vertices)
        {
            var normalized = new List<Phase3GridPoint>(vertices);
            if (normalized.Count > 1 && normalized[0] == normalized[normalized.Count - 1])
                normalized.RemoveAt(normalized.Count - 1);
            if (SignedDoubleAreaUnchecked(normalized) < 0L) normalized.Reverse();
            int start = 0;
            for (int i = 1; i < normalized.Count; i++)
                if (normalized[i] < normalized[start]) start = i;
            var canonical = new Phase3GridPoint[normalized.Count];
            for (int i = 0; i < canonical.Length; i++) canonical[i] = normalized[(start + i) % normalized.Count];
            return Array.AsReadOnly(canonical);
        }

        private static long SignedDoubleAreaUnchecked(IReadOnlyList<Phase3GridPoint> vertices)
        {
            long area = 0L;
            for (int i = 0; i < vertices.Count; i++)
            {
                Phase3GridPoint next = vertices[(i + 1) % vertices.Count];
                area += (long)vertices[i].X * next.Y - (long)next.X * vertices[i].Y;
            }
            return area;
        }

        private readonly struct Vector
        {
            public Vector(long x, long y) { X = x; Y = y; }
            public long X { get; }
            public long Y { get; }
            public long LengthSquared => X * X + Y * Y;
        }

        private readonly struct Region : IComparable<Region>
        {
            public Region(IEnumerable<Phase3GridPoint> vertices)
            {
                Vertices = CanonicalizeRegionVertices(new List<Phase3GridPoint>(vertices));
                Area = Math.Abs(SignedDoubleAreaUnchecked(Vertices)) * 0.5d;
                Key = BuildAbsolutePolygonKey(Vertices);
            }
            public IReadOnlyList<Phase3GridPoint> Vertices { get; }
            public double Area { get; }
            public string Key { get; }
            public int CompareTo(Region other) => StringComparer.Ordinal.Compare(Key, other.Key);
        }

        private readonly struct SplitOption
        {
            public SplitOption(int regionIndex, Region first, Region second, int score, ulong order)
            {
                RegionIndex = regionIndex;
                First = first;
                Second = second;
                Score = score;
                Order = order;
            }
            public int RegionIndex { get; }
            public Region First { get; }
            public Region Second { get; }
            public int Score { get; }
            public ulong Order { get; }
        }

        private sealed class SearchState
        {
            public SearchState(int budget) { BacktrackingBudget = budget; }
            public int BacktrackingBudget { get; }
            public int BacktrackingCount { get; set; }
            public int ExhaustedCandidateCount { get; set; }
            public int BacktrackingBudgetExhaustionCount { get; set; }
            public int CurrentShapeDuplicateRejectionCount { get; set; }
            public string LastFailure { get; set; } = string.Empty;
        }

        private readonly struct Candidate
        {
            public Candidate(
                string puzzleId,
                string canonicalHash,
                Phase3PuzzleStructureSignature signature,
                Phase3PuzzleDefinition puzzle,
                IReadOnlyList<Phase3GeneratedPieceData> generatedPieces,
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

        private readonly struct GenerationAttempt
        {
            private GenerationAttempt(
                bool succeeded,
                ulong effectiveSeed,
                int cycles,
                int backtracking,
                int exhausted,
                int budgetExhaustions,
                int shapeDuplicateRejections,
                Candidate candidate,
                string failure)
            {
                Succeeded = succeeded;
                EffectiveSeed = effectiveSeed;
                GenerationCycles = cycles;
                BacktrackingCount = backtracking;
                ExhaustedCandidateCount = exhausted;
                BacktrackingBudgetExhaustionCount = budgetExhaustions;
                CurrentShapeDuplicateRejectionCount = shapeDuplicateRejections;
                Candidate = candidate;
                FailureReason = failure ?? string.Empty;
            }
            public bool Succeeded { get; }
            public ulong EffectiveSeed { get; }
            public int GenerationCycles { get; }
            public int BacktrackingCount { get; }
            public int ExhaustedCandidateCount { get; }
            public int BacktrackingBudgetExhaustionCount { get; }
            public int CurrentShapeDuplicateRejectionCount { get; }
            public Candidate Candidate { get; }
            public string FailureReason { get; }
            public static GenerationAttempt Success(
                ulong seed, int cycles, int backtracking, int exhausted, int budgetExhaustions,
                int shapeDuplicateRejections, Candidate candidate) =>
                new GenerationAttempt(
                    true, seed, cycles, backtracking, exhausted, budgetExhaustions,
                    shapeDuplicateRejections, candidate, string.Empty);
            public static GenerationAttempt Failed(
                ulong seed, string failure, int backtracking, int exhausted,
                int budgetExhaustions, int shapeDuplicateRejections, int cycles) =>
                new GenerationAttempt(
                    false, seed, cycles, backtracking, exhausted, budgetExhaustions,
                    shapeDuplicateRejections, default, failure);
        }

        private sealed class ShapeHistoryProfile
        {
            private readonly HashSet<string> previous = new HashSet<string>(StringComparer.Ordinal);
            private readonly Dictionary<string, int> olderOccurrences = new Dictionary<string, int>(StringComparer.Ordinal);

            public static ShapeHistoryProfile Empty { get; } = new ShapeHistoryProfile(null);

            public ShapeHistoryProfile(IReadOnlyList<IReadOnlyList<string>> history)
            {
                if (history == null || history.Count == 0) return;
                int first = Math.Max(0, history.Count - 5);
                int previousIndex = history.Count - 1;
                for (int puzzleIndex = first; puzzleIndex < history.Count; puzzleIndex++)
                {
                    IReadOnlyList<string> signatures = history[puzzleIndex];
                    for (int signatureIndex = 0; signatureIndex < signatures.Count; signatureIndex++)
                    {
                        string signature = signatures[signatureIndex];
                        if (puzzleIndex == previousIndex) previous.Add(signature);
                        else
                        {
                            olderOccurrences.TryGetValue(signature, out int count);
                            olderOccurrences[signature] = count + 1;
                        }
                    }
                }
            }

            public bool IsForbiddenByPreviousPuzzle(string signature) => previous.Contains(signature);

            public int WeightFor(string signature)
            {
                if (!olderOccurrences.TryGetValue(signature, out int count)) return 100;
                if (count == 1) return 60;
                if (count == 2) return 35;
                return 15;
            }
        }

        private sealed class DeterministicRandom
        {
            private ulong state;
            public DeterministicRandom(ulong seed) { state = seed == 0UL ? 0x9E3779B97F4A7C15UL : seed; }
            public ulong NextUInt64()
            {
                state += 0x9E3779B97F4A7C15UL;
                return Mix(state);
            }
            public int NextInt(int maximumExclusive)
            {
                if (maximumExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maximumExclusive));
                return (int)(NextUInt64() % (uint)maximumExclusive);
            }
        }
    }
}
