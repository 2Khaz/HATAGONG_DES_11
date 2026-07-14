using System;
using System.Collections.Generic;
using System.Globalization;
using HATAGONG.GameFlow;

namespace HATAGONG.Phase3
{
    public enum Phase3GeneratedShapeKind
    {
        RightTriangle = 0,
        Triangle = RightTriangle,
        Trapezoid = 1,
        Quadrilateral = Trapezoid,
        Rectangle,
        Square,
        Parallelogram,
        Rhombus
    }

    public sealed class Phase3GeneratedPieceData
    {
        public Phase3GeneratedPieceData(
            string pieceId,
            string slotId,
            IEnumerable<Phase3GridPoint> vertices,
            Phase3GeneratedShapeKind shapeKind,
            int rotationalSymmetryPeriodSteps,
            Phase3RotationStep targetRotation,
            Phase3RotationStep initialRotation)
        {
            if (string.IsNullOrWhiteSpace(pieceId)) throw new ArgumentException("Piece ID is required.", nameof(pieceId));
            if (string.IsNullOrWhiteSpace(slotId)) throw new ArgumentException("Slot ID is required.", nameof(slotId));
            if (vertices == null) throw new ArgumentNullException(nameof(vertices));
            Phase3RotationStep.ValidateSymmetryPeriod(rotationalSymmetryPeriodSteps);
            IReadOnlyList<Phase3GridPoint> canonical = Phase3Geometry.CanonicalizeVertices(NormalizeInputVertices(vertices));
            var copiedVertices = new Phase3GridPoint[canonical.Count];
            for (int i = 0; i < canonical.Count; i++) copiedVertices[i] = canonical[i];

            PieceId = pieceId;
            SlotId = slotId;
            Vertices = Array.AsReadOnly(copiedVertices);
            ShapeKind = shapeKind;
            RotationalSymmetryPeriodSteps = rotationalSymmetryPeriodSteps;
            TargetRotation = targetRotation;
            InitialRotation = initialRotation;
            ShapeSignature = Phase3PuzzleGenerator.ComputeShapeSignature(Vertices);
        }

        public string PieceId { get; }
        public string SlotId { get; }
        public IReadOnlyList<Phase3GridPoint> Vertices { get; }
        public Phase3GeneratedShapeKind ShapeKind { get; }
        public int RotationalSymmetryPeriodSteps { get; }
        public Phase3RotationStep TargetRotation { get; }
        public Phase3RotationStep InitialRotation { get; }
        public string ShapeSignature { get; }
        public double Area => Phase3Geometry.AbsoluteArea(Vertices);

        private static IReadOnlyList<Phase3GridPoint> NormalizeInputVertices(IEnumerable<Phase3GridPoint> vertices)
        {
            var normalized = new List<Phase3GridPoint>();
            foreach (Phase3GridPoint vertex in vertices)
            {
                if (normalized.Count == 0 || normalized[normalized.Count - 1] != vertex) normalized.Add(vertex);
            }
            if (normalized.Count > 1 && normalized[normalized.Count - 1] == normalized[0]) normalized.RemoveAt(normalized.Count - 1);
            return normalized;
        }
    }

    public sealed class Phase3PuzzleGeneratorDifficultyConfig
    {
        private Phase3PuzzleGeneratorDifficultyConfig(
            GameDifficulty difficulty,
            int pieceCount,
            double minimumPieceArea,
            double minimumInteriorAngleDegrees,
            double minimumThickness,
            double minimumEdgeLength,
            int cycleBudget,
            int backtrackingBudget,
            Phase3DifficultyRuleSet partitionRules)
        {
            Difficulty = difficulty;
            PieceCount = pieceCount;
            MinimumPieceArea = minimumPieceArea;
            MinimumInteriorAngleDegrees = minimumInteriorAngleDegrees;
            MinimumThickness = minimumThickness;
            MinimumEdgeLength = minimumEdgeLength;
            CycleBudget = cycleBudget;
            BacktrackingBudget = backtrackingBudget;
            PartitionRules = partitionRules;
        }

