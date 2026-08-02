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

        Hotkey hotkey = pump.Invoke(() => Hotkey.TryRegister(Held, Tapped, arrived.Set))
            ?? throw new InvalidOperationException(
                $"{Combination} is already owned on this machine, so this test cannot run.");

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
    public void Hotkey_WhenTappedTwice_FiresTwice()
    {
        // The case the maintainer found by using it: quick repeats did nothing,
        // because the first implementation told a repeat from a press by
        // elapsed time. Pressing and releasing is now what separates them, and
        // this is the test that would have caught it.
        using var pump = new MessagePump();
        int fired = 0;

        Hotkey hotkey = pump.Invoke(() => Hotkey.TryRegister(Held, Tapped, () => Interlocked.Increment(ref fired)))
            ?? throw new InvalidOperationException(
                $"{Combination} is already owned on this machine, so this test cannot run.");

        try
        {
            Input.Press(Held, Tapped);
            Input.Press(Held, Tapped);

            Eventually.True(
                () => Volatile.Read(ref fired) == 2,
                LongEnoughToArrive).ShouldBeTrue(
                    $"two taps should be two presses, and this saw {Volatile.Read(ref fired)}");
        }
        finally
        {
            pump.Invoke(hotkey.Dispose);
        }
    }

    [Fact]
    public void Hotkey_WhenTheCombinationIsAlreadyOwned_IsAbsentNotFailed()
    {
        // The second registration stands in for another application, which is
        // the real case: a combination is claimed system-wide, so whoever asks
        // second is refused.
        //
        // Absent rather than thrown, and that was a deliberate change. It used
        // to throw, on the reasoning that a hotkey silently doing nothing is
        // indistinguishable from one that is broken — but the caller was the
        // application's startup, so the consequence was that Directive 47
        // refused to run at all because something else owned one key. Another
        // application holding a combination is a fact about the machine, the
        // same shape as one that cannot composite. What keeps it from being
        // silent is the line written to the log, not the process dying.
        using var pump = new MessagePump();

        Hotkey owner = pump.Invoke(() => Hotkey.TryRegister(Held, Tapped, () => { }))
            ?? throw new InvalidOperationException(
                $"{Combination} is already owned on this machine, so this test cannot run.");

        try
        {
            Hotkey? refused = pump.Invoke(() => Hotkey.TryRegister(Held, Tapped, () => { }));

            refused.ShouldBeNull("the second claim on a combination should come back empty");
        }
        finally
        {
            pump.Invoke(owner.Dispose);
        }
    }
}
