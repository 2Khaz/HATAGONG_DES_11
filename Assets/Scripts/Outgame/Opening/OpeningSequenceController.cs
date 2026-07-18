using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HATAGONG.Outgame.Opening
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class OpeningSequenceController : MonoBehaviour
    {
        private const float FadeInDuration = 1.2f;
        private const float HoldDuration = 0.8f;
        private const float FadeOutDuration = 0.8f;
        private const float BlackHoldDuration = 0.2f;
        private const float MaximumFadeInDelta = 1f / 30f;

        [SerializeField] private CanvasGroup openingCanvasGroup;
        [SerializeField] private Camera openingCamera;
        [SerializeField] private string lobbySceneName = "OUTGAME_LOBBY";

        private AsyncOperation lobbyLoadOperation;
        private bool sequenceStarted;
        private bool loadRequested;
        private bool activationRequested;

        private void Awake()
        {
            if (openingCanvasGroup == null)
            {
                openingCanvasGroup = GetComponent<CanvasGroup>();
            }

            openingCanvasGroup.alpha = 0f;
            openingCanvasGroup.interactable = false;
            openingCanvasGroup.blocksRaycasts = false;

            if (openingCamera == null)
            {
                openingCamera = Camera.main;
            }

            if (openingCamera != null)
            {
                openingCamera.clearFlags = CameraClearFlags.SolidColor;
                openingCamera.backgroundColor = Color.black;
            }
        }

        private void Start()
        {
            if (sequenceStarted)
            {
                return;
            }

            sequenceStarted = true;
            StartCoroutine(PlayOpening());
        }

        private IEnumerator PlayOpening()
        {
            // Awake already made the canvas transparent. Waiting one rendered frame here
            // guarantees that no opening graphic can flash before the fade begins.
            yield return null;

            yield return FadeInCanvas();
            RequestLobbyLoad();
            yield return WaitUnscaled(HoldDuration);
            yield return FadeCanvas(1f, 0f, FadeOutDuration);

            openingCanvasGroup.alpha = 0f;
            yield return WaitUnscaled(BlackHoldDuration);

            if (lobbyLoadOperation == null)
            {
                yield break;
            }

            while (lobbyLoadOperation.progress < 0.9f)
            {
                yield return null;
            }

            if (!activationRequested)
            {
                activationRequested = true;
                lobbyLoadOperation.allowSceneActivation = true;
            }
        }

        private IEnumerator FadeInCanvas()
        {
            float elapsed = 0f;
            openingCanvasGroup.alpha = 0f;

            while (elapsed < FadeInDuration)
            {
                float frameDelta = Mathf.Min(Time.unscaledDeltaTime, MaximumFadeInDelta);
                elapsed += frameDelta;
                float t = Mathf.Clamp01(elapsed / FadeInDuration);
                openingCanvasGroup.alpha = Mathf.SmoothStep(0f, 1f, t);
                yield return null;
            }

            openingCanvasGroup.alpha = 1f;
        }

        private void RequestLobbyLoad()
        {
            if (loadRequested)
            {
                return;
            }

            loadRequested = true;
            lobbyLoadOperation = SceneManager.LoadSceneAsync(lobbySceneName, LoadSceneMode.Single);
            if (lobbyLoadOperation == null)
            {
                Debug.LogError($"[Opening] Failed to start loading Scene '{lobbySceneName}'.", this);
                return;
            }

            lobbyLoadOperation.allowSceneActivation = false;
        }

        private IEnumerator FadeCanvas(float from, float to, float duration)
        {
            float elapsed = 0f;
            openingCanvasGroup.alpha = from;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                openingCanvasGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            openingCanvasGroup.alpha = to;
        }

        private static IEnumerator WaitUnscaled(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }
    }
}
