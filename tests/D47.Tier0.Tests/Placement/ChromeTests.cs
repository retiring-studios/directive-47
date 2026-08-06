using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using D47.Placement;

using Shouldly;

using Xunit;

namespace D47.Tier0.Tests.Placement;

/// <summary>
/// Where the chrome quad goes: big enough to carry the bar and the handles, and
/// lined up with the panel it belongs to.
///
/// <para>
/// A second quad rather than anything drawn on the panel, which is what keeps
/// the shared render free of VR-specific painting — see the Architecture section
/// of <c>docs/decisions.md</c>. Everything here is in metres, so the chrome and
/// the hit test agree about where a grab target starts by reading the same
/// numbers rather than by two people keeping two constants in step.
/// </para>
/// </summary>
public class ChromeTests
{
    /// <summary>
    /// A micrometre, which is the slack the fitting facts below allow.
    ///
    /// <para>
    /// The extremes touch the quad's edges exactly, and "exactly" in floats
    /// means the bar's bottom comes out at -0.18750001 against a half-height of
    /// -0.1875 — the same number by two routes. Forcing the two arithmetics to
    /// agree bit for bit would make the code worse to read for nothing anybody
    /// in a cockpit could see.
    /// </para>
    /// </summary>
    private const float Hair = 0.000001f;

    /// <summary>
    /// The shipped quad's proportions. At half a metre by three tenths, the bar
    /// beneath is 0.045 tall and each handle is a 0.06 square straddling its
    /// corner — so the chrome reaches 0.03 past each side and past the top, and
    /// 0.045 past the bottom.
    /// </summary>
    private static readonly Board Panel =
        new(new Pose(Vector3.Zero, Quaternion.Identity), 0.5f, 0.3f);

    [Fact]
    public void Chrome_IsWideEnough_ForTheHandlesToOverhangBothSides()
    {
        Chrome.Around(Panel).Width.ShouldBe(0.56f, 0.0001f);
    }

    [Fact]
    public void Nothing_IsNearALaserInTheMiddleOfThePanel()
    {
        // Reading the panel is not reaching for it. This is the state a
        // Commander is in whenever they are actually using the overlay, and it
        // is the whole point of the chrome keeping out of the way.
        Chrome.Nearby(Panel, 0, 0).ShouldBe(Near.Nothing);
    }

    [Fact]
    public void ALaser_OnAHandle_BringsOutThatHandleAndNoOther()
    {
        // The top right handle straddles (0.25, 0.1575) in the chrome's frame.
        Near near = Chrome.Nearby(Panel, 0.25f, 0.1575f);

        near.TopRight.ShouldBeTrue();

        near.TopLeft.ShouldBeFalse("the far corner is half a metre away");
        near.BottomRight.ShouldBeFalse();
        near.BottomLeft.ShouldBeFalse();
    }

    [Fact]
    public void ALaser_ShortOfAHandle_StillBringsItOut()
    {
        // Five centimetres inside the panel from the top right corner, which is
        // within the six the share allows. "At or near it" is the requirement,
        // and a handle that only appeared once the laser was already on it would
        // be one nobody could find.
        Chrome.Nearby(Panel, 0.20f, 0.1075f).TopRight.ShouldBeTrue();
    }

    [Fact]
    public void ALaser_NearTheBottomEdge_BringsOutTheBar()
    {
        // Just inside the bottom of the panel, above where the bar hangs.
        Near near = Chrome.Nearby(Panel, 0, -0.13f);

        near.Bar.ShouldBeTrue();

        // The bar spans the whole width, so its two ends reach the lower
        // handles — but the middle of it does not.
        near.BottomLeft.ShouldBeFalse();
        near.BottomRight.ShouldBeFalse();
    }

    [Fact]
    public void Nearby_MeasuresToThePieceRatherThanItsMiddle()
    {
        // The bar is the width of the panel. Measured to its centre, the left
        // end would need the laser almost a whole panel away before it counted
        // as near; measured to the rectangle, anywhere along it is close.
        Chrome.Nearby(Panel, -0.24f, -0.13f).Bar.ShouldBeTrue();
        Chrome.Nearby(Panel, 0.24f, -0.13f).Bar.ShouldBeTrue();
    }

    [Fact]
    public void Chrome_IsTallEnough_ForTheBarBeneathAndTheHandlesAbove()
    {
        // 0.3 of panel, plus 0.03 of handle above, plus 0.045 of bar below. The
        // bar is deeper than the handle overhang, so the bottom is the bar's and
        // the handles cost nothing extra down there.
        Chrome.Around(Panel).Height.ShouldBe(0.375f, 0.0001f);
    }

