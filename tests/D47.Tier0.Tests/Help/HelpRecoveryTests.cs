using System;
using System.Collections.Generic;

using D47.Capabilities;
using D47.Help;

using Shouldly;

using Xunit;

namespace D47.Tier0.Tests.Help;

/// <summary>
/// Help doubles as failure recovery. An utterance that resolved to nothing gets
/// the nearest name help actually knows — "I don't recognize 'fewl'. Did you
/// mean fuel?" — because a dead end is the one answer that leaves the Commander
/// with nothing to do next.
///
/// <para>
/// The pool is what help already owns: capability ids and group names. Example
/// utterances are deliberately not in it. They are examples, not a matcher, and
/// matching them against speech-to-text output is the failure mode
/// <c>docs/decisions.md</c> rules out.
/// </para>
/// </summary>
public class HelpRecoveryTests
{
    [Fact]
    public void Help_ForAnUnresolvedUtterance_OffersTheNearestCanonicalName()
    {
        var help = new HelpCapability(new CapabilityRegistry(
            Descriptor("fuel", "Your ship"),
            Descriptor("location", "Where you are")));

        IReadOnlyList<string> lines = help.Recover("fewl");

        lines.ShouldBe(
        [
            "I don't recognize \"fewl\".",
            "Did you mean fuel?",
        ]);
    }

    [Fact]
    public void Help_ForAnUtteranceNearAGroupName_OffersTheGroup()
    {
        var help = new HelpCapability(new CapabilityRegistry(
            Descriptor("fuel", "Your ship")));

        IReadOnlyList<string> lines = help.Recover("your shp");

        lines.ShouldBe(
        [
            "I don't recognize \"your shp\".",
            "Did you mean Your ship?",
        ]);
    }

    [Fact]
    public void Help_ForAnUtteranceNearNothing_SaysSoAndOffersTheGroups()
    {
        var help = new HelpCapability(new CapabilityRegistry(
            Descriptor("fuel", "Your ship"),
            Descriptor("location", "Where you are")));

        IReadOnlyList<string> lines = help.Recover("xyzzy");

        lines.ShouldBe(
        [
            "I don't recognize \"xyzzy\".",
            "Here's what I can help with:",
            "Your ship",
            "Where you are",
        ]);
    }

    [Fact]
    public void Help_ForAnUtteranceNearTwoNames_OffersTheNearer()
    {
        var help = new HelpCapability(new CapabilityRegistry(
            Descriptor("cargo", "Your ship"),
            Descriptor("carto", "Your ship")));

        help.Recover("cartn").ShouldContain("Did you mean carto?");
    }

    [Fact]
    public void Help_ForAnUtteranceNearTwoNamesEqually_OffersTheFirstRegistered()
    {
        // Registration order is already the order a spoken answer reads in.
        // Nothing better distinguishes a tie, and picking arbitrarily would
        // make the same utterance answer differently between runs.
        var help = new HelpCapability(new CapabilityRegistry(
            Descriptor("cargo", "Your ship"),
            Descriptor("cargb", "Your ship")));

        help.Recover("cargx").ShouldContain("Did you mean cargo?");
    }

    [Theory]
    [InlineData("FEWL")]
    [InlineData("  fewl  ")]
    [InlineData("Fewl")]
    public void Help_Recovering_IgnoresCaseAndSurroundingWhitespace(string utterance)
    {
        var help = new HelpCapability(new CapabilityRegistry(Descriptor("fuel", "Your ship")));

        help.Recover(utterance).ShouldContain("Did you mean fuel?");
    }

    [Fact]
    public void Help_Recovering_EchoesTheUtteranceInTheCaseItArrived()
    {
        // The Commander needs to hear what was heard. Echoing the case-folded
        // form hides half of the transcription error that caused the miss.
        var help = new HelpCapability(new CapabilityRegistry(Descriptor("fuel", "Your ship")));

        help.Recover("  FEWL  ").ShouldContain("I don't recognize \"FEWL\".");
    }

    [Fact]
    public void Help_ForAnUtteranceThatIsAlreadyACanonicalName_StillOffersIt()
    {
        // Reaching recovery at all means something upstream failed to resolve
        // it. Confirming the name is the useful answer; staying silent because
        // the distance was zero is not.
        var help = new HelpCapability(new CapabilityRegistry(Descriptor("fuel", "Your ship")));

        help.Recover("fuel").ShouldContain("Did you mean fuel?");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Help_ForAnEmptyUtterance_FailsLoudly(string utterance)
    {
        var help = new HelpCapability(new CapabilityRegistry(Descriptor("fuel", "Your ship")));

        Should.Throw<ArgumentException>(() => help.Recover(utterance));
    }

    [Fact]
    public void Help_AnswerForAnUtterance_IsAResultConformingToItsDeclaredDisplayModel()
    {
        var help = new HelpCapability(new CapabilityRegistry(
            HelpCapability.Descriptor,
            Descriptor("fuel", "Your ship")));

        ListResult listed = help.AnswerForUtterance("fewl").ShouldBeOfType<ListResult>();

        listed.CapabilityId.ShouldBe(HelpCapability.Descriptor.Id);
        listed.Items.ShouldBe(
        [
            "I don't recognize \"fewl\".",
            "Did you mean fuel?",
        ]);
    }

    private static CapabilityDescriptor Descriptor(string id, string group) => new()
    {
        Id = id,
        Group = group,
        HelpText = $"Does the {id} thing.",
        Display = new ListDisplay(),
        Examples = [$"do the {id} thing"],
    };
}
