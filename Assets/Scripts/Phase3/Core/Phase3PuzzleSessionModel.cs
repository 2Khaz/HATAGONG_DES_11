using System;
using System.Collections.Generic;
using HATAGONG.GameFlow;

namespace HATAGONG.Phase3
{
    public sealed class Phase3SessionPieceGeometry
    {
        public Phase3SessionPieceGeometry(
            string pieceId,
            string slotId,
            IEnumerable<Phase3Point2D> targetAbsoluteVertices,
            Phase3Point2D targetCentroid,
            IEnumerable<Phase3Point2D> baseLocalVertices,
            Phase3RotationStep requiredSnapRotation,
            Phase3RotationStep targetRotation,
            Phase3RotationStep initialRotation)
        {
            if (string.IsNullOrWhiteSpace(pieceId)) throw new ArgumentException("Piece ID is required.", nameof(pieceId));
            if (string.IsNullOrWhiteSpace(slotId)) throw new ArgumentException("Slot ID is required.", nameof(slotId));
            if (targetAbsoluteVertices == null) throw new ArgumentNullException(nameof(targetAbsoluteVertices));
            if (baseLocalVertices == null) throw new ArgumentNullException(nameof(baseLocalVertices));
            if (!targetCentroid.IsFinite) throw new ArgumentException("Target centroid must be finite.", nameof(targetCentroid));
            var target = new List<Phase3Point2D>(targetAbsoluteVertices).ToArray();
            var local = new List<Phase3Point2D>(baseLocalVertices).ToArray();
            var imageUvs = new Phase3Point2D[target.Length];
            if (target.Length < 3 || local.Length != target.Length)
                throw new ArgumentException("Target and base polygons must have the same vertex count of at least three.");
            for (int i = 0; i < target.Length; i++)
            {
                if (!target[i].IsFinite || !local[i].IsFinite)
                    throw new ArgumentException("Geometry vertices must be finite.");
                imageUvs[i] = new Phase3Point2D(
                    target[i].X / Phase3CoreConstants.LogicalGridSize,
                    target[i].Y / Phase3CoreConstants.LogicalGridSize);
            }

            PieceId = pieceId;
            SlotId = slotId;
            TargetAbsoluteVertices = Array.AsReadOnly(target);
            TargetCentroid = targetCentroid;
            BaseLocalVertices = Array.AsReadOnly(local);
            ImageUvs = Array.AsReadOnly(imageUvs);
            RequiredSnapRotation = requiredSnapRotation;
            TargetRotation = targetRotation;
            InitialRotation = initialRotation;
        }

        public string PieceId { get; }
        public string SlotId { get; }
        public IReadOnlyList<Phase3Point2D> TargetAbsoluteVertices { get; }
        public Phase3Point2D TargetCentroid { get; }
        public IReadOnlyList<Phase3Point2D> BaseLocalVertices { get; }
        public IReadOnlyList<Phase3Point2D> ImageUvs { get; }
        public Phase3RotationStep RequiredSnapRotation { get; }
        public Phase3RotationStep TargetRotation { get; }
        public Phase3RotationStep InitialRotation { get; }

        public Phase3Point2D[] BuildAbsoluteVertices(
            Phase3RotationStep rotation,
            Phase3Point2D logicalCentroid)
        {
            if (!logicalCentroid.IsFinite) throw new ArgumentException("Logical centroid must be finite.", nameof(logicalCentroid));
            var vertices = new Phase3Point2D[BaseLocalVertices.Count];
            double radians = -rotation.Degrees * Math.PI / 180d;
            double cosine = Math.Cos(radians);
            double sine = Math.Sin(radians);
            for (int i = 0; i < BaseLocalVertices.Count; i++)
            {
                Phase3Point2D local = BaseLocalVertices[i];
                var rotated = new Phase3Point2D(
                    local.X * cosine - local.Y * sine,
                    local.X * sine + local.Y * cosine);
                vertices[i] = logicalCentroid + rotated;
            }
            return vertices;
        }
    }

    public sealed class Phase3PuzzleSessionModel
    {
        private readonly Dictionary<string, Phase3PieceModel> piecesById;
        private readonly Dictionary<string, Phase3SlotModel> slotsById;
        private readonly Dictionary<string, Phase3SessionPieceGeometry> geometryByPieceId;
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
            : this(puzzle, difficulty, initialRotations, Phase3DifficultyRules.For(difficulty), null)
        {
        }

