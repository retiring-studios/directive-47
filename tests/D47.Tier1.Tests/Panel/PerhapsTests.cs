using System;
using System.Collections.Generic;

using D47.Panel;

using Shouldly;

using Xunit;

namespace D47.Tier1.Tests.Panel;

/// <summary>
/// Something Directive 47 may have to carry on without, and the reason it is
/// carrying on.
///
/// <para>
/// The point is not that the value may be missing — a nullable already says that
/// — but that a missing one cannot be *reached* without handing over somewhere to
/// put the reason. Twice now this project has chosen absent over dying
/// ([#101](https://github.com/retiring-studios/directive-47/issues/101),
/// [#103](https://github.com/retiring-studios/directive-47/issues/103)), and one
/// of those has been silent since the day it was written.
/// </para>
/// </summary>
public class PerhapsTests
{
    [Fact]
    public void Perhaps_WhenTheThingIsThere_HandsItOverAndSaysNothing()
    {
        List<string> recorded = [];

        var present = Perhaps<string>.Of("an overlay");

        present.Or(recorded.Add).ShouldBe("an overlay");

        recorded.ShouldBeEmpty("nothing was carried on without, so there is nothing to record");
    }

    [Fact]
    public void Perhaps_WhenTheThingIsAbsent_RecordsWhyBeforeAnybodyGetsTheNull()
    {
        List<string> recorded = [];

        var absent = Perhaps<string>.Absent("this machine cannot composite");

        absent.Or(recorded.Add).ShouldBeNull();

        recorded.ShouldHaveSingleItem().ShouldBe("this machine cannot composite");
    }

    [Fact]
    public void Perhaps_WhenAskedTwice_RecordsEachTime()
    {
        // Not memoized, deliberately. Asking twice is a caller doing something
        // odd, and quietly recording it once would hide that rather than the
        // reverse. There is exactly one ask per absence in the application.
        List<string> recorded = [];

        var absent = Perhaps<string>.Absent("no compositor");

        absent.Or(recorded.Add);
        absent.Or(recorded.Add);

        recorded.Count.ShouldBe(2);
    }

    [Fact]
    public void Perhaps_WhenItIsTheDefaultRatherThanOneAnybodyMade_RefusesToHandBackASilentNull()
    {
        // The hole a struct cannot close with constructors. default(Perhaps<T>)
        // is neither present nor explained, and handing back its null would be
        // exactly the unexplained absence this type promises never to produce —
        // so it is caught on the way out instead.
        Perhaps<string> never = default;

        Should.Throw<InvalidOperationException>(() => never.Or(_ => { }));
    }

    [Fact]
    public void Perhaps_WhenAnAbsenceIsGivenNoWords_RefusesToBeMade()
    {
        // A blank reason is the silence this was built against, spelled
        // differently. Better to fail where somebody wrote it than to write an
        // empty line into the log.
        Should.Throw<ArgumentException>(() => Perhaps<string>.Absent("   "));
    }

    [Fact]
    public void Perhaps_WhenNothingWasGivenToRecordWith_SaysSoRatherThanGoingQuiet()
    {
        // The one way to defeat this type is to hand it a recorder that is not
        // there, and that is our defect rather than the machine's — so it throws
        // like any other.
        var absent = Perhaps<string>.Absent("no compositor");

        Should.Throw<ArgumentNullException>(() => absent.Or(null!));
    }
}
