#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using HATAGONG.GameFlow;
using UnityEditor;
using UnityEngine;

namespace HATAGONG.Phase3.Editor
{
    public static class Phase3StageP3_1LogicCoreValidation
    {
        [MenuItem("Tools/HATAGONG/Phase 3/Stage P3-1 Logic Core Validation")]
        public static void Validate()
        {
            var validation = new ValidationContext();
            validation.RunSection("Constants", () => ValidateConstants(validation));
            validation.RunSection("GridPoint", () => ValidateGridPoint(validation));
            validation.RunSection("Point2D", () => ValidatePoint2D(validation));
            validation.RunSection("Rotation", () => ValidateRotation(validation));
            validation.RunSection("Geometry", () => ValidateGeometry(validation));
            validation.RunSection("Canonicalization", () => ValidateCanonicalization(validation));
            validation.RunSection("Difficulty", () => ValidateDifficulty(validation));
            validation.RunSection("Definitions", () => ValidateDefinitions(validation));
            validation.RunSection("Snap", () => ValidateSnap(validation));
            validation.RunSection("Score", () => ValidateScore(validation));
            validation.RunSection("Forbidden concepts", () => ValidateForbiddenConcepts(validation));

            for (int i = 0; i < validation.Failures.Count; i++)
            {
                Debug.LogError(validation.Failures[i]);
            }

            Debug.Log($"[Phase3][P3-1] result={validation.Passed}/{validation.Total}, failures={validation.Failures.Count}");
        }

        private static void ValidateConstants(ValidationContext validation)
        {
            validation.Equal(16, Phase3CoreConstants.LogicalGridSize, "A.Constants.GridSize");
            validation.Equal(256, Phase3CoreConstants.LogicalFieldArea, "A.Constants.FieldArea");
            validation.Equal(1250, Phase3CoreConstants.CanvasFieldSize, "A.Constants.CanvasSize");
            validation.Equal(45, Phase3CoreConstants.RotationStepDegrees, "A.Constants.RotationDegrees");
            validation.Equal(8, Phase3CoreConstants.FullRotationStepCount, "A.Constants.StepCount");
        }

        private static void ValidateGridPoint(ValidationContext validation)
        {
            var a = new Phase3GridPoint(2, 3);
            var same = new Phase3GridPoint(2, 3);
            var laterX = new Phase3GridPoint(3, 0);
            var laterY = new Phase3GridPoint(2, 4);
            validation.Check(a == same, "B.GridPoint.Equality", true, a == same);
            validation.Equal(a.GetHashCode(), same.GetHashCode(), "B.GridPoint.Hash");
            validation.Check(a.CompareTo(laterX) < 0 && a.CompareTo(laterY) < 0, "B.GridPoint.Sorting", true, $"x={a.CompareTo(laterX)}, y={a.CompareTo(laterY)}");
            validation.Check(new Phase3GridPoint(0, 0).IsWithinLogicalGrid, "B.GridPoint.MinBoundary", true, true);
            validation.Check(new Phase3GridPoint(16, 16).IsWithinLogicalGrid, "B.GridPoint.MaxBoundary", true, true);
            validation.Check(!new Phase3GridPoint(-1, 0).IsWithinLogicalGrid, "B.GridPoint.RejectNegative", true, false);
            validation.Check(!new Phase3GridPoint(17, 16).IsWithinLogicalGrid, "B.GridPoint.RejectSeventeen", true, false);
            validation.Equal(25L, new Phase3GridPoint(0, 0).DistanceSquaredTo(new Phase3GridPoint(3, 4)), "B.GridPoint.DistanceSquared");
            validation.Equal(new Phase3GridPoint(2, 3), laterX.DifferenceFrom(new Phase3GridPoint(1, -3)), "B.GridPoint.Difference");
        }

        private static void ValidatePoint2D(ValidationContext validation)
        {
            var point = new Phase3Point2D(1.5d, -2d);
            validation.Check(point.IsFinite, "B2.Point2D.Finite", true, point.IsFinite);
            validation.Near(25d, new Phase3Point2D(0d, 0d).DistanceSquaredTo(new Phase3Point2D(3d, 4d)), "B2.Point2D.DistanceSquared");
            validation.Equal(new Phase3Point2D(2.5d, 0d), point + new Phase3Point2D(1d, 2d), "B2.Point2D.Add");
            validation.Check(!new Phase3Point2D(double.NaN, 0d).IsFinite, "B2.Point2D.RejectNaN", true, false);
            validation.Check(!new Phase3Point2D(0d, double.PositiveInfinity).IsFinite, "B2.Point2D.RejectInfinity", true, false);
        }

