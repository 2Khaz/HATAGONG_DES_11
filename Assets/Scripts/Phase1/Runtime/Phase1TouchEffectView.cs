using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace HATAGONG.Phase1
{
    public sealed class Phase1TouchEffectView : MonoBehaviour
    {
        private Image image;
        private Coroutine playback;
        private Color playbackColor = Color.white;

        public bool IsPlaying { get; private set; }
        public long StartedOrder { get; private set; }

        public void Initialize(Image targetImage)
        {
            image = targetImage;
            image.raycastTarget = false;
            image.preserveAspect = false;
            gameObject.SetActive(false);
        }

        public void Play(Vector2 localPosition, Sprite[] frames, long startedOrder, Color color)
        {
            StopAndHide();
            StartedOrder = startedOrder;
            playbackColor = color;
            RectTransform rect = (RectTransform)transform;
            rect.anchoredPosition = localPosition;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            image.color = playbackColor;
            IsPlaying = true;
            gameObject.SetActive(true);
            playback = StartCoroutine(PlayRoutine(frames));
        }

        public void StopAndHide()
        {
            if (playback != null) StopCoroutine(playback);
            playback = null;
            IsPlaying = false;
            playbackColor = Color.white;
            if (image) image.color = Color.white;
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        private IEnumerator PlayRoutine(Sprite[] frames)
        {
            SetFrame(frames[0]);
            yield return WaitUnscaled(0.05f);
            SetFrame(frames[1]);
            yield return WaitUnscaled(0.07f);
            SetFrame(frames[2]);
            yield return WaitUnscaled(0.10f);

            float elapsed = 0f;
            while (elapsed < 0.05f)
            {
                elapsed += Time.unscaledDeltaTime;
                Color color = image.color;
                color.a = 1f - Mathf.Clamp01(elapsed / 0.05f);
                image.color = color;
                yield return null;
            }

            playback = null;
            IsPlaying = false;
            playbackColor = Color.white;
            image.color = Color.white;
            gameObject.SetActive(false);
        }

        private void SetFrame(Sprite sprite)
        {
            image.sprite = sprite;
            image.color = playbackColor;
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
