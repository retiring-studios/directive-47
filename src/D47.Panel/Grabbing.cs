using System;
using System.Globalization;
using System.Threading;

using D47.Placement;
using D47.VrOverlay;

namespace D47.Panel;

/// <summary>
/// Watches the Commander's hands and keeps the chrome showing what they are on.
///
/// <para>
/// The loop between two things that already exist:
/// <see cref="IControllers"/> says where the hands are, and
/// <see cref="IGrabChrome"/> asks what the laser is on and shows it. Neither
/// needs the other, which is why this is the only part that needs a thread.
/// </para>
/// </summary>
///
/// <remarks>
/// A thread of its own rather than a timer on the UI thread. Reading poses is a
/// call into the runtime, and
/// [#7](https://github.com/retiring-studios/directive-47/issues/7)'s "no stage
/// blocks the UI thread" is not a rule about voice — it is a rule about this
/// application. Nothing here touches WPF, which is what makes that possible: the
/// chrome is flat rectangles and never builds a visual tree.
/// </remarks>
internal sealed class Grabbing : IDisposable
{
    /// <summary>
    /// How often to look while a hand is tracked.
    ///
    /// <para>
    /// Fast enough that the highlight follows a hand rather than catching up
    /// with it. It is not a frame rate — nothing redraws unless the answer
    /// changes — so this is how quickly a Commander is told, not how hard the
    /// machine works.
    /// </para>
    /// </summary>
    private static readonly TimeSpan WhileTheyAreThere = TimeSpan.FromMilliseconds(33);

    /// <summary>
    /// How often to look when nothing is tracked.
    ///
    /// <para>
    /// Both controllers asleep on the desk is most of a session, and
    /// [#235](https://github.com/retiring-studios/directive-47/issues/235) asks
    /// for that to cost nothing. Twice a second is fast enough that picking a
    /// controller up feels immediate and slow enough to be free.
    /// </para>
    /// </summary>
    private static readonly TimeSpan WhileTheyAreNot = TimeSpan.FromMilliseconds(500);

    private readonly IGrabChrome _chrome;
    private readonly IHeadsetOverlay _overlay;
    private readonly IControllers _controllers;
    private readonly Action<string> _record;
    private readonly CancellationTokenSource _stopping = new();
    private readonly Thread _watching;

    /// <summary>
    /// The drag in progress, or nothing. Touched only from the watching thread,
    /// which is why it needs no guard.
    /// </summary>
    private Grab? _moving;

    /// <summary>
    /// The scale in progress, or nothing. Never set at the same time as
    /// <see cref="_moving"/> — one gesture starts on the bar and the other on a
    /// corner, and the laser is only ever on one of them.
    /// </summary>
    ///
    /// <remarks>
    /// A second field rather than one holding both. They answer different
    /// questions — a drag says where the overlay goes and a scale says how big it
    /// is and therefore where its middle ends up — and a shared abstraction over
    /// the two would be a type whose only job is to be two types.
    /// </remarks>
    private Stretch? _sizing;

    private bool _disposed;

    private Grabbing(
        IGrabChrome chrome,
        IHeadsetOverlay overlay,
        IControllers controllers,
        Action<string> record)
    {
        _chrome = chrome;
        _overlay = overlay;
        _controllers = controllers;
        _record = record;

        _watching = new Thread(Watch)
        {
            IsBackground = true,
            Name = "Directive 47 grab chrome",
        };
    }

