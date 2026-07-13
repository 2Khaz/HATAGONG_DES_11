using System;
using System.Collections.Generic;
using HATAGONG.GameFlow;

namespace HATAGONG.Phase3
{
    public enum Phase3PartitionFailure
    {
        None = 0,
        InvalidDifficulty,
        PieceCountMismatch,
        SlotCountMismatch,
        PieceSlotCountMismatch,
        InvalidPieceShape,
        InvalidSlotShape,
        ShapeOutsideField,
        PieceAreaRatioOutOfRange,
        SlotAreaRatioOutOfRange,
        AspectRatioExceeded,
        VertexCountExceeded,
        PieceAreaSumMismatch,
        SlotAreaSumMismatch,
        PieceSlotAreaMismatch,
        PieceSlotShapeMismatch,
        ProperEdgeCrossing,
        InteriorOverlap,
        UntargetedSlot,
        MissingAllowedTarget,
        NoCompletePieceSlotMatching
    }

    public readonly struct Phase3PartitionIssue
    {
        public Phase3PartitionIssue(Phase3PartitionFailure failure, string subjectId, string message)
        {
            Failure = failure;
            SubjectId = subjectId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public Phase3PartitionFailure Failure { get; }
        public string SubjectId { get; }
        public string Message { get; }
        public override string ToString() => $"{Failure}: {SubjectId}: {Message}";
    }

    public sealed class Phase3PartitionValidationResult
    {
        internal Phase3PartitionValidationResult(
            IEnumerable<Phase3PartitionIssue> issues,
            long pieceSignedDoubleAreaSum,
            long slotSignedDoubleAreaSum,
            double minimumPieceAreaRatio,
            double maximumPieceAreaRatio,
            double maximumAspectRatio,
            int maximumVertexCount)
        {
            var copiedIssues = new List<Phase3PartitionIssue>(issues);
            Issues = copiedIssues.AsReadOnly();
            PieceSignedDoubleAreaSum = pieceSignedDoubleAreaSum;
            SlotSignedDoubleAreaSum = slotSignedDoubleAreaSum;
            MinimumPieceAreaRatio = minimumPieceAreaRatio;
            MaximumPieceAreaRatio = maximumPieceAreaRatio;
            MaximumAspectRatio = maximumAspectRatio;
            MaximumVertexCount = maximumVertexCount;
        }

        public bool IsValid => Issues.Count == 0;
        public IReadOnlyList<Phase3PartitionIssue> Issues { get; }
        public long PieceSignedDoubleAreaSum { get; }
        public long SlotSignedDoubleAreaSum { get; }
        public double PieceAreaSum => PieceSignedDoubleAreaSum * 0.5d;
        public double SlotAreaSum => SlotSignedDoubleAreaSum * 0.5d;
        public double MinimumPieceAreaRatio { get; }
        public double MaximumPieceAreaRatio { get; }
        public double MaximumAspectRatio { get; }
        public int MaximumVertexCount { get; }

        public bool HasFailure(Phase3PartitionFailure failure)
        {
            for (int i = 0; i < Issues.Count; i++)
            {
                if (Issues[i].Failure == failure)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public static class Phase3PartitionValidator
    {
        private const long ExpectedFieldSignedDoubleArea = Phase3CoreConstants.LogicalFieldArea * 2L;

        public static Phase3PartitionValidationResult Validate(Phase3PuzzleDefinition puzzle, GameDifficulty difficulty)
        {
            if (puzzle == null)
            {
                throw new ArgumentNullException(nameof(puzzle));
            }

            Phase3DifficultyRuleSet rules;
            try
            {
                rules = Phase3DifficultyRules.For(difficulty);
            }
            catch (ArgumentOutOfRangeException)
            {
                return CreateSingleFailure(Phase3PartitionFailure.InvalidDifficulty, difficulty.ToString(), "Difficulty must be Easy, Normal, or Hard.");
            }

            var issues = new List<Phase3PartitionIssue>();
            if (puzzle.Pieces.Count != rules.TargetPieceCount)
            {
                AddIssue(issues, Phase3PartitionFailure.PieceCountMismatch, "puzzle", $"Expected {rules.TargetPieceCount} pieces but found {puzzle.Pieces.Count}.");
            }

            if (puzzle.Slots.Count != rules.TargetPieceCount)
            {
                AddIssue(issues, Phase3PartitionFailure.SlotCountMismatch, "puzzle", $"Expected {rules.TargetPieceCount} slots but found {puzzle.Slots.Count}.");
            }

            if (puzzle.Pieces.Count != puzzle.Slots.Count)
            {
                AddIssue(issues, Phase3PartitionFailure.PieceSlotCountMismatch, "puzzle", "Piece and slot counts must match.");
            }

            long pieceAreaSum = 0L;
            long slotAreaSum = 0L;
            double minimumPieceRatio = double.PositiveInfinity;
            double maximumPieceRatio = 0d;
            double maximumAspectRatio = 0d;
            int maximumVertexCount = 0;

            for (int i = 0; i < puzzle.Pieces.Count; i++)
            {
                Phase3PieceDefinition piece = puzzle.Pieces[i];
                Phase3ShapeDefinition shape = piece.ShapeDefinition;
                Phase3PolygonValidationResult shapeValidation = Phase3Geometry.ValidatePolygon(shape.Vertices);
                if (!shapeValidation.IsValid)
                {
                    AddIssue(issues, Phase3PartitionFailure.InvalidPieceShape, piece.PieceId, shapeValidation.Message);
                    continue;
                }

                long doubleArea = Math.Abs(Phase3Geometry.SignedDoubleArea(shape.Vertices));
                pieceAreaSum += doubleArea;
                double ratio = doubleArea / (double)ExpectedFieldSignedDoubleArea;
                minimumPieceRatio = Math.Min(minimumPieceRatio, ratio);
                maximumPieceRatio = Math.Max(maximumPieceRatio, ratio);
                maximumAspectRatio = Math.Max(maximumAspectRatio, shape.AspectRatio);
                maximumVertexCount = Math.Max(maximumVertexCount, shape.Vertices.Count);
                ValidateShapeLimits(issues, piece.PieceId, shape, ratio, rules, true);
                if (piece.AllowedTargets.Count == 0)
                {
                    AddIssue(issues, Phase3PartitionFailure.MissingAllowedTarget, piece.PieceId, "Every piece requires at least one allowed target.");
                }
            }

            var targetedSlots = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < puzzle.Pieces.Count; i++)
            {
                Phase3PieceDefinition piece = puzzle.Pieces[i];
                for (int targetIndex = 0; targetIndex < piece.AllowedTargets.Count; targetIndex++)
                {
                    Phase3AllowedTarget target = piece.AllowedTargets[targetIndex];
                    targetedSlots.Add(target.SlotId);
                    Phase3SlotDefinition slot = FindSlot(puzzle.Slots, target.SlotId);
                    if (slot == null)
                    {
                        continue;
                    }

                    if (Math.Abs(piece.ShapeDefinition.Area - slot.ShapeDefinition.Area) > Phase3CoreConstants.ComparisonEpsilon)
                    {
                        AddIssue(issues, Phase3PartitionFailure.PieceSlotAreaMismatch, $"{piece.PieceId}->{slot.SlotId}", "Allowed piece and slot areas must match.");
                    }
                    else
                    {
                        Phase3RotationStep finalRotation = target.RequiredRotationStep.Add(target.RotationCorrectionStep.Value);
                        if (!ShapesMatchAtRotation(piece.ShapeDefinition, slot.ShapeDefinition, finalRotation))
                        {
                            AddIssue(
                                issues,
                                Phase3PartitionFailure.PieceSlotShapeMismatch,
                                $"{piece.PieceId}->{slot.SlotId}",
                                $"Allowed piece and slot shapes must be congruent at final rotation {finalRotation}.");
                        }
                    }
                }
            }

            for (int i = 0; i < puzzle.Slots.Count; i++)
            {
                Phase3SlotDefinition slot = puzzle.Slots[i];
                Phase3ShapeDefinition shape = slot.ShapeDefinition;
                Phase3PolygonValidationResult shapeValidation = Phase3Geometry.ValidatePolygon(shape.Vertices);
                if (!shapeValidation.IsValid)
                {
                    AddIssue(issues, Phase3PartitionFailure.InvalidSlotShape, slot.SlotId, shapeValidation.Message);
                    continue;
                }

                if (!IsInsideField(shape.Bounds))
                {
                    AddIssue(issues, Phase3PartitionFailure.ShapeOutsideField, slot.SlotId, "Slot shape must remain inside the 0..16 field.");
                }

                long doubleArea = Math.Abs(Phase3Geometry.SignedDoubleArea(shape.Vertices));
                slotAreaSum += doubleArea;
                double ratio = doubleArea / (double)ExpectedFieldSignedDoubleArea;
                ValidateShapeLimits(issues, slot.SlotId, shape, ratio, rules, false);
                if (!targetedSlots.Contains(slot.SlotId))
                {
                    AddIssue(issues, Phase3PartitionFailure.UntargetedSlot, slot.SlotId, "Every slot must be targeted by at least one piece.");
                }
            }

            if (pieceAreaSum != ExpectedFieldSignedDoubleArea)
            {
                AddIssue(issues, Phase3PartitionFailure.PieceAreaSumMismatch, "pieces", $"Expected double area {ExpectedFieldSignedDoubleArea} but found {pieceAreaSum}.");
            }

            if (slotAreaSum != ExpectedFieldSignedDoubleArea)
            {
                AddIssue(issues, Phase3PartitionFailure.SlotAreaSumMismatch, "slots", $"Expected double area {ExpectedFieldSignedDoubleArea} but found {slotAreaSum}.");
            }

            ValidatePairwiseSlotInteriors(puzzle.Slots, issues);
            if (!HasCompleteMatching(puzzle))
            {
                AddIssue(issues, Phase3PartitionFailure.NoCompletePieceSlotMatching, "puzzle", "Allowed targets do not admit a complete one-piece-per-slot matching.");
            }

            if (double.IsPositiveInfinity(minimumPieceRatio))
            {
                minimumPieceRatio = 0d;
            }

            return new Phase3PartitionValidationResult(
                issues,
                pieceAreaSum,
                slotAreaSum,
                minimumPieceRatio,
                maximumPieceRatio,
                maximumAspectRatio,
                maximumVertexCount);
        }

        private static void ValidateShapeLimits(
            ICollection<Phase3PartitionIssue> issues,
            string subjectId,
            Phase3ShapeDefinition shape,
            double areaRatio,
            Phase3DifficultyRuleSet rules,
            bool isPiece)
        {
            if (areaRatio < rules.MinimumPieceAreaRatio - Phase3CoreConstants.ComparisonEpsilon ||
                areaRatio > rules.MaximumPieceAreaRatio + Phase3CoreConstants.ComparisonEpsilon)
            {
                AddIssue(issues, isPiece ? Phase3PartitionFailure.PieceAreaRatioOutOfRange : Phase3PartitionFailure.SlotAreaRatioOutOfRange, subjectId, $"Area ratio {areaRatio:R} is outside difficulty limits.");
            }

            if (shape.AspectRatio > rules.MaximumAspectRatio + Phase3CoreConstants.ComparisonEpsilon)
            {
                AddIssue(issues, Phase3PartitionFailure.AspectRatioExceeded, subjectId, $"Aspect ratio {shape.AspectRatio:R} exceeds {rules.MaximumAspectRatio:R}.");
            }

            if (shape.Vertices.Count > rules.MaximumVertexCount)
            {
                AddIssue(issues, Phase3PartitionFailure.VertexCountExceeded, subjectId, $"Vertex count {shape.Vertices.Count} exceeds {rules.MaximumVertexCount}.");
            }
        }

        private static void ValidatePairwiseSlotInteriors(
            IReadOnlyList<Phase3SlotDefinition> slots,
            ICollection<Phase3PartitionIssue> issues)
        {
            for (int first = 0; first < slots.Count; first++)
            {
                for (int second = first + 1; second < slots.Count; second++)
                {
                    Phase3ShapeDefinition firstShape = slots[first].ShapeDefinition;
                    Phase3ShapeDefinition secondShape = slots[second].ShapeDefinition;
                    if (!BoundsHavePositiveAreaIntersection(firstShape.Bounds, secondShape.Bounds))
                    {
                        continue;
                    }

                    if (HasProperEdgeCrossing(firstShape.Vertices, secondShape.Vertices))
                    {
                        AddIssue(issues, Phase3PartitionFailure.ProperEdgeCrossing, $"{slots[first].SlotId}|{slots[second].SlotId}", "Slot boundaries cross through each other.");
                        continue;
                    }

                    if (ConvexIntersectionArea(firstShape.Vertices, secondShape.Vertices) > Phase3CoreConstants.ComparisonEpsilon)
                    {
                        AddIssue(issues, Phase3PartitionFailure.InteriorOverlap, $"{slots[first].SlotId}|{slots[second].SlotId}", "Slot interiors overlap.");
                    }
                }
            }
        }

        private static bool ShapesMatchAtRotation(
            Phase3ShapeDefinition piece,
            Phase3ShapeDefinition slot,
            Phase3RotationStep finalRotation)
        {
            if (piece.Vertices.Count != slot.Vertices.Count)
            {
                return false;
            }

            EdgeSignature[] pieceEdges = CreateEdgeSignatures(piece.Vertices);
            EdgeSignature[] slotEdges = CreateEdgeSignatures(slot.Vertices);
            for (int offset = 0; offset < slotEdges.Length; offset++)
            {
                bool matches = true;
                for (int edgeIndex = 0; edgeIndex < pieceEdges.Length; edgeIndex++)
                {
                    EdgeSignature rotatedPiece = pieceEdges[edgeIndex].Rotate(finalRotation.Value);
                    EdgeSignature slotEdge = slotEdges[(offset + edgeIndex) % slotEdges.Length];
                    if (!rotatedPiece.Equals(slotEdge))
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches)
                {
                    return true;
                }
            }

            return false;
        }

        private static EdgeSignature[] CreateEdgeSignatures(IReadOnlyList<Phase3GridPoint> vertices)
        {
            var signatures = new EdgeSignature[vertices.Count];
            for (int i = 0; i < vertices.Count; i++)
            {
                Phase3GridPoint start = vertices[i];
                Phase3GridPoint end = vertices[(i + 1) % vertices.Count];
                long deltaX = (long)end.X - start.X;
                long deltaY = (long)end.Y - start.Y;
                signatures[i] = new EdgeSignature(DirectionIndex(deltaX, deltaY), deltaX * deltaX + deltaY * deltaY);
            }

            return signatures;
        }

        private static int DirectionIndex(long deltaX, long deltaY)
        {
            if (deltaX > 0L)
            {
                return deltaY > 0L ? 1 : deltaY < 0L ? 7 : 0;
            }

            if (deltaX < 0L)
            {
                return deltaY > 0L ? 3 : deltaY < 0L ? 5 : 4;
            }

            return deltaY > 0L ? 2 : 6;
        }

        private static double ConvexIntersectionArea(
            IReadOnlyList<Phase3GridPoint> first,
            IReadOnlyList<Phase3GridPoint> second)
        {
            // Clipping in both directions and taking the maximum makes the result
            // independent of which convex polygon is supplied as subject or clip.
            double firstClippedBySecond = ClippedConvexArea(first, second);
            double secondClippedByFirst = ClippedConvexArea(second, first);
            if (!Phase3Point2D.IsFiniteValue(firstClippedBySecond) ||
                !Phase3Point2D.IsFiniteValue(secondClippedByFirst))
            {
                return 0d;
            }

            return Math.Max(firstClippedBySecond, secondClippedByFirst);
        }

        private static double ClippedConvexArea(
            IReadOnlyList<Phase3GridPoint> subject,
            IReadOnlyList<Phase3GridPoint> clip)
        {
            var output = new List<ClipPoint>(subject.Count);
            for (int i = 0; i < subject.Count; i++)
            {
                output.Add(new ClipPoint(subject[i].X, subject[i].Y));
            }

            for (int clipIndex = 0; clipIndex < clip.Count && output.Count > 0; clipIndex++)
            {
                ClipPoint clipStart = new ClipPoint(clip[clipIndex].X, clip[clipIndex].Y);
                Phase3GridPoint clipEndGrid = clip[(clipIndex + 1) % clip.Count];
                ClipPoint clipEnd = new ClipPoint(clipEndGrid.X, clipEndGrid.Y);
                var input = output;
                output = new List<ClipPoint>(input.Count + 1);
                ClipPoint previous = input[input.Count - 1];
                bool previousInside = IsInsideClipEdge(previous, clipStart, clipEnd);

                for (int inputIndex = 0; inputIndex < input.Count; inputIndex++)
                {
                    ClipPoint current = input[inputIndex];
                    bool currentInside = IsInsideClipEdge(current, clipStart, clipEnd);
                    if (currentInside)
                    {
                        if (!previousInside && TryLineIntersection(previous, current, clipStart, clipEnd, out ClipPoint entering))
                        {
                            output.Add(entering);
                        }

                        output.Add(current);
                    }
                    else if (previousInside && TryLineIntersection(previous, current, clipStart, clipEnd, out ClipPoint leaving))
                    {
                        output.Add(leaving);
                    }

                    previous = current;
                    previousInside = currentInside;
                }
            }

            return AbsoluteArea(output);
        }

        private static bool IsInsideClipEdge(ClipPoint point, ClipPoint edgeStart, ClipPoint edgeEnd)
        {
            return Cross(edgeStart, edgeEnd, point) >= -Phase3CoreConstants.ComparisonEpsilon;
        }

        private static bool TryLineIntersection(
            ClipPoint segmentStart,
            ClipPoint segmentEnd,
            ClipPoint edgeStart,
            ClipPoint edgeEnd,
            out ClipPoint intersection)
        {
            double startDistance = Cross(edgeStart, edgeEnd, segmentStart);
            double endDistance = Cross(edgeStart, edgeEnd, segmentEnd);
            double denominator = startDistance - endDistance;
            if (!Phase3Point2D.IsFiniteValue(denominator) ||
                Math.Abs(denominator) <= Phase3CoreConstants.ComparisonEpsilon)
            {
                intersection = default;
                return false;
            }

            double ratio = startDistance / denominator;
            double x = segmentStart.X + (segmentEnd.X - segmentStart.X) * ratio;
            double y = segmentStart.Y + (segmentEnd.Y - segmentStart.Y) * ratio;
            if (!Phase3Point2D.IsFiniteValue(x) || !Phase3Point2D.IsFiniteValue(y))
            {
                intersection = default;
                return false;
            }

            intersection = new ClipPoint(x, y);
            return true;
        }

        private static double AbsoluteArea(IReadOnlyList<ClipPoint> polygon)
        {
            if (polygon.Count < 3)
            {
                return 0d;
            }

            double twiceArea = 0d;
            for (int i = 0; i < polygon.Count; i++)
            {
                ClipPoint current = polygon[i];
                ClipPoint next = polygon[(i + 1) % polygon.Count];
                twiceArea += current.X * next.Y - next.X * current.Y;
            }

            return Phase3Point2D.IsFiniteValue(twiceArea) ? Math.Abs(twiceArea) * 0.5d : 0d;
        }

        private static double Cross(ClipPoint origin, ClipPoint first, ClipPoint second)
        {
            return (first.X - origin.X) * (second.Y - origin.Y) -
                   (first.Y - origin.Y) * (second.X - origin.X);
        }

        private static bool HasProperEdgeCrossing(
            IReadOnlyList<Phase3GridPoint> first,
            IReadOnlyList<Phase3GridPoint> second)
        {
            for (int firstEdge = 0; firstEdge < first.Count; firstEdge++)
            {
                Phase3GridPoint a = first[firstEdge];
                Phase3GridPoint b = first[(firstEdge + 1) % first.Count];
                for (int secondEdge = 0; secondEdge < second.Count; secondEdge++)
                {
                    Phase3GridPoint c = second[secondEdge];
                    Phase3GridPoint d = second[(secondEdge + 1) % second.Count];
                    long abC = Cross(a, b, c);
                    long abD = Cross(a, b, d);
                    long cdA = Cross(c, d, a);
                    long cdB = Cross(c, d, b);
                    if (HaveOppositeSigns(abC, abD) && HaveOppositeSigns(cdA, cdB))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool HasCompleteMatching(Phase3PuzzleDefinition puzzle)
        {
            var slotToPiece = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < puzzle.Pieces.Count; i++)
            {
                if (!TryMatchPiece(puzzle.Pieces[i], puzzle, slotToPiece, new HashSet<string>(StringComparer.Ordinal)))
                {
                    return false;
                }
            }

            return slotToPiece.Count == puzzle.Pieces.Count;
        }

        private static bool TryMatchPiece(
            Phase3PieceDefinition piece,
            Phase3PuzzleDefinition puzzle,
            IDictionary<string, string> slotToPiece,
            ISet<string> visitedSlots)
        {
            for (int i = 0; i < piece.AllowedTargets.Count; i++)
            {
                string slotId = piece.AllowedTargets[i].SlotId;
                if (!visitedSlots.Add(slotId))
                {
                    continue;
                }

                if (!slotToPiece.TryGetValue(slotId, out string occupyingPieceId))
                {
                    slotToPiece[slotId] = piece.PieceId;
                    return true;
                }

                Phase3PieceDefinition occupyingPiece = FindPiece(puzzle.Pieces, occupyingPieceId);
                if (occupyingPiece != null && TryMatchPiece(occupyingPiece, puzzle, slotToPiece, visitedSlots))
                {
                    slotToPiece[slotId] = piece.PieceId;
                    return true;
                }
            }

            return false;
        }

        private static Phase3PieceDefinition FindPiece(IReadOnlyList<Phase3PieceDefinition> pieces, string pieceId)
        {
            for (int i = 0; i < pieces.Count; i++)
            {
                if (string.Equals(pieces[i].PieceId, pieceId, StringComparison.Ordinal))
                {
                    return pieces[i];
                }
            }

            return null;
        }

        private static Phase3SlotDefinition FindSlot(IReadOnlyList<Phase3SlotDefinition> slots, string slotId)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (string.Equals(slots[i].SlotId, slotId, StringComparison.Ordinal))
                {
                    return slots[i];
                }
            }

            return null;
        }

        private static bool IsInsideField(Phase3Bounds2D bounds)
        {
            return bounds.MinX >= 0d && bounds.MinY >= 0d &&
                   bounds.MaxX <= Phase3CoreConstants.LogicalGridSize &&
                   bounds.MaxY <= Phase3CoreConstants.LogicalGridSize;
        }

        private static bool BoundsHavePositiveAreaIntersection(Phase3Bounds2D first, Phase3Bounds2D second)
        {
            double width = Math.Min(first.MaxX, second.MaxX) - Math.Max(first.MinX, second.MinX);
            double height = Math.Min(first.MaxY, second.MaxY) - Math.Max(first.MinY, second.MinY);
            return width > 0d && height > 0d;
        }

        private static long Cross(Phase3GridPoint origin, Phase3GridPoint first, Phase3GridPoint second)
        {
            long firstX = (long)first.X - origin.X;
            long firstY = (long)first.Y - origin.Y;
            long secondX = (long)second.X - origin.X;
            long secondY = (long)second.Y - origin.Y;
            return firstX * secondY - firstY * secondX;
        }

        private static bool HaveOppositeSigns(long first, long second)
        {
            return (first > 0L && second < 0L) || (first < 0L && second > 0L);
        }

        private readonly struct EdgeSignature
        {
            public EdgeSignature(int direction, long squaredLength)
            {
                Direction = direction;
                SquaredLength = squaredLength;
            }

            public int Direction { get; }
            public long SquaredLength { get; }

            public EdgeSignature Rotate(int step)
            {
                // Direction indices increase counterclockwise, while Core step +1 is clockwise.
                // Subtracting the step keeps geometry congruence aligned with Phase3RotationStep.
                int direction = (Direction - step) % Phase3CoreConstants.FullRotationStepCount;
                if (direction < 0)
                {
                    direction += Phase3CoreConstants.FullRotationStepCount;
                }

                return new EdgeSignature(direction, SquaredLength);
            }

            public bool Equals(EdgeSignature other)
            {
                return Direction == other.Direction && SquaredLength == other.SquaredLength;
            }
        }

        private readonly struct ClipPoint
        {
            public ClipPoint(double x, double y)
            {
                X = x;
                Y = y;
            }

            public double X { get; }
            public double Y { get; }
        }

        private static void AddIssue(
            ICollection<Phase3PartitionIssue> issues,
            Phase3PartitionFailure failure,
            string subjectId,
            string message)
        {
            issues.Add(new Phase3PartitionIssue(failure, subjectId, message));
        }

        private static Phase3PartitionValidationResult CreateSingleFailure(
            Phase3PartitionFailure failure,
            string subjectId,
            string message)
        {
            return new Phase3PartitionValidationResult(
                new[] { new Phase3PartitionIssue(failure, subjectId, message) },
                0L,
                0L,
                0d,
                0d,
                0d,
                0);
        }
    }
}
