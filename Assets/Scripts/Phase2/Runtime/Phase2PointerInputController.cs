using HATAGONG.GameFlow;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HATAGONG.Phase2
{
    public sealed class Phase2PointerInputController : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler, IPointerExitHandler, IEndDragHandler
    {
        [SerializeField] private Phase2PhaseAdapter adapter;
        [SerializeField] private RectTransform inputSurface;
        private int _activePointerId = int.MinValue;
        private Vector2 _lastBoardUv;

        public bool InputEnabled { get; private set; }
        public bool HasActivePointer => _activePointerId != int.MinValue;
        public int ActivePointerId => _activePointerId;

        public void SetInputEnabled(bool enabled)
        {
            InputEnabled = enabled;
            if (!enabled) CancelActiveStroke();
        }

        public void CancelActiveStroke()
        {
            _activePointerId = int.MinValue;
            _lastBoardUv = default;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!CanHandle(eventData) || HasActivePointer || !TryMap(eventData, out Vector2 uv)) return;
            _activePointerId = eventData.pointerId;
            _lastBoardUv = uv;
            if (!adapter.TryBeginStroke(uv, out _)) CancelActiveStroke();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!CanHandle(eventData) || eventData.pointerId != _activePointerId || !TryMap(eventData, out Vector2 uv)) return;
            Vector2 previous = _lastBoardUv;
            _lastBoardUv = uv;
            adapter.TryContinueStroke(previous, uv, out _);
            if (!InputEnabled) CancelActiveStroke();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData != null && eventData.pointerId == _activePointerId) CancelActiveStroke();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (eventData == null || eventData.pointerId != _activePointerId) return;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (eventData != null && eventData.pointerId == _activePointerId) CancelActiveStroke();
        }

        public static Vector2 NormalizeLocalPoint(Rect rect, Vector2 localPoint)
        {
            return new Vector2(
                (localPoint.x - rect.xMin) / rect.width,
                (localPoint.y - rect.yMin) / rect.height);
        }

        private bool CanHandle(PointerEventData eventData)
        {
            return eventData != null && InputEnabled && adapter && adapter.IsRunning;
        }

        public void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) CancelActiveStroke();
        }

        public void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus) CancelActiveStroke();
        }

        private bool TryMap(PointerEventData eventData, out Vector2 boardUv)
        {
            boardUv = default;
            if (!inputSurface || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    inputSurface, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
            {
                return false;
            }
            boardUv = NormalizeLocalPoint(inputSurface.rect, localPoint);
            return true;
        }

        private void OnDisable()
        {
            SetInputEnabled(false);
        }
    }
}