        private static void ValidateRotation(ValidationContext validation)
        {
            validation.Equal(7, new Phase3RotationStep(-1).Value, "C.Rotation.NegativeNormalize");
            validation.Equal(0, new Phase3RotationStep(8).Value, "C.Rotation.EightNormalize");
            validation.Equal(1, new Phase3RotationStep(9).Value, "C.Rotation.NineNormalize");
            validation.Equal(1, new Phase3RotationStep(17).Value, "C.Rotation.SeventeenNormalize");
            validation.Equal(315, new Phase3RotationStep(7).Degrees, "C.Rotation.Degrees");
            validation.Equal(new Phase3RotationStep(0), new Phase3RotationStep(7).Clockwise, "C.Rotation.Clockwise");
            validation.Equal(new Phase3RotationStep(7), new Phase3RotationStep(0).CounterClockwise, "C.Rotation.CounterClockwise");
            validation.Equal(new Phase3RotationStep(5), new Phase3RotationStep(1).Add(12), "C.Rotation.Add");
            validation.Equal(2, new Phase3RotationStep(7).NormalizedDeltaTo(new Phase3RotationStep(1)), "C.Rotation.Delta");
            validation.Check(new Phase3RotationStep(0).IsEquivalentTo(new Phase3RotationStep(0), 2), "D.Symmetry.Square0", true, true);
            validation.Check(new Phase3RotationStep(2).IsEquivalentTo(new Phase3RotationStep(0), 2), "D.Symmetry.Square2", true, true);
            validation.Check(new Phase3RotationStep(4).IsEquivalentTo(new Phase3RotationStep(0), 2), "D.Symmetry.Square4", true, true);
            validation.Check(new Phase3RotationStep(6).IsEquivalentTo(new Phase3RotationStep(0), 2), "D.Symmetry.Square6", true, true);
            validation.Check(!new Phase3RotationStep(1).IsEquivalentTo(new Phase3RotationStep(0), 2), "D.Symmetry.SquareReject1", true, false);
            validation.Check(new Phase3RotationStep(5).IsEquivalentTo(new Phase3RotationStep(1), 4), "D.Symmetry.Rectangle1And5", true, true);
            validation.Check(!new Phase3RotationStep(2).IsEquivalentTo(new Phase3RotationStep(0), 4), "D.Symmetry.RectangleReject2", true, false);
            validation.Check(new Phase3RotationStep(3).IsEquivalentTo(new Phase3RotationStep(3), 8), "D.Symmetry.GenericExact", true, true);
            validation.Check(!new Phase3RotationStep(7).IsEquivalentTo(new Phase3RotationStep(3), 8), "D.Symmetry.GenericReject", true, false);
            validation.Throws<ArgumentOutOfRangeException>(() => new Phase3RotationStep(0).IsEquivalentTo(default, 3), "D.Symmetry.InvalidPeriod");
        }

