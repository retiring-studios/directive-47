using System;
using System.Collections.Generic;
using System.Threading;

namespace D47.VoiceLoop;

/// <summary>
/// Where a turn says what it is doing, and where the surfaces listen.
///
/// <para>
/// A bus rather than an event on the turn itself. The turn is not the only thing
/// that will publish here — Wave 3's journal and status watchers are the same
/// shape, and the predecessor project ended up with exactly three publishers on
/// one bus. An event on the state machine assumes a single publisher and is
/// wrong the first time that stops being true.
/// </para>
///
/// <para>
/// Thread-safe, because a turn runs off the UI thread and the surfaces are on
/// it. Marshalling to a dispatcher is the subscriber's job: this project is
/// Tier 0 and has never heard of one.
/// </para>
/// </summary>
///
/// <remarks>
/// <para>
/// No backlog and no replay, deliberately. The predecessor keeps the last few
/// hundred events and replays them to a late subscriber, which it needs because
/// its consumer is a browser that connects after the fact. Every surface here is
/// in process and exists before a turn can start. When something does join late
/// — a live log that opens mid-turn is the obvious one — that is the moment to
/// add it, and not before.
/// </para>
/// </remarks>
public sealed class Events
{
    private readonly Lock _guard = new();
    private readonly List<Action<TurnEvent>> _listening = [];

    /// <summary>
    /// Starts listening.
    /// </summary>
    /// <param name="listener">What to call with each event.</param>
    /// <returns>
    /// A subscription. Disposing it stops the calls; letting it go without
    /// disposing keeps them coming, which is the leak this returns something to
    /// prevent.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="listener"/> is null.
    /// </exception>
    public IDisposable Subscribe(Action<TurnEvent> listener)
    {
        ArgumentNullException.ThrowIfNull(listener);

        lock (_guard)
        {
            _listening.Add(listener);
        }

        return new Subscription(this, listener);
    }

    /// <summary>
    /// Tells everyone listening.
    /// </summary>
    ///
    /// <remarks>
    /// The listeners are copied out before any of them is called, so a
    /// subscriber that unsubscribes while being called does not change the list
    /// underneath the loop — and so nothing holds the lock while somebody else's
    /// code runs.
    /// </remarks>
    /// <param name="published">What happened.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="published"/> is null.
    /// </exception>
    public void Publish(TurnEvent published)
    {
        ArgumentNullException.ThrowIfNull(published);

        Action<TurnEvent>[] listening;

        lock (_guard)
        {
            listening = [.. _listening];
        }

        foreach (Action<TurnEvent> listener in listening)
        {
            listener(published);
        }
    }

    private void Unsubscribe(Action<TurnEvent> listener)
    {
        lock (_guard)
        {
            _listening.Remove(listener);
        }
    }

    private sealed class Subscription(Events events, Action<TurnEvent> listener) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            events.Unsubscribe(listener);
        }
    }
}
