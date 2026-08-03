using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace D47.Panel.Tests;

/// <summary>
/// A folder of its own, for a test about something that writes files.
///
/// <para>
/// One per instance rather than one shared, because the subject in both cases is
/// a file that outlives the object that wrote it — two tests sharing a path
/// would be two runs of the application sharing a folder, which is the thing
/// under test rather than the setup for it.
/// </para>
///
/// <para>
/// Deliberately not under <c>%LOCALAPPDATA%</c>, where the real ones live. These
/// run on the maintainer's machine on every <c>dotnet test</c>, and a test that
/// writes to the folder the application uses is a test that can eat his log.
/// </para>
/// </summary>
internal sealed class TemporaryFolder : IDisposable
{
    internal TemporaryFolder()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "d47-tests",
            Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

        Directory.CreateDirectory(Path);
    }

    /// <summary>
    /// The folder itself.
    /// </summary>
    internal string Path { get; }

    /// <summary>
    /// A path to a file in it, whether or not anything has written one.
    /// </summary>
    /// <param name="name">The file's name.</param>
    /// <returns>The full path.</returns>
    internal string File(string name) => System.IO.Path.Combine(Path, name);

    /// <summary>
    /// What a file in it holds.
    /// </summary>
    /// <param name="name">The file's name.</param>
    /// <returns>Its contents.</returns>
    /// <exception cref="FileNotFoundException">Nothing wrote it.</exception>
    internal string Read(string name) => System.IO.File.ReadAllText(File(name));

    /// <summary>
    /// Every file in it, sorted, so a test can say what was left behind as well
    /// as what was written.
    /// </summary>
    internal IReadOnlyList<string> Files()
    {
        string[] found = Directory.GetFiles(Path);
        var names = new List<string>(found.Length);

        foreach (string path in found)
        {
            names.Add(System.IO.Path.GetFileName(path));
        }

        names.Sort(StringComparer.Ordinal);
        return names;
    }

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
