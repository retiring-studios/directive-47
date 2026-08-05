using System;

namespace D47.Tier1.Tests.Panel;

/// <summary>
/// Stands in for something going wrong, where the test needs a failure of its
/// own to throw.
///
/// <para>
/// Its own type, and that is the entire point of it. Both tests using this are
/// about a <c>catch</c> that must not be wide enough to hold a real failure —
/// one where an assertion fails mid-test and the panel must still be torn down,
/// one where the overlay is broken and must not be reported as an unsupported
/// machine. A stand-in of a shared type such as
/// <see cref="InvalidOperationException"/> would be caught by the very code
/// being guarded against.
/// </para>
/// </summary>
internal sealed class StandInFailure : Exception
{
    internal StandInFailure()
        : base("stand-in for something going wrong")
    {
    }

    internal StandInFailure(string message)
        : base(message)
    {
    }

    internal StandInFailure(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
