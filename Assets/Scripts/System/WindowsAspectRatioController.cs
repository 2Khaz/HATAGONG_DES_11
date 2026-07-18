using System;
using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;

namespace HATAGONG.Systems
{
    [DisallowMultipleComponent]
    public sealed class WindowsAspectRatioController : MonoBehaviour
    {
        private static WindowsAspectRatioController instance;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private const int PreferredWidth = 540;
        private const int PreferredHeight = 960;
        private const int MinimumWidth = 360;
        private const int MinimumHeight = 640;
        private const float ResizeDebounceSeconds = 0.15f;
        private const int AspectPixelTolerance = 2;
        private const uint MonitorDefaultToNearest = 2;
        private const uint SwpNoSize = 0x0001;
        private const uint SwpNoZOrder = 0x0004;
        private const uint SwpNoActivate = 0x0010;

        private IntPtr windowHandle;
        private int stableWidth;
        private int stableHeight;
        private int observedWidth;
        private int observedHeight;
        private float lastResizeTime;
        private bool resizePending;
        private bool widthIsPrimary;
        private bool applyingResolution;
#endif

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private IEnumerator Start()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            yield return null;
            yield return new WaitForEndOfFrame();

            windowHandle = FindPlayerWindow();
            for (int frame = 0; windowHandle == IntPtr.Zero && frame < 60; frame++)
            {
                yield return null;
                windowHandle = FindPlayerWindow();
            }

            Vector2Int initialSize = CalculateInitialClientSize();
            yield return ApplyClientSize(initialSize.x, initialSize.y, true);
#else
            yield break;
#endif
        }

        private void Update()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (applyingResolution)
            {
                return;
            }

            if ((windowHandle == IntPtr.Zero || !IsWindow(windowHandle)) &&
                (windowHandle = FindPlayerWindow()) == IntPtr.Zero)
            {
                return;
            }

            if (!TryGetClientSize(windowHandle, out int currentWidth, out int currentHeight))
            {
                return;
            }

            if (currentWidth != observedWidth || currentHeight != observedHeight)
            {
                if (!resizePending)
                {
                    stableWidth = observedWidth;
                    stableHeight = observedHeight;
                }

                resizePending = true;
                observedWidth = currentWidth;
                observedHeight = currentHeight;
                widthIsPrimary = Mathf.Abs(currentWidth - stableWidth) >= Mathf.Abs(currentHeight - stableHeight);
                lastResizeTime = Time.realtimeSinceStartup;
                return;
            }

            if (!resizePending || Time.realtimeSinceStartup - lastResizeTime < ResizeDebounceSeconds)
            {
                return;
            }

            resizePending = false;
            Vector2Int corrected = CalculateCorrectedClientSize(currentWidth, currentHeight, widthIsPrimary);
            if (Mathf.Abs(corrected.x - currentWidth) <= AspectPixelTolerance &&
                Mathf.Abs(corrected.y - currentHeight) <= AspectPixelTolerance)
            {
                stableWidth = currentWidth;
                stableHeight = currentHeight;
                KeepWindowInsideWorkArea(false);
                return;
            }

            StartCoroutine(ApplyClientSize(corrected.x, corrected.y, false));
#endif
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private IEnumerator ApplyClientSize(int width, int height, bool centerWindow)
        {
            applyingResolution = true;
            resizePending = false;

            if (windowHandle == IntPtr.Zero || !IsWindow(windowHandle))
            {
                windowHandle = FindPlayerWindow();
            }

            if (windowHandle != IntPtr.Zero &&
                GetWindowRect(windowHandle, out NativeRect windowRect) &&
                GetClientRect(windowHandle, out NativeRect clientRect))
            {
                int frameWidth = Mathf.Max(0, windowRect.Width - clientRect.Width);
                int frameHeight = Mathf.Max(0, windowRect.Height - clientRect.Height);
                if (Mathf.Abs(clientRect.Width - width) > AspectPixelTolerance ||
                    Mathf.Abs(clientRect.Height - height) > AspectPixelTolerance)
                {
                    SetWindowPos(
                        windowHandle,
                        IntPtr.Zero,
                        windowRect.Left,
                        windowRect.Top,
                        width + frameWidth,
                        height + frameHeight,
                        SwpNoZOrder | SwpNoActivate);
                }
            }

            yield return null;
            yield return null;

            KeepWindowInsideWorkArea(centerWindow);
            if (!TryGetClientSize(windowHandle, out observedWidth, out observedHeight))
            {
                observedWidth = width;
                observedHeight = height;
            }

            stableWidth = observedWidth;
            stableHeight = observedHeight;
            applyingResolution = false;
        }

        private Vector2Int CalculateInitialClientSize()
        {
            if (!TryGetAvailableClientSize(out int availableWidth, out int availableHeight))
            {
                return new Vector2Int(PreferredWidth, PreferredHeight);
            }

            return FitWithinLimits(PreferredWidth, PreferredHeight, availableWidth, availableHeight);
        }

        private Vector2Int CalculateCorrectedClientSize(int width, int height, bool useWidth)
        {
            int availableWidth = int.MaxValue;
            int availableHeight = int.MaxValue;
            TryGetAvailableClientSize(out availableWidth, out availableHeight);

            int targetWidth;
            int targetHeight;
            if (useWidth)
            {
                targetWidth = RoundToEven(width);
                targetHeight = RoundToEven(targetWidth * 16f / 9f);
            }
            else
            {
                targetHeight = RoundToEven(height);
                targetWidth = RoundToEven(targetHeight * 9f / 16f);
            }

            return FitWithinLimits(targetWidth, targetHeight, availableWidth, availableHeight);
        }

