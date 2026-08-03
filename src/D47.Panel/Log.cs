using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;

namespace D47.Panel;

/// <summary>
/// Somewhere for a swallowed failure to leave a mark.
///
/// <para>
/// Deliberately small, and deliberately not a logging framework. It exists
/// because carrying on without something — an overlay this machine cannot draw,
/// a hotkey another application already owns — is only acceptable if the
/// carrying-on is recorded somewhere. Absence that leaves no trace is
/// indistinguishable from a bug.
/// </para>
///
/// <para>
/// It is not the live log. That one belongs to
/// [#72](https://github.com/retiring-studios/directive-47/issues/72), is about
/// prompts and replies inside a turn, and arrives with the voice loop in Wave 2
/// — which is no use to a startup that failed before any turn existed. What
/// replaces this is a real application log, filed separately.
/// </para>
/// </summary>
internal static class Log
{
    /// <summary>
    /// Where the log goes. The path it used to work out for itself was chosen
    /// because a single line had to go somewhere; the folder is now a decision,
    /// made once and shared with the store.
    /// </summary>
    private static readonly string File = ApplicationData.File("directive-47.log");

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
    internal static void Warning(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(File)!);

            System.IO.File.AppendAllText(
                File,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}  {message}{Environment.NewLine}"));
        }
        catch (Exception)
        {
            // Nothing to do about it and nowhere to say so. This is the one
            // place in the application where swallowing is the whole point.
        }
    }
}
