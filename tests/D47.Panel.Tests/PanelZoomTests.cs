using System.Windows;
using System.Windows.Controls;

using Shouldly;

using Xunit;

namespace D47.Panel.Tests;

/// <summary>
/// What the zoom does to what is on the screen. <see cref="ZoomTests"/> covers
/// the decisions; this covers the only part of them that needs a window.
/// </summary>
[Collection(CompiledXaml.Collection)]
public class PanelZoomTests
{
    /// <summary>
    /// A zoom worth telling apart from a rounding error, and one every panel can
    /// reach: twice life size, which is in the middle of the levels rather than
    /// at the end of them.
    /// </summary>
    private const double TwiceLifeSize = 2.0;

    [Fact]
    public void Panel_WhenZoomed_ScalesItsContentsTheWayABrowserDoes()
    {
        (Size before, Size after, Size window, Size windowAfter) = OpenedPanel.Opened(
            (panel, zoom) =>
            {
                // Given room first. The render is one line of label in a window
                // fitted to it, so at twice the size it would wrap — and a
                // wrapped line is two lines tall, which reads as four times the
                // height and is not what this is about. Reflow is real and is
                // the next test's subject; here the question is only whether
                // the type got bigger.
                panel.Width = panel.ActualWidth * 3;
                panel.UpdateLayout();

                Size lifeSize = OpenedPanel.LineOnTheWindow(panel);
                Size box = new(panel.ActualWidth, panel.ActualHeight);

                while (zoom.Factor < TwiceLifeSize)
                {
                    zoom.In();
                }

                panel.UpdateLayout();

                return (
                    lifeSize,
                    OpenedPanel.LineOnTheWindow(panel),
                    box,
                    new Size(panel.ActualWidth, panel.ActualHeight));
            });

        // Height, and deliberately only height. The obvious companion assertion
        // is that the line got twice as wide, and it is worthless: the label
        // wraps, so it takes whatever width it is offered and reports that
        // rather than the width of its text. Under a layout scale the offer is
        // the window divided by the zoom and the drawing multiplies it back, so
        // the number is the window's width whichever way the zoom is applied,
        // so it cannot tell the two apart. Height is font-driven while the line
        // fits on one line, which the window widened above guarantees.
        after.Height.ShouldBe(
            before.Height * TwiceLifeSize,
            tolerance: 1.0,
            customMessage:
                $"the line went from {before.Height} to {after.Height} tall, where twice the zoom "
                + "would have made it " + (before.Height * TwiceLifeSize)
                + " — a zoom that scaled one direction only is a stretch");

        // The other half of "like a browser". Zooming and resizing are
        // independent: the type got bigger inside the window the Commander
        // already has, rather than the window growing to keep the layout the
        // same. A window that resized here would also be a window that took back
        // the size he chose, which #96 settled it does not.
        windowAfter.ShouldBe(
            window,
            customMessage:
                $"the window was {window} and became {windowAfter}. Zoom is not a resize");
    }

    [Fact]
    public void Panel_WhenZoomedPastWhatFits_ReflowsAndThenScrolls()
    {
        // The claim that tells a layout scale from a drawing one, and the reason
        // it is worth a test of its own: a transform applied when the render is
        // drawn rather than when it is laid out looks identical at this zoom.
        // The type is bigger either way. What it cannot do is tell the scrolling
        // region that there is more to reach — so the content overflows a window
        // that still believes everything fits, and the part past the edge is
        // gone rather than scrolled to.
        (double scrollable, Size drawn, Size laidOut, Size roomAvailable) = OpenedPanel.Opened(
            (panel, zoom) =>
            {
                while (zoom.CanGoIn)
                {
                    zoom.In();
                }

                panel.UpdateLayout();

                ScrollViewer scrolling = OpenedPanel.ScrollingRegion(panel);

                return (
                    scrolling.ScrollableHeight,
                    OpenedPanel.RenderOnTheWindow(panel),
                    OpenedPanel.RoomTheRenderGot(panel),
                    new Size(scrolling.ViewportWidth, scrolling.ViewportHeight));
            });

        scrollable.ShouldBeGreaterThan(
            0,
            $"the render is {drawn.Height} tall in {roomAvailable.Height} of room, so there has "
            + "to be somewhere to scroll to");

        // Reflowed into the window rather than running off the side of it. The
        // width is where a zoom shows whether it is a layout or a drawing: laid
        // out again, the render is given the window's width and wraps inside it.
        drawn.Width.ShouldBe(
            roomAvailable.Width,
            tolerance: 1.0,
            customMessage:
                $"the render covers {drawn.Width} of a window offering {roomAvailable.Width}. "
                + "Zoomed content reflows across the window it is in rather than running off it");

        // And it reflowed to get there, rather than being drawn wider than it
        // was measured. Laid out at the window divided by the zoom is what a
        // layout scale does and what a drawing scale cannot: that one measures
        // against the whole window and then draws the result larger than it.
        laidOut.Width.ShouldBe(
            roomAvailable.Width / Zoom.Levels[^1],
            tolerance: 1.0,
            customMessage:
                $"the render was laid out {laidOut.Width} wide for a window offering "
                + $"{roomAvailable.Width} at {Zoom.Levels[^1]}x");
    }
}
