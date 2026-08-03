using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Windows.Input;

namespace D47.Panel;

/// <summary>
/// Modifiers and a key as one thing, spelled the way a person writes it.
///
/// <para>
/// The two have to travel together — into a claim, out of a file, back into a
/// file — and a pair passed as two arguments is a pair that can be swapped,
/// half-defaulted, or described one way in one place and another way in
/// another. The refusal message naming a combination the caller never asked for
/// was exactly that bug, caught in
/// [#103](https://github.com/retiring-studios/directive-47/issues/103).
/// </para>
/// </summary>
/// <param name="Modifiers">The modifiers to hold.</param>
/// <param name="Key">The key to press.</param>
internal sealed record Combination(ModifierKeys Modifiers, Key Key)
{
    private const char Between = '+';

    /// <summary>
    /// Reads a combination out of the words somebody wrote.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// Whatever <see cref="ToString"/> writes, this reads, and in any order and
    /// any case — a Commander editing <c>remembered.json</c> writes the
    /// modifiers in whatever order they think of them.
    /// </para>
    /// <para>
    /// A combination needs at least one modifier. Windows would take a bare
    /// letter, and a bare letter claimed system-wide swallows that key
    /// everywhere on the machine, including in the game — so the first the
    /// Commander knows of it is that typing has stopped working. The API
    /// accepting it does not make it what this setting means, which is the same
    /// line the overlay's opacity draws at 0 and 1.
    /// </para>
    /// </remarks>
    /// <param name="written">What the file said.</param>
    /// <param name="combination">What it meant, if it meant anything.</param>
    /// <returns>Whether it was a combination at all.</returns>
    internal static bool TryParse(
        string written, [NotNullWhen(true)] out Combination? combination)
    {
        combination = null;

        if (string.IsNullOrWhiteSpace(written))
        {
            return false;
        }

        ModifierKeys modifiers = ModifierKeys.None;
        Key? key = null;

        foreach (string part in written.Split(Between))
        {
            string token = part.Trim();

            if (ModifierNamed(token) is { } modifier)
            {
                modifiers |= modifier;
                continue;
            }

            // The second one is the giveaway that this is not a combination at
            // all rather than one with an odd key in it: "Ctrl+D+F" names two
            // keys, and picking either would be a guess.
            if (key is not null || KeyNamed(token) is not { } named)
            {
                return false;
            }

            key = named;
        }

        if (modifiers == ModifierKeys.None || key is not { } pressed)
        {
            return false;
        }

        combination = new Combination(modifiers, pressed);
        return true;
    }

    /// <summary>
    /// The combination, spelled the way a person writes it.
    /// </summary>
    ///
    /// <remarks>
    /// Built rather than taken from <c>ModifierKeys.ToString()</c>, which spells
    /// the flags in the enum's own order — "Alt, Control, Shift" — and would put
    /// both the refusal message and the line in <c>remembered.json</c> at the
    /// mercy of how somebody declared an enum.
    /// </remarks>
    /// <returns>Something like <c>Ctrl+Alt+D</c>.</returns>
    public override string ToString()
    {
        var described = new StringBuilder();

        if (Modifiers.HasFlag(ModifierKeys.Control))
        {
            described.Append("Ctrl").Append(Between);
        }

        if (Modifiers.HasFlag(ModifierKeys.Alt))
        {
            described.Append("Alt").Append(Between);
        }

        if (Modifiers.HasFlag(ModifierKeys.Shift))
        {
            described.Append("Shift").Append(Between);
        }

        if (Modifiers.HasFlag(ModifierKeys.Windows))
        {
            described.Append("Win").Append(Between);
        }

        return described.Append(Key).ToString();
    }

    /// <summary>
    /// The modifier a word names, or <see langword="null"/> if it names none.
    /// </summary>
    ///
    /// <remarks>
    /// The short spellings only, because these are the ones this writes and the
    /// ones anybody looking at a keyboard would type. <c>Control</c> is allowed
    /// beside <c>Ctrl</c> because WPF's own enum spells it that way and somebody
    /// will copy it from there.
    /// </remarks>
    private static ModifierKeys? ModifierNamed(string token) =>
        token.ToUpperInvariant() switch
        {
            "CTRL" or "CONTROL" => ModifierKeys.Control,
            "ALT" => ModifierKeys.Alt,
            "SHIFT" => ModifierKeys.Shift,
            "WIN" or "WINDOWS" => ModifierKeys.Windows,
            _ => null,
        };

    /// <summary>
    /// The key a word names, or <see langword="null"/> if it names none.
    /// </summary>
    ///
    /// <remarks>
    /// Spelling is what round-trips here, not the number behind it.
    /// <c>Enum.TryParse</c> says yes to <c>"1"</c> and hands back
    /// <see cref="Key.Cancel"/>, so a Commander writing the digit on their
    /// keyboard would silently get a key they have never heard of. Comparing
    /// the parse back against what was written is what rules that out, and it
    /// rules out <c>"999"</c> with it.
    /// </remarks>
    private static Key? KeyNamed(string token) =>
        Enum.TryParse(token, ignoreCase: true, out Key named)
        && named.ToString().Equals(token, StringComparison.OrdinalIgnoreCase)
            ? named
            : null;
}
