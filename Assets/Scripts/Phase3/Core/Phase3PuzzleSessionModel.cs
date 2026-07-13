using System;
using System.Collections.Generic;
using HATAGONG.GameFlow;

namespace HATAGONG.Phase3
{
    public sealed class Phase3PuzzleSessionModel
    {
        private readonly Dictionary<string, Phase3PieceModel> piecesById;
        private readonly Dictionary<string, Phase3SlotModel> slotsById;
        private Phase3ActiveDrag? activeDrag;
        private bool clearScoreAwarded;

        public Phase3PuzzleSessionModel(Phase3PuzzleDefinition puzzle, GameDifficulty difficulty)
            : this(puzzle, difficulty, null)
        {
        }

        public Phase3PuzzleSessionModel(
            Phase3PuzzleDefinition puzzle,
            GameDifficulty difficulty,
            IEnumerable<Phase3InitialPieceRotation> initialRotations)
        {
            Puzzle = puzzle ?? throw new ArgumentNullException(nameof(puzzle));
            Difficulty = difficulty;
            DifficultyRules = Phase3DifficultyRules.For(difficulty);
            PartitionValidation = Phase3PartitionValidator.Validate(puzzle, difficulty);
            if (!PartitionValidation.IsValid)
            {
                throw new ArgumentException($"Puzzle does not satisfy the {difficulty} partition contract: {PartitionValidation.Issues[0]}", nameof(puzzle));
            }

            var initialRotationByPiece = BuildInitialRotations(puzzle, initialRotations);
            piecesById = new Dictionary<string, Phase3PieceModel>(StringComparer.Ordinal);
            slotsById = new Dictionary<string, Phase3SlotModel>(StringComparer.Ordinal);
            var pieces = new Phase3PieceModel[puzzle.Pieces.Count];
            var slots = new Phase3SlotModel[puzzle.Slots.Count];
            for (int i = 0; i < puzzle.Pieces.Count; i++)
            {
                Phase3PieceDefinition definition = puzzle.Pieces[i];
                var piece = new Phase3PieceModel(definition, initialRotationByPiece[definition.PieceId]);
                pieces[i] = piece;
                piecesById.Add(piece.PieceId, piece);
            }

            for (int i = 0; i < puzzle.Slots.Count; i++)
            {
                var slot = new Phase3SlotModel(puzzle.Slots[i]);
                slots[i] = slot;
                slotsById.Add(slot.SlotId, slot);
            }

            Pieces = Array.AsReadOnly(pieces);
            Slots = Array.AsReadOnly(slots);
        }

        public Phase3PuzzleDefinition Puzzle { get; }
        public GameDifficulty Difficulty { get; }
        public Phase3DifficultyRuleSet DifficultyRules { get; }
        public Phase3PartitionValidationResult PartitionValidation { get; }
        public IReadOnlyList<Phase3PieceModel> Pieces { get; }
        public IReadOnlyList<Phase3SlotModel> Slots { get; }
        public bool HasActiveDrag => activeDrag.HasValue;
        public Phase3ActiveDrag ActiveDrag => activeDrag.GetValueOrDefault();
        public string ActivePieceId => activeDrag.HasValue ? activeDrag.Value.PieceId : string.Empty;
        public bool IsCleared { get; private set; }
        public int PhaseScore { get; private set; }

        public Phase3PieceModel GetPiece(string pieceId)
        {
            return pieceId != null && piecesById.TryGetValue(pieceId, out Phase3PieceModel piece) ? piece : null;
        }

        public Phase3SlotModel GetSlot(string slotId)
        {
            return slotId != null && slotsById.TryGetValue(slotId, out Phase3SlotModel slot) ? slot : null;
        }

