using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
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
    /// What to say on a machine that cannot keep a key. The sentence is shared
    /// even though the guard that throws it cannot be — see
    /// <see cref="Scrambled"/>.
    /// </summary>
    private const string NotWindows =
        "Keys are kept with Windows DPAPI, and this is not Windows.";

    /// <summary>
    /// Indented on purpose. This is a file somebody opens when they are already
    /// puzzled — most likely because something told them it could not be read —
    /// and one long line is a worse answer than four short ones.
    /// </summary>
    private static readonly JsonSerializerOptions Layout = new() { WriteIndented = true };

    private readonly string _file;
    private readonly Dictionary<string, string> _remembered;
    private readonly Action<string> _record;

    private DataStore(
        string file,
        Dictionary<string, string> remembered,
        Action<string> record)
    {
        _file = file;
        _remembered = remembered;
        _record = record;
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

        return new(file, ReadFrom(file, record), record);
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
    /// Writes a key down the way <see cref="Write"/> writes anything else,
    /// except that what lands in the file is unreadable to anything but this
    /// Commander on this machine.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// Windows scrambles the value against the signed-in account, so the
    /// Commander manages nothing and there is no passphrase to lose. What it
    /// defends against is anything that reads the file — another account on the
    /// machine, a stray backup, a settings file pasted into a bug report. It
    /// does not defend against malware already running as that Commander, which
    /// can ask Windows to unscramble the value exactly as this does.
    /// </para>
    /// <para>
    /// The cost, accepted: keys do not travel. On another machine or under
    /// another account they are unreadable, and the Commander types them again
    /// once.
    /// </para>
    /// </remarks>
    /// <param name="key">The name to write it down under.</param>
    /// <param name="secret">The key, in the clear.</param>
    public void WriteSecret(string key, string secret)
    {
        ArgumentNullException.ThrowIfNull(secret);

        Write(key, Scrambled(secret));
    }

    /// <summary>
    /// A key written down by <see cref="WriteSecret"/>, or
    /// <see langword="null"/> if there is not one this Commander can read.
    /// </summary>
    ///
    /// <remarks>
    /// Two absences again, told apart the same way the file itself is. A key
    /// nobody has given us is every first run and is not an event. A key that
    /// is there and will not unscramble has travelled — another machine,
    /// another account, a restored backup — and that gets written down, because
    /// caller's move is to ask for it again and a silent one leaves the
    /// Commander wondering why nothing works.
    /// </remarks>
    /// <param name="key">The name it was written down under.</param>
    /// <returns>The key in the clear, or <see langword="null"/>.</returns>
    public string? ReadSecret(string key)
    {
        if (Read(key) is not { } scrambled)
        {
            return null;
        }

        try
        {
            return Unscrambled(scrambled);
        }

        // Windows refusing to unscramble it, and the value not being base64 at
        // all. Both mean the same thing to a caller and neither is a crash: the
        // key we have is not one we can use.
        catch (Exception failed)
            when (failed is CryptographicException or FormatException)
        {
            _record(
                $"The key under {key} could not be read back: {failed.Message}");

            return null;
        }
    }

    /// <summary>
    /// The value as it goes into the file: scrambled by Windows against the
    /// signed-in account, then base64 so it is a JSON string like every other
    /// value in there.
    /// </summary>
    ///
    /// <remarks>
    /// The Windows check is written out here and again below rather than
    /// factored into one place, and that is not an oversight. CA1416 reads
    /// control flow and cannot see through a delegate, so a shared
    /// <c>OnWindows(Func&lt;byte[]&gt;)</c> helper leaves both call sites
    /// failing the build exactly as if the guard were not there — which was
    /// tried. The guard has to be lexically beside the call.
    ///
    /// <para>
    /// A guard rather than <c>[SupportedOSPlatform("windows")]</c> on the two
    /// public methods, because an attribute spreads to every caller and
    /// <c>D47.Providers</c> and <c>D47.VoiceLoop</c> are both <c>net10.0</c>.
    /// Throwing rather than falling back, because the only other thing this
    /// could do is write the key in the clear on a machine that cannot scramble
    /// it.
    /// </para>
    /// </remarks>
    /// <param name="secret">The key, in the clear.</param>
    /// <returns>What to write down.</returns>
    private static string Scrambled(string secret)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(NotWindows);
        }

        return Convert.ToBase64String(ProtectedData.Protect(
            Encoding.UTF8.GetBytes(secret),
            optionalEntropy: null,
            DataProtectionScope.CurrentUser));
    }

    /// <summary>
    /// The reverse, and the only two ways it can fail are the two
    /// <see cref="ReadSecret"/> catches.
    /// </summary>
    /// <param name="scrambled">What was written down.</param>
    /// <returns>The key, in the clear.</returns>
    private static string Unscrambled(string scrambled)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(NotWindows);
        }

        return Encoding.UTF8.GetString(ProtectedData.Unprotect(
            Convert.FromBase64String(scrambled),
            optionalEntropy: null,
            DataProtectionScope.CurrentUser));
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
