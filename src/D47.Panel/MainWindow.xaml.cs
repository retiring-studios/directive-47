using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

using D47.Render;

namespace D47.Panel;

/// <summary>
/// The panel window. It hosts one <see cref="CapabilityView"/> and hands it the
/// answer to show; it does not know what is in that answer, and it no longer
/// decides what the answer is.
///
/// <para>
/// Composing that answer moved to <see cref="App"/> when the game overlay
/// arrived. Two surfaces showing the same answer means one object shown twice,
/// and a window that builds its own is a window that can disagree with the
/// overlay about what the Commander asked.
/// </para>
/// </summary>
internal partial class MainWindow : Window
{
    /// <summary>
    /// How big the controls in it want to be, worked out once and applied once.
    /// </summary>
    private readonly Size _controls;

    /// <summary>
    /// Creates the window around an answer to show.
    /// </summary>
    /// <param name="answer">What to render.</param>
    public MainWindow(Answer answer)
    {
        InitializeComponent();

        View.DataContext = answer;

        _controls = WhatTheControlsWant(answer);

        Closing += HideInsteadOfClosing;
    }

    /// <summary>
    /// How big the controls want to be, asked of an instance that belongs to
    /// nobody.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// A throwaway rather than the one in the window. The one in the window
    /// already has a parent, so measuring it asks what it wants inside whatever
    /// it is already sitting in rather than what it wants.
    /// </para>
    /// <para>
    /// Arranged and laid out, not merely measured. Templates are resolved during
    /// layout, and the answer's template is chosen by a resource lookup that
    /// needs the control connected — so measuring alone returns the size of an
    /// almost empty box. That is not a hypothetical: it shipped once on the
    /// overlay, which came out too narrow for its own chrome while looking
    /// internally consistent.
    /// </para>
    /// <para>
    /// This is the second window to ask the question and <c>GameOverlayWindow</c>
    /// is the first, so there is a shared question here and no shared code for
    /// it. Deliberately left as two and raised in the pull request rather than
    /// extracted quietly: zoom is a third caller, and where "how big does the
    /// render want to be" belongs is the maintainer's call.
    /// </para>
    /// </remarks>
    /// <param name="answer">What the render would be showing.</param>
    /// <returns>Its natural size.</returns>
    private static Size WhatTheControlsWant(Answer answer)
    {
        var asking = new CapabilityView { DataContext = answer };

        asking.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        asking.Arrange(new Rect(asking.DesiredSize));
        asking.UpdateLayout();

        return asking.DesiredSize;
    }

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

        Size frame = TheFrameWindowsDraws();

        Width = _controls.Width + frame.Width;
        Height = _controls.Height + frame.Height;
    }

    /// <summary>
    /// How much bigger this window is than the room inside it — a border either
    /// side and a title bar above.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// Asked of the window rather than of the system, and the system's answer was
    /// written first. <c>SystemParameters.WindowNonClientFrameThickness</c> is
    /// short by 8 device-independent pixels in each direction on Windows 11,
    /// which draws a resize border it does not count. Measured, not reasoned
    /// about: a window sized that way came out 8 shorter than its own contents,
    /// which raised the scrollbar, which took 17 more off the width, which
    /// wrapped the one line of label onto two and made it taller still. The
    /// difference between the two rectangles is not an estimate of any of that.
    /// </para>
    /// <para>
    /// Both rectangles are in physical pixels, which is what Windows speaks and
    /// what makes the scale necessary — everything else here is in
    /// device-independent ones. The frame does not depend on how big the window
    /// currently is, so it can be read before the window has been given its size.
    /// </para>
    /// </remarks>
    /// <returns>The width and height the frame adds.</returns>
    private Size TheFrameWindowsDraws()
    {
        IntPtr handle = new WindowInteropHelper(this).Handle;

        GetWindowRect(handle, out Rectangle window);
        GetClientRect(handle, out Rectangle inside);

        DpiScale scale = VisualTreeHelper.GetDpi(this);

        return new Size(
            ((window.Right - window.Left) - (inside.Right - inside.Left)) / scale.DpiScaleX,
            ((window.Bottom - window.Top) - (inside.Bottom - inside.Top)) / scale.DpiScaleY);
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
}
