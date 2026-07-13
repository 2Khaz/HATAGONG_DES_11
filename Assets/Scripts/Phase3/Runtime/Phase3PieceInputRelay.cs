using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace HATAGONG.Phase3
{
    public sealed class Phase3PieceInputRelay : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private Phase3RuntimeOrchestrator orchestrator;
        private string pieceId;
        private Vector2 pendingAnchor;
        private int pendingPointerId = int.MinValue;
        private bool eligible;

        public void Bind(Phase3RuntimeOrchestrator value) => orchestrator = value;
        public void BindPiece(string value) => pieceId = value;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            eligible = orchestrator && orchestrator.TryCalculatePressAnchor(pieceId, transform as RectTransform, eventData, out pendingAnchor);
            pendingPointerId = eligible ? eventData.pointerId : int.MinValue;
        }
        public void OnBeginDrag(PointerEventData eventData) { if (eligible && eventData.pointerId == pendingPointerId) orchestrator.TryBeginPointerDrag(pieceId, eventData, pendingAnchor, GetDeviceKind(eventData), GetTouchId(eventData), GetDeviceId(eventData)); }
        public void OnDrag(PointerEventData eventData) { if (orchestrator) orchestrator.UpdatePointerDrag(eventData.pointerId, eventData.position, eventData.pressEventCamera); }
        public void OnEndDrag(PointerEventData eventData) { if (eventData.button != PointerEventData.InputButton.Left) return; if (orchestrator) orchestrator.EndPointerDrag(eventData.pointerId); ClearCandidate(); }
        public void OnPointerUp(PointerEventData eventData) { if (eventData.button != PointerEventData.InputButton.Left) return; if (orchestrator) orchestrator.EndPointerDrag(eventData.pointerId); ClearCandidate(); }
        public void OnPointerExit(PointerEventData eventData) { }

        private void OnDisable() => ClearCandidate();

        private void ClearCandidate()
        {
            eligible = false;
            pendingPointerId = int.MinValue;
            pendingAnchor = default;
        }

        public static Phase3PointerDeviceKind GetDeviceKind(PointerEventData eventData)
        {
            if (eventData is ExtendedPointerEventData extended)
            {
                if (extended.device is Touchscreen) return Phase3PointerDeviceKind.Touch;
                if (extended.device is Mouse) return Phase3PointerDeviceKind.Mouse;
            }
            return eventData.pointerId == PointerInputModule.kMouseLeftId ? Phase3PointerDeviceKind.Mouse : Phase3PointerDeviceKind.Unknown;
        }

        public static int GetTouchId(PointerEventData eventData) => eventData is ExtendedPointerEventData extended && extended.device is Touchscreen ? extended.touchId : 0;
        public static int GetDeviceId(PointerEventData eventData) => eventData is ExtendedPointerEventData extended && extended.device != null ? extended.device.deviceId : 0;
    }
}