        private static void ValidateGeometry(ValidationContext validation)
        {
            Phase3GridPoint[] triangle = Triangle();
            Phase3GridPoint[] square = Square();
            Phase3GridPoint[] rectangle = Rectangle();
            Phase3GridPoint[] parallelogram = Parallelogram();
            Phase3GridPoint[] trapezoid = Trapezoid();
            validation.Equal(16L, Phase3Geometry.SignedDoubleArea(triangle), "E.Geometry.TriangleDoubleArea");
            validation.Near(8d, Phase3Geometry.AbsoluteArea(triangle), "E.Geometry.TriangleArea");
            validation.Equal(32L, Phase3Geometry.SignedDoubleArea(square), "E.Geometry.SquareDoubleArea");
            validation.Near(12d, Phase3Geometry.AbsoluteArea(rectangle), "E.Geometry.RectangleArea");
            validation.Near(8d, Phase3Geometry.AbsoluteArea(parallelogram), "E.Geometry.ParallelogramArea");
            validation.Near(8d, Phase3Geometry.AbsoluteArea(trapezoid), "E.Geometry.TrapezoidArea");
            validation.Equal(-32L, Phase3Geometry.SignedDoubleArea(Reverse(square)), "E.Geometry.ClockwiseSign");
            validation.Check(Phase3Geometry.TryGetCentroid(triangle, out Phase3Point2D triangleCentroid), "E.Geometry.TriangleCentroidAvailable", true, true);
            validation.Near(4d / 3d, triangleCentroid.X, "E.Geometry.TriangleCentroidX");
            validation.Near(4d / 3d, triangleCentroid.Y, "E.Geometry.TriangleCentroidY");
            validation.Check(Phase3Geometry.TryGetCentroid(square, out Phase3Point2D squareCentroid), "E.Geometry.SquareCentroidAvailable", true, true);
            validation.Equal(new Phase3Point2D(2d, 2d), squareCentroid, "E.Geometry.SquareCentroid");
            validation.Check(!Phase3Geometry.TryGetCentroid(new[] { new Phase3GridPoint(0, 0), new Phase3GridPoint(1, 0), new Phase3GridPoint(2, 0) }, out _), "E.Geometry.ZeroAreaCentroidRejected", true, false);
            Phase3Bounds2D bounds = Phase3Geometry.GetBounds(rectangle);
            validation.Equal(new Phase3Bounds2D(0d, 0d, 6d, 2d), bounds, "E.Geometry.Bounds");
            validation.Near(3d, bounds.AspectRatio, "E.Geometry.AspectRatio");
            validation.Check(Phase3Geometry.PointOnSegment(new Phase3GridPoint(2, 2), new Phase3GridPoint(0, 0), new Phase3GridPoint(4, 4)), "E.Geometry.PointOnSegment", true, true);
            validation.Check(Phase3Geometry.SegmentsIntersect(new Phase3GridPoint(0, 0), new Phase3GridPoint(4, 4), new Phase3GridPoint(0, 4), new Phase3GridPoint(4, 0)), "E.Geometry.SegmentIntersection", true, true);
            validation.Check(Phase3Geometry.PointInPolygon(new Phase3Point2D(2d, 2d), square), "E.Geometry.PointInside", true, true);
            validation.Check(!Phase3Geometry.PointInPolygon(new Phase3Point2D(6d, 2d), square), "E.Geometry.PointOutside", true, false);
            validation.Check(Phase3Geometry.PointInPolygon(new Phase3Point2D(0d, 2d), square), "E.Geometry.PointBoundaryIncluded", true, true);

            validation.Check(Phase3Geometry.ValidatePolygon(square).IsValid, "F.Validity.ConvexAccepted", true, true);
            validation.Check(!Phase3Geometry.ValidatePolygon(new[] { new Phase3GridPoint(0, 0), new Phase3GridPoint(4, 0), new Phase3GridPoint(4, 4), new Phase3GridPoint(2, 2), new Phase3GridPoint(0, 4) }).IsValid, "F.Validity.ConcaveRejected", true, false);
            Phase3GridPoint[] bowTie = { new Phase3GridPoint(0, 0), new Phase3GridPoint(4, 4), new Phase3GridPoint(0, 4), new Phase3GridPoint(4, 0) };
            validation.Check(Phase3Geometry.HasSelfIntersection(bowTie), "F.Validity.SelfIntersectionDetected", true, true);
            validation.Check(!Phase3Geometry.ValidatePolygon(bowTie).IsValid, "F.Validity.SelfIntersectionRejected", true, false);
            validation.Check(!Phase3Geometry.ValidatePolygon(new[] { new Phase3GridPoint(0, 0), new Phase3GridPoint(4, 0), new Phase3GridPoint(4, 0), new Phase3GridPoint(0, 4) }).IsValid, "F.Validity.DuplicateRejected", true, false);
            validation.Check(Phase3Geometry.HasZeroLengthEdge(new[] { new Phase3GridPoint(0, 0), new Phase3GridPoint(0, 0), new Phase3GridPoint(1, 1) }), "F.Validity.ZeroEdgeDetected", true, true);
            validation.Check(!Phase3Geometry.ValidatePolygon(new[] { new Phase3GridPoint(0, 0), new Phase3GridPoint(2, 0), new Phase3GridPoint(4, 0), new Phase3GridPoint(4, 4), new Phase3GridPoint(0, 4) }).IsValid, "F.Validity.CollinearRejected", true, false);
            validation.Check(!Phase3Geometry.ValidatePolygon(new[] { new Phase3GridPoint(0, 0), new Phase3GridPoint(3, 1), new Phase3GridPoint(0, 2) }).IsValid, "F.Validity.UnsupportedAngleRejected", true, false);
            validation.Check(Phase3Geometry.IsAllowedEdgeDirection(new Phase3GridPoint(0, 0), new Phase3GridPoint(4, 0)), "F.Validity.HorizontalAllowed", true, true);
            validation.Check(Phase3Geometry.IsAllowedEdgeDirection(new Phase3GridPoint(0, 0), new Phase3GridPoint(0, 4)), "F.Validity.VerticalAllowed", true, true);
            validation.Check(Phase3Geometry.IsAllowedEdgeDirection(new Phase3GridPoint(0, 0), new Phase3GridPoint(4, 4)), "F.Validity.Positive45Allowed", true, true);
            validation.Check(Phase3Geometry.IsAllowedEdgeDirection(new Phase3GridPoint(0, 4), new Phase3GridPoint(4, 0)), "F.Validity.Negative45Allowed", true, true);
            validation.Check(!Phase3Geometry.ValidatePolygon(new[] { new Phase3GridPoint(0, 0), new Phase3GridPoint(1, 0) }).IsValid, "F.Validity.TooFewRejected", true, false);
            validation.Check(!Phase3Geometry.ValidatePolygon(new[] { new Phase3GridPoint(0, 0), new Phase3GridPoint(2, 0), new Phase3GridPoint(3, 1), new Phase3GridPoint(2, 2), new Phase3GridPoint(0, 2), new Phase3GridPoint(1, 1) }).IsValid, "F.Validity.TooManyRejected", true, false);
            validation.Check(!Phase3Geometry.ValidatePolygon(new[] { new Phase3GridPoint(-1, 0), new Phase3GridPoint(1, 0), new Phase3GridPoint(0, 1) }).IsValid, "F.Validity.OutsideGridRejected", true, false);
            validation.Check(!Phase3Geometry.ValidatePolygon(new[] { new Phase3GridPoint(0, 0), new Phase3GridPoint(1, 0), new Phase3GridPoint(2, 0) }).IsValid, "F.Validity.ZeroAreaRejected", true, false);
        }

