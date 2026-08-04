using System;
using System.Collections.Generic;
using System.Linq;

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

    /// <summary>
    /// Whether two results say the same thing.
    /// </summary>
    ///
    /// <remarks>
    /// Written out for the same reason as
    /// <see cref="CapabilityDescriptor.Equals(CapabilityDescriptor)"/>: a record
    /// compares a list by reference, so two results holding identical lines were
    /// unequal. <c>base.Equals</c> first, because it carries the equality
    /// contract and the capability id — without it, two different capabilities
    /// that happened to list the same lines would be the same result.
    /// </remarks>
    /// <param name="other">The result to compare with.</param>
    /// <returns>Whether they say the same thing.</returns>
    public bool Equals(ListResult? other) =>
        base.Equals(other) && Items.SequenceEqual(other.Items);

    /// <summary>
    /// A hash consistent with <see cref="Equals(ListResult)"/>.
    /// </summary>
    /// <returns>The hash.</returns>
    public override int GetHashCode()
    {
        var hash = default(HashCode);

        hash.Add(base.GetHashCode());

        foreach (string item in Items)
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }
}
