using System.Numerics;

using D47.Placement;
using D47.Render;
using D47.VrOverlay;

using Shouldly;

using Xunit;

namespace D47.Tier1.Tests.VrOverlay;

/// <summary>
/// The chrome's pixels: transparent where the panel shows through, opaque where
/// there is something to grab, and brighter on whichever part the controller is
/// aimed at.
///
/// <para>
/// It is a second texture on a second quad, which is what keeps VR-only drawing
/// out of the shared render — see the Architecture section of
/// <c>docs/decisions.md</c>. Nothing here builds a visual tree, so it needs no
/// STA thread and no desktop; it is Tier 1 only because it lives beside
/// <c>PanelRender</c> in the adapter.
/// </para>
/// </summary>
public class ChromeRenderTests
{
    private static readonly Board Panel =
        new(new Pose(Vector3.Zero, Quaternion.Identity), 0.5f, 0.3f);

    /// <summary>
    /// Where the middle of the panel lands in the chrome texture.
    ///
    /// <para>
    /// Not the middle of the texture. The chrome hangs lower than the panel to
    /// carry the bar, so the panel's centre sits at 48% down rather than 50% —
    /// worked out from the geometry in <c>ChromeTests</c> rather than from this
    /// code, so that a mapping bug cannot agree with itself.
    /// </para>
    /// </summary>
    private const double PanelMiddleDown = 0.48;

    /// <summary>
    /// Where the middle of the bar lands: 94% down, by the same arithmetic.
    /// </summary>
    private const double BarMiddleDown = 0.94;

    /// <summary>
    /// The middle of the top right corner handle: 94.6% across, 8% down.
    ///
    /// <para>
    /// Inside the grab target and outside the ink, which is the whole of what a
    /// bracket changed. <c>Chrome.On</c> still answers <c>TopRightCorner</c>
    /// here.
    /// </para>
    /// </summary>
    private const double CornerAcross = 0.946;
    private const double CornerDown = 0.08;

    /// <summary>
    /// On the top arm of that handle's bracket: the same place across, but up
    /// against the top edge of the quad, which is where the handle's outer edge
    /// sits.
    /// </summary>
    private const double ArmDown = 0.016;

    [Fact]
    public void Chrome_WhereThePanelShowsThrough_IsTransparent()
    {
        // The whole reason there are two quads. An opaque middle would hide the
        // render this is supposed to be framing.
        Taken(Grabbed.Nothing).Alpha(0.5, PanelMiddleDown).ShouldBe((byte)0);
    }

    [Fact]
    public void Chrome_WhereTheBarIs_IsSomethingYouCanSee()
    {
        Taken(Grabbed.Nothing).Alpha(0.5, BarMiddleDown).ShouldBeGreaterThan((byte)0);
    }

    [Fact]
    public void TheBar_IsAShortPillRatherThanASlabTheWidthOfThePanel()
    {
        Chromed chrome = Taken(Grabbed.Nothing);

        // Inked in the middle and clear towards the ends. A band the full width
        // of the panel is more ink than all four corners together, and it is
        // what read as a slab across the bottom of the cockpit.
        chrome.Alpha(0.5, BarMiddleDown).ShouldBeGreaterThan((byte)0);

        chrome.Alpha(0.2, BarMiddleDown).ShouldBe(
            (byte)0, "the bar should not reach the left of the panel");

        chrome.Alpha(0.8, BarMiddleDown).ShouldBe(
            (byte)0, "nor the right");
    }

    [Fact]
    public void Chrome_WithTheLaserNowhereNear_IsNotThereAtAll()
    {
        Chromed empty = Taken(Grabbed.Nothing, Near.Nothing);

        // The whole quad, sampled where each piece would be. Chrome that is
        // always faintly present is furniture in the cockpit; the maintainer
        // looked at it in the headset and asked for it to keep out of the way
        // until it is wanted.
        empty.Alpha(0.5, BarMiddleDown).ShouldBe((byte)0);
        empty.Alpha(CornerAcross, ArmDown).ShouldBe((byte)0);
    }

    [Fact]
    public void APiece_TheLaserIsNear_ComesOutWithoutBringingTheOthersWithIt()
    {
        Chromed justTheBar = Taken(
            Grabbed.Nothing, new Near(Bar: true, false, false, false, false));

        justTheBar.Alpha(0.5, BarMiddleDown).ShouldBeGreaterThan((byte)0);

        // A Commander reaching for the bar has not asked to see the corners.
        justTheBar.Alpha(CornerAcross, ArmDown).ShouldBe(
            (byte)0, "being near the bar should not light up the corners");
    }

    [Fact]
    public void TheBar_IsStillTheWholeWidthToAimAt()
    {
        // The same trade the brackets make: less ink, same target. Somebody
        // aiming near the end of the bar is aiming at the bar.
        Board around = Chrome.Around(Panel);

        float across = (0.2f - 0.5f) * around.Width;
        float up = (0.5f - (float)BarMiddleDown) * around.Height;

        Chrome.On(Panel, across, up).ShouldBe(Grabbed.Bar);
    }

    [Fact]
    public void Chrome_WhereACornerHandleIs_IsSomethingYouCanSee()
    {
        Taken(Grabbed.Nothing).Alpha(CornerAcross, ArmDown).ShouldBeGreaterThan((byte)0);
    }

