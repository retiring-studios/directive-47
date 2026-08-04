using System.Windows;

using D47.Capabilities;

using Shouldly;

using Xunit;

namespace D47.Render.Tests;

/// <summary>
/// How big the render wants to be, asked in the one place that works it out.
///
/// <para>
/// Three surfaces asked this independently and each wrote the same four steps —
/// build a throwaway, measure against infinity, arrange, update the layout — a
/// sequence whose failure mode is not an exception but a plausible wrong number.
/// [#179](https://github.com/retiring-studios/directive-47/issues/179) put it in
/// one place. These are the two ways it has actually been got wrong, so that
/// getting it wrong again fails here rather than on a surface.
/// </para>
///
/// <para>
/// Nothing here calls the production code to work out what it expects. The three
/// test suites that assert sizes still compute them themselves, deliberately —
/// an expectation that runs the code under test is not an expectation.
/// </para>
/// </summary>
public class RenderSizeTests
{
    [Fact]
    public void RenderSize_ForMoreToShow_IsBiggerRatherThanTheSizeOfAnEmptyBox()
    {
        // The layout pass, pinned by its consequence. The answer's template is
        // chosen by a resource lookup that needs the control connected, so
        // measuring without one returns an almost-empty box at a default size —
        // the same default whatever the answer says. That shipped once: the
        // overlay came out too narrow for its own title bar while looking
        // internally consistent.
        //
        // Two answers rather than one absolute number, because the number is a
        // property of the typeface and the padding and would have to be updated
        // whenever either moved. What cannot change is that eight lines are
        // taller than one.
        Size one = SizeOf(Answering("Main tank: 24 tonnes."));
        Size many = SizeOf(Answering(
            "Main tank: 24 tonnes.",
            "Reserve: 0.6 tonnes.",
            "Scoop rate: 875 kilograms a second.",
            "Range laden: 34.7 light years.",
            "Range unladen: 41.2 light years.",
            "Mass laden: 412 tonnes.",
            "Fuel used last jump: 3.1 tonnes.",
            "Jumps to empty: 7."));

        many.Height.ShouldBeGreaterThan(
            one.Height,
            "eight lines take more room than one, and a render that says otherwise "
            + "measured a box its template never filled");

        one.Height.ShouldBeGreaterThan(
            0, "a render with an answer in it wants some room to put it in");
    }

    [Fact]
    public void RenderSize_ForAControlWithAParent_IsNotWhatIsAsked()
    {
        // The other recorded way to get it wrong. Measuring the instance already
        // in a window asks what it wants inside whatever it is sitting in rather
        // than what it wants — which came out exactly one padding narrower than
        // the truth, 64 device-independent pixels, and looked plausible.
        //
        // So what comes back has to belong to nobody. A control handed out with
        // a parent is one somebody will measure in place without noticing.
        object? parent = Rendering.OnAWpfThread(
            () => CapabilityView.LaidOutFor(Answering("Main tank: 24 tonnes.")).Parent);

        parent.ShouldBeNull(
            "the render is asked how big it wants to be, which only an instance "
            + "belonging to nobody can answer");
    }

    /// <summary>
    /// How big the laid-out render wants to be, read on the thread that built
    /// it. A control cannot outlive its thread, so only the size comes back.
    /// </summary>
    private static Size SizeOf(Answer answer) =>
        Rendering.OnAWpfThread(() => CapabilityView.LaidOutFor(answer).DesiredSize);

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
