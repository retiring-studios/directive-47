using System;
using System.Globalization;

using D47.GameOverlay;
using D47.Render;

namespace D47.Panel;

/// <summary>
/// The game overlay, as the application deals with it: one that may not exist,
/// a way to turn it on and off, and how see-through it is.
///
/// <para>
/// The decisions live here rather than in <c>D47.GameOverlay</c>, because
/// carrying on without a surface, knowing which way a toggle goes, and turning
/// what was remembered into a number are logic, and that project is an adapter.
/// It is also what makes all of it assertable in CI: a stand-in can refuse to be
/// created, and a stand-in overlay is a boolean and a number, where a real
/// machine cannot be asked to stop compositing and a real window needs a
/// desktop.
/// </para>
/// </summary>
internal sealed class Overlay
{
    /// <summary>
    /// What to say when the machine cannot host one.
    ///
    /// <para>
    /// This absence was silent from the day it was built until
    /// [#104](https://github.com/retiring-studios/directive-47/issues/104).
    /// Nothing was wrong with the code — the Commander simply had no overlay
    /// and nothing anywhere said so, which is exactly the failure that story is
    /// about.
    /// </para>
    /// </summary>
    private const string NoOverlay =
        "This machine cannot host an overlay over the game — it is not compositing the "
        + "desktop, which is what a transparent window needs. Directive 47 has started "
        + "without one; the panel is unaffected.";

    /// <summary>
    /// What the store calls how see-through the overlay is.
    ///
    /// <para>
    /// Spelled the way it would be said out loud, because <c>remembered.json</c>
    /// is a file somebody opens and reads. It is also, until there is a settings
    /// page, the way this value is changed.
    /// </para>
    /// </summary>
    private const string HowSeeThrough = "game overlay opacity";

    /// <summary>
    /// Where it starts on a machine that has never been told otherwise.
    ///
    /// <para>
    /// Not 1. The overlay covers a cockpit, and a setting that ships fully
    /// opaque is one nobody discovers. This is a number to react to rather than
    /// a measured one — the maintainer's own framing — and it is expected to
    /// move once it has been seen over a bright station interior.
    /// </para>
    /// </summary>
    private const double SeeThroughEnough = 0.75;

    private readonly IGameOverlay? _overlay;
    private readonly Func<IntPtr, bool> _isTheGame;

    private Overlay(IGameOverlay? overlay, Func<IntPtr, bool> isTheGame)
    {
        _overlay = overlay;
        _isTheGame = isTheGame;
    }

    /// <summary>
    /// Asks for an overlay, and accepts the answer either way.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// Nothing is caught. A factory that throws has a defect in it, and a defect
    /// reported as an unsupported machine is a defect nobody ever finds. The
    /// only thing this understands is <see langword="null"/>.
    /// </para>
    /// <para>
    /// The machine is asked at startup, before anything knows whether Elite is
    /// running. Whether a machine can host an overlay has nothing to do with
    /// whether the game happens to be up, and asking in the other order would
    /// leave this unreached — and so untested — on every machine without the
    /// game. That includes CI.
    /// </para>
    /// </remarks>
    /// <param name="overlays">Where an overlay comes from.</param>
    /// <param name="answer">What it should render.</param>
    /// <param name="remembered">
    /// What the last run left behind — how see-through the overlay should be,
    /// and eventually where it was put and what size it was left.
    /// </param>
    /// <param name="record">Where to note that there is no overlay, and why.</param>
    /// <param name="isTheGame">
    /// Whether a given window belongs to Elite. Defaults to asking the real
    /// game.
    /// </param>
    /// <returns>The overlay, or one that will quietly do nothing.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="overlays"/> or <paramref name="remembered"/> is null.
    /// </exception>
    internal static Overlay From(
        IGameOverlayFactory overlays,
        Answer answer,
        Store remembered,
        Action<string> record,
        Func<IntPtr, bool>? isTheGame = null)
    {
        ArgumentNullException.ThrowIfNull(overlays);
        ArgumentNullException.ThrowIfNull(remembered);

        // The reason is composed here rather than asked of the factory, and the
        // contract is what makes that honest: null from Create means the
        // machine and only the machine — no compositor, no driver — because
        // every other failure throws. So "this machine cannot host one" is the
        // whole of what null is allowed to mean, and saying it here keeps
        // Perhaps out of an adapter's public contract, where it would need a
        // home both projects can see and would be a project decision taken for
        // one type.
        Perhaps<IGameOverlay> made = overlays.Create(answer) is { } overlay
            ? Perhaps<IGameOverlay>.Of(overlay)
            : Perhaps<IGameOverlay>.Absent(NoOverlay);

        IGameOverlay? theOverlay = made.Or(record);

        // Before it is ever shown. Setting it afterwards would be a solid
        // rectangle over the cockpit for as long as the first frame lasts,
        // which is the thing this is for.
        //
        // Nothing is read on a machine with no overlay, which is what the
        // conditional buys over an if: an unusable opacity is only worth
        // reporting to somebody who has a surface it would have applied to.
        theOverlay?.Opacity = HowSeeThroughToBe(remembered, record);

        return new Overlay(theOverlay, isTheGame ?? EliteWindow.IsTheGame);
    }