        private static void ValidateCanonicalization(ValidationContext validation)
        {
            Phase3GridPoint[] source = { new Phase3GridPoint(4, 4), new Phase3GridPoint(4, 0), new Phase3GridPoint(0, 0), new Phase3GridPoint(0, 4) };
            Phase3GridPoint[] originalCopy = (Phase3GridPoint[])source.Clone();
            IReadOnlyList<Phase3GridPoint> canonical = Phase3Geometry.CanonicalizeVertices(source);
            validation.Equal(new Phase3GridPoint(0, 0), canonical[0], "G.Canonical.MinimumFirst");
            validation.Check(Phase3Geometry.SignedDoubleArea(canonical) > 0L, "G.Canonical.CounterClockwise", true, true);
            validation.Check(Phase3Geometry.IsCounterClockwise(Phase3Geometry.NormalizeCounterClockwise(Reverse(Square()))), "G.Canonical.WindingNormalization", true, true);
            validation.SequenceEqual(originalCopy, source, "G.Canonical.SourceUnchanged");
            validation.SequenceEqual(canonical, Phase3Geometry.CanonicalizeVertices(Square()), "G.Canonical.RotatedStartSame");
            validation.SequenceEqual(canonical, Phase3Geometry.CanonicalizeVertices(Reverse(Square())), "G.Canonical.WindingSame");
            validation.SequenceEqual(canonical, Phase3Geometry.CanonicalizeVertices(source), "G.Canonical.Deterministic");
        }

        private static void ValidateDifficulty(ValidationContext validation)
        {
            Phase3DifficultyRuleSet easy = Phase3DifficultyRules.For(GameDifficulty.Easy);
            Phase3DifficultyRuleSet normal = Phase3DifficultyRules.For(GameDifficulty.Normal);
            Phase3DifficultyRuleSet hard = Phase3DifficultyRules.For(GameDifficulty.Hard);
            validation.Check(easy.TargetPieceCount == 6 && easy.SnapDistance == 45d && easy.MinimumPieceAreaRatio == 0.10d && easy.MaximumPieceAreaRatio == 0.35d && easy.MaximumAspectRatio == 2.2d && easy.MaximumVertexCount == 4, "H.Difficulty.Easy", "6/45/.10/.35/2.2/4", $"{easy.TargetPieceCount}/{easy.SnapDistance}/{easy.MinimumPieceAreaRatio}/{easy.MaximumPieceAreaRatio}/{easy.MaximumAspectRatio}/{easy.MaximumVertexCount}");
            validation.Check(normal.TargetPieceCount == 7 && normal.SnapDistance == 40d && normal.MinimumPieceAreaRatio == 0.07d && normal.MaximumPieceAreaRatio == 0.30d && normal.MaximumAspectRatio == 2.6d && normal.MaximumVertexCount == 4, "H.Difficulty.Normal", "7/40/.07/.30/2.6/4", $"{normal.TargetPieceCount}/{normal.SnapDistance}/{normal.MinimumPieceAreaRatio}/{normal.MaximumPieceAreaRatio}/{normal.MaximumAspectRatio}/{normal.MaximumVertexCount}");
            validation.Check(hard.TargetPieceCount == 8 && hard.SnapDistance == 35d && hard.MinimumPieceAreaRatio == 0.05d && hard.MaximumPieceAreaRatio == 0.28d && hard.MaximumAspectRatio == 3d && hard.MaximumVertexCount == 5, "H.Difficulty.Hard", "8/35/.05/.28/3/5", $"{hard.TargetPieceCount}/{hard.SnapDistance}/{hard.MinimumPieceAreaRatio}/{hard.MaximumPieceAreaRatio}/{hard.MaximumAspectRatio}/{hard.MaximumVertexCount}");
            validation.Throws<ArgumentOutOfRangeException>(() => Phase3DifficultyRules.For(GameDifficulty.Unspecified), "H.Difficulty.UnspecifiedRejected");
            validation.Throws<ArgumentOutOfRangeException>(() => Phase3DifficultyRules.For((GameDifficulty)999), "H.Difficulty.UnknownRejected");
        }

