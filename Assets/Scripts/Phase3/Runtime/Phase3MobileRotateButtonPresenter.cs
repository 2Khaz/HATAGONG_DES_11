using UnityEngine;
using UnityEngine.UI;

namespace HATAGONG.Phase3
{
    public sealed class Phase3MobileRotateButtonPresenter
    {
        private readonly Button button;
        private readonly Phase3RuntimeOrchestrator orchestrator;
        public Phase3MobileRotateButtonPresenter(Button button, Phase3RuntimeOrchestrator orchestrator)
        {
            this.button = button; this.orchestrator = orchestrator; button.onClick.AddListener(OnPressed);
        }
        public void Refresh(bool mobilePlatform)
        {
            bool visible = mobilePlatform && orchestrator.CanRotateActiveDrag;
            button.gameObject.SetActive(visible); button.interactable = visible;
        }
        public void Dispose() => button.onClick.RemoveListener(OnPressed);
        private void OnPressed() => orchestrator.TryRotateClockwise();
    }
}
