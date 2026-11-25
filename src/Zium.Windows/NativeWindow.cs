using System;
using System.Runtime.InteropServices;
using Zium.Core;

namespace Zium.Windows;

internal static class NativeWindow
{
    internal const int HotkeyMessage = 0x0312;
    internal const int HotkeyId = 41;
    internal static readonly int RestoreMessage = RegisterWindowMessage("Zium.Desktop.Restore");

    internal static void BroadcastRestore() => PostMessage(new IntPtr(0xffff), RestoreMessage, IntPtr.Zero, IntPtr.Zero);

    internal static bool RegisterToggle(IntPtr window) => RegisterHotKey(window, HotkeyId, 0x4003, 0x20);
    internal static void UnregisterToggle(IntPtr window) => UnregisterHotKey(window, HotkeyId);

    internal static ScreenPoint Cursor()
    {
        GetCursorPos(out var point);
        return new(point.X, point.Y);
    }

    internal static (ScreenPoint Origin, ScreenSize Size) Bounds(IntPtr window)
    {
        GetWindowRect(window, out var rectangle);
        return (new(rectangle.Left, rectangle.Top), new(rectangle.Right - rectangle.Left, rectangle.Bottom - rectangle.Top));
    }

    internal static void Move(IntPtr window, ScreenPoint origin) =>
        SetWindowPos(window, IntPtr.Zero, (int)Math.Round(origin.X), (int)Math.Round(origin.Y), 0, 0, 0x0215);

    internal static double Scale(IntPtr window) => Math.Max(96, GetDpiForWindow(window)) / 96d;

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr window, out Rectangle rectangle);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr window, int id);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int RegisterWindowMessage(string name);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rectangle { public int Left, Top, Right, Bottom; }
}
