using System;
using System.Collections.Generic;

namespace HATAGONG.Phase3
{
    public enum Phase3SnapResultCode
    {
        Success = 0,
        NoAllowedTarget,
        AllAllowedTargetsOccupied,
        OutOfSnapDistance,
        RotationMismatch,
        InvalidInput
    }

    public readonly struct Phase3SnapResult
    {
        private Phase3SnapResult(
            Phase3SnapResultCode code,
            string targetSlotId,
            Phase3Point2D targetCentroid,
            Phase3RotationStep requiredRotation,
            Phase3RotationStep rotationCorrection,
            double distanceSquared)
        {
            Code = code;
            TargetSlotId = targetSlotId ?? string.Empty;
            TargetCentroid = targetCentroid;
            RequiredRotation = requiredRotation;
            RotationCorrection = rotationCorrection;
            DistanceSquared = distanceSquared;
        }

        public Phase3SnapResultCode Code { get; }
        public bool IsSuccess => Code == Phase3SnapResultCode.Success;
        public string TargetSlotId { get; }
        public Phase3Point2D TargetCentroid { get; }
        public Phase3RotationStep RequiredRotation { get; }
        public Phase3RotationStep RotationCorrection { get; }
        public double DistanceSquared { get; }

        public static Phase3SnapResult Success(
            string targetSlotId,
            Phase3Point2D targetCentroid,
            Phase3RotationStep requiredRotation,
            Phase3RotationStep rotationCorrection,
            double distanceSquared)
        {
            return new Phase3SnapResult(
                Phase3SnapResultCode.Success,
                targetSlotId,
                targetCentroid,
                requiredRotation,
                rotationCorrection,
                distanceSquared);
        }

        public static Phase3SnapResult Failure(Phase3SnapResultCode code)
        {
            if (code == Phase3SnapResultCode.Success)
            {
                throw new ArgumentException("Use Success to create a successful snap result.", nameof(code));
            }

            return new Phase3SnapResult(code, string.Empty, default, default, default, double.PositiveInfinity);
        }
    }

    public static class Phase3SnapRules
    {
        public static Phase3SnapResult Evaluate(
            Phase3PieceDefinition piece,
            Phase3Point2D displayedPieceCentroid,
            Phase3RotationStep currentRotation,
            double snapDistance,
            IEnumerable<Phase3SlotDefinition> slots,
            IEnumerable<string> occupiedSlotIds)
        {
            if (piece == null || !displayedPieceCentroid.IsFinite ||
                !Phase3Point2D.IsFiniteValue(snapDistance) || snapDistance < 0d ||
                slots == null || occupiedSlotIds == null)
            {
                return Phase3SnapResult.Failure(Phase3SnapResultCode.InvalidInput);
            }

            var slotsById = new Dictionary<string, Phase3SlotDefinition>(StringComparer.Ordinal);
            foreach (Phase3SlotDefinition slot in slots)
            {
                if (slot == null || slotsById.ContainsKey(slot.SlotId))
                {
                    return Phase3SnapResult.Failure(Phase3SnapResultCode.InvalidInput);
                }

                slotsById.Add(slot.SlotId, slot);
            }

            var occupied = new HashSet<string>(StringComparer.Ordinal);
            foreach (string occupiedSlotId in occupiedSlotIds)
            {
                if (string.IsNullOrWhiteSpace(occupiedSlotId))
                {
                    return Phase3SnapResult.Failure(Phase3SnapResultCode.InvalidInput);
                }

                occupied.Add(occupiedSlotId);
            }

            if (piece.AllowedTargets.Count == 0)
            {
                return Phase3SnapResult.Failure(Phase3SnapResultCode.NoAllowedTarget);
            }

            int existingTargetCount = 0;
            int availableTargetCount = 0;
            int rotationCompatibleCount = 0;
            bool hasBest = false;
            double bestDistanceSquared = double.PositiveInfinity;
            Phase3AllowedTarget bestAllowedTarget = default;
            Phase3SlotDefinition bestSlot = null;
            double maximumDistanceSquared = snapDistance * snapDistance;

            for (int i = 0; i < piece.AllowedTargets.Count; i++)
            {
                Phase3AllowedTarget allowedTarget = piece.AllowedTargets[i];
                if (!slotsById.TryGetValue(allowedTarget.SlotId, out Phase3SlotDefinition slot))
                {
                    continue;
                }

                existingTargetCount++;
                if (occupied.Contains(allowedTarget.SlotId))
                {
                    continue;
                }

                availableTargetCount++;
                if (!piece.ShapeDefinition.IsRotationEquivalent(currentRotation, allowedTarget.RequiredRotationStep))
                {
                    continue;
                }

                rotationCompatibleCount++;
                double distanceSquared = displayedPieceCentroid.DistanceSquaredTo(slot.CorrectCentroid);
                if (!Phase3Point2D.IsFiniteValue(distanceSquared) ||
                    distanceSquared - maximumDistanceSquared > Phase3CoreConstants.ComparisonEpsilon)
                {
                    continue;
                }

                bool closer = distanceSquared < bestDistanceSquared - Phase3CoreConstants.ComparisonEpsilon;
                bool tiedAndLowerId = Math.Abs(distanceSquared - bestDistanceSquared) <= Phase3CoreConstants.ComparisonEpsilon &&
                                      (bestSlot == null || string.CompareOrdinal(slot.SlotId, bestSlot.SlotId) < 0);
                if (!hasBest || closer || tiedAndLowerId)
                {
                    hasBest = true;
                    bestDistanceSquared = distanceSquared;
                    bestAllowedTarget = allowedTarget;
                    bestSlot = slot;
                }
            }

            if (existingTargetCount == 0)
            {
                return Phase3SnapResult.Failure(Phase3SnapResultCode.NoAllowedTarget);
            }

            if (availableTargetCount == 0)
            {
                return Phase3SnapResult.Failure(Phase3SnapResultCode.AllAllowedTargetsOccupied);
            }

            if (rotationCompatibleCount == 0)
            {
                return Phase3SnapResult.Failure(Phase3SnapResultCode.RotationMismatch);
            }

            if (!hasBest)
            {
                return Phase3SnapResult.Failure(Phase3SnapResultCode.OutOfSnapDistance);
            }

            return Phase3SnapResult.Success(
                bestSlot.SlotId,
                bestSlot.CorrectCentroid,
                bestAllowedTarget.RequiredRotationStep,
                bestAllowedTarget.RotationCorrectionStep,
                bestDistanceSquared);
        }
    }
}
