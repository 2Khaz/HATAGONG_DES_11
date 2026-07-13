#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using HATAGONG.GameFlow;
using UnityEditor;
using UnityEngine;

namespace HATAGONG.Phase3.Editor
{
    public static class Phase3StageP3_2SafeTemplatePlayCoreValidation
    {
        [MenuItem("Tools/HATAGONG/Phase 3/Stage P3-2 Safe Template Play Core Validation")]
        public static void Validate()
        {
            var validation = new ValidationContext();
            validation.RunSection("A. Piece State", () => ValidatePieceState(validation));
            validation.RunSection("B. Active Drag", () => ValidateActiveDrag(validation));
            validation.RunSection("C. Rotation", () => ValidateRotation(validation));
            validation.RunSection("D. Cancel", () => ValidateCancel(validation));
            validation.RunSection("E. Deck Return", () => ValidateDeckReturn(validation));
            validation.RunSection("F. Loose Drop", () => ValidateLooseDrop(validation));
            validation.RunSection("G. Snap Success", () => ValidateSnapSuccess(validation));
            validation.RunSection("H. Snap Failure", () => ValidateSnapFailure(validation));
            validation.RunSection("I. Piece Substitution", () => ValidateSubstitution(validation));
            validation.RunSection("J. Score", () => ValidateScore(validation));
            validation.RunSection("K. Clear", () => ValidateClear(validation));
            validation.RunSection("L. Safe Template Easy", () => ValidateTemplate(validation, GameDifficulty.Easy));
            validation.RunSection("M. Safe Template Normal", () => ValidateTemplate(validation, GameDifficulty.Normal));
            validation.RunSection("N. Safe Template Hard", () => ValidateTemplate(validation, GameDifficulty.Hard));
            validation.RunSection("O. Partition Invalid Cases", () => ValidateInvalidPartitions(validation));
            validation.RunSection("P. Immutability and Determinism", () => ValidateImmutabilityAndDeterminism(validation));
            validation.RunSection("Q. Catalog Rotation Correction", () => ValidateCatalogRotationCorrection(validation));
            validation.RunSection("R. Shape Congruence", () => ValidateShapeCongruence(validation));
            validation.RunSection("S. Convex Overlap", () => ValidateConvexOverlap(validation));
            validation.RunSection("T. Initial Rotation Contract", () => ValidateInitialRotationContract(validation));
            validation.RunSection("U. Clockwise Geometry Direction", () => ValidateClockwiseGeometryDirection(validation));

            for (int i = 0; i < validation.Failures.Count; i++)
            {
                Debug.LogError(validation.Failures[i]);
            }

            Debug.Log($"[Phase3][P3-2] result={validation.Passed}/{validation.Total}, failures={validation.Failures.Count}");
        }

        private static void ValidatePieceState(ValidationContext validation)
        {
            Phase3PuzzleSessionModel session = NewSession(GameDifficulty.Easy);
            Phase3PieceModel piece = session.Pieces[0];
            string pieceId = piece.PieceId;
            string deckId = piece.OriginalDeckSlotId;
            validation.Equal(4, Enum.GetValues(typeof(Phase3PieceState)).Length, "State.ExactlyFourValues");
            validation.Equal(Phase3PieceState.InDeck, piece.State, "State.InitialInDeck");
            validation.Equal(pieceId, piece.PieceId, "State.PieceIdStableInitially");
            validation.Equal(deckId, piece.OriginalDeckSlotId, "State.DeckIdStableInitially");
            validation.Check(session.BeginDrag(pieceId).IsSuccess, "State.InDeckToDragging", true, piece.State);
            Phase3PlayResult loose = session.DropActiveOnField(pieceId, new Phase3Point2D(-1000d, -1000d));
            validation.Equal(Phase3PieceState.Loose, piece.State, "State.DraggingToLoose");
            validation.Equal(Phase3PlayFailure.SnapOutOfDistance, loose.Failure, "State.LooseFailureReason");
            validation.Check(session.BeginDrag(pieceId).IsSuccess, "State.LooseToDragging", true, piece.State);
            Phase3AllowedTarget target = piece.Definition.AllowedTargets[0];
            Phase3PlayResult placed = PlaceActiveAtTarget(session, piece, target);
            validation.Check(placed.PiecePlaced && piece.State == Phase3PieceState.Placed, "State.DraggingToPlaced", true, $"{placed.PiecePlaced}/{piece.State}");
            validation.Equal(pieceId, piece.PieceId, "State.PieceIdStableAfterPlacement");
            validation.Equal(deckId, piece.OriginalDeckSlotId, "State.DeckIdStableAfterPlacement");
        }

        private static void ValidateActiveDrag(ValidationContext validation)
        {
            Phase3PuzzleSessionModel session = NewSession(GameDifficulty.Easy);
            Phase3PieceModel first = session.Pieces[0];
            Phase3PieceModel second = session.Pieces[1];
            validation.Check(session.BeginDrag(first.PieceId).IsSuccess && session.HasActiveDrag, "Drag.BeginFromDeck", true, session.ActivePieceId);
            validation.Equal(first.PieceId, session.ActivePieceId, "Drag.ActivePieceId");
            validation.Equal(Phase3PieceState.InDeck, session.ActiveDrag.OriginState, "Drag.OriginStateCaptured");
            validation.Equal(Phase3PlayFailure.PieceAlreadyDragging, session.BeginDrag(first.PieceId).Failure, "Drag.SamePieceRejected");
            validation.Equal(Phase3PlayFailure.AnotherPieceAlreadyDragging, session.BeginDrag(second.PieceId).Failure, "Drag.SecondPieceRejected");
            validation.Equal(Phase3PlayFailure.PieceNotFound, NewSession(GameDifficulty.Easy).BeginDrag("missing").Failure, "Drag.MissingPieceRejected");
            validation.Check(session.CancelActiveDrag().IsSuccess && !session.HasActiveDrag, "Drag.CancelReleasesOwnership", true, session.HasActiveDrag);

            Phase3PuzzleSessionModel placedSession = NewSession(GameDifficulty.Easy);
            Phase3PieceModel placedPiece = placedSession.Pieces[0];
            placedSession.BeginDrag(placedPiece.PieceId);
            Phase3PlayResult placement = PlaceActiveAtTarget(placedSession, placedPiece, placedPiece.Definition.AllowedTargets[0]);
            validation.Check(placement.IsSuccess, "Drag.PreconditionPlaced", true, placement.Failure);
            validation.Equal(Phase3PlayFailure.PiecePlacedImmutable, placedSession.BeginDrag(placedPiece.PieceId).Failure, "Drag.PlacedRejected");

            Phase3PuzzleSessionModel clearSession = NewSession(GameDifficulty.Easy);
            SolveAll(clearSession);
            validation.Equal(Phase3PlayFailure.PhaseAlreadyCleared, clearSession.BeginDrag(clearSession.Pieces[0].PieceId).Failure, "Drag.AfterClearRejected");
        }

        private static void ValidateRotation(ValidationContext validation)
        {
            Phase3PuzzleSessionModel session = NewSession(GameDifficulty.Normal);
            Phase3PieceModel piece = session.Pieces[0];
            Phase3RotationStep initial = piece.CurrentRotation;
            validation.Equal(Phase3PlayFailure.NoActiveDrag, session.RotateActiveClockwise(piece.PieceId).Failure, "Rotation.NonDraggingRejected");
            session.BeginDrag(piece.PieceId);
            validation.Check(session.RotateActiveClockwise(piece.PieceId).IsSuccess, "Rotation.ClockwiseSuccess", true, piece.CurrentRotation);
            validation.Equal(initial.Clockwise, piece.CurrentRotation, "Rotation.ClockwiseStep");
            validation.Check(session.RotateActiveCounterClockwise(piece.PieceId).IsSuccess, "Rotation.CounterClockwiseSuccess", true, piece.CurrentRotation);
            validation.Equal(initial, piece.CurrentRotation, "Rotation.CounterClockwiseStep");
            validation.Check(session.RotateActive(piece.PieceId, 17).IsSuccess, "Rotation.SignedDeltaSuccess", true, piece.CurrentRotation);
            validation.Equal(initial.Add(1), piece.CurrentRotation, "Rotation.DeltaNormalized");
            validation.Equal(0, session.PhaseScore, "Rotation.ScoreZero");
            validation.Equal(Phase3PieceState.Dragging, piece.State, "Rotation.StatePreserved");
            validation.Check(!HasMemberContaining(typeof(Phase3PieceModel), "Mirror"), "Rotation.NoMirrorState", true, false);
        }

        private static void ValidateCancel(ValidationContext validation)
        {
            Phase3PuzzleSessionModel deckSession = NewSession(GameDifficulty.Easy);
            Phase3PieceModel deckPiece = deckSession.Pieces[0];
            Phase3RotationStep deckRotation = deckPiece.CurrentRotation.Clockwise;
            deckSession.BeginDrag(deckPiece.PieceId);
            deckSession.RotateActiveClockwise(deckPiece.PieceId);
            Phase3PlayResult deckCancel = deckSession.CancelActiveDrag();
            validation.Check(deckCancel.IsSuccess, "Cancel.DeckDragSuccess", true, deckCancel.Failure);
            validation.Equal(Phase3PieceState.InDeck, deckPiece.State, "Cancel.DeckReturnsInDeck");
            validation.Equal(deckRotation, deckPiece.CurrentRotation, "Cancel.DeckRotationPreserved");
            validation.Check(!deckPiece.HasFieldCentroid, "Cancel.DeckHasNoFieldCentroid", false, deckPiece.HasFieldCentroid);
            validation.Equal(0, deckCancel.TotalScoreDelta, "Cancel.DeckScoreZero");
            validation.Check(!deckSession.HasActiveDrag && NoSlotsOccupied(deckSession), "Cancel.DeckOwnershipAndSlotsClear", true, $"{deckSession.HasActiveDrag}/{CountOccupied(deckSession)}");

            Phase3PuzzleSessionModel looseSession = NewSession(GameDifficulty.Easy);
            Phase3PieceModel loosePiece = looseSession.Pieces[0];
            Phase3Point2D loosePoint = new Phase3Point2D(-500d, -400d);
            looseSession.BeginDrag(loosePiece.PieceId);
            looseSession.DropActiveOnField(loosePiece.PieceId, loosePoint);
            looseSession.BeginDrag(loosePiece.PieceId);
            looseSession.RotateActiveCounterClockwise(loosePiece.PieceId);
            Phase3RotationStep looseRotation = loosePiece.CurrentRotation;
            Phase3PlayResult looseCancel = looseSession.CancelActiveDrag();
            validation.Check(looseCancel.IsSuccess, "Cancel.LooseDragSuccess", true, looseCancel.Failure);
            validation.Equal(Phase3PieceState.Loose, loosePiece.State, "Cancel.LooseStateRestored");
            validation.Equal(loosePoint, loosePiece.FieldCentroid, "Cancel.LooseCentroidRestored");
            validation.Equal(looseRotation, loosePiece.CurrentRotation, "Cancel.LooseRotationPreserved");
            validation.Equal(0, looseSession.PhaseScore, "Cancel.LooseScoreZero");
            validation.Check(!looseSession.HasActiveDrag && NoSlotsOccupied(looseSession), "Cancel.LooseOwnershipAndSlotsClear", true, $"{looseSession.HasActiveDrag}/{CountOccupied(looseSession)}");
            validation.Equal(Phase3PlayFailure.NoActiveDrag, looseSession.CancelActiveDrag().Failure, "Cancel.NoActiveRejected");
        }

