using System.IO;

namespace D47.Data;

/// <summary>
/// Writing a small file in a way that a crash cannot leave half done.
///
/// <para>
/// Somewhere else first, then one move. Writing over the real file in place
/// means anything interrupting it — the machine going down, the process being
/// killed — leaves half a file, which is precisely the unreadable file both
/// stores then have to have an answer for. The cheapest way to handle that case
/// is to stop causing it.
/// </para>
///
/// <para>
/// Shared because there are two files now and they are the same problem.
/// <see cref="DataStore"/> and <see cref="SettingsStore"/> differ over what an
/// unrecognized key means, which is why they are two classes; they do not
/// differ over how bytes reach a disk.
/// </para>
/// </summary>
internal static class DurableFile
{
    /// <summary>
    /// The suffix a half-written file wears for the moment it exists.
    ///
    /// <para>
    /// Beside the real file rather than in the temporary folder, because the
    /// move at the end is only atomic within one volume. A temporary folder on
    /// another drive would turn the one step that must not be interruptible
    /// into a copy and a delete.
    /// </para>
    /// </summary>
    private const string WhileWriting = ".writing";

    /// <summary>
    /// Puts the contents there, creating the folder if this is the first thing
    /// Directive 47 has ever written.
    /// </summary>
    /// <param name="file">Where it goes.</param>
    /// <param name="contents">What to write.</param>
    internal static void Write(string file, string contents)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);

        string half = file + WhileWriting;

        File.WriteAllText(half, contents);
        File.Move(half, file, overwrite: true);
    }
}
