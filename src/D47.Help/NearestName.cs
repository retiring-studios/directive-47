using System;
using System.Collections.Generic;

namespace D47.Help;

/// <summary>
/// The closest of a handful of short canonical names to what was actually said,
/// or nothing when none of them is close.
///
/// <para>
/// Internal, and named for the one job it does rather than for the general one
/// it resembles. Search machinery will want name resolution too, over a far
/// larger and more hostile vocabulary — but that is a second call site, and the
/// question of whether the two share machinery or a package is worth asking
/// once there are two of them to compare rather than one to guess from.
/// </para>
/// </summary>
internal static class NearestName
{
    /// <summary>
    /// The nearest candidate within tolerance, preferring the earlier of two
    /// equally near ones — registration order is already the order a spoken
    /// answer reads in, and an arbitrary tie-break would answer the same
    /// utterance differently between runs.
    /// </summary>
    /// <param name="utterance">What was heard.</param>
    /// <param name="candidates">The canonical names, in the order they were registered.</param>
    /// <returns>The nearest candidate, or <see langword="null"/> if none is close.</returns>
    internal static string? To(string utterance, IReadOnlyList<string> candidates)
    {
        string heard = utterance.Trim().ToUpperInvariant();
        string? nearest = null;
        int nearestDistance = int.MaxValue;

        foreach (string candidate in candidates)
        {
            int distance = Distance(heard, candidate.ToUpperInvariant());

            if (distance < nearestDistance && distance <= Tolerance(candidate))
            {
                nearest = candidate;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    /// <summary>
    /// How wrong a name may be and still be the one that was meant. Two edits
    /// flat for short names, because "fewl" for "fuel" is two and is obviously
    /// fuel; proportional above that, so a long name is not held to the same
    /// absolute standard as a four-letter one.
    /// </summary>
    private static int Tolerance(string candidate) => Math.Max(2, candidate.Length / 3);

    /// <summary>
    /// Levenshtein distance, two rows at a time. Textbook, and deliberately so:
    /// the alternative is a dependency inside a single-file exe for a function
    /// that reads in one screen.
    /// </summary>
    private static int Distance(string left, string right)
    {
        int[] previous = new int[right.Length + 1];
        int[] current = new int[right.Length + 1];

        for (int j = 0; j <= right.Length; j++)
        {
            previous[j] = j;
        }

        for (int i = 1; i <= left.Length; i++)
        {
            current[0] = i;

            for (int j = 1; j <= right.Length; j++)
            {
                int substitution = previous[j - 1] + (left[i - 1] == right[j - 1] ? 0 : 1);

                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), substitution);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }
}
