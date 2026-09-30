using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ValheimHeadTracking
{
    internal sealed class StartupWindow : MonoBehaviour
    {
        private IntPtr _window;
        private Rect _previous;
        private float _stableSince;
        private float _started = -1f;

        private void Awake()
        {
            enabled = Application.platform == RuntimePlatform.WindowsPlayer;
        }

        private void Update()
        {
            float now = Time.realtimeSinceStartup;
            if (_started < 0f) _started = now;
            if (now - _started > 30f)
            {
                enabled = false;
                ValheimHeadTrackingPlugin.Log.LogWarning("window: no stable game window within 30 seconds");
                return;
            }

            IntPtr window;
            using (var process = Process.GetCurrentProcess()) window = process.MainWindowHandle;
            if (window == IntPtr.Zero || IsIconic(window)) return;
            if (!GetWindowRect(window, out Rect rect)) throw new Win32Exception();
            if (window != _window || !rect.Equals(_previous))
            {
                _window = window;
                _previous = rect;
                _stableSince = now;
                return;
            }

            // Unity applies startup display settings after the plugin's Awake.
            if (now - _started < 3f || now - _stableSince < 1f) return;
            enabled = false;
            if (Screen.fullScreen || IsZoomed(window) || (GetWindowLong(window, -16) & 0x00C00000) == 0)
            {
                ValheimHeadTrackingPlugin.Log.LogInfo("window: fullscreen, borderless or maximized; position unchanged");
                return;
            }

            var monitor = new MonitorInfo { Size = Marshal.SizeOf(typeof(MonitorInfo)) };
            if (!GetMonitorInfo(MonitorFromWindow(window, 2), ref monitor)) throw new Win32Exception();
            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;
            int x = monitor.Work.Left + Math.Max(0, (monitor.Work.Right - monitor.Work.Left - width) / 2);
            int y = monitor.Work.Top + Math.Max(0, (monitor.Work.Bottom - monitor.Work.Top - height) / 2);
            if (!SetWindowPos(window, IntPtr.Zero, x, y, 0, 0, 0x0015)) throw new Win32Exception();
            if (!GetWindowRect(window, out Rect actual)) throw new Win32Exception();
            ValheimHeadTrackingPlugin.Log.LogInfo(
                $"window: requested ({x}, {y}), actual ({actual.Left}, {actual.Top}), size {width}x{height}, previous ({rect.Left}, {rect.Top})");
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public int Left, Top, Right, Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MonitorInfo
        {
            public int Size;
            public Rect Monitor, Work;
            public uint Flags;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr window, out Rect rect);
        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")]
        private static extern bool IsZoomed(IntPtr window);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        private static extern int GetWindowLong(IntPtr window, int index);
        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
        [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", SetLastError = true)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    }
}