    /// <summary>
    /// Starts watching.
    /// </summary>
    /// <param name="chrome">What shows the Commander their answer.</param>
    /// <param name="overlay">The panel a drag moves.</param>
    /// <param name="controllers">Where the hands are.</param>
    /// <param name="record">Where to note anything it had to carry on past.</param>
    /// <returns>The watch, which the caller owns and must dispose.</returns>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    internal static Grabbing Watching(
        IGrabChrome chrome,
        IHeadsetOverlay overlay,
        IControllers controllers,
        Action<string> record)
    {
        ArgumentNullException.ThrowIfNull(chrome);
        ArgumentNullException.ThrowIfNull(overlay);
        ArgumentNullException.ThrowIfNull(controllers);
        ArgumentNullException.ThrowIfNull(record);

        var grabbing = new Grabbing(chrome, overlay, controllers, record);

        grabbing._watching.Start();

        return grabbing;
    }

    /// <summary>
    /// Stops watching.
    /// </summary>
    ///
    /// <remarks>
    /// Waits for the thread rather than only asking it to stop, so that nothing
    /// is still painting a chrome overlay while the thing that owns it is being
    /// given back to the compositor.
    /// </remarks>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _stopping.Cancel();
        _watching.Join();
        _stopping.Dispose();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification =
            "Nothing is above this to catch anything — it is the body of a thread nobody "
            + "awaits, so an exception that escapes ends the watching silently. The runtime "
            + "going away underneath is a fact about the machine rather than a defect of "
            + "ours, and it must not leave the chrome frozen on whatever it last showed.")]
    private void Watch()
    {
        while (!_stopping.IsCancellationRequested)
        {
            TimeSpan next = WhileTheyAreNot;

            try
            {
                // SteamVR draws the laser, works out where it meets the quad,
                // and reports the trigger as a mouse button because the overlay
                // asked for pointer input. Reading any of that ourselves would
                // be a second answer to a question the runtime already answers.
                Grip grip = _chrome.Follow();

                Dragging(grip);

                // Faster while a hand is on it, because that is when the answer
                // can change from one look to the next. Pointing at nothing is
                // the state a Commander spends hours in.
                next = grip.On == Grabbed.Nothing ? WhileTheyAreNot : WhileTheyAreThere;
            }
            catch (Exception failed)
            {
                // A watcher that died on the first hiccup would leave the chrome
                // frozen on whatever it happened to be showing, which reads as a
                // stuck highlight rather than as a missing watcher.
                _record(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Could not tell what the controllers are pointing at: {failed.Message}"));
            }

            // Cancellation is waited on rather than slept through, so stopping
            // is immediate instead of taking up to half a second.
            if (_stopping.Token.WaitHandle.WaitOne(next))
            {
                return;
            }
        }
    }

    /// <summary>
    /// Starts, continues or ends a drag, given what the laser is doing.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// A grab starts on the bar or on a corner and nowhere else. Pointing at the
    /// panel itself grabs nothing — the decision in <c>docs/decisions.md</c> — so
    /// a Commander can read it without shoving it out of the way. The bar moves
    /// it and a corner scales it.
    /// </para>
    /// <para>
    /// Once it has started, what the laser is on stops mattering. A hand dragging
    /// the panel takes the panel with it, so the laser sits wherever the geometry
    /// puts it, and a grab that needed to stay over the bar would let go the
    /// moment it began to work. It goes double for a corner, which the panel
    /// grows out from under.
    /// </para>
    /// </remarks>
    /// <param name="grip">What the laser is on and whether the trigger is down.</param>
    private void Dragging(Grip grip)
    {
        if (!grip.Held)
        {
            // Includes the release, and also the laser leaving the quad, which
            // the adapter reports as the trigger being up because no button-up
            // is coming for an overlay nobody is aiming at any more.
            LetGo();

            return;
        }

        if (_moving is null && _sizing is null)
        {
            TakeHold(grip);

            return;
        }

        if (_controllers.At(grip.Hand) is not { } hand)
        {
            // The hand stopped being tracked mid-drag — put down without letting
            // go. The overlay stays as it was rather than following a pose nobody
            // has, and the grab ends so that picking the controller up again does
            // not resume a drag the Commander has forgotten about.
            LetGo();

            return;
        }

        if (_moving is { } moving)
        {
            _overlay.MoveTo(moving.Follows(hand));
        }
        else if (_sizing is { } sizing)
        {
            // A board rather than a pose and then a size: the pinned corner
            // holding still is what moves the middle, so the two are one answer.
            _overlay.ResizeTo(sizing.Follows(hand.Position));
        }

        // And the chrome after it. It is a second quad, so it does not follow the
        // panel on its own — the panel would slide out of its own bar, or swell
        // out from behind its own handles.
        _chrome.Frames(_overlay.Placed);
    }

    /// <summary>
    /// Starts a drag or a scale, if the laser is on something that offers one.
    /// </summary>
    /// <param name="grip">What the laser is on, and which hand is on it.</param>
    private void TakeHold(Grip grip)
    {
        if (_controllers.At(grip.Hand) is not { } took)
        {
            return;
        }

        if (grip.On == Grabbed.Bar)
        {
            _moving = Grab.Started(took, _overlay.Placed.Where);
        }
        else if (Stretch.Scales(grip.On))
        {
            _sizing = Stretch.Started(grip.On, took.Position, _overlay.Placed);
        }
    }

    /// <summary>
    /// Ends whichever gesture was in progress.
    /// </summary>
    ///
    /// <remarks>
    /// Both, unconditionally, rather than the one that is set. Only ever one of
    /// them is, and a release that cleared the wrong field would leave the
    /// overlay following a hand that had let go of it.
    /// </remarks>
    private void LetGo()
    {
        _moving = null;
        _sizing = null;
    }
}
