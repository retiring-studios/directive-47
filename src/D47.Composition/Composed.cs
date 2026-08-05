using D47.Capabilities;
using D47.Help;

namespace D47.Composition;

/// <summary>
/// What the application is made of, named once.
/// </summary>
public static class Composed
{
    /// <summary>
    /// Every capability the application has, in the order they are listed.
    ///
    /// <para>
    /// Registering a capability is adding it here, and there is nowhere else to
    /// add it. That is the whole point of this type: the registry used to be
    /// constructed in the application, again in the shared test fixture, and
    /// again in the parity test, and nothing failed when the three disagreed —
    /// the parity test would carry on checking a registry that was not the
    /// application's, which is the one test whose job is to notice a capability
    /// that was not wired up.
    /// </para>
    ///
    /// <para>
    /// Order is part of the contract: it is the order a spoken answer reads in.
    /// Help first, because it is what a Commander reaches for when they do not
    /// yet know what else there is.
    /// </para>
    /// </summary>
    public static CapabilityRegistry Capabilities { get; } = new(HelpCapability.Descriptor);
}
