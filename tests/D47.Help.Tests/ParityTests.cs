using System;
using System.Collections.Generic;
using System.Linq;

using D47.Capabilities;

using Shouldly;

using Xunit;

namespace D47.Help.Tests;

/// <summary>
/// Parity is strict: every capability is present on every surface, and a miss
/// is a red build rather than a missed checkbox.
///
/// <para>
/// Today the per-surface dimension is satisfied by construction — one render
/// presented three ways means a capability cannot appear on one visual surface
/// and not another. The loop over surfaces is here anyway, because it is where
/// the escape hatch fails: a capability supplying its own view owes one for
/// every surface, and this is what will notice when it does not.
/// </para>
///
/// <para>
/// Lives beside the help tests only because help is the sole capability so far.
/// It moves to its own project when there is a composition root that knows
/// about all of them.
/// </para>
/// </summary>
public class ParityTests
{
    private static readonly CapabilityRegistry Registry = new(HelpCapability.Descriptor);

    [Fact]
    public void TheCapabilityEnumeration_IsNotEmpty()
    {
        // A discovery-based test that finds nothing passes, and a test that
        // passes because it checked nothing is worse than no test — it reports
        // confidence it never earned.
        Registry.Descriptors.ShouldNotBeEmpty();
        Enum.GetValues<Surface>().ShouldNotBeEmpty();
    }

    [Fact]
    public void EveryCapability_HasSomethingToShow_OnEverySurface()
    {
        List<string> unrenderable =
        [
            .. from descriptor in Registry.Descriptors
               from surface in Enum.GetValues<Surface>()
               where !CanBeShown(descriptor)
               select $"{descriptor.Id} on {surface}",
        ];

        unrenderable.ShouldBeEmpty();
    }

    [Fact]
    public void EveryCapability_HasAUniqueIdentifier()
    {
        IReadOnlyList<string> ids = [.. Registry.Descriptors.Select(descriptor => descriptor.Id)];

        ids.Distinct(StringComparer.Ordinal).Count().ShouldBe(ids.Count);
    }

    [Fact]
    public void EveryCapability_CanBeReached_ByModelAndByCommander()
    {
        // Unreachable in two different ways. Without a tool schema the model
        // cannot call it; without example utterances neither the model nor the
        // Commander has any idea it is there.
        List<string> unreachable =
        [
            .. from descriptor in Registry.Descriptors
               where string.IsNullOrWhiteSpace(descriptor.Tool.Name)
                     || string.IsNullOrWhiteSpace(descriptor.Tool.Description)
                     || descriptor.Examples.Count == 0
               select descriptor.Id,
        ];

        unreachable.ShouldBeEmpty();
    }

    private static bool CanBeShown(CapabilityDescriptor descriptor) =>
        !string.IsNullOrWhiteSpace(descriptor.Id)
        && !string.IsNullOrWhiteSpace(descriptor.Group)
        && !string.IsNullOrWhiteSpace(descriptor.HelpText);
}
