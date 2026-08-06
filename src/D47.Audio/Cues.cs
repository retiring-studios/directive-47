using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

using D47.VoiceLoop;

namespace D47.Audio;

/// <summary>
/// The sound a state makes, and which one when there is more than one.
/// </summary>
///
/// <remarks>
/// <para>
/// Deciding rather than playing, and it opens no device — which is what lets it
/// be asserted in CI while <see cref="CuePlayer"/> cannot be. The cut every other
/// pair in this repository already makes.
/// </para>
/// <para>
/// Read from the assembly rather than from a list written out by hand. A name in
/// a table is a name that can be wrong, and the way it goes wrong is a Commander
/// hearing nothing in one state and nobody finding out.
/// </para>
/// </remarks>
public static class Cues
{
    /// <summary>
    /// Where the embedded names start. Set by the project file's Link, so it is
    /// stable rather than derived from wherever the files happen to sit on disk.
    /// </summary>
    private const string Inside = "D47.Audio.assets.cues.";

    private static readonly ImmutableDictionary<TurnState, ImmutableArray<string>> Sounds =
        FoundInTheAssembly();

    /// <summary>
    /// Every state that has anything to play, in no particular order.
    /// </summary>
    public static IReadOnlyCollection<TurnState> EveryStateWithASound { get; } =
        [.. Sounds.Keys];

    /// <summary>
    /// Everything a state could play.
    /// </summary>
    ///
    /// <remarks>
    /// Empty for a state with no sound, which is an ordinary answer rather than
    /// an absence to report — see <see cref="Pick"/>.
    /// </remarks>
    /// <param name="state">The state being entered.</param>
    /// <returns>The names of its sounds.</returns>
    public static IReadOnlyList<string> For(TurnState state) =>
        Sounds.TryGetValue(state, out ImmutableArray<string> set) ? set : [];

    /// <summary>
    /// One of a state's sounds, chosen at random.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// Nothing is a real answer. <see cref="TurnState.Speaking"/> has no sound
    /// deliberately — the reply is the sound, and a cue underneath it would be
    /// talking over the answer the Commander asked for.
    /// </para>
    /// <para>
    /// The randomness is passed in rather than reached for, so a test can say
    /// what it wants and the caller can decide whether two surfaces share a
    /// sequence.
    /// </para>
    /// </remarks>
    /// <param name="state">The state being entered.</param>
    /// <param name="draw">Where the choice comes from.</param>
    /// <returns>A sound to play, or <see langword="null"/> for silence.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="draw"/> is null.</exception>
    [SuppressMessage(
        "Security",
        "CA5394:Do not use insecure randomness",
        Justification =
            "It picks which of three chimes to play. Nothing is being protected, and a "
            + "cryptographic generator would cost an allocation per state transition to make "
            + "a sound less guessable.")]
    public static string? Pick(TurnState state, Random draw)
    {
        ArgumentNullException.ThrowIfNull(draw);

        IReadOnlyList<string> set = For(state);

        return set.Count == 0 ? null : set[draw.Next(set.Count)];
    }

    /// <summary>
    /// Opens one of the sounds.
    /// </summary>
    /// <param name="cue">A name from <see cref="For"/>.</param>
    /// <returns>The bytes, which the caller owns and must dispose.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cue"/> is null.</exception>
    public static Stream? Open(string cue)
    {
        ArgumentNullException.ThrowIfNull(cue);

        return typeof(Cues).Assembly.GetManifestResourceStream(cue);
    }

    /// <summary>
    /// Every embedded cue, grouped by the state its folder is named for.
    /// </summary>
    ///
    /// <remarks>
    /// A folder that is not a state's name is skipped rather than thrown over.
    /// Somebody adding an ambient sound under <c>assets/cues</c> should get an
    /// unused file, not an application that will not start.
    /// </remarks>
    private static ImmutableDictionary<TurnState, ImmutableArray<string>> FoundInTheAssembly()
    {
        ImmutableDictionary<TurnState, ImmutableArray<string>>.Builder found =
            ImmutableDictionary.CreateBuilder<TurnState, ImmutableArray<string>>();

        IEnumerable<IGrouping<string, string>> byFolder = typeof(Cues).Assembly
            .GetManifestResourceNames()
            .Where(name => name.StartsWith(Inside, StringComparison.Ordinal))
            .GroupBy(name => name[Inside.Length..].Split('.')[0]);

        foreach (IGrouping<string, string> folder in byFolder)
        {
            // Named, and the name checked back. Enum.TryParse also accepts
            // numbers, so a folder called "3" would come back as Speaking and
            // put ambient audio in the middle of a reply — which is the one
            // thing the empty Speaking set exists to prevent.
            if (Enum.TryParse(folder.Key, ignoreCase: true, out TurnState state)
                && state.ToString().Equals(folder.Key, StringComparison.OrdinalIgnoreCase))
            {
                found[state] = [.. folder.Order(StringComparer.Ordinal)];
            }
        }

        return found.ToImmutable();
    }
}
