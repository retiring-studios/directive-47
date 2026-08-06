using System;
using System.Collections.Generic;
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
/// <see cref="IControllers"/> says where the hands are,
/// <see cref="Pointing"/> says what that means, and
/// <see cref="IGrabChrome"/> shows it. None of them needs the others, which is
/// why this is the only part that needs a thread.
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

    private readonly IControllers _controllers;
    private readonly IGrabChrome _chrome;
    private readonly Board _panel;
    private readonly Action<string> _record;
    private readonly CancellationTokenSource _stopping = new();
    private readonly Thread _watching;

    private bool _disposed;

    private Grabbing(
        IControllers controllers, IGrabChrome chrome, Board panel, Action<string> record)
    {
        _controllers = controllers;
        _chrome = chrome;
        _panel = panel;
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
    /// <param name="controllers">Where the hands are.</param>
    /// <param name="chrome">What shows the Commander their answer.</param>
    /// <param name="panel">The quad being pointed at.</param>
    /// <param name="record">Where to note anything it had to carry on past.</param>
    /// <returns>The watch, which the caller owns and must dispose.</returns>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    internal static Grabbing Watching(
        IControllers controllers, IGrabChrome chrome, Board panel, Action<string> record)
    {
        ArgumentNullException.ThrowIfNull(controllers);
        ArgumentNullException.ThrowIfNull(chrome);
        ArgumentNullException.ThrowIfNull(record);

        var grabbing = new Grabbing(controllers, chrome, panel, record);

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
                IReadOnlyList<Pose> held = _controllers.Tracked();

                _chrome.Showing(Pointing.At(held, _panel));

                next = held.Count > 0 ? WhileTheyAreThere : WhileTheyAreNot;
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
}
