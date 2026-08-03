using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

using D47.Render;

namespace D47.GameOverlay.Tests;

/// <summary>
/// The overlay, actually on screen over the running game.
///
/// <para>
/// It lives on a thread of its own with a dispatcher running, because that is
/// what a real window needs and because a window without a message loop is not
/// something the shell has an opinion about. The point of these tests is what
/// Windows thinks of the overlay, so a window that only exists as objects would
/// answer the wrong question.
/// </para>
///
/// <para>
/// Disposable, and the disposal is the point: a test that fails an assertion
/// must not leave a transparent, always-on-top window over somebody's game.
/// </para>
/// </summary>
internal sealed class RunningOverlay : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan LongEnoughToClose = TimeSpan.FromSeconds(5);

    private readonly Thread _thread;
    private GameOverlayWindow? _window;
    private Dispatcher? _dispatcher;
    private bool _disposed;

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification =
            "Marshalling a failure back to the calling thread is the whole job. Rethrowing "
            + "inside the catch would tear down the process instead of failing the test, and "
            + "narrowing the type would swallow whatever it did not name.")]
    private RunningOverlay(EliteWindow game, Answer answer)
    {
        using var ready = new ManualResetEventSlim();
        Exception? failure = null;

        _thread = new Thread(() =>
        {
            try
            {
                _dispatcher = Dispatcher.CurrentDispatcher;
                _window = new GameOverlayWindow(answer);

                // Waited for rather than assumed. A shown window is not a drawn
                // one, and every question this class answers is about what is
                // on the screen.
                _window.ContentRendered += (_, _) =>
                {
                    Handle = new WindowInteropHelper(_window).Handle;
                    ready.Set();
                };

                _window.ShowOver(game);
            }
            catch (Exception error)
            {
                failure = error;
                ready.Set();
                return;
            }

            Dispatcher.Run();
        })
        {
            IsBackground = true,
        };

        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();

        bool appeared = ready.Wait(Patience);

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }

        if (!appeared)
        {
            Dispose();

            throw new InvalidOperationException(
                $"The overlay did not draw anything within {Patience.TotalSeconds:0} seconds.");
        }
    }

    /// <summary>
    /// The overlay's window, as the operating system knows it.
    /// </summary>
    internal IntPtr Handle { get; private set; }

    /// <summary>
    /// Where the overlay is, in physical screen pixels.
    /// </summary>
    internal Rect Bounds => Screen.BoundsOf(Handle);

    /// <summary>
    /// Shows the overlay over the game and waits until it has drawn.
    /// </summary>
    /// <param name="game">The running game to draw over.</param>
    /// <param name="answer">What the overlay should render.</param>
    /// <returns>The overlay, which the caller owns and must dispose.</returns>
    internal static RunningOverlay ShownOver(EliteWindow game, Answer answer) =>
        new(game, answer);

    /// <summary>
    /// Turns click-through on or off, on the thread that owns the window.
    /// </summary>
    /// <param name="through">Whether the mouse should go straight past it.</param>
    internal void PassInputThrough(bool through)
    {
        if (_dispatcher is not { } dispatcher || _window is not { } window)
        {
            throw new InvalidOperationException("The overlay is not running.");
        }

        dispatcher.Invoke(() => window.PassesInputThrough = through);
    }

    /// <summary>
    /// Every line of text the overlay put on screen, top to bottom.
    ///
    /// <para>
    /// Read off the visual tree the overlay actually built, on the thread that
    /// owns it. Asserting on the answer instead would prove the answer, which
    /// is not in question here — what is in question is whether this window is
    /// showing the same render the panel does.
    /// </para>
    /// </summary>
    internal IReadOnlyList<string> VisibleText()
    {
        if (_dispatcher is not { } dispatcher || _window is not { } window)
        {
            throw new InvalidOperationException("The overlay is not running.");
        }

        return dispatcher.Invoke(() =>
        {
            List<string> lines = [];
            Collect(window, lines);
            return (IReadOnlyList<string>)lines;
        });
    }

    /// <summary>
    /// Takes the overlay off the screen and ends the thread it was living on.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_dispatcher is { } dispatcher)
        {
            dispatcher.Invoke(() => _window?.Close());
            dispatcher.InvokeShutdown();
        }

        _thread.Join(LongEnoughToClose);
    }

    private static void Collect(DependencyObject node, List<string> lines)
    {
        int children = VisualTreeHelper.GetChildrenCount(node);

        for (int child = 0; child < children; child++)
        {
            DependencyObject descendant = VisualTreeHelper.GetChild(node, child);

            if (descendant is TextBlock block)
            {
                lines.Add(block.Text);
            }

            Collect(descendant, lines);
        }
    }
}
