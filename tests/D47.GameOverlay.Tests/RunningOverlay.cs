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
using D47.TestSupport;

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
    private RunningOverlay(EliteWindow game, Answer answer, (Point Corner, double Scale)? left)
    {
        using var ready = new ManualResetEventSlim();
        Exception? failure = null;

        _thread = new Thread(() =>
        {
            try
            {
                _dispatcher = Dispatcher.CurrentDispatcher;
                _window = new GameOverlayWindow(answer);

                // Before it is shown, which is the only moment a remembered
                // place is any use — the composition root does the same thing
                // for the same reason, and an overlay placed afterwards is one
                // the Commander watches walk across the screen.
                if (left is { } place)
                {
                    _window.PlaceAt(place.Corner, place.Scale);
                }

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
        new(game, answer, null);

    /// <summary>
    /// Shows the overlay where a previous run left it, rather than over the
    /// game, and waits until it has drawn.
    /// </summary>
    /// <param name="game">The running game the overlay would otherwise sit on.</param>
    /// <param name="answer">What the overlay should render.</param>
    /// <param name="corner">Its top-left corner, in physical screen pixels.</param>
    /// <param name="scale">How much bigger than the render's own size to be.</param>
    /// <returns>The overlay, which the caller owns and must dispose.</returns>
    internal static RunningOverlay PlacedAt(
        EliteWindow game, Answer answer, Point corner, double scale) =>
        new(game, answer, (corner, scale));

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
    /// The size the overlay laid its render out at.
    /// </summary>
    internal Size NaturalSize() => OnItsOwnThread(window => window.NaturalSize);

    /// <summary>
    /// How big the window actually is, in the device-independent units it was
    /// laid out in.
    ///
    /// <para>
    /// Not <see cref="Bounds"/>, which is physical pixels. Comparing this
    /// against <see cref="NaturalSize"/> is comparing two numbers in the same
    /// units, so the ratio between them is a scale and not a scale multiplied
    /// by whatever this monitor is set to.
    /// </para>
    /// </summary>
    internal Size WindowSize() =>
        OnItsOwnThread(window => new Size(window.ActualWidth, window.ActualHeight));

    /// <summary>
    /// What the render actually wants, worked out independently of the overlay.
    ///
    /// <para>
    /// Laid out rather than merely measured, which is the whole point of it. A
    /// control that has never been through a layout pass has not resolved its
    /// templates, so measuring alone reports the size of an almost empty box —
    /// and an overlay that sized itself that way would be too small for its own
    /// contents while looking internally consistent.
    /// </para>
    /// </summary>
    internal Size WhatTheRenderWants(Answer answer) =>
        OnItsOwnThread(_ =>
        {
            var view = new CapabilityView { DataContext = answer };

            view.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            view.Arrange(new Rect(view.DesiredSize));
            view.UpdateLayout();

            return view.DesiredSize;
        });

    /// <summary>
    /// The shape of the window and the shape of the render in it, as
    /// width over height. They are supposed to be the same number.
    /// </summary>
    internal (double Window, double Render) Shapes() =>
        OnItsOwnThread(window =>
        {
            Size render = window.NaturalSize;

            return (window.ActualWidth / window.ActualHeight, render.Width / render.Height);
        });

    /// <summary>
    /// How much of the window a line of the render actually covers.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// The line's own layout size is not returned alongside it, and two attempts
    /// to do so were thrown away rather than kept. Asserting that the line's
    /// <c>ActualWidth</c> holds still is worthless — a single line of text wants
    /// the width of its text however much room it is given. Asserting it of the
    /// whole render is worthless for a subtler reason: a <c>Viewbox</c> measures
    /// its child against infinity whatever size it is, so the render never
    /// reflows while there is one, and the assertion passes against every build
    /// that could be made to fail it. Both were measured against deliberately
    /// broken code before being dropped.
    /// </para>
    /// <para>
    /// What is left is the one number that moves: a scaled line covers more of
    /// the window and a reflowed one does not. <c>TransformToAncestor</c> rather
    /// than arithmetic, because the scale belongs to the <c>Viewbox</c> and
    /// computing it here would be asserting the number this test exists to
    /// check.
    /// </para>
    /// </remarks>
    /// <returns>The size the first line covers, in the window's own coordinates.</returns>
    internal Size LineOnTheWindow() =>
        OnItsOwnThread(window =>
        {
            TextBlock line = FirstVisibleLine(window)
                ?? throw new InvalidOperationException(
                    "The overlay is showing no text, so there is nothing to have scaled.");

            return line
                .TransformToAncestor(window)
                .TransformBounds(new Rect(0, 0, line.ActualWidth, line.ActualHeight))
                .Size;
        });

    /// <summary>
    /// Makes the overlay a given width, the way dragging the grip would.
    /// </summary>
    /// <param name="width">How wide to make it.</param>
    internal void ResizeTo(double width) =>
        OnItsOwnThread(window =>
        {
            Size render = window.NaturalSize;

            window.Width = width;
            window.Height = width * (render.Height / render.Width);
        });

    /// <summary>
    /// Shows or hides the furniture for moving and resizing.
    /// </summary>
    /// <param name="shown">Whether it should be on show.</param>
    internal void ShowChrome(bool shown) =>
        OnItsOwnThread(window => window.ShowsChrome = shown);

    /// <summary>
    /// The names of the chrome elements currently on screen, which is empty
    /// when the chrome is away.
    /// </summary>
    internal IReadOnlyList<string> ChromeOnScreen() =>
        OnItsOwnThread(window =>
        {
            List<string> shown = [];
            CollectChrome(window, shown);
            return (IReadOnlyList<string>)shown;
        });

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

        return dispatcher.Invoke(() => VisualTree.TextIn(window));
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

    /// <summary>
    /// Runs something against the window on the thread that owns it.
    /// </summary>
    private T OnItsOwnThread<T>(Func<GameOverlayWindow, T> what)
    {
        if (_dispatcher is not { } dispatcher || _window is not { } window)
        {
            throw new InvalidOperationException("The overlay is not running.");
        }

        return dispatcher.Invoke(() => what(window));
    }

    private void OnItsOwnThread(Action<GameOverlayWindow> what) =>
        OnItsOwnThread<object?>(window =>
        {
            what(window);
            return null;
        });

    /// <summary>
    /// The named elements that are actually rendering. A collapsed parent takes
    /// its children with it, so asking each element whether it is visible is
    /// what "on screen" means here.
    /// </summary>
    private static void CollectChrome(DependencyObject node, List<string> shown)
    {
        int children = VisualTreeHelper.GetChildrenCount(node);

        for (int child = 0; child < children; child++)
        {
            DependencyObject descendant = VisualTreeHelper.GetChild(node, child);

            if (descendant is FrameworkElement { Name.Length: > 0 } named
                && named.IsVisible
                && (named.Name == "DragBar" || named.Name == "Grip"))
            {
                shown.Add(named.Name);
            }

            CollectChrome(descendant, shown);
        }
    }

    /// <summary>
    /// The first line of the render that is actually being drawn.
    /// </summary>
    ///
    /// <remarks>
    /// Visibility is checked for the same reason <see cref="CollectChrome"/>
    /// checks it: the chrome's own label is a <c>TextBlock</c> in this tree, and
    /// a walk that ignored visibility would measure the drag bar's caption
    /// instead of the render's first line.
    /// </remarks>
    /// <param name="node">Where to start looking.</param>
    /// <returns>The line, or null if nothing is being drawn.</returns>
    private static TextBlock? FirstVisibleLine(DependencyObject node)
    {
        int children = VisualTreeHelper.GetChildrenCount(node);

        for (int child = 0; child < children; child++)
        {
            DependencyObject descendant = VisualTreeHelper.GetChild(node, child);

            if (descendant is TextBlock { IsVisible: true } line)
            {
                return line;
            }

            if (FirstVisibleLine(descendant) is { } found)
            {
                return found;
            }
        }

        return null;
    }

}
