using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace D47.Panel.Tests;

/// <summary>
/// A store in a folder of its own, and everything it wrote down while it was
/// there.
///
/// <para>
/// A folder per instance rather than one shared between tests, because the whole
/// subject here is a file that outlives the object that wrote it. Two tests
/// sharing a path would be two runs of the application sharing a store, which is
/// the thing under test rather than the setup for it.
/// </para>
///
/// <para>
/// Deliberately not under <c>%LOCALAPPDATA%</c>, where the real one lives. These
/// tests run on the maintainer's machine on every <c>dotnet test</c>, and a test
/// that writes to the folder the application uses is a test that can lose his
/// overlay's position.
/// </para>
/// </summary>
internal sealed class TemporaryStore : IDisposable
{
    private readonly string _folder;
    private readonly List<string> _recorded = [];

    internal TemporaryStore()
    {
        _folder = Path.Combine(
            Path.GetTempPath(),
            "d47-store-tests",
            Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

        Directory.CreateDirectory(_folder);
    }

    /// <summary>
    /// Where this store's file is, whether or not anything has written one yet.
    /// </summary>
    internal string File => Path.Combine(_folder, "remembered.json");

    /// <summary>
    /// Everything the store asked to have written down, in order. A store that
    /// carries on from something unexpected is supposed to leave a mark, and
    /// this is where the mark lands instead of in a real log file.
    /// </summary>
    internal IReadOnlyList<string> Recorded => _recorded;

    /// <summary>
    /// Opens the store, the way a run of the application would. Called more than
    /// once on purpose: a second open is the next run.
    /// </summary>
    internal Store Open() => Store.OpenAt(File, _recorded.Add);

    /// <summary>
    /// Puts something in the store's place that is not a store.
    /// </summary>
    /// <param name="content">Whatever the file should hold instead.</param>
    internal void Corrupt(string content) => System.IO.File.WriteAllText(File, content);

    /// <summary>
    /// Every file in the folder, so a test can say what was left behind as well
    /// as what was written.
    /// </summary>
    internal IReadOnlyList<string> Files()
    {
        string[] found = Directory.GetFiles(_folder);
        var names = new List<string>(found.Length);

        foreach (string path in found)
        {
            names.Add(Path.GetFileName(path));
        }

        names.Sort(StringComparer.Ordinal);
        return names;
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);
}
