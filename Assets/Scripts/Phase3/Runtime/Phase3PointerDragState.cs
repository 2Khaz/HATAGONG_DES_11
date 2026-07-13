using UnityEngine;

namespace HATAGONG.Phase3
{
    public enum Phase3PointerDeviceKind { Unknown, Mouse, Touch }

    public sealed class Phase3PointerDragState
    {
        public string PieceId { get; internal set; }
        public int PointerId { get; internal set; }
        public Phase3PointerDeviceKind DeviceKind { get; internal set; }
        public int DeviceId { get; internal set; }
        public int TouchId { get; internal set; }
        public Vector2 AnchorCanonical { get; internal set; }
        public Vector2 ScreenPosition { get; internal set; }
        public Camera EventCamera { get; internal set; }
        public Vector2 FieldLocalPosition { get; internal set; }
        public Phase3Point2D CanonicalPointer { get; internal set; }
        public Phase3RotationStep RotationAtBegin { get; internal set; }
        public Phase3PieceState StateAtBegin { get; internal set; }
        public bool UsesMobileLift { get; internal set; }
        public bool DropHandled { get; internal set; }
    }
}
