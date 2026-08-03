using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace D47.GameOverlay.Tests;

/// <summary>
/// What the screen actually looks like, asked of the operating system rather
/// than of the application. Where a window is, and how far forward it sits.
///
/// <para>
/// Both questions are Windows' to answer. A window can believe it is topmost
/// and be behind something; "over the game" is a fact about the stack, and the
/// stack belongs to the shell.
/// </para>
/// </summary>
internal static class Screen
{
    /// <summary>
    /// How many windows to name when describing the stack. Enough to show what
    /// got in front, short enough that a failure message stays readable.
    /// </summary>
    private const int EnoughToDiagnose = 12;

    /// <summary>
    /// How much of a window's title to read when describing the stack. A title
    /// is for recognising the window, and nothing needs a thousand characters
    /// of it.
    /// </summary>
    private const int LongestTitleWorthReading = 256;

    private const uint NextInZOrder = 2;

    /// <summary>
    /// Where a window is, in physical screen pixels.
    /// </summary>
    /// <param name="window">The window to measure.</param>
    /// <returns>Its rectangle on the desktop.</returns>
    /// <exception cref="InvalidOperationException">
    /// The window is gone, so it has no rectangle.
    /// </exception>
    internal static Rect BoundsOf(IntPtr window)
    {
        if (!GetWindowRect(window, out Box box))
        {
            throw new InvalidOperationException(
                $"Window {window} is not on the desktop, so it has no bounds.");
        }

        return new Rect(box.Left, box.Top, box.Right - box.Left, box.Bottom - box.Top);
    }

    /// <summary>
    /// How far back a window sits, counting from the front. Zero is the
    /// frontmost window on the desktop.
    ///
    /// <para>
    /// Only the relative numbers mean anything. What "over the game" asserts is
    /// that the overlay's depth is the smaller of the two — nothing about the
    /// absolute position, which changes every time anything else opens.
    /// </para>
    /// </summary>
    /// <param name="window">The window to place in the stack.</param>
    /// <returns>Its depth, or -1 if it is not in the stack at all.</returns>
    internal static int DepthOf(IntPtr window)
    {
        int depth = 0;

        for (IntPtr next = GetTopWindow(IntPtr.Zero);
             next != IntPtr.Zero;
             next = GetWindow(next, NextInZOrder))
        {
            if (next == window)
            {
                return depth;
            }

            depth++;
        }

        return -1;
    }

    /// <summary>
    /// Which window owns a pixel — the window a click there would go to.
    ///
    /// <para>
    /// This is the question "does input pass through" actually reduces to.
    /// Windows skips click-through windows when answering it, so a window that
    /// is visibly on top of a pixel and does not own it is one the mouse goes
    /// straight past.
    /// </para>
    /// </summary>
    /// <param name="x">Screen x.</param>
    /// <param name="y">Screen y.</param>
    /// <returns>The window that would receive a click there.</returns>
    internal static IntPtr OwnerOf(int x, int y) =>
        WindowFromPoint(new NativePoint { X = x, Y = y });

    /// <summary>
    /// A window's title, for saying which window is meant. Empty when it has
    /// none, which plenty of real windows do.
    /// </summary>
    /// <param name="window">The window to name.</param>
    /// <returns>Its title.</returns>
    internal static string TitleOf(IntPtr window)
    {
        char[] title = new char[LongestTitleWorthReading];
        int length = GetWindowText(window, title, title.Length);

        return new string(title, 0, length);
    }

    /// <summary>
    /// The front of the window stack, named. "It was not in front" says nothing
    /// about why; what is in front usually does.
    /// </summary>
    internal static string DescribeStack()
    {
        var description = new StringBuilder();
        description.AppendLine("The front of the window stack:");

        int shown = 0;

        for (IntPtr next = GetTopWindow(IntPtr.Zero);
             next != IntPtr.Zero && shown < EnoughToDiagnose;
             next = GetWindow(next, NextInZOrder))
        {
            if (!IsWindowVisible(next))
            {
                continue;
            }

            description.AppendLine(
                CultureInfo.InvariantCulture,
                $"  {shown}: {next} \"{TitleOf(next)}\"");

            shown++;
        }

        return description.ToString();
    }

    /// <summary>
    /// A Win32 <c>RECT</c>, which is two corners where WPF's
    /// <see cref="Rect"/> is a corner and a size.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        internal int X;
        internal int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Box
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    // System32 rather than the default probing order: user32 is an operating
    // system library, and naming where it comes from is what stops a DLL of the
    // same name beside the test binaries being loaded instead.
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out Box box);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr GetTopWindow(IntPtr parent);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr GetWindow(IntPtr window, uint direction);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int GetWindowText(IntPtr window, [Out] char[] text, int capacity);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr WindowFromPoint(NativePoint point);
}