        public Phase3PuzzleSessionModel(Phase3PuzzleGenerationResult generationResult)
            : this(
                RequireSuccessfulGeneration(generationResult).Puzzle,
                generationResult.Difficulty,
                generationResult.InitialRotations,
                Phase3PuzzleGeneratorDifficultyConfig.For(generationResult.Difficulty).PartitionRules,
                generationResult)
        {
        }

        private Phase3PuzzleSessionModel(
            Phase3PuzzleDefinition puzzle,
            GameDifficulty difficulty,
            IEnumerable<Phase3InitialPieceRotation> initialRotations,
            Phase3DifficultyRuleSet difficultyRules,
            Phase3PuzzleGenerationResult generationResult)
        {
            Puzzle = puzzle ?? throw new ArgumentNullException(nameof(puzzle));
            Difficulty = difficulty;
            DifficultyRules = difficultyRules;
            GenerationResult = generationResult;
            PartitionValidation = generationResult == null
                ? Phase3PartitionValidator.Validate(puzzle, difficulty)
                : Phase3PartitionValidator.Validate(puzzle, difficulty, difficultyRules);
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
            PieceGeometries = BuildPieceGeometries(puzzle, generationResult, initialRotationByPiece);
            geometryByPieceId = new Dictionary<string, Phase3SessionPieceGeometry>(StringComparer.Ordinal);
            for (int i = 0; i < PieceGeometries.Count; i++)
            {
                geometryByPieceId.Add(PieceGeometries[i].PieceId, PieceGeometries[i]);
            }
        }

        public Phase3PuzzleDefinition Puzzle { get; }
        public GameDifficulty Difficulty { get; }
        public Phase3DifficultyRuleSet DifficultyRules { get; }
        public Phase3PartitionValidationResult PartitionValidation { get; }
        public Phase3PuzzleGenerationResult GenerationResult { get; }
        public bool HasGenerationMetadata => GenerationResult != null;
        public IReadOnlyList<Phase3PieceModel> Pieces { get; }
        public IReadOnlyList<Phase3SlotModel> Slots { get; }
        public IReadOnlyList<Phase3SessionPieceGeometry> PieceGeometries { get; }
        public bool HasActiveDrag => activeDrag.HasValue;
        public Phase3ActiveDrag ActiveDrag => activeDrag.GetValueOrDefault();
        public string ActivePieceId => activeDrag.HasValue ? activeDrag.Value.PieceId : string.Empty;
        public bool IsCleared { get; private set; }
        public int PhaseScore { get; private set; }

        private static Phase3PuzzleGenerationResult RequireSuccessfulGeneration(Phase3PuzzleGenerationResult generationResult)
        {
            if (generationResult == null) throw new ArgumentNullException(nameof(generationResult));
            if (!generationResult.Succeeded || generationResult.Puzzle == null)
                throw new ArgumentException("A successful generation result with a puzzle is required.", nameof(generationResult));
            return generationResult;
        }

        public Phase3PieceModel GetPiece(string pieceId)
        {
            return pieceId != null && piecesById.TryGetValue(pieceId, out Phase3PieceModel piece) ? piece : null;
        }

        public Phase3SlotModel GetSlot(string slotId)
        {
            return slotId != null && slotsById.TryGetValue(slotId, out Phase3SlotModel slot) ? slot : null;
        }

        public Phase3SessionPieceGeometry GetPieceGeometry(string pieceId)
        {
            return pieceId != null && geometryByPieceId.TryGetValue(pieceId, out Phase3SessionPieceGeometry geometry)
                ? geometry
                : null;
        }

