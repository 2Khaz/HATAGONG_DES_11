using TMPro;
using UnityEngine;

namespace HATAGONG.GameFlow
{
    public sealed class GameTimerPresenter : MonoBehaviour
    {
        [SerializeField] private GameTimerController controller;
        [SerializeField] private TextMeshProUGUI timeValueText;
        private GameCountdownTimer subscribedTimer;

        private void Start() { Subscribe(); Refresh(); }
        private void OnEnable() { Subscribe(); Refresh(); }
        private void OnDisable() { Unsubscribe(); }

        private void Subscribe()
        {
            var next = controller ? controller.Timer : null;
            if (ReferenceEquals(next, subscribedTimer)) return;
            Unsubscribe(); subscribedTimer = next;
            if (subscribedTimer != null) subscribedTimer.DisplayedSecondChanged += Present;
        }

        private void Unsubscribe()
        {
            if (subscribedTimer != null) subscribedTimer.DisplayedSecondChanged -= Present;
            subscribedTimer = null;
        }

        private void Refresh() { if (controller && controller.Timer != null) Present(controller.DisplayedSeconds); }
        private void Present(int seconds) { if (timeValueText) timeValueText.text = GameCountdownTimer.FormatSeconds(seconds); }
    }
}
