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
/// </summary>
public class HelpTests
{
    [Fact]
    public void Help_WhenAsked_ListsEveryRegisteredCapability()
    {
        var registry = new CapabilityRegistry(
            Descriptor("location", "Tells you what system and body you're at."),
            Descriptor("fuel", "Reports how much fuel you have."));

        IReadOnlyList<string> lines = new HelpCapability(registry).ListCapabilities();

        lines.ShouldBe(
        [
            "Tells you what system and body you're at.",
            "Reports how much fuel you have.",
        ]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    public void Help_WhenACapabilityIsAdded_IncludesItWithNoEditToHelp(int count)
    {
        CapabilityDescriptor[] registered =
            [.. Enumerable.Range(0, count).Select(i => Descriptor($"cap{i}", $"Does thing {i}."))];

        IReadOnlyList<string> lines = new HelpCapability(new CapabilityRegistry(registered))
            .ListCapabilities();

        lines.Count.ShouldBe(count);
        foreach (CapabilityDescriptor descriptor in registered)
        {
            lines.ShouldContain(descriptor.HelpText);
        }
    }

    [Fact]
    public void Help_ContributesItsOwnDescriptor_LikeAnyOtherCapability()
    {
        var registry = new CapabilityRegistry(HelpCapability.Descriptor);

        IReadOnlyList<string> lines = new HelpCapability(registry).ListCapabilities();

        HelpCapability.Descriptor.Id.ShouldBe("help");
        lines.ShouldHaveSingleItem().ShouldBe(HelpCapability.Descriptor.HelpText);
    }

    [Fact]
    public void Help_Answer_IsAResultConformingToItsDeclaredDisplayModel()
    {
        var registry = new CapabilityRegistry(
            HelpCapability.Descriptor,
            Descriptor("fuel", "Reports how much fuel you have."));

        HelpCapability.Descriptor.Display.ShouldBeOfType<ListDisplay>();

        CapabilityResult answer = new HelpCapability(registry).Answer();

        ListResult listed = answer.ShouldBeOfType<ListResult>();
        listed.CapabilityId.ShouldBe(HelpCapability.Descriptor.Id);
        listed.Items.ShouldBe(
        [
            HelpCapability.Descriptor.HelpText,
            "Reports how much fuel you have.",
        ]);
    }

    private static CapabilityDescriptor Descriptor(string id, string helpText) => new()
    {
        Id = id,
        Group = "Where you are",
        HelpText = helpText,
        Display = new ListDisplay(),
    };
}
