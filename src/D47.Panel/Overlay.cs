using System;
using System.Globalization;
using System.Windows;

using D47.Data;

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
    /// What the store calls where the overlay was left, and how big.
    ///
    /// <para>
    /// Three plain numbers under three names rather than one line holding all
    /// of them, for the same reason the file is indented: somebody correcting a
    /// position by hand should be able to change the one they mean. Spelled the
    /// way they would be said out loud, like everything else in there.
    /// </para>
    /// </summary>
    private const string HowFarAcross = "game overlay left";

    /// <inheritdoc cref="HowFarAcross"/>
    private const string HowFarDown = "game overlay top";

    /// <inheritdoc cref="HowFarAcross"/>
    private const string HowBig = "game overlay scale";

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
    /// <param name="presented">What it should render.</param>
    /// <param name="remembered">
    /// What the last run left behind — how see-through the overlay should be,
    /// and eventually where it was put and what size it was left.
    /// </param>
    /// <param name="record">Where to note that there is no overlay, and why.</param>
    /// <param name="isTheGame">
    /// Whether a given window belongs to Elite. Defaults to asking the real
    /// game.
    /// </param>
    /// <param name="whereTheScreensAre">
    /// How far this machine's screens reach between them, for deciding whether
    /// a remembered place is still somewhere the Commander could see it.
    /// Defaults to asking the real desktop.
    /// </param>
    /// <returns>The overlay, or one that will quietly do nothing.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="overlays"/> or <paramref name="remembered"/> is null.
    /// </exception>
    internal static Overlay From(
        IGameOverlayFactory overlays,
        Presentation presented,
        DataStore remembered,
        Action<string> record,
        Func<IntPtr, bool>? isTheGame = null,
        Func<Rect>? whereTheScreensAre = null)
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
        Perhaps<IGameOverlay> made = overlays.Create(presented) is { } overlay
            ? Perhaps<IGameOverlay>.Of(overlay)
            : Perhaps<IGameOverlay>.Absent(NoOverlay);

        IGameOverlay? theOverlay = made.Or(record);

        // Nothing at all is read on a machine with no overlay, which is what
        // the conditional buys: an unusable setting is only worth reporting to
        // somebody who has a surface it would have been applied to.
        if (theOverlay is { } surface)
        {
            // Before it is ever shown. Setting either of these afterwards would
            // be a solid rectangle at the game's corner for as long as the first
            // frame lasts, which is the thing they are both for.
            surface.Opacity = HowSeeThroughToBe(remembered, record);

            if (WhereItWasLeft(remembered, record, whereTheScreensAre ?? Desktop.Everything)
                is { } place)
            {
                surface.PlaceAt(place.Corner, place.Scale);
            }

            // Written down as the Commander lets go, rather than on the way
            // out. There is no way out to hook that reaches this — the panel's
            // close hides it and the deliberate exit is on the tray menu — and
            // a place that only survives a clean shutdown is one that does not
            // survive a crash.
            surface.MovedOrResized += (_, _) => WriteDownWhereItIs(surface, remembered);
        }

        return new Overlay(theOverlay, isTheGame ?? EliteWindow.IsTheGame);
    }

    /// <summary>
    /// How see-through the overlay should be: whatever was left behind, or the
    /// default when nothing usable was.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// Read through <see cref="AsNumber"/>, which is where this file's one
    /// opinion about how numbers are spelled lives.
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
    private static double HowSeeThroughToBe(
        DataStore remembered, Action<string> record)
    {
        if (remembered.Read(HowSeeThrough) is not { } written)
        {
            return SeeThroughEnough;
        }

        if (AsNumber(written) is { } opacity && opacity is >= 0 and <= 1)
        {
            return opacity;
        }

        record(CouldNotUse(written));
        return SeeThroughEnough;
    }

    /// <summary>
    /// Where the overlay should go and how big it should be: wherever it was
    /// left, or nothing at all — which leaves the overlay to put itself over the
    /// game, the way a first run does.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// Three values or none. A file with one of them in it is somebody's
    /// half-finished edit rather than a place, and picking the two that did
    /// parse would put the overlay somewhere nobody asked for. Nothing written
    /// down at all is the ordinary first run and is not an event; anything else
    /// is said out loud, because a setting that was quietly ignored looks
    /// exactly like an overlay that failed to come back.
    /// </para>
    /// <para>
    /// The same trade as the opacity above, and for the same reason. This is a
    /// file the Commander opens and edits, so refusing to start over a
    /// hand-typed number would be a worse presented than starting where a first
    /// run would have started.
    /// </para>
    /// </remarks>
    /// <param name="remembered">What the last run left behind.</param>
    /// <param name="record">Where to note a place that could not be used.</param>
    /// <param name="screens">How far this machine's screens reach.</param>
    /// <returns>The place to use, or null for none.</returns>
    private static (Point Corner, double Scale)? WhereItWasLeft(
        DataStore remembered, Action<string> record, Func<Rect> screens)
    {
        string? across = remembered.Read(HowFarAcross);
        string? down = remembered.Read(HowFarDown);
        string? size = remembered.Read(HowBig);

        if (across is null && down is null && size is null)
        {
            return null;
        }

        // A scale of zero or less parses perfectly and is not a size. Nothing
        // caps it from above: too big is a mistake somebody can see and drag
        // back, where too small is an overlay with no grip to grab.
        if (AsNumber(across) is not { } x
            || AsNumber(down) is not { } y
            || AsNumber(size) is not { } scale
            || scale <= 0)
        {
            record(CouldNotUsePlace(across, down, size));
            return null;
        }

        var corner = new Point(x, y);
        Rect desktop = screens();

        // The monitor that is not there any more. The numbers are fine and the
        // place is nowhere, and an overlay restored onto it is one the Commander
        // can neither see nor grab — a failure with no symptom except that the
        // overlay did not come back.
        if (!desktop.Contains(corner))
        {
            record(NoLongerOnAnyScreen(corner, desktop));
            return null;
        }

        return (corner, scale);
    }

    /// <summary>
    /// A number as <c>remembered.json</c> spells them, or null for anything
    /// that is not one.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// The invariant culture, because <c>remembered.json</c> has to mean the
    /// same thing on every machine that opens it — a value written as
    /// <c>0,4</c> is a different number in half the world and no number in the
    /// other half. Every number this file reads comes through here, so that is
    /// decided once rather than per setting.
    /// </para>
    /// <para>
    /// Infinities parse and are neither places nor sizes, which is what the
    /// finiteness check is for. A NaN would fail every comparison it was put
    /// through afterwards anyway, and is excluded here rather than left to
    /// that.
    /// </para>
    /// </remarks>
    private static double? AsNumber(string? written) =>
        double.TryParse(written, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
        && double.IsFinite(value)
            ? value
            : null;

    /// <summary>
    /// Writes down where the overlay has ended up.
    /// </summary>
    ///
    /// <remarks>
    /// Three writes and so three saves, because the store has no way to put
    /// several values down at once and inventing one for this would be a change
    /// to something every other stored value goes through. The file holds a
    /// handful of short strings, and this happens once per gesture rather than
    /// once per mouse move.
    /// </remarks>
    /// <param name="overlay">The overlay that has just been let go of.</param>
    /// <param name="remembered">Where to write it down.</param>
    private static void WriteDownWhereItIs(
        IGameOverlay overlay, DataStore remembered)
    {
        Point corner = overlay.Position;

        remembered.Write(HowFarAcross, AsWritten(corner.X));
        remembered.Write(HowFarDown, AsWritten(corner.Y));
        remembered.Write(HowBig, AsWritten(overlay.Scale));
    }

    /// <summary>
    /// A number the way it goes into the file — invariant, so it reads back the
    /// same on any machine.
    /// </summary>
    private static string AsWritten(double value) =>
        value.ToString(CultureInfo.InvariantCulture);

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
    /// What to say about a remembered place that is not one. Names all three
    /// values, because which of them is wrong is exactly what somebody about to
    /// edit the file needs to know.
    /// </summary>
    private static string CouldNotUsePlace(string? across, string? down, string? size) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"The game overlay was remembered at \"{HowFarAcross}\": \"{across}\", "
            + $"\"{HowFarDown}\": \"{down}\", \"{HowBig}\": \"{size}\" — which is not two numbers "
            + $"and a size above zero. Directive 47 has put the overlay over the game instead, and "
            + $"will remember a new place as soon as one is moved there.");

    /// <summary>
    /// What to say about a place that has stopped existing. Names the screens
    /// as well as the position, because the position on its own looks perfectly
    /// reasonable and the whole point is that the desktop changed under it.
    /// </summary>
    private static string NoLongerOnAnyScreen(Point corner, Rect screens) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"The game overlay was left at {corner.X},{corner.Y}, which is not on any screen this "
            + $"machine has now — they reach {screens}. Directive 47 has put the overlay over the "
            + $"game instead, and will remember a new place as soon as one is moved there.");

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
