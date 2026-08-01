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
/// </summary>
public sealed class HelpCapability
{
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
    /// One line per registered capability, in registration order.
    /// </summary>
    /// <returns>The help text of every registered capability.</returns>
    public IReadOnlyList<string> ListCapabilities() =>
        [.. _registry.Descriptors.Select(descriptor => descriptor.HelpText)];
}
