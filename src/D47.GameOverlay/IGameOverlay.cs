namespace D47.GameOverlay;

/// <summary>
/// The overlay, as the application deals with it: on screen or not, and two
/// ways to change that.
///
/// <para>
/// Deliberately says nothing about Elite, about windows, or about where the
/// thing goes. Finding the game and placing a window over it is interacting
/// with an external system, which is the adapter's job; deciding when to show
/// and when to hide is logic, and logic is asserted in CI against a stand-in
/// that is nothing but a boolean.
/// </para>
/// </summary>
public interface IGameOverlay
{
    /// <summary>
    /// Whether the overlay is currently on screen.
    /// </summary>
    bool IsVisible { get; }

    /// <summary>
    /// Puts the overlay over the game.
    ///
    /// <para>
    /// Does nothing when there is no game to be over. The game is looked for
    /// each time rather than once, so one that has moved — or one that started
    /// after Directive 47 did — is picked up on the next showing. That is not
    /// the same as following the game's lifecycle, which is nobody's story yet.
    /// </para>
    /// </summary>
    void Show();

    /// <summary>
    /// Takes the overlay off the screen, leaving it ready to be shown again.
    /// </summary>
    void Hide();
}