        public GameDifficulty Difficulty { get; }
        public int PieceCount { get; }
        public double MinimumPieceArea { get; }
        public double MinimumInteriorAngleDegrees { get; }
        public double MinimumThickness { get; }
        public double MinimumEdgeLength { get; }
        public int CycleBudget { get; }
        public int BacktrackingBudget { get; }
        public Phase3DifficultyRuleSet PartitionRules { get; }

        public static Phase3PuzzleGeneratorDifficultyConfig For(GameDifficulty difficulty)
        {
            switch (difficulty)
            {
                case GameDifficulty.Easy:
                    return Create(difficulty, 8, 14d, 30d, 2.5d, 2d, 32, 16, 45d, 3.5d);
                case GameDifficulty.Normal:
                    return Create(difficulty, 10, 10d, 30d, 2d, 1.5d, 64, 32, 40d, 4d);
                case GameDifficulty.Hard:
                    return Create(difficulty, 12, 7d, 30d, 1.5d, 1d, 96, 48, 35d, 5d);
                default:
                    throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, "Generator difficulty must be Easy, Normal, or Hard.");
            }
        }

        private static Phase3PuzzleGeneratorDifficultyConfig Create(
            GameDifficulty difficulty,
            int pieceCount,
            double minimumArea,
            double minimumAngle,
            double minimumThickness,
            double minimumEdgeLength,
            int cycleBudget,
            int backtrackingBudget,
            double snapDistance,
            double maximumAspectRatio)
        {
            double minimumRatio = minimumArea / Phase3CoreConstants.LogicalFieldArea;
            var rules = new Phase3DifficultyRuleSet(pieceCount, snapDistance, minimumRatio, 0.25d, maximumAspectRatio, 4);
            return new Phase3PuzzleGeneratorDifficultyConfig(
                difficulty, pieceCount, minimumArea, minimumAngle, minimumThickness, minimumEdgeLength,
                cycleBudget, backtrackingBudget, rules);
        }
    }

    public readonly struct Phase3PuzzleStructureSignature : IEquatable<Phase3PuzzleStructureSignature>
    {
        public Phase3PuzzleStructureSignature(
            GameDifficulty difficulty,
            int pieceCount,
            IEnumerable<double> sortedPieceAreas,
            IEnumerable<double> sortedPiecePerimeters,
            double internalBoundaryLength,
            IEnumerable<int> shapeKindCounts)
        {
            if (sortedPieceAreas == null) throw new ArgumentNullException(nameof(sortedPieceAreas));
            if (sortedPiecePerimeters == null) throw new ArgumentNullException(nameof(sortedPiecePerimeters));
            if (shapeKindCounts == null) throw new ArgumentNullException(nameof(shapeKindCounts));
            var areas = new List<double>(sortedPieceAreas);
            var perimeters = new List<double>(sortedPiecePerimeters);
            var kinds = new List<int>(shapeKindCounts);
            areas.Sort();
            perimeters.Sort();
            Difficulty = difficulty;
            PieceCount = pieceCount;
            PieceAreas = areas.AsReadOnly();
            PiecePerimeters = perimeters.AsReadOnly();
            InternalBoundaryLength = internalBoundaryLength;
            ShapeKindCounts = kinds.AsReadOnly();
            Value = $"sig-v2|{(int)difficulty}|{pieceCount}|A:{Join(areas)}|P:{Join(perimeters)}|B:{internalBoundaryLength.ToString("R", CultureInfo.InvariantCulture)}|K:{string.Join(",", kinds)}";
        }

        public GameDifficulty Difficulty { get; }
        public int PieceCount { get; }
        public IReadOnlyList<double> PieceAreas { get; }
        public IReadOnlyList<double> PiecePerimeters { get; }
        public double InternalBoundaryLength { get; }
        public IReadOnlyList<int> ShapeKindCounts { get; }
        public string Value { get; }
        public bool Equals(Phase3PuzzleStructureSignature other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is Phase3PuzzleStructureSignature other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;

        private static string Join(IReadOnlyList<double> values)
        {
            var text = new string[values.Count];
            for (int i = 0; i < values.Count; i++) text[i] = values[i].ToString("R", CultureInfo.InvariantCulture);
            return string.Join(",", text);
        }
    }

    public sealed class Phase3PuzzleGenerationRequest
    {
        public Phase3PuzzleGenerationRequest(
            long seed,
            GameDifficulty difficulty,
            IEnumerable<string> recentCanonicalHashes = null,
            int maximumAttempts = Phase3PuzzleGenerator.DefaultMaximumAttempts,
            IEnumerable<IEnumerable<string>> recentShapeSignaturePuzzles = null)
        {
            if (maximumAttempts < 1 || maximumAttempts > Phase3PuzzleGenerator.MaximumAllowedAttempts)
                throw new ArgumentOutOfRangeException(nameof(maximumAttempts), maximumAttempts, "Maximum attempts must be between 1 and 64.");
            Seed = seed;
            Difficulty = difficulty;
            MaximumAttempts = maximumAttempts;
            var hashes = new List<string>();
            if (recentCanonicalHashes != null)
                foreach (string hash in recentCanonicalHashes)
                    if (!string.IsNullOrWhiteSpace(hash)) hashes.Add(hash.Trim());
            RecentCanonicalHashes = hashes.AsReadOnly();
            var shapeHistory = new List<IReadOnlyList<string>>();
            if (recentShapeSignaturePuzzles != null)
                foreach (IEnumerable<string> puzzleSignatures in recentShapeSignaturePuzzles)
                {
                    var signatures = new List<string>();
                    if (puzzleSignatures != null)
                        foreach (string signature in puzzleSignatures)
                            if (!string.IsNullOrWhiteSpace(signature)) signatures.Add(signature.Trim());
                    shapeHistory.Add(signatures.AsReadOnly());
                }
            RecentShapeSignaturePuzzles = shapeHistory.AsReadOnly();
        }

        public long Seed { get; }
        public GameDifficulty Difficulty { get; }
        public int MaximumAttempts { get; }
        public IReadOnlyList<string> RecentCanonicalHashes { get; }
        public IReadOnlyList<IReadOnlyList<string>> RecentShapeSignaturePuzzles { get; }
    }

    public enum Phase3PuzzleGenerationFailure
    {
        None = 0,
        InvalidDifficulty,
        DuplicateHistoryExhausted,
        AttemptsExhausted
    }

    public sealed class Phase3PuzzleGenerationResult
    {
        private Phase3PuzzleGenerationResult(
            bool succeeded,
            Phase3PuzzleGenerationFailure failure,
            string failureReason,
            long requestedSeed,
            ulong effectiveSeed,
            int attemptIndex,
            GameDifficulty difficulty,
            int attemptsUsed,
            string puzzleId,
            string canonicalHash,
            Phase3PuzzleStructureSignature signature,
            Phase3PuzzleDefinition puzzle,
            IEnumerable<Phase3GeneratedPieceData> generatedPieces,
            IEnumerable<Phase3InitialPieceRotation> initialRotations)
            : this(
                succeeded, failure, failureReason, requestedSeed, effectiveSeed, attemptIndex, difficulty,
                attemptsUsed, puzzleId, canonicalHash, signature, puzzle, generatedPieces, initialRotations,
                0, 0, 0, 0, 0, 0)
        {
        }

        private Phase3PuzzleGenerationResult(
            bool succeeded,
            Phase3PuzzleGenerationFailure failure,
            string failureReason,
            long requestedSeed,
            ulong effectiveSeed,
            int attemptIndex,
            GameDifficulty difficulty,
            int attemptsUsed,
            string puzzleId,
            string canonicalHash,
            Phase3PuzzleStructureSignature signature,
            Phase3PuzzleDefinition puzzle,
            IEnumerable<Phase3GeneratedPieceData> generatedPieces,
            IEnumerable<Phase3InitialPieceRotation> initialRotations,
            int generationCycles,
            int backtrackingCount,
            int exhaustedCandidateCount,
            int backtrackingBudgetExhaustionCount,
            int currentShapeDuplicateRejectionCount,
            int exactHistoryRejectionCount)
        {
            Succeeded = succeeded;
            Failure = failure;
            FailureReason = failureReason ?? string.Empty;
            RequestedSeed = requestedSeed;
            EffectiveSeed = effectiveSeed;
            AttemptIndex = attemptIndex;
            Difficulty = difficulty;
            AttemptsUsed = attemptsUsed;
            PuzzleId = puzzleId ?? string.Empty;
            CanonicalHash = canonicalHash ?? string.Empty;
            Signature = signature;
            Puzzle = puzzle;
            GeneratedPieces = Array.AsReadOnly(generatedPieces == null ? Array.Empty<Phase3GeneratedPieceData>() : new List<Phase3GeneratedPieceData>(generatedPieces).ToArray());
            InitialRotations = Array.AsReadOnly(initialRotations == null ? Array.Empty<Phase3InitialPieceRotation>() : new List<Phase3InitialPieceRotation>(initialRotations).ToArray());
            GenerationCycles = generationCycles;
            BacktrackingCount = backtrackingCount;
            ExhaustedCandidateCount = exhaustedCandidateCount;
            BacktrackingBudgetExhaustionCount = backtrackingBudgetExhaustionCount;
            CurrentShapeDuplicateRejectionCount = currentShapeDuplicateRejectionCount;
            ExactHistoryRejectionCount = exactHistoryRejectionCount;
        }

        public bool Succeeded { get; }
        public Phase3PuzzleGenerationFailure Failure { get; }
        public string FailureReason { get; }
        public string GeneratorVersion => Phase3PuzzleGenerator.GeneratorVersion;
        public long RequestedSeed { get; }
        public long Seed => RequestedSeed;
        public ulong EffectiveSeed { get; }
        public int AttemptIndex { get; }
        public GameDifficulty Difficulty { get; }
        public int AttemptsUsed { get; }
        public string PuzzleId { get; }
        public string CanonicalHash { get; }
        public Phase3PuzzleStructureSignature Signature { get; }
        public Phase3PuzzleDefinition Puzzle { get; }
        public IReadOnlyList<Phase3GeneratedPieceData> GeneratedPieces { get; }
        public IReadOnlyList<Phase3InitialPieceRotation> InitialRotations { get; }
        public int GenerationCycles { get; }
        public int BacktrackingCount { get; }
        public int ExhaustedCandidateCount { get; }
        public int BacktrackingBudgetExhaustionCount { get; }
        public int CurrentShapeDuplicateRejectionCount { get; }
        public int ExactHistoryRejectionCount { get; }

        internal static Phase3PuzzleGenerationResult Success(
            long requestedSeed,
            ulong effectiveSeed,
            int attemptIndex,
            GameDifficulty difficulty,
            int attemptsUsed,
            string puzzleId,
            string canonicalHash,
            Phase3PuzzleStructureSignature signature,
            Phase3PuzzleDefinition puzzle,
            IEnumerable<Phase3GeneratedPieceData> generatedPieces,
            IEnumerable<Phase3InitialPieceRotation> initialRotations,
            int generationCycles = 0,
            int backtrackingCount = 0,
            int exhaustedCandidateCount = 0,
            int backtrackingBudgetExhaustionCount = 0,
            int currentShapeDuplicateRejectionCount = 0,
            int exactHistoryRejectionCount = 0) =>
            new Phase3PuzzleGenerationResult(
                true, Phase3PuzzleGenerationFailure.None, string.Empty, requestedSeed, effectiveSeed, attemptIndex,
                difficulty, attemptsUsed, puzzleId, canonicalHash, signature, puzzle, generatedPieces, initialRotations,
                generationCycles, backtrackingCount, exhaustedCandidateCount,
                backtrackingBudgetExhaustionCount, currentShapeDuplicateRejectionCount, exactHistoryRejectionCount);

        internal static Phase3PuzzleGenerationResult Failed(
            Phase3PuzzleGenerationFailure failure,
            string reason,
            long seed,
            GameDifficulty difficulty,
            int attemptsUsed) =>
            new Phase3PuzzleGenerationResult(false, failure, reason, seed, 0UL, -1, difficulty, attemptsUsed, string.Empty, string.Empty, default, null, null, null);
    }
}
