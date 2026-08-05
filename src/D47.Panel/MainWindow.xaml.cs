using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

using D47.Render;

namespace D47.Panel;

/// <summary>
/// The panel window. It hosts one <see cref="CapabilityView"/> and hands it what
/// to show; it does not know what is in that, and it does not decide it.
///
/// <para>
/// Composing what every surface shows moved to <see cref="App"/> when the game
/// overlay arrived. Two surfaces composing their own means two that can
/// disagree about what the Commander asked and about whether there is a newer
/// version.
/// </para>
/// </summary>
internal partial class MainWindow : Window
{
    /// <summary>
    /// The window's own styles, as opposed to its extended ones.
    /// </summary>
    private const int ItsOwnStyles = -16;

    /// <summary>
    /// <c>WS_MAXIMIZEBOX</c>: whether this window can be thrown to full screen.
    ///
    /// <para>
    /// One bit for three gestures. The caption button is the visible one, and
    /// double-clicking the title bar and Win+Up maximize a window without going
    /// anywhere near it — so this is the thing to clear, and hiding the button
    /// alone would leave two ways in.
    /// </para>
    /// </summary>
    private const long CanBeMaximized = 0x00010000;

    /// <summary>
    /// How big the controls in it want to be, worked out once and applied once.
    /// </summary>
    private readonly Size _controls;

    /// <summary>
    /// How big the panel is drawing what it is showing.
    /// </summary>
    private readonly Zoom _zoom;

    /// <summary>
    /// What to tell when the Commander answers the notice.
    /// </summary>
    private readonly Updates _updates;

    /// <summary>
    /// Creates the window around what it is to show, at the size it was last left
    /// drawing things.
    /// </summary>
    /// <param name="presented">What to render.</param>
    /// <param name="zoom">How big to draw it.</param>
    /// <param name="updates">What to tell about an answered update.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="zoom"/> or <paramref name="updates"/> is null.
    /// </exception>
    public MainWindow(Presentation presented, Zoom zoom, Updates updates)
    {
        ArgumentNullException.ThrowIfNull(presented);
        ArgumentNullException.ThrowIfNull(zoom);
        ArgumentNullException.ThrowIfNull(updates);

        InitializeComponent();

        View.DataContext = presented;

        _zoom = zoom;
        _updates = updates;
        _controls = CapabilityView.LaidOutFor(presented).DesiredSize;

        // Offered only when there is something to answer. The notice itself is
        // the render's and reaches every surface; these two are the panel's, and
        // a pair of buttons about an update nobody has is furniture that means
        // nothing.
        if (presented.UpdateWaiting is not null)
        {
            AboutTheUpdate.Visibility = Visibility.Visible;
        }

        DrawAtTheZoom();
        _zoom.Changed += (_, _) => DrawAtTheZoom();

        PreviewKeyDown += (_, pressed) =>
            pressed.Handled = Do(ZoomGestures.For(pressed.Key, Keyboard.Modifiers));

        PreviewMouseWheel += (_, turned) =>
            turned.Handled = Do(ZoomGestures.For(turned.Delta, Keyboard.Modifiers));

        Closing += HideInsteadOfClosing;
    }