        private static void ValidateDeckReturn(ValidationContext validation)
        {
            Phase3PuzzleSessionModel deckSession = NewSession(GameDifficulty.Easy);
            Phase3PieceModel deckPiece = deckSession.Pieces[0];
            string deckId = deckPiece.OriginalDeckSlotId;
            deckSession.BeginDrag(deckPiece.PieceId);
            deckSession.RotateActiveClockwise(deckPiece.PieceId);
            Phase3RotationStep rotated = deckPiece.CurrentRotation;
            Phase3PlayResult returned = deckSession.ReturnActiveToDeck(deckPiece.PieceId);
            validation.Check(returned.IsSuccess, "Deck.FromDeckSuccess", true, returned.Failure);
            validation.Equal(Phase3PieceState.InDeck, deckPiece.State, "Deck.StateInDeck");
            validation.Equal(rotated, deckPiece.CurrentRotation, "Deck.RotationPreserved");
            validation.Equal(deckId, deckPiece.OriginalDeckSlotId, "Deck.OriginalSlotPreserved");
            validation.Check(!deckPiece.HasFieldCentroid && !deckPiece.HasPlacedSlot, "Deck.FieldPlacementCleared", true, $"{deckPiece.HasFieldCentroid}/{deckPiece.PlacedSlotId}");
            validation.Equal(0, returned.TotalScoreDelta, "Deck.ScoreZero");
            validation.Check(!deckSession.HasActiveDrag && NoSlotsOccupied(deckSession), "Deck.ActiveAndOccupancyClear", true, $"{deckSession.HasActiveDrag}/{CountOccupied(deckSession)}");

            Phase3PuzzleSessionModel looseSession = NewSession(GameDifficulty.Easy);
            Phase3PieceModel loosePiece = looseSession.Pieces[0];
            looseSession.BeginDrag(loosePiece.PieceId);
            looseSession.DropActiveOnField(loosePiece.PieceId, new Phase3Point2D(-800d, -800d));
            looseSession.BeginDrag(loosePiece.PieceId);
            Phase3PlayResult looseReturn = looseSession.ReturnActiveToDeck(loosePiece.PieceId);
            validation.Check(looseReturn.IsSuccess && loosePiece.State == Phase3PieceState.InDeck, "Deck.FromLooseSuccess", true, $"{looseReturn.Failure}/{loosePiece.State}");
            validation.Equal(1, CountPieceModels(looseSession, loosePiece.PieceId), "Deck.NoPieceClone");
            validation.Equal(Phase3PlayFailure.NoActiveDrag, looseSession.ReturnActiveToDeck(loosePiece.PieceId).Failure, "Deck.NoActiveRejected");
        }

        private static void ValidateLooseDrop(ValidationContext validation)
        {
            Phase3PuzzleSessionModel session = NewSession(GameDifficulty.Easy);
            Phase3PieceModel piece = session.Pieces[0];
            Phase3Point2D firstPoint = new Phase3Point2D(-900d, -900d);
            Phase3RotationStep initialRotation = piece.CurrentRotation;
            session.BeginDrag(piece.PieceId);
            Phase3PlayResult first = session.DropActiveOnField(piece.PieceId, firstPoint);
            validation.Equal(Phase3PlayFailure.SnapOutOfDistance, first.Failure, "Loose.InitialFailureCode");
            validation.Equal(Phase3PieceState.Loose, piece.State, "Loose.InitialState");
            validation.Equal(firstPoint, piece.FieldCentroid, "Loose.InitialCentroid");
            validation.Equal(initialRotation, piece.CurrentRotation, "Loose.InitialRotation");
            validation.Equal(0, first.TotalScoreDelta, "Loose.InitialScoreZero");
            validation.Check(!session.HasActiveDrag && NoSlotsOccupied(session), "Loose.InitialReleasesWithoutOccupancy", true, $"{session.HasActiveDrag}/{CountOccupied(session)}");

            Phase3Point2D secondPoint = new Phase3Point2D(-700d, -650d);
            session.BeginDrag(piece.PieceId);
            session.RotateActiveClockwise(piece.PieceId);
            Phase3RotationStep rotated = piece.CurrentRotation;
            Phase3PlayResult second = session.DropActiveOnField(piece.PieceId, secondPoint);
            validation.Equal(Phase3PieceState.Loose, piece.State, "Loose.RedropState");
            validation.Equal(secondPoint, piece.FieldCentroid, "Loose.RedropCentroid");
            validation.Equal(rotated, piece.CurrentRotation, "Loose.RedropRotation");
            validation.Equal(0, second.TotalScoreDelta, "Loose.RedropScoreZero");
            validation.Check(piece.State != Phase3PieceState.InDeck, "Loose.NoAutomaticDeckReturn", true, piece.State);
        }

        private static void ValidateSnapSuccess(ValidationContext validation)
        {
            Phase3PuzzleSessionModel session = NewSession(GameDifficulty.Easy);
            Phase3PieceModel square = FindPieceByShapeIdPart(session, "square");
            Phase3AllowedTarget directTarget = square.Definition.AllowedTargets[0];
            session.BeginDrag(square.PieceId);
            Phase3PlayResult direct = PlaceActiveAtTarget(session, square, directTarget);
            Phase3SlotModel directSlot = session.GetSlot(direct.TargetSlotId);
            validation.Check(direct.IsSuccess && direct.PiecePlaced, "Snap.DirectSuccess", true, direct.Failure);
            validation.Equal(Phase3PieceState.Placed, square.State, "Snap.PiecePlaced");
            validation.Check(directSlot.IsOccupied && directSlot.OccupyingPieceId == square.PieceId, "Snap.SlotOccupied", square.PieceId, directSlot.OccupyingPieceId);
            validation.Equal(directSlot.Definition.CorrectCentroid, square.FieldCentroid, "Snap.CentroidAligned");
            validation.Equal(directTarget.RequiredRotationStep.Add(directTarget.RotationCorrectionStep.Value), square.CurrentRotation, "Snap.RotationAligned");
            validation.Check(!session.HasActiveDrag, "Snap.ActiveDragEnded", false, session.HasActiveDrag);
            validation.Equal(200, direct.ManualSnapScoreDelta, "Snap.ManualScore");
            validation.Equal(0, direct.ClearScoreDelta, "Snap.NonFinalClearScoreZero");

            Phase3PuzzleSessionModel thresholdSession = NewSession(GameDifficulty.Easy);
            Phase3PieceModel thresholdPiece = FindPieceByShapeIdPart(thresholdSession, "square");
            Phase3AllowedTarget thresholdTarget = thresholdPiece.Definition.AllowedTargets[0];
            Phase3SlotModel thresholdSlot = thresholdSession.GetSlot(thresholdTarget.SlotId);
            thresholdSession.BeginDrag(thresholdPiece.PieceId);
            RotateTo(thresholdSession, thresholdPiece, thresholdTarget.RequiredRotationStep);
            Phase3Point2D boundaryPoint = thresholdSlot.Definition.CorrectCentroid + new Phase3Point2D(thresholdSession.DifficultyRules.SnapDistance, 0d);
            Phase3PlayResult boundary = thresholdSession.DropActiveOnField(thresholdPiece.PieceId, boundaryPoint);
            validation.Check(boundary.IsSuccess, "Snap.ThresholdBoundarySuccess", true, boundary.Failure);

            Phase3PuzzleSessionModel symmetrySession = NewSession(GameDifficulty.Easy);
            Phase3PieceModel symmetryPiece = FindPieceByShapeIdPart(symmetrySession, "square");
            Phase3AllowedTarget symmetryTarget = symmetryPiece.Definition.AllowedTargets[0];
            symmetrySession.BeginDrag(symmetryPiece.PieceId);
            RotateTo(symmetrySession, symmetryPiece, symmetryTarget.RequiredRotationStep.Add(2));
            Phase3PlayResult symmetric = symmetrySession.DropActiveOnField(symmetryPiece.PieceId, symmetrySession.GetSlot(symmetryTarget.SlotId).Definition.CorrectCentroid);
            validation.Check(symmetric.IsSuccess, "Snap.SymmetricRotationSuccess", true, symmetric.Failure);
        }

