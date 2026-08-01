using System;
using System.Collections.Generic;
using System.Linq;

using D47.Capabilities;

namespace D47.Help;

/// <summary>
/// Answers "what can you do" as a projection of the capability registry —
/// derived by template from the registered descriptors, never authored by hand
/// and never asked of the model.
///
/// <para>
/// The two alternatives are both real failure modes. A hand-written help page
/// goes stale the first time a capability is added and nobody remembers to
/// edit it. Asking the model produces confident, invented capabilities: "yes,
/// I can plot you a neutron route", two waves before that exists.
/// </para>
///
/// <para>
/// The answer is a hierarchy — groups, then the capabilities in one group, then
/// one capability's detail — because forty lines spoken aloud is not an answer.
/// Each level is asked with a name the level above it produced. Recovering from
/// a name that resolves to nothing belongs in front of this, not here.
/// </para>
/// </summary>
public sealed class HelpCapability
{
    /// <summary>
    /// The line introducing a capability's example utterances, at the bottom of
    /// the hierarchy. Reads the same spoken as it does rendered.
    /// </summary>
    private const string TrySaying = "Try saying:";

    /// <summary>
    /// Help's own declaration. Help is a capability like any other and appears
    /// in its own listing — if it did not, the one thing that tells the
    /// Commander what exists would be the one thing not accounted for.
    /// </summary>
    public static CapabilityDescriptor Descriptor { get; } = new()
    {
        Id = "help",
        Group = "Getting around",
        HelpText = "Lists what I can do, and narrows down from there.",
        Display = new ListDisplay(),
        Examples =
        [
            "what can you do",
            "help",
            "what else do you know",
        ],
    };

    private readonly CapabilityRegistry _registry;

    /// <summary>
    /// Creates help over the given registry.
    /// </summary>
    /// <param name="registry">The capabilities to project.</param>
    public HelpCapability(CapabilityRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        _registry = registry;
    }

    /// <summary>
    /// The top of the hierarchy: every group that has a capability in it, named
    /// once, in registration order.
    /// </summary>
    /// <returns>The group names, ready to be spoken or drilled into.</returns>
    public IReadOnlyList<string> ListGroups() =>
        [.. _registry.Descriptors.Select(descriptor => descriptor.Group).Distinct(StringComparer.Ordinal)];

    /// <summary>
    /// One line per capability in a group, in registration order.
    /// </summary>
    /// <param name="group">A group name, as <see cref="ListGroups"/> returned it.</param>
    /// <returns>The help text of every capability in that group.</returns>
    /// <exception cref="ArgumentException">No group is registered by that name.</exception>
    public IReadOnlyList<string> ListCapabilitiesIn(string group) =>
        [.. InGroup(group).Select(descriptor => descriptor.HelpText)];

    /// <summary>
    /// The bottom of the hierarchy: what one capability does, and what to say
    /// to reach it.
    /// </summary>
    /// <param name="capabilityId">A registered <see cref="CapabilityDescriptor.Id"/>.</param>
    /// <returns>The capability's help text followed by its example utterances.</returns>
    /// <exception cref="ArgumentException">No capability is registered by that id.</exception>
    public IReadOnlyList<string> Describe(string capabilityId)
    {
        CapabilityDescriptor descriptor = Find(capabilityId);

        return [descriptor.HelpText, TrySaying, .. descriptor.Examples];
    }

    /// <summary>
    /// Help's answer when asked generally: the groups.
    /// </summary>
    /// <returns>The listing, ready for any surface to render or speak.</returns>
    public CapabilityResult Answer() => Listing(ListGroups());

    /// <summary>
    /// Help's answer when asked about one group: the capabilities in it.
    /// </summary>
    /// <param name="group">A group name, as <see cref="ListGroups"/> returned it.</param>
    /// <returns>The listing, ready for any surface to render or speak.</returns>
    /// <exception cref="ArgumentException">No group is registered by that name.</exception>
    public CapabilityResult AnswerForGroup(string group) => Listing(ListCapabilitiesIn(group));

    /// <summary>
    /// Help's answer when asked about one capability: its detail.
    /// </summary>
    /// <param name="capabilityId">A registered <see cref="CapabilityDescriptor.Id"/>.</param>
    /// <returns>The listing, ready for any surface to render or speak.</returns>
    /// <exception cref="ArgumentException">No capability is registered by that id.</exception>
    public CapabilityResult AnswerForCapability(string capabilityId) => Listing(Describe(capabilityId));

    /// <summary>
    /// Every level of the hierarchy answers in the shape help's descriptor
    /// declares, and every level is help answering — so a surface looking up
    /// how to render it finds help either way.
    /// </summary>
    private static ListResult Listing(IReadOnlyList<string> items) => new()
    {
        CapabilityId = Descriptor.Id,
        Items = items,
    };

    /// <summary>
    /// A group with nothing in it is not a group help ever named, so asking for
    /// one is a caller that made a name up. An empty listing would read as
    /// "that group is empty", which is a different answer and an untrue one.
    /// </summary>
    private IReadOnlyList<CapabilityDescriptor> InGroup(string group)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(group);

        IReadOnlyList<CapabilityDescriptor> members =
        [
            .. _registry.Descriptors.Where(
                descriptor => string.Equals(descriptor.Group, group, StringComparison.Ordinal)),
        ];

        return members.Count > 0
            ? members
            : throw new ArgumentException($"No capability group is registered as '{group}'.", nameof(group));
    }

    private CapabilityDescriptor Find(string capabilityId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(capabilityId);

        return _registry.Descriptors.FirstOrDefault(
                   descriptor => string.Equals(descriptor.Id, capabilityId, StringComparison.Ordinal))
               ?? throw new ArgumentException(
                   $"No capability is registered as '{capabilityId}'.", nameof(capabilityId));
    }
}
