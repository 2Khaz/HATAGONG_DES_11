using UnityEngine;
using UnityEngine.EventSystems;

namespace HATAGONG.Phase3
{
    public sealed class Phase3EmptyFieldSurface : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private Phase3MobileInputController mobileInput;
        public void Bind(Phase3MobileInputController controller) => mobileInput = controller;
        public void Unbind() => mobileInput = null;
        public bool IsBound => mobileInput != null;
        public void OnPointerDown(PointerEventData eventData) { if (mobileInput) mobileInput.BeginSecondaryTap(eventData); }
        public void OnPointerUp(PointerEventData eventData) { if (mobileInput) mobileInput.EndSecondaryTap(eventData); }
    }
}