        private static void ValidateSnapFailure(ValidationContext validation)
        {
            Phase3PuzzleSessionModel distanceSession = NewSession(GameDifficulty.Easy);
            Phase3PieceModel distancePiece = distanceSession.Pieces[0];
            distanceSession.BeginDrag(distancePiece.PieceId);
            int occupiedBefore = CountOccupied(distanceSession);
            Phase3PlayResult distance = distanceSession.DropActiveOnField(distancePiece.PieceId, new Phase3Point2D(-1000d, -1000d));
            validation.Equal(Phase3PlayFailure.SnapOutOfDistance, distance.Failure, "Failure.DistanceCode");
            validation.Equal(Phase3SnapResultCode.OutOfSnapDistance, distance.SnapResultCode, "Failure.DistanceSnapCodePreserved");
            validation.Equal(occupiedBefore, CountOccupied(distanceSession), "Failure.DistanceSlotsUnchanged");
            validation.Equal(0, distance.TotalScoreDelta, "Failure.DistanceScoreZero");

            Phase3PuzzleSessionModel rotationSession = NewSession(GameDifficulty.Easy);
            Phase3PieceModel triangle = FindPieceByShapeIdPart(rotationSession, "triangle");
            Phase3AllowedTarget target = triangle.Definition.AllowedTargets[0];
            rotationSession.BeginDrag(triangle.PieceId);
            RotateTo(rotationSession, triangle, target.RequiredRotationStep.Add(1));
            Phase3PlayResult rotation = rotationSession.DropActiveOnField(triangle.PieceId, rotationSession.GetSlot(target.SlotId).Definition.CorrectCentroid);
            validation.Equal(Phase3PlayFailure.SnapRotationMismatch, rotation.Failure, "Failure.RotationCode");
            validation.Equal(Phase3SnapResultCode.RotationMismatch, rotation.SnapResultCode, "Failure.RotationSnapCodePreserved");
            validation.Check(NoSlotsOccupied(rotationSession), "Failure.RotationSlotsUnchanged", true, CountOccupied(rotationSession));

            Phase3PuzzleSessionModel invalidSession = NewSession(GameDifficulty.Easy);
            Phase3PieceModel invalidPiece = invalidSession.Pieces[0];
            invalidSession.BeginDrag(invalidPiece.PieceId);
            Phase3PlayResult invalid = invalidSession.DropActiveOnField(invalidPiece.PieceId, new Phase3Point2D(double.NaN, 0d));
            validation.Equal(Phase3PlayFailure.InvalidCentroid, invalid.Failure, "Failure.InvalidCentroidCode");
            validation.Check(invalidSession.HasActiveDrag && invalidPiece.State == Phase3PieceState.Dragging, "Failure.InvalidCentroidPreservesDrag", true, $"{invalidSession.HasActiveDrag}/{invalidPiece.State}");
            validation.Equal(0, invalid.TotalScoreDelta, "Failure.InvalidCentroidScoreZero");

            Phase3PuzzleSessionModel occupiedSession = CreateRestrictedTargetSession();
            Phase3PieceModel flexible = occupiedSession.GetPiece("easy-piece-triangle-01");
            Phase3PieceModel restricted = occupiedSession.GetPiece("easy-piece-triangle-02");
            Phase3AllowedTarget occupiedTarget = flexible.Definition.AllowedTargets[0];
            occupiedSession.BeginDrag(flexible.PieceId);
            Phase3PlayResult firstPlacement = PlaceActiveAtTarget(occupiedSession, flexible, occupiedTarget);
            validation.Check(firstPlacement.IsSuccess, "Failure.OccupiedPrecondition", true, firstPlacement.Failure);
            occupiedSession.BeginDrag(restricted.PieceId);
            Phase3AllowedTarget onlyTarget = restricted.Definition.AllowedTargets[0];
            RotateTo(occupiedSession, restricted, onlyTarget.RequiredRotationStep);
            Phase3PlayResult occupied = occupiedSession.DropActiveOnField(restricted.PieceId, occupiedSession.GetSlot(onlyTarget.SlotId).Definition.CorrectCentroid);
            validation.Equal(Phase3PlayFailure.SnapAllTargetsOccupied, occupied.Failure, "Failure.AllOccupiedCode");
            validation.Equal(Phase3SnapResultCode.AllAllowedTargetsOccupied, occupied.SnapResultCode, "Failure.AllOccupiedSnapCodePreserved");
            validation.Equal(1, CountOccupied(occupiedSession), "Failure.OccupiedSlotNotMutated");
            validation.Equal(0, occupied.TotalScoreDelta, "Failure.OccupiedScoreZero");

            Phase3ShapeDefinition shape = new Phase3ShapeDefinition("no-target-shape", new[] { P(0, 0), P(4, 0), P(0, 4) }, 8);
            var noTargetPiece = new Phase3PieceDefinition("no-target-piece", "no-target-deck", shape, Array.Empty<Phase3AllowedTarget>());
            validation.Equal(Phase3SnapResultCode.NoAllowedTarget, Phase3SnapRules.Evaluate(noTargetPiece, default, default, 1d, Array.Empty<Phase3SlotDefinition>(), Array.Empty<string>()).Code, "Failure.NoAllowedTargetCode");
        }

        private static void ValidateSubstitution(ValidationContext validation)
        {
            Phase3PuzzleSessionModel session = NewSession(GameDifficulty.Easy);
            Phase3PieceModel first = session.GetPiece("easy-piece-triangle-01");
            Phase3PieceModel second = session.GetPiece("easy-piece-triangle-02");
            Phase3AllowedTarget firstToSecond = FindTarget(first, "easy-slot-02");
            Phase3AllowedTarget secondToFirst = FindTarget(second, "easy-slot-01");
            session.BeginDrag(first.PieceId);
            Phase3PlayResult firstPlacement = PlaceActiveAtTarget(session, first, firstToSecond);
            validation.Check(firstPlacement.IsSuccess && first.PlacedSlotId == "easy-slot-02", "Substitution.FirstUsesOtherSlot", "easy-slot-02", first.PlacedSlotId);
            session.BeginDrag(second.PieceId);
            Phase3PlayResult secondPlacement = PlaceActiveAtTarget(session, second, secondToFirst);
            validation.Check(secondPlacement.IsSuccess && second.PlacedSlotId == "easy-slot-01", "Substitution.SecondUsesRemainingSlot", "easy-slot-01", second.PlacedSlotId);
            validation.Check(session.GetSlot("easy-slot-01").IsOccupied && session.GetSlot("easy-slot-02").IsOccupied, "Substitution.DistinctOccupancy", true, CountOccupied(session));

            Phase3PieceModel third = session.GetPiece("easy-piece-triangle-03");
            session.BeginDrag(third.PieceId);
            Phase3AllowedTarget reused = FindTarget(third, "easy-slot-02");
            RotateTo(session, third, reused.RequiredRotationStep);
            Phase3PlayResult reuse = session.DropActiveOnField(third.PieceId, session.GetSlot("easy-slot-02").Definition.CorrectCentroid);
            validation.Check(!reuse.IsSuccess && third.State == Phase3PieceState.Loose, "Substitution.OccupiedSlotNotReused", true, $"{reuse.Failure}/{third.State}");
            validation.Equal(first.PieceId, session.GetSlot("easy-slot-02").OccupyingPieceId, "Substitution.FirstOccupantPreserved");
            validation.Check(ReferenceEquals(first.Definition.ShapeDefinition, second.Definition.ShapeDefinition), "Substitution.SameShapeDefinition", true, false);
            validation.Check(!HasMemberContaining(typeof(Phase3AllowedTarget), "Mirror"), "Substitution.NoMirrorTarget", true, false);

            Phase3PuzzleSessionModel repeat = NewSession(GameDifficulty.Easy);
            Phase3PieceModel repeatFirst = repeat.GetPiece(first.PieceId);
            repeat.BeginDrag(repeatFirst.PieceId);
            Phase3PlayResult repeatPlacement = PlaceActiveAtTarget(repeat, repeatFirst, FindTarget(repeatFirst, "easy-slot-02"));
            validation.Equal(firstPlacement.TargetSlotId, repeatPlacement.TargetSlotId, "Substitution.DeterministicResult");
        }

        private static void ValidateScore(ValidationContext validation)
        {
            Phase3PuzzleSessionModel session = NewSession(GameDifficulty.Easy);
            Phase3PieceModel piece = session.Pieces[0];
            Phase3PlayResult begin = session.BeginDrag(piece.PieceId);
            validation.Equal(0, begin.TotalScoreDelta, "Score.BeginZero");
            validation.Equal(0, session.RotateActiveClockwise(piece.PieceId).TotalScoreDelta, "Score.RotateZero");
            validation.Equal(0, session.CancelActiveDrag().TotalScoreDelta, "Score.CancelZero");
            session.BeginDrag(piece.PieceId);
            validation.Equal(0, session.ReturnActiveToDeck(piece.PieceId).TotalScoreDelta, "Score.DeckZero");
            session.BeginDrag(piece.PieceId);
            validation.Equal(0, session.DropActiveOnField(piece.PieceId, new Phase3Point2D(-1000d, -1000d)).TotalScoreDelta, "Score.LooseZero");
            session.BeginDrag(piece.PieceId);
            Phase3PlayResult placed = PlaceActiveAtTarget(session, piece, piece.Definition.AllowedTargets[0]);
            validation.Equal(200, placed.ManualSnapScoreDelta, "Score.ManualSnap200");
            validation.Equal(0, placed.ClearScoreDelta, "Score.NonFinalClearZero");
            validation.Equal(200, session.PhaseScore, "Score.SessionAfterOneSnap");
            validation.Equal(Phase3PlayFailure.PiecePlacedImmutable, session.BeginDrag(piece.PieceId).Failure, "Score.PlacedRepeatRejected");
            validation.Equal(200, session.PhaseScore, "Score.NoDuplicateManualSnap");

            Phase3PuzzleSessionModel clearSession = NewSession(GameDifficulty.Easy);
            IReadOnlyList<Phase3PlayResult> results = SolveAll(clearSession);
            Phase3PlayResult final = results[results.Count - 1];
            validation.Equal(200, final.ManualSnapScoreDelta, "Score.FinalManualSeparate");
            validation.Equal(1000, final.ClearScoreDelta, "Score.FinalClearSeparate");
            validation.Equal(1200, final.TotalScoreDelta, "Score.FinalTotal1200");
            validation.Equal(2200, clearSession.PhaseScore, "Score.EasyMaximum");
            validation.Equal(Phase3PlayFailure.PhaseAlreadyCleared, clearSession.BeginDrag(clearSession.Pieces[0].PieceId).Failure, "Score.ClearRepeatInputRejected");
            validation.Equal(2200, clearSession.PhaseScore, "Score.NoDuplicateClear");
        }

        private static void ValidateClear(ValidationContext validation)
        {
            Phase3PuzzleSessionModel session = NewSession(GameDifficulty.Easy);
            IReadOnlyList<Phase3PlayResult> firstFive = SolveCount(session, session.Pieces.Count - 1);
            validation.Check(!session.IsCleared, "Clear.OnePieceMissing", false, session.IsCleared);
            validation.Equal(0, firstFive[firstFive.Count - 1].ClearScoreDelta, "Clear.NoEarlyClearScore");
            validation.Equal(session.Pieces.Count - 1, CountPlaced(session), "Clear.PlacedCountBeforeFinal");
            validation.Equal(session.Slots.Count - 1, CountOccupied(session), "Clear.OccupiedCountBeforeFinal");

            Phase3PieceModel lastPiece = FirstUnplaced(session);
            Phase3AllowedTarget lastTarget = FirstAvailableTarget(session, lastPiece);
            session.BeginDrag(lastPiece.PieceId);
            Phase3PlayResult final = PlaceActiveAtTarget(session, lastPiece, lastTarget);
            validation.Check(final.IsSuccess && final.PhaseCleared && session.IsCleared, "Clear.LastPlacementClears", true, $"{final.Failure}/{final.PhaseCleared}/{session.IsCleared}");
            validation.Equal(session.Pieces.Count, CountPlaced(session), "Clear.AllPiecesPlaced");
            validation.Equal(session.Slots.Count, CountOccupied(session), "Clear.AllSlotsOccupied");
            validation.Check(session.IsClearStateConsistent(), "Clear.RelationshipsConsistent", true, false);
            validation.Equal(1000, final.ClearScoreDelta, "Clear.ScoreOnce");
            validation.Check(!session.HasActiveDrag, "Clear.NoActiveDrag", false, session.HasActiveDrag);
            validation.Equal(Phase3PlayFailure.PhaseAlreadyCleared, session.BeginDrag(lastPiece.PieceId).Failure, "Clear.BeginBlocked");
            validation.Equal(Phase3PlayFailure.PhaseAlreadyCleared, session.RotateActiveClockwise(lastPiece.PieceId).Failure, "Clear.RotateBlocked");
            validation.Equal(Phase3PlayFailure.PhaseAlreadyCleared, session.DropActiveOnField(lastPiece.PieceId, lastPiece.FieldCentroid).Failure, "Clear.DropBlocked");
            validation.Equal(Phase3PieceState.Placed, session.GetPiece(lastPiece.PieceId).State, "Clear.StateQueryPreserved");
        }

