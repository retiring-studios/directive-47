using System;
using System.Collections.Generic;
using System.Globalization;

namespace D47.Panel;

/// <summary>
/// How big the panel draws what it is showing — a browser's zoom, and not a
/// resize.
///
/// <para>
/// The two are independent and stay that way. Resizing the window reflows the
/// content into a different shape at the same size; zooming changes the size and
/// lets it reflow into the window it is already in. A Commander who wants the
/// text bigger and a Commander who wants the window bigger are asking for
/// different things.
/// </para>
///
/// <para>
/// Logic rather than a control, for the same reason <see cref="Overlay"/> is:
/// where the levels are, what the ends do, and what a remembered value turns
/// into are decisions, and decisions are assertable in CI without a window.
/// Applying the result is <c>MainWindow</c>'s job and needs one.
/// </para>
/// </summary>
internal sealed class Zoom
{
    /// <summary>
    /// What the store calls this, spelled the way it would be said out loud —
    /// <c>remembered.json</c> is a file somebody opens and reads.
    /// </summary>
    internal const string HowBig = "panel zoom";

    /// <summary>
    /// Life size, where a panel that has never been told otherwise sits.
    /// </summary>
    internal const double LifeSize = 1.0;

    /// <summary>
    /// The sizes the panel will draw at, smallest first.
    /// </summary>
    ///
    /// <remarks>
    /// A browser's levels rather than a multiplier. A fixed percentage step
    /// either crawls at the bottom of the range or leaps at the top, and these
    /// are spaced the way they are because 110% is worth having and 105% is not.
    /// They stop at 300%: past that a single line of the render is wider than
    /// the window at any size worth opening, and every step is a step into
    /// scrolling sideways through one label.
    /// </remarks>
    internal static readonly IReadOnlyList<double> Levels =
        [0.5, 0.67, 0.75, 0.8, 0.9, 1.0, 1.1, 1.25, 1.5, 1.75, 2.0, 2.5, 3.0];

    private readonly Store _remembered;
    private int _level;

    /// <summary>
    /// Picks up wherever the last run left off.
    /// </summary>
    /// <param name="remembered">Where the last run wrote it down.</param>
    /// <param name="record">Where to say so when what was written cannot be used.</param>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    public Zoom(Store remembered, Action<string> record)
    {
        ArgumentNullException.ThrowIfNull(remembered);
        ArgumentNullException.ThrowIfNull(record);

        _remembered = remembered;
        _level = WhereItWasLeft(remembered, record);
    }

    /// <summary>
    /// How much bigger than life size the panel is drawing.
    /// </summary>
    public double Factor => Levels[_level];

    /// <summary>
    /// The zoom as it would be said and as the panel shows it — "100%".
    /// </summary>
    public string AsSaid => AsWritten(Factor) + "%";

    /// <summary>
    /// Whether there is anywhere further in to go, which is what greys out the
    /// panel's own control at the end of the range.
    /// </summary>
    public bool CanGoIn => _level < Levels.Count - 1;

    /// <summary>
    /// Whether there is anywhere further out to go.
    /// </summary>
    public bool CanGoOut => _level > 0;

    /// <summary>
    /// Said whenever the zoom changes, so whoever is drawing can draw it again.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// One level bigger, or nothing at all if this is already the biggest.
    /// </summary>
    public void In() => MoveTo(_level + 1);

    /// <summary>
    /// One level smaller, or nothing at all if this is already the smallest.
    /// </summary>
    public void Out() => MoveTo(_level - 1);

    /// <summary>
    /// Straight back to life size, however far away it is.
    /// </summary>
    ///
    /// <remarks>
    /// The way back, and the reason the range can be as wide as it is. Without
    /// one, a Commander who has leant on the wheel has to count their way home.
    /// </remarks>
    public void Reset() => MoveTo(Nearest(Percentage(LifeSize)));

    /// <summary>
    /// Moves to a level if it exists and is not the one already showing, and
    /// writes it down.
    /// </summary>
    ///
    /// <remarks>
    /// Silent at the ends rather than throwing. Leaning on Ctrl and the wheel is
    /// the ordinary way to arrive at one, and a Commander who has arrived there
    /// has not made a mistake.
    /// </remarks>
    /// <param name="level">Which level to move to.</param>
    private void MoveTo(int level)
    {
        if (level < 0 || level >= Levels.Count || level == _level)
        {
            return;
        }

        _level = level;

        _remembered.Write(HowBig, AsWritten(Factor));

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Where the last run left it, or life size.
    /// </summary>
    ///
    /// <remarks>
    /// Loud when what is written down cannot be used, because carrying on at
    /// life size in silence looks exactly like the edit having worked. The value
    /// is matched to a level rather than used as it stands: a hand-typed 137
    /// belongs at one of the sizes the panel draws at, and the nearest one is
    /// what the Commander meant.
    /// </remarks>
    /// <param name="remembered">Where the last run wrote it down.</param>
    /// <param name="record">Where to say so when what was written cannot be used.</param>
    /// <returns>The level to start at.</returns>
    private static int WhereItWasLeft(Store remembered, Action<string> record)
    {
        int lifeSize = Nearest(Percentage(LifeSize));

        if (remembered.Read(HowBig) is not { } written)
        {
            return lifeSize;
        }

        if (double.TryParse(written, NumberStyles.Float, CultureInfo.InvariantCulture, out double percent)
            && percent >= Percentage(Levels[0])
            && percent <= Percentage(Levels[^1]))
        {
            return Nearest(percent);
        }

        record(CouldNotUse(written));

        return lifeSize;
    }

    /// <summary>
    /// The level closest to a percentage somebody wrote down.
    /// </summary>
    /// <param name="percent">What was written.</param>
    /// <returns>The level nearest to it.</returns>
    private static int Nearest(double percent)
    {
        int nearest = 0;

        for (int level = 1; level < Levels.Count; level++)
        {
            if (Math.Abs(Percentage(Levels[level]) - percent)
                < Math.Abs(Percentage(Levels[nearest]) - percent))
            {
                nearest = level;
            }
        }

        return nearest;
    }

    /// <summary>
    /// A factor as the whole number of percent it is written down and spoken as.
    /// </summary>
    private static double Percentage(double factor) => Math.Round(factor * 100);

    /// <summary>
    /// A factor as it goes into the file: whole percent, no decimal point, the
    /// same thing somebody would type there by hand.
    /// </summary>
    private static string AsWritten(double factor) =>
        Percentage(factor).ToString("0", CultureInfo.InvariantCulture);

    /// <summary>
    /// What to say about a remembered zoom that is not one. Names the key,
    /// because which line to go and fix is exactly what somebody with the file
    /// open needs to know.
    /// </summary>
    private static string CouldNotUse(string written) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"The panel zoom was remembered as \"{written}\", which is not a percentage between "
            + $"{Percentage(Levels[0])} and {Percentage(Levels[^1])}. Directive 47 is drawing the "
            + $"panel at life size, and will remember a new zoom as soon as one is set — until "
            + $"\"{HowBig}\" says something it can read.");
}