    /// <summary>
    /// How see-through the overlay should be: whatever was left behind, or the
    /// default when nothing usable was.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// Read in the invariant culture, because <c>remembered.json</c> has to mean
    /// the same thing on every machine that opens it — a value written as
    /// <c>0,4</c> is a different number in half the world and no number in the
    /// other half.
    /// </para>
    /// <para>
    /// A value outside 0 to 1 is not an opacity, and neither is a word. Both are
    /// absences rather than failures, in the same sense as an unreadable store:
    /// the application starts, and says what it found. Refusing to start over a
    /// hand-edited number would be a worse trade for a setting whose whole
    /// purpose is to be adjusted.
    /// </para>
    /// </remarks>
    /// <param name="remembered">What the last run left behind.</param>
    /// <param name="record">Where to note a setting that could not be used.</param>
    /// <returns>The opacity to use.</returns>
    private static double HowSeeThroughToBe(Store remembered, Action<string> record)
    {
        if (remembered.Read(HowSeeThrough) is not { } written)
        {
            return SeeThroughEnough;
        }

        if (double.TryParse(
                written,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double opacity)
            && opacity is >= 0 and <= 1)
        {
            return opacity;
        }

        record(CouldNotUse(written));
        return SeeThroughEnough;
    }

    /// <summary>
    /// What to say about a remembered opacity that is not one. Quotes what was
    /// found, because the fix is a line in a file and nobody can correct a line
    /// they cannot identify.
    /// </summary>
    private static string CouldNotUse(string written) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"The game overlay's opacity was remembered as \"{written}\", which is not a number "
            + $"from 0 to 1. Directive 47 has started at {SeeThroughEnough} instead, and will keep "
            + $"using that until \"{HowSeeThrough}\" says something it can read.");

    /// <summary>
    /// Takes the mouse out of the way when the game comes forward, and takes it
    /// back when the game goes away.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// Both halves matter. While Elite is in front, a click over the overlay
    /// has to reach the cockpit underneath — an overlay that swallows one is
    /// worse than no overlay. While it is not, the overlay has to be reachable,
    /// or there is nothing for a Commander to grab and move.
    /// </para>
    /// <para>
    /// Told rather than asked. Windows says when the foreground changes and the
    /// application reacts; polling for it would be a timer that is either
    /// slower than an alt-tab or busy for no reason.
    /// </para>
    /// </remarks>
    /// <param name="window">Whatever is now in front.</param>
    internal void ForegroundIsNow(IntPtr window)
    {
        if (_overlay is not { } overlay)
        {
            return;
        }

        bool theGameIsInFront = _isTheGame(window);

        overlay.PassesInputThrough = theGameIsInFront;
        overlay.ShowsChrome = !theGameIsInFront;
    }

    /// <summary>
    /// Puts the overlay over the game if it is not on screen, and takes it away
    /// if it is.
    ///
    /// <para>
    /// Does nothing at all when the machine could not give us an overlay. There
    /// is no error to report: the Commander pressing a key for a thing this
    /// machine cannot draw is not a failure, it is the same absence the
    /// application already carried on without.
    /// </para>
    /// </summary>
    internal void Toggle()
    {
        if (_overlay is not { } overlay)
        {
            return;
        }

        if (overlay.IsVisible)
        {
            overlay.Hide();
            return;
        }

        overlay.Show();
    }

    /// <summary>
    /// Shows the overlay, if there is one and there is a game to put it over.
    /// </summary>
    internal void Show() => _overlay?.Show();
}