        private static void ValidateTemplate(ValidationContext validation, GameDifficulty difficulty)
        {
            Phase3SafeTemplate template = Phase3SafeTemplateCatalog.GetDefault(difficulty);
            Phase3DifficultyRuleSet rules = Phase3DifficultyRules.For(difficulty);
            Phase3PartitionValidationResult result = Phase3PartitionValidator.Validate(template.Puzzle, difficulty);
            validation.Equal(rules.TargetPieceCount, template.Puzzle.Pieces.Count, $"Template.{difficulty}.PieceCount");
            validation.Equal(rules.TargetPieceCount, template.Puzzle.Slots.Count, $"Template.{difficulty}.SlotCount");
            validation.Check(result.IsValid, $"Template.{difficulty}.PartitionValid", true, FirstIssue(result));
            validation.Near(256d, result.SlotAreaSum, $"Template.{difficulty}.AreaSum");
            validation.Check(!result.HasFailure(Phase3PartitionFailure.ProperEdgeCrossing) && !result.HasFailure(Phase3PartitionFailure.InteriorOverlap), $"Template.{difficulty}.NoOverlap", true, FirstIssue(result));
            validation.Check(result.SlotSignedDoubleAreaSum == Phase3CoreConstants.LogicalFieldArea * 2L, $"Template.{difficulty}.NoGap", Phase3CoreConstants.LogicalFieldArea * 2L, result.SlotSignedDoubleAreaSum);
            validation.Check(result.MinimumPieceAreaRatio >= rules.MinimumPieceAreaRatio - Phase3CoreConstants.ComparisonEpsilon && result.MaximumPieceAreaRatio <= rules.MaximumPieceAreaRatio + Phase3CoreConstants.ComparisonEpsilon && result.MaximumAspectRatio <= rules.MaximumAspectRatio + Phase3CoreConstants.ComparisonEpsilon && result.MaximumVertexCount <= rules.MaximumVertexCount, $"Template.{difficulty}.DifficultyLimits", true, $"{result.MinimumPieceAreaRatio:R}/{result.MaximumPieceAreaRatio:R}/{result.MaximumAspectRatio:R}/{result.MaximumVertexCount}");
            validation.Check(HasInterchangeablePair(template.Puzzle), $"Template.{difficulty}.InterchangeablePair", true, false);
        }

        private static void ValidateInvalidPartitions(ValidationContext validation)
        {
            Phase3SafeTemplate easy = Phase3SafeTemplateCatalog.GetDefault(GameDifficulty.Easy);
            validation.Throws<ArgumentException>(() => new Phase3ShapeDefinition("outside", new[] { P(-1, 0), P(2, 0), P(0, 2) }, 8), "Partition.OutsideFieldRejected");

            Phase3PuzzleDefinition shortage = ReplaceSlotShape(easy.Puzzle, "easy-slot-06", new Phase3ShapeDefinition("short-slot", new[] { P(8, 8), P(16, 8), P(16, 16) }, 8));
            Phase3PartitionValidationResult shortageResult = Phase3PartitionValidator.Validate(shortage, GameDifficulty.Easy);
            validation.Check(shortageResult.HasFailure(Phase3PartitionFailure.SlotAreaSumMismatch), "Partition.AreaShortageRejected", true, FirstIssue(shortageResult));

            Phase3PuzzleDefinition excess = ReplaceSlotShape(easy.Puzzle, "easy-slot-06", new Phase3ShapeDefinition("large-slot", new[] { P(4, 4), P(16, 4), P(16, 16), P(4, 16) }, 2));
            Phase3PartitionValidationResult excessResult = Phase3PartitionValidator.Validate(excess, GameDifficulty.Easy);
            validation.Check(excessResult.HasFailure(Phase3PartitionFailure.SlotAreaSumMismatch), "Partition.AreaExcessRejected", true, FirstIssue(excessResult));
            validation.Check(excessResult.HasFailure(Phase3PartitionFailure.InteriorOverlap) || excessResult.HasFailure(Phase3PartitionFailure.ProperEdgeCrossing), "Partition.InteriorOverlapRejected", true, FirstIssue(excessResult));

            Phase3PuzzleDefinition crossing = ReplaceSlotShape(easy.Puzzle, "easy-slot-01", new Phase3ShapeDefinition("crossing-slot", new[] { P(8, 2), P(14, 8), P(8, 14), P(2, 8) }, 2));
            Phase3PartitionValidationResult crossingResult = Phase3PartitionValidator.Validate(crossing, GameDifficulty.Easy);
            validation.Check(crossingResult.HasFailure(Phase3PartitionFailure.ProperEdgeCrossing), "Partition.ProperCrossingRejected", true, FirstIssue(crossingResult));

            Phase3PuzzleDefinition duplicateInterior = ReplaceSlotShape(easy.Puzzle, "easy-slot-02", easy.Puzzle.Slots[0].ShapeDefinition);
            Phase3PartitionValidationResult duplicateResult = Phase3PartitionValidator.Validate(duplicateInterior, GameDifficulty.Easy);
            validation.Check(duplicateResult.HasFailure(Phase3PartitionFailure.InteriorOverlap), "Partition.ContainedOverlapRejected", true, FirstIssue(duplicateResult));

            Phase3PartitionValidationResult countResult = Phase3PartitionValidator.Validate(easy.Puzzle, GameDifficulty.Normal);
            validation.Check(countResult.HasFailure(Phase3PartitionFailure.PieceCountMismatch) && countResult.HasFailure(Phase3PartitionFailure.SlotCountMismatch), "Partition.DifficultyCountMismatchRejected", true, FirstIssue(countResult));

            Phase3PuzzleDefinition smallPiecePuzzle = ReplacePieceShape(easy.Puzzle, easy.Puzzle.Pieces[0].PieceId, new Phase3ShapeDefinition("small-piece", new[] { P(0, 0), P(4, 0), P(4, 4) }, 8));
            Phase3PartitionValidationResult smallResult = Phase3PartitionValidator.Validate(smallPiecePuzzle, GameDifficulty.Easy);
            validation.Check(smallResult.HasFailure(Phase3PartitionFailure.PieceAreaRatioOutOfRange), "Partition.AreaRatioRejected", true, FirstIssue(smallResult));

            Phase3PuzzleDefinition thinPiecePuzzle = ReplacePieceShape(easy.Puzzle, easy.Puzzle.Pieces[0].PieceId, new Phase3ShapeDefinition("thin-piece", new[] { P(0, 0), P(2, 0), P(2, 8), P(0, 8) }, 4));
            Phase3PartitionValidationResult thinResult = Phase3PartitionValidator.Validate(thinPiecePuzzle, GameDifficulty.Easy);
            validation.Check(thinResult.HasFailure(Phase3PartitionFailure.AspectRatioExceeded), "Partition.AspectRatioRejected", true, FirstIssue(thinResult));

            Phase3PuzzleDefinition fiveVertexPuzzle = ReplacePieceShape(easy.Puzzle, easy.Puzzle.Pieces[0].PieceId, new Phase3ShapeDefinition("five-piece", new[] { P(0, 0), P(4, 0), P(5, 1), P(4, 2), P(0, 2) }, 8));
            Phase3PartitionValidationResult fiveResult = Phase3PartitionValidator.Validate(fiveVertexPuzzle, GameDifficulty.Easy);
            validation.Check(fiveResult.HasFailure(Phase3PartitionFailure.VertexCountExceeded), "Partition.VertexCountRejected", true, FirstIssue(fiveResult));

            Phase3ShapeDefinition targetShape = new Phase3ShapeDefinition("target-shape", new[] { P(0, 0), P(4, 0), P(0, 4) }, 8);
            var missingTargetPiece = new Phase3PieceDefinition("missing-piece", "missing-deck", targetShape, new[] { new Phase3AllowedTarget("missing-slot", default) });
            var existingSlot = new Phase3SlotDefinition("existing-slot", targetShape, default, default);
            validation.Throws<ArgumentException>(() => new Phase3PuzzleDefinition(new[] { missingTargetPiece }, new[] { existingSlot }), "Partition.TargetReferenceRejected");

            Phase3PartitionValidationResult validEasy = Phase3PartitionValidator.Validate(easy.Puzzle, GameDifficulty.Easy);
            validation.Check(validEasy.IsValid && HasSharedEdge(easy.Puzzle.Slots), "Partition.SharedEdgeAllowed", true, FirstIssue(validEasy));
            validation.Check(validEasy.IsValid && HasSharedVertexWithoutSharedEdge(easy.Puzzle.Slots), "Partition.SharedVertexAllowed", true, FirstIssue(validEasy));
        }

