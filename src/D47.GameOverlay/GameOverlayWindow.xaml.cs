using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

using D47.Render;

namespace D47.GameOverlay;

/// <summary>
/// The overlay drawn over Elite: a transparent, always-on-top window whose
/// whole content is the same render the panel shows.
///
/// <para>
/// It instantiates <see cref="CapabilityView"/> rather than sharing the panel's
/// instance, because WPF cannot put one element in two windows. "The same
/// render" is one definition and one answer, not one object.
/// </para>
/// </summary>
public partial class GameOverlayWindow : Window
{
    /// <summary>
    /// Where the window goes in the stack. -1 is Windows' constant for the
    /// topmost band, which is the band the overlay has to be in to be over a
    /// game that is itself a full-screen window.
    /// </summary>
    private static readonly IntPtr TheTopmostBand = new(-1);

    /// <summary>
    /// Move it, do not resize it — the size is the render's business.
    /// </summary>
    private const uint KeepTheSize = 0x0001;

    /// <summary>
    /// Move it without giving it the foreground, which the game is holding and
    /// should keep.
    /// </summary>
    private const uint LeaveTheFocusAlone = 0x0010;

    /// <summary>
    /// The index of a window's extended style, for reading and writing it.
    /// </summary>
    private const int ExtendedStyle = -20;

    /// <summary>
    /// <c>WS_EX_TRANSPARENT</c>. The mouse is not offered to this window at
    /// all — Windows looks straight past it to whatever is behind.
    /// </summary>
    private const long PassesThrough = 0x00000020;

    private EliteWindow? _game;
    private bool _passesInputThrough;

    /// <summary>
    /// Creates the overlay around an answer to show.
    /// </summary>
    /// <param name="answer">What to render.</param>
    public GameOverlayWindow(Answer answer)
    {
        InitializeComponent();
        View.DataContext = answer;
    }

    /// <summary>
    /// Whether the mouse goes straight through to the game.
    /// </summary>
    ///
    /// <remarks>
    /// A window style rather than anything WPF knows about, toggled on the live
    /// window. The window is never recreated to change it — measured in the
    /// spike, which set and cleared this flag and watched the pixel change hands
    /// while the handle stayed the same.
    ///
    /// <para>
    /// Remembered even before there is a handle to apply it to, because the
    /// composition decides where the foreground is before the overlay has ever
    /// been shown.
    /// </para>
    /// </remarks>
    public bool PassesInputThrough
    {
        get => _passesInputThrough;

        set
        {
            _passesInputThrough = value;
            ApplyPassingThrough();
        }
    }

    /// <summary>
    /// Shows the overlay above the game.
    /// </summary>
    /// <param name="game">The running game to draw over.</param>
    /// <exception cref="ArgumentNullException"><paramref name="game"/> is null.</exception>
    public void ShowOver(EliteWindow game)
    {
        ArgumentNullException.ThrowIfNull(game);

        _game = game;

        // First showing and every showing after it need placing, and they need
        // it at different moments. Before there is a handle, the placement has
        // to wait for one, which is what OnSourceInitialized is for — and doing
        // it there is what keeps the window from appearing somewhere else
        // first. Afterwards the handle already exists, so the window is placed
        // while it is still hidden and then shown, which has the same effect
        // for the same reason.
        if (new WindowInteropHelper(this).Handle == IntPtr.Zero)
        {
            Show();
            return;
        }

        PlaceOver(game);
        Show();
    }

    /// <summary>
    /// Puts the window where it belongs, at the moment it has a handle and
    /// before anything has been drawn.
    /// </summary>
    ///
    /// <remarks>
    /// Deliberately here rather than after <c>Show</c> returns. By then the
    /// window is on the screen, and moving it would be a flash at wherever
    /// Windows first put it — which for an overlay meant to look like part of
    /// the game is the difference between appearing and being noticed.
    /// </remarks>
    /// <param name="e">The event arguments.</param>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        if (_game is { } game)
        {
            PlaceOver(game);
        }

        // Whatever was decided before there was a handle to decide it on.
        ApplyPassingThrough();
    }

    /// <summary>
    /// Puts the current decision onto the window, if there is a window yet.
    /// </summary>
    private void ApplyPassingThrough()
    {
        IntPtr handle = new WindowInteropHelper(this).Handle;

        if (handle == IntPtr.Zero)
        {
            return;
        }

        long style = GetWindowLongPtr(handle, ExtendedStyle).ToInt64();

        SetWindowLongPtr(
            handle,
            ExtendedStyle,
            new IntPtr(_passesInputThrough ? style | PassesThrough : style & ~PassesThrough));
    }

    /// <summary>
    /// Puts the window's corner on the game's corner.
    /// </summary>
    ///
    /// <remarks>
    /// In physical pixels, which is what the game's bounds are read in and what
    /// <c>SetWindowPos</c> speaks. Going through WPF's <c>Left</c> and
    /// <c>Top</c> would mean converting to device-independent units and back
    /// for no gain — the only thing being decided here is a corner.
    /// </remarks>
    /// <param name="game">The game to sit over.</param>
    private void PlaceOver(EliteWindow game) =>
        SetWindowPos(
            new WindowInteropHelper(this).Handle,
            TheTopmostBand,
            (int)game.Bounds.X,
            (int)game.Bounds.Y,
            0,
            0,
            KeepTheSize | LeaveTheFocusAlone);

    // System32 rather than the default probing order: user32 is an operating
    // system library, and naming where it comes from is what stops a DLL of the
    // same name beside the executable being loaded instead.
    // The Ptr forms, not the plain ones. On 64-bit Windows an extended style is
    // a pointer-sized value, and the 32-bit calls silently truncate it.
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetWindowLongPtrW")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SetWindowLongPtrW")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr window,
        IntPtr after,
        int x,
        int y,
        int width,
        int height,
        uint flags);
}
