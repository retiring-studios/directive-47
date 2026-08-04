using Shouldly;

using Xunit;

namespace D47.Panel.Tests;

/// <summary>
/// What accepting an update does, and when.
///
/// <para>
/// The whole of this feature is a question of timing. Velopack's updater waits a
/// minute for the application to exit and then stops waiting, so telling it to
/// apply at the moment the Commander accepts would spend that minute while
/// somebody is still flying — and the one thing an update must not do is take a
/// Commander out of a headset mid-session. Accepting therefore records intent
/// and nothing else.
/// </para>
///
/// <para>
/// Tier 1 and in process, against a stand-in. A real updater cannot be asked
/// whether it would have applied something, and a test that let it find out
/// would install over the machine running it.
/// </para>
/// </summary>
public class UpdateTests
{
    [Fact]
    public void Update_WhenAccepted_IsAppliedOnTheWayOutAndNotBefore()
    {
        var updater = new UpdateSourceOffering("0.2.0");
        var updates = Updates.From(updater);

        updates.Look();

        updates.Accept();

        // The half that matters. An accepted update that installs immediately is
        // exactly the mid-session interruption this feature exists to prevent,
        // and it would pass a test that only checked the end state.
        updater.Applied.ShouldBeFalse(
            "accepting records intent; the Commander is still flying");

        updates.Exiting();

        updater.Applied.ShouldBeTrue("the way out is when it was always going to happen");
    }

    [Fact]
    public void Update_WhenAccepted_IsFetchedWhileTheApplicationIsStillRunning()
    {
        // The other side of the same rule. The updater's minute starts when it
        // is told to apply, so anything slow has to be done before then —
        // fetching on the way out would spend the exit waiting on a download and
        // could miss the window entirely.
        var updater = new UpdateSourceOffering("0.2.0");
        var updates = Updates.From(updater);

        updates.Look();

        updates.Accept();

        updater.Fetched.ShouldBeTrue(
            "the slow part belongs to the running session, not to the shutdown");
    }

    [Fact]
    public void Update_WhenDeclined_LeavesTheRunningApplicationUntouched()
    {
        var updater = new UpdateSourceOffering("0.2.0");
        var updates = Updates.From(updater);

        updates.Look();

        updates.Decline();
        updates.Exiting();

        updater.Applied.ShouldBeFalse("declining is an answer, and it is no");
        updater.Fetched.ShouldBeFalse("nothing declined is worth downloading");
    }

    [Fact]
    public void Update_WhenAcceptedAndThenDeclined_TakesTheLastAnswer()
    {
        // Changing your mind is ordinary, and the notice stays up until it is
        // answered. The last word wins, and a fetch already begun is not a
        // commitment to install it.
        var updater = new UpdateSourceOffering("0.2.0");
        var updates = Updates.From(updater);

        updates.Look();

        updates.Accept();
        updates.Decline();
        updates.Exiting();

        updater.Applied.ShouldBeFalse("the Commander's last answer was no");
    }

    [Fact]
    public void Update_WhenNoneIsWaiting_HasNothingToOfferAndDoesNothingOnExit()
    {
        var updater = new UpdateSourceOffering(nothing: true);
        var updates = Updates.From(updater);

        updates.Look();

        updates.Waiting.ShouldBeNull();

        // Accepting what was never offered is not an error, it is nothing. The
        // surfaces have no notice to answer, so this can only be reached by a
        // defect above — and throwing here would turn that into a crash on the
        // way out.
        Should.NotThrow(updates.Accept);
        Should.NotThrow(updates.Exiting);

        updater.Applied.ShouldBeFalse();
        updater.Fetched.ShouldBeFalse();
    }

    [Fact]
    public void Update_WhenOneIsWaiting_SaysWhichVersion()
    {
        // What #143 will put on the panel and the overlay. A notice that cannot
        // name the version is one nobody can decide about.
        var updates = Updates.From(new UpdateSourceOffering("0.2.0"));

        updates.Waiting.ShouldBeNull("nothing has been asked yet");

        updates.Look();

        updates.Waiting.ShouldBe("0.2.0");
    }

    /// <summary>
    /// An updater that answers, and remembers what it was asked to do.
    /// </summary>
    private sealed class UpdateSourceOffering : IUpdateSource
    {
        private readonly string? _version;

        internal UpdateSourceOffering(string version)
        {
            _version = version;
        }

        internal UpdateSourceOffering(bool nothing)
        {
            _version = nothing ? null : "0.2.0";
        }

        internal bool Fetched { get; private set; }

        internal bool Applied { get; private set; }

        public string? Waiting() => _version;

        public void Fetch() => Fetched = true;

        public void ApplyOnExit() => Applied = true;
    }
}
