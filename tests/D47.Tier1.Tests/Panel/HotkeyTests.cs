using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Input;

using D47.Panel;

using Shouldly;

using Xunit;

namespace D47.Tier1.Tests.Panel;

/// <summary>
/// A real system-wide hotkey, claimed, actually pressed, and changed while the
/// application is running.
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
/// A desktop test, because it presses real keys into whatever is in front, and
/// because a claimed combination is machine-wide state that two tests cannot
/// hold at once. Which combination is <em>chosen</em> is a different question,
/// answered by reading a file — it needs none of this and is asserted in
/// <see cref="ChosenHotkeyTests"/>.
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
    private const string Spelled = "Ctrl+Shift+D";

    /// <summary>
    /// A second combination, for the tests that change one. Also an ordinary
    /// letter, and for the same reason.
    /// </summary>
    private const Key AlsoTapped = Key.B;
    private const string AlsoSpelled = "Ctrl+Shift+B";

    /// <summary>
    /// What the store calls the combination that toggles the overlay.
    /// </summary>
    private const string WhatItIsCalled = "overlay hotkey";

    private static readonly TimeSpan LongEnoughToArrive = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Where a refused claim writes itself down. One per test, because xUnit
    /// builds the class afresh for each fact.
    /// </summary>
    private readonly List<string> _recorded = [];

    [Fact]
    public void Hotkey_WhenPressed_RunsWhatItWasGiven()
    {
        using var pump = new MessagePump();
        using var arrived = new ManualResetEventSlim();

        Hotkey hotkey = pump.Invoke(() => Hotkey.TryRegister(new Combination(Held, Tapped), arrived.Set).Or(_recorded.Add))
            ?? throw new InvalidOperationException(
                $"{Spelled} is already owned on this machine, so this test cannot run.");

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

        Hotkey hotkey = pump.Invoke(() => Hotkey.TryRegister(new Combination(Held, Tapped), () => Interlocked.Increment(ref fired))
                .Or(_recorded.Add))
            ?? throw new InvalidOperationException(
                $"{Spelled} is already owned on this machine, so this test cannot run.");

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

        Hotkey owner = pump.Invoke(() => Hotkey.TryRegister(new Combination(Held, Tapped), () => { }).Or(_recorded.Add))
            ?? throw new InvalidOperationException(
                $"{Spelled} is already owned on this machine, so this test cannot run.");

        try
        {
            Perhaps<Hotkey> refused = pump.Invoke(
                () => Hotkey.TryRegister(new Combination(Held, Tapped), () => { }));

            refused.Or(_recorded.Add).ShouldBeNull(
                "the second claim on a combination should come back empty");

            // And the words come from the claim rather than from whoever asked
            // for it. The application used to write its own sentence naming
            // Ctrl+Alt+D, which was a second copy of a value it had passed in
            // and free to disagree with what was actually attempted. This test
            // asks for Ctrl+Shift+D, so a sentence naming anything else is that
            // bug.
            _recorded.ShouldHaveSingleItem().ShouldContain(Spelled);
        }
        finally
        {
            pump.Invoke(owner.Dispose);
        }
    }

    [Fact]
    public void OverlayHotkey_WhenChanged_TakesEffectWithoutARestart()
    {
        // The criterion this story exists for. A combination that only works
        // after the application is restarted is not a setting, it is a startup
        // argument spelled differently.
        using var pump = new MessagePump();
        using var remembered = new TemporaryStore();
        using var arrived = new ManualResetEventSlim();

        remembered.Settings().Write(WhatItIsCalled, AlsoSpelled);

        using ChosenHotkey chosen = pump.Invoke(
            () => ChosenHotkey.From(remembered.Settings(), _recorded.Add, arrived.Set));

        try
        {
            pump.Invoke(() => chosen.Rebind(new Combination(Held, Tapped)))
                .Or(_recorded.Add)
                .ShouldNotBeNull("the combination was free, so claiming it should have worked");

            Input.Press(Held, Tapped);

            arrived.Wait(LongEnoughToArrive, TestContext.Current.CancellationToken).ShouldBeTrue(
                $"{Spelled} should be toggling the overlay from the moment it was chosen");
        }
        finally
        {
            pump.Invoke(chosen.Dispose);
        }
    }

    [Fact]
    public void OverlayHotkey_WhenChanged_LetsGoOfTheOneItHad()
    {
        // The other half, and the one a rebind written as "claim the new one"
        // gets wrong. A combination nobody gave back is one no other application
        // can have, and the Commander who changed away from it has no way left
        // to find that out.
        using var pump = new MessagePump();
        using var remembered = new TemporaryStore();

        remembered.Settings().Write(WhatItIsCalled, AlsoSpelled);

        using ChosenHotkey chosen = pump.Invoke(
            () => ChosenHotkey.From(remembered.Settings(), _recorded.Add, () => { }));

        try
        {
            pump.Invoke(() => chosen.Rebind(new Combination(Held, Tapped))).Or(_recorded.Add)
                .ShouldNotBeNull();

            // Somebody else can now have the old one, which is only true if it
            // was actually released.
            Hotkey after = pump.Invoke(
                    () => Hotkey.TryRegister(new Combination(Held, AlsoTapped), () => { }).Or(_recorded.Add))
                ?? throw new ShouldAssertException(
                    $"{AlsoSpelled} should have been given back when the hotkey changed away "
                    + "from it, and something is still holding it.");

            pump.Invoke(after.Dispose);
        }
        finally
        {
            pump.Invoke(chosen.Dispose);
        }
    }

    [Fact]
    public void OverlayHotkey_WhenChanged_IsRememberedForNextTime()
    {
        using var pump = new MessagePump();
        using var remembered = new TemporaryStore();

        remembered.Settings().Write(WhatItIsCalled, AlsoSpelled);

        using ChosenHotkey chosen = pump.Invoke(
            () => ChosenHotkey.From(remembered.Settings(), _recorded.Add, () => { }));

        try
        {
            pump.Invoke(() => chosen.Rebind(new Combination(Held, Tapped))).Or(_recorded.Add)
                .ShouldNotBeNull();

            // A second open is the next run.
            remembered.Settings().Read(WhatItIsCalled).ShouldBe(Spelled);
        }
        finally
        {
            pump.Invoke(chosen.Dispose);
        }
    }

    [Fact]
    public void OverlayHotkey_WhenTheChosenCombinationIsRefused_TellsWhoeverAskedForIt()
    {
        // The open question this story had to answer. Startup writes a refusal
        // to a log because there is nobody to tell; a Commander who has just
        // chosen a combination is somebody to tell, and telling them means
        // handing the reason back rather than only filing it.
        using var pump = new MessagePump();
        using var remembered = new TemporaryStore();

        remembered.Settings().Write(WhatItIsCalled, AlsoSpelled);

        Hotkey owner = pump.Invoke(() => Hotkey.TryRegister(new Combination(Held, Tapped), () => { }).Or(_recorded.Add))
            ?? throw new InvalidOperationException(
                $"{Spelled} is already owned on this machine, so this test cannot run.");

        using ChosenHotkey chosen = pump.Invoke(
            () => ChosenHotkey.From(remembered.Settings(), _recorded.Add, () => { }));

        List<string> toldTheCommander = [];

        try
        {
            Perhaps<Combination> refused = pump.Invoke(
                () => chosen.Rebind(new Combination(Held, Tapped)));

            refused.Or(toldTheCommander.Add).ShouldBeNull(
                "a combination another application owns cannot be chosen");

            toldTheCommander.ShouldHaveSingleItem().ShouldContain(
                Spelled,
                customMessage: "the Commander should be told which combination was refused");
        }
        finally
        {
            pump.Invoke(chosen.Dispose);
            pump.Invoke(owner.Dispose);
        }
    }

    [Fact]
    public void OverlayHotkey_WhenTheChosenCombinationIsRefused_KeepsTheOneItHad()
    {
        // Trying something and being told no should not cost the Commander the
        // key that was working a moment ago.
        using var pump = new MessagePump();
        using var remembered = new TemporaryStore();
        using var arrived = new ManualResetEventSlim();

        remembered.Settings().Write(WhatItIsCalled, AlsoSpelled);

        Hotkey owner = pump.Invoke(() => Hotkey.TryRegister(new Combination(Held, Tapped), () => { }).Or(_recorded.Add))
            ?? throw new InvalidOperationException(
                $"{Spelled} is already owned on this machine, so this test cannot run.");

        using ChosenHotkey chosen = pump.Invoke(
            () => ChosenHotkey.From(remembered.Settings(), _recorded.Add, arrived.Set));

        try
        {
            pump.Invoke(() => chosen.Rebind(new Combination(Held, Tapped))).Or(_ => { });

            Input.Press(Held, AlsoTapped);

            arrived.Wait(LongEnoughToArrive, TestContext.Current.CancellationToken).ShouldBeTrue(
                $"{AlsoSpelled} should still work after a refused attempt to change it");

            remembered.Settings().Read(WhatItIsCalled).ShouldBe(
                AlsoSpelled, "a combination that was never claimed is not worth remembering");
        }
        finally
        {
            pump.Invoke(chosen.Dispose);
            pump.Invoke(owner.Dispose);
        }
    }

    [Fact]
    public void OverlayHotkey_WhenChosenAgain_IsNotRefusedByItsOwnRegistration()
    {
        // Claiming the new one before releasing the old is what keeps a refusal
        // from costing the working key, and it puts the application in the way
        // of its own request when nothing actually changed. A Commander
        // confirming the combination they already have should not be told
        // somebody else owns it.
        using var pump = new MessagePump();
        using var remembered = new TemporaryStore();

        remembered.Settings().Write(WhatItIsCalled, Spelled);

        using ChosenHotkey chosen = pump.Invoke(
            () => ChosenHotkey.From(remembered.Settings(), _recorded.Add, () => { }));

        try
        {
            pump.Invoke(() => chosen.Rebind(new Combination(Held, Tapped)))
                .Or(_recorded.Add)
                .ShouldNotBeNull("choosing the combination already in force changes nothing");
        }
        finally
        {
            pump.Invoke(chosen.Dispose);
        }
    }

    [Fact]
    public void OverlayHotkey_WhenTheRememberedOneIsAlreadyOwned_CanStillBeChanged()
    {
        // The whole reason this story exists. #103 left a Commander whose
        // combination was taken by another application with a line in a log and
        // no way to get a hotkey back at all.
        using var pump = new MessagePump();
        using var remembered = new TemporaryStore();
        using var arrived = new ManualResetEventSlim();

        remembered.Settings().Write(WhatItIsCalled, AlsoSpelled);

        Hotkey squatter = pump.Invoke(
                () => Hotkey.TryRegister(new Combination(Held, AlsoTapped), () => { }).Or(_recorded.Add))
            ?? throw new InvalidOperationException(
                $"{AlsoSpelled} is already owned on this machine, so this test cannot run.");

        using ChosenHotkey chosen = pump.Invoke(
            () => ChosenHotkey.From(remembered.Settings(), _recorded.Add, arrived.Set));

        try
        {
            _recorded.ShouldHaveSingleItem().ShouldContain(
                AlsoSpelled, customMessage: "starting without a hotkey still has to be recorded");

            pump.Invoke(() => chosen.Rebind(new Combination(Held, Tapped))).Or(_recorded.Add)
                .ShouldNotBeNull();

            Input.Press(Held, Tapped);

            arrived.Wait(LongEnoughToArrive, TestContext.Current.CancellationToken).ShouldBeTrue(
                "a Commander who started with no hotkey should be able to choose one that works");
        }
        finally
        {
            pump.Invoke(chosen.Dispose);
            pump.Invoke(squatter.Dispose);
        }
    }
}
