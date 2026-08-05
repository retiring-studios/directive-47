using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace D47.Data;

/// <summary>
/// Somewhere for Directive 47 to write things down and find them again next
/// time.
///
/// <para>
/// Built before there is any way to change a setting, and before anything is
/// stored in it. The overlay should stay where it was put and at the size it was
/// left, and so should the one in the headset — none of which needs a settings
/// page, because a page is a way of *surfacing* state and state can exist before
/// anything surfaces it.
/// </para>
///
/// <para>
/// Not settings. [#18](https://github.com/retiring-studios/directive-47/issues/18)
/// is one schema projected to the panel and to a voice capability so the two can
/// never disagree, and that Feature should be built *on* this rather than beside
/// it — otherwise the application ends up with two ways of remembering things
/// and a question about which one is right.
/// </para>
///
/// <para>
/// Not a database. A handful of values that survive a restart — which is also
/// why it is <c>DataStore</c> rather than <c>Store</c>: "store" is what half the
/// .NET world calls one, and this is a JSON file with a dictionary in it.
/// </para>
///
/// <para>
/// It was <c>internal</c> to <c>D47.Panel</c> until Wave 2. The panel is the
/// executable and sits at the top of the dependency graph, so nothing below it
/// could see this — and almost everything that needs to read something back is
/// below it: the voice loop, the transcriber, the speech service, both overlays.
/// </para>
/// </summary>
public sealed class DataStore
{
    /// <summary>
    /// Named for what it holds rather than for what it is. Nobody opening
    /// <c>%LOCALAPPDATA%\Directive 47\</c> to see what the application has been
    /// doing is helped by the word "store".
    /// </summary>
    private const string FileName = "remembered.json";

    /// <summary>
    /// Where a half-written store lives for the moment it exists.
    ///
    /// <para>
    /// Beside the real file rather than in the temporary folder, because the
    /// move at the end of a save is only atomic within one volume. A temporary
    /// folder on another drive would turn the one step that must not be
    /// interruptible into a copy and a delete.
    /// </para>
    /// </summary>
    private const string WhileWriting = ".writing";

    /// <summary>
    /// Indented on purpose. This is a file somebody opens when they are already
    /// puzzled — most likely because something told them it could not be read —
    /// and one long line is a worse answer than four short ones.
    /// </summary>
    private static readonly JsonSerializerOptions Layout = new() { WriteIndented = true };

    private readonly string _file;
    private readonly Dictionary<string, string> _remembered;

    private DataStore(string file, Dictionary<string, string> remembered)
    {
        _file = file;
        _remembered = remembered;
    }

    /// <summary>
    /// Opens the store the application uses.
    /// </summary>
    /// <param name="record">Where to note a store that would not open, and why.</param>
    /// <returns>The store, holding whatever the last run left in it.</returns>
    public static DataStore Open(Action<string> record) =>
        OpenAt(ApplicationData.File(FileName), record);

    /// <summary>
    /// Opens a store at a given path, for a test that must not touch the
    /// Commander's own.
    /// </summary>
    ///
    /// <remarks>
    /// Where to record is an argument rather than a default, and it stopped
    /// being one when the log became a thing the application owns rather than a
    /// static. A default would have been this class quietly deciding where its
    /// own absences go.
    /// </remarks>
    /// <param name="file">Where the store is, or will be.</param>
    /// <param name="record">Where to note a store that would not open, and why.</param>
    /// <returns>The store, holding whatever was readable.</returns>
    /// <exception cref="ArgumentNullException">
    /// There is nowhere to record to. Deciding that for the caller would mean
    /// this class choosing where its own absences go, which is exactly what
    /// taking the argument was meant to stop.
    /// </exception>
    public static DataStore OpenAt(string file, Action<string> record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new(file, ReadFrom(file, record));
    }

    /// <summary>
    /// The file this store is kept in.
    ///
    /// <para>
    /// Exposed because where it writes is part of the decision rather than an
    /// implementation detail — it is shared with the log, and a store that
    /// cannot say where it lives cannot be asserted to have honoured that. It is
    /// also the first thing anyone diagnosing one would want named.
    /// </para>
    /// </summary>
    public string Location => _file;

    /// <summary>
    /// What was written down under a name, or <see langword="null"/> if nothing
    /// was.
    /// </summary>
    /// <param name="key">The name it was written down under.</param>
    /// <returns>The value, or <see langword="null"/>.</returns>
    public string? Read(string key) =>
        _remembered.TryGetValue(key, out string? value) ? value : null;

    /// <summary>
    /// Writes something down, and saves at once.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// There is no separate save, because a save is a thing to forget. The file
    /// holds a handful of values, so rewriting all of them costs nothing worth
    /// measuring, and the alternative is a store that is correct in memory and
    /// wrong on disk for as long as nobody remembered to flush it.
    /// </para>
    /// <para>
    /// A write that cannot be completed throws, deliberately. The absent-not-
    /// failed line is drawn at reading, where the answer is genuinely "nothing
    /// was remembered"; nothing has been decided about a machine that cannot be
    /// written to, and swallowing it here would decide it silently on behalf of
    /// a caller that does not exist yet.
    /// </para>
    /// </remarks>
    /// <param name="key">The name to write it down under.</param>
    /// <param name="value">What to remember.</param>
    public void Write(string key, string value)
    {
        _remembered[key] = value;

        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);

        // Somewhere else first, then one move. Writing over the real file in
        // place means that anything interrupting the write — the machine going
        // down, the process being killed — leaves a half-written store, which is
        // precisely the unreadable file this class has to have an answer for.
        // The cheapest way to handle that case is to stop causing it.
        string half = _file + WhileWriting;

        File.WriteAllText(half, JsonSerializer.Serialize(_remembered, Layout));
        File.Move(half, _file, overwrite: true);
    }

    /// <summary>
    /// Reads the file, and starts empty rather than failing when it cannot.
    /// </summary>
    ///
    /// <remarks>
    /// Two absences, told apart on purpose. A file that is not there is a
    /// machine that has never been asked, which is every first run and is not an
    /// event. A file that is there and cannot be read is our defect or somebody's
    /// editor, and it gets written down — because an absence nobody recorded is
    /// indistinguishable from a defect, and the Commander's only evidence would
    /// otherwise be that the overlay moved.
    /// </remarks>
    private static Dictionary<string, string> ReadFrom(string file, Action<string> record)
    {
        string written;

        try
        {
            written = File.ReadAllText(file);
        }

        // The two silent ones come first because they have to. Both derive from
        // IOException, so moving either below it turns every first run into a
        // recorded fault — a change that looks like tidying and reads as one.
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
            record(CouldNotRead(file, problem.Message));
            return [];
        }
        catch (UnauthorizedAccessException problem)
        {
            record(CouldNotRead(file, problem.Message));
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(written) ?? [];
        }
        catch (JsonException problem)
        {
            record(CouldNotRead(file, problem.Message));
            return [];
        }
    }

    /// <summary>
    /// What to say about a store that would not open. Names the file, because a
    /// record nobody can act on is not much better than silence.
    /// </summary>
    private static string CouldNotRead(string file, string why) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"Could not read {file} — {why} Directive 47 has started without whatever it "
            + $"remembered, and will write over it next time something is worth remembering.");
}
