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
    /// Whether the mouse goes straight through to whatever is underneath.
    ///
    /// <para>
    /// True while Elite is in front, because a rectangle that quietly swallows
    /// a click is not something to put on a cockpit. False when it is not,
    /// because an overlay nothing can ever click is an overlay nothing can ever
    /// grab and move.
    /// </para>
    /// </summary>
    bool PassesInputThrough { get; set; }

    /// <summary>
    /// Whether the furniture for moving and resizing is on show.
    ///
    /// <para>
    /// The other half of the same fact as
    /// <see cref="PassesInputThrough"/> — the game being in front means no
    /// chrome and a mouse that goes straight past; the game being away means
    /// chrome and a mouse that lands. Two properties rather than one flag,
    /// because each says what it controls and a test can hold them apart.
    /// </para>
    /// </summary>
    bool ShowsChrome { get; set; }

    /// <summary>
    /// How solid the overlay is, from 0 for invisible to 1 for opaque.
    ///
    /// <para>
    /// The whole surface, text included. One number for all of it is what a
    /// window does unaided; the alternative — no background on the render, and
    /// each surface supplying its own backdrop so the text stays solid at every
    /// setting — was considered and not taken, because it moves the backdrop out
    /// of the render for a benefit nobody has found they need yet.
    /// </para>
    ///
    /// <para>
    /// Not <c>AllowsTransparency</c>, which is the capability to have per-pixel
    /// alpha at all and is already on. This is the knob.
    /// </para>
    /// </summary>
    double Opacity { get; set; }

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