        public Phase3PlayResult BeginDrag(string pieceId)
        {
            if (IsCleared)
            {
                return Failure(Phase3PlayOperation.BeginDrag, Phase3PlayFailure.PhaseAlreadyCleared, pieceId);
            }

            Phase3PieceModel piece = GetPiece(pieceId);
            if (piece == null)
            {
                return Failure(Phase3PlayOperation.BeginDrag, Phase3PlayFailure.PieceNotFound, pieceId);
            }

            if (activeDrag.HasValue)
            {
                return Failure(
                    Phase3PlayOperation.BeginDrag,
                    string.Equals(activeDrag.Value.PieceId, pieceId, StringComparison.Ordinal)
                        ? Phase3PlayFailure.PieceAlreadyDragging
                        : Phase3PlayFailure.AnotherPieceAlreadyDragging,
                    pieceId);
            }

            if (piece.State == Phase3PieceState.Placed)
            {
                return Failure(Phase3PlayOperation.BeginDrag, Phase3PlayFailure.PiecePlacedImmutable, pieceId);
            }

            if (piece.State == Phase3PieceState.Dragging)
            {
                return Failure(Phase3PlayOperation.BeginDrag, Phase3PlayFailure.InternalStateConflict, pieceId);
            }

            if (piece.State != Phase3PieceState.InDeck && piece.State != Phase3PieceState.Loose)
            {
                return Failure(Phase3PlayOperation.BeginDrag, Phase3PlayFailure.InternalStateConflict, pieceId);
            }

            activeDrag = new Phase3ActiveDrag(piece);
            piece.BeginDrag();
            return Phase3PlayResult.Success(Phase3PlayOperation.BeginDrag, pieceId);
        }

        public Phase3PlayResult RotateActiveClockwise(string pieceId) => RotateActive(pieceId, 1);
        public Phase3PlayResult RotateActiveCounterClockwise(string pieceId) => RotateActive(pieceId, -1);

        public Phase3PlayResult RotateActive(string pieceId, int signedStepDelta)
        {
            Phase3PlayResult validation = ValidateActiveOperation(Phase3PlayOperation.Rotate, pieceId);
            if (!validation.IsSuccess)
            {
                return validation;
            }

            Phase3PieceModel piece = piecesById[pieceId];
            piece.ApplyRotation(signedStepDelta);
            return Phase3PlayResult.Success(Phase3PlayOperation.Rotate, pieceId);
        }

        public Phase3PlayResult CancelActiveDrag()
        {
            if (IsCleared)
            {
                return Failure(Phase3PlayOperation.CancelDrag, Phase3PlayFailure.PhaseAlreadyCleared, string.Empty);
            }

            if (!activeDrag.HasValue)
            {
                return Failure(Phase3PlayOperation.CancelDrag, Phase3PlayFailure.NoActiveDrag, string.Empty);
            }

            Phase3ActiveDrag drag = activeDrag.Value;
            Phase3PieceModel piece = piecesById[drag.PieceId];
            if (piece.State != Phase3PieceState.Dragging)
            {
                return Failure(Phase3PlayOperation.CancelDrag, Phase3PlayFailure.PieceNotDragging, drag.PieceId);
            }

            if (drag.OriginState == Phase3PieceState.InDeck)
            {
                piece.RestoreInDeck();
            }
            else if (drag.OriginState == Phase3PieceState.Loose && drag.HadStableLooseCentroid)
            {
                piece.StabilizeLoose(drag.StableLooseCentroidAtBegin);
            }
            else
            {
                return Failure(Phase3PlayOperation.CancelDrag, Phase3PlayFailure.InternalStateConflict, drag.PieceId);
            }

            activeDrag = null;
            return Phase3PlayResult.Success(Phase3PlayOperation.CancelDrag, piece.PieceId);
        }

        public Phase3PlayResult ReturnActiveToDeck(string pieceId)
        {
            Phase3PlayResult validation = ValidateActiveOperation(Phase3PlayOperation.DeckReturn, pieceId);
            if (!validation.IsSuccess)
            {
                return validation;
            }

            Phase3PieceModel piece = piecesById[pieceId];
            piece.RestoreInDeck();
            activeDrag = null;
            return Phase3PlayResult.Success(Phase3PlayOperation.DeckReturn, pieceId);
        }

