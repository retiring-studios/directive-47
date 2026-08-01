using System.Collections.Generic;

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
    /// What the model is told so it can call this capability.
    /// </summary>
    public required ToolSchema Tool { get; init; }

    /// <summary>
    /// Things a Commander might say to reach this capability. They are
    /// few-shot examples inside the tool schema, helping the model map a sloppy
    /// transcription to the right capability, and "try saying…" text so the
    /// surfaces are discoverable.
    ///
    /// <para>
    /// They are examples, not a matcher. Nothing compares these against
    /// speech-to-text output: transcription of system, ship and commodity names
    /// is exactly where that fails, and a phrase matcher fails silently when it
    /// misses.
    /// </para>
    /// </summary>
    public required IReadOnlyList<string> Examples { get; init; }
}
