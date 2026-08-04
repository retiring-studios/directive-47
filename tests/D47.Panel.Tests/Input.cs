using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Input;

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

    private const byte ShiftKey = 0x10;
    private const byte ControlKey = 0x11;
    private const byte AltKey = 0x12;

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
        // Before anything is aimed, because the middle of nowhere is a real
        // place. A UI Automation element with no presence on screen reports
        // (0,0,0,0) — which is what a tray icon reports the moment the overflow
        // flyout closes, and the flyout closes whenever anything takes focus.
        // The middle of that is the top-left pixel of the screen, where a window
        // keeps its system-menu icon.
        //
        // Clicking it opened an unrelated application's menu, which put that
        // application's thread into a modal loop, which hung the next UI
        // Automation call in this process — so a bounded retry loop with a throw
        // at the end of it never reached the throw. The whole desktop suite
        // stopped there, twice.
        //
        // The check below this one does not catch it: it asks whether the
        // pointer arrived where it was sent, and the pointer had.
        if (box.IsEmpty || box.Width <= 0 || box.Height <= 0)
        {
            throw new InvalidOperationException(
                $"Asked to click {box}, which is not a place — whatever this was aimed at is not "
                + "on screen. An element that has gone away reports a rectangle of zeroes, and "
                + "the middle of that is the corner of the desktop.");
        }

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

    /// <summary>
    /// Holds the modifiers, taps the key, and lets go — the way a Commander
    /// presses a hotkey.
    /// </summary>
    ///
    /// <remarks>
    /// Order matters and is not decoration. Windows matches a hotkey on the key
    /// going down while the modifiers are already held, so releasing the
    /// modifiers first, or pressing the key first, produces a keystroke nobody
    /// is listening for and a test that fails for a reason that is not the
    /// code's.
    /// </remarks>
    /// <param name="modifiers">The modifiers to hold.</param>
    /// <param name="key">The key to tap.</param>
    internal static void Press(ModifierKeys modifiers, Key key)
    {
        byte[] held = VirtualKeysFor(modifiers);

        foreach (byte modifier in held)
        {
            keybd_event(modifier, 0, 0, UIntPtr.Zero);
        }

        Thread.Sleep(40);

        byte pressed = (byte)KeyInterop.VirtualKeyFromKey(key);
        keybd_event(pressed, 0, 0, UIntPtr.Zero);
        Thread.Sleep(40);
        keybd_event(pressed, 0, KeyUp, UIntPtr.Zero);
        Thread.Sleep(40);

        // Released in reverse, which is what a hand does and what leaves no
        // modifier stuck down for whatever runs next.
        for (int index = held.Length - 1; index >= 0; index--)
        {
            keybd_event(held[index], 0, KeyUp, UIntPtr.Zero);
        }
    }

    private static byte[] VirtualKeysFor(ModifierKeys modifiers)
    {
        List<byte> keys = [];

        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            keys.Add(ControlKey);
        }

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            keys.Add(AltKey);
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            keys.Add(ShiftKey);
        }

        return [.. keys];
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
