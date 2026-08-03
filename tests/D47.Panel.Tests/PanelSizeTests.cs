using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

using D47.Render;
using D47.TestSupport;

using Shouldly;

using Xunit;

namespace D47.Panel.Tests;

/// <summary>
/// How big the panel window is, which is a question about the controls in it
/// rather than about the window.
///
/// <para>
/// The window is shown rather than merely built, and that is the whole reason
/// these are worth writing. A window that has never been shown has the size
/// somebody asked for and no client area, so every assertion available to it is
/// an assertion about the number this code wrote down. Shown, the render reports
/// the room it was actually given — which is what the Commander sees, and which
/// is the only version of the question that could have caught what it caught: a
/// first implementation asked the system how thick the window frame was, was told
/// 8 device-independent pixels less than the truth, and produced a window that
/// looked deliberate and was too small for its own contents.
/// </para>
///
/// <para>
/// In-process for all that: one small window, opened without taking the
/// foreground and gone again within the test. Nothing outside this thread is
/// touched, which is the line <c>DesktopTest</c> draws and the side of it these
/// are on.
/// </para>
/// </summary>
[Collection(CompiledXaml.Collection)]
public class PanelSizeTests
{
    /// <summary>
    /// How far out the fit is allowed to be, in device-independent pixels.
    ///
    /// <para>
    /// A window is a whole number of real pixels, and what the controls want is
    /// not — so the fit lands on the pixel either side of it, which is a whole
    /// device-independent pixel at 100% and two thirds of one at 150%. Anything
    /// this tolerance would hide is smaller than the smallest thing the screen
    /// can draw.
    /// </para>
    /// </summary>
    private const double ARoundingError = 1.0;

    [Fact]
    public void Panel_OnOpening_IsTheSizeOfTheControlsInIt()
    {
        (Size wanted, Size given) = Opened((panel, answer) =>
            (WhatTheControlsWant(answer), RoomTheRenderGot(panel)));

        given.Width.ShouldBe(
            wanted.Width,
            tolerance: ARoundingError,
            customMessage:
                $"the controls want {wanted.Width} wide and the window gave them {given.Width}. "
                + "The panel is supposed to open around its controls, not at a number somebody "
                + "typed");

        given.Height.ShouldBe(
            wanted.Height,
            tolerance: ARoundingError,
            customMessage:
                $"the controls want {wanted.Height} tall and the window gave them {given.Height}. "
                + "A window sized without allowing for its own title bar is short by exactly one, "
                + "which is a third of the height when the render is one line of label");
    }

    [Fact]
    public void Panel_WhenDraggedSmallerThanItsControls_ScrollsRatherThanSquashingThem()
    {
        // The other half of "fitted at startup". Fitting a window that then
        // refused to shrink would make the fitted size a floor rather than a
        // starting point, and the size is the Commander's from the moment he
        // touches it.
        (double fitted, double dragged, double kept, double render, double scrollable) =
            Opened((panel, _) =>
            {
                double before = RoomTheRenderGot(panel).Height;
                double half = panel.ActualHeight / 2;

                panel.Height = half;
                panel.UpdateLayout();

                ScrollViewer scroll = Descendant<ScrollViewer>(panel)
                    ?? throw new InvalidOperationException(
                        "The panel has nothing to scroll with, so anything that does not fit is "
                        + "simply not visible.");

                return (
                    before,
                    half,
                    panel.ActualHeight,
                    RoomTheRenderGot(panel).Height,
                    scroll.ScrollableHeight);
            });

        kept.ShouldBe(
            dragged,
            tolerance: ARoundingError,
            customMessage:
                $"the window was made {dragged} tall and came back {kept}. From the moment it is "
                + "dragged the size is the Commander's, so nothing may fit it to its contents "
                + "again");

        // At least as tall, rather than exactly as tall. The scrollbar that
        // appears takes width off the render, the label wraps into the width
        // that is left, and wrapped text is taller — which is the reflow rule
        // doing its job rather than a failure of this one. What cannot happen is
        // the render coming back smaller, which is what drawing the controls to
        // fit the room would look like.
        render.ShouldBeGreaterThanOrEqualTo(
            fitted,
            customMessage:
                $"the render was {fitted} tall in a window that fitted it and {render} tall in "
                + "half that window. Half the room is not a reason to draw the controls smaller");

        scrollable.ShouldBeGreaterThan(
            0,
            "a window smaller than its controls has to offer a way to reach the rest of them");
    }

    /// <summary>
    /// Opens a panel around help's answer, asks it something, and takes it away
    /// again.
    /// </summary>
    ///
    /// <remarks>
    /// <c>ShowActivated</c> is off because a test has no business taking the
    /// foreground from whoever is at the keyboard, and closing goes through the
    /// dispatcher rather than through <c>Close</c> — the panel turns a close into
    /// a hide, so a test that called it would leave the window behind.
    /// </remarks>
    /// <typeparam name="T">Whatever the asking works out.</typeparam>
    /// <param name="asking">What to ask of the open panel.</param>
    /// <returns>Whatever the asking worked out.</returns>
    private static T Opened<T>(Func<MainWindow, Answer, T> asking) =>
        StaThread.Run(() =>
        {
            Answer answer = Fixtures.HelpsAnswer();
            var panel = new MainWindow(answer) { ShowActivated = false };

            try
            {
                panel.Show();

                return asking(panel, answer);
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });

    /// <summary>
    /// What the controls want, worked out independently of the window.
    /// </summary>
    ///
    /// <remarks>
    /// Arranged and laid out, not merely measured. The answer's template is
    /// chosen by a resource lookup that needs the control connected, so measuring
    /// alone reports the size of an almost empty box — a bug that has already
    /// shipped once, on the overlay, and one this expectation would quietly agree
    /// with if it made the same mistake.
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
    /// How much room the render was actually given, read off the window's own
    /// tree rather than worked back from the window's size.
    /// </summary>
    /// <param name="panel">The open panel.</param>
    /// <returns>The size the render was laid out at.</returns>
    private static Size RoomTheRenderGot(MainWindow panel)
    {
        CapabilityView render = Descendant<CapabilityView>(panel)
            ?? throw new InvalidOperationException("The panel is showing no render.");

        return new Size(render.ActualWidth, render.ActualHeight);
    }

    /// <summary>
    /// The first element of a kind under something, or nothing at all.
    /// </summary>
    /// <typeparam name="T">The kind being looked for.</typeparam>
    /// <param name="node">Where to start looking.</param>
    /// <returns>The first match, or <see langword="null"/>.</returns>
    private static T? Descendant<T>(DependencyObject node)
        where T : DependencyObject
    {
        int children = VisualTreeHelper.GetChildrenCount(node);

        for (int child = 0; child < children; child++)
        {
            DependencyObject descendant = VisualTreeHelper.GetChild(node, child);

            if (descendant is T found)
            {
                return found;
            }

            if (Descendant<T>(descendant) is { } deeper)
            {
                return deeper;
            }
        }

        return null;
    }
}
