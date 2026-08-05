using D47.Composition;
using D47.Help;
using D47.Render;

namespace D47.TestSupport;

/// <summary>
/// Things a test needs to have in front of it before it can ask its question.
/// </summary>
public static class Fixtures
{
    /// <summary>
    /// What every surface shows today: help, answered from the registry the
    /// application answers from.
    ///
    /// <para>
    /// A real capability's answer rather than a hand-built stand-in, because a
    /// stand-in would let the render and the surfaces agree on a shape nothing
    /// actually produces. Help needs no microphone, no network and no game,
    /// which is what makes it usable from a test at any tier.
    /// </para>
    ///
    /// <para>
    /// The registry comes from <see cref="Composed.Capabilities"/> rather than
    /// being built here. Built here, it was a second answer to what the
    /// application is made of, and a capability added to one and not the other
    /// left every surface test asserting the old answer without failing.
    /// </para>
    /// </summary>
    /// <returns>The answer, composed the way the application composes it.</returns>
    public static Answer HelpsAnswer() =>
        new()
        {
            Descriptor = HelpCapability.Descriptor,
            Result = new HelpCapability(Composed.Capabilities).Answer(),
        };
}
