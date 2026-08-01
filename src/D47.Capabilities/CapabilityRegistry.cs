using System;
using System.Collections.Generic;

namespace D47.Capabilities;

/// <summary>
/// Everything the application can do, as descriptors. The registry is the one
/// source help projects from, the one source the LLM's tool schemas are built
/// from, and the one source the parity test enumerates — so a capability that
/// registers here needs no edit anywhere else to become discoverable.
///
/// <para>
/// Immutable once constructed: the descriptors it holds ride the cached system
/// prefix sent to the model, and are read from whichever thread happens to be
/// answering.
/// </para>
/// </summary>
public sealed class CapabilityRegistry
{
    private readonly IReadOnlyList<CapabilityDescriptor> _descriptors;

    /// <summary>
    /// Creates a registry over the given descriptors, in the order they should
    /// be listed.
    /// </summary>
    /// <param name="descriptors">The registered capability declarations.</param>
    public CapabilityRegistry(params CapabilityDescriptor[] descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);
        _descriptors = [.. descriptors];
    }

    /// <summary>
    /// The registered descriptors, in listing order.
    /// </summary>
    public IReadOnlyList<CapabilityDescriptor> Descriptors => _descriptors;
}
