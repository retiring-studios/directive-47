using System.Collections.Generic;
using System.Linq;

using Shouldly;

using Xunit;

namespace D47.Capabilities.Tests;

/// <summary>
/// The registry is the one source help projects from, the one source the
/// model's tool schemas are built from, and the one source parity enumerates.
/// Order is part of the contract: it is the order a spoken answer reads in.
/// </summary>
public class CapabilityRegistryTests
{
    [Fact]
    public void Registry_PreservesRegistrationOrder()
    {
        var registry = new CapabilityRegistry(
            Descriptor("first"), Descriptor("second"), Descriptor("third"));

        IReadOnlyList<string> ids = [.. registry.Descriptors.Select(descriptor => descriptor.Id)];

        ids.ShouldBe(["first", "second", "third"]);
    }

    [Fact]
    public void Registry_WithNothingRegistered_IsEmpty()
    {
        new CapabilityRegistry().Descriptors.ShouldBeEmpty();
    }

    [Fact]
    public void Registry_DoesNotSeeLaterChangesToTheArrayItWasGiven()
    {
        CapabilityDescriptor[] registered = [Descriptor("first")];
        var registry = new CapabilityRegistry(registered);

        registered[0] = Descriptor("swapped");

        registry.Descriptors.ShouldHaveSingleItem().Id.ShouldBe("first");
    }

    private static CapabilityDescriptor Descriptor(string id) => new()
    {
        Id = id,
        Group = "Test",
        HelpText = $"Does the {id} thing.",
        Display = new ListDisplay(),
        Tool = new ToolSchema { Name = id, Description = $"Does the {id} thing." },
        Examples = [$"do the {id} thing"],
    };
}
