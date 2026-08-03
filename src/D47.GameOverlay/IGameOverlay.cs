using System;
using System.Windows;

namespace D47.GameOverlay;

/// <summary>
/// The overlay, as the application deals with it: on screen or not, two ways to
/// change that, and where on the screen it is.
///
/// <para>
/// Deliberately says nothing about Elite or about windows. Finding the game and
/// moving a window is interacting with an external system, which is the
/// adapter's job; deciding when to show, when to hide, and where it ought to go
/// is logic, and logic is asserted in CI against a stand-in that is nothing but
/// a few fields.
/// </para>
///
/// <para>
/// It does now say where the overlay is, which is the one thing the interface
/// used to withhold. Nothing outside could ask, so nothing outside could write
/// it down — and an overlay that forgets where it was put is one more thing to
/// set up before every session.
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
    /// Where the overlay's top-left corner is, in physical screen pixels.
    ///
    /// <para>
    /// Physical rather than the device-independent units WPF places windows
    /// with, because every other coordinate this feature touches is physical —
    /// where Windows says the game is, where Windows says the overlay is, and
    /// how far the screens reach. One set of units end to end means no
    /// conversion anywhere, and a number in <c>remembered.json</c> that means
    /// the same thing as the one any other tool on the machine would report.
    /// </para>
    /// </summary>
    Point Position { get; }

    /// <summary>
    /// How much bigger than the render's own size the overlay has been made. 1
    /// is the size at which the render is drawn exactly as it was laid out.
    ///
    /// <para>
    /// A ratio rather than a width, so it survives the render changing size.
    /// The overlay has one degree of freedom — it keeps the render's shape at
    /// every size — so one number is the whole of how big it is.
    /// </para>
    /// </summary>
    double Scale { get; }

    /// <summary>
    /// Puts the overlay at a given corner and size, instead of over the game.
    ///
    /// <para>
    /// One call rather than two settable properties, because a position applied
    /// separately from a size is a frame of the overlay in a place it was never
    /// asked to be. Called before the overlay is ever shown, so there is
    /// nothing to see move.
    /// </para>
    /// </summary>
    /// <param name="corner">Its top-left corner, in physical screen pixels.</param>
    /// <param name="scale">How much bigger than the render's own size to be.</param>
    void PlaceAt(Point corner, double scale);

    /// <summary>
    /// Raised when the Commander has finished dragging the overlay somewhere
    /// else or pulling it to a different size.
    ///
    /// <para>
    /// At the end of the gesture rather than throughout it. A drag is hundreds
    /// of positions and one intent, and something that wrote each of them down
    /// would be a file rewritten per mouse move to record a place the overlay
    /// was passing through.
    /// </para>
    /// </summary>
    event EventHandler MovedOrResized;

    /// <summary>
    /// Puts the overlay on screen — where it was left, or over the game if it
    /// has never been put anywhere.
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
