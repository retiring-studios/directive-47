using System;
using System.Globalization;

namespace D47.Panel;

/// <summary>
/// Every setting Directive 47 has, named in one place.
///
/// <para>
/// The schema, and it is a list of names rather than a list of values because
/// the defaults are already where they belong — each consumer reads its key,
/// parses it, and falls back to its own constant when there is nothing to read.
/// That is already "defaults are never rewritten, overrides are a separate
/// layer". See
/// [#18](https://github.com/retiring-studios/directive-47/issues/18).
/// </para>
///
/// <para>
/// One place, because the point of the file is that a key it does not know is a
/// fault. A schema assembled from two places is one that disagrees with itself
/// the first time somebody adds a setting and forgets the other half.
/// </para>
///
/// <para>
/// What is <em>not</em> here is as deliberate: where the game overlay was left
/// and how big it is are running memory, not settings. There is no right answer
/// to either until the Commander drags something, so neither has a default and
/// neither belongs on a page.
/// </para>
/// </summary>
internal static class Settings
{
    /// <summary>
    /// Every key <c>settings.json</c> recognizes. Anything else in that file is
    /// a fault and the file is not applied.
    /// </summary>
    internal static readonly string[] Known =
    [
        ChosenHotkey.WhatItIsCalled,
        PushToTalk.WhatItIsCalled,
        Overlay.HowSeeThrough,
        Zoom.HowBig,
    ];

    /// <summary>
    /// What a setting is when the Commander has not chosen otherwise, written
    /// the way the file would write it.
    /// </summary>
    ///
    /// <remarks>
    /// Taken from each consumer's own constant rather than restated here. The
    /// defaults live with the code that falls back to them — that is what
    /// "defaults are never rewritten, overrides are a separate layer" means in
    /// practice — and a second copy for the settings page to show would be a
    /// second thing to keep in step.
    ///
    /// <para>
    /// The page shows these behind an empty box rather than in it. A default
    /// sitting in the box as text is a value the Commander can commit by
    /// tabbing past it, which would write the shipped answer into the file as
    /// an override and make the two layers one.
    /// </para>
    /// </remarks>
    /// <param name="setting">The setting's key.</param>
    /// <returns>The default, or empty for a setting that has none.</returns>
    internal static string DefaultFor(string setting) => setting switch
    {
        ChosenHotkey.WhatItIsCalled => ChosenHotkey.Familiar.ToString(),
        PushToTalk.WhatItIsCalled => PushToTalk.ShippedWith.ToString(),
        Overlay.HowSeeThrough =>
            Overlay.SeeThroughEnough.ToString(CultureInfo.InvariantCulture),
        Zoom.HowBig =>
            Math.Round(Zoom.LifeSize * 100).ToString(CultureInfo.InvariantCulture),
        _ => string.Empty,
    };
}
