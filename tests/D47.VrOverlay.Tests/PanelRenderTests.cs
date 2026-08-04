using D47.Render;
using D47.TestSupport;

using Shouldly;

using Xunit;

namespace D47.VrOverlay.Tests;

/// <summary>
/// The pixels handed to the headset, before SteamVR ever sees them.
///
/// <para>
/// Deliberately not a <c>HeadsetTest</c>. Nothing here joins the runtime, and
/// inheriting that base would have made a fact about a byte array refuse to run
/// unless somebody started SteamVR first — which is why the base lives in
/// D47.VrOverlay.HardwareTests and this does not. These run in CI on every
/// pull request.
/// </para>
///
/// <para>
/// These exist because the compositor will not answer for a raw overlay's
/// pixels. It reports on textures set through other calls, so nothing on the
/// far side of <c>SetOverlayRaw</c> can be inspected, and the questions that
/// can be settled at all are settled on this side of it.
/// </para>
/// </summary>
public class PanelRenderTests
{
    /// <summary>
    /// Where "opaque" stops being worth arguing about.
    ///
    /// <para>
    /// Not 255, and the difference is a finding rather than a fudge. Antialiased
    /// text composited over the opaque background comes back at 254 in a few
    /// hundred places — WPF's premultiplied arithmetic rounding by one, invisible
    /// at any size and on any surface. Insisting on 255 would fail on that and
    /// say nothing about it. What this is looking for is a pixel the cockpit
    /// shows through, which is an alpha near zero rather than one short of full.
    /// </para>
    /// </summary>
    private const byte NearEnoughToOpaque = 250;

    [Fact]
    public void Render_ForTheHeadset_IsThePanelsLayoutAndIsNotBlank()
    {
        // The failure the story is most exposed to, and the one every
        // compositor-level assertion sails straight past: a render that came
        // out empty. An overlay carrying a fully transparent buffer exists, is
        // ours, is visible, is the right size in metres — and shows the
        // Commander nothing at all.
        Answer answer = Fixtures.HelpsAnswer();

        (int wide, int tall) = StaThread.Run(() => PanelRender.Measure(answer));
        (byte[] pixels, int width, int height) = StaThread.Run(() => PanelRender.Take(answer));

        width.ShouldBe(wide, "drawing it should not change how big it decided to be");
        height.ShouldBe(tall);

        pixels.Length.ShouldBe(width * height * 4, "four bytes a pixel, and no padding");

        bool anythingAtAll = false;

        for (int at = 3; at < pixels.Length; at += 4)
        {
            if (pixels[at] != 0)
            {
                anythingAtAll = true;
                break;
            }
        }

        anythingAtAll.ShouldBeTrue(
            $"the {width}x{height} render handed to the headset is entirely transparent, so the "
            + "overlay would be there and show nothing");
    }

    [Fact]
    public void Render_ForTheHeadset_HasRedAndBlueTheWayOpenVrReadsThem()
    {
        // WPF works in blue-green-red-alpha and OpenVR reads a raw buffer in
        // red-green-blue-alpha, so PanelRender swaps the two ends of every
        // pixel. Get that wrong and the overlay is not subtly off — it is
        // Directive 47's orange rendered as vivid blue.
        //
        // This is the handover risk that can be settled without a headset,
        // which is the whole reason it is written: it turns a deliberate look
        // in VR into a build failure.
        //
        // The background cannot answer the question. It is #1A1A1A, so its red
        // and blue are identical and it reads the same either way round. The
        // body text is #FFE000 — full red, no blue at all — so every pixel of
        // it decides the matter on its own.
        Answer answer = Fixtures.HelpsAnswer();

        (byte[] pixels, int width, int height) = StaThread.Run(() => PanelRender.Take(answer));

        int redder = 0;
        int bluer = 0;

        for (int at = 0; at < pixels.Length; at += 4)
        {
            if (pixels[at] > pixels[at + 2])
            {
                redder++;
            }
            else if (pixels[at + 2] > pixels[at])
            {
                bluer++;
            }
        }

        // Counted rather than sampled at a fixed coordinate, because the text
        // moves the moment anything about the layout does and a hard-coded
        // pixel would then be asserting about the background.
        redder.ShouldBeGreaterThan(
            0, $"the {width}x{height} render should have some of Directive 47's orange in it");

        bluer.ShouldBe(
            0,
            "nothing in this render is bluer than it is red, so a pixel that is means the two "
            + "ends of every pixel have been swapped and the overlay will come out blue");
    }

    [Fact]
    public void Render_ForTheHeadset_IsOpaqueRightToItsEdges()
    {
        // Written expecting to be trivially true, and it was not: the first
        // version of PanelRender left 1157 partly transparent pixels in a
        // 222x128 render. The bitmap is sized by rounding the layout up to whole
        // pixels, and the control had been arranged at its own fractional size —
        // so the last row and column, and every edge the padding rounded, were
        // never painted on.
        //
        // It matters more here than it would in a window. A translucent fringe
        // on a desktop overlay is a hairline nobody sees; in a headset the quad
        // is half a metre wide and that fringe is a visible soft edge. It also
        // decides a question nobody has had to answer: premultiplied and
        // straight alpha differ only where something is partly see-through, so
        // an opaque render means OpenVR's convention cannot bite us. Let
        // transparency back in and somebody has to go and find out which one it
        // wants.
        Answer answer = Fixtures.HelpsAnswer();

        (byte[] pixels, int width, int height) = StaThread.Run(() => PanelRender.Take(answer));

        int onTheEdge = 0;
        int inside = 0;

        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                if (pixels[(((row * width) + column) * 4) + 3] >= NearEnoughToOpaque)
                {
                    continue;
                }

                bool border = row == 0 || column == 0 || row == height - 1 || column == width - 1;

                if (border)
                {
                    onTheEdge++;
                }
                else
                {
                    inside++;
                }
            }
        }

        (onTheEdge + inside).ShouldBe(
            0,
            $"{onTheEdge + inside} of the {width}x{height} render's pixels are see-through "
            + $"— {onTheEdge} around the border and {inside} within it");
    }
}
