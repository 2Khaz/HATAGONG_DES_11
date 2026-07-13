using System;
using System.Collections.Generic;
using UnityEngine;

namespace HATAGONG.Phase3
{
    public enum Phase3PieceDisplayState { Hidden, Deck, Dragging, Loose, Placed }

    public sealed class Phase3PieceView : MonoBehaviour
    {
        [SerializeField] private RectTransform rectTransform;
        [SerializeField] private Phase3PolygonGraphic graphic;
        [SerializeField] private Phase3PieceInputRelay inputRelay;

        public string PieceId { get; private set; }
        public RectTransform RectTransform => rectTransform;
        public Phase3PolygonGraphic Graphic => graphic;
        public Phase3PieceInputRelay InputRelay => inputRelay;
        public Phase3PieceDisplayState DisplayState { get; private set; }
        public float GeometryScale { get; private set; } = 1f;

        public void Configure(string pieceId, RectTransform rect, Phase3PolygonGraphic polygon, Phase3PieceInputRelay relay)
        {
            PieceId = pieceId ?? throw new ArgumentNullException(nameof(pieceId)); rectTransform = rect; graphic = polygon; inputRelay = relay;
            if (inputRelay) inputRelay.BindPiece(pieceId);
        }

        public void SetGeometry(Phase3ShapeDefinition shape, float scale = 1f)
        {
            GeometryScale = scale;
            var points = new List<Vector2>(shape.Vertices.Count);
            Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < shape.Vertices.Count; i++)
            {
                Phase3Point2D relative = new Phase3Point2D(shape.Vertices[i].X - shape.Centroid.X, shape.Vertices[i].Y - shape.Centroid.Y);
                var point = new Vector2((float)(relative.X * Phase3FieldCoordinateMapper.LogicalScale * scale), (float)(relative.Y * Phase3FieldCoordinateMapper.LogicalScale * scale));
                points.Add(point);
                minimum = Vector2.Min(minimum, point);
                maximum = Vector2.Max(maximum, point);
            }
            Vector2 size = maximum - minimum;
            rectTransform.sizeDelta = size;
            rectTransform.pivot = new Vector2(size.x > Mathf.Epsilon ? -minimum.x / size.x : 0.5f, size.y > Mathf.Epsilon ? -minimum.y / size.y : 0.5f);
            graphic.SetVertices(points);
        }

        public void Present(Phase3PieceDisplayState state, Phase3PieceModel model, RectTransform parent, Vector2 localPosition, Color color, float emphasis = 1f)
        {
            DisplayState = state;
            if (rectTransform.parent != parent) rectTransform.SetParent(parent, false);
            bool shown = state != Phase3PieceDisplayState.Hidden;
            gameObject.SetActive(shown);
            if (!shown) return;
            rectTransform.anchoredPosition = localPosition;
            rectTransform.localRotation = Quaternion.Euler(0f, 0f, Phase3FieldCoordinateMapper.ToUiZ(model.CurrentRotation));
            rectTransform.localScale = Vector3.one * emphasis;
            if (state == Phase3PieceDisplayState.Dragging) rectTransform.SetAsLastSibling();
            graphic.color = color;
            graphic.raycastTarget = state == Phase3PieceDisplayState.Deck || state == Phase3PieceDisplayState.Loose || state == Phase3PieceDisplayState.Dragging;
        }
    }
}
