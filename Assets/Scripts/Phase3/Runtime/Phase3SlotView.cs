using System.Collections.Generic;
using UnityEngine;

namespace HATAGONG.Phase3
{
    public sealed class Phase3SlotView : MonoBehaviour
    {
        [SerializeField] private RectTransform rectTransform;
        [SerializeField] private Phase3PolygonGraphic graphic;
        public string SlotId { get; private set; }

        public void Configure(Phase3SlotModel slot, RectTransform targetLayer, Phase3FieldCoordinateMapper mapper, Phase3RuntimeVisualSettings settings)
        {
            if (!rectTransform) rectTransform = transform as RectTransform;
            if (!graphic) graphic = GetComponent<Phase3PolygonGraphic>();
            SlotId = slot.SlotId;
            rectTransform.SetParent(targetLayer, false);
            rectTransform.anchoredPosition = mapper.CanonicalToRectLocal(slot.Definition.CorrectCentroid);
            rectTransform.localRotation = Quaternion.Euler(0f, 0f, Phase3FieldCoordinateMapper.ToUiZ(slot.Definition.CorrectBaseRotationStep));
            var points = new List<Vector2>();
            Phase3ShapeDefinition shape = slot.Definition.ShapeDefinition;
            for (int i = 0; i < shape.Vertices.Count; i++) points.Add(new Vector2((float)((shape.Vertices[i].X - shape.Centroid.X) * Phase3FieldCoordinateMapper.LogicalScale), (float)((shape.Vertices[i].Y - shape.Centroid.Y) * Phase3FieldCoordinateMapper.LogicalScale)));
            graphic.SetVertices(points); graphic.color = settings.TargetColor; graphic.raycastTarget = false;
            graphic.OutlineColor = settings.OutlineColor; graphic.OutlineWidth = settings.OutlineWidth;
            gameObject.SetActive(!slot.IsOccupied);
        }

        public void Refresh(Phase3SlotModel slot) => gameObject.SetActive(!slot.IsOccupied);
    }
}
