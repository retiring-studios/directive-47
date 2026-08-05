using System.Collections.Generic;

using D47.Panel;

using Shouldly;

using Xunit;

namespace D47.Tier1.Tests.Panel;

/// <summary>
/// What happens when the check for a newer version does not work.
///
/// <para>
/// No network, no server, no answer — none of that is a reason to interrupt
/// someone flying. A Commander in a headset who cannot reach GitHub has an
/// application that works and nothing to hear about, and the failure is between
/// Directive 47 and its log.
/// </para>
///
/// <para>
/// The same absent-not-failed line the two overlays draw, and the third place it
/// is drawn. Every reason a check fails is outside this application: a machine
/// offline, a rate limit, a proxy, a server having a bad day.
/// </para>
/// </summary>
public class FailedCheckTests
{
    /// <summary>
    /// Where this test's absences are written down. One per test, because xUnit
    /// builds the class afresh for each fact.
    /// </summary>
    private readonly List<string> _recorded = [];

    [Fact]
    public void Update_WhenTheCheckFails_LeavesTheApplicationRunning()
    {
        var updates = Updates.From(new UpdateSources.ThatCannotAnswer(), _recorded.Add);

        Should.NotThrow(updates.Look);
    }

    [Fact]
    public void Update_WhenTheCheckFails_OffersNothingRatherThanGuessing()
    {
        // Which is what "does not interrupt" comes to. There is no dialog here
        // to suppress — this class has never been able to draw one — so the way
        // a failed check could still reach the Commander is by leaving a notice
        // on the panel and the overlay, and it must not.
        var updates = Updates.From(new UpdateSources.ThatCannotAnswer(), _recorded.Add);

        updates.Look();

        updates.Waiting.ShouldBeNull(
            "a check that failed found nothing, and says so by saying nothing");
    }

    [Fact]
    public void Update_WhenTheCheckFails_LeavesARecord()
    {
        // A check that fails silently and a check that never ran look identical
        // otherwise, and the Commander wondering why they were never offered
        // 0.3.0 has no other way to find out.
        var updates = Updates.From(new UpdateSources.ThatCannotAnswer(), _recorded.Add);

        updates.Look();

        string written = _recorded.ShouldHaveSingleItem();

        written.ShouldContain(
            "newer",
            customMessage: "the record should say what was being attempted");

        written.ShouldContain(
            UpdateSources.ThatCannotAnswer.Because,
            customMessage:
                "the record should carry why it failed — 'the check failed' is a line nobody "
                + "can act on");
    }

    [Fact]
    public void Update_WhenTheCheckWorks_SaysNothingAtAll()
    {
        // The other half, and the one that keeps the log worth reading. A line
        // written on every ordinary startup is a line nobody looks at twice.
        var updates = Updates.From(new UpdateSources.Offering("0.2.0"), _recorded.Add);

        updates.Look();

        _recorded.ShouldBeEmpty("nothing was carried on without");
    }

    [Fact]
    public void Update_WhenTheCheckFailsTwice_SaysSoTwice()
    {
        // Looking again is ordinary — #143 may well ask more than once. What
        // must not happen is a second failure going missing because it looked
        // like the first.
        var updater = new UpdateSources.ThatCannotAnswer();
        var updates = Updates.From(updater, _recorded.Add);

        updates.Look();
        updates.Look();

        updater.TimesAsked.ShouldBe(2, "asking again is the point of asking again");
        _recorded.Count.ShouldBe(2, "each failure is its own event, at its own time");
    }

    [Fact]
    public void Update_WhenACheckFailsAfterOneSucceeded_TakesTheOfferBackDown()
    {
        // The case that only exists once looking twice does. An offer left
        // standing from an earlier check is a version this run never confirmed
        // is there — and accepting it would send the updater after something
        // nothing has seen.
        var updates = Updates.From(new UpdateSources.OfferingThenFailing("0.2.0"), _recorded.Add);

        updates.Look();
        updates.Waiting.ShouldBe("0.2.0");

        updates.Look();
        updates.Waiting.ShouldBeNull("the second check found nothing it could stand behind");
    }
}
