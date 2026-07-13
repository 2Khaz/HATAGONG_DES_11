using System;
using System.Collections.Generic;

namespace HATAGONG.Phase3
{
    public sealed class Phase3PieceDefinition
    {
        public Phase3PieceDefinition(
            string pieceId,
            string originalDeckSlotId,
            Phase3ShapeDefinition shapeDefinition,
            IEnumerable<Phase3AllowedTarget> allowedTargets)
        {
            if (string.IsNullOrWhiteSpace(pieceId))
            {
                throw new ArgumentException("Piece ID cannot be null or whitespace.", nameof(pieceId));
            }

            if (string.IsNullOrWhiteSpace(originalDeckSlotId))
            {
                throw new ArgumentException("Original deck slot ID cannot be null or whitespace.", nameof(originalDeckSlotId));
            }

            ShapeDefinition = shapeDefinition ?? throw new ArgumentNullException(nameof(shapeDefinition));
            if (allowedTargets == null)
            {
                throw new ArgumentNullException(nameof(allowedTargets));
            }

            var sortedTargets = new List<Phase3AllowedTarget>(allowedTargets);
            sortedTargets.Sort();
            for (int i = 0; i < sortedTargets.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(sortedTargets[i].SlotId))
                {
                    throw new ArgumentException("Allowed targets cannot contain an uninitialized target.", nameof(allowedTargets));
                }
            }

            for (int i = 1; i < sortedTargets.Count; i++)
            {
                if (string.Equals(sortedTargets[i - 1].SlotId, sortedTargets[i].SlotId, StringComparison.Ordinal))
                {
                    throw new ArgumentException($"Duplicate allowed target slot ID '{sortedTargets[i].SlotId}'.", nameof(allowedTargets));
                }
            }

            PieceId = pieceId;
            OriginalDeckSlotId = originalDeckSlotId;
            AllowedTargets = sortedTargets.AsReadOnly();
        }

        public string PieceId { get; }
        public string OriginalDeckSlotId { get; }
        public Phase3ShapeDefinition ShapeDefinition { get; }
        public IReadOnlyList<Phase3AllowedTarget> AllowedTargets { get; }
    }
}