        private static void ValidateDefinitions(ValidationContext validation)
        {
            var mutableVertices = new List<Phase3GridPoint>(Square());
            var square = new Phase3ShapeDefinition("square", mutableVertices, 2);
            mutableVertices[0] = new Phase3GridPoint(16, 16);
            validation.Equal("square", square.ShapeId, "I.Shape.Id");
            validation.Equal(4, square.Vertices.Count, "I.Shape.VertexCount");
            validation.Equal(new Phase3GridPoint(0, 0), square.Vertices[0], "I.Shape.VerticesIsolated");
            validation.Near(16d, square.Area, "I.Shape.AreaCache");
            validation.Equal(new Phase3Point2D(2d, 2d), square.Centroid, "I.Shape.CentroidCache");
            validation.Equal(new Phase3Bounds2D(0d, 0d, 4d, 4d), square.Bounds, "I.Shape.BoundsCache");
            validation.Near(1d, square.AspectRatio, "I.Shape.AspectCache");
            validation.Equal(2, square.RotationalSymmetryPeriodSteps, "I.Shape.SymmetryCache");
            validation.Throws<ArgumentException>(() => new Phase3ShapeDefinition(" ", Square(), 2), "I.Shape.InvalidIdRejected");
            validation.Throws<ArgumentException>(() => new Phase3ShapeDefinition("bad", new[] { new Phase3GridPoint(0, 0), new Phase3GridPoint(3, 1), new Phase3GridPoint(0, 2) }, 8), "I.Shape.InvalidPolygonRejected");
            validation.Throws<ArgumentOutOfRangeException>(() => new Phase3ShapeDefinition("bad-period", Square(), 3), "I.Shape.InvalidSymmetryRejected");

            var targets = new List<Phase3AllowedTarget>
            {
                new Phase3AllowedTarget("slot-b", new Phase3RotationStep(2)),
                new Phase3AllowedTarget("slot-a", new Phase3RotationStep(0), new Phase3RotationStep(1))
            };
            var piece = new Phase3PieceDefinition("piece-a", "deck-a", square, targets);
            targets.Clear();
            validation.Equal("piece-a", piece.PieceId, "J.Piece.Id");
            validation.Equal("deck-a", piece.OriginalDeckSlotId, "J.Piece.DeckId");
            validation.Equal(2, piece.AllowedTargets.Count, "J.Piece.TargetsIsolated");
            validation.Equal("slot-a", piece.AllowedTargets[0].SlotId, "J.Piece.TargetsSorted");
            validation.Equal(new Phase3RotationStep(1), piece.AllowedTargets[0].RotationCorrectionStep, "J.AllowedTarget.Correction");
            validation.Throws<ArgumentException>(() => new Phase3PieceDefinition("duplicate-target", "deck-x", square, new[] { new Phase3AllowedTarget("slot-a", default), new Phase3AllowedTarget("slot-a", new Phase3RotationStep(1)) }), "J.Piece.DuplicateTargetRejected");

            var slotA = new Phase3SlotDefinition("slot-a", square, new Phase3Point2D(10d, 20d), new Phase3RotationStep(0));
            var slotB = new Phase3SlotDefinition("slot-b", square, new Phase3Point2D(30d, 20d), new Phase3RotationStep(2));
            validation.Equal(new Phase3Point2D(10d, 20d), slotA.CorrectCentroid, "J.Slot.Centroid");
            validation.Equal(new Phase3RotationStep(2), slotB.CorrectBaseRotationStep, "J.Slot.BaseRotation");
            validation.Throws<ArgumentOutOfRangeException>(() => new Phase3SlotDefinition("bad", square, new Phase3Point2D(double.NaN, 0d), default), "J.Slot.NonFiniteRejected");

            var pieceB = new Phase3PieceDefinition("piece-b", "deck-b", square, new[] { new Phase3AllowedTarget("slot-b", new Phase3RotationStep(2)) });
            var puzzle = new Phase3PuzzleDefinition(new[] { pieceB, piece }, new[] { slotB, slotA });
            validation.Equal(16, puzzle.GridSize, "J.Puzzle.GridSize");
            validation.Equal("piece-a", puzzle.Pieces[0].PieceId, "J.Puzzle.PiecesSorted");
            validation.Equal("slot-a", puzzle.Slots[0].SlotId, "J.Puzzle.SlotsSorted");
            validation.Throws<ArgumentException>(() => new Phase3PuzzleDefinition(new[] { piece, piece }, new[] { slotA, slotB }), "J.Puzzle.DuplicatePieceRejected");
            validation.Throws<ArgumentException>(() => new Phase3PuzzleDefinition(new[] { piece }, new[] { slotA, slotA }), "J.Puzzle.DuplicateSlotRejected");
            var duplicateDeckPiece = new Phase3PieceDefinition("piece-c", "deck-a", square, new[] { new Phase3AllowedTarget("slot-a", default) });
            validation.Throws<ArgumentException>(() => new Phase3PuzzleDefinition(new[] { piece, duplicateDeckPiece }, new[] { slotA, slotB }), "J.Puzzle.DuplicateDeckSlotRejected");
            var missingTargetPiece = new Phase3PieceDefinition("piece-missing", "deck-missing", square, new[] { new Phase3AllowedTarget("missing", default) });
            validation.Throws<ArgumentException>(() => new Phase3PuzzleDefinition(new[] { missingTargetPiece }, new[] { slotA }), "J.Puzzle.MissingTargetRejected");
            var noTargetPiece = new Phase3PieceDefinition("piece-empty", "deck-empty", square, Array.Empty<Phase3AllowedTarget>());
            validation.Throws<ArgumentException>(() => new Phase3PuzzleDefinition(new[] { noTargetPiece }, new[] { slotA }), "J.Puzzle.NoTargetRejected");
        }