        public Phase3PlayResult DropActiveOnField(string pieceId, Phase3Point2D displayedCentroid)
        {
            Phase3PlayResult validation = ValidateActiveOperation(Phase3PlayOperation.FieldDrop, pieceId);
            if (!validation.IsSuccess)
            {
                return validation;
            }

            if (!displayedCentroid.IsFinite)
            {
                return Failure(Phase3PlayOperation.FieldDrop, Phase3PlayFailure.InvalidCentroid, pieceId);
            }

            Phase3PieceModel piece = piecesById[pieceId];
            var occupiedSlotIds = new List<string>();
            for (int i = 0; i < Slots.Count; i++)
            {
                if (Slots[i].IsOccupied)
                {
                    occupiedSlotIds.Add(Slots[i].SlotId);
                }
            }

            var slotDefinitions = new Phase3SlotDefinition[Slots.Count];
            for (int i = 0; i < Slots.Count; i++)
            {
                slotDefinitions[i] = Slots[i].Definition;
            }

            Phase3SnapResult snap = Phase3SnapRules.Evaluate(
                piece.Definition,
                displayedCentroid,
                piece.CurrentRotation,
                DifficultyRules.SnapDistance,
                slotDefinitions,
                occupiedSlotIds);
            if (!snap.IsSuccess)
            {
                piece.StabilizeLoose(displayedCentroid);
                activeDrag = null;
                return Phase3PlayResult.FailureResult(
                    Phase3PlayOperation.FieldDrop,
                    MapSnapFailure(snap.Code),
                    pieceId,
                    true,
                    snap.Code);
            }

            if (!slotsById.TryGetValue(snap.TargetSlotId, out Phase3SlotModel targetSlot))
            {
                return Failure(Phase3PlayOperation.FieldDrop, Phase3PlayFailure.SlotNotFound, pieceId);
            }

            if (targetSlot.IsOccupied || piece.State != Phase3PieceState.Dragging || piece.HasPlacedSlot)
            {
                return Failure(Phase3PlayOperation.FieldDrop, Phase3PlayFailure.InternalStateConflict, pieceId);
            }

            Phase3RotationStep finalRotation = snap.RequiredRotation.Add(snap.RotationCorrection.Value);
            targetSlot.Occupy(pieceId);
            piece.Place(targetSlot.SlotId, targetSlot.Definition.CorrectCentroid, finalRotation);
            activeDrag = null;

            int manualSnapScore = piece.TryMarkManualSnapScoreAwarded() ? Phase3ScoreRules.ManualSnapScore : 0;
            int clearScore = 0;
            bool clearedThisOperation = false;
            if (!IsCleared && IsClearStateConsistent())
            {
                IsCleared = true;
                clearedThisOperation = true;
                if (!clearScoreAwarded)
                {
                    clearScoreAwarded = true;
                    clearScore = Phase3ScoreRules.PhaseClearScore;
                }
            }

            PhaseScore = checked(PhaseScore + manualSnapScore + clearScore);
            return Phase3PlayResult.Placement(pieceId, targetSlot.SlotId, manualSnapScore, clearScore, clearedThisOperation);
        }

