using System;

namespace D47.Panel;

/// <summary>
/// Something Directive 47 may have to carry on without, carrying the reason it
/// is carrying on.
///
/// <para>
/// A nullable already says a value may be missing. What it cannot say is that
/// somebody has to write down *why*, and this project has now chosen "absent,
/// not failed" over dying three times — a hotkey another application owns
/// ([#103](https://github.com/retiring-studios/directive-47/issues/103)), a
/// machine that cannot composite
/// ([#101](https://github.com/retiring-studios/directive-47/issues/101)), and a
/// store that will not open
/// ([#117](https://github.com/retiring-studios/directive-47/issues/117)). Each
/// is by construction a thing that did not happen and did not complain, and the
/// second of them was silent from the day it was written until this story.
/// </para>
///
/// <para>
/// So the reason travels with the absence, and the only way to reach the value
/// is <see cref="Or"/>, which takes somewhere to put it. There is no property
/// that hands the value over quietly. Forgetting to record becomes a thing you
/// cannot express rather than a thing to remember.
/// </para>
///
/// <para>
/// Deliberately not a general-purpose optional. There is no mapping, no
/// chaining, and no way to ask whether it is present without dealing with the
/// absence — all of which would make it a convenient nullable again, which is
/// the thing it exists to stop being. Revisit trigger: a caller that genuinely
/// needs to know without recording, argued on its own merits.
/// </para>
/// </summary>
/// <typeparam name="T">What may or may not be there.</typeparam>
internal readonly struct Perhaps<T>
    where T : class
{
    private readonly T? _value;
    private readonly string? _absence;

    /// <summary>
    /// Private, so that the only two ways to make one are the two named below.
    /// An internal constructor would have accepted no value and no reason —
    /// an absence nobody explained, which is precisely what this type exists to
    /// make unsayable.
    /// </summary>
    private Perhaps(T? value, string? absence)
    {
        _value = value;
        _absence = absence;
    }

    /// <summary>
    /// Something that is there.
    /// </summary>
    /// <param name="value">The thing.</param>
    /// <returns>A present one.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="value"/> is null, which would be an absence with no
    /// reason — the state this type does not have.
    /// </exception>
    internal static Perhaps<T> Of(T value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return new Perhaps<T>(value, absence: null);
    }

    /// <summary>
    /// Something that is not there, and why.
    /// </summary>
    /// <param name="because">
    /// What to write down, in the words somebody reading the log needs. Not a
    /// stack trace and not an exception message on its own: the reader is a
    /// Commander wondering why a key did nothing.
    /// </param>
    /// <returns>An absent one.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="because"/> says nothing. An absence recorded as a blank
    /// line is the silence this was built against, spelled differently.
    /// </exception>
    internal static Perhaps<T> Absent(string because)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(because);

        return new Perhaps<T>(value: null, because);
    }

    /// <summary>
    /// The thing, or <see langword="null"/> — and when it is null, the reason
    /// has been written down first.
    /// </summary>
    /// <param name="record">Where to put the reason.</param>
    /// <returns>The thing, or <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="record"/> is null. Nowhere to put the reason is our
    /// defect, not the machine's, so it throws like any other.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// This is a <c>default(Perhaps&lt;T&gt;)</c>. A struct can always be
    /// default-constructed past whatever its constructors say, and the default
    /// here is neither present nor explained — so it is caught on the way out
    /// rather than handing back a silent null, which is the one thing this type
    /// promises never to do.
    /// </exception>
    internal T? Or(Action<string> record)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (_value is null && _absence is null)
        {
            throw new InvalidOperationException(
                "This is a default(Perhaps<T>) rather than one made by Of or Absent, so there "
                + "is neither a thing to hand over nor a reason to record.");
        }

        if (_absence is not null)
        {
            record(_absence);
        }

        return _value;
    }
}
