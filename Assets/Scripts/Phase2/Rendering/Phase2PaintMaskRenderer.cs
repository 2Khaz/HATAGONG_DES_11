using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace HATAGONG.Phase2.Rendering
{
    public sealed class Phase2PaintMaskRenderer : IDisposable
    {
        public const int DefaultResolution = 1024;
        public const string BrushShaderName = "HATAGONG/Phase2/Paint Mask Brush";
        public const string CompositeShaderName = "HATAGONG/Phase2/Paint Mask Composite";
        public const string DisplayShaderName = "HATAGONG/Phase2/Paint Mask Display";

        public static readonly int MainTextureId = Shader.PropertyToID("_MainTex");
        public static readonly int FrameStampTextureId = Shader.PropertyToID("_Phase2FrameStampTex");
        public static readonly int RenderTargetYSignId = Shader.PropertyToID("_Phase2RenderTargetYSign");
        public static readonly int UnpaintedColorId = Shader.PropertyToID("_UnpaintedColor");
        public static readonly int PaintedColorId = Shader.PropertyToID("_PaintedColor");

        private static readonly List<Phase2PaintMaskRenderer> LiveInstances = new List<Phase2PaintMaskRenderer>();

        private readonly List<Vector3> _vertices = new List<Vector3>(256);
        private readonly List<Vector2> _brushCoordinates = new List<Vector2>(256);
        private readonly List<int> _indices = new List<int>(384);
        private readonly Phase2PaintStamp[] _singleStamp = new Phase2PaintStamp[1];

        private RenderTexture _mask;
        private RenderTexture _scratch;
        private RenderTexture _frameStamp;
        private Material _brushMaterial;
        private Material _compositeMaterial;
        private Mesh _batchMesh;
        private CommandBuffer _commandBuffer;
        private int _resolution;
        private GraphicsFormat _graphicsFormat;

        public RenderTexture MaskTexture => _mask;
        public int Resolution => _resolution;
        public GraphicsFormat SelectedFormat => _graphicsFormat;
        public bool IsInitialized => ResourcesAreValid();
        public int OwnedRenderTextureCount => IsInitialized ? 3 : 0;

        public static bool SupportsR8Mask()
        {
            return SupportsMaskFormat(GraphicsFormat.R8_UNorm);
        }

        public static GraphicsFormat SelectMaskFormat()
        {
            return SupportsR8Mask() ? GraphicsFormat.R8_UNorm : GraphicsFormat.R8G8B8A8_UNorm;
        }

        public static bool SupportsMaskFormat(GraphicsFormat format)
        {
            return SystemInfo.IsFormatSupported(format, GraphicsFormatUsage.Render) &&
                   SystemInfo.IsFormatSupported(format, GraphicsFormatUsage.Sample) &&
                   SystemInfo.IsFormatSupported(format, GraphicsFormatUsage.Blend);
        }

        public bool Initialize(int resolution = DefaultResolution)
        {
            return Initialize(Shader.Find(BrushShaderName), Shader.Find(CompositeShaderName), resolution);
        }

        public bool Initialize(Shader brushShader, Shader compositeShader, int resolution = DefaultResolution)
        {
            if (resolution <= 0 || !brushShader || !compositeShader || !brushShader.isSupported || !compositeShader.isSupported)
            {
                return false;
            }

            GraphicsFormat selectedFormat = SelectMaskFormat();
            if (!SupportsMaskFormat(selectedFormat))
            {
                return false;
            }
            if (ResourcesAreValid() && _resolution == resolution && _graphicsFormat == selectedFormat)
            {
                return true;
            }

            Release();
            _resolution = resolution;
            _graphicsFormat = selectedFormat;
            _mask = CreateRenderTexture("Phase2_PaintMask_A");
            _scratch = CreateRenderTexture("Phase2_PaintMask_B");
            _frameStamp = CreateRenderTexture("Phase2_FrameStamp");

            if (!_mask || !_scratch || !_frameStamp || !_mask.IsCreated() || !_scratch.IsCreated() || !_frameStamp.IsCreated())
            {
                Release();
                return false;
            }

            _brushMaterial = new Material(brushShader) { name = "Phase2_PaintMaskBrush_Runtime", hideFlags = HideFlags.HideAndDontSave };
            _brushMaterial.SetFloat(RenderTargetYSignId, SystemInfo.graphicsUVStartsAtTop ? -1f : 1f);
            _compositeMaterial = new Material(compositeShader) { name = "Phase2_PaintMaskComposite_Runtime", hideFlags = HideFlags.HideAndDontSave };
            _batchMesh = new Mesh { name = "Phase2_FrameStampBatch_Runtime", hideFlags = HideFlags.HideAndDontSave, indexFormat = IndexFormat.UInt32 };
            _batchMesh.MarkDynamic();
            _commandBuffer = new CommandBuffer { name = "Phase2 Paint Mask Frame Batch" };
            LiveInstances.Add(this);
            ResetMask();
            return true;
        }

        public Phase2PaintBatchResult ApplyStamp(Phase2PaintStamp stamp)
        {
            _singleStamp[0] = stamp;
            return ApplyStampBatch(_singleStamp);
        }

        public Phase2PaintBatchResult ApplyStampBatch(IReadOnlyList<Phase2PaintStamp> stamps)
        {
            int inputCount = stamps?.Count ?? 0;
            if (!IsInitialized || inputCount == 0)
            {
                return new Phase2PaintBatchResult(inputCount, 0, inputCount, false);
            }

            _vertices.Clear();
            _brushCoordinates.Clear();
            _indices.Clear();

            int acceptedCount = 0;
            for (int i = 0; i < inputCount; i++)
            {
                Phase2PaintStamp stamp = stamps[i];
                if (!IsValidAndIntersectsBoard(stamp))
                {
                    continue;
                }

                AppendStampQuad(stamp);
                acceptedCount++;
            }

            int rejectedCount = inputCount - acceptedCount;
            if (acceptedCount == 0)
            {
                return new Phase2PaintBatchResult(inputCount, 0, rejectedCount, false);
            }

            _batchMesh.Clear(false);
            _batchMesh.SetVertices(_vertices);
            _batchMesh.SetUVs(0, _brushCoordinates);
            _batchMesh.SetTriangles(_indices, 0, false);
            _batchMesh.UploadMeshData(false);

            _commandBuffer.Clear();
            _commandBuffer.SetRenderTarget(_frameStamp);
            _commandBuffer.ClearRenderTarget(false, true, Color.clear);
            _commandBuffer.DrawMesh(_batchMesh, Matrix4x4.identity, _brushMaterial, 0, 0);
            _compositeMaterial.SetTexture(FrameStampTextureId, _frameStamp);
            _commandBuffer.Blit(_mask, _scratch, _compositeMaterial, 0);
            ExecuteAndRestoreActiveRenderTexture();

            RenderTexture previousMask = _mask;
            _mask = _scratch;
            _scratch = previousMask;
            return new Phase2PaintBatchResult(inputCount, acceptedCount, rejectedCount, true);
        }

        public void ResetMask()
        {
            if (!ResourcesAreValid())
            {
                return;
            }

            _commandBuffer.Clear();
            ClearRenderTexture(_mask);
            ClearRenderTexture(_scratch);
            ClearRenderTexture(_frameStamp);
            ExecuteAndRestoreActiveRenderTexture();
        }

        public void Release()
        {
            LiveInstances.Remove(this);
            _commandBuffer?.Release();
            _commandBuffer = null;
            DestroyOwnedObject(_batchMesh);
            DestroyOwnedObject(_brushMaterial);
            DestroyOwnedObject(_compositeMaterial);
            ReleaseRenderTexture(ref _mask);
            ReleaseRenderTexture(ref _scratch);
            ReleaseRenderTexture(ref _frameStamp);
            _batchMesh = null;
            _brushMaterial = null;
            _compositeMaterial = null;
            _resolution = 0;
            _graphicsFormat = GraphicsFormat.None;
            _vertices.Clear();
            _brushCoordinates.Clear();
            _indices.Clear();
        }

        public void Dispose()
        {
            Release();
        }

        public static void ReleaseAllLiveResources()
        {
            while (LiveInstances.Count > 0)
            {
                LiveInstances[LiveInstances.Count - 1].Release();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticResources()
        {
            ReleaseAllLiveResources();
            LiveInstances.Clear();
        }

        private RenderTexture CreateRenderTexture(string textureName)
        {
            var descriptor = new RenderTextureDescriptor(_resolution, _resolution, _graphicsFormat, 0)
            {
                graphicsFormat = _graphicsFormat,
                depthBufferBits = 0,
                msaaSamples = 1,
                volumeDepth = 1,
                dimension = TextureDimension.Tex2D,
                sRGB = false,
                useMipMap = false,
                autoGenerateMips = false,
                enableRandomWrite = false
            };
            var texture = new RenderTexture(descriptor)
            {
                name = textureName,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.Create();
            return texture;
        }

        private bool ResourcesAreValid()
        {
            return _mask && _scratch && _frameStamp && _mask.IsCreated() && _scratch.IsCreated() && _frameStamp.IsCreated() &&
                   _brushMaterial && _compositeMaterial && _batchMesh && _commandBuffer != null;
        }

        private void ClearRenderTexture(RenderTexture texture)
        {
            _commandBuffer.SetRenderTarget(texture);
            _commandBuffer.ClearRenderTarget(false, true, Color.clear);
        }

        private void ExecuteAndRestoreActiveRenderTexture()
        {
            RenderTexture previous = RenderTexture.active;
            try
            {
                Graphics.ExecuteCommandBuffer(_commandBuffer);
            }
            finally
            {
                RenderTexture.active = previous;
            }
        }

        private static bool IsValidAndIntersectsBoard(Phase2PaintStamp stamp)
        {
            float radius = stamp.RadiusRatio;
            if (!IsFinite(stamp.CenterU) || !IsFinite(stamp.CenterV) || !IsFinite(radius) || radius <= 0f)
            {
                return false;
            }

            double centerU = stamp.CenterU;
            double centerV = stamp.CenterV;
            double nearestU = Math.Max(0d, Math.Min(1d, centerU));
            double nearestV = Math.Max(0d, Math.Min(1d, centerV));
            double deltaU = centerU - nearestU;
            double deltaV = centerV - nearestV;
            double radiusDouble = radius;
            return deltaU * deltaU + deltaV * deltaV < radiusDouble * radiusDouble;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private void AppendStampQuad(Phase2PaintStamp stamp)
        {
            int vertexStart = _vertices.Count;
            float minX = (stamp.CenterU - stamp.RadiusRatio) * 2f - 1f;
            float maxX = (stamp.CenterU + stamp.RadiusRatio) * 2f - 1f;
            float minY = (stamp.CenterV - stamp.RadiusRatio) * 2f - 1f;
            float maxY = (stamp.CenterV + stamp.RadiusRatio) * 2f - 1f;

            _vertices.Add(new Vector3(minX, minY, 0f));
            _vertices.Add(new Vector3(maxX, minY, 0f));
            _vertices.Add(new Vector3(maxX, maxY, 0f));
            _vertices.Add(new Vector3(minX, maxY, 0f));
            _brushCoordinates.Add(new Vector2(-1f, -1f));
            _brushCoordinates.Add(new Vector2(1f, -1f));
            _brushCoordinates.Add(new Vector2(1f, 1f));
            _brushCoordinates.Add(new Vector2(-1f, 1f));
            _indices.Add(vertexStart);
            _indices.Add(vertexStart + 1);
            _indices.Add(vertexStart + 2);
            _indices.Add(vertexStart);
            _indices.Add(vertexStart + 2);
            _indices.Add(vertexStart + 3);
        }

        private static void ReleaseRenderTexture(ref RenderTexture texture)
        {
            if (!texture)
            {
                texture = null;
                return;
            }

            if (texture.IsCreated())
            {
                texture.Release();
            }
            DestroyOwnedObject(texture);
            texture = null;
        }

        private static void DestroyOwnedObject(UnityEngine.Object ownedObject)
        {
            if (!ownedObject)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(ownedObject);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(ownedObject);
            }
        }
    }
}