        private static void ValidateImmutabilityAndDeterminism(ValidationContext validation)
        {
            Phase3SafeTemplate first = Phase3SafeTemplateCatalog.GetDefault(GameDifficulty.Easy);
            Phase3SafeTemplate second = Phase3SafeTemplateCatalog.GetDefault(GameDifficulty.Easy);
            validation.Throws<NotSupportedException>(() => ((IList<Phase3PieceDefinition>)first.Puzzle.Pieces).Add(first.Puzzle.Pieces[0]), "Immutable.CatalogPieces");
            validation.Throws<NotSupportedException>(() => ((IList<Phase3SlotDefinition>)first.Puzzle.Slots).Clear(), "Immutable.CatalogSlots");
            validation.Throws<NotSupportedException>(() => ((IList<Phase3InitialPieceRotation>)first.InitialRotations).Clear(), "Immutable.InitialRotations");

            Phase3PuzzleSessionModel session = NewSession(GameDifficulty.Easy);
            validation.Throws<NotSupportedException>(() => ((IList<Phase3PieceModel>)session.Pieces).Clear(), "Immutable.SessionPieces");
            validation.Throws<NotSupportedException>(() => ((IList<Phase3SlotModel>)session.Slots).Clear(), "Immutable.SessionSlots");
            validation.SequenceEqual(GetPieceIds(first.Puzzle), GetPieceIds(second.Puzzle), "Determinism.TemplatePieceIds");
            validation.SequenceEqual(GetSlotIds(first.Puzzle), GetSlotIds(second.Puzzle), "Determinism.TemplateSlotIds");
            validation.SequenceEqual(GetInitialSteps(first), GetInitialSteps(second), "Determinism.InitialRotations");

            Phase3PuzzleDefinition reversed = new Phase3PuzzleDefinition(Reverse(first.Puzzle.Pieces), Reverse(first.Puzzle.Slots));
            Phase3PartitionValidationResult originalResult = Phase3PartitionValidator.Validate(first.Puzzle, GameDifficulty.Easy);
            Phase3PartitionValidationResult reversedResult = Phase3PartitionValidator.Validate(reversed, GameDifficulty.Easy);
            validation.Check(originalResult.IsValid == reversedResult.IsValid && originalResult.SlotSignedDoubleAreaSum == reversedResult.SlotSignedDoubleAreaSum, "Determinism.InputOrderIndependent", "same result", $"{originalResult.IsValid}/{originalResult.SlotSignedDoubleAreaSum} vs {reversedResult.IsValid}/{reversedResult.SlotSignedDoubleAreaSum}");

            Phase3PuzzleSessionModel operationOne = NewSession(GameDifficulty.Easy);
            Phase3PuzzleSessionModel operationTwo = NewSession(GameDifficulty.Easy);
            Phase3PieceModel pieceOne = operationOne.Pieces[0];
            Phase3PieceModel pieceTwo = operationTwo.GetPiece(pieceOne.PieceId);
            operationOne.BeginDrag(pieceOne.PieceId);
            operationTwo.BeginDrag(pieceTwo.PieceId);
            Phase3PlayResult resultOne = PlaceActiveAtTarget(operationOne, pieceOne, pieceOne.Definition.AllowedTargets[0]);
            Phase3PlayResult resultTwo = PlaceActiveAtTarget(operationTwo, pieceTwo, pieceTwo.Definition.AllowedTargets[0]);
            validation.Check(resultOne.Failure == resultTwo.Failure && resultOne.TargetSlotId == resultTwo.TargetSlotId && resultOne.TotalScoreDelta == resultTwo.TotalScoreDelta, "Determinism.OperationRepeat", "same result", $"{resultOne.Failure}/{resultOne.TargetSlotId}/{resultOne.TotalScoreDelta} vs {resultTwo.Failure}/{resultTwo.TargetSlotId}/{resultTwo.TotalScoreDelta}");

            var occupiedForward = new HashSet<string>(StringComparer.Ordinal) { "z", "a" };
            var occupiedReverse = new HashSet<string>(StringComparer.Ordinal) { "a", "z" };
            Phase3PieceDefinition snapPiece = first.Puzzle.Pieces[0];
            Phase3SlotDefinition[] slotArray = CopySlots(first.Puzzle.Slots);
            Phase3SnapResult forward = Phase3SnapRules.Evaluate(snapPiece, first.Puzzle.Slots[0].CorrectCentroid, snapPiece.AllowedTargets[0].RequiredRotationStep, 2000d, slotArray, occupiedForward);
            Phase3SnapResult reverseOrder = Phase3SnapRules.Evaluate(snapPiece, first.Puzzle.Slots[0].CorrectCentroid, snapPiece.AllowedTargets[0].RequiredRotationStep, 2000d, Reverse(slotArray), occupiedReverse);
            validation.Check(forward.Code == reverseOrder.Code && forward.TargetSlotId == reverseOrder.TargetSlotId, "Determinism.HashSetAndCollectionOrderIndependent", "same snap", $"{forward.Code}/{forward.TargetSlotId} vs {reverseOrder.Code}/{reverseOrder.TargetSlotId}");
            validation.Check(!HasMutablePublicSetter(typeof(Phase3PieceModel)) && !HasMutablePublicSetter(typeof(Phase3SlotModel)), "Immutable.NoPublicStateSetters", true, false);
        }

        private static void ValidateCatalogRotationCorrection(ValidationContext validation)
        {
            int affectedTargetCount = 0;
            int totalTargetCount = 0;
            GameDifficulty[] difficulties = { GameDifficulty.Easy, GameDifficulty.Normal, GameDifficulty.Hard };
            for (int difficultyIndex = 0; difficultyIndex < difficulties.Length; difficultyIndex++)
            {
                GameDifficulty difficulty = difficulties[difficultyIndex];
                Phase3SafeTemplate template = Phase3SafeTemplateCatalog.GetDefault(difficulty);
                Phase3PartitionValidationResult partition = Phase3PartitionValidator.Validate(template.Puzzle, difficulty);
                validation.Check(!partition.HasFailure(Phase3PartitionFailure.PieceSlotShapeMismatch), $"Correction.{difficulty}.AllTargetsCongruent", true, FirstIssue(partition));

                for (int pieceIndex = 0; pieceIndex < template.Puzzle.Pieces.Count; pieceIndex++)
                {
                    Phase3PieceDefinition piece = template.Puzzle.Pieces[pieceIndex];
                    for (int targetIndex = 0; targetIndex < piece.AllowedTargets.Count; targetIndex++)
                    {
                        Phase3AllowedTarget target = piece.AllowedTargets[targetIndex];
                        bool affected = IsCorrectedTriangleSlot(difficulty, target.SlotId);
                        int finalStep = target.RequiredRotationStep.Add(target.RotationCorrectionStep.Value).Value;
                        bool valuesCorrect = affected
                            ? target.RequiredRotationStep.Value == 2 && target.RotationCorrectionStep.Value == 2 && finalStep == 4
                            : target.RotationCorrectionStep.Value == 0 && finalStep == target.RequiredRotationStep.Value;
                        validation.Check(valuesCorrect, $"Correction.{piece.PieceId}->{target.SlotId}", true, $"required={target.RequiredRotationStep.Value}, correction={target.RotationCorrectionStep.Value}, final={finalStep}");
                        totalTargetCount++;
                        if (affected)
                        {
                            affectedTargetCount++;
                        }
                    }
                }
            }

            validation.Equal(34, affectedTargetCount, "Correction.AffectedTargetCount");
            validation.Equal(89, totalTargetCount, "Correction.TotalTargetCount");

            Phase3SafeTemplate easy = Phase3SafeTemplateCatalog.GetDefault(GameDifficulty.Easy);
            Phase3PieceDefinition definition = FindPieceDefinition(easy.Puzzle, "easy-piece-triangle-01");
            Phase3AllowedTarget correctedTarget = FindAllowed(definition, "easy-slot-02");
            Phase3SlotDefinition correctedSlot = FindSlotDefinition(easy.Puzzle, correctedTarget.SlotId);
            double snapDistance = Phase3DifficultyRules.For(GameDifficulty.Easy).SnapDistance;
            Phase3SnapResult accepted = Phase3SnapRules.Evaluate(
                definition,
                correctedSlot.CorrectCentroid,
                new Phase3RotationStep(2),
                snapDistance,
                easy.Puzzle.Slots,
                Array.Empty<string>());
            validation.Equal(Phase3SnapResultCode.Success, accepted.Code, "Correction.SnapRequiredStepAccepted");
            validation.Equal(new Phase3RotationStep(2), accepted.RequiredRotation, "Correction.SnapResultRequired");
            validation.Equal(new Phase3RotationStep(2), accepted.RotationCorrection, "Correction.SnapResultCorrection");

            Phase3SnapResult rejected = Phase3SnapRules.Evaluate(
                definition,
                correctedSlot.CorrectCentroid,
                new Phase3RotationStep(1),
                snapDistance,
                easy.Puzzle.Slots,
                Array.Empty<string>());
            validation.Equal(Phase3SnapResultCode.RotationMismatch, rejected.Code, "Correction.IncompatibleRotationRejected");

            Phase3PuzzleSessionModel session = NewSession(GameDifficulty.Easy);
            Phase3PieceModel sessionPiece = session.GetPiece(definition.PieceId);
            Phase3PlayResult begin = session.BeginDrag(sessionPiece.PieceId);
            RotateTo(session, sessionPiece, correctedTarget.RequiredRotationStep);
            validation.Check(begin.IsSuccess && sessionPiece.CurrentRotation == new Phase3RotationStep(2), "Correction.SessionInputStepTwo", true, $"{begin.Failure}/{sessionPiece.CurrentRotation}");
            Phase3PlayResult placement = session.DropActiveOnField(sessionPiece.PieceId, correctedSlot.CorrectCentroid);
            validation.Check(placement.IsSuccess && placement.PiecePlaced && placement.TargetSlotId == correctedSlot.SlotId, "Correction.SessionPlacementSuccess", true, $"{placement.Failure}/{placement.TargetSlotId}");
            validation.Equal(new Phase3RotationStep(4), sessionPiece.CurrentRotation, "Correction.SessionFinalStepFour");
            validation.Equal(200, placement.ManualSnapScoreDelta, "Correction.ManualSnapScoreUnchanged");
            validation.Equal(200, placement.TotalScoreDelta, "Correction.NoAdditionalScore");
            validation.Check(CountOccupied(session) == 1 && CountPlaced(session) == 1, "Correction.SingleOccupancyAndPlacement", "1/1", $"{CountOccupied(session)}/{CountPlaced(session)}");
            validation.Check(!session.IsCleared && placement.ClearScoreDelta == 0, "Correction.NoEarlyClearEffect", "false/0", $"{session.IsCleared}/{placement.ClearScoreDelta}");
        }

        private static void ValidateShapeCongruence(ValidationContext validation)
        {
            Phase3SafeTemplate hard = Phase3SafeTemplateCatalog.GetDefault(GameDifficulty.Hard);
            Phase3PieceDefinition triangle = FindPieceDefinition(hard.Puzzle, "hard-piece-triangle-01");
            string triangleRectangleSubject = $"{triangle.PieceId}->hard-slot-01";
            Phase3PuzzleDefinition triangleToRectangle = ReplacePiece(
                hard.Puzzle,
                triangle.PieceId,
                triangle.ShapeDefinition,
                new[] { new Phase3AllowedTarget("hard-slot-01", default) });
            Phase3PartitionValidationResult triangleRectangleResult = Phase3PartitionValidator.Validate(triangleToRectangle, GameDifficulty.Hard);
            validation.Check(HasIssue(triangleRectangleResult, Phase3PartitionFailure.PieceSlotShapeMismatch, triangleRectangleSubject), "Shape.TriangleRectangleRejected", true, FirstIssue(triangleRectangleResult));
            validation.Check(!HasIssue(triangleRectangleResult, Phase3PartitionFailure.PieceSlotAreaMismatch, triangleRectangleSubject), "Shape.TriangleRectangleAreaStillMatches", true, FirstIssue(triangleRectangleResult));

            Phase3PartitionValidationResult translatedResult = Phase3PartitionValidator.Validate(hard.Puzzle, GameDifficulty.Hard);
            validation.Check(!HasIssue(translatedResult, Phase3PartitionFailure.PieceSlotShapeMismatch, "hard-piece-rectangle-01->hard-slot-07"), "Shape.TranslationAllowed", true, FirstIssue(translatedResult));

            string correctedSubject = $"{triangle.PieceId}->hard-slot-04";
            Phase3PuzzleDefinition requiredOnly = ReplacePiece(
                hard.Puzzle,
                triangle.PieceId,
                triangle.ShapeDefinition,
                new[] { new Phase3AllowedTarget("hard-slot-04", new Phase3RotationStep(2)) });
            Phase3PartitionValidationResult requiredOnlyResult = Phase3PartitionValidator.Validate(requiredOnly, GameDifficulty.Hard);
            validation.Check(HasIssue(requiredOnlyResult, Phase3PartitionFailure.PieceSlotShapeMismatch, correctedSubject), "Shape.RequiredWithoutCorrectionRejected", true, FirstIssue(requiredOnlyResult));
            validation.Check(!HasIssue(translatedResult, Phase3PartitionFailure.PieceSlotShapeMismatch, correctedSubject), "Shape.RequiredPlusCorrectionAccepted", true, FirstIssue(translatedResult));

            Phase3PieceDefinition rectangle = FindPieceDefinition(hard.Puzzle, "hard-piece-rectangle-01");
            var cyclicRectangle = new Phase3ShapeDefinition(
                "cyclic-rectangle",
                new[] { P(4, 0), P(4, 8), P(0, 8), P(0, 0) },
                4);
            Phase3PuzzleDefinition cyclicPuzzle = ReplacePiece(hard.Puzzle, rectangle.PieceId, cyclicRectangle, rectangle.AllowedTargets);
            Phase3PartitionValidationResult cyclicResult = Phase3PartitionValidator.Validate(cyclicPuzzle, GameDifficulty.Hard);
            validation.Check(!cyclicResult.HasFailure(Phase3PartitionFailure.PieceSlotShapeMismatch), "Shape.CyclicStartAllowed", true, FirstIssue(cyclicResult));

            var original = new Phase3ShapeDefinition(
                "asymmetric-original",
                new[] { P(0, 0), P(8, 0), P(4, 4), P(0, 4) },
                8);
            var mirrored = new Phase3ShapeDefinition(
                "asymmetric-mirrored",
                new[] { P(0, 0), P(8, 0), P(8, 4), P(4, 4) },
                8);
            string mirrorSubject = $"{triangle.PieceId}->hard-slot-03";
            Phase3PuzzleDefinition mirrorPuzzle = ReplaceSlotShape(
                ReplacePiece(hard.Puzzle, triangle.PieceId, original, new[] { new Phase3AllowedTarget("hard-slot-03", default) }),
                "hard-slot-03",
                mirrored);
            Phase3PartitionValidationResult mirrorResult = Phase3PartitionValidator.Validate(mirrorPuzzle, GameDifficulty.Hard);
            validation.Check(HasIssue(mirrorResult, Phase3PartitionFailure.PieceSlotShapeMismatch, mirrorSubject), "Shape.MirrorReverseTraversalRejected", true, FirstIssue(mirrorResult));
            validation.Check(!HasIssue(mirrorResult, Phase3PartitionFailure.PieceSlotAreaMismatch, mirrorSubject), "Shape.MirrorAreaStillMatches", true, FirstIssue(mirrorResult));

            Phase3PartitionValidationResult repeatedMirrorResult = Phase3PartitionValidator.Validate(mirrorPuzzle, GameDifficulty.Hard);
            validation.Check(IssueSummary(mirrorResult) == IssueSummary(repeatedMirrorResult), "Shape.RepeatDeterministic", IssueSummary(mirrorResult), IssueSummary(repeatedMirrorResult));
        }

