using System.Windows;
using System.Windows.Controls;

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
    /// How far out the fit is allowed to be, in device-independent pixels: one
    /// real pixel, at the densest the screen can be and still call them the same
    /// thing.
    ///
    /// <para>
    /// A window is a whole number of real pixels and what the controls want is
    /// not, so the fit lands on the pixel above what was asked for — up rather
    /// than to nearest, because landing short raises a scrollbar in a window
    /// built not to need one. What this tolerance hides is that one pixel, and
    /// nothing the screen can draw is smaller.
    /// </para>
    /// </summary>
    private const double ARoundingError = 1.0;

    [Fact]
    public void Panel_OnOpening_IsTheSizeOfTheControlsInIt()
    {
        (Size wanted, Size given) = OpenedPanel.Opened((panel, _) =>
            (WhatTheControlsWant(Fixtures.HelpsAnswer()), OpenedPanel.RoomTheRenderGot(panel)));

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
            OpenedPanel.Opened((panel, _) =>
            {
                double before = OpenedPanel.RoomTheRenderGot(panel).Height;
                double half = panel.ActualHeight / 2;

                panel.Height = half;
                panel.UpdateLayout();

                ScrollViewer scroll = OpenedPanel.ScrollingRegion(panel);

                return (
                    before,
                    half,
                    panel.ActualHeight,
                    OpenedPanel.RoomTheRenderGot(panel).Height,
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
}