        private static void ValidateSnap(ValidationContext validation)
        {
            var generic = new Phase3ShapeDefinition("generic", Triangle(), 8);
            var square = new Phase3ShapeDefinition("square-snap", Square(), 2);
            var slotA = new Phase3SlotDefinition("slot-a", generic, new Phase3Point2D(0d, 0d), new Phase3RotationStep(0));
            var slotB = new Phase3SlotDefinition("slot-b", generic, new Phase3Point2D(10d, 0d), new Phase3RotationStep(0));
            var piece = new Phase3PieceDefinition("piece", "deck", generic, new[]
            {
                new Phase3AllowedTarget("slot-b", new Phase3RotationStep(0), new Phase3RotationStep(2)),
                new Phase3AllowedTarget("slot-a", new Phase3RotationStep(0))
            });

            Phase3SnapResult exact = Phase3SnapRules.Evaluate(piece, new Phase3Point2D(0d, 0d), default, 10d, new[] { slotA, slotB }, Array.Empty<string>());
            validation.Check(exact.IsSuccess && exact.TargetSlotId == "slot-a", "K.Snap.ExactSuccess", "slot-a", $"{exact.Code}/{exact.TargetSlotId}");
            Phase3SnapResult inside = Phase3SnapRules.Evaluate(piece, new Phase3Point2D(9d, 0d), default, 10d, new[] { slotA, slotB }, Array.Empty<string>());
            validation.Equal("slot-b", inside.TargetSlotId, "K.Snap.InsideThreshold");
            Phase3SnapResult boundary = Phase3SnapRules.Evaluate(piece, new Phase3Point2D(20d, 0d), default, 10d, new[] { slotA, slotB }, Array.Empty<string>());
            validation.Check(boundary.IsSuccess && boundary.TargetSlotId == "slot-b", "K.Snap.BoundarySuccess", "slot-b", $"{boundary.Code}/{boundary.TargetSlotId}");
            Phase3SnapResult outside = Phase3SnapRules.Evaluate(piece, new Phase3Point2D(20.001d, 0d), default, 10d, new[] { slotA, slotB }, Array.Empty<string>());
            validation.Equal(Phase3SnapResultCode.OutOfSnapDistance, outside.Code, "K.Snap.OutsideFailure");
            Phase3SnapResult rotation = Phase3SnapRules.Evaluate(piece, new Phase3Point2D(0d, 0d), new Phase3RotationStep(1), 10d, new[] { slotA, slotB }, Array.Empty<string>());
            validation.Equal(Phase3SnapResultCode.RotationMismatch, rotation.Code, "K.Snap.RotationMismatch");

            var squarePiece = new Phase3PieceDefinition("square", "deck-square", square, new[] { new Phase3AllowedTarget("slot-square", default) });
            var squareSlot = new Phase3SlotDefinition("slot-square", square, new Phase3Point2D(0d, 0d), default);
            Phase3SnapResult symmetric = Phase3SnapRules.Evaluate(squarePiece, new Phase3Point2D(0d, 0d), new Phase3RotationStep(2), 1d, new[] { squareSlot }, Array.Empty<string>());
            validation.Check(symmetric.IsSuccess, "K.Snap.SymmetrySuccess", true, symmetric.Code);
            Phase3SnapResult occupied = Phase3SnapRules.Evaluate(piece, new Phase3Point2D(0d, 0d), default, 20d, new[] { slotA, slotB }, new[] { "slot-a", "slot-b" });
            validation.Equal(Phase3SnapResultCode.AllAllowedTargetsOccupied, occupied.Code, "K.Snap.OccupiedFailure");
            var unrelated = new Phase3SlotDefinition("slot-z", generic, new Phase3Point2D(0d, 0d), default);
            Phase3SnapResult noAllowed = Phase3SnapRules.Evaluate(piece, new Phase3Point2D(0d, 0d), default, 10d, new[] { unrelated }, Array.Empty<string>());
            validation.Equal(Phase3SnapResultCode.NoAllowedTarget, noAllowed.Code, "K.Snap.NonAllowedExcluded");
            var emptyPiece = new Phase3PieceDefinition("empty", "deck-empty", generic, Array.Empty<Phase3AllowedTarget>());
            validation.Equal(Phase3SnapResultCode.NoAllowedTarget, Phase3SnapRules.Evaluate(emptyPiece, default, default, 1d, new[] { slotA }, Array.Empty<string>()).Code, "K.Snap.NoAllowedTargetCode");

            Phase3SnapResult nearest = Phase3SnapRules.Evaluate(piece, new Phase3Point2D(8d, 0d), default, 20d, new[] { slotA, slotB }, Array.Empty<string>());
            validation.Equal("slot-b", nearest.TargetSlotId, "K.Snap.NearestSelected");
            var tieSlotA = new Phase3SlotDefinition("slot-a", generic, new Phase3Point2D(-5d, 0d), default);
            var tieSlotB = new Phase3SlotDefinition("slot-b", generic, new Phase3Point2D(5d, 0d), default);
            Phase3SnapResult tie = Phase3SnapRules.Evaluate(piece, default, default, 5d, new[] { tieSlotB, tieSlotA }, Array.Empty<string>());
            validation.Equal("slot-a", tie.TargetSlotId, "K.Snap.TieBreakBySlotId");
            Phase3SnapResult reordered = Phase3SnapRules.Evaluate(piece, new Phase3Point2D(8d, 0d), default, 20d, new[] { slotB, slotA }, Array.Empty<string>());
            validation.Equal(nearest.TargetSlotId, reordered.TargetSlotId, "K.Snap.InputOrderIndependent");
            validation.Near(nearest.DistanceSquared, reordered.DistanceSquared, "K.Snap.DistanceDeterministic");
            validation.Equal(new Phase3RotationStep(2), inside.RotationCorrection, "K.Snap.CorrectionReturned");
            validation.Equal(new Phase3Point2D(10d, 0d), inside.TargetCentroid, "K.Snap.TargetCentroidReturned");
            validation.Equal(new Phase3RotationStep(0), inside.RequiredRotation, "K.Snap.RequiredRotationReturned");
            validation.Equal(Phase3SnapResultCode.InvalidInput, Phase3SnapRules.Evaluate(null, default, default, 1d, new[] { slotA }, Array.Empty<string>()).Code, "K.Snap.NullPieceInvalid");
            validation.Equal(Phase3SnapResultCode.InvalidInput, Phase3SnapRules.Evaluate(piece, new Phase3Point2D(double.NaN, 0d), default, 1d, new[] { slotA }, Array.Empty<string>()).Code, "K.Snap.NaNInvalid");
            validation.Equal(Phase3SnapResultCode.InvalidInput, Phase3SnapRules.Evaluate(piece, default, default, double.PositiveInfinity, new[] { slotA }, Array.Empty<string>()).Code, "K.Snap.InfinityInvalid");
            validation.Equal(Phase3SnapResultCode.InvalidInput, Phase3SnapRules.Evaluate(piece, default, default, -1d, new[] { slotA }, Array.Empty<string>()).Code, "K.Snap.NegativeDistanceInvalid");
            validation.Equal(Phase3SnapResultCode.InvalidInput, Phase3SnapRules.Evaluate(piece, default, default, 1d, new[] { slotA, slotA }, Array.Empty<string>()).Code, "K.Snap.DuplicateSlotInvalid");

            Phase3SnapResult repeatOne = Phase3SnapRules.Evaluate(piece, new Phase3Point2D(8d, 0d), default, 20d, new[] { slotA, slotB }, Array.Empty<string>());
            Phase3SnapResult repeatTwo = Phase3SnapRules.Evaluate(piece, new Phase3Point2D(8d, 0d), default, 20d, new[] { slotA, slotB }, Array.Empty<string>());
            validation.Check(repeatOne.Code == repeatTwo.Code && repeatOne.TargetSlotId == repeatTwo.TargetSlotId && Math.Abs(repeatOne.DistanceSquared - repeatTwo.DistanceSquared) <= Phase3CoreConstants.ComparisonEpsilon, "M.Determinism.RepeatedSnap", "same result", $"{repeatOne.Code}/{repeatOne.TargetSlotId}/{repeatOne.DistanceSquared:R} vs {repeatTwo.Code}/{repeatTwo.TargetSlotId}/{repeatTwo.DistanceSquared:R}");
        }

