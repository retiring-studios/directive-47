using D47.Capabilities;

using Shouldly;

using Xunit;

namespace D47.Render.Tests;

/// <summary>
/// The line the render draws above an answer when there is something the
/// application needs to say.
///
/// <para>
/// In the render rather than in any window's chrome, and that is the whole
/// reason this file exists. The headset is drawn from
/// <see cref="CapabilityView"/> alone — no window, no title bar, no strip along
/// the bottom — so a notice put anywhere else reaches the panel and the game
/// overlay and never the surface a Commander is actually wearing. Every surface
/// gets it here for the same reason they all get the palette and the typeface.
/// </para>
/// </summary>
public class NoticeTests
{
    [Fact]
    public void Render_WhenAnUpdateIsWaiting_SaysSoAboveTheAnswer()
    {
        // Above, not below. The answer is what was asked for and the notice is
        // not, so it goes where it is read first and then got out of the way of
        // — the same order a headline sits in.
        var presented = new Presentation
        {
            Answer = Answering("Main tank: 24 tonnes."),
            UpdateWaiting = "0.2.0",
        };

        Rendering.LinesFor(presented).ShouldBe(
        [
            "Update available — 0.2.0",
            "Main tank: 24 tonnes.",
        ]);
    }

    [Fact]
    public void Render_WhenNothingIsWaiting_SaysNothingAboutUpdates()
    {
        // The ordinary case, which is every run on a current copy. A render that
        // reserved a line for a notice that is not there would cost every
        // surface the space, and the overlay is sized to what the render wants.
        var presented = new Presentation { Answer = Answering("Main tank: 24 tonnes.") };

        Rendering.LinesFor(presented).ShouldBe(["Main tank: 24 tonnes."]);
    }

    [Fact]
    public void Render_WhenAnUpdateIsWaiting_NamesTheVersion()
    {
        // A notice that cannot say which version is one nobody can decide
        // about — and after 0.3.0 lands, one nobody can tell is stale.
        var presented = new Presentation
        {
            Answer = Answering("Main tank: 24 tonnes."),
            UpdateWaiting = "1.4.2",
        };

        Rendering.LinesFor(presented)[0].ShouldContain("1.4.2");
    }

    private static Answer Answering(params string[] lines) => new()
    {
        Descriptor = new CapabilityDescriptor
        {
            Id = "fuel",
            Group = "Your ship",
            HelpText = "Does the fuel thing.",
            Display = new ListDisplay(),
            Examples = ["do the fuel thing"],
        },
        Result = new ListResult { CapabilityId = "fuel", Items = lines },
    };
}
