using UnityEngine;
using UnityEngine.EventSystems;

namespace HATAGONG.Phase3Tangram
{
    public sealed class Phase3TangramInputAdapter : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        private Phase3TangramManager manager;

        public void Configure(Phase3TangramManager value) => manager = value;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!manager) return;
            if (eventData.button == PointerEventData.InputButton.Right) manager.RotateActive(1);
            else if (eventData.button == PointerEventData.InputButton.Left) manager.SelectPieceAt(eventData.position);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && manager) manager.BeginSelectedDrag(eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && manager) manager.DragSelected(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && manager) manager.EndSelectedDrag(eventData.position);
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (!manager || Mathf.Approximately(eventData.scrollDelta.y, 0f)) return;
            manager.RotateActive(eventData.scrollDelta.y > 0f ? -1 : 1);
        }
    }
}
