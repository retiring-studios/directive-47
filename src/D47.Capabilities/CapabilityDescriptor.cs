using System;
using System.Collections.Generic;
using System.Linq;

namespace D47.Capabilities;

/// <summary>
/// The declaration of a capability, as data — never a variable. A descriptor
/// says what a capability <em>is</em> and what to show; it is registered once
/// and never mutates. What a capability <em>returns</em> is a separate
/// per-invocation result conforming to the shape declared here.
///
/// <para>
/// Blank form versus filled-in form. Collapsing the two would put the LLM tool
/// schema on a mutating object when prompt caching needs it byte-identical,
/// race two callers on one instance, and stop the parity test running without
/// invoking capabilities.
/// </para>
///
/// <para>
/// A descriptor carries no drawing logic and references no UI assembly, which
/// is what lets every surface consume the same object.
/// </para>
/// </summary>
public sealed record CapabilityDescriptor
{
    /// <summary>
    /// Stable identifier, unique across the registry.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The group help lists this capability under, so a spoken answer stays
    /// short at every level of the hierarchy.
    /// </summary>
    public required string Group { get; init; }

    /// <summary>
    /// One line describing what the capability does, in the words help will
    /// speak.
    /// </summary>
    public required string HelpText { get; init; }

    /// <summary>
    /// The shape this capability's answers take. Required, because parity is
    /// strict: a capability with nothing to show on a surface is a capability
    /// that is half built.
    /// </summary>
    public required DisplayModel Display { get; init; }

    /// <summary>
    /// Things a Commander might say to reach this capability. Help speaks them
    /// as "try saying…" at the bottom of its hierarchy, which is where a
    /// capability stops being a name on a list and becomes something you know
    /// how to invoke.
    ///
    /// <para>
    /// They are examples, not a matcher. Nothing compares these against
    /// speech-to-text output: transcription of system, ship and commodity names
    /// is exactly where that fails, and a phrase matcher fails silently when it
    /// misses. The LLM integration will read the same list as few-shot examples
    /// inside the tool schema; that is a second consumer, not a second field.
    /// </para>
    /// </summary>
    public required IReadOnlyList<string> Examples { get; init; }

    /// <summary>
    /// Whether two descriptors are the same descriptor.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// Written out because the generated one is wrong here.
    /// <see cref="Examples"/> is a list, a record compares each member with
    /// <c>EqualityComparer&lt;T&gt;.Default</c>, and for a list that is reference
    /// equality — so two descriptors with identical examples compared unequal
    /// and the <c>record</c> keyword quietly stopped being true partway down the
    /// type.
    /// </para>
    /// <para>
    /// The cost of writing it out is that a member added below is a member this
    /// forgets, silently — the same shape of fault, moved. That is what
    /// <c>ValueEqualityTests.CapabilityDescriptor_HasTheMembersThisFileKnowsAbout</c>
    /// is for: add one and it fails, naming this method.
    /// </para>
    /// </remarks>
    /// <param name="other">The descriptor to compare with.</param>
    /// <returns>Whether they say the same thing.</returns>
    public bool Equals(CapabilityDescriptor? other) =>
        other is not null
        && Id == other.Id
        && Group == other.Group
        && HelpText == other.HelpText
        && Display == other.Display
        && Examples.SequenceEqual(other.Examples);

    /// <summary>
    /// A hash consistent with <see cref="Equals(CapabilityDescriptor)"/>.
    /// </summary>
    ///
    /// <remarks>
    /// Over the examples one at a time rather than over the list, for the same
    /// reason the equality is written out: hashing the list would hash its
    /// identity, and equal values that hash differently are two entries in every
    /// dictionary — the same defect wearing a different coat.
    /// </remarks>
    /// <returns>The hash.</returns>
    public override int GetHashCode()
    {
        var hash = default(HashCode);

        hash.Add(Id);
        hash.Add(Group);
        hash.Add(HelpText);
        hash.Add(Display);

        foreach (string example in Examples)
        {
            hash.Add(example);
        }

        return hash.ToHashCode();
    }
}