        private static void ValidateConvexOverlap(ValidationContext validation)
        {
            Phase3SafeTemplate hard = Phase3SafeTemplateCatalog.GetDefault(GameDifficulty.Hard);
            var first = new Phase3ShapeDefinition(
                "collinear-overlap-a",
                new[] { P(0, 0), P(10, 0), P(10, 2), P(0, 2) },
                4);
            var second = new Phase3ShapeDefinition(
                "collinear-overlap-b",
                new[] { P(5, 0), P(15, 0), P(15, 2), P(5, 2) },
                4);
            Phase3PuzzleDefinition overlapPuzzle = ReplaceSlotShape(
                ReplaceSlotShape(hard.Puzzle, "hard-slot-01", first),
                "hard-slot-02",
                second);
            Phase3PartitionValidationResult overlapResult = Phase3PartitionValidator.Validate(overlapPuzzle, GameDifficulty.Hard);
            const string overlapSubject = "hard-slot-01|hard-slot-02";
            validation.Check(HasIssue(overlapResult, Phase3PartitionFailure.InteriorOverlap, overlapSubject), "Overlap.CollinearPositiveAreaRejected", true, FirstIssue(overlapResult));
            validation.Check(!HasIssue(overlapResult, Phase3PartitionFailure.ProperEdgeCrossing, overlapSubject), "Overlap.CollinearNotMisreportedAsProperCrossing", true, FirstIssue(overlapResult));

            MethodInfo areaMethod = typeof(Phase3PartitionValidator).GetMethod("ConvexIntersectionArea", BindingFlags.NonPublic | BindingFlags.Static);
            double area = areaMethod == null
                ? double.NaN
                : (double)areaMethod.Invoke(null, new object[] { first.Vertices, second.Vertices });
            double reverseArea = areaMethod == null
                ? double.NaN
                : (double)areaMethod.Invoke(null, new object[] { second.Vertices, first.Vertices });
            validation.Near(10d, area, "Overlap.CollinearIntersectionAreaTen");
            validation.Near(10d, reverseArea, "Overlap.SubjectClipOrderIndependentArea");

            Phase3PuzzleDefinition reverseOrderPuzzle = new Phase3PuzzleDefinition(hard.Puzzle.Pieces, Reverse(overlapPuzzle.Slots));
            Phase3PartitionValidationResult reverseOrderResult = Phase3PartitionValidator.Validate(reverseOrderPuzzle, GameDifficulty.Hard);
            validation.Check(HasIssue(reverseOrderResult, Phase3PartitionFailure.InteriorOverlap, overlapSubject), "Overlap.PolygonCollectionOrderIndependent", true, FirstIssue(reverseOrderResult));

            Phase3PartitionValidationResult repeated = Phase3PartitionValidator.Validate(overlapPuzzle, GameDifficulty.Hard);
            validation.Check(IssueSummary(overlapResult) == IssueSummary(repeated), "Overlap.RepeatDeterministic", IssueSummary(overlapResult), IssueSummary(repeated));
        }

        private static void ValidateInitialRotationContract(ValidationContext validation)
        {
            ValidateInitialRotationTemplate(validation, GameDifficulty.Easy, new[]
            {
                E("easy-piece-triangle-01", 0), E("easy-piece-triangle-02", 2),
                E("easy-piece-triangle-03", 0), E("easy-piece-triangle-04", 2),
                E("easy-piece-square-01", 0), E("easy-piece-square-02", 2)
            }, false);
            ValidateInitialRotationTemplate(validation, GameDifficulty.Normal, new[]
            {
                E("normal-piece-triangle-01", 0), E("normal-piece-triangle-02", 1),
                E("normal-piece-triangle-03", 2), E("normal-piece-triangle-04", 3),
                E("normal-piece-triangle-05", 4), E("normal-piece-triangle-06", 5),
                E("normal-piece-square-01", 6)
            }, true);
            ValidateInitialRotationTemplate(validation, GameDifficulty.Hard, new[]
            {
                E("hard-piece-rectangle-01", 0), E("hard-piece-rectangle-02", 1),
                E("hard-piece-triangle-01", 2), E("hard-piece-triangle-02", 3),
                E("hard-piece-triangle-03", 4), E("hard-piece-triangle-04", 5),
                E("hard-piece-rectangle-03", 6), E("hard-piece-rectangle-04", 7)
            }, true);

            Phase3SafeTemplate easy = Phase3SafeTemplateCatalog.GetDefault(GameDifficulty.Easy);
            Phase3SafeTemplate normal = Phase3SafeTemplateCatalog.GetDefault(GameDifficulty.Normal);
            Phase3SafeTemplate hard = Phase3SafeTemplateCatalog.GetDefault(GameDifficulty.Hard);
            validation.Check(AllStepsAre(easy, 0, 2), "Initial.EasyOnlyZeroAndTwo", true, string.Join(",", GetInitialSteps(easy)));
            validation.Check(ContainsOddStep(normal), "Initial.NormalContainsOddStep", true, string.Join(",", GetInitialSteps(normal)));
            validation.Check(ContainsEveryStepExactlyOnce(hard), "Initial.HardUsesEveryStepOnce", true, string.Join(",", GetInitialSteps(hard)));
        }

        private static void ValidateInitialRotationTemplate(
            ValidationContext validation,
            GameDifficulty difficulty,
            IReadOnlyList<InitialRotationExpectation> expected,
            bool validateCatalogRepeat)
        {
            Phase3SafeTemplate first = Phase3SafeTemplateCatalog.GetDefault(difficulty);
            Phase3SafeTemplate second = Phase3SafeTemplateCatalog.GetDefault(difficulty);
            Phase3PuzzleSessionModel firstSession = new Phase3PuzzleSessionModel(first.Puzzle, difficulty, first.InitialRotations);
            Phase3PuzzleSessionModel secondSession = new Phase3PuzzleSessionModel(second.Puzzle, difficulty, second.InitialRotations);
            validation.Equal(expected.Count, first.InitialRotations.Count, $"Initial.{difficulty}.OneToOneCount");
            for (int i = 0; i < expected.Count; i++)
            {
                InitialRotationExpectation item = expected[i];
                validation.Equal(new Phase3RotationStep(item.Step), first.GetInitialRotation(item.PieceId), $"Initial.{difficulty}.{item.PieceId}.Catalog");
                validation.Equal(new Phase3RotationStep(item.Step), firstSession.GetPiece(item.PieceId).CurrentRotation, $"Initial.{difficulty}.{item.PieceId}.Session");
            }

            if (validateCatalogRepeat)
            {
                validation.SequenceEqual(GetInitialSteps(first), GetInitialSteps(second), $"Initial.{difficulty}.CatalogRepeat");
            }

            validation.SequenceEqual(GetSessionRotationSteps(firstSession), GetSessionRotationSteps(secondSession), $"Initial.{difficulty}.SessionRepeat");
        }

        private static void ValidateClockwiseGeometryDirection(ValidationContext validation)
        {
            validation.Equal(7, RotateProductionDirection(0, 1), "Direction.EastStepOneClockwiseToSouthEast");
            validation.Equal(1, RotateProductionDirection(0, 7), "Direction.EastStepSevenToNorthEast");
            validation.Equal(0, RotateProductionDirection(1, 1), "Direction.NorthEastStepOneToEast");
            validation.Equal(0, RotateProductionDirection(7, 7), "Direction.SouthEastStepSevenToEast");
            validation.Equal(0, RotateProductionDirection(0, 8), "Direction.FullTurnReturnsEast");
            validation.Equal(1, RotateProductionDirection(0, -1), "Direction.NegativeStepNormalizesToNorthEast");

            const string subject = "hard-piece-triangle-01->hard-slot-03";
            Phase3PuzzleDefinition clockwisePuzzle = CreateAsymmetricRotationPuzzle(new Phase3RotationStep(2), false);
            Phase3PartitionValidationResult clockwiseResult = Phase3PartitionValidator.Validate(clockwisePuzzle, GameDifficulty.Hard);
            validation.Check(!HasIssue(clockwiseResult, Phase3PartitionFailure.PieceSlotShapeMismatch, subject), "Direction.AsymmetricClockwiseStepTwoAccepted", true, FirstIssue(clockwiseResult));

            Phase3PieceDefinition clockwisePiece = FindPieceDefinition(clockwisePuzzle, "hard-piece-triangle-01");
            Phase3SlotDefinition clockwiseSlot = FindSlotDefinition(clockwisePuzzle, "hard-slot-03");
            validation.Check(
                clockwisePiece.ShapeDefinition.Centroid != clockwiseSlot.ShapeDefinition.Centroid &&
                !HasIssue(clockwiseResult, Phase3PartitionFailure.PieceSlotShapeMismatch, subject),
                "Direction.AsymmetricTranslationAllowed",
                true,
                $"{clockwisePiece.ShapeDefinition.Centroid}/{clockwiseSlot.ShapeDefinition.Centroid}/{FirstIssue(clockwiseResult)}");

            Phase3PuzzleDefinition counterclockwisePuzzle = CreateAsymmetricRotationPuzzle(new Phase3RotationStep(6), false);
            Phase3PartitionValidationResult counterclockwiseResult = Phase3PartitionValidator.Validate(counterclockwisePuzzle, GameDifficulty.Hard);
            validation.Check(HasIssue(counterclockwiseResult, Phase3PartitionFailure.PieceSlotShapeMismatch, subject), "Direction.AsymmetricOppositeStepSixRejected", true, FirstIssue(counterclockwiseResult));

            Phase3PuzzleDefinition cyclicPuzzle = CreateAsymmetricRotationPuzzle(new Phase3RotationStep(2), true);
            Phase3PartitionValidationResult cyclicResult = Phase3PartitionValidator.Validate(cyclicPuzzle, GameDifficulty.Hard);
            validation.Check(!HasIssue(cyclicResult, Phase3PartitionFailure.PieceSlotShapeMismatch, subject), "Direction.AsymmetricCyclicStartAllowed", true, FirstIssue(cyclicResult));

            Phase3PartitionValidationResult repeatedResult = Phase3PartitionValidator.Validate(clockwisePuzzle, GameDifficulty.Hard);
            validation.Check(IssueSummary(clockwiseResult) == IssueSummary(repeatedResult), "Direction.AsymmetricRepeatDeterministic", IssueSummary(clockwiseResult), IssueSummary(repeatedResult));
        }

