using System;
using System.Threading;
using System.Windows.Input;

using Shouldly;

using Xunit;

namespace D47.Panel.Tests;

/// <summary>
/// A real system-wide hotkey, claimed and then actually pressed.
///
/// <para>
/// Tier 1, not Tier 2. Registering a combination and synthesizing the keystroke
/// that fires it needs a desktop and nothing else, and a hosted runner has one
/// — which is the line <c>docs/decisions.md</c> draws. What it cannot check is
/// whether the message still arrives while Elite holds the foreground; that
/// needs the game and lives with the overlay's other Tier 3 tests.
/// </para>
///
/// <para>
/// A desktop test, because it presses real keys into whatever is in front.
/// </para>
/// </summary>
public class HotkeyTests : DesktopTest
{
    /// <summary>
    /// Deliberately not the combination the application ships with.
    ///
    /// <para>
    /// The default is a value, not a behaviour, and testing through it would
    /// couple these facts to a product decision that is expected to move to
    /// settings. It would also fail on any machine where something else already
    /// owns it, which is precisely the case the second fact below is about — a
    /// test that cannot tell its own subject from its environment is not worth
    /// having.
    /// </para>
    ///
    /// <para>
    /// An ordinary letter rather than something exotic like F24, which has no
    /// place on a real keyboard and therefore no scan code for
    /// <c>keybd_event</c> to send. Found by trying it.
    /// </para>
    /// </summary>
    private const ModifierKeys Held = ModifierKeys.Control | ModifierKeys.Shift;
    private const Key Tapped = Key.D;
    private const string Combination = "Ctrl+Shift+D";

    private static readonly TimeSpan LongEnoughToArrive = TimeSpan.FromSeconds(5);

    [Fact]
    public void Hotkey_WhenPressed_RunsWhatItWasGiven()
    {
        using var pump = new MessagePump();
        using var arrived = new ManualResetEventSlim();

        Hotkey hotkey = pump.Invoke(() => Hotkey.Register(Held, Tapped, arrived.Set));

        try
        {
            Input.Press(Held, Tapped);

            arrived.Wait(LongEnoughToArrive, TestContext.Current.CancellationToken).ShouldBeTrue(
                "the hotkey should have fired within "
                + $"{LongEnoughToArrive.TotalSeconds:0} seconds of the keystroke");
        }
        finally
        {
            // Back on the thread that claimed it. A registration belongs to its
            // thread, so unregistering from anywhere else quietly does nothing
            // and leaves the combination claimed for the rest of the run.
            pump.Invoke(hotkey.Dispose);
        }
    }

    [Fact]
    public void Hotkey_WhenTheCombinationIsAlreadyOwned_SaysSoRatherThanGoingQuiet()
    {
        // The second registration stands in for another application, which is
        // the real case: a combination is claimed system-wide, so whoever asks
        // second is refused. A hotkey that silently does nothing is
        // indistinguishable from one that is broken, and the Commander presses
        // it repeatedly either way.
        using var pump = new MessagePump();

        Hotkey owner = pump.Invoke(() => Hotkey.Register(Held, Tapped, () => { }));

        try
        {
            Exception refused = Should.Throw<InvalidOperationException>(
                () => pump.Invoke(() => Hotkey.Register(Held, Tapped, () => { })));

            refused.Message.ShouldContain(Combination);
        }
        finally
        {
            pump.Invoke(owner.Dispose);
        }
    }
}
