using System;
using System.Threading;

namespace D47.Panel.Tests;

/// <summary>
/// Waiting for something to become true.
///
/// <para>
/// Every question these tests ask of the desktop is answered a moment late. A
/// click is acted on when the application gets round to it, a window leaves the
/// automation tree when the shell notices, and a tray icon appears when
/// <c>explorer.exe</c> feels like it. Sampling once and asserting is how a test
/// that is right becomes a test that is flaky.
/// </para>
/// </summary>
internal static class Eventually
{
    private static readonly TimeSpan BetweenLooks = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Polls until a condition holds, or until the patience runs out.
    /// </summary>
    /// <param name="what">The condition to wait for.</param>
    /// <param name="within">How long to wait.</param>
    /// <param name="betweenLooks">
    /// How long to wait between looks. Worth raising when looking is expensive
    /// — reading the hidden tray icons costs a flyout opening and closing, so
    /// polling that four times a second would spend the whole wait animating
    /// the shell.
    /// </param>
    /// <returns>Whether it became true in time.</returns>
    internal static bool True(Func<bool> what, TimeSpan within, TimeSpan? betweenLooks = null)
    {
        DateTime deadline = DateTime.UtcNow + within;

        while (DateTime.UtcNow < deadline)
        {
            if (what())
            {
                return true;
            }

            Thread.Sleep(betweenLooks ?? BetweenLooks);
        }

        // Asked once more rather than returning false on the clock. The last
        // sleep may have been the one it needed.
        return what();
    }
}
