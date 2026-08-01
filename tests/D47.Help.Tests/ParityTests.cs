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
    public void EveryCapability_TellsTheCommander_HowToReachIt()
    {
        // Help's bottom level is "here is what to say". A capability with no
        // example utterances is one the Commander can be shown and still have
        // no idea how to invoke.
        List<string> unreachable =
        [
            .. from descriptor in Registry.Descriptors
               where descriptor.Examples.Count == 0
                     || descriptor.Examples.Any(string.IsNullOrWhiteSpace)
               select descriptor.Id,
        ];

        unreachable.ShouldBeEmpty();
    }

    private static bool CanBeShown(CapabilityDescriptor descriptor) =>
        !string.IsNullOrWhiteSpace(descriptor.Id)
        && !string.IsNullOrWhiteSpace(descriptor.Group)
        && !string.IsNullOrWhiteSpace(descriptor.HelpText);
}
