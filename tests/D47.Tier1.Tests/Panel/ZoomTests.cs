using System.Collections.Generic;
using System.Linq;

using D47.Panel;

using Shouldly;

using Xunit;

namespace D47.Tier1.Tests.Panel;

/// <summary>
/// How big the panel draws what it is showing, which is a browser's zoom and
/// not a resize.
///
/// <para>
/// Pure logic, deliberately. Where the levels are, what the ends do, and what a
/// remembered value turns into are decisions; applying the result to a control
/// is an adapter's job and needs a window to assert. Keeping them apart is what
/// lets the decisions be checked without one.
/// </para>
/// </summary>
public class ZoomTests
{
    [Fact]
    public void Zoom_WithNothingRemembered_StartsAtLifeSize()
    {
        using var folder = new TemporaryStore();
        var zoom = new Zoom(folder.Open(), Ignored);

        zoom.Factor.ShouldBe(1.0);
        zoom.AsSaid.ShouldBe("100%");
    }

    [Fact]
    public void Zoom_WhenSteppedIn_MovesUpOneLevelRatherThanByAFixedAmount()
    {
        // A browser's levels, not a multiplier. Steps of a fixed percentage
        // either crawl at the bottom or leap at the top, and the levels are
        // where they are because somebody wanted 110% to exist and 105% not to.
        using var folder = new TemporaryStore();
        var zoom = new Zoom(folder.Open(), Ignored);

        zoom.In();

        zoom.AsSaid.ShouldBe("110%");

        zoom.In();

        zoom.AsSaid.ShouldBe("125%");
    }

    [Fact]
    public void Zoom_WhenSteppedOut_MovesDownOneLevel()
    {
        using var folder = new TemporaryStore();
        var zoom = new Zoom(folder.Open(), Ignored);

        zoom.Out();

        zoom.AsSaid.ShouldBe("90%");
    }

    [Fact]
    public void Zoom_AtTheEnds_StopsRatherThanRunningAway()
    {
        // Stepping past the last level does nothing, rather than throwing or
        // walking off into a size nothing can be read at. A Commander leaning on
        // Ctrl and the wheel is the ordinary way to arrive here.
        using var folder = new TemporaryStore();
        var zoom = new Zoom(folder.Open(), Ignored);

        for (int step = 0; step < 50; step++)
        {
            zoom.In();
        }

        zoom.Factor.ShouldBe(Zoom.Levels.Max());

        for (int step = 0; step < 50; step++)
        {
            zoom.Out();
        }

        zoom.Factor.ShouldBe(Zoom.Levels.Min());
    }

    [Fact]
    public void Zoom_WhenReset_GoesBackToLifeSize()
    {
        using var folder = new TemporaryStore();
        var zoom = new Zoom(folder.Open(), Ignored);

        zoom.In();
        zoom.In();
        zoom.Reset();

        zoom.AsSaid.ShouldBe("100%");
    }

    [Fact]
    public void Zoom_WhenChanged_IsRememberedForNextTime()
    {
        // The Commander who zooms for legibility should not have to do it again
        // every launch. Written through the same store the overlay's opacity and
        // position go to, under a key spelled the way it would be said aloud.
        using var folder = new TemporaryStore();

        var setting = new Zoom(folder.Open(), Ignored);
        setting.In();
        setting.In();

        var nextLaunch = new Zoom(folder.Open(), Ignored);

        nextLaunch.AsSaid.ShouldBe("125%");
    }

    [Fact]
    public void Zoom_WithSomethingUnreadableRemembered_SaysSoAndCarriesOnAtLifeSize()
    {
        // The file is one somebody opens and edits by hand, so a value that is
        // not a zoom is a thing that happens. Loud rather than silent: starting
        // at 100% without a word looks identical to the edit having worked.
        using var folder = new TemporaryStore();
        folder.Open().Write(Zoom.HowBig, "enormous");

        List<string> complaints = [];
        var zoom = new Zoom(folder.Open(), complaints.Add);

        zoom.Factor.ShouldBe(1.0);

        complaints.ShouldHaveSingleItem()
            .ShouldContain(
                Zoom.HowBig,
                customMessage: "the complaint should name the key somebody has to go and fix");
    }

    private static void Ignored(string complaint)
    {
        // Most of these have nothing to complain about, and a test that asserted
        // on silence would be asserting the absence of a message it never asked
        // for.
    }
}
