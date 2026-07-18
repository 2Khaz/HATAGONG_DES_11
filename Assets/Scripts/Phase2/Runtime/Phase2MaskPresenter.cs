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
        private static readonly int DustTextureId = Shader.PropertyToID("_DustTex");

        [SerializeField] private Image paintedLayer;
        [SerializeField] private RawImage cover;
        [SerializeField] private Material materialTemplate;
        [SerializeField] private Sprite dustSprite;
        [SerializeField] private Sprite paintSprite;
        private Material _runtimeMaterial;
        private float _completionDuration;
        private float _completionElapsed;
        private Action _completionCallback;
        private RawImage _chemicalOverlay;
        private Texture2D _chemicalTexture;
        private Color32[] _chemicalPixels;
        private int _chemicalSeed;

        public bool IsBound => cover && cover.texture && _runtimeMaterial;
        public Texture BoundTexture => cover ? cover.texture : null;
        public bool HasRuntimeMaterial => _runtimeMaterial;
        public float CompletionFill { get; private set; }
        public bool IsCompleting { get; private set; }

        public bool Bind(RenderTexture maskTexture)
        {
            if (!ConfigureFloorLayers() || !materialTemplate || !maskTexture) return false;
            CancelCompletion();
            ReleaseRuntimeMaterial();
            _runtimeMaterial = new Material(materialTemplate)
            {
                name = materialTemplate.name + " (Runtime)",
                hideFlags = HideFlags.DontSave
            };
            _runtimeMaterial.SetFloat(MaskBoundId, 1f);
            _runtimeMaterial.SetTexture(DustTextureId, dustSprite.texture);
            SetCompletionFill(0f);
            cover.texture = maskTexture;
            cover.material = _runtimeMaterial;
            return true;
        }

        public bool RefreshBoundMask(RenderTexture maskTexture)
        {
            if (!cover || !_runtimeMaterial || !maskTexture) return false;
            cover.texture = maskTexture;
            cover.SetAllDirty();
            return ReferenceEquals(cover.texture, maskTexture);
        }

        public bool BindChemicalOverlay(Phase2PaintGrid grid, int permanentSeed)
        {
            ReleaseChemicalOverlay();
            if (grid == null || grid.ChemicalCellCount <= 0 || !cover) return grid != null && grid.ChemicalCellCount == 0;

            GameObject overlayObject = new GameObject("ChemicalOverlayLayer", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            overlayObject.layer = cover.gameObject.layer;
            RectTransform rect = overlayObject.GetComponent<RectTransform>();
            rect.SetParent(cover.rectTransform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            _chemicalOverlay = overlayObject.GetComponent<RawImage>();
            _chemicalOverlay.raycastTarget = false;
            _chemicalOverlay.color = Color.white;
            _chemicalSeed = permanentSeed;
            _chemicalTexture = new Texture2D(128, 128, TextureFormat.RGBA32, false, false)
            {
                name = "Phase2 Chemical Overlay (Runtime)",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            _chemicalPixels = new Color32[grid.TotalCellCount];
            _chemicalOverlay.texture = _chemicalTexture;
            return RefreshChemicalOverlay(grid);
        }

        public bool RefreshChemicalOverlay(Phase2PaintGrid grid)
        {
            if (grid == null || grid.ChemicalCellCount == 0) return true;
            if (!_chemicalOverlay || !_chemicalTexture || _chemicalPixels == null || _chemicalPixels.Length != grid.TotalCellCount) return false;
            for (int index = 0; index < _chemicalPixels.Length; index++)
            {
                if (grid.RequiredCoats(index) != 2 || grid.IsPainted(index))
                {
                    _chemicalPixels[index] = new Color32(0, 0, 0, 0);
                    continue;
                }

                if (grid.CoatCount(index) == 1)
                {
                    _chemicalPixels[index] = new Color32(29, 31, 28, 245);
                    continue;
                }

                uint noise = ChemicalNoise(_chemicalSeed, index);
                byte red = (byte)(62 + noise % 43);
                byte green = (byte)(54 + (noise >> 8) % 38);
                byte blue = (byte)(28 + (noise >> 16) % 28);
                byte alpha = (byte)(215 + (noise >> 24) % 36);
                _chemicalPixels[index] = new Color32(red, green, blue, alpha);
            }
            _chemicalTexture.SetPixels32(_chemicalPixels);
            _chemicalTexture.Apply(false, false);
            _chemicalOverlay.SetAllDirty();
            return true;
        }

        public void ReleaseBinding()
        {
            CancelCompletion();
            ReleaseChemicalOverlay();
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

        private bool ConfigureFloorLayers()
        {
            if (!paintedLayer || !cover || !paintSprite || !dustSprite) return false;
            paintedLayer.sprite = paintSprite;
            paintedLayer.color = Color.white;
            paintedLayer.preserveAspect = false;
            cover.color = Color.white;
            return true;
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

        private void ReleaseChemicalOverlay()
        {
            if (_chemicalOverlay)
            {
                _chemicalOverlay.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(_chemicalOverlay.gameObject);
                else DestroyImmediate(_chemicalOverlay.gameObject);
            }
            if (_chemicalTexture)
            {
                if (Application.isPlaying) Destroy(_chemicalTexture);
                else DestroyImmediate(_chemicalTexture);
            }
            _chemicalOverlay = null;
            _chemicalTexture = null;
            _chemicalPixels = null;
        }

        private static uint ChemicalNoise(int seed, int index)
        {
            unchecked
            {
                uint value = (uint)(seed ^ (index * 374761393));
                value = (value ^ (value >> 13)) * 1274126177u;
                return value ^ (value >> 16);
            }
        }
    }
}