        private static Phase3PuzzleDefinition CreateAsymmetricRotationPuzzle(
            Phase3RotationStep targetRotation,
            bool cyclicSlotStart)
        {
            Phase3SafeTemplate hard = Phase3SafeTemplateCatalog.GetDefault(GameDifficulty.Hard);
            Phase3PieceDefinition sourcePiece = FindPieceDefinition(hard.Puzzle, "hard-piece-triangle-01");
            var pieceShape = new Phase3ShapeDefinition(
                "clockwise-asymmetric-piece",
                new[] { P(0, 0), P(6, 0), P(4, 2), P(0, 2) },
                8);
            Phase3GridPoint[] slotVertices = cyclicSlotStart
                ? new[] { P(6, 6), P(6, 10), P(4, 10), P(4, 4) }
                : new[] { P(4, 10), P(4, 4), P(6, 6), P(6, 10) };
            var slotShape = new Phase3ShapeDefinition("clockwise-asymmetric-slot", slotVertices, 8);
            Phase3PuzzleDefinition pieceReplaced = ReplacePiece(
                hard.Puzzle,
                sourcePiece.PieceId,
                pieceShape,
                new[] { new Phase3AllowedTarget("hard-slot-03", targetRotation) });
            return ReplaceSlotShape(pieceReplaced, "hard-slot-03", slotShape);
        }

        private static int RotateProductionDirection(int direction, int step)
        {
            Type edgeType = typeof(Phase3PartitionValidator).GetNestedType("EdgeSignature", BindingFlags.NonPublic);
            ConstructorInfo constructor = edgeType?.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(int), typeof(long) },
                null);
            MethodInfo rotate = edgeType?.GetMethod("Rotate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            PropertyInfo directionProperty = edgeType?.GetProperty("Direction", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (constructor == null || rotate == null || directionProperty == null)
            {
                throw new MissingMemberException(typeof(Phase3PartitionValidator).FullName, "EdgeSignature rotation contract");
            }

            object edge = constructor.Invoke(new object[] { direction, 1L });
            object rotated = rotate.Invoke(edge, new object[] { step });
            return (int)directionProperty.GetValue(rotated);
        }

        private static Phase3PuzzleSessionModel NewSession(GameDifficulty difficulty)
        {
            Phase3SafeTemplate template = Phase3SafeTemplateCatalog.GetDefault(difficulty);
            return new Phase3PuzzleSessionModel(template.Puzzle, difficulty, template.InitialRotations);
        }

        private static Phase3PlayResult PlaceActiveAtTarget(
            Phase3PuzzleSessionModel session,
            Phase3PieceModel piece,
            Phase3AllowedTarget target)
        {
            RotateTo(session, piece, target.RequiredRotationStep);
            return session.DropActiveOnField(piece.PieceId, session.GetSlot(target.SlotId).Definition.CorrectCentroid);
        }

        private static void RotateTo(Phase3PuzzleSessionModel session, Phase3PieceModel piece, Phase3RotationStep target)
        {
            int delta = piece.CurrentRotation.NormalizedDeltaTo(target);
            session.RotateActive(piece.PieceId, delta);
        }

        private static IReadOnlyList<Phase3PlayResult> SolveAll(Phase3PuzzleSessionModel session)
        {
            return SolveCount(session, session.Pieces.Count);
        }

        private static IReadOnlyList<Phase3PlayResult> SolveCount(Phase3PuzzleSessionModel session, int count)
        {
            var results = new List<Phase3PlayResult>();
            for (int i = 0; i < session.Pieces.Count && results.Count < count; i++)
            {
                Phase3PieceModel piece = session.Pieces[i];
                if (piece.State == Phase3PieceState.Placed)
                {
                    continue;
                }

                Phase3AllowedTarget target = FirstAvailableTarget(session, piece);
                session.BeginDrag(piece.PieceId);
                results.Add(PlaceActiveAtTarget(session, piece, target));
            }

            return results.AsReadOnly();
        }

        private static Phase3AllowedTarget FirstAvailableTarget(Phase3PuzzleSessionModel session, Phase3PieceModel piece)
        {
            for (int i = 0; i < piece.Definition.AllowedTargets.Count; i++)
            {
                Phase3AllowedTarget target = piece.Definition.AllowedTargets[i];
                Phase3SlotModel slot = session.GetSlot(target.SlotId);
                if (slot != null && !slot.IsOccupied)
                {
                    return target;
                }
            }

            throw new InvalidOperationException($"Piece '{piece.PieceId}' has no available target.");
        }

        private static Phase3AllowedTarget FindTarget(Phase3PieceModel piece, string slotId)
        {
            for (int i = 0; i < piece.Definition.AllowedTargets.Count; i++)
            {
                if (piece.Definition.AllowedTargets[i].SlotId == slotId)
                {
                    return piece.Definition.AllowedTargets[i];
                }
            }

            throw new InvalidOperationException($"Target '{slotId}' was not found for '{piece.PieceId}'.");
        }

        private static Phase3PieceModel FindPieceByShapeIdPart(Phase3PuzzleSessionModel session, string part)
        {
            for (int i = 0; i < session.Pieces.Count; i++)
            {
                if (session.Pieces[i].Definition.ShapeDefinition.ShapeId.IndexOf(part, StringComparison.Ordinal) >= 0)
                {
                    return session.Pieces[i];
                }
            }

            throw new InvalidOperationException($"No piece shape contains '{part}'.");
        }

        private static Phase3PieceModel FirstUnplaced(Phase3PuzzleSessionModel session)
        {
            for (int i = 0; i < session.Pieces.Count; i++)
            {
                if (session.Pieces[i].State != Phase3PieceState.Placed)
                {
                    return session.Pieces[i];
                }
            }

            throw new InvalidOperationException("No unplaced piece remains.");
        }

        private static Phase3PuzzleSessionModel CreateRestrictedTargetSession()
        {
            Phase3SafeTemplate template = Phase3SafeTemplateCatalog.GetDefault(GameDifficulty.Easy);
            var pieces = new List<Phase3PieceDefinition>();
            for (int i = 0; i < template.Puzzle.Pieces.Count; i++)
            {
                Phase3PieceDefinition source = template.Puzzle.Pieces[i];
                if (source.PieceId == "easy-piece-triangle-01")
                {
                    pieces.Add(new Phase3PieceDefinition(source.PieceId, source.OriginalDeckSlotId, source.ShapeDefinition, new[] { FindAllowed(source, "easy-slot-01"), FindAllowed(source, "easy-slot-02") }));
                }
                else if (source.PieceId == "easy-piece-triangle-02")
                {
                    pieces.Add(new Phase3PieceDefinition(source.PieceId, source.OriginalDeckSlotId, source.ShapeDefinition, new[] { FindAllowed(source, "easy-slot-01") }));
                }
                else
                {
                    pieces.Add(source);
                }
            }

            var puzzle = new Phase3PuzzleDefinition(pieces, template.Puzzle.Slots);
            return new Phase3PuzzleSessionModel(puzzle, GameDifficulty.Easy);
        }

        private static Phase3AllowedTarget FindAllowed(Phase3PieceDefinition piece, string slotId)
        {
            for (int i = 0; i < piece.AllowedTargets.Count; i++)
            {
                if (piece.AllowedTargets[i].SlotId == slotId)
                {
                    return piece.AllowedTargets[i];
                }
            }

            throw new InvalidOperationException($"Allowed target '{slotId}' was not found.");
        }

        private static Phase3PieceDefinition FindPieceDefinition(Phase3PuzzleDefinition puzzle, string pieceId)
        {
            for (int i = 0; i < puzzle.Pieces.Count; i++)
            {
                if (puzzle.Pieces[i].PieceId == pieceId)
                {
                    return puzzle.Pieces[i];
                }
            }

            throw new InvalidOperationException($"Piece definition '{pieceId}' was not found.");
        }

        private static Phase3SlotDefinition FindSlotDefinition(Phase3PuzzleDefinition puzzle, string slotId)
        {
            for (int i = 0; i < puzzle.Slots.Count; i++)
            {
                if (puzzle.Slots[i].SlotId == slotId)
                {
                    return puzzle.Slots[i];
                }
            }

            throw new InvalidOperationException($"Slot definition '{slotId}' was not found.");
        }

        private static bool IsCorrectedTriangleSlot(GameDifficulty difficulty, string slotId)
        {
            switch (difficulty)
            {
                case GameDifficulty.Easy:
                    return slotId == "easy-slot-02" || slotId == "easy-slot-04";
                case GameDifficulty.Normal:
                    return slotId == "normal-slot-02" || slotId == "normal-slot-04" || slotId == "normal-slot-06";
                case GameDifficulty.Hard:
                    return slotId == "hard-slot-04" || slotId == "hard-slot-06";
                default:
                    return false;
            }
        }

        private static Phase3PuzzleDefinition ReplacePiece(
            Phase3PuzzleDefinition source,
            string pieceId,
            Phase3ShapeDefinition replacementShape,
            IEnumerable<Phase3AllowedTarget> replacementTargets)
        {
            var pieces = new List<Phase3PieceDefinition>();
            for (int i = 0; i < source.Pieces.Count; i++)
            {
                Phase3PieceDefinition piece = source.Pieces[i];
                pieces.Add(piece.PieceId == pieceId
                    ? new Phase3PieceDefinition(piece.PieceId, piece.OriginalDeckSlotId, replacementShape, replacementTargets)
                    : piece);
            }

            return new Phase3PuzzleDefinition(pieces, source.Slots);
        }