        private static IReadOnlyList<Phase3SessionPieceGeometry> BuildPieceGeometries(
            Phase3PuzzleDefinition puzzle,
            Phase3PuzzleGenerationResult generationResult,
            IReadOnlyDictionary<string, Phase3RotationStep> initialRotations)
        {
            var geometries = new Phase3SessionPieceGeometry[puzzle.Pieces.Count];
            if (generationResult != null)
            {
                if (generationResult.GeneratedPieces.Count != puzzle.Pieces.Count)
                    throw new ArgumentException("Generated piece geometry count must match the puzzle piece count.", nameof(generationResult));
                var generatedByPieceId = new Dictionary<string, Phase3GeneratedPieceData>(StringComparer.Ordinal);
                for (int i = 0; i < generationResult.GeneratedPieces.Count; i++)
                    generatedByPieceId.Add(generationResult.GeneratedPieces[i].PieceId, generationResult.GeneratedPieces[i]);
                for (int i = 0; i < puzzle.Pieces.Count; i++)
                {
                    Phase3PieceDefinition piece = puzzle.Pieces[i];
                    if (!generatedByPieceId.TryGetValue(piece.PieceId, out Phase3GeneratedPieceData generated))
                        throw new ArgumentException($"Generated geometry is missing piece '{piece.PieceId}'.", nameof(generationResult));
                    var absolute = new Phase3Point2D[generated.Vertices.Count];
                    for (int vertexIndex = 0; vertexIndex < generated.Vertices.Count; vertexIndex++)
                        absolute[vertexIndex] = new Phase3Point2D(generated.Vertices[vertexIndex].X, generated.Vertices[vertexIndex].Y);
                    Phase3Point2D centroid = CalculatePolygonCentroid(absolute);
                    Phase3AllowedTarget allowedTarget = FindAllowedTarget(piece, generated.SlotId);
                    if (allowedTarget.RequiredRotationStep != generated.TargetRotation)
                        throw new ArgumentException($"Generated snap rotation does not match piece '{piece.PieceId}'.", nameof(generationResult));
                    Phase3RotationStep finalRotation = allowedTarget.RequiredRotationStep.Add(
                        allowedTarget.RotationCorrectionStep.Value);
                    geometries[i] = CreateGeometry(
                        piece.PieceId,
                        generated.SlotId,
                        absolute,
                        centroid,
                        allowedTarget.RequiredRotationStep,
                        finalRotation,
                        generated.InitialRotation);
                }
                return Array.AsReadOnly(geometries);
            }

            var piecesBySlotIndex = new Phase3PieceDefinition[puzzle.Slots.Count];
            if (!TryAssignPiecesToSlots(puzzle.Pieces, puzzle.Slots, 0, new HashSet<string>(StringComparer.Ordinal), piecesBySlotIndex))
                throw new ArgumentException("Puzzle slots do not have a one-to-one presentation geometry assignment.", nameof(puzzle));
            for (int slotIndex = 0; slotIndex < puzzle.Slots.Count; slotIndex++)
            {
                Phase3SlotDefinition slot = puzzle.Slots[slotIndex];
                Phase3PieceDefinition piece = piecesBySlotIndex[slotIndex];
                Phase3AllowedTarget target = FindAllowedTarget(piece, slot.SlotId);
                var absolute = new Phase3Point2D[slot.ShapeDefinition.Vertices.Count];
                Phase3Point2D logicalCentroid = slot.CorrectCentroid /
                    ((double)Phase3CoreConstants.CanvasFieldSize / Phase3CoreConstants.LogicalGridSize);
                for (int vertexIndex = 0; vertexIndex < absolute.Length; vertexIndex++)
                {
                    Phase3GridPoint vertex = slot.ShapeDefinition.Vertices[vertexIndex];
                    Phase3Point2D baseLocal = new Phase3Point2D(
                        vertex.X - slot.ShapeDefinition.Centroid.X,
                        vertex.Y - slot.ShapeDefinition.Centroid.Y);
                    absolute[vertexIndex] = RotateClockwise(baseLocal, slot.CorrectBaseRotationStep) + logicalCentroid;
                }
                geometries[slotIndex] = CreateGeometry(
                    piece.PieceId,
                    slot.SlotId,
                    absolute,
                    logicalCentroid,
                    target.RequiredRotationStep,
                    target.RequiredRotationStep.Add(target.RotationCorrectionStep.Value),
                    initialRotations[piece.PieceId]);
            }
            return Array.AsReadOnly(geometries);
        }

