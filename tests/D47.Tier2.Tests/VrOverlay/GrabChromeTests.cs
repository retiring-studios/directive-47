using System;

using D47.Placement;
using D47.Render;
using D47.TestSupport;
using D47.VrOverlay;

using Shouldly;

using Valve.VR;

using Xunit;

namespace D47.Tier2.Tests.VrOverlay;

/// <summary>
/// The grab chrome: a second quad in front of the compositor, framing the panel
/// rather than being drawn on it.
///
/// <para>
/// Tier 2 for the same reason <see cref="HeadsetOverlayTests"/> is — what is
/// asserted is what SteamVR thinks, not what our own object remembers being
/// told. Whether the bar and the handles look right on it is a person putting
/// the headset on, and that is asked for in the pull request rather than
/// pretended at here.
/// </para>
///
/// <para>
/// The pixels on our side of <c>SetOverlayRaw</c> are covered by
/// <c>ChromeRenderTests</c> in <c>D47.Tier1.Tests</c>, which needs no runtime
/// and runs in CI.
/// </para>
/// </summary>
public class GrabChromeTests : HeadsetTest
{
    [Fact]
    public void Chrome_IsASecondOverlay_WithItsOwnKey()
    {
        // Two quads, not one. The whole design rests on the chrome being
        // somewhere the shared render is not, so "the compositor knows two
        // overlays" is the fact that says it happened.
        using IHeadsetOverlay panel = Panelled();

        using IGrabChrome chrome = HeadsetOverlayFactory.Around(panel.Placed)
            ?? throw new InvalidOperationException(HeadsetTest.NeedsSteamVr);

        chrome.Showing(Grabbed.Nothing, Near.Nothing);

        ulong itsPanel = 0;
        ulong itsChrome = 0;

        OpenVR.Overlay.FindOverlay(HeadsetOverlayFactory.Key, ref itsPanel)
            .ShouldBe(EVROverlayError.None, "the panel's overlay should be there");

        OpenVR.Overlay.FindOverlay(HeadsetOverlayFactory.ChromeKey, ref itsChrome)
            .ShouldBe(EVROverlayError.None, "and the chrome should be a second one");

        itsChrome.ShouldNotBe(itsPanel, "sharing a handle would be sharing a quad");

        OpenVR.Overlay.IsOverlayVisible(itsChrome).ShouldBeTrue(
            "the chrome should be up once it has been shown");
    }

    [Fact]
    public void ChromeQuad_IsWiderThanThePanel_BecauseTheHandlesOverhang()
    {
        // The geometry, asked of the runtime rather than of Chrome.Around. That
        // arithmetic is Tier 0 and already asserted; what this adds is that the
        // number reached the compositor.
        using IHeadsetOverlay panel = Panelled();

        using IGrabChrome chrome = HeadsetOverlayFactory.Around(panel.Placed)
            ?? throw new InvalidOperationException(HeadsetTest.NeedsSteamVr);

        chrome.Showing(Grabbed.Nothing, Near.Nothing);

        ulong handle = 0;

        OpenVR.Overlay.FindOverlay(HeadsetOverlayFactory.ChromeKey, ref handle)
            .ShouldBe(EVROverlayError.None);

        float across = 0;

        OpenVR.Overlay.GetOverlayWidthInMeters(handle, ref across)
            .ShouldBe(EVROverlayError.None);

        across.ShouldBe(
            Chrome.Around(panel.Placed).Width,
            tolerance: 0.001,
            customMessage: "the chrome quad should be the size the placement asked for");

        across.ShouldBeGreaterThan(
            panel.Placed.Width, "and wider than the panel, or the handles have nowhere to be");
    }

    [Fact]
    public void Chrome_TakesAPointer_SoThatSteamVrDrawsALaserAtIt()
    {
        // The fix for the defect manual testing found. Without an input method
        // an overlay is a picture: the chrome appeared in the headset exactly as
        // designed and there was no way to aim at it, because SteamVR draws a
        // laser for overlays that ask for one and nothing for overlays that do
        // not.
        //
        // Asked of the compositor rather than of our own object, which is the
        // whole point of this tier — a field we set ourselves would pass against
        // a build where the call never reached SteamVR.
        using IHeadsetOverlay panel = Panelled();

        using IGrabChrome chrome = HeadsetOverlayFactory.Around(panel.Placed)
            ?? throw new InvalidOperationException(HeadsetTest.NeedsSteamVr);

        ulong handle = 0;

        OpenVR.Overlay.FindOverlay(HeadsetOverlayFactory.ChromeKey, ref handle)
            .ShouldBe(EVROverlayError.None);

        VROverlayInputMethod how = VROverlayInputMethod.None;

        OpenVR.Overlay.GetOverlayInputMethod(handle, ref how)
            .ShouldBe(EVROverlayError.None);

        how.ShouldBe(
            VROverlayInputMethod.Mouse,
            "the chrome should take a pointer, or there is nothing to aim with");
    }

    [Fact]
    public void Chrome_WithNoLaserOnIt_IsPointingAtNothing()
    {
        // Nobody is holding a controller at this overlay during a test run, so
        // the honest answer is nothing — and it has to be nothing rather than a
        // stale position or a throw. Following with no pointer anywhere near is
        // the state the watching thread is in for most of a session.
        using IHeadsetOverlay panel = Panelled();

        using IGrabChrome chrome = HeadsetOverlayFactory.Around(panel.Placed)
            ?? throw new InvalidOperationException(HeadsetTest.NeedsSteamVr);

        chrome.Follow().On.ShouldBe(Grabbed.Nothing);

        // And again, because a first call that primes something and a second
        // that reads it back differently is the shape of a bug here.
        chrome.Follow().On.ShouldBe(Grabbed.Nothing);
    }

    [Fact]
    public void Chrome_WhenGivenBack_LeavesNothingBehind()
    {
        // A quad nobody released stays floating in the cockpit after the
        // application that put it there has gone. Two overlays means two chances
        // to leave one.
        using IHeadsetOverlay panel = Panelled();

        using (IGrabChrome chrome = HeadsetOverlayFactory.Around(panel.Placed)
            ?? throw new InvalidOperationException(HeadsetTest.NeedsSteamVr))
        {
            chrome.Showing(Grabbed.Bar, new Near(Bar: true, false, false, false, false));
        }

        ulong handle = 0;

        OpenVR.Overlay.FindOverlay(HeadsetOverlayFactory.ChromeKey, ref handle)
            .ShouldNotBe(
                EVROverlayError.None,
                "the chrome overlay should be gone once it has been disposed");
    }

    /// <summary>
    /// The panel's overlay, which the chrome needs in order to know what it is
    /// framing.
    /// </summary>
    ///
    /// <remarks>
    /// On an STA thread, because getting pixels out of the render means building
    /// a WPF visual tree. The chrome itself needs no such thing, which is the
    /// point of it being rectangles.
    /// </remarks>
    private static IHeadsetOverlay Panelled() =>
        StaThread.Run(
            () => new HeadsetOverlayFactory().Create(Presentation.Of(Fixtures.HelpsAnswer())))
        ?? throw new InvalidOperationException(HeadsetTest.NeedsSteamVr);
}