        public bool IsClearStateConsistent()
        {
            if (Pieces.Count != Slots.Count || Pieces.Count != DifficultyRules.TargetPieceCount)
            {
                return false;
            }

            var occupiedSlotIds = new HashSet<string>(StringComparer.Ordinal);
            var placedPieceIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < Pieces.Count; i++)
            {
                Phase3PieceModel piece = Pieces[i];
                if (piece.State != Phase3PieceState.Placed || !piece.HasPlacedSlot || !piece.HasFieldCentroid)
                {
                    return false;
                }

                if (!placedPieceIds.Add(piece.PieceId) || !occupiedSlotIds.Add(piece.PlacedSlotId))
                {
                    return false;
                }

                if (!slotsById.TryGetValue(piece.PlacedSlotId, out Phase3SlotModel slot) ||
                    !slot.IsOccupied || !string.Equals(slot.OccupyingPieceId, piece.PieceId, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            for (int i = 0; i < Slots.Count; i++)
            {
                Phase3SlotModel slot = Slots[i];
                if (!slot.IsOccupied || !placedPieceIds.Contains(slot.OccupyingPieceId))
                {
                    return false;
                }
            }

            return occupiedSlotIds.Count == Slots.Count && placedPieceIds.Count == Pieces.Count;
        }

        private Phase3PlayResult ValidateActiveOperation(Phase3PlayOperation operation, string pieceId)
        {
            if (IsCleared)
            {
                return Failure(operation, Phase3PlayFailure.PhaseAlreadyCleared, pieceId);
            }

            Phase3PieceModel piece = GetPiece(pieceId);
            if (piece == null)
            {
                return Failure(operation, Phase3PlayFailure.PieceNotFound, pieceId);
            }

            if (!activeDrag.HasValue)
            {
                return Failure(operation, piece.State == Phase3PieceState.Placed ? Phase3PlayFailure.PiecePlacedImmutable : Phase3PlayFailure.NoActiveDrag, pieceId);
            }

            if (!string.Equals(activeDrag.Value.PieceId, pieceId, StringComparison.Ordinal))
            {
                return Failure(operation, Phase3PlayFailure.WrongActivePiece, pieceId);
            }

            if (piece.State != Phase3PieceState.Dragging)
            {
                return Failure(operation, Phase3PlayFailure.PieceNotDragging, pieceId);
            }

            return Phase3PlayResult.Success(operation, pieceId);
        }

        private static Dictionary<string, Phase3RotationStep> BuildInitialRotations(
            Phase3PuzzleDefinition puzzle,
            IEnumerable<Phase3InitialPieceRotation> initialRotations)
        {
            var rotations = new Dictionary<string, Phase3RotationStep>(StringComparer.Ordinal);
            for (int i = 0; i < puzzle.Pieces.Count; i++)
            {
                rotations.Add(puzzle.Pieces[i].PieceId, default);
            }

            if (initialRotations == null)
            {
                return rotations;
            }

            var suppliedIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (Phase3InitialPieceRotation initialRotation in initialRotations)
            {
                if (!rotations.ContainsKey(initialRotation.PieceId))
                {
                    throw new ArgumentException($"Initial rotation references missing piece '{initialRotation.PieceId}'.", nameof(initialRotations));
                }

                if (!suppliedIds.Add(initialRotation.PieceId))
                {
                    throw new ArgumentException($"Duplicate initial rotation for piece '{initialRotation.PieceId}'.", nameof(initialRotations));
                }

                rotations[initialRotation.PieceId] = initialRotation.Rotation;
            }

            if (suppliedIds.Count != puzzle.Pieces.Count)
            {
                throw new ArgumentException("When initial rotations are supplied, every piece requires exactly one value.", nameof(initialRotations));
            }

            return rotations;
        }

        private static Phase3PlayFailure MapSnapFailure(Phase3SnapResultCode snapFailure)
        {
            switch (snapFailure)
            {
                case Phase3SnapResultCode.NoAllowedTarget:
                    return Phase3PlayFailure.SnapNoAllowedTarget;
                case Phase3SnapResultCode.AllAllowedTargetsOccupied:
                    return Phase3PlayFailure.SnapAllTargetsOccupied;
                case Phase3SnapResultCode.OutOfSnapDistance:
                    return Phase3PlayFailure.SnapOutOfDistance;
                case Phase3SnapResultCode.RotationMismatch:
                    return Phase3PlayFailure.SnapRotationMismatch;
                default:
                    return Phase3PlayFailure.InternalStateConflict;
            }
        }

        private static Phase3PlayResult Failure(Phase3PlayOperation operation, Phase3PlayFailure failure, string pieceId)
        {
            return Phase3PlayResult.FailureResult(operation, failure, pieceId);
        }
    }
}