        private static Phase3SessionPieceGeometry CreateGeometry(
            string pieceId,
            string slotId,
            IReadOnlyList<Phase3Point2D> targetAbsoluteVertices,
            Phase3Point2D targetCentroid,
            Phase3RotationStep requiredSnapRotation,
            Phase3RotationStep targetRotation,
            Phase3RotationStep initialRotation)
        {
            var baseLocal = new Phase3Point2D[targetAbsoluteVertices.Count];
            Phase3RotationStep inverseTargetRotation = new Phase3RotationStep(-targetRotation.Value);
            for (int i = 0; i < targetAbsoluteVertices.Count; i++)
                baseLocal[i] = RotateClockwise(targetAbsoluteVertices[i] - targetCentroid, inverseTargetRotation);
            return new Phase3SessionPieceGeometry(
                pieceId,
                slotId,
                targetAbsoluteVertices,
                targetCentroid,
                baseLocal,
                requiredSnapRotation,
                targetRotation,
                initialRotation);
        }

        private static Phase3Point2D CalculatePolygonCentroid(IReadOnlyList<Phase3Point2D> vertices)
        {
            if (vertices == null || vertices.Count < 3) throw new ArgumentException("Polygon requires at least three vertices.", nameof(vertices));
            double signedDoubleArea = 0d;
            double weightedX = 0d;
            double weightedY = 0d;
            for (int i = 0; i < vertices.Count; i++)
            {
                Phase3Point2D current = vertices[i];
                Phase3Point2D next = vertices[(i + 1) % vertices.Count];
                double cross = current.X * next.Y - next.X * current.Y;
                signedDoubleArea += cross;
                weightedX += (current.X + next.X) * cross;
                weightedY += (current.Y + next.Y) * cross;
            }
            if (Math.Abs(signedDoubleArea) <= Phase3CoreConstants.ComparisonEpsilon)
                throw new ArgumentException("Polygon centroid requires non-zero area.", nameof(vertices));
            double divisor = 3d * signedDoubleArea;
            return new Phase3Point2D(weightedX / divisor, weightedY / divisor);
        }

        private static bool TryAssignPiecesToSlots(
            IReadOnlyList<Phase3PieceDefinition> pieces,
            IReadOnlyList<Phase3SlotDefinition> slots,
            int slotIndex,
            ISet<string> assignedPieceIds,
            Phase3PieceDefinition[] assignments)
        {
            if (slotIndex >= slots.Count) return true;
            string slotId = slots[slotIndex].SlotId;
            for (int i = 0; i < pieces.Count; i++)
            {
                if (assignedPieceIds.Contains(pieces[i].PieceId)) continue;
                for (int targetIndex = 0; targetIndex < pieces[i].AllowedTargets.Count; targetIndex++)
                    if (string.Equals(pieces[i].AllowedTargets[targetIndex].SlotId, slotId, StringComparison.Ordinal))
                    {
                        assignedPieceIds.Add(pieces[i].PieceId);
                        assignments[slotIndex] = pieces[i];
                        if (TryAssignPiecesToSlots(pieces, slots, slotIndex + 1, assignedPieceIds, assignments)) return true;
                        assignments[slotIndex] = null;
                        assignedPieceIds.Remove(pieces[i].PieceId);
                        break;
                    }
            }
            return false;
        }

        private static Phase3AllowedTarget FindAllowedTarget(Phase3PieceDefinition piece, string slotId)
        {
            for (int i = 0; i < piece.AllowedTargets.Count; i++)
                if (string.Equals(piece.AllowedTargets[i].SlotId, slotId, StringComparison.Ordinal))
                    return piece.AllowedTargets[i];
            throw new ArgumentException($"Piece '{piece.PieceId}' is not allowed in slot '{slotId}'.", nameof(slotId));
        }

