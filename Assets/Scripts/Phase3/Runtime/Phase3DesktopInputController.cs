using UnityEngine;
using UnityEngine.InputSystem;

namespace HATAGONG.Phase3
{
    public readonly struct Phase3DesktopInputSnapshot
    {
        public Phase3DesktopInputSnapshot(float wheel, bool rightPressed, bool rPressed)
        {
            Wheel = wheel; RightPressed = rightPressed; RPressed = rPressed;
        }
        public float Wheel { get; }
        public bool RightPressed { get; }
        public bool RPressed { get; }
    }

    public sealed class Phase3DesktopInputController : MonoBehaviour
    {
        [SerializeField] private Phase3RuntimeOrchestrator orchestrator;
        public void Bind(Phase3RuntimeOrchestrator value) => orchestrator = value;
        private void Update()
        {
            float wheel = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
            ProcessSnapshot(new Phase3DesktopInputSnapshot(
                wheel,
                Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame,
                Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame));
        }

        public bool ProcessSnapshot(Phase3DesktopInputSnapshot snapshot)
        {
            if (!orchestrator || !orchestrator.CanRotateActiveDrag) return false;
            if (snapshot.Wheel > 0f) return orchestrator.TryRotateClockwise();
            if (snapshot.Wheel < 0f) return orchestrator.TryRotateCounterClockwise();
            if (snapshot.RightPressed) return orchestrator.TryRotateClockwise();
            if (snapshot.RPressed) return orchestrator.TryRotateClockwise();
            return false;
        }
    }
}