    [Fact]
    public void ACornerHandle_IsABracketRatherThanABlock()
    {
        Chromed chrome = Taken(Grabbed.Nothing);

        // Ink on the arm, nothing in the middle. A solid handle is a fifth of
        // the panel's height in ink at each corner, which is four blocks around
        // something meant to be read.
        chrome.Alpha(CornerAcross, ArmDown).ShouldBeGreaterThan((byte)0);

        chrome.Alpha(CornerAcross, CornerDown).ShouldBe(
            (byte)0, "the middle of a handle should be empty, not filled");
    }

    [Fact]
    public void ACornerHandle_IsStillTheWholeSquareToAimAt()
    {
        // The point of a bracket rather than a smaller handle: less ink, same
        // target. If this ever disagrees with the test above, the drawing and
        // the hit test have drifted apart — which is the thing Chrome.Parts
        // exists to prevent.
        Board around = Chrome.Around(Panel);

        float across = ((float)CornerAcross - 0.5f) * around.Width;
        float up = (0.5f - (float)CornerDown) * around.Height;

        Chrome.On(Panel, across, up).ShouldBe(Grabbed.TopRightCorner);
    }

    [Fact]
    public void Chrome_OutsideTheBarAndTheHandles_IsTransparent()
    {
        // Straight up from the middle, above the panel and between the two top
        // handles. There is chrome quad there and nothing on it.
        Taken(Grabbed.Nothing).Alpha(0.5, 0.02).ShouldBe((byte)0);
    }

    [Fact]
    public void Chrome_WhenTheBarIsWhatWouldBeGrabbed_IsBrighterThere()
    {
        byte dim = Taken(Grabbed.Nothing).Alpha(0.5, BarMiddleDown);
        byte lit = Taken(Grabbed.Bar).Alpha(0.5, BarMiddleDown);

        lit.ShouldBeGreaterThan(dim);
    }

    [Fact]
    public void Chrome_WhenACornerIsWhatWouldBeGrabbed_IsBrighterThereAndNowhereElse()
    {
        Chromed lit = Taken(Grabbed.TopRightCorner);
        Chromed dim = Taken(Grabbed.Nothing);

        lit.Alpha(CornerAcross, ArmDown).ShouldBeGreaterThan(
            dim.Alpha(CornerAcross, ArmDown));

        // The bar is not the corner, so it stays as it was. Lighting the whole
        // chrome would tell the Commander they were holding all of it.
        lit.Alpha(0.5, BarMiddleDown).ShouldBe(dim.Alpha(0.5, BarMiddleDown));
    }

    [Fact]
    public void Chrome_WhenThePanelItselfIsPointedAt_LightsNothing()
    {
        // Content grabs nothing, so nothing brightens. The Commander is aimed at
        // the overlay and is not about to move it.
        Chromed content = Taken(Grabbed.Content);
        Chromed nothing = Taken(Grabbed.Nothing);

        content.Alpha(0.5, BarMiddleDown).ShouldBe(nothing.Alpha(0.5, BarMiddleDown));
        content.Alpha(CornerAcross, ArmDown).ShouldBe(
            nothing.Alpha(CornerAcross, ArmDown));
    }

    [Fact]
    public void Chrome_IsDrawnInTheSameColourTheRenderWrites()
    {
        // Elite's own HUD yellow, dimmed rather than a sixth palette value
        // re-sourced for this. Palette says the other five sampled colours are
        // undeclared because nothing draws with them, and this draws with none
        // of them either.
        Chromed lit = Taken(Grabbed.Bar);
        (byte red, byte green, byte blue) = lit.Colour(0.5, BarMiddleDown);

        red.ShouldBe(Palette.BodyText.Color.R);
        green.ShouldBe(Palette.BodyText.Color.G);
        blue.ShouldBe(Palette.BodyText.Color.B);
    }

    [Fact]
    public void Chrome_IsBigEnoughToCarryWhatIsDrawnOnIt()
    {
        // Wider than it is tall, in the proportions Chrome.Around gives — so the
        // texture and the quad it goes on agree about their shape, and nothing
        // is stretched.
        Chromed taken = Taken(Grabbed.Nothing);
        Board around = Chrome.Around(Panel);

        ((double)taken.Width / taken.Height).ShouldBe(
            around.Width / around.Height, 0.02);
    }

    /// <summary>
    /// The chrome with everything close enough to draw, which is the state most
    /// of these facts are about. What is drawn when the laser is elsewhere is
    /// its own pair of tests below.
    /// </summary>
    private static Chromed Taken(Grabbed lit) => Taken(lit, Everything);

    private static Chromed Taken(Grabbed lit, Near shown)
    {
        (byte[] pixels, int width, int height) = ChromeRender.Take(Panel, lit, shown);

        return new Chromed(pixels, width, height);
    }

    private static Near Everything => new(true, true, true, true, true);

    /// <summary>
    /// A chrome texture, with a way to ask what is at a spot on it.
    /// </summary>
    private sealed record Chromed(byte[] Pixels, int Width, int Height)
    {
        internal byte Alpha(double across, double down) => At(across, down)[3];

        internal (byte Red, byte Green, byte Blue) Colour(double across, double down)
        {
            byte[] found = At(across, down);

            return (found[0], found[1], found[2]);
        }

        /// <summary>
        /// The four bytes at a fraction of the way across and down.
        /// </summary>
        ///
        /// <remarks>
        /// RGBA, because that is what OpenVR reads and what
        /// <c>PanelRender.Take</c> already hands over.
        /// </remarks>
        private byte[] At(double across, double down)
        {
            int x = (int)(across * (Width - 1));
            int y = (int)(down * (Height - 1));
            int start = ((y * Width) + x) * 4;

            return [Pixels[start], Pixels[start + 1], Pixels[start + 2], Pixels[start + 3]];
        }
    }
}