        private static bool HasIssue(
            Phase3PartitionValidationResult result,
            Phase3PartitionFailure failure,
            string subjectId)
        {
            for (int i = 0; i < result.Issues.Count; i++)
            {
                if (result.Issues[i].Failure == failure && result.Issues[i].SubjectId == subjectId)
                {
                    return true;
                }
            }

            return false;
        }

        private static string IssueSummary(Phase3PartitionValidationResult result)
        {
            var values = new string[result.Issues.Count];
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = result.Issues[i].ToString();
            }

            return string.Join("|", values);
        }

        private static InitialRotationExpectation E(string pieceId, int step)
        {
            return new InitialRotationExpectation(pieceId, step);
        }

        private static bool AllStepsAre(Phase3SafeTemplate template, int firstAllowed, int secondAllowed)
        {
            for (int i = 0; i < template.InitialRotations.Count; i++)
            {
                int step = template.InitialRotations[i].Rotation.Value;
                if (step != firstAllowed && step != secondAllowed)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ContainsOddStep(Phase3SafeTemplate template)
        {
            for (int i = 0; i < template.InitialRotations.Count; i++)
            {
                if ((template.InitialRotations[i].Rotation.Value & 1) != 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsEveryStepExactlyOnce(Phase3SafeTemplate template)
        {
            var seen = new bool[Phase3CoreConstants.FullRotationStepCount];
            for (int i = 0; i < template.InitialRotations.Count; i++)
            {
                int step = template.InitialRotations[i].Rotation.Value;
                if (seen[step])
                {
                    return false;
                }

                seen[step] = true;
            }

            for (int step = 0; step < seen.Length; step++)
            {
                if (!seen[step])
                {
                    return false;
                }
            }

            return true;
        }

        private static Phase3PuzzleDefinition ReplaceSlotShape(Phase3PuzzleDefinition source, string slotId, Phase3ShapeDefinition replacementShape)
        {
            var slots = new List<Phase3SlotDefinition>();
            for (int i = 0; i < source.Slots.Count; i++)
            {
                Phase3SlotDefinition slot = source.Slots[i];
                slots.Add(slot.SlotId == slotId
                    ? new Phase3SlotDefinition(slot.SlotId, replacementShape, slot.CorrectCentroid, slot.CorrectBaseRotationStep)
                    : slot);
            }

            return new Phase3PuzzleDefinition(source.Pieces, slots);
        }

        private static Phase3PuzzleDefinition ReplacePieceShape(Phase3PuzzleDefinition source, string pieceId, Phase3ShapeDefinition replacementShape)
        {
            var pieces = new List<Phase3PieceDefinition>();
            for (int i = 0; i < source.Pieces.Count; i++)
            {
                Phase3PieceDefinition piece = source.Pieces[i];
                pieces.Add(piece.PieceId == pieceId
                    ? new Phase3PieceDefinition(piece.PieceId, piece.OriginalDeckSlotId, replacementShape, piece.AllowedTargets)
                    : piece);
            }

            return new Phase3PuzzleDefinition(pieces, source.Slots);
        }

        private static bool HasInterchangeablePair(Phase3PuzzleDefinition puzzle)
        {
            for (int first = 0; first < puzzle.Pieces.Count; first++)
            {
                for (int second = first + 1; second < puzzle.Pieces.Count; second++)
                {
                    Phase3PieceDefinition a = puzzle.Pieces[first];
                    Phase3PieceDefinition b = puzzle.Pieces[second];
                    if (ReferenceEquals(a.ShapeDefinition, b.ShapeDefinition) &&
                        a.AllowedTargets.Count >= 2 && b.AllowedTargets.Count >= 2 &&
                        a.AllowedTargets[0].SlotId == b.AllowedTargets[0].SlotId &&
                        a.AllowedTargets[1].SlotId == b.AllowedTargets[1].SlotId)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool HasSharedEdge(IReadOnlyList<Phase3SlotDefinition> slots)
        {
            for (int first = 0; first < slots.Count; first++)
            {
                for (int second = first + 1; second < slots.Count; second++)
                {
                    IReadOnlyList<Phase3GridPoint> a = slots[first].ShapeDefinition.Vertices;
                    IReadOnlyList<Phase3GridPoint> b = slots[second].ShapeDefinition.Vertices;
                    for (int ai = 0; ai < a.Count; ai++)
                    {
                        for (int bi = 0; bi < b.Count; bi++)
                        {
                            if (a[ai] == b[(bi + 1) % b.Count] && a[(ai + 1) % a.Count] == b[bi])
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        private static bool HasSharedVertexWithoutSharedEdge(IReadOnlyList<Phase3SlotDefinition> slots)
        {
            for (int first = 0; first < slots.Count; first++)
            {
                for (int second = first + 1; second < slots.Count; second++)
                {
                    if (ShareVertex(slots[first].ShapeDefinition.Vertices, slots[second].ShapeDefinition.Vertices) &&
                        !ShareEdge(slots[first].ShapeDefinition.Vertices, slots[second].ShapeDefinition.Vertices))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool ShareVertex(IReadOnlyList<Phase3GridPoint> first, IReadOnlyList<Phase3GridPoint> second)
        {
            for (int i = 0; i < first.Count; i++)
            {
                for (int j = 0; j < second.Count; j++)
                {
                    if (first[i] == second[j])
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool ShareEdge(IReadOnlyList<Phase3GridPoint> first, IReadOnlyList<Phase3GridPoint> second)
        {
            for (int i = 0; i < first.Count; i++)
            {
                for (int j = 0; j < second.Count; j++)
                {
                    if ((first[i] == second[j] && first[(i + 1) % first.Count] == second[(j + 1) % second.Count]) ||
                        (first[i] == second[(j + 1) % second.Count] && first[(i + 1) % first.Count] == second[j]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static int CountOccupied(Phase3PuzzleSessionModel session)
        {
            int count = 0;
            for (int i = 0; i < session.Slots.Count; i++)
            {
                if (session.Slots[i].IsOccupied)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountPlaced(Phase3PuzzleSessionModel session)
        {
            int count = 0;
            for (int i = 0; i < session.Pieces.Count; i++)
            {
                if (session.Pieces[i].State == Phase3PieceState.Placed)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountPieceModels(Phase3PuzzleSessionModel session, string pieceId)
        {
            int count = 0;
            for (int i = 0; i < session.Pieces.Count; i++)
            {
                if (session.Pieces[i].PieceId == pieceId)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool NoSlotsOccupied(Phase3PuzzleSessionModel session) => CountOccupied(session) == 0;

        private static string FirstIssue(Phase3PartitionValidationResult result)
        {
            return result.Issues.Count == 0 ? "none" : result.Issues[0].ToString();
        }

        private static IReadOnlyList<string> GetPieceIds(Phase3PuzzleDefinition puzzle)
        {
            var values = new string[puzzle.Pieces.Count];
            for (int i = 0; i < values.Length; i++) values[i] = puzzle.Pieces[i].PieceId;
            return Array.AsReadOnly(values);
        }

        private static IReadOnlyList<string> GetSlotIds(Phase3PuzzleDefinition puzzle)
        {
            var values = new string[puzzle.Slots.Count];
            for (int i = 0; i < values.Length; i++) values[i] = puzzle.Slots[i].SlotId;
            return Array.AsReadOnly(values);
        }

        private static IReadOnlyList<int> GetInitialSteps(Phase3SafeTemplate template)
        {
            var values = new int[template.InitialRotations.Count];
            for (int i = 0; i < values.Length; i++) values[i] = template.InitialRotations[i].Rotation.Value;
            return Array.AsReadOnly(values);
        }

        private static IReadOnlyList<int> GetSessionRotationSteps(Phase3PuzzleSessionModel session)
        {
            var values = new int[session.Pieces.Count];
            for (int i = 0; i < values.Length; i++) values[i] = session.Pieces[i].CurrentRotation.Value;
            return Array.AsReadOnly(values);
        }

        private static T[] Reverse<T>(IReadOnlyList<T> source)
        {
            var values = new T[source.Count];
            for (int i = 0; i < source.Count; i++) values[i] = source[source.Count - 1 - i];
            return values;
        }

        private static Phase3SlotDefinition[] CopySlots(IReadOnlyList<Phase3SlotDefinition> slots)
        {
            var values = new Phase3SlotDefinition[slots.Count];
            for (int i = 0; i < slots.Count; i++) values[i] = slots[i];
            return values;
        }

        private static bool HasMemberContaining(Type type, string text)
        {
            MemberInfo[] members = type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            for (int i = 0; i < members.Length; i++)
            {
                if (members[i].Name.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        private static bool HasMutablePublicSetter(Type type)
        {
            PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < properties.Length; i++)
            {
                MethodInfo setter = properties[i].GetSetMethod();
                if (setter != null && setter.IsPublic) return true;
            }
            return false;
        }

        private static Phase3GridPoint P(int x, int y) => new Phase3GridPoint(x, y);

        private readonly struct InitialRotationExpectation
        {
            public InitialRotationExpectation(string pieceId, int step)
            {
                PieceId = pieceId;
                Step = step;
            }

            public string PieceId { get; }
            public int Step { get; }
        }

        private sealed class ValidationContext
        {
            private readonly List<string> failures = new List<string>();
            public int Passed { get; private set; }
            public int Total { get; private set; }
            public IReadOnlyList<string> Failures => failures;

            public void RunSection(string section, Action action)
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    Total++;
                    failures.Add($"[Phase3][P3-2][FAIL] section={section}, assertion=unexpected exception, expected=no exception, actualException={exception.GetType().FullName}, message={exception.Message}");
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
                failures.Add($"[Phase3][P3-2][FAIL] assertion={name}, expected={Format(expected)}, actual={Format(actual)}");
            }

            public void Equal<T>(T expected, T actual, string name)
            {
                Check(EqualityComparer<T>.Default.Equals(expected, actual), name, expected, actual);
            }

            public void Near(double expected, double actual, string name)
            {
                Check(Phase3Point2D.IsFiniteValue(actual) && Math.Abs(expected - actual) <= Phase3CoreConstants.ComparisonEpsilon, name, expected, actual);
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
                    failures.Add($"[Phase3][P3-2][FAIL] assertion={name}, expectedException={typeof(TException).FullName}, actual=no exception");
                }
                catch (TException)
                {
                    Passed++;
                }
                catch (Exception exception)
                {
                    failures.Add($"[Phase3][P3-2][FAIL] assertion={name}, expectedException={typeof(TException).FullName}, actualException={exception.GetType().FullName}, message={exception.Message}");
                }
            }

            private static string Join<T>(IReadOnlyList<T> values)
            {
                if (values == null) return "<null>";
                var parts = new string[values.Count];
                for (int i = 0; i < values.Count; i++) parts[i] = Format(values[i]);
                return "[" + string.Join(", ", parts) + "]";
            }

            private static string Format(object value) => value == null ? "<null>" : value.ToString();
        }
    }
}
#endif
