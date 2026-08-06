using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;

namespace D47.GameOverlay;

/// <summary>
/// Elite Dangerous, as much of it as anything drawing over the game needs to
/// know: a window handle, and where that window is on screen.
///
/// <para>
/// Found by process name rather than by window title. The title is text the
/// game is free to change and has changed across releases; the executable's
/// name is what the launcher starts and what Task Manager shows.
/// </para>
///
/// <para>
/// Requires the game in borderless windowed mode, which is a requirement of the
/// whole feature rather than of this type — nothing can draw over fullscreen
/// exclusive, because the game owns the display. A fullscreen-exclusive Elite
/// still has a window and is still found here; what fails is the drawing.
/// </para>
/// </summary>
public sealed class EliteWindow
{
    /// <summary>
    /// The game itself, not <c>EDLaunch</c>, which is the launcher and has a
    /// window of its own.
    /// </summary>
    private const string GameProcess = "EliteDangerous64";

    private EliteWindow(IntPtr handle, Rect bounds)
    {
        Handle = handle;
        Bounds = bounds;
    }

    /// <summary>
    /// The game's main window.
    /// </summary>
    public IntPtr Handle { get; }

    /// <summary>
    /// Where that window is, in physical screen pixels rather than in the
    /// device-independent units WPF places windows with. Read from the
    /// operating system, so it is what anything else on the machine would see.
    /// </summary>
    public Rect Bounds { get; }

    /// <summary>
    /// Looks for the running game.
    /// </summary>
    /// <returns>
    /// The game's window, or <see langword="null"/> when Elite is not running
    /// or has not presented a window yet.
    /// </returns>
    public static EliteWindow? Find()
    {
        foreach (Process game in Process.GetProcessesByName(GameProcess))
        {
            using (game)
            {
                IntPtr handle = game.MainWindowHandle;

                if (handle != IntPtr.Zero && GetWindowRect(handle, out Box box))
                {
                    return new EliteWindow(
                        handle,
                        new Rect(
                            box.Left,
                            box.Top,
                            box.Right - box.Left,
                            box.Bottom - box.Top));
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Whether a window belongs to the running game.
    /// </summary>
    ///
    /// <remarks>
    /// Asked by process rather than by comparing handles. The game has more
    /// than one window over its lifetime — a launcher, a splash, the game
    /// itself — and any of them being in front means the Commander is looking
    /// at Elite rather than at us.
    /// </remarks>
    /// <param name="window">The window to ask about.</param>
    /// <returns>Whether the game owns it.</returns>
    public static bool IsTheGame(IntPtr window)
    {
        if (window == IntPtr.Zero)
        {
            return false;
        }

        // A zero thread id means the window is gone, which is not the game.
        return GetWindowThreadProcessId(window, out uint owner) != 0 && IsTheGame(owner);
    }

    /// <summary>
    /// Whether a process is the running game.
    /// </summary>
    ///
    /// <remarks>
    /// The question the window overload was always asking underneath, given a
    /// name of its own because not everything that wants it has a window to
    /// start from. SteamVR names the scene application by process id and never
    /// by handle, so asking whether Elite is what the headset is rendering means
    /// asking this.
    /// </remarks>
    /// <param name="process">The process id to ask about.</param>
    /// <returns>Whether it is the game.</returns>
    public static bool IsTheGame(uint process)
    {
        // Zero is what the window lookup and SteamVR both answer with when there
        // is nothing to name, and no real process has it.
        if (process == 0)
        {
            return false;
        }

        foreach (Process game in Process.GetProcessesByName(GameProcess))
        {
            using (game)
            {
                if ((uint)game.Id == process)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// A Win32 <c>RECT</c>, which is two corners where WPF's
    /// <see cref="Rect"/> is a corner and a size.
    /// </summary>
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
    // same name beside the executable being loaded instead.
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out Box box);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
}
