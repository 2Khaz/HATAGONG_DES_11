using System;
using UnityEngine;

namespace HATAGONG.Phase3
{
    public sealed class Phase3FieldCoordinateMapper
    {
        public const float LogicalScale = (float)Phase3CoreConstants.CanvasFieldSize / Phase3CoreConstants.LogicalGridSize;
        private readonly RectTransform fieldRoot;

        public Phase3FieldCoordinateMapper(RectTransform fieldRoot)
        {
            this.fieldRoot = fieldRoot ? fieldRoot : throw new ArgumentNullException(nameof(fieldRoot));
            ValidateFieldRect(fieldRoot.rect);
        }

        public RectTransform FieldRoot => fieldRoot;
        public Phase3Point2D LogicalToCanonical(Phase3Point2D point) => point * LogicalScale;
        public Phase3Point2D CanonicalToLogical(Phase3Point2D point) => point / LogicalScale;
        public Vector2 CanonicalToRectLocal(Phase3Point2D point) => new Vector2((float)point.X + fieldRoot.rect.xMin, (float)point.Y + fieldRoot.rect.yMin);
        public Phase3Point2D RectLocalToCanonical(Vector2 point) => new Phase3Point2D(point.x - fieldRoot.rect.xMin, point.y - fieldRoot.rect.yMin);
        public Vector2 LogicalToRectLocal(Phase3Point2D point) => CanonicalToRectLocal(LogicalToCanonical(point));
        public Phase3Point2D RectLocalToLogical(Vector2 point) => CanonicalToLogical(RectLocalToCanonical(point));
        public Vector2 LogicalVectorToCanonical(Phase3Point2D vector) => new Vector2((float)(vector.X * LogicalScale), (float)(vector.Y * LogicalScale));
        public Vector2 CanonicalVectorToFieldLocal(Phase3Point2D vector) => new Vector2((float)vector.X, (float)vector.Y);
        public Phase3Point2D FieldLocalVectorToCanonical(Vector2 vector) => new Phase3Point2D(vector.x, vector.y);

        public bool TryScreenToRectLocal(Vector2 screen, Camera camera, out Vector2 local) =>
            RectTransformUtility.ScreenPointToLocalPointInRectangle(fieldRoot, screen, camera, out local);

        public bool TryScreenToCanonical(Vector2 screen, Camera camera, out Phase3Point2D canonical)
        {
            if (TryScreenToRectLocal(screen, camera, out Vector2 local))
            {
                canonical = RectLocalToCanonical(local);
                return true;
            }
            canonical = default;
            return false;
        }

        public bool IsCanonicalInside(Phase3Point2D point) => point.IsFinite && point.X >= 0d && point.X <= Phase3CoreConstants.CanvasFieldSize && point.Y >= 0d && point.Y <= Phase3CoreConstants.CanvasFieldSize;
        public bool IsRectLocalInside(Vector2 point) => fieldRoot.rect.Contains(point);
        public static bool IsStrictInterior(Rect rect, Vector2 point) => point.x > rect.xMin && point.x < rect.xMax && point.y > rect.yMin && point.y < rect.yMax;
        public static float ToUiZ(Phase3RotationStep rotation) => -rotation.Degrees;

        public static Vector2 RotateClockwise(Vector2 value, Phase3RotationStep rotation)
        {
            float radians = -rotation.Degrees * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(radians);
            float sine = Mathf.Sin(radians);
            return new Vector2(value.x * cosine - value.y * sine, value.x * sine + value.y * cosine);
        }

        public static void ValidateFieldRect(Rect rect)
        {
            if (Mathf.Abs(rect.width - Phase3CoreConstants.CanvasFieldSize) > 0.01f || Mathf.Abs(rect.height - Phase3CoreConstants.CanvasFieldSize) > 0.01f)
                throw new ArgumentException("Field rect must be exactly 1250 by 1250 Canvas Units.");
        }
    }
}
