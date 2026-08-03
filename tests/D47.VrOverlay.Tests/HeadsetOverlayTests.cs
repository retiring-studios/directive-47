using System;

using D47.Render;
using D47.TestSupport;

using Shouldly;

using Valve.VR;

using Xunit;

namespace D47.VrOverlay.Tests;

/// <summary>
/// Surface two: the same render the panel shows, on a quad floating in the
/// cockpit.
///
/// <para>
/// Tier 2. What is asserted is what the compositor thinks — that an overlay
/// exists under Directive 47's key, that it is showing, and that the texture on
/// it is the size of the render rather than of something else. Nobody can look
/// through the headset from a test, so "shows the panel's content" is claimed
/// as far as the runtime will answer for it and no further.
/// </para>
///
/// <para>
/// The arithmetic of where the quad goes is not here and never will be — that
/// is <c>D47.Placement</c>, and it is Tier 0. Neither is deciding whether to
/// have an overlay at all, which is
/// [#136](https://github.com/retiring-studios/directive-47/issues/136) and
/// belongs outside the adapter.
/// </para>
/// </summary>
public class HeadsetOverlayTests : HeadsetTest
{
    [Fact]
    public void Overlay_WhenSteamVrIsRunning_ShowsThePanelContentInTheHeadset()
    {
        Answer answer = Fixtures.HelpsAnswer();

        // On an STA thread because getting pixels out of the render means
        // building the WPF control and asking it to draw, and WPF will not
        // build a visual tree anywhere else.
        using IHeadsetOverlay overlay = StaThread.Run(
            () => new HeadsetOverlayFactory().Create(answer))
            ?? throw new InvalidOperationException(HeadsetTest.NeedsSteamVr);

        overlay.Show();

        overlay.IsVisible.ShouldBeTrue("the overlay should be up in the headset");

        // Asked of the compositor rather than of our own object. A property
        // that answers from a field it was told to set would pass against a
        // build that never reached SteamVR at all, which is the assertion this
        // whole tier exists to avoid.
        ulong handle = 0;

        OpenVR.Overlay.FindOverlay(HeadsetOverlayFactory.Key, ref handle)
            .ShouldBe(
                EVROverlayError.None,
                "SteamVR should know an overlay by Directive 47's key");

        OpenVR.Overlay.IsOverlayVisible(handle).ShouldBeTrue(
            "and should agree that it is showing");
    }

    [Fact]
    public void Overlay_ShowsTheRenderRatherThanSomethingItsOwnSize()
    {
        // The half that makes the fact above mean "the panel's content". An
        // overlay showing a blank quad of the wrong size would satisfy every
        // assertion up there.
        Answer answer = Fixtures.HelpsAnswer();

        (int width, int height) = StaThread.Run(() => PanelRender.Measure(answer));

        using IHeadsetOverlay overlay = StaThread.Run(
            () => new HeadsetOverlayFactory().Create(answer))
            ?? throw new InvalidOperationException(HeadsetTest.NeedsSteamVr);

        overlay.Show();

        ulong handle = 0;
        OpenVR.Overlay.FindOverlay(HeadsetOverlayFactory.Key, ref handle);

        uint onTheQuad = 0;
        uint andTallness = 0;

        OpenVR.Overlay.GetOverlayTextureSize(handle, ref onTheQuad, ref andTallness)
            .ShouldBe(EVROverlayError.None);

        onTheQuad.ShouldBe(
            (uint)width, "the texture should be the render, not a quad of some other size");

        andTallness.ShouldBe((uint)height);
    }

    [Fact]
    public void Overlay_WhenDisposed_LeavesNothingBehindInTheCompositor()
    {
        // A test that fails an assertion must not leave a quad floating in
        // somebody's cockpit, and neither must an application that exits.
        Answer answer = Fixtures.HelpsAnswer();

        IHeadsetOverlay overlay = StaThread.Run(() => new HeadsetOverlayFactory().Create(answer))
            ?? throw new InvalidOperationException(HeadsetTest.NeedsSteamVr);

        overlay.Show();
        overlay.Dispose();

        ulong handle = 0;

        OpenVR.Overlay.FindOverlay(HeadsetOverlayFactory.Key, ref handle).ShouldNotBe(
            EVROverlayError.None,
            "the key should be free again once the overlay has been given back");
    }
}
