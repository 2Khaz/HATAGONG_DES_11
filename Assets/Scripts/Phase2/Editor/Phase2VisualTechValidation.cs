#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using HATAGONG.Phase2.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace HATAGONG.Phase2.Editor
{
    public static class Phase2VisualTechValidation
    {
        [MenuItem("Tools/HATAGONG/Phase2/Validate Visual Tech Core")]
        public static void Validate()
        {
            Debug.Log("[Phase2VisualTech][Test] validation started");
            int passed = 0;
            int total = 0;

            void Check(bool condition, string name)
            {
                total++;
                if (!condition)
                {
                    throw new InvalidOperationException("[Phase2VisualTech][Test] " + name);
                }
                passed++;
            }

            var renderer = new Phase2PaintMaskRenderer();
            Texture2D readback = null;
            try
            {
                Shader brushShader = Shader.Find(Phase2PaintMaskRenderer.BrushShaderName);
                Shader compositeShader = Shader.Find(Phase2PaintMaskRenderer.CompositeShaderName);
                Shader displayShader = Shader.Find(Phase2PaintMaskRenderer.DisplayShaderName);
                Check(brushShader && brushShader.isSupported, "brush shader available");
                Check(compositeShader && compositeShader.isSupported, "composite shader available");
                Check(displayShader && displayShader.isSupported, "display shader available");
                Check(renderer.Initialize(brushShader, compositeShader), "renderer initialize");
                Check(renderer.IsInitialized && renderer.OwnedRenderTextureCount == 3, "owned GPU resources initialized");
                Check(renderer.Resolution == 1024 && renderer.MaskTexture.width == 1024 && renderer.MaskTexture.height == 1024, "mask resolution 1024");

                GraphicsFormat expectedFormat = Phase2PaintMaskRenderer.SupportsR8Mask()
                    ? GraphicsFormat.R8_UNorm
                    : GraphicsFormat.R8G8B8A8_UNorm;
                Check(renderer.SelectedFormat == expectedFormat, "R8 format selection with RGBA8 fallback");
                Check(renderer.MaskTexture.graphicsFormat == expectedFormat, "render texture uses selected format");

                RenderTexture firstMask = renderer.MaskTexture;
                Check(renderer.Initialize(brushShader, compositeShader), "duplicate initialize safe");
                Check(renderer.MaskTexture == firstMask, "duplicate initialize reuses resources");

                readback = ReadMask(renderer.MaskTexture, readback);
                Check(MaximumRed(readback) == 0, "initial mask cleared to zero");

                var centerResult = renderer.ApplyStamp(new Phase2PaintStamp(0.5f, 0.5f, 0.2f));
                Check(centerResult.InputCount == 1 && centerResult.AcceptedCount == 1 && centerResult.RejectedCount == 0 && centerResult.GpuSubmitted, "central stamp accepted");
                readback = ReadMask(renderer.MaskTexture, readback);
                float centerValue = SampleRed(readback, 0.5f, 0.5f);
                float featherValue = SampleRed(readback, 0.69f, 0.5f);
                float outsideValue = SampleRed(readback, 0.75f, 0.5f);
                Check(centerValue >= 0.98f, "brush center fully painted");
                Check(featherValue > 0.02f && featherValue < 0.98f, "brush feather is partial");
                Check(outsideValue <= 0.01f, "outside brush remains zero");

                Color32[] beforeDuplicate = readback.GetPixels32();
                var duplicateResult = renderer.ApplyStamp(new Phase2PaintStamp(0.5f, 0.5f, 0.2f));
                Check(duplicateResult.AcceptedCount == 1 && duplicateResult.GpuSubmitted, "duplicate stamp submitted");
                readback = ReadMask(renderer.MaskTexture, readback);
                Color32[] afterDuplicate = readback.GetPixels32();
                Check(IsMonotonic(beforeDuplicate, afterDuplicate), "duplicate stamp never decreases mask");

                var edgeResult = renderer.ApplyStamp(new Phase2PaintStamp(0f, 0.5f, 0.12f));
                Check(edgeResult.AcceptedCount == 1 && edgeResult.GpuSubmitted, "edge stamp accepted");
                readback = ReadMask(renderer.MaskTexture, readback);
                Check(SampleRed(readback, 0f, 0.5f) >= 0.98f, "edge stamp clipped and painted");

                var cornerResult = renderer.ApplyStamp(new Phase2PaintStamp(0f, 0f, 0.12f));
                Check(cornerResult.AcceptedCount == 1 && cornerResult.GpuSubmitted, "corner stamp accepted");
                readback = ReadMask(renderer.MaskTexture, readback);
                float cornerValue = SampleRed(readback, 0f, 0f);
                float cornerInsideU = 2f / (readback.width - 1f);
                float cornerInsideV = 2f / (readback.height - 1f);
                float cornerInsideValue = SampleRed(readback, cornerInsideU, cornerInsideV);
                float cornerOutsideValue = SampleRed(readback, 0.09f, 0.09f);
                Debug.Log(
                    $"[Phase2VisualTech][Corner] center=(0,0), radius=0.12, accepted={cornerResult.AcceptedCount}, rejected={cornerResult.RejectedCount}, gpuSubmitted={cornerResult.GpuSubmitted}, " +
                    $"quadClip=(-1.24,-1.24)-(-0.76,-0.76), graphicsUVStartsAtTop={SystemInfo.graphicsUVStartsAtTop}, format={renderer.SelectedFormat}, channel=R, " +
                    $"cornerPixel=(0,0):{cornerValue:F4}, insidePixel=(2,2):{cornerInsideValue:F4}, outsidePixel=({PixelCoordinate(0.09f, readback.width)},{PixelCoordinate(0.09f, readback.height)}):{cornerOutsideValue:F4}, " +
                    $"lowerLeft5x5={FormatRedRegion(readback, 0, 0, 5)}, upperLeft5x5={FormatRedRegion(readback, 0, readback.height - 5, 5)}");
                Check(cornerValue >= 0.98f && cornerInsideValue >= 0.98f && cornerOutsideValue <= 0.01f, "corner stamp clipped and painted");

                var outsideResult = renderer.ApplyStamp(new Phase2PaintStamp(-0.5f, 0.5f, 0.1f));
                Check(outsideResult.InputCount == 1 && outsideResult.AcceptedCount == 0 && outsideResult.RejectedCount == 1 && !outsideResult.GpuSubmitted, "non-intersecting stamp rejected without GPU submission");

                renderer.ResetMask();
                readback = ReadMask(renderer.MaskTexture, readback);
                Color32[] beforeNearCornerMiss = readback.GetPixels32();
                var nearCornerMiss = renderer.ApplyStamp(new Phase2PaintStamp(-0.09f, -0.09f, 0.10f));
                Check(nearCornerMiss.AcceptedCount == 0 && nearCornerMiss.RejectedCount == 1 && !nearCornerMiss.GpuSubmitted, "near-corner circle miss rejected");
                readback = ReadMask(renderer.MaskTexture, readback);
                Check(AreEqual(beforeNearCornerMiss, readback.GetPixels32()), "near-corner circle miss preserves mask");

                var nearCornerHit = renderer.ApplyStamp(new Phase2PaintStamp(-0.05f, -0.05f, 0.10f));
                Check(nearCornerHit.AcceptedCount == 1 && nearCornerHit.RejectedCount == 0 && nearCornerHit.GpuSubmitted, "near-corner circle intersection accepted");
                readback = ReadMask(renderer.MaskTexture, readback);
                Check(SampleRed(readback, 0f, 0f) > 0f, "near-corner circle intersection paints board corner");

                renderer.ResetMask();
                var batch = new List<Phase2PaintStamp>
                {
                    new Phase2PaintStamp(0.2f, 0.25f, 0.08f),
                    new Phase2PaintStamp(0.5f, 0.5f, 0.08f),
                    new Phase2PaintStamp(0.8f, 0.75f, 0.08f)
                };
                var batchResult = renderer.ApplyStampBatch(batch);
                Check(batchResult.InputCount == 3 && batchResult.AcceptedCount == 3 && batchResult.RejectedCount == 0 && batchResult.GpuSubmitted, "frame batch accepts every valid stamp");
                readback = ReadMask(renderer.MaskTexture, readback);
                Check(SampleRed(readback, 0.2f, 0.25f) >= 0.98f &&
                      SampleRed(readback, 0.5f, 0.5f) >= 0.98f &&
                      SampleRed(readback, 0.8f, 0.75f) >= 0.98f, "frame batch contains every stamp");

                renderer.ResetMask();
                readback = ReadMask(renderer.MaskTexture, readback);
                Check(MaximumRed(readback) == 0, "reset clears mask to zero");

                RenderTexture maskBeforeResize = renderer.MaskTexture;
                Check(renderer.Initialize(brushShader, compositeShader, 512), "resolution change reinitializes");
                Check(renderer.Resolution == 512 && renderer.MaskTexture.width == 512 && renderer.MaskTexture.height == 512, "resolution change applied");
                Check(!maskBeforeResize || !maskBeforeResize.IsCreated(), "old render texture released on resize");

                RenderTexture maskBeforeRelease = renderer.MaskTexture;
                renderer.Release();
                Check(!renderer.IsInitialized && renderer.MaskTexture == null && renderer.OwnedRenderTextureCount == 0, "release clears owned references");
                Check(!maskBeforeRelease || !maskBeforeRelease.IsCreated(), "release destroys GPU resource");
                Debug.Log($"[Phase2VisualTech][Test] result={passed}/{total}, failures=0, format={expectedFormat}");
            }
            finally
            {
                if (readback)
                {
                    UnityEngine.Object.DestroyImmediate(readback);
                }
                renderer.Release();
            }
        }

        private static Texture2D ReadMask(RenderTexture source, Texture2D existing)
        {
            if (!existing || existing.width != source.width || existing.height != source.height)
            {
                if (existing)
                {
                    UnityEngine.Object.DestroyImmediate(existing);
                }
                existing = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true)
                {
                    name = "Phase2_ValidationReadback",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = source;
                existing.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0, false);
                existing.Apply(false, false);
                return existing;
            }
            finally
            {
                RenderTexture.active = previous;
            }
        }

        private static float SampleRed(Texture2D texture, float u, float v)
        {
            int x = PixelCoordinate(u, texture.width);
            int y = PixelCoordinate(v, texture.height);
            return texture.GetPixel(x, y).r;
        }

        private static int PixelCoordinate(float coordinate, int size)
        {
            return Mathf.Clamp(Mathf.RoundToInt(coordinate * (size - 1)), 0, size - 1);
        }

        private static string FormatRedRegion(Texture2D texture, int startX, int startY, int size)
        {
            var builder = new StringBuilder(size * size * 6);
            builder.Append('[');
            for (int y = 0; y < size; y++)
            {
                if (y > 0) builder.Append(';');
                for (int x = 0; x < size; x++)
                {
                    if (x > 0) builder.Append(',');
                    builder.Append(texture.GetPixel(startX + x, startY + y).r.ToString("F3"));
                }
            }
            builder.Append(']');
            return builder.ToString();
        }

        private static byte MaximumRed(Texture2D texture)
        {
            Color32[] pixels = texture.GetPixels32();
            byte maximum = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].r > maximum)
                {
                    maximum = pixels[i].r;
                }
            }
            return maximum;
        }

        private static bool IsMonotonic(Color32[] before, Color32[] after)
        {
            if (before.Length != after.Length)
            {
                return false;
            }

            for (int i = 0; i < before.Length; i++)
            {
                if (after[i].r < before[i].r)
                {
                    return false;
                }
            }
            return true;
        }

        private static bool AreEqual(Color32[] before, Color32[] after)
        {
            if (before.Length != after.Length)
            {
                return false;
            }

            for (int i = 0; i < before.Length; i++)
            {
                if (!before[i].Equals(after[i]))
                {
                    return false;
                }
            }
            return true;
        }
    }

    [InitializeOnLoad]
    internal static class Phase2PaintMaskEditorResourceCleanup
    {
        static Phase2PaintMaskEditorResourceCleanup()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Phase2PaintMaskRenderer.ReleaseAllLiveResources;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                Phase2PaintMaskRenderer.ReleaseAllLiveResources();
            }
        }
    }
}
#endif
