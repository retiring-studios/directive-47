using System.Collections.Generic;

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

    private static CapabilityDescriptor Descriptor(string id, string helpText) => new()
    {
        Id = id,
        Group = "Where you are",
        HelpText = helpText,
    };
}
