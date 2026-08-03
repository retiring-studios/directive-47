using D47.Capabilities;
using D47.Help;
using D47.Render;

namespace D47.TestSupport;

/// <summary>
/// Things a test needs to have in front of it before it can ask its question.
/// </summary>
public static class Fixtures
{
    /// <summary>
    /// What every surface shows today: help, answered from a registry holding
    /// the one capability that exists.
    ///
    /// <para>
    /// A real capability's answer rather than a hand-built stand-in, because a
    /// stand-in would let the render and the surfaces agree on a shape nothing
    /// actually produces. Help needs no microphone, no network and no game,
    /// which is what makes it usable from a test at any tier.
    /// </para>
    /// </summary>
    /// <returns>The answer, composed the way the application composes it.</returns>
    public static Answer HelpsAnswer()
    {
        var registry = new CapabilityRegistry(HelpCapability.Descriptor);

        return new Answer
        {
            Descriptor = HelpCapability.Descriptor,
            Result = new HelpCapability(registry).Answer(),
        };
    }
}
