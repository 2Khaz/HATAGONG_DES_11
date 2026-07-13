namespace HATAGONG.Phase3
{
    public enum Phase3PlayOperation
    {
        None = 0,
        BeginDrag,
        Rotate,
        CancelDrag,
        FieldDrop,
        DeckReturn
    }

    public readonly struct Phase3PlayResult
    {
        private Phase3PlayResult(
            Phase3PlayOperation operation,
            Phase3PlayFailure failure,
            string pieceId,
            string targetSlotId,
            bool piecePlaced,
            bool phaseCleared,
            int manualSnapScoreDelta,
            int clearScoreDelta,
            bool hasSnapResult,
            Phase3SnapResultCode snapResultCode)
        {
            Operation = operation;
            Failure = failure;
            PieceId = pieceId ?? string.Empty;
            TargetSlotId = targetSlotId ?? string.Empty;
            PiecePlaced = piecePlaced;
            PhaseCleared = phaseCleared;
            ManualSnapScoreDelta = manualSnapScoreDelta;
            ClearScoreDelta = clearScoreDelta;
            HasSnapResult = hasSnapResult;
            SnapResultCode = snapResultCode;
        }

        public Phase3PlayOperation Operation { get; }
        public Phase3PlayFailure Failure { get; }
        public bool IsSuccess => Failure == Phase3PlayFailure.None;
        public string PieceId { get; }
        public string TargetSlotId { get; }
        public bool PiecePlaced { get; }
        public bool PhaseCleared { get; }
        public int ManualSnapScoreDelta { get; }
        public int ClearScoreDelta { get; }
        public int TotalScoreDelta => ManualSnapScoreDelta + ClearScoreDelta;
        public bool HasSnapResult { get; }
        public Phase3SnapResultCode SnapResultCode { get; }

        internal static Phase3PlayResult Success(Phase3PlayOperation operation, string pieceId)
        {
            return new Phase3PlayResult(operation, Phase3PlayFailure.None, pieceId, string.Empty, false, false, 0, 0, false, default);
        }

        internal static Phase3PlayResult Placement(
            string pieceId,
            string slotId,
            int manualSnapScoreDelta,
            int clearScoreDelta,
            bool phaseCleared)
        {
            return new Phase3PlayResult(
                Phase3PlayOperation.FieldDrop,
                Phase3PlayFailure.None,
                pieceId,
                slotId,
                true,
                phaseCleared,
                manualSnapScoreDelta,
                clearScoreDelta,
                true,
                Phase3SnapResultCode.Success);
        }

        internal static Phase3PlayResult FailureResult(
            Phase3PlayOperation operation,
            Phase3PlayFailure failure,
            string pieceId,
            bool hasSnapResult = false,
            Phase3SnapResultCode snapResultCode = default)
        {
            return new Phase3PlayResult(operation, failure, pieceId, string.Empty, false, false, 0, 0, hasSnapResult, snapResultCode);
        }
    }
}
