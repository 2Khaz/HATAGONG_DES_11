using System;
using UnityEngine;
using UnityEngine.UI;

namespace HATAGONG.Phase2
{
    public sealed class Phase2MaskPresenter : MonoBehaviour
    {
        public const string ShaderName = "HATAGONG/Phase2/UI Black Cover Mask";
        private static readonly int MaskBoundId = Shader.PropertyToID("_MaskBound");
        private static readonly int CompletionFillId = Shader.PropertyToID("_CompletionFill");

        [SerializeField] private RawImage cover;
        [SerializeField] private Material materialTemplate;
        private Material _runtimeMaterial;
        private float _completionDuration;
        private float _completionElapsed;
        private Action _completionCallback;

        public bool IsBound => cover && cover.texture && _runtimeMaterial;
        public Texture BoundTexture => cover ? cover.texture : null;
        public bool HasRuntimeMaterial => _runtimeMaterial;
        public float CompletionFill { get; private set; }
        public bool IsCompleting { get; private set; }

        public bool Bind(RenderTexture maskTexture)
        {
            if (!cover || !materialTemplate || !maskTexture) return false;
            CancelCompletion();
            ReleaseRuntimeMaterial();
            _runtimeMaterial = new Material(materialTemplate)
            {
                name = materialTemplate.name + " (Runtime)",
                hideFlags = HideFlags.DontSave
            };
            _runtimeMaterial.SetFloat(MaskBoundId, 1f);
            SetCompletionFill(0f);
            cover.texture = maskTexture;
            cover.material = _runtimeMaterial;
            return true;
        }

        public void ReleaseBinding()
        {
            CancelCompletion();
            if (cover)
            {
                cover.texture = null;
                cover.material = materialTemplate;
            }
            ReleaseRuntimeMaterial();
        }

        public static float CoverAlphaFromMask(float maskValue)
        {
            return CoverAlphaFromMask(maskValue, 0f);
        }

        public static float CoverAlphaFromMask(float maskValue, float completionFill)
        {
            return 1f - Mathf.Max(Mathf.Clamp01(maskValue), Mathf.Clamp01(completionFill));
        }

        internal bool BeginCompletion(float duration, Action completed)
        {
            if (!_runtimeMaterial || duration <= 0f || float.IsNaN(duration) || float.IsInfinity(duration)) return false;
            CancelCompletion();
            _completionDuration = duration;
            _completionElapsed = 0f;
            _completionCallback = completed;
            IsCompleting = true;
            SetCompletionFill(0f);
            return true;
        }

        internal void AdvanceCompletion(float unscaledDeltaTime)
        {
            if (!IsCompleting || unscaledDeltaTime <= 0f || float.IsNaN(unscaledDeltaTime) || float.IsInfinity(unscaledDeltaTime)) return;
            _completionElapsed = Mathf.Min(_completionDuration, _completionElapsed + unscaledDeltaTime);
            SetCompletionFill(_completionElapsed / _completionDuration);
            if (_completionElapsed < _completionDuration) return;

            IsCompleting = false;
            Action completed = _completionCallback;
            _completionCallback = null;
            completed?.Invoke();
        }

        private void Update()
        {
            AdvanceCompletion(Time.unscaledDeltaTime);
        }

        private void CancelCompletion()
        {
            IsCompleting = false;
            _completionDuration = 0f;
            _completionElapsed = 0f;
            _completionCallback = null;
        }

        private void SetCompletionFill(float value)
        {
            CompletionFill = Mathf.Clamp01(value);
            if (_runtimeMaterial) _runtimeMaterial.SetFloat(CompletionFillId, CompletionFill);
        }

        private void OnDisable()
        {
            ReleaseBinding();
        }

        private void OnDestroy()
        {
            ReleaseBinding();
        }

        private void ReleaseRuntimeMaterial()
        {
            if (!_runtimeMaterial) return;
            if (Application.isPlaying) Destroy(_runtimeMaterial);
            else DestroyImmediate(_runtimeMaterial);
            _runtimeMaterial = null;
        }
    }
}
