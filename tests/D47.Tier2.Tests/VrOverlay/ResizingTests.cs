using System;
using System.Linq;

using D47.Capabilities;
using D47.Render;
using D47.TestSupport;
using D47.VrOverlay;

using Shouldly;

using Valve.VR;

using Xunit;

namespace D47.Tier2.Tests.VrOverlay;

/// <summary>
/// Whether a live overlay will take an image of a different shape.
///
/// <para>
/// The unknown [#175](https://github.com/retiring-studios/directive-47/issues/175)
/// named and deferred: until
/// [#134](https://github.com/retiring-studios/directive-47/issues/134) one image
/// went up once at one size, so nothing had ever asked SteamVR to accept a
/// second one that was not the same shape. A render grows when its answer has
/// more lines in it, so repainting is exactly where that gets asked.
/// </para>
///
/// <para>
/// It may work, it may need the overlay recreated, and nobody knew which. If it
/// does not work, <c>Headset.Showing</c> has a defect that only appears on a
/// machine with a headset and only once a capability answers twice — which is
/// the worst shape of bug this repository can produce, because Wave 2 is where
/// it would surface.
/// </para>
/// </summary>
public class ResizingTests : HeadsetTest
{
    [Fact]
    public void Overlay_WhenTheRenderChangesShape_TakesTheNewImage()
    {
        Answer few = Answering(2);
        Answer many = Answering(40);

        // The precondition, asserted rather than assumed. If both answers
        // rendered to the same size this test would pass while asking nothing,
        // which is the failure mode of every "it changed" test that does not
        // check that it changed.
        (int wide, int tall) =
            StaThread.Run(() => PanelRender.Measure(Presentation.Of(few)));
        (int widerOrTaller, int tallerStill) =
            StaThread.Run(() => PanelRender.Measure(Presentation.Of(many)));

        (widerOrTaller, tallerStill).ShouldNotBe(
            (wide, tall), "the two answers should render at different sizes, or this asks nothing");

        using IHeadsetOverlay overlay = StaThread.Run(
            () => new HeadsetOverlayFactory().Create(Presentation.Of(few)))
            ?? throw new InvalidOperationException(HeadsetTest.NeedsSteamVr);

        overlay.Show();

        // The question. SetOverlayRaw is told the width and height every time,
        // so it may simply take the new ones — or the compositor may have sized
        // the quad's texture once at creation and refuse.
        Should.NotThrow(
            () => StaThread.Run(() =>
            {
                overlay.Paint(Presentation.Of(many));
                return true;
            }),
            "SteamVR should accept an image of a different shape on an overlay it already has");

        // And it is still there afterwards. A call that returned success while
        // leaving the compositor holding nothing would pass the line above.
        ulong handle = 0;

        OpenVR.Overlay.FindOverlay(HeadsetOverlayFactory.Key, ref handle)
            .ShouldBe(EVROverlayError.None, "the overlay should have survived the repaint");

        OpenVR.Overlay.IsOverlayVisible(handle).ShouldBeTrue("and should still be showing");
    }

    [Fact]
    public void Overlay_WhenTheRenderChangesShapeSeveralTimes_TakesEachOne()
    {
        // Because a compositor that accepts the second size by growing a buffer
        // might refuse the third, and because shrinking is a different question
        // from growing.
        Answer first = Answering(2);

        using IHeadsetOverlay overlay = StaThread.Run(
            () => new HeadsetOverlayFactory().Create(Presentation.Of(first)))
            ?? throw new InvalidOperationException(HeadsetTest.NeedsSteamVr);

        overlay.Show();

        foreach (int lines in new[] { 20, 5, 60, 1 })
        {
            Answer answer = Answering(lines);

            Should.NotThrow(
                () => StaThread.Run(() =>
                {
                    overlay.Paint(Presentation.Of(answer));
                    return true;
                }),
                $"SteamVR refused a render of {lines} lines");
        }
    }

    /// <summary>
    /// Help's answer with a given number of lines in it, so two answers differ
    /// by the shape they render at rather than by their contents alone.
    /// </summary>
    /// <param name="lines">How many lines the answer should hold.</param>
    /// <returns>An answer that renders that tall.</returns>
    private static Answer Answering(int lines)
    {
        Answer help = Fixtures.HelpsAnswer();

        return help with
        {
            Result = new ListResult
            {
                CapabilityId = help.Descriptor.Id,
                Items = [.. Enumerable.Range(1, lines).Select(at => $"Line {at} of the answer")],
            },
        };
    }
}
