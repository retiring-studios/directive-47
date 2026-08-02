using System;
using System.Threading;
using System.Windows.Threading;

namespace D47.Panel.Tests;

/// <summary>
/// A thread with a message loop on it, which is what a hotkey needs to exist
/// at all.
///
/// <para>
/// <c>RegisterHotKey</c> gives Windows somewhere to post to, and a post that
/// nothing dispatches is a message that never arrives. The test runner's
/// threads have no loop, so a registration made on one would look perfectly
/// successful and never fire — which is the failure this class exists to avoid
/// rather than to reproduce.
/// </para>
///
/// <para>
/// A registration also belongs to the thread that made it, so everything done
/// to a hotkey has to come back here. That is why this hands out an
/// <see cref="Invoke{T}"/> rather than just starting a loop and leaving.
/// </para>
/// </summary>
internal sealed class MessagePump : IDisposable
{
    private static readonly TimeSpan LongEnoughToStart = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan LongEnoughToStop = TimeSpan.FromSeconds(5);

    private readonly Thread _thread;
    private Dispatcher? _dispatcher;
    private bool _disposed;

    internal MessagePump()
    {
        using var running = new ManualResetEventSlim();

        _thread = new Thread(() =>
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            running.Set();
            Dispatcher.Run();
        })
        {
            IsBackground = true,
        };

        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();

        if (!running.Wait(LongEnoughToStart))
        {
            throw new InvalidOperationException("The message loop never started.");
        }
    }

    /// <summary>
    /// Runs something on the pumping thread and waits for the result.
    /// </summary>
    /// <typeparam name="T">What it produces.</typeparam>
    /// <param name="what">What to run.</param>
    /// <returns>What it produced.</returns>
    internal T Invoke<T>(Func<T> what) => Pumping().Invoke(what);

    /// <summary>
    /// Runs something on the pumping thread and waits for it to finish.
    /// </summary>
    /// <param name="what">What to run.</param>
    internal void Invoke(Action what) => Pumping().Invoke(what);

    /// <summary>
    /// Stops the loop and ends the thread.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _dispatcher?.InvokeShutdown();
        _thread.Join(LongEnoughToStop);
    }

    private Dispatcher Pumping() =>
        _dispatcher ?? throw new InvalidOperationException("The message loop is not running.");
}
