using System;
using System.Collections.Generic;
using System.Linq;

using D47.Capabilities;

using Shouldly;

using Xunit;

namespace D47.Help.Tests;

/// <summary>
/// Help is a projection of the capability registry: its answer is derived from
/// the registered descriptors by template, never authored by hand and never
/// asked of the model. Registering a capability is therefore the only thing
/// needed to make it appear here.
///
/// <para>
/// It is a hierarchy — groups, then the capabilities in one group, then one
/// capability's detail — because forty lines spoken aloud is not an answer.
/// </para>
/// </summary>
public class HelpTests
{
    [Fact]
    public void Help_WhenAsked_ListsGroupsRatherThanEveryCapability()
    {
        var registry = new CapabilityRegistry(
            Descriptor("location", "Where you are", "Tells you what system and body you're at."),
            Descriptor("distance", "Where you are", "Measures the jump between two systems."),
            Descriptor("fuel", "Your ship", "Reports how much fuel you have."));

        IReadOnlyList<string> groups = new HelpCapability(registry).ListGroups();

        groups.ShouldBe(["Where you are", "Your ship"]);
    }

    [Fact]
    public void Help_WhenAsked_ListsEveryRegisteredCapability()
    {
        // The flat listing this replaced asserted the same thing directly.
        // Reachability is what that criterion was always about: a capability
        // nobody can get to is a capability nobody has.
        var registry = new CapabilityRegistry(
            Descriptor("location", "Where you are", "Tells you what system and body you're at."),
            Descriptor("fuel", "Your ship", "Reports how much fuel you have."));

        IReadOnlyList<string> reached = ReachableByDrilling(registry);

        reached.ShouldBe(
        [
            "Tells you what system and body you're at.",
            "Reports how much fuel you have.",
        ], ignoreOrder: true);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    public void Help_WhenACapabilityIsAdded_IncludesItWithNoEditToHelp(int count)
    {
        CapabilityDescriptor[] registered =
        [
            .. Enumerable.Range(0, count)
                .Select(index => Descriptor(
                    $"cap{index}", $"Group {index % 3}", $"Does thing {index}.")),
        ];
        var registry = new CapabilityRegistry(registered);

        IReadOnlyList<string> reached = ReachableByDrilling(registry);

        reached.Count.ShouldBe(count);
        foreach (CapabilityDescriptor descriptor in registered)
        {
            reached.ShouldContain(descriptor.HelpText);
        }
    }

    [Fact]
    public void Help_WhenAsked_NamesEachGroupOnceInRegistrationOrder()
    {
        var registry = new CapabilityRegistry(
            Descriptor("fuel", "Your ship", "Reports how much fuel you have."),
            Descriptor("location", "Where you are", "Tells you what system you're at."),
            Descriptor("cargo", "Your ship", "Reports what you're carrying."));

        IReadOnlyList<string> groups = new HelpCapability(registry).ListGroups();

        groups.ShouldBe(["Your ship", "Where you are"]);
    }

    [Fact]
    public void Help_ForAGroup_ListsTheCapabilitiesInThatGroupAndNoOther()
    {
        var registry = new CapabilityRegistry(
            Descriptor("location", "Where you are", "Tells you what system you're at."),
            Descriptor("fuel", "Your ship", "Reports how much fuel you have."),
            Descriptor("cargo", "Your ship", "Reports what you're carrying."));

        IReadOnlyList<string> lines = new HelpCapability(registry).ListCapabilitiesIn("Your ship");

        lines.ShouldBe(
        [
            "Reports how much fuel you have.",
            "Reports what you're carrying.",
        ]);
    }

    [Fact]
    public void Help_ForACapability_ReturnsItsDetailAndExampleUtterances()
    {
        var registry = new CapabilityRegistry(new CapabilityDescriptor
        {
            Id = "fuel",
            Group = "Your ship",
            HelpText = "Reports how much fuel you have.",
            Display = new ListDisplay(),
            Examples = ["how's my fuel", "fuel check"],
        });

        IReadOnlyList<string> lines = new HelpCapability(registry).Describe("fuel");

        lines.ShouldBe(
        [
            "Reports how much fuel you have.",
            "Try saying:",
            "how's my fuel",
            "fuel check",
        ]);
    }

    [Fact]
    public void Help_ForAGroup_DrillsIntoItsCapabilitiesAndTheirDetail()
    {
        var registry = new CapabilityRegistry(
            Descriptor("location", "Where you are", "Tells you what system you're at."),
            Descriptor("fuel", "Your ship", "Reports how much fuel you have."));
        var help = new HelpCapability(registry);

        // Each level is asked with a name the level above it produced, which is
        // the whole point of the hierarchy: three short answers, not one long
        // one.
        help.ListGroups().ShouldContain("Your ship");
        help.ListCapabilitiesIn("Your ship").ShouldHaveSingleItem()
            .ShouldBe("Reports how much fuel you have.");
        help.Describe("fuel").ShouldContain("Say the fuel thing.");
    }

    [Fact]
    public void Help_ForAnUnknownGroup_FailsLoudlyRatherThanReturningNothing()
    {
        // An empty listing here would read as "that group has nothing in it",
        // which is a different and untrue answer. Recovering from a name that
        // resolves to nothing is #55's job, in front of this.
        var registry = new CapabilityRegistry(
            Descriptor("fuel", "Your ship", "Reports how much fuel you have."));

        Should.Throw<ArgumentException>(
            () => new HelpCapability(registry).ListCapabilitiesIn("Astronavigation"));
    }

    [Fact]
    public void Help_ForAnUnknownCapability_FailsLoudlyRatherThanReturningNothing()
    {
        var registry = new CapabilityRegistry(
            Descriptor("fuel", "Your ship", "Reports how much fuel you have."));

        Should.Throw<ArgumentException>(() => new HelpCapability(registry).Describe("neutron"));
    }

    [Fact]
    public void Help_ContributesItsOwnDescriptor_LikeAnyOtherCapability()
    {
        var registry = new CapabilityRegistry(HelpCapability.Descriptor);
        var help = new HelpCapability(registry);

        HelpCapability.Descriptor.Id.ShouldBe("help");
        help.ListGroups().ShouldHaveSingleItem().ShouldBe(HelpCapability.Descriptor.Group);
        help.ListCapabilitiesIn(HelpCapability.Descriptor.Group).ShouldHaveSingleItem()
            .ShouldBe(HelpCapability.Descriptor.HelpText);
        help.Describe("help").ShouldContain(HelpCapability.Descriptor.HelpText);
    }

    [Fact]
    public void Help_EveryAnswer_IsAResultConformingToItsDeclaredDisplayModel()
    {
        var registry = new CapabilityRegistry(
            HelpCapability.Descriptor,
            Descriptor("fuel", "Your ship", "Reports how much fuel you have."));
        var help = new HelpCapability(registry);

        HelpCapability.Descriptor.Display.ShouldBeOfType<ListDisplay>();

        CapabilityResult[] answers =
        [
            help.Answer(),
            help.AnswerForGroup("Your ship"),
            help.AnswerForCapability("fuel"),
        ];

        foreach (CapabilityResult answer in answers)
        {
            ListResult listed = answer.ShouldBeOfType<ListResult>();
            listed.CapabilityId.ShouldBe(HelpCapability.Descriptor.Id);
            listed.Items.ShouldNotBeEmpty();
        }
    }

    [Fact]
    public void Help_Answer_ListsTheGroupsItWouldSpeak()
    {
        var registry = new CapabilityRegistry(
            HelpCapability.Descriptor,
            Descriptor("fuel", "Your ship", "Reports how much fuel you have."));
        var help = new HelpCapability(registry);

        ListResult listed = help.Answer().ShouldBeOfType<ListResult>();

        listed.Items.ShouldBe([HelpCapability.Descriptor.Group, "Your ship"]);
    }

    /// <summary>
    /// Every capability help can be reached by walking the hierarchy top to
    /// bottom, as a Commander would.
    /// </summary>
    private static IReadOnlyList<string> ReachableByDrilling(CapabilityRegistry registry)
    {
        var help = new HelpCapability(registry);

        return [.. help.ListGroups().SelectMany(help.ListCapabilitiesIn)];
    }

    private static CapabilityDescriptor Descriptor(string id, string group, string helpText) => new()
    {
        Id = id,
        Group = group,
        HelpText = helpText,
        Display = new ListDisplay(),
        Examples = [$"Say the {id} thing."],
    };
}
