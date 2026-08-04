using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
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
/// render" is one definition and one presented, not one object.
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

    /// <summary>
    /// Small enough to be a glance and large enough to still be grabbed. Below
    /// this the grip is most of what is left.
    /// </summary>
    private const double SmallestWorthHaving = 120;

    private EliteWindow? _game;
    private bool _passesInputThrough;
    private double _shape;

    /// <summary>
    /// Where the overlay has been put, in physical screen pixels, or null while
    /// it has never been put anywhere.
    ///
    /// <para>
    /// Null is the whole of "never been placed", and it is what makes the
    /// game's corner a fallback rather than the answer. It fills in from two
    /// directions — <see cref="PlaceAt"/> on the way up, from whatever the last
    /// run wrote down, and the end of every drag after that.
    /// </para>
    /// </summary>
    private Point? _putAt;

    /// <summary>
    /// Creates the overlay around what it is to show.
    /// </summary>
    /// <param name="presented">What to render.</param>
    public GameOverlayWindow(Presentation presented)
    {
        InitializeComponent();
        View.DataContext = presented;

        FitTheRender(presented);
    }

    /// <summary>
    /// Gives the window the render's own size to start at, and pins the render
    /// to it.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// Asked of the render rather than declared anywhere: the controls decide
    /// how big the render is, and every surface is that size. From here the
    /// <c>Viewbox</c> scales whatever the window becomes, so this is the size at
    /// which the scale is exactly one.
    /// </para>
    /// <para>
    /// The pin is what makes the <c>Viewbox</c> a pure scale rather than a
    /// scale of something that keeps changing size. It is not what stops the
    /// render reflowing — a <c>Viewbox</c> measures its child against infinity
    /// at every window size, so nothing under one reflows with or without it.
    /// Established while writing
    /// <c>GameOverlay_WhenResized_ScalesItsContentsRatherThanReflowingThem</c>,
    /// which could not be made to fail by removing the pin.
    /// </para>
    /// </remarks>
    /// <param name="presented">What the render will be showing.</param>
    private void FitTheRender(Presentation presented)
    {
        Size natural = CapabilityView.LaidOutFor(presented).DesiredSize;

        View.Width = natural.Width;
        View.Height = natural.Height;

        Width = natural.Width;
        Height = natural.Height;
        _shape = natural.Height / natural.Width;
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
    /// The size the render was laid out at — the size at which the
    /// <c>Viewbox</c>'s scale is exactly one.
    ///
    /// <para>
    /// The window is expected to keep this shape at every size. When it does
    /// not, the render letterboxes inside its own window and the chrome, which
    /// hangs off the window's edges, is left stranded away from anything
    /// visible.
    /// </para>
    /// </summary>
    public Size NaturalSize => new(View.Width, View.Height);

    /// <summary>
    /// Whether the furniture for moving and resizing is on show.
    /// </summary>
    public bool ShowsChrome
    {
        get => Chrome.Visibility == Visibility.Visible;
        set => Chrome.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Where the window's top-left corner is, in physical screen pixels.
    /// </summary>
    ///
    /// <remarks>
    /// Asked of Windows rather than computed from <c>Left</c> and <c>Top</c>.
    /// Those are device-independent, so turning them into pixels means applying
    /// a scale factor that is itself per-monitor — arithmetic that is wrong in
    /// exactly the case this feature has to survive, which is a window on the
    /// second screen. Before there is a window to ask, the answer is wherever
    /// it has been told to go.
    /// </remarks>
    public Point Position =>
        GetWindowRect(new WindowInteropHelper(this).Handle, out Box box)
            ? new Point(box.Left, box.Top)
            : _putAt ?? default;

    /// <summary>
    /// How much bigger than the render's own size the window has been made.
    /// </summary>
    ///
    /// <remarks>
    /// Both widths are device-independent, so the ratio between them is not in
    /// any units at all — which is the reason a scale is what gets remembered
    /// rather than a width.
    /// </remarks>
    public double Scale => Width / View.Width;

    /// <summary>
    /// Raised once the Commander has finished moving or resizing the overlay.
    /// </summary>
    public event EventHandler? MovedOrResized;

    /// <summary>
    /// Puts the window at a given corner and size, instead of over the game.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// The size is applied here and the corner is only remembered, because a
    /// window without a handle cannot be moved and this is normally called
    /// before there is one. <c>OnSourceInitialized</c> is where it lands, which
    /// is also what keeps the overlay from appearing at the game's corner first
    /// and stepping across the screen afterwards.
    /// </para>
    /// <para>
    /// A scale that would make the overlay smaller than the grip is worth is
    /// pulled back up to that size, by the same rule and the same number the
    /// grip itself enforces. A remembered value nobody could grab is not a
    /// reason to refuse to start.
    /// </para>
    /// </remarks>
    /// <param name="corner">Its top-left corner, in physical screen pixels.</param>
    /// <param name="scale">How much bigger than the render's own size to be.</param>
    public void PlaceAt(Point corner, double scale)
    {
        _putAt = corner;

        MakeItThisWide(View.Width * scale);

        if (new WindowInteropHelper(this).Handle != IntPtr.Zero)
        {
            PutItWhereItGoes();
        }
    }

    /// <summary>
    /// Makes the window a given width, keeping the render's shape and never
    /// going below the size a grip is worth having.
    /// </summary>
    ///
    /// <remarks>
    /// The one place the overlay's single degree of freedom is turned into two
    /// numbers. Both callers used to do this themselves, which made "a
    /// remembered size obeys the same floor the grip does" a claim in a comment
    /// rather than something the code could not get wrong.
    /// </remarks>
    /// <param name="width">How wide to make it.</param>
    private void MakeItThisWide(double width)
    {
        double wide = Math.Max(SmallestWorthHaving, width);

        Width = wide;
        Height = wide * _shape;
    }

    /// <summary>
    /// Drags the whole overlay by its bar.
    /// </summary>
    ///
    /// <remarks>
    /// <c>DragMove</c> throws if the left button is not actually down by the
    /// time it is called, which can happen when the press and the release race
    /// each other. Guarded rather than caught, because the guard says what is
    /// true and a catch would only say that something went wrong.
    /// </remarks>
    /// <param name="sender">The bar.</param>
    /// <param name="e">The press.</param>
    private void MoveTheOverlay(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        DragMove();

        // After it returns, not during. DragMove runs its own message loop and
        // comes back when the button does, so this is the end of one gesture
        // rather than every position the overlay passed through on the way.
        Settled();
    }

    /// <summary>
    /// Notices that the overlay has come to rest somewhere.
    /// </summary>
    ///
    /// <remarks>
    /// The corner is taken from the window rather than tracked through the
    /// gesture, so what gets remembered is where Windows actually put it. It is
    /// kept as well as announced, because the next showing has to leave it here
    /// instead of snapping it back to wherever the last run said.
    /// </remarks>
    private void Settled()
    {
        _putAt = Position;

        MovedOrResized?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Notices the end of a pull on the grip.
    /// </summary>
    ///
    /// <remarks>
    /// Separate from <see cref="ResizeTheOverlay"/>, which fires per pixel of
    /// the drag. This one fires once, when the button comes up.
    /// </remarks>
    /// <param name="sender">The grip.</param>
    /// <param name="e">Where the drag ended.</param>
    private void SettleTheSize(object sender, DragCompletedEventArgs e) => Settled();

    /// <summary>
    /// Scales the overlay by its grip, keeping its shape.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// One dimension is dragged and the other follows, because the overlay's
    /// shape is the render's and a resize is only ever a scale. The horizontal
    /// change leads for no better reason than that a corner drag reads as a
    /// width to most people.
    /// </para>
    /// <para>
    /// The shape came from the render at construction, so it is the render's
    /// proportions being preserved rather than whatever the window happened to
    /// be when somebody first grabbed the grip.
    /// </para>
    /// </remarks>
    /// <param name="sender">The grip.</param>
    /// <param name="e">How far it moved.</param>
    private void ResizeTheOverlay(object sender, DragDeltaEventArgs e) =>
        MakeItThisWide(ActualWidth + e.HorizontalChange);

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

        PutItWhereItGoes();
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

        PutItWhereItGoes();

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
    /// Puts the window's corner where it was left, or on the game's corner if
    /// it has never been put anywhere.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// The game's corner is the fallback and not the answer. It used to be the
    /// presented, applied on every showing, which is a perfectly good way to open
    /// an overlay exactly once and a guarantee that it will never stay
    /// anywhere.
    /// </para>
    /// <para>
    /// In physical pixels, which is what the game's bounds are read in and what
    /// <c>SetWindowPos</c> speaks. Going through WPF's <c>Left</c> and
    /// <c>Top</c> would mean converting to device-independent units and back
    /// for no gain — the only thing being decided here is a corner.
    /// </para>
    /// </remarks>
    private void PutItWhereItGoes()
    {
        if (WhereItGoes() is not { } corner)
        {
            return;
        }

        SetWindowPos(
            new WindowInteropHelper(this).Handle,
            TheTopmostBand,
            (int)corner.X,
            (int)corner.Y,
            0,
            0,
            KeepTheSize | LeaveTheFocusAlone);
    }

    /// <summary>
    /// The corner to use: where it was left, else the game's, else nowhere —
    /// which is a window that has never been placed and has no game to be over,
    /// and is left wherever Windows chose to put it.
    /// </summary>
    private Point? WhereItGoes() =>
        _putAt ?? (_game is { } game ? new Point(game.Bounds.X, game.Bounds.Y) : null);

    /// <summary>
    /// A Win32 <c>RECT</c>, which is two corners where WPF's
    /// <see cref="Rect"/> is a corner and a size.
    /// </summary>
    ///
    /// <remarks>
    /// The second one in this assembly, and deliberately not shared with
    /// <see cref="EliteWindow"/>'s. Sharing them means a third type that exists
    /// only to be shared by two adapters, which is how a junk drawer starts;
    /// four ints declared where they are used is the cheaper of the two.
    /// </remarks>
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
    // The Ptr forms, not the plain ones. On 64-bit Windows an extended style is
    // a pointer-sized value, and the 32-bit calls silently truncate it.
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out Box box);

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
