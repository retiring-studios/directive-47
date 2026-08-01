using System;
using System.Runtime.InteropServices;
using System.Threading;

using Rect = System.Windows.Rect;

namespace D47.Panel.Tests;

/// <summary>
/// Synthesized mouse and keyboard input, for the things UI Automation cannot
/// do.
///
/// <para>
/// Automation can invoke, expand, select and close, but it has no notion of a
/// right-click, and no way to dismiss a menu it did not open. A tray icon's
/// context menu needs both.
/// </para>
///
/// <para>
/// This is not the hardware tier. Tier 2 is about devices no hosted runner has
/// — a microphone, a headset, a game. Synthesized input needs only a desktop,
/// and the CI runner has one.
/// </para>
/// </summary>
internal static class Input
{
    private const uint Absolute = 0x8000;

    /// <summary>
    /// Not optional, and not obvious. <see cref="Absolute"/> on its own maps
    /// 0..65535 onto the <em>primary monitor</em>, so on a multi-monitor
    /// machine every click lands on the wrong screen while the arithmetic looks
    /// correct. Found by asking Windows where the cursor actually went.
    /// </summary>
    private const uint VirtualDesktop = 0x4000;

    private const uint Move = 0x0001;
    private const uint LeftDown = 0x0002;
    private const uint LeftUp = 0x0004;
    private const uint RightDown = 0x0008;
    private const uint RightUp = 0x0010;

    private const byte Escape = 0x1B;
    private const uint KeyUp = 0x0002;

    private const int VirtualScreenLeft = 76;
    private const int VirtualScreenTop = 77;
    private const int VirtualScreenWidth = 78;
    private const int VirtualScreenHeight = 79;

    /// <summary>
    /// Right-clicks the middle of a rectangle.
    /// </summary>
    /// <param name="box">The rectangle to click the middle of.</param>
    /// <exception cref="InvalidOperationException">
    /// The pointer did not end up where it was sent.
    /// </exception>
    internal static void RightClick(Rect box) => Click(box, RightDown, RightUp);

    /// <summary>
    /// Left-clicks the middle of a rectangle — how a tray icon is activated.
    /// </summary>
    /// <param name="box">The rectangle to click the middle of.</param>
    /// <exception cref="InvalidOperationException">
    /// The pointer did not end up where it was sent.
    /// </exception>
    internal static void LeftClick(Rect box) => Click(box, LeftDown, LeftUp);

    private static void Click(Rect box, uint down, uint up)
    {
        double x = box.X + (box.Width / 2);
        double y = box.Y + (box.Height / 2);

        (uint absoluteX, uint absoluteY) = ToAbsolute(x, y);

        mouse_event(Move | Absolute | VirtualDesktop, absoluteX, absoluteY, 0, UIntPtr.Zero);
        Thread.Sleep(200);

        // Asked of Windows rather than assumed. A click that silently lands
        // somewhere else shows up as "nothing happened", which is the same
        // symptom as a click that landed and was ignored.
        if (!GetCursorPos(out NativePoint landed)
            || Math.Abs(landed.X - x) > 2
            || Math.Abs(landed.Y - y) > 2)
        {
            throw new InvalidOperationException(
                $"Sent the pointer to {x:0},{y:0} but it arrived at {landed.X},{landed.Y}.");
        }

        mouse_event(down | Absolute | VirtualDesktop, absoluteX, absoluteY, 0, UIntPtr.Zero);
        Thread.Sleep(80);
        mouse_event(up | Absolute | VirtualDesktop, absoluteX, absoluteY, 0, UIntPtr.Zero);
    }

    /// <summary>
    /// Presses Escape, to put away a menu that opened when it should not have.
    ///
    /// <para>
    /// A right-click that misses the tray icon still opens something — the
    /// context menu of whatever was underneath. Leaving that on screen would
    /// hand the next test a desktop with a menu over it, so a miss has to clean
    /// up after itself.
    /// </para>
    /// </summary>
    internal static void PressEscape()
    {
        keybd_event(Escape, 0, 0, UIntPtr.Zero);
        Thread.Sleep(40);
        keybd_event(Escape, 0, KeyUp, UIntPtr.Zero);
        Thread.Sleep(300);
    }

    private static (uint X, uint Y) ToAbsolute(double x, double y)
    {
        int left = GetSystemMetrics(VirtualScreenLeft);
        int top = GetSystemMetrics(VirtualScreenTop);
        int width = GetSystemMetrics(VirtualScreenWidth);
        int height = GetSystemMetrics(VirtualScreenHeight);

        return (
            (uint)Math.Round((x - left) * 65535.0 / (width - 1)),
            (uint)Math.Round((y - top) * 65535.0 / (height - 1)));
    }

    // System32 rather than the default probing order: user32 is an operating
    // system library, and naming where it comes from is what stops a DLL of the
    // same name beside the test binaries being loaded instead.
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        internal int X;
        internal int Y;
    }
}
