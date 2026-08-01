using System.Collections.Generic;

namespace D47.Capabilities;

/// <summary>
/// What a capability returns from one invocation, conforming to the
/// <see cref="DisplayModel"/> its descriptor declared. The filled-in form to
/// the descriptor's blank one.
///
/// <para>
/// Separate from the descriptor on purpose: the descriptor rides the cached
/// system prefix sent to the model and must not change, while this is different
/// every time it is asked.
/// </para>
/// </summary>
public abstract record CapabilityResult
{
    private protected CapabilityResult()
    {
    }

    /// <summary>
    /// The <see cref="CapabilityDescriptor.Id"/> this answer came from, so a
    /// surface can find the declaration that says how to render it.
    /// </summary>
    public required string CapabilityId { get; init; }
}

/// <summary>
/// An answer conforming to <see cref="ListDisplay"/>.
/// </summary>
public sealed record ListResult : CapabilityResult
{
    /// <summary>
    /// The lines, in the order they should be shown or spoken.
    /// </summary>
    public required IReadOnlyList<string> Items { get; init; }
}
