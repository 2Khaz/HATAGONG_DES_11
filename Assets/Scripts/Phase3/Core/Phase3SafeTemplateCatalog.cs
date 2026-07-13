using System;
using System.Collections.Generic;
using HATAGONG.GameFlow;

namespace HATAGONG.Phase3
{
    public readonly struct Phase3InitialPieceRotation
    {
        public Phase3InitialPieceRotation(string pieceId, Phase3RotationStep rotation)
        {
            if (string.IsNullOrWhiteSpace(pieceId))
            {
                throw new ArgumentException("Piece ID cannot be null or whitespace.", nameof(pieceId));
            }

            PieceId = pieceId;
            Rotation = rotation;
        }

        public string PieceId { get; }
        public Phase3RotationStep Rotation { get; }
    }

    public sealed class Phase3SafeTemplate
    {
        internal Phase3SafeTemplate(
            string templateId,
            GameDifficulty difficulty,
            Phase3PuzzleDefinition puzzle,
            IEnumerable<Phase3InitialPieceRotation> initialRotations)
        {
            if (string.IsNullOrWhiteSpace(templateId))
            {
                throw new ArgumentException("Template ID cannot be null or whitespace.", nameof(templateId));
            }

            TemplateId = templateId;
            Difficulty = difficulty;
            Puzzle = puzzle ?? throw new ArgumentNullException(nameof(puzzle));
            if (initialRotations == null)
            {
                throw new ArgumentNullException(nameof(initialRotations));
            }

            var rotations = new List<Phase3InitialPieceRotation>(initialRotations);
            rotations.Sort((left, right) => string.CompareOrdinal(left.PieceId, right.PieceId));
            if (rotations.Count != puzzle.Pieces.Count)
            {
                throw new ArgumentException("Every piece requires exactly one initial rotation.", nameof(initialRotations));
            }

            for (int i = 0; i < rotations.Count; i++)
            {
                if (i > 0 && string.Equals(rotations[i - 1].PieceId, rotations[i].PieceId, StringComparison.Ordinal))
                {
                    throw new ArgumentException($"Duplicate initial rotation for piece '{rotations[i].PieceId}'.", nameof(initialRotations));
                }

                if (!ContainsPiece(puzzle.Pieces, rotations[i].PieceId))
                {
                    throw new ArgumentException($"Initial rotation references missing piece '{rotations[i].PieceId}'.", nameof(initialRotations));
                }
            }

            InitialRotations = rotations.AsReadOnly();
        }

        public string TemplateId { get; }
        public GameDifficulty Difficulty { get; }
        public Phase3PuzzleDefinition Puzzle { get; }
        public IReadOnlyList<Phase3InitialPieceRotation> InitialRotations { get; }

        public Phase3RotationStep GetInitialRotation(string pieceId)
        {
            int low = 0;
            int high = InitialRotations.Count - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) / 2);
                int comparison = string.CompareOrdinal(InitialRotations[middle].PieceId, pieceId);
                if (comparison == 0)
                {
                    return InitialRotations[middle].Rotation;
                }

