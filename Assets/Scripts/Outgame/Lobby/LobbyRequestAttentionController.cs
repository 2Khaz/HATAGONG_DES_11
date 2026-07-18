using UnityEngine;
using UnityEngine.UI;

namespace HATAGONG.Outgame
{
    [DisallowMultipleComponent]
    public sealed class LobbyRequestAttentionController : MonoBehaviour
    {
        private const float UnclickedDelay = 3f;
        private const float ToOrangeDuration = 0.4f;
        private const float OrangeHoldDuration = 0.15f;
        private const float ToOriginalDuration = 0.4f;
        private const float OriginalHoldDuration = 0.15f;
        private const float CycleDuration =
            ToOrangeDuration + OrangeHoldDuration + ToOriginalDuration + OriginalHoldDuration;

        private static readonly Color Orange = new Color(1f, 138f / 255f, 0f, 1f);

        [SerializeField] private Button questIndicatorButton;
        [SerializeField] private Graphic attentionGraphic;
        [SerializeField] private OutgameRequestSelectionController requestSelectionController;
        [SerializeField] private OutgameLobbyProgressQuitController quitController;
        [SerializeField] private GameObject[] blockingPopupRoots;

        private Color originalColor;
        private Selectable.Transition originalButtonTransition;
        private float idleElapsed;
        private float pulseElapsed;
        private bool wasSuppressed;
        private bool ownsButtonTransition;

        private void Awake()
        {
            if (attentionGraphic != null)
            {
                originalColor = attentionGraphic.color;
            }

            if (questIndicatorButton != null)
            {
                originalButtonTransition = questIndicatorButton.transition;
                ownsButtonTransition = questIndicatorButton.targetGraphic == attentionGraphic;
            }
        }

        private void OnEnable()
        {
            if (ownsButtonTransition && questIndicatorButton != null)
            {
                questIndicatorButton.transition = Selectable.Transition.None;
                ClearButtonTint();
            }

            if (questIndicatorButton != null)
            {
                questIndicatorButton.onClick.AddListener(HandleQuestIndicatorClicked);
            }

            ResetAttentionTimer();
            wasSuppressed = IsAttentionSuppressed();
        }

        private void Update()
        {
            bool suppressed = IsAttentionSuppressed();

            if (suppressed)
            {
                StopAndRestore();
                wasSuppressed = true;
                return;
            }

            if (wasSuppressed)
            {
                wasSuppressed = false;
                ResetAttentionTimer();
                return;
            }

            idleElapsed += Time.unscaledDeltaTime;
            if (idleElapsed < UnclickedDelay)
            {
                return;
            }

            pulseElapsed = Mathf.Repeat(pulseElapsed + Time.unscaledDeltaTime, CycleDuration);
            ApplyPulseColor(pulseElapsed);
        }

        private void OnDisable()
        {
            if (questIndicatorButton != null)
            {
                questIndicatorButton.onClick.RemoveListener(HandleQuestIndicatorClicked);
                if (ownsButtonTransition)
                {
                    questIndicatorButton.transition = originalButtonTransition;
                }
            }

            StopAndRestore();
            wasSuppressed = false;
        }

        private void HandleQuestIndicatorClicked()
        {
            ResetAttentionTimer();
        }

        private void ResetAttentionTimer()
        {
            idleElapsed = 0f;
            StopAndRestore();
        }

        private void StopAndRestore()
        {
            pulseElapsed = 0f;
            if (attentionGraphic != null)
            {
                ClearButtonTint();
                attentionGraphic.color = originalColor;
            }
        }

        private void ClearButtonTint()
        {
            if (attentionGraphic == null)
            {
                return;
            }

            // Button ColorTint is applied through Graphic.CrossFadeColor and can leave
            // its disabled alpha on the CanvasRenderer after the transition is disabled.
            // A zero-duration white tint cancels that tween without changing Image.color.
            attentionGraphic.CrossFadeColor(Color.white, 0f, true, true);
        }

        private void ApplyPulseColor(float cycleTime)
        {
            if (attentionGraphic == null)
            {
                return;
            }

            Color orangeWithOriginalAlpha = Orange;
            orangeWithOriginalAlpha.a = originalColor.a;

            if (cycleTime < ToOrangeDuration)
            {
                float t = Mathf.SmoothStep(0f, 1f, cycleTime / ToOrangeDuration);
                attentionGraphic.color = Color.Lerp(originalColor, orangeWithOriginalAlpha, t);
                return;
            }

            cycleTime -= ToOrangeDuration;
            if (cycleTime < OrangeHoldDuration)
            {
                attentionGraphic.color = orangeWithOriginalAlpha;
                return;
            }

            cycleTime -= OrangeHoldDuration;
            if (cycleTime < ToOriginalDuration)
            {
                float t = Mathf.SmoothStep(0f, 1f, cycleTime / ToOriginalDuration);
                attentionGraphic.color = Color.Lerp(orangeWithOriginalAlpha, originalColor, t);
                return;
            }

            attentionGraphic.color = originalColor;
        }

        private bool IsAttentionSuppressed()
        {
            if (attentionGraphic == null || !attentionGraphic.isActiveAndEnabled ||
                questIndicatorButton == null || !questIndicatorButton.isActiveAndEnabled ||
                !questIndicatorButton.interactable || requestSelectionController == null ||
                !requestSelectionController.IsReady || requestSelectionController.IsTransitionRequested)
            {
                return true;
            }

            if (quitController != null && quitController.IsQuitPopupOpen)
            {
                return true;
            }

            if (blockingPopupRoots != null)
            {
                for (int i = 0; i < blockingPopupRoots.Length; i++)
                {
                    GameObject popupRoot = blockingPopupRoots[i];
                    if (popupRoot != null && popupRoot.activeInHierarchy)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