        private static Phase3Point2D RotateClockwise(Phase3Point2D point, Phase3RotationStep rotation)
        {
            double radians = -rotation.Degrees * Math.PI / 180d;
            double cosine = Math.Cos(radians);
            double sine = Math.Sin(radians);
            return new Phase3Point2D(point.X * cosine - point.Y * sine, point.X * sine + point.Y * cosine);
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

        public Phase3PlayResult DropActiveOnField(
            string pieceId,
            Phase3Point2D displayedCentroid,
            double? maximumSnapDistance = null)
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

            if (maximumSnapDistance.HasValue &&
                slotsById.TryGetValue(snap.TargetSlotId, out Phase3SlotModel distanceSlot))
            {
                double dx = displayedCentroid.X - distanceSlot.Definition.CorrectCentroid.X;
                double dy = displayedCentroid.Y - distanceSlot.Definition.CorrectCentroid.Y;
                if (dx * dx + dy * dy > maximumSnapDistance.Value * maximumSnapDistance.Value)
                {
                    piece.StabilizeLoose(displayedCentroid);
                    activeDrag = null;
                    return Phase3PlayResult.FailureResult(
                        Phase3PlayOperation.FieldDrop,
                        Phase3PlayFailure.SnapOutOfDistance,
                        pieceId,
                        true,
                        Phase3SnapResultCode.OutOfSnapDistance);
                }
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
            if (HasGenerationMetadata && !TryValidatePlacementGeometry(
                    pieceId,
                    targetSlot.SlotId,
                    finalRotation,
                    targetSlot.Definition.CorrectCentroid,
                    out _,
                    out _,
                    out _))
            {
                piece.StabilizeLoose(displayedCentroid);
                activeDrag = null;
                return Phase3PlayResult.FailureResult(
                    Phase3PlayOperation.FieldDrop,
                    Phase3PlayFailure.SnapGeometryMismatch,
                    pieceId,
                    true,
                    Phase3SnapResultCode.GeometryMismatch);
            }
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

        public bool TryValidatePlacementGeometry(
            string pieceId,
            string slotId,
            Phase3RotationStep finalRotation,
            Phase3Point2D canonicalTargetCentroid,
            out double maximumVertexError,
            out int outsideVertexCount,
            out string reason)
        {
            maximumVertexError = 0d;
            outsideVertexCount = 0;
            reason = string.Empty;
            Phase3SessionPieceGeometry geometry = GetPieceGeometry(pieceId);
            if (geometry == null || !string.Equals(geometry.PieceId, pieceId, StringComparison.Ordinal))
            {
                reason = "PieceGeometryMissing";
                return false;
            }
            if (!string.Equals(geometry.SlotId, slotId, StringComparison.Ordinal))
            {
                reason = "PieceToTargetIdMismatch";
                return false;
            }
            if (geometry.TargetRotation != finalRotation)
            {
                reason = "FinalRotationMismatch";
                return false;
            }
            Phase3Point2D logicalCentroid = canonicalTargetCentroid /
                ((double)Phase3CoreConstants.CanvasFieldSize / Phase3CoreConstants.LogicalGridSize);
            if (logicalCentroid.DistanceSquaredTo(geometry.TargetCentroid) >
                Phase3CoreConstants.ComparisonEpsilon * Phase3CoreConstants.ComparisonEpsilon)
            {
                reason = "TargetCentroidMismatch";
                return false;
            }
            Phase3Point2D[] reconstructed = geometry.BuildAbsoluteVertices(finalRotation, logicalCentroid);
            if (reconstructed.Length != geometry.TargetAbsoluteVertices.Count)
            {
                reason = "VertexCountMismatch";
                return false;
            }
            for (int i = 0; i < reconstructed.Length; i++)
            {
                Phase3Point2D target = geometry.TargetAbsoluteVertices[i];
                double errorSquared = reconstructed[i].DistanceSquaredTo(target);
                maximumVertexError = Math.Max(maximumVertexError, Math.Sqrt(errorSquared));
                if (target.X < -Phase3CoreConstants.ComparisonEpsilon ||
                    target.X > Phase3CoreConstants.LogicalGridSize + Phase3CoreConstants.ComparisonEpsilon ||
                    target.Y < -Phase3CoreConstants.ComparisonEpsilon ||
                    target.Y > Phase3CoreConstants.LogicalGridSize + Phase3CoreConstants.ComparisonEpsilon)
                    outsideVertexCount++;
            }
            if (outsideVertexCount != 0)
            {
                reason = "TargetOutsideField";
                return false;
            }
            if (maximumVertexError > Phase3CoreConstants.ComparisonEpsilon)
            {
                reason = "TargetVertexMismatch";
                return false;
            }
            return true;
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
                case Phase3SnapResultCode.GeometryMismatch:
                    return Phase3PlayFailure.SnapGeometryMismatch;
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