                if (comparison < 0)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle - 1;
                }
            }

            throw new ArgumentException($"Piece '{pieceId}' has no initial rotation.", nameof(pieceId));
        }

        private static bool ContainsPiece(IReadOnlyList<Phase3PieceDefinition> pieces, string pieceId)
        {
            for (int i = 0; i < pieces.Count; i++)
            {
                if (string.Equals(pieces[i].PieceId, pieceId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public static class Phase3SafeTemplateCatalog
    {
        public static Phase3SafeTemplate GetDefault(GameDifficulty difficulty)
        {
            switch (difficulty)
            {
                case GameDifficulty.Easy:
                    return BuildEasy();
                case GameDifficulty.Normal:
                    return BuildNormal();
                case GameDifficulty.Hard:
                    return BuildHard();
                default:
                    throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, "Safe templates exist only for Easy, Normal, and Hard.");
            }
        }

        public static IReadOnlyList<Phase3SafeTemplate> GetTemplates(GameDifficulty difficulty)
        {
            return Array.AsReadOnly(new[] { GetDefault(difficulty) });
        }

        private static Phase3SafeTemplate BuildEasy()
        {
            var triangle = CreateShape("easy-shape-triangle-8", Triangle8(), 8);
            var square = CreateShape("easy-shape-square-8", Square8(), 2);
            var regions = new[]
            {
                new Region("easy-slot-01", "triangle", new[] { P(0, 0), P(8, 0), P(8, 8) }, 0),
                new Region("easy-slot-02", "triangle", new[] { P(0, 0), P(8, 8), P(0, 8) }, 2, 2),
                new Region("easy-slot-03", "triangle", new[] { P(8, 0), P(16, 0), P(16, 8) }, 0),
                new Region("easy-slot-04", "triangle", new[] { P(8, 0), P(16, 8), P(8, 8) }, 2, 2),
                new Region("easy-slot-05", "square", new[] { P(0, 8), P(8, 8), P(8, 16), P(0, 16) }, 0),
                new Region("easy-slot-06", "square", new[] { P(8, 8), P(16, 8), P(16, 16), P(8, 16) }, 0)
            };
            var shapes = new Dictionary<string, Phase3ShapeDefinition>(StringComparer.Ordinal)
            {
                { "triangle", triangle }, { "square", square }
            };
            int[] initialSteps = { 0, 2, 0, 2, 0, 2 };
            return BuildTemplate("phase3-safe-easy-01", GameDifficulty.Easy, regions, shapes, initialSteps);
        }

        private static Phase3SafeTemplate BuildNormal()
        {
            var triangle = CreateShape("normal-shape-triangle-8", Triangle8(), 8);
            var square = CreateShape("normal-shape-square-8", Square8(), 2);
            var regions = new[]
            {
                new Region("normal-slot-01", "triangle", new[] { P(0, 0), P(8, 0), P(8, 8) }, 0),
                new Region("normal-slot-02", "triangle", new[] { P(0, 0), P(8, 8), P(0, 8) }, 2, 2),
                new Region("normal-slot-03", "triangle", new[] { P(8, 0), P(16, 0), P(16, 8) }, 0),
                new Region("normal-slot-04", "triangle", new[] { P(8, 0), P(16, 8), P(8, 8) }, 2, 2),
                new Region("normal-slot-05", "triangle", new[] { P(0, 8), P(8, 8), P(8, 16) }, 0),
                new Region("normal-slot-06", "triangle", new[] { P(0, 8), P(8, 16), P(0, 16) }, 2, 2),
                new Region("normal-slot-07", "square", new[] { P(8, 8), P(16, 8), P(16, 16), P(8, 16) }, 0)
            };
            var shapes = new Dictionary<string, Phase3ShapeDefinition>(StringComparer.Ordinal)
            {
                { "triangle", triangle }, { "square", square }
            };
            int[] initialSteps = { 0, 1, 2, 3, 4, 5, 6 };
            return BuildTemplate("phase3-safe-normal-01", GameDifficulty.Normal, regions, shapes, initialSteps);
        }

        private static Phase3SafeTemplate BuildHard()
        {
            var triangle = CreateShape("hard-shape-triangle-8", Triangle8(), 8);
            var rectangle = CreateShape("hard-shape-rectangle-4x8", Rectangle4By8(), 4);
            var regions = new[]
            {
                new Region("hard-slot-01", "rectangle", new[] { P(0, 0), P(4, 0), P(4, 8), P(0, 8) }, 0),
                new Region("hard-slot-02", "rectangle", new[] { P(0, 8), P(4, 8), P(4, 16), P(0, 16) }, 0),
                new Region("hard-slot-03", "triangle", new[] { P(4, 0), P(12, 0), P(12, 8) }, 0),
                new Region("hard-slot-04", "triangle", new[] { P(4, 0), P(12, 8), P(4, 8) }, 2, 2),
                new Region("hard-slot-05", "triangle", new[] { P(4, 8), P(12, 8), P(12, 16) }, 0),
                new Region("hard-slot-06", "triangle", new[] { P(4, 8), P(12, 16), P(4, 16) }, 2, 2),
                new Region("hard-slot-07", "rectangle", new[] { P(12, 0), P(16, 0), P(16, 8), P(12, 8) }, 0),
                new Region("hard-slot-08", "rectangle", new[] { P(12, 8), P(16, 8), P(16, 16), P(12, 16) }, 0)
            };
            var shapes = new Dictionary<string, Phase3ShapeDefinition>(StringComparer.Ordinal)
            {
                { "triangle", triangle }, { "rectangle", rectangle }
            };
            int[] initialSteps = { 0, 1, 2, 3, 4, 5, 6, 7 };
            return BuildTemplate("phase3-safe-hard-01", GameDifficulty.Hard, regions, shapes, initialSteps);
        }

        private static Phase3SafeTemplate BuildTemplate(
            string templateId,
            GameDifficulty difficulty,
            IReadOnlyList<Region> regions,
            IReadOnlyDictionary<string, Phase3ShapeDefinition> inventoryShapes,
            IReadOnlyList<int> initialSteps)
        {
            var slots = new List<Phase3SlotDefinition>(regions.Count);
            var groupTargets = new Dictionary<string, List<Phase3AllowedTarget>>(StringComparer.Ordinal);
            for (int i = 0; i < regions.Count; i++)
            {
                Region region = regions[i];
                var slotShape = CreateShape($"{templateId}-{region.SlotId}-shape", region.Vertices, inventoryShapes[region.Group].RotationalSymmetryPeriodSteps);
                var required = new Phase3RotationStep(region.RequiredRotationStep);
                slots.Add(new Phase3SlotDefinition(region.SlotId, slotShape, ToCanvasCentroid(slotShape.Centroid), required));
                if (!groupTargets.TryGetValue(region.Group, out List<Phase3AllowedTarget> targets))
                {
                    targets = new List<Phase3AllowedTarget>();
                    groupTargets.Add(region.Group, targets);
                }

                targets.Add(new Phase3AllowedTarget(
                    region.SlotId,
                    required,
                    new Phase3RotationStep(region.RotationCorrectionStep)));
            }

            var pieces = new List<Phase3PieceDefinition>(regions.Count);
            var rotations = new List<Phase3InitialPieceRotation>(regions.Count);
            var groupNumbers = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < regions.Count; i++)
            {
                string group = regions[i].Group;
                groupNumbers.TryGetValue(group, out int groupNumber);
                groupNumber++;
                groupNumbers[group] = groupNumber;
                string pieceId = $"{difficulty.ToString().ToLowerInvariant()}-piece-{group}-{groupNumber:D2}";
                string deckSlotId = $"{difficulty.ToString().ToLowerInvariant()}-deck-{i + 1:D2}";
                pieces.Add(new Phase3PieceDefinition(pieceId, deckSlotId, inventoryShapes[group], groupTargets[group]));
                rotations.Add(new Phase3InitialPieceRotation(pieceId, new Phase3RotationStep(initialSteps[i])));
            }

            var puzzle = new Phase3PuzzleDefinition(pieces, slots);
            Phase3PartitionValidationResult validation = Phase3PartitionValidator.Validate(puzzle, difficulty);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException($"Safe template '{templateId}' is invalid: {validation.Issues[0]}");
            }

            return new Phase3SafeTemplate(templateId, difficulty, puzzle, rotations);
        }

        private static Phase3ShapeDefinition CreateShape(string shapeId, IEnumerable<Phase3GridPoint> vertices, int symmetryPeriod)
        {
            return new Phase3ShapeDefinition(shapeId, vertices, symmetryPeriod);
        }

        private static Phase3GridPoint[] Triangle8() => new[] { P(0, 0), P(8, 0), P(8, 8) };
        private static Phase3GridPoint[] Square8() => new[] { P(0, 0), P(8, 0), P(8, 8), P(0, 8) };
        private static Phase3GridPoint[] Rectangle4By8() => new[] { P(0, 0), P(4, 0), P(4, 8), P(0, 8) };
        private static Phase3GridPoint P(int x, int y) => new Phase3GridPoint(x, y);

        private static Phase3Point2D ToCanvasCentroid(Phase3Point2D logicalCentroid)
        {
            double scale = Phase3CoreConstants.CanvasFieldSize / (double)Phase3CoreConstants.LogicalGridSize;
            return logicalCentroid * scale;
        }

        private readonly struct Region
        {
            public Region(
                string slotId,
                string group,
                IReadOnlyList<Phase3GridPoint> vertices,
                int requiredRotationStep,
                int rotationCorrectionStep = 0)
            {
                SlotId = slotId;
                Group = group;
                Vertices = vertices;
                RequiredRotationStep = requiredRotationStep;
                RotationCorrectionStep = rotationCorrectionStep;
            }

            public string SlotId { get; }
            public string Group { get; }
            public IReadOnlyList<Phase3GridPoint> Vertices { get; }
            public int RequiredRotationStep { get; }
            public int RotationCorrectionStep { get; }
        }
    }
}
