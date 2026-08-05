using System;
using System.Collections.Generic;

using D47.Data;

namespace D47.Tier1.Tests;

/// <summary>
/// A store in a folder of its own, and everything it wrote down while it was
/// there.
///
/// <para>
/// At the project's root for the same reason as
/// <see cref="TemporaryFolder"/>: the panel's tests use it as much as the
/// store's own do, and the folders here name production projects.
/// </para>
/// </summary>
internal sealed class TemporaryStore : IDisposable
{
    private readonly TemporaryFolder _folder = new();
    private readonly List<string> _recorded = [];

    /// <summary>
    /// Where this store's file is, whether or not anything has written one yet.
    /// </summary>
    internal string File => _folder.File("remembered.json");

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
    internal DataStore Open() => DataStore.OpenAt(File, _recorded.Add);

    /// <summary>
    /// Where the settings file is: the same folder, the way the two sit on a
    /// real machine.
    /// </summary>
    internal string SettingsFile => _folder.File("settings.json");

    /// <summary>
    /// Opens the settings, against the application's own schema.
    ///
    /// <para>
    /// The real list of keys rather than one invented per test. A test that
    /// declares its own schema keeps passing after somebody adds a setting and
    /// forgets to register it.
    /// </para>
    /// </summary>
    /// <returns>The settings store.</returns>
    internal SettingsStore Settings() =>
        SettingsStore.OpenAt(SettingsFile, D47.Panel.Settings.Known, _recorded.Add);

    /// <summary>
    /// Puts something in the store's place that is not a store.
    /// </summary>
    /// <param name="content">Whatever the file should hold instead.</param>
    internal void Corrupt(string content) => System.IO.File.WriteAllText(File, content);

    /// <summary>
    /// Puts a settings file in place, whatever it says.
    /// </summary>
    /// <param name="content">Whatever the file should hold.</param>
    internal void WriteSettings(string content) =>
        System.IO.File.WriteAllText(SettingsFile, content);

    /// <summary>
    /// Every file in the folder, so a test can say what was left behind as well
    /// as what was written.
    /// </summary>
    internal IReadOnlyList<string> Files() => _folder.Files();

    public void Dispose() => _folder.Dispose();
}