        private static void ValidateScore(ValidationContext validation)
        {
            validation.Equal(200, Phase3ScoreRules.ManualSnapScore, "L.Score.Manual");
            validation.Equal(100, Phase3ScoreRules.TileCutterAutoPlaceScore, "L.Score.Cutter");
            validation.Equal(1000, Phase3ScoreRules.PhaseClearScore, "L.Score.Clear");
            validation.Equal(0, Phase3ScoreRules.PiecePickScore, "L.Score.PickZero");
            validation.Equal(0, Phase3ScoreRules.PieceRotateScore, "L.Score.RotateZero");
            validation.Equal(0, Phase3ScoreRules.LooseDropScore, "L.Score.LooseZero");
            validation.Equal(0, Phase3ScoreRules.WrongPlacementScore, "L.Score.WrongZero");
            validation.Equal(0, Phase3ScoreRules.TileGrinderScore, "L.Score.GrinderZero");
            validation.Equal(0, Phase3ScoreRules.ConsecutiveSnapBonus, "L.Score.NoCombo");
            validation.Equal(2200, Phase3ScoreRules.GetMaximumDirectPlayScore(GameDifficulty.Easy), "L.Score.EasyMaximum");
            validation.Equal(2400, Phase3ScoreRules.GetMaximumDirectPlayScore(GameDifficulty.Normal), "L.Score.NormalMaximum");
            validation.Equal(2600, Phase3ScoreRules.GetMaximumDirectPlayScore(GameDifficulty.Hard), "L.Score.HardMaximum");
            validation.Throws<ArgumentOutOfRangeException>(() => Phase3ScoreRules.CalculateMaximumDirectPlayScore(-1), "L.Score.NegativePieceCountRejected");
        }

