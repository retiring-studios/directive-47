using System.Collections.Generic;
using System.Text.Json;

namespace D47.Data;

/// <summary>
/// A handful of named strings in a JSON file, and the three things anyone does
/// to them.
///
/// <para>
/// Both stores are this plus a loading rule. <see cref="DataStore"/> shrugs at
/// a key it does not know and <see cref="SettingsStore"/> refuses the file, which
/// is why they are two classes and stay two classes — but reading a name,
/// writing one, and taking one out again are the same three methods, and by the
/// third of them they were written twice.
/// </para>
///
/// <para>
/// Composed rather than inherited. Each store keeps its own public surface,
/// because "what was written down" and "what the Commander chose" are different
/// things to a caller even where the code underneath is not.
/// </para>
/// </summary>
internal sealed class KeyedJson
{
    /// <summary>
    /// Indented on purpose. These are files somebody opens when they are
    /// already puzzled — most likely because something told them one could not
    /// be read — and one long line is a worse answer than four short ones.
    /// </summary>
    private static readonly JsonSerializerOptions Layout = new() { WriteIndented = true };

    private readonly string _file;
    private readonly Dictionary<string, string> _values;

    /// <summary>
    /// Wraps what a store's loader made of the file.
    /// </summary>
    /// <param name="file">Where it lives.</param>
    /// <param name="values">Whatever was readable, which may be nothing.</param>
    internal KeyedJson(string file, Dictionary<string, string> values)
    {
        _file = file;
        _values = values;
    }

    /// <summary>
    /// Where this is kept.
    /// </summary>
    internal string File => _file;

    /// <summary>
    /// What is under a name, or <see langword="null"/> if nothing is.
    /// </summary>
    /// <param name="key">The name.</param>
    /// <returns>The value, or <see langword="null"/>.</returns>
    internal string? Read(string key) =>
        _values.TryGetValue(key, out string? value) ? value : null;

    /// <summary>
    /// Puts something under a name, and saves at once.
    /// </summary>
    ///
    /// <remarks>
    /// There is no separate save, because a save is a thing to forget. These
    /// files hold a handful of values, so rewriting all of them costs nothing
    /// worth measuring, and the alternative is a store that is correct in
    /// memory and wrong on disk for as long as nobody remembered to flush it.
    /// </remarks>
    /// <param name="key">The name.</param>
    /// <param name="value">What to put there.</param>
    internal void Write(string key, string value)
    {
        _values[key] = value;

        Save();
    }

    /// <summary>
    /// Takes a name out, and saves at once. Taking out something that was never
    /// there is not an error and writes nothing — the caller's intent is that it
    /// is gone afterwards, and it is.
    /// </summary>
    /// <param name="key">The name.</param>
    internal void Forget(string key)
    {
        if (_values.Remove(key))
        {
            Save();
        }
    }

    private void Save() =>
        DurableFile.Write(_file, JsonSerializer.Serialize(_values, Layout));
}
