using System;

namespace HATAGONG.Phase3
{
    public sealed class Phase3PieceModel
    {
        internal Phase3PieceModel(Phase3PieceDefinition definition, Phase3RotationStep initialRotation)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            State = Phase3PieceState.InDeck;
            CurrentRotation = initialRotation;
            PlacedSlotId = string.Empty;
        }

        public Phase3PieceDefinition Definition { get; }
        public string PieceId => Definition.PieceId;
        public string OriginalDeckSlotId => Definition.OriginalDeckSlotId;
        public Phase3PieceState State { get; private set; }
        public Phase3RotationStep CurrentRotation { get; private set; }
        public bool HasFieldCentroid { get; private set; }
        public Phase3Point2D FieldCentroid { get; private set; }
        public string PlacedSlotId { get; private set; }
        public bool HasPlacedSlot => !string.IsNullOrEmpty(PlacedSlotId);
        public bool ManualSnapScoreAwarded { get; private set; }
        public bool HasLastStableLooseCentroid { get; private set; }
        public Phase3Point2D LastStableLooseCentroid { get; private set; }

        internal void BeginDrag()
        {
            State = Phase3PieceState.Dragging;
            PlacedSlotId = string.Empty;
        }

        internal void ApplyRotation(int signedStepDelta)
        {
            CurrentRotation = CurrentRotation.Add(signedStepDelta);
        }

        internal void RestoreInDeck()
        {
            State = Phase3PieceState.InDeck;
            HasFieldCentroid = false;
            FieldCentroid = default;
            PlacedSlotId = string.Empty;
        }

        internal void StabilizeLoose(Phase3Point2D centroid)
        {
            State = Phase3PieceState.Loose;
            HasFieldCentroid = true;
            FieldCentroid = centroid;
            HasLastStableLooseCentroid = true;
            LastStableLooseCentroid = centroid;
            PlacedSlotId = string.Empty;
        }

        internal void Place(string slotId, Phase3Point2D centroid, Phase3RotationStep finalRotation)
        {
            State = Phase3PieceState.Placed;
            CurrentRotation = finalRotation;
            HasFieldCentroid = true;
            FieldCentroid = centroid;
            PlacedSlotId = slotId;
        }

        internal bool TryMarkManualSnapScoreAwarded()
        {
            if (ManualSnapScoreAwarded)
            {
                return false;
            }

            ManualSnapScoreAwarded = true;
            return true;
        }
    }
}