        private static void ValidateForbiddenConcepts(ValidationContext validation)
        {
            validation.Check(!HasMemberContaining(typeof(Phase3ShapeDefinition), "Mirror"), "D.Symmetry.NoMirrorShapeState", true, false);
            validation.Check(!HasMemberContaining(typeof(Phase3RotationStep), "Mirror"), "D.Symmetry.NoMirrorRotationState", true, false);
            validation.Check(!HasMemberContaining(typeof(Phase3RotationStep), "Scale"), "D.Symmetry.NoScaleRotationState", true, false);
            validation.Check(typeof(Phase3CoreConstants).GetField("LogicalFieldWidth", BindingFlags.Public | BindingFlags.Static) == null, "N.Scope.NoRectangularFieldWidth", true, false);
            validation.Check(typeof(Phase3PuzzleDefinition).GetProperty("PieceState") == null, "N.Scope.NoPieceStateMachine", true, false);
        }

        private static bool HasMemberContaining(Type type, string text)
        {
            MemberInfo[] members = type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            for (int i = 0; i < members.Length; i++)
            {
                if (members[i].Name.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static Phase3GridPoint[] Triangle() => new[]
        {
            new Phase3GridPoint(0, 0), new Phase3GridPoint(4, 0), new Phase3GridPoint(0, 4)
        };

        private static Phase3GridPoint[] Square() => new[]
        {
            new Phase3GridPoint(0, 0), new Phase3GridPoint(4, 0),
            new Phase3GridPoint(4, 4), new Phase3GridPoint(0, 4)
        };

        private static Phase3GridPoint[] Rectangle() => new[]
        {
            new Phase3GridPoint(0, 0), new Phase3GridPoint(6, 0),
            new Phase3GridPoint(6, 2), new Phase3GridPoint(0, 2)
        };

        private static Phase3GridPoint[] Parallelogram() => new[]
        {
            new Phase3GridPoint(0, 0), new Phase3GridPoint(4, 0),
            new Phase3GridPoint(6, 2), new Phase3GridPoint(2, 2)
        };

        private static Phase3GridPoint[] Trapezoid() => new[]
        {
            new Phase3GridPoint(0, 0), new Phase3GridPoint(6, 0),
            new Phase3GridPoint(4, 2), new Phase3GridPoint(2, 2)
        };

        private static Phase3GridPoint[] Reverse(IReadOnlyList<Phase3GridPoint> source)
        {
            var reversed = new Phase3GridPoint[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                reversed[i] = source[source.Count - 1 - i];
            }

            return reversed;
        }

        private sealed class ValidationContext
        {
            private readonly List<string> failures = new List<string>();

            public int Passed { get; private set; }
            public int Total { get; private set; }
            public IReadOnlyList<string> Failures => failures;

            public void RunSection(string name, Action action)
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    RecordUnexpectedException(name, exception);
                }
            }

            public void Check(bool condition, string name, object expected, object actual)
            {
                Total++;
                if (condition)
                {
                    Passed++;
                    return;
                }

                failures.Add($"[Phase3][P3-1][FAIL] assertion={name}, expected={Format(expected)}, actual={Format(actual)}");
            }

            public void Equal<T>(T expected, T actual, string name)
            {
                bool equal = EqualityComparer<T>.Default.Equals(expected, actual);
                Check(equal, name, expected, actual);
            }

            public void Near(double expected, double actual, string name)
            {
                bool equal = Phase3Point2D.IsFiniteValue(actual) && Math.Abs(expected - actual) <= Phase3CoreConstants.ComparisonEpsilon;
                Check(equal, name, expected, actual);
            }

            public void SequenceEqual<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual, string name)
            {
                bool equal = expected != null && actual != null && expected.Count == actual.Count;
                if (equal)
                {
                    for (int i = 0; i < expected.Count; i++)
                    {
                        if (!EqualityComparer<T>.Default.Equals(expected[i], actual[i]))
                        {
                            equal = false;
                            break;
                        }
                    }
                }

                Check(equal, name, Join(expected), Join(actual));
            }

            public void Throws<TException>(Action action, string name) where TException : Exception
            {
                Total++;
                try
                {
                    action();
                    failures.Add($"[Phase3][P3-1][FAIL] assertion={name}, expectedException={typeof(TException).FullName}, actual=no exception");
                }
                catch (TException)
                {
                    Passed++;
                }
                catch (Exception exception)
                {
                    failures.Add($"[Phase3][P3-1][FAIL] assertion={name}, expectedException={typeof(TException).FullName}, actualException={exception.GetType().FullName}, message={exception.Message}");
                }
            }

            public void RecordUnexpectedException(string name, Exception exception)
            {
                Total++;
                failures.Add($"[Phase3][P3-1][FAIL] assertion={name}, expected=no exception, actualException={exception.GetType().FullName}, message={exception.Message}");
            }

            private static string Join<T>(IReadOnlyList<T> values)
            {
                if (values == null)
                {
                    return "<null>";
                }

                var parts = new string[values.Count];
                for (int i = 0; i < values.Count; i++)
                {
                    parts[i] = Format(values[i]);
                }

                return "[" + string.Join(", ", parts) + "]";
            }

            private static string Format(object value) => value == null ? "<null>" : value.ToString();
        }
    }
}
#endif
