using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace D47.Data;

/// <summary>
/// What the Commander chose, as opposed to what the last run left behind.
///
/// <para>
/// The second file, and the difference between it and
/// <see cref="DataStore"/> is one row of a table: a key this does not know is a
/// fault, and a key that one does not know is ignored. One loader cannot both
/// fail loudly and shrug, which is why there are two. See
/// [#18](https://github.com/retiring-studios/directive-47/issues/18).
/// </para>
///
/// <para>
/// Defaults are not in here. Every consumer already reads its key, parses it,
/// and falls back to a constant when it is missing or unusable — which is
/// already "defaults are never rewritten, overrides are a separate layer". So
/// the schema this needs is a list of names, and a file holding nothing is a
/// Commander who has changed nothing.
/// </para>
/// </summary>
public sealed class SettingsStore
{
    /// <summary>
    /// Named for what it holds, the same way <c>remembered.json</c> is.
    /// </summary>
    private const string FileName = "settings.json";

    /// <summary>
    /// Indented, because this is the one file a Commander is meant to be able
    /// to open and read.
    /// </summary>
    private static readonly JsonSerializerOptions Layout = new() { WriteIndented = true };

    private readonly string _file;
    private readonly Dictionary<string, string> _chosen;

    private SettingsStore(string file, Dictionary<string, string> chosen)
    {
        _file = file;
        _chosen = chosen;
    }

    /// <summary>
    /// Opens the settings the application uses.
    /// </summary>
    /// <param name="known">Every key the schema recognizes.</param>
    /// <param name="record">Where to note a file that could not be used, and why.</param>
    /// <returns>The settings, holding whatever the Commander has chosen.</returns>
    public static SettingsStore Open(
        IReadOnlyCollection<string> known, Action<string> record) =>
        OpenAt(ApplicationData.File(FileName), known, record);

    /// <summary>
    /// Opens settings at a given path, for a test that must not touch the
    /// Commander's own.
    /// </summary>
    /// <param name="file">Where the settings are, or will be.</param>
    /// <param name="known">Every key the schema recognizes.</param>
    /// <param name="record">Where to note a file that could not be used, and why.</param>
    /// <returns>The settings, or empty ones when the file could not be used.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="known"/> or <paramref name="record"/> is null.
    /// </exception>
    public static SettingsStore OpenAt(
        string file, IReadOnlyCollection<string> known, Action<string> record)
    {
        ArgumentNullException.ThrowIfNull(known);
        ArgumentNullException.ThrowIfNull(record);

        return new(file, ReadFrom(file, known, record));
    }

    /// <summary>
    /// Moves settings an earlier version wrote into the running-memory file
    /// across to where they now belong, and takes them out of the old one.
    /// </summary>
    ///
    /// <remarks>
    /// Deliberately temporary. There is no supported upgrade path from a
    /// prerelease once 1.0.0 exists, so this comes out then. Until it does, it
    /// is what keeps an installed build's hotkey, opacity and zoom from
    /// vanishing on the run that splits the file in two.
    ///
    /// <para>
    /// A setting already chosen in the new file wins. The migration can run
    /// after the Commander has used the settings page — every run calls it —
    /// and undoing them would be worse than never having moved anything.
    /// </para>
    /// </remarks>
    /// <param name="remembered">
    /// The running-memory file, possibly still holding settings.
    /// </param>
    /// <param name="settings">Where they belong now.</param>
    /// <param name="known">Every key the schema recognizes.</param>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    public static void MigrateFrom(
        DataStore remembered, SettingsStore settings, IReadOnlyCollection<string> known)
    {
        ArgumentNullException.ThrowIfNull(remembered);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(known);

        foreach (string key in known)
        {
            if (remembered.Read(key) is not { } left || settings.Read(key) is not null)
            {
                continue;
            }

            settings.Write(key, left);
            remembered.Forget(key);
        }
    }

    /// <summary>
    /// What the Commander chose for a setting, or <see langword="null"/> if
    /// they have not chosen one and the default stands.
    /// </summary>
    /// <param name="key">The setting's name.</param>
    /// <returns>The chosen value, or <see langword="null"/>.</returns>
    public string? Read(string key) =>
        _chosen.TryGetValue(key, out string? value) ? value : null;

    /// <summary>
    /// Records a choice, and saves at once for the same reason
    /// <see cref="DataStore.Write"/> does.
    /// </summary>
    /// <param name="key">The setting's name.</param>
    /// <param name="value">What the Commander chose.</param>
    public void Write(string key, string value)
    {
        _chosen[key] = value;

        DurableFile.Write(_file, JsonSerializer.Serialize(_chosen, Layout));
    }

    /// <summary>
    /// Reads the file, and refuses all of it rather than part of it when it
    /// holds a key the schema does not know.
    /// </summary>
    ///
    /// <remarks>
    /// Three outcomes, and only one of them is an event. No file is a Commander
    /// who has changed nothing, which is every fresh install. A file that will
    /// not parse, or holds a key we do not know, is somebody's edit gone wrong
    /// — and the whole file goes, because applying the half that parsed leaves
    /// them hunting for the half that did not.
    ///
    /// <para>
    /// It does not throw. An unknown key means a hand-edited file or a
    /// downgrade, and neither should leave a Commander with an application that
    /// will not launch and no surface left to say why.
    /// </para>
    /// </remarks>
    private static Dictionary<string, string> ReadFrom(
        string file, IReadOnlyCollection<string> known, Action<string> record)
    {
        string written;

        try
        {
            written = File.ReadAllText(file);
        }

        // The two silent ones first, and both derive from IOException, so
        // moving either below it turns every first run into a fault.
        catch (FileNotFoundException)
        {
            return [];
        }
        catch (DirectoryNotFoundException)
        {
            return [];
        }
        catch (IOException problem)
        {
            record(NotUsed(file, problem.Message));

            return [];
        }
        catch (UnauthorizedAccessException problem)
        {
            record(NotUsed(file, problem.Message));

            return [];
        }

        Dictionary<string, string>? chosen;

        try
        {
            chosen = JsonSerializer.Deserialize<Dictionary<string, string>>(written);
        }
        catch (JsonException problem)
        {
            record(NotUsed(file, problem.Message));

            return [];
        }

        if (chosen is null)
        {
            record(NotUsed(file, "it holds nothing that could be read as settings"));

            return [];
        }

        // Every unknown key, not the first one. A Commander who mistyped two
        // should be told about two rather than sent round again.
        string[] strangers = [.. chosen.Keys.Where(key => !known.Contains(key))];

        if (strangers.Length > 0)
        {
            record(NotUsed(
                file,
                $"it holds settings we do not know: {string.Join(", ", strangers)}"));

            return [];
        }

        return chosen;
    }

    private static string NotUsed(string file, string why) =>
        $"{file} was not applied and Directive 47 is running on its defaults, "
        + $"because {why}.";
}
