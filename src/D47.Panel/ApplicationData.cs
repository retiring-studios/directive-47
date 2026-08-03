using System;
using System.IO;

namespace D47.Panel;

/// <summary>
/// Where Directive 47 keeps what it writes down.
///
/// <para>
/// One answer, in one place, because two things need it and they must not
/// disagree: the log ([#104](https://github.com/retiring-studios/directive-47/issues/104))
/// and the store ([#117](https://github.com/retiring-studios/directive-47/issues/117)).
/// The log picked a path first, for a single line, and that choice was a
/// throwaway rather than a decision. This is the decision, and it happens to
/// land in the same place.
/// </para>
///
/// <para>
/// <c>%LOCALAPPDATA%</c> rather than the roaming profile. Everything Directive
/// 47 remembers is machine-shaped — where an overlay sits, how large it was
/// left — and a position on a 3840×2160 monitor is wrong on a laptop. Roaming
/// would carry those to machines they do not fit, and take the log with them.
/// </para>
///
/// <para>
/// And not beside the executable, which would have made the whole product
/// portable and is genuinely attractive for a single-file self-contained exe.
/// The cost is that install and update
/// ([#19](https://github.com/retiring-studios/directive-47/issues/19)) would
/// then have to promise a writable install location forever — anything under
/// Program Files breaks it, and it breaks silently.
/// </para>
/// </summary>
internal static class ApplicationData
{
    /// <summary>
    /// The folder itself. Not created here: the things that write decide when
    /// there is something worth creating it for, and a folder made at startup
    /// for a file nobody ever writes is litter on every machine that runs this.
    /// </summary>
    internal static string Folder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Directive 47");

    /// <summary>
    /// A file in it, named.
    /// </summary>
    /// <param name="name">The file's name, with its extension.</param>
    /// <returns>The full path.</returns>
    internal static string File(string name) => Path.Combine(Folder, name);
}
