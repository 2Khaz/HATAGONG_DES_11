using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace HATAGONG.Phase3
{
    public sealed class Phase3MobileInputController : MonoBehaviour
    {
        private struct Tap { public int PointerId; public int TouchId; public int DeviceId; public Vector2 Start; public float Time; }
        [SerializeField] private Phase3RuntimeOrchestrator orchestrator;
        [SerializeField] private Phase3RuntimeBinding binding;
        private Tap? tap;
        public bool HasPendingSecondaryTap => tap.HasValue;
        public int PendingPointerId => tap.HasValue ? tap.Value.PointerId : int.MinValue;
        public int PendingTouchId => tap.HasValue ? tap.Value.TouchId : 0;
        public void Bind(Phase3RuntimeOrchestrator value, Phase3RuntimeBinding valueBinding) { orchestrator = value; binding = valueBinding; CancelPendingSecondaryTap(); }
        public void Unbind() { CancelPendingSecondaryTap(); orchestrator = null; binding = null; }
        public void CancelPendingSecondaryTap() => tap = null;

        public void BeginSecondaryTap(PointerEventData data)
        {
            BeginSecondaryTap(data, Time.unscaledTime);
        }

        public void BeginSecondaryTap(PointerEventData data, float currentTime)
        {
            if (tap.HasValue || !orchestrator || !binding || !orchestrator.HasPrimaryTouchDrag || data.pointerId == orchestrator.ActivePointerId) return;
            if (!TryGetTouchIdentity(data, out int touchId, out int deviceId) || touchId == orchestrator.ActiveTouchId) return;
            if (deviceId != orchestrator.ActiveDeviceId) return;
            if (!orchestrator.Mapper.TryScreenToRectLocal(data.position, data.pressEventCamera, out Vector2 local) || !orchestrator.Mapper.IsRectLocalInside(local)) return;
            if (!IsEmptyAt(data)) return;
            tap = new Tap { PointerId = data.pointerId, TouchId = touchId, DeviceId = deviceId, Start = data.position, Time = currentTime };
        }

        public void EndSecondaryTap(PointerEventData data)
        {
            EndSecondaryTap(data, Time.unscaledTime);
        }

        public void EndSecondaryTap(PointerEventData data, float currentTime)
        {
            if (!tap.HasValue || tap.Value.PointerId != data.pointerId) return;
            Tap candidate = tap.Value;
            if (!TryGetTouchIdentity(data, out int touchId, out int deviceId) || touchId != candidate.TouchId || deviceId != candidate.DeviceId) return;
            tap = null;
            if (!orchestrator || !orchestrator.HasPrimaryTouchDrag || !orchestrator.InputEnabled || orchestrator.SessionCleared) return;
            if (currentTime - candidate.Time > Phase3RuntimeInputPolicy.SecondaryTapMaximumDuration || Vector2.Distance(candidate.Start, data.position) > Phase3RuntimeInputPolicy.SecondaryTapMaximumMovement) return;
            if (!IsEmptyAt(data)) return;
            if (!orchestrator.Mapper.TryScreenToRectLocal(data.position, data.pressEventCamera, out Vector2 local) || !orchestrator.Mapper.IsRectLocalInside(local)) return;
            orchestrator.TryRotateClockwise();
        }

        private static bool TryGetTouchIdentity(PointerEventData data, out int touchId, out int deviceId)
        {
            if (data is ExtendedPointerEventData extended && extended.device is Touchscreen touchscreen && extended.touchId != 0)
            {
                touchId = extended.touchId;
                deviceId = touchscreen.deviceId;
                return true;
            }
            touchId = 0;
            deviceId = 0;
            return false;
        }
        public static bool IsTopRaycastEmpty(IList<RaycastResult> results, Phase3EmptyFieldSurface surface) => results != null && results.Count > 0 && results[0].gameObject == surface.gameObject;

        private bool IsEmptyAt(PointerEventData data)
        {
            var results = new List<RaycastResult>();
            binding.GraphicRaycaster.Raycast(data, results);
            if (results.Count == 0) return false;
            Transform hit = results[0].gameObject.transform;
            Transform surface = binding.EmptyFieldSurface.transform;
            return hit == surface || hit.IsChildOf(surface);
        }
    }
}
