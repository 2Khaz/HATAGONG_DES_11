namespace HATAGONG.Phase3
{
    public readonly struct Phase3ActiveDrag
    {
        internal Phase3ActiveDrag(Phase3PieceModel piece)
        {
            PieceId = piece.PieceId;
            OriginState = piece.State;
            HadOriginFieldCentroid = piece.HasFieldCentroid;
            OriginFieldCentroid = piece.FieldCentroid;
            RotationAtBegin = piece.CurrentRotation;
            HadStableLooseCentroid = piece.HasLastStableLooseCentroid;
            StableLooseCentroidAtBegin = piece.LastStableLooseCentroid;
        }

        public string PieceId { get; }
        public Phase3PieceState OriginState { get; }
        public bool HadOriginFieldCentroid { get; }
        public Phase3Point2D OriginFieldCentroid { get; }
        public Phase3RotationStep RotationAtBegin { get; }
        public bool HadStableLooseCentroid { get; }
        public Phase3Point2D StableLooseCentroidAtBegin { get; }
    }
}
