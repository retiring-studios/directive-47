using System;

namespace D47.GameOverlay;

/// <summary>
/// The overlay over the running game — the window, plus the one thing the
/// window cannot know for itself, which is where Elite currently is.
///
/// <para>
/// A wrapper rather than an interface on <see cref="GameOverlayWindow"/>
/// itself. <c>Window</c> already has <c>Show</c>, <c>Hide</c> and
/// <c>IsVisible</c>, so a window implementing <see cref="IGameOverlay"/>
/// directly would satisfy the contract with WPF's own members and quietly skip
/// finding the game. Shadowing them with <c>new</c> would work and would be a
/// trap for whoever called the base method next.
/// </para>
/// </summary>
public sealed class EliteOverlay : IGameOverlay
{
    private readonly GameOverlayWindow _window;

    /// <summary>
    /// Wraps a window.
    /// </summary>
    /// <param name="window">The overlay window.</param>
    /// <exception cref="ArgumentNullException"><paramref name="window"/> is null.</exception>
    public EliteOverlay(GameOverlayWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);

        _window = window;
    }

    /// <inheritdoc />
    public bool IsVisible => _window.IsVisible;

    /// <inheritdoc />
    public bool PassesInputThrough
    {
        get => _window.PassesInputThrough;
        set => _window.PassesInputThrough = value;
    }

    /// <inheritdoc />
    public bool ShowsChrome
    {
        get => _window.ShowsChrome;
        set => _window.ShowsChrome = value;
    }

    /// <inheritdoc />
    ///
    /// <remarks>
    /// The window's own <c>Opacity</c>, which WPF applies to everything drawn
    /// in it. Nothing inside the render knows this happened, which is the point
    /// — the panel shows the same render and is fully opaque.
    /// </remarks>
    public double Opacity
    {
        get => _window.Opacity;
        set => _window.Opacity = value;
    }

    /// <inheritdoc />
    public void Show()
    {
        if (EliteWindow.Find() is { } game)
        {
            _window.ShowOver(game);
        }
    }

    /// <inheritdoc />
    public void Hide() => _window.Hide();
}
