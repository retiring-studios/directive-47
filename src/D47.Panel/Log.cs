using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Threading;

using D47.Data;

namespace D47.Panel;

/// <summary>
/// Somewhere for a swallowed failure to leave a mark.
///
/// <para>
/// It exists because carrying on without something — an overlay this machine
/// cannot draw, a hotkey another application already owns, a store that will not
/// open — is only acceptable if the carrying-on is recorded. An absence that
/// leaves no trace is indistinguishable from a defect, and the Commander whose
/// key does nothing has no other way to find out why. See
/// [#104](https://github.com/retiring-studios/directive-47/issues/104).
/// </para>
///
/// <para>
/// Hand-rolled rather than a logging framework, and the argument is the one that
/// kept <c>NearestName</c> hand-rolled over two perfectly good MIT packages: a
/// single-file exe redistributes everything inside it, and this is a lock, an
/// append and a size check. <c>Microsoft.Extensions.Logging</c> has no file
/// provider in the box, so it would have bought the interface and left the
/// writing here anyway. Revisit trigger: the first thing that needs structured
/// events, a second sink, or levels this does not have.
/// </para>
///
/// <para>
/// It is not the live log. That one belongs to
/// [#72](https://github.com/retiring-studios/directive-47/issues/72), is about
/// prompts and replies inside a turn, and arrives with the voice loop in Wave 2
/// — which is no use to a startup that failed before any turn existed.
/// </para>
/// </summary>
internal sealed class Log
{
    private const string FileName = "directive-47.log";

    /// <summary>
    /// How large the log is allowed to get before the current one becomes the
    /// previous one.
    ///
    /// <para>
    /// A megabyte is thousands of lines, which is far more than any session
    /// produces today and enough to still be useful when a session produces
    /// many. Two of them is two megabytes, on a machine that is already hosting
    /// a 165 MB executable.
    /// </para>
    /// </summary>
    private const long BiggerThanUseful = 1024 * 1024;

    private readonly string _file;
    private readonly string _previous;
    private readonly Lock _oneAtATime = new();

    private Log(string file)
    {
        _file = file;

        // "directive-47.log" becomes "directive-47.previous.log", so the older
        // one is still a .log and still opens in whatever the Commander reads
        // text with. Appending ".previous" to the whole name would have made it
        // something Windows has no opinion about.
        _previous = Path.ChangeExtension(file, ".previous.log");
    }

    /// <summary>
    /// The log the application writes to.
    /// </summary>
    /// <returns>The log, in the folder the store also uses.</returns>
    internal static Log InApplicationData() => At(ApplicationData.File(FileName));

    /// <summary>
    /// A log at a given path, for a test that must not write to the Commander's
    /// own.
    /// </summary>
    /// <param name="file">Where to write.</param>
    /// <returns>The log.</returns>
    internal static Log At(string file) => new(file);

    /// <summary>
    /// Where this log is kept. Part of the decision about where application data
    /// lives rather than an implementation detail, and the first thing anyone
    /// looking for it needs.
    /// </summary>
    internal string Location => _file;

    /// <summary>
    /// Records that something did not work and the application went on anyway.
    /// </summary>
    /// <param name="message">What happened, in the words somebody reading it needs.</param>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification =
            "A logger that takes the application down is worse than one that loses a line, "
            + "and this one is called from startup paths whose whole purpose is to survive. "
            + "The disk being full, the folder being read-only, and a virus scanner holding "
            + "the file are all outside our control and all indistinguishable here.")]
    internal void Warning(string message)
    {
        try
        {
            // One writer at a time. There is one process since
            // [#107](https://github.com/retiring-studios/directive-47/issues/107),
            // but a startup that composes several things records from whichever
            // thread each of them is on, and two appends racing is how a line
            // gets lost or interleaved with another.
            lock (_oneAtATime)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_file)!);

                RollIfItHasOutgrownItself();

                File.AppendAllText(_file, Line(message));
            }
        }
        catch (Exception)
        {
            // Nothing to do about it and nowhere to say so. This is the one
            // place in the application where swallowing is the whole point —
            // everywhere else, this class is what stops it being one.
        }
    }

    /// <summary>
    /// Makes the current log the previous one once it has grown past its cap,
    /// so that disk use is bounded however long Directive 47 runs.
    /// </summary>
    ///
    /// <remarks>
    /// Two files and no more. A numbered history is a folder that grows without
    /// limit unless something prunes it, which is the same problem moved rather
    /// than solved — and the recent past is what a bug report wants
    /// ([#20](https://github.com/retiring-studios/directive-47/issues/20)), not
    /// the distant one.
    /// </remarks>
    private void RollIfItHasOutgrownItself()
    {
        var current = new FileInfo(_file);

        if (!current.Exists || current.Length < BiggerThanUseful)
        {
            return;
        }

        // Overwriting whatever was there. The one before last is older than the
        // thing being kept, and keeping it would be the numbered history this
        // deliberately does not have.
        File.Move(_file, _previous, overwrite: true);
    }

    private static string Line(string message) => string.Create(
        CultureInfo.InvariantCulture,
        $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}  {message}{Environment.NewLine}");
}