    [Fact]
    public void Chrome_SitsLowerThanThePanel_BecauseTheBarHangsBeneathIt()
    {
        // It reaches further down than up, so its middle is not the panel's
        // middle. Centring the two would put the bar half off the bottom of the
        // quad meant to be carrying it.
        Vector3 where = Chrome.Around(Panel).Where.Position;

        where.Y.ShouldBe(-0.0075f, 0.0001f);
        where.X.ShouldBe(0f, 0.0001f);
        where.Z.ShouldBe(0f, 0.0001f);
    }

    [Fact]
    public void Chrome_FacesTheSameWayAsThePanel()
    {
        Chrome.Around(Panel).Where.Orientation.ShouldBe(Quaternion.Identity);
    }

    [Fact]
    public void Chrome_OnATurnedPanel_DropsAlongThePanelsOwnDown()
    {
        // The offset is in the panel's frame, not the world's. A panel rolled on
        // its side has its bar out to one side, and chrome that always dropped
        // along world down would leave it hanging off a corner.
        var rolled = new Board(
            new Pose(Vector3.Zero, Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2)),
            0.5f,
            0.3f);

        Vector3 where = Chrome.Around(rolled).Where.Position;

        // Rolling a quarter turn about forward sends the panel's own down to
        // world right, so the drop shows up on X and not on Y.
        where.X.ShouldBe(0.0075f, 0.0001f);
        where.Y.ShouldBe(0f, 0.0001f);
    }

    [Fact]
    public void Chrome_AroundASquarerPanel_TakesItsHandlesFromTheShorterSide()
    {
        // The handle is a share of the shorter side, so a panel that is already
        // square gets handles sized from either — and one twice as wide as it is
        // tall does not get handles taller than half of it.
        var square = new Board(new Pose(Vector3.Zero, Quaternion.Identity), 0.4f, 0.4f);

        // 0.4 plus 0.04 of handle on each side.
        Chrome.Around(square).Width.ShouldBe(0.48f, 0.0001f);
    }

    [Fact]
    public void Chrome_IsABarAndFourHandles_AndNothingElse()
    {
        IReadOnlyList<Patch> parts = Chrome.Parts(Panel);

        parts.Select(patch => patch.What).ShouldBe(
            [
                Grabbed.Bar,
                Grabbed.TopLeftCorner,
                Grabbed.TopRightCorner,
                Grabbed.BottomLeftCorner,
                Grabbed.BottomRightCorner,
            ],
            ignoreOrder: true);
    }

    [Fact]
    public void EveryPieceOfChrome_FitsOnTheQuadItIsDrawnOn()
    {
        // The one that catches the drop being forgotten. Worked out in the
        // panel's frame instead of the chrome's, every piece sits half a bar too
        // high and the bar falls off the bottom — which in a headset reads as the
        // bar simply not being there.
        Board around = Chrome.Around(Panel);

        foreach (Patch patch in Chrome.Parts(Panel))
        {
            patch.Left.ShouldBeGreaterThanOrEqualTo(
                (-around.Width / 2) - Hair, $"{patch.What} off the left");
            patch.Right.ShouldBeLessThanOrEqualTo(
                (around.Width / 2) + Hair, $"{patch.What} off the right");
            patch.Bottom.ShouldBeGreaterThanOrEqualTo(
                (-around.Height / 2) - Hair, $"{patch.What} off the bottom");
            patch.Top.ShouldBeLessThanOrEqualTo(
                (around.Height / 2) + Hair, $"{patch.What} off the top");
        }
    }

    [Fact]
    public void TheQuad_IsNoBiggerThanTheChromeItCarries()
    {
        // The other half of the fact above. A quad that merely contains
        // everything could be any size at all; this says it is exactly the size
        // of what is on it, so each extreme touches an edge.
        Board around = Chrome.Around(Panel);
        IReadOnlyList<Patch> parts = Chrome.Parts(Panel);

        parts.Max(patch => patch.Right).ShouldBe(around.Width / 2, 0.0001f);
        parts.Min(patch => patch.Left).ShouldBe(-around.Width / 2, 0.0001f);
        parts.Max(patch => patch.Top).ShouldBe(around.Height / 2, 0.0001f);
        parts.Min(patch => patch.Bottom).ShouldBe(-around.Height / 2, 0.0001f);
    }

    [Fact]
    public void TheBar_HangsBeneathThePanel_AndSpansIt()
    {
        Patch bar = Chrome.Parts(Panel).Single(patch => patch.What == Grabbed.Bar);

        // As wide as the panel and no wider — the handles are what reach past
        // the sides.
        bar.Left.ShouldBe(-0.25f, 0.0001f);
        bar.Right.ShouldBe(0.25f, 0.0001f);

        // And entirely below where the panel's bottom edge falls in this frame.
        bar.Top.ShouldBe(-0.1425f, 0.0001f);
        bar.Bottom.ShouldBe(-0.1875f, 0.0001f);
    }

    [Fact]
    public void OnTheChrome_WhereTheBarIs_IsTheBar()
    {
        // Chrome-local metres, measured from the middle of the chrome quad. The
        // bar runs from -0.1875 to -0.1425 in that frame, so the middle of it is
        // around -0.165.
        Chrome.On(Panel, 0, -0.165f).ShouldBe(Grabbed.Bar);
    }

    [Theory]
    [InlineData(-0.25f, 0.1575f, Grabbed.TopLeftCorner)]
    [InlineData(0.25f, 0.1575f, Grabbed.TopRightCorner)]
    [InlineData(-0.25f, -0.1425f, Grabbed.BottomLeftCorner)]
    [InlineData(0.25f, -0.1425f, Grabbed.BottomRightCorner)]
    public void OnTheChrome_WhereAHandleIs_IsThatHandle(
        float across, float up, Grabbed expected)
    {
        Chrome.On(Panel, across, up).ShouldBe(expected);
    }

    [Fact]
    public void OnTheChrome_WhereThePanelShowsThrough_IsTheContent()
    {
        // The middle of the quad is transparent and the render shows through it,
        // so a laser landing there is a Commander looking at what the overlay
        // says rather than at nothing.
        Chrome.On(Panel, 0, 0).ShouldBe(Grabbed.Content);
    }

    [Fact]
    public void OnTheChrome_JustInsideTheEdgeButOnNoPiece_IsStillTheContent()
    {
        // Between the two top handles, above the panel. There is quad there and
        // nothing drawn on it, and it grabs nothing — the same answer the middle
        // gives, because neither is a thing to take hold of.
        Chrome.On(Panel, 0, 0.17f).ShouldBe(Grabbed.Content);
    }

    [Fact]
    public void OnTheChrome_AgreesWithWhatIsDrawnThere()
    {
        // The two questions have to give one answer: whatever Parts says is at a
        // spot is what On says is there. They are the same list read two ways,
        // and this is what says they stay that way.
        //
        // It caught the first real defect in this pair. A handle straddles its
        // corner, so the bottom two overlap the bar — and with the bar first in
        // the list, the middle of both bottom handles answered Bar. Two of the
        // four corners were unreachable and nothing else would have noticed.
        foreach (Patch patch in Chrome.Parts(Panel))
        {
            float middleAcross = (patch.Left + patch.Right) / 2;
            float middleUp = (patch.Bottom + patch.Top) / 2;

            Chrome.On(Panel, middleAcross, middleUp).ShouldBe(
                patch.What, $"the middle of the {patch.What} should be the {patch.What}");
        }
    }

    [Fact]
    public void OnTheChrome_WhereAHandleOverlapsTheBar_IsTheHandle()
    {
        // The specific case behind the fact above, stated on its own so that a
        // reordering of Parts cannot quietly pass by making the loop vacuous.
        Patch handle = Chrome.Parts(Panel)
            .Single(patch => patch.What == Grabbed.BottomRightCorner);

        Patch bar = Chrome.Parts(Panel).Single(patch => patch.What == Grabbed.Bar);

        // A point inside both.
        float across = handle.Left + 0.001f;
        float up = bar.Top - 0.001f;

        up.ShouldBeGreaterThan(handle.Bottom, "the fixture should be inside the handle too");

        Chrome.On(Panel, across, up).ShouldBe(Grabbed.BottomRightCorner);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-0.5f)]
    [InlineData(float.NaN)]
    public void Chrome_PartsOfABoardWithNoSize_SaysSo(float width)
    {
        var nothing = new Board(new Pose(Vector3.Zero, Quaternion.Identity), width, 0.3f);

        Should.Throw<ArgumentOutOfRangeException>(() => Chrome.Parts(nothing));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-0.5f)]
    [InlineData(float.NaN)]
    public void Chrome_AroundABoardWithNoSize_SaysSo(float width)
    {
        var nothing = new Board(new Pose(Vector3.Zero, Quaternion.Identity), width, 0.3f);

        Should.Throw<ArgumentOutOfRangeException>(() => Chrome.Around(nothing));
    }

    [Fact]
    public void Chrome_AroundABoardThatIsNowhere_SaysSo()
    {
        var nowhere = new Board(
            new Pose(new Vector3(float.NaN, 0, 0), Quaternion.Identity), 0.5f, 0.3f);

        Should.Throw<ArgumentException>(() => Chrome.Around(nowhere));
    }

    [Fact]
    public void Chrome_AroundABoardWithNoOrientationAtAll_SaysSo()
    {
        var nothing = new Board(
            new Pose(Vector3.Zero, new Quaternion(0, 0, 0, 0)), 0.5f, 0.3f);

        Should.Throw<ArgumentException>(() => Chrome.Around(nothing));
    }
}