        private static Vector2Int FitWithinLimits(int width, int height, int availableWidth, int availableHeight)
        {
            int safeAvailableWidth = Mathf.Max(2, availableWidth);
            int safeAvailableHeight = Mathf.Max(2, availableHeight);
            float fitScale = Mathf.Min(1f, Mathf.Min(
                safeAvailableWidth / (float)Mathf.Max(1, width),
                safeAvailableHeight / (float)Mathf.Max(1, height)));

            int fittedWidth = RoundToEven(width * fitScale);
            int fittedHeight = RoundToEven(fittedWidth * 16f / 9f);
            if (fittedHeight > safeAvailableHeight)
            {
                fittedHeight = RoundToEven(safeAvailableHeight);
                fittedWidth = RoundToEven(fittedHeight * 9f / 16f);
            }

            if (safeAvailableWidth >= MinimumWidth && safeAvailableHeight >= MinimumHeight)
            {
                if (fittedWidth < MinimumWidth || fittedHeight < MinimumHeight)
                {
                    fittedWidth = MinimumWidth;
                    fittedHeight = MinimumHeight;
                }
            }

            fittedWidth = Mathf.Min(fittedWidth, safeAvailableWidth);
            fittedHeight = Mathf.Min(fittedHeight, safeAvailableHeight);
            return new Vector2Int(Mathf.Max(2, fittedWidth), Mathf.Max(2, fittedHeight));
        }

        private bool TryGetAvailableClientSize(out int width, out int height)
        {
            width = PreferredWidth;
            height = PreferredHeight;
            if (windowHandle == IntPtr.Zero || !TryGetWorkArea(windowHandle, out NativeRect workArea))
            {
                return false;
            }

            int frameWidth = 0;
            int frameHeight = 0;
            if (GetWindowRect(windowHandle, out NativeRect windowRect) && GetClientRect(windowHandle, out NativeRect clientRect))
            {
                frameWidth = Mathf.Max(0, windowRect.Width - clientRect.Width);
                frameHeight = Mathf.Max(0, windowRect.Height - clientRect.Height);
            }

            width = Mathf.Max(2, workArea.Width - frameWidth);
            height = Mathf.Max(2, workArea.Height - frameHeight);
            return true;
        }

        private static bool TryGetClientSize(IntPtr window, out int width, out int height)
        {
            if (window != IntPtr.Zero && GetClientRect(window, out NativeRect clientRect))
            {
                width = clientRect.Width;
                height = clientRect.Height;
                return width > 0 && height > 0;
            }

            width = 0;
            height = 0;
            return false;
        }

        private void KeepWindowInsideWorkArea(bool centerWindow)
        {
            if (windowHandle == IntPtr.Zero ||
                !TryGetWorkArea(windowHandle, out NativeRect workArea) ||
                !GetWindowRect(windowHandle, out NativeRect windowRect))
            {
                return;
            }

            int x = centerWindow
                ? workArea.Left + (workArea.Width - windowRect.Width) / 2
                : Mathf.Clamp(windowRect.Left, workArea.Left, Mathf.Max(workArea.Left, workArea.Right - windowRect.Width));
            int y = centerWindow
                ? workArea.Top + (workArea.Height - windowRect.Height) / 2
                : Mathf.Clamp(windowRect.Top, workArea.Top, Mathf.Max(workArea.Top, workArea.Bottom - windowRect.Height));

            SetWindowPos(windowHandle, IntPtr.Zero, x, y, 0, 0, SwpNoSize | SwpNoZOrder | SwpNoActivate);
        }

        private static int RoundToEven(float value)
        {
            int rounded = Mathf.RoundToInt(value);
            return (rounded & 1) == 0 ? rounded : rounded + 1;
        }

        private static IntPtr FindPlayerWindow()
        {
            int processId = Process.GetCurrentProcess().Id;
            IntPtr result = IntPtr.Zero;
            long largestArea = 0L;
            EnumWindows((window, _) =>
            {
                GetWindowThreadProcessId(window, out uint windowProcessId);
                if (windowProcessId == processId && IsWindowVisible(window) &&
                    GetWindowRect(window, out NativeRect rect))
                {
                    long area = (long)Mathf.Max(0, rect.Width) * Mathf.Max(0, rect.Height);
                    if (area > largestArea)
                    {
                        largestArea = area;
                        result = window;
                    }
                }

                return true;
            }, IntPtr.Zero);
            return result;
        }

        private static bool TryGetWorkArea(IntPtr window, out NativeRect workArea)
        {
            IntPtr monitor = MonitorFromWindow(window, MonitorDefaultToNearest);
            MonitorInfo info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (monitor != IntPtr.Zero && GetMonitorInfo(monitor, ref info))
            {
                workArea = info.WorkArea;
                return true;
            }

            workArea = default;
            return false;
        }

        private delegate bool EnumWindowsCallback(IntPtr window, IntPtr parameter);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr window);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetClientRect(IntPtr window, out NativeRect rect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(
            IntPtr window,
            IntPtr insertAfter,
            int x,
            int y,
            int width,
            int height,
            uint flags);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
            public int Width => Right - Left;
            public int Height => Bottom - Top;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MonitorInfo
        {
            public int Size;
            public NativeRect Monitor;
            public NativeRect WorkArea;
            public uint Flags;
        }
#endif
    }
}
