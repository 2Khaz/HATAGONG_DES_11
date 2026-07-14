namespace HATAGONG.Phase3
{
    public enum Phase3PlayFailure
    {
        None = 0,
        PieceNotFound,
        SlotNotFound,
        PhaseAlreadyCleared,
        AnotherPieceAlreadyDragging,
        PieceAlreadyDragging,
        PiecePlacedImmutable,
        NoActiveDrag,
        WrongActivePiece,
        PieceNotDragging,
        InvalidCentroid,
        InvalidDropIntent,
        SnapNoAllowedTarget,
        SnapAllTargetsOccupied,
        SnapOutOfDistance,
        SnapRotationMismatch,
        SnapGeometryMismatch,
        InternalStateConflict
    }
}
