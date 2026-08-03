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

    /// <summary>
    /// Small enough to be a glance and large enough to still be grabbed. Below
    /// this the grip is most of what is left.
    /// </summary>
    private const double SmallestWorthHaving = 120;

    private EliteWindow? _game;
    private bool _passesInputThrough;
    private double _shape;

    /// <summary>
    /// Creates the overlay around an answer to show.
    /// </summary>
    /// <param name="answer">What to render.</param>
    public GameOverlayWindow(Answer answer)
    {
        InitializeComponent();
        View.DataContext = answer;

        FitTheRender(answer);
    }

    /// <summary>
    /// How big the render wants to be, asked of an instance that belongs to
    /// nobody.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// A throwaway rather than the one in the window, and the difference is not
    /// academic: the one in the window already has a parent, so measuring it
    /// out of band asks what it wants *inside a Viewbox inside a Grid* rather
    /// than what it wants. That came out exactly one padding narrower — 64
    /// device-independent pixels — than the truth, which is the sort of wrong
    /// that looks plausible.
    /// </para>
    /// <para>
    /// Arranged and laid out, not merely measured. Templates are resolved
    /// during layout, and the answer's template is chosen by a resource lookup
    /// that needs the control connected — so measuring alone returns the size of
    /// an almost empty box.
    /// </para>
    /// </remarks>
    /// <param name="answer">What the render would be showing.</param>
    /// <returns>Its natural size.</returns>
    private static Size WhatTheRenderWants(Answer answer)
    {
        var asking = new CapabilityView { DataContext = answer };

        asking.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        asking.Arrange(new Rect(asking.DesiredSize));
        asking.UpdateLayout();

        return asking.DesiredSize;
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
    /// <param name="answer">What the render will be showing.</param>
    private void FitTheRender(Answer answer)
    {
        Size natural = WhatTheRenderWants(answer);

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
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

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
    private void ResizeTheOverlay(object sender, DragDeltaEventArgs e)
    {
        double width = Math.Max(SmallestWorthHaving, ActualWidth + e.HorizontalChange);

        Width = width;
        Height = width * _shape;
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
