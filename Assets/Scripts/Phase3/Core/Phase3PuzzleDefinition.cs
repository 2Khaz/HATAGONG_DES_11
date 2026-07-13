using System;
using System.Collections.Generic;

namespace HATAGONG.Phase3
{
    public sealed class Phase3PuzzleDefinition
    {
        public Phase3PuzzleDefinition(
            IEnumerable<Phase3PieceDefinition> pieces,
            IEnumerable<Phase3SlotDefinition> slots)
        {
            if (pieces == null)
            {
                throw new ArgumentNullException(nameof(pieces));
            }

            if (slots == null)
            {
                throw new ArgumentNullException(nameof(slots));
            }

            var sortedPieces = new List<Phase3PieceDefinition>(pieces);
            var sortedSlots = new List<Phase3SlotDefinition>(slots);
            if (sortedPieces.Count == 0)
            {
                throw new ArgumentException("A puzzle requires at least one piece.", nameof(pieces));
            }

            if (sortedSlots.Count == 0)
            {
                throw new ArgumentException("A puzzle requires at least one slot.", nameof(slots));
            }

            if (sortedPieces.Exists(piece => piece == null))
            {
                throw new ArgumentException("Piece collection cannot contain null.", nameof(pieces));
            }

            if (sortedSlots.Exists(slot => slot == null))
            {
                throw new ArgumentException("Slot collection cannot contain null.", nameof(slots));
            }

            sortedPieces.Sort((left, right) => string.CompareOrdinal(left.PieceId, right.PieceId));
            sortedSlots.Sort((left, right) => string.CompareOrdinal(left.SlotId, right.SlotId));

            for (int i = 1; i < sortedPieces.Count; i++)
            {
                if (string.Equals(sortedPieces[i - 1].PieceId, sortedPieces[i].PieceId, StringComparison.Ordinal))
                {
                    throw new ArgumentException($"Duplicate piece ID '{sortedPieces[i].PieceId}'.", nameof(pieces));
                }
            }

            for (int i = 1; i < sortedSlots.Count; i++)
            {
                if (string.Equals(sortedSlots[i - 1].SlotId, sortedSlots[i].SlotId, StringComparison.Ordinal))
                {
                    throw new ArgumentException($"Duplicate slot ID '{sortedSlots[i].SlotId}'.", nameof(slots));
                }
            }

            var deckSlotIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < sortedPieces.Count; i++)
            {
                Phase3PieceDefinition piece = sortedPieces[i];
                if (!deckSlotIds.Add(piece.OriginalDeckSlotId))
                {
                    throw new ArgumentException($"Duplicate original deck slot ID '{piece.OriginalDeckSlotId}'.", nameof(pieces));
                }

                if (piece.AllowedTargets.Count == 0)
                {
                    throw new ArgumentException($"Piece '{piece.PieceId}' requires at least one allowed target.", nameof(pieces));
                }

                for (int targetIndex = 0; targetIndex < piece.AllowedTargets.Count; targetIndex++)
                {
                    string targetSlotId = piece.AllowedTargets[targetIndex].SlotId;
                    if (!ContainsSlot(sortedSlots, targetSlotId))
                    {
                        throw new ArgumentException($"Piece '{piece.PieceId}' references missing slot '{targetSlotId}'.", nameof(pieces));
                    }
                }
            }

            GridSize = Phase3CoreConstants.LogicalGridSize;
            Pieces = sortedPieces.AsReadOnly();
            Slots = sortedSlots.AsReadOnly();
        }

        public int GridSize { get; }
        public IReadOnlyList<Phase3PieceDefinition> Pieces { get; }
        public IReadOnlyList<Phase3SlotDefinition> Slots { get; }

        private static bool ContainsSlot(IReadOnlyList<Phase3SlotDefinition> sortedSlots, string slotId)
        {
            int low = 0;
            int high = sortedSlots.Count - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) / 2);
                int comparison = string.CompareOrdinal(sortedSlots[middle].SlotId, slotId);
                if (comparison == 0)
                {
                    return true;
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

            return false;
        }
    }
}