    /// <summary>
    /// Does what a gesture asked for, and says whether it was one.
    /// </summary>
    ///
    /// <remarks>
    /// Whether it was one is what the caller marks the event handled with, and
    /// the distinction matters in both directions: a zoom gesture that was not
    /// marked would also scroll the region under it, and an ordinary wheel or
    /// keypress that was marked would stop working entirely.
    /// </remarks>
    /// <param name="asked">What the gesture asked for.</param>
    /// <returns>Whether it asked for anything.</returns>
    private bool Do(ZoomGesture asked)
    {
        switch (asked)
        {
            case ZoomGesture.Bigger:
                _zoom.In();
                return true;

            case ZoomGesture.Smaller:
                _zoom.Out();
                return true;

            case ZoomGesture.LifeSize:
                _zoom.Reset();
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Puts the current zoom onto the render.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// A <c>LayoutTransform</c>, which is the whole of "the way a browser does".
    /// The render is laid out again at the new size and then reflows into the
    /// window it is already in — wrapping across, running on down, and scrolling
    /// once there is more of it than the window holds.
    /// </para>
    /// <para>
    /// Deliberately not a <c>RenderTransform</c>, and the difference is
    /// invisible until it matters. That one scales the drawing after the layout
    /// is settled, so the type is just as big and nothing above it knows: the
    /// scrolling region still believes the render fits, and everything past the
    /// window's edge is gone rather than somewhere to scroll to. It is also what
    /// the overlay wants and has — a <c>Viewbox</c>, scaling a fixed layout —
    /// which is the same mechanism pointed at the opposite behaviour.
    /// </para>
    /// </remarks>
    private void DrawAtTheZoom()
    {
        // Whatever the surface is showing, not the render by name. The settings
        // page is a page in the panel, so the panel's zoom is its zoom too —
        // scaling View while the page was in there left the one thing a
        // Commander had just changed the zoom on as the only thing unaffected.
        if (Surface.Content is FrameworkElement showing)
        {
            showing.LayoutTransform = new ScaleTransform(_zoom.Factor, _zoom.Factor);
        }

        // The strip says where the zoom is and stops offering the direction it
        // cannot go. A control that stays enabled at the end of the range does
        // nothing when it is pressed, which nobody can tell from broken.
        Reading.Text = _zoom.AsSaid;
        Bigger.IsEnabled = _zoom.CanGoIn;
        Smaller.IsEnabled = _zoom.CanGoOut;
    }

    /// <summary>
    /// Asked for the settings page, and asked again to put it away.
    ///
    /// <para>
    /// An event rather than the window building the page itself. What a
    /// settings page needs — the store, and somewhere to send a change — is
    /// the composition root's, and a window that reached for either would be a
    /// second place that knows how the application is assembled.
    /// </para>
    /// </summary>
    internal event Action? SettingsAsked;

    /// <summary>
    /// Puts a page in the panel in place of whatever it was showing.
    /// </summary>
    ///
    /// <remarks>
    /// Two pages today and no more built than that: what the panel shows, and
    /// settings. [#72](https://github.com/retiring-studios/directive-47/issues/72)
    /// assumes there are pages — its cancel button works from any of them — so
    /// this is the seam that story arrives at rather than a general one built
    /// in advance.
    /// </remarks>
    /// <param name="page">What to show, or null to go back to the render.</param>
    internal void Showing(UIElement? page)
    {
        Surface.Content = page ?? View;

        // The window is as big as what is in it, and swapping the content does
        // not re-ask. Without this the panel keeps the render's size and the
        // page arrives with a scrollbar it does not need.
        InvalidateMeasure();

        // The zoom is a property of the panel rather than of a page, so it
        // follows whatever is in there. Applied here as well as at startup
        // because the page arrives after the window did.
        DrawAtTheZoom();
    }

    /// <summary>
    /// Whether the panel is on its settings page right now.
    /// </summary>
    internal bool ShowingSettings => !ReferenceEquals(Surface.Content, View);

    private void ShowOrHideSettings(object sender, RoutedEventArgs e) =>
        SettingsAsked?.Invoke();

    /// <summary>
    /// The strip's own way in.
    /// </summary>
    /// <param name="sender">The control.</param>
    /// <param name="e">The click.</param>
    private void ZoomIn(object sender, RoutedEventArgs e) => _zoom.In();

    /// <summary>
    /// The strip's own way out.
    /// </summary>
    /// <param name="sender">The control.</param>
    /// <param name="e">The click.</param>
    private void ZoomOut(object sender, RoutedEventArgs e) => _zoom.Out();

    /// <summary>
    /// Takes the update, which installs it when Directive 47 is closed.
    /// </summary>
    /// <param name="sender">The control.</param>
    /// <param name="e">The click.</param>
    private void AcceptTheUpdate(object sender, RoutedEventArgs e)
    {
        _updates.Accept();

        Answered();
    }

    /// <summary>
    /// Leaves the update alone.
    /// </summary>
    /// <param name="sender">The control.</param>
    /// <param name="e">The click.</param>
    private void DeclineTheUpdate(object sender, RoutedEventArgs e)
    {
        _updates.Decline();

        Answered();
    }

    /// <summary>
    /// Puts the two controls away once one of them has been pressed.
    /// </summary>
    ///
    /// <remarks>
    /// Both of them, whichever was pressed. A question that stays on screen
    /// after it has been answered is one the Commander cannot tell they
    /// answered — and the strip is chrome, so anything left in it is competing
    /// with the render for a reason that has gone away.
    /// </remarks>
    private void Answered() => AboutTheUpdate.Visibility = Visibility.Collapsed;

    /// <summary>
    /// Opens the window around its controls, at the moment it has a handle and
    /// before anything has been drawn.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// Here rather than in the constructor because the frame is only knowable
    /// once there is a window to have one, and here rather than after
    /// <c>Show</c> returns because by then the wrong size is on the screen.
    /// </para>
    /// <para>
    /// Once, and the once is what makes it correct. Fitted at startup and then
    /// the Commander's size wins: from the moment the window is dragged that is
    /// its size, and everything afterwards reflows or scrolls within it. A handle
    /// is created once and survives every hide and show, so this runs at startup
    /// and never again — which is also what keeps switching views from resizing
    /// the window when views arrive. A <c>TabControl</c> sizes its content area
    /// to the tab in front rather than to the largest one, so anything that kept
    /// fitting the window would make it jump on every click.
    /// </para>
    /// </remarks>
    /// <param name="e">The event arguments.</param>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        TakeAwayMaximize();

        DpiScale scale = VisualTreeHelper.GetDpi(this);
        Size frame = TheFrameWindowsDraws();

        // At the zoom, not at life size. A panel reopening at the size it was
        // last left drawing at is a panel that fits what is actually in it,
        // which is what #96 asked for — fitting it to a render nobody is being
        // shown would open every zoomed panel scrolling.
        double across = _controls.Width * _zoom.Factor;

        // Plus the strip, which is one of the controls in the window and so is
        // one of the things it is the size of. Measured rather than declared:
        // its height is a button's, which is the theme's business and not this
        // window's to predict.
        ZoomStrip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        double down = (_controls.Height * _zoom.Factor) + ZoomStrip.DesiredSize.Height;

        Width = (WholePixels(across, scale.DpiScaleX) + frame.Width) / scale.DpiScaleX;
        Height = (WholePixels(down, scale.DpiScaleY) + frame.Height) / scale.DpiScaleY;
    }

    /// <summary>
    /// Takes full screen off the window, leaving drag-resize alone.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// A style bit rather than <c>ResizeMode</c>, because <c>ResizeMode</c>
    /// cannot say this. Its four values pair drag-resize and maximize together —
    /// <c>CanResize</c> and <c>CanResizeWithGrip</c> give both, <c>NoResize</c>
    /// and <c>CanMinimize</c> give neither — and what is wanted here is one
    /// without the other. Drag-resize has to stay: it is how the Commander
    /// chooses the panel's size, and
    /// [#96](https://github.com/retiring-studios/directive-47/issues/96) shipped
    /// scrolling underneath it.
    /// </para>
    /// <para>
    /// Here rather than after <c>Show</c>, and the timing is the whole reason
    /// there is no frame-changed call beside it. Windows works out which caption
    /// buttons to draw when it first calculates the frame, which has not happened
    /// yet at this point — so the bit is simply true by the time anything reads
    /// it. Clearing it on a window already on screen would need the frame
    /// recalculated to catch up, and would leave a button that draws and does
    /// nothing until it did.
    /// </para>
    /// </remarks>
    private void TakeAwayMaximize()
    {
        IntPtr handle = new WindowInteropHelper(this).Handle;
        long style = GetWindowLongPtr(handle, ItsOwnStyles).ToInt64();

        SetWindowLongPtr(handle, ItsOwnStyles, new IntPtr(style & ~CanBeMaximized));
    }

    /// <summary>
    /// What the controls want, in the whole pixels a window is obliged to be a
    /// number of, rounded up.
    /// </summary>
    ///
    /// <remarks>
    /// Up rather than to nearest, and the difference is 17 pixels rather than
    /// one. What the controls want is fractional, a window is a whole number of
    /// real pixels, and rounding to nearest lands short about half the time —
    /// at which point the scrollbar appears in a window built not to need one,
    /// takes its own width off the render, wraps the label into what is left and
    /// makes it taller still. Rounding up costs at most one row of pixels of
    /// background nobody can distinguish from the render's own padding. Found on
    /// CI, which runs at 100% and rounded down where this desk at 150% had
    /// rounded up.
    /// </remarks>
    /// <param name="want">What the controls asked for.</param>
    /// <param name="scale">How many real pixels there are to one of those.</param>
    /// <returns>The same size in whole real pixels, never less.</returns>
    private static double WholePixels(double want, double scale) =>
        Math.Ceiling(want * scale);

    /// <summary>
    /// How much bigger this window is than the room inside it — a border either
    /// side and a title bar above, in physical pixels.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// Asked of the window rather than of the system, and the system's presented was
    /// written first. <c>SystemParameters.WindowNonClientFrameThickness</c> is
    /// short by 8 device-independent pixels in each direction on Windows 11,
    /// which draws a resize border it does not count. Measured, not reasoned
    /// about: a window sized that way came out 8 shorter than its own contents,
    /// and 8 short is 17 wrong once the scrollbar it raises has taken its width
    /// off the render. The difference between the two rectangles is not an
    /// estimate of any of that.
    /// </para>
    /// <para>
    /// Physical pixels, which is what Windows speaks and what the fit is worked
    /// out in — everything else here is in device-independent ones. The frame
    /// does not depend on how big the window currently is, so it can be read
    /// before the window has been given its size.
    /// </para>
    /// </remarks>
    /// <returns>The width and height the frame adds.</returns>
    private Size TheFrameWindowsDraws()
    {
        IntPtr handle = new WindowInteropHelper(this).Handle;

        GetWindowRect(handle, out Rectangle window);
        GetClientRect(handle, out Rectangle inside);

        return new Size(
            (window.Right - window.Left) - (inside.Right - inside.Left),
            (window.Bottom - window.Top) - (inside.Bottom - inside.Top));
    }

    /// <summary>
    /// Turns the close control into a hide.
    /// </summary>
    ///
    /// <remarks>
    /// The panel is convenience; the voice loop is the product. Closing the
    /// convenience must not take the product down with it, so the window goes
    /// away and the application does not. Exiting on purpose is the tray icon's
    /// menu, which is a different gesture rather than the same one.
    /// </remarks>
    /// <param name="sender">The window being closed.</param>
    /// <param name="e">The cancellable close.</param>
    private void HideInsteadOfClosing(object? sender, CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }

    /// <summary>
    /// Windows' own rectangle, in physical pixels and as edges rather than as a
    /// size.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Rectangle
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    // System32 rather than the default probing order: user32 is an operating
    // system library, and naming where it comes from is what stops a DLL of the
    // same name beside the executable being loaded instead.
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out Rectangle bounds);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(IntPtr window, out Rectangle bounds);

    // The Ptr forms, not the plain ones. On 64-bit Windows a style is a
    // pointer-sized value, and the 32-bit calls silently truncate it.
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetWindowLongPtrW")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SetWindowLongPtrW")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);
}
