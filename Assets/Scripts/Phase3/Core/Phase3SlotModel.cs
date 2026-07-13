using System;

namespace HATAGONG.Phase3
{
    public sealed class Phase3SlotModel
    {
        internal Phase3SlotModel(Phase3SlotDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            OccupyingPieceId = string.Empty;
        }

        public Phase3SlotDefinition Definition { get; }
        public string SlotId => Definition.SlotId;
        public bool IsOccupied { get; private set; }
        public string OccupyingPieceId { get; private set; }

        internal void Occupy(string pieceId)
        {
            IsOccupied = true;
            OccupyingPieceId = pieceId;
        }
    }
}
