using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace D47.Journal;

/// <summary>
/// Follows the journal Elite is currently writing: picks the newest one in the
/// directory, and hands back what has been appended to it since the last time
/// it was asked.
///
/// <para>
/// It reads the files the game already writes. There is no memory reading and
/// no API — Elite's journal is the supported way to know what a Commander is
/// doing, and it costs nothing but a file handle.
/// </para>
///
/// <para>
/// What was in the file at the moment it opened is a different thing from what
/// has arrived since, so the two are separate members rather than one stream
/// with a marker in it. A session's backlog is history to be caught up on; an
/// appended line is something that just happened.
/// </para>
///
/// <para>
/// Reading is pulled rather than pushed, and nothing in here starts a thread or
/// a timer. That keeps the class free of the one thing a file watcher is
/// usually impossible to test around, and leaves whoever composes it into the
/// application to decide how often to ask.
/// </para>
/// </summary>
public sealed class JournalWatcher
{
    /// <summary>
    /// What Elite names a journal: <c>Journal.2026-08-02T171717.01.log</c> —
    /// the session's start time, then a part number for when a long session is
    /// continued into a second file.
    ///
    /// <para>
    /// The pattern is not decoration. The same directory holds
    /// <c>Status.json</c>, <c>Cargo.json</c>, <c>NavRoute.json</c> and several
    /// more besides, so "the newest file in the directory" taken literally
    /// picks one of those almost every time.
    /// </para>
    /// </summary>
    private const string JournalFilePattern = "Journal.*.log";

    private long _offset;

    /// <summary>
    /// Opens the newest journal in the given directory and reads what is
    /// already in it.
    /// </summary>
    /// <param name="directory">The directory Elite writes its journals to,
    /// normally <c>%USERPROFILE%\Saved Games\Frontier Developments\Elite
    /// Dangerous</c>.</param>
    /// <exception cref="ArgumentException">No directory was named.</exception>
    /// <exception cref="DirectoryNotFoundException">The directory is not there.</exception>
    /// <exception cref="FileNotFoundException">The directory holds no journal.</exception>
    public JournalWatcher(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        FilePath = NewestJournalIn(directory);

        // The same read every later one is: everything from the offset to the
        // end of the file. The offset is zero here, so this is the file as it
        // stood, and every read after it starts where this one stopped.
        LinesAlreadyPresent = ReadAppendedLines();
    }

    /// <summary>
    /// The journal being followed — the newest one the directory held when the
    /// watcher opened it.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// The lines the journal already held at that moment, oldest first. A
    /// session that was underway before Directive 47 started is all in here.
    /// </summary>
    public IReadOnlyList<string> LinesAlreadyPresent { get; }

    /// <summary>
    /// Everything appended since the previous read, oldest first, and nothing
    /// twice. Empty when the Commander has done nothing since.
    /// </summary>
    /// <returns>The new lines, with their terminators stripped.</returns>
    public IReadOnlyList<string> ReadAppendedLines()
    {
        // FileShare.ReadWrite because Elite holds its journal open for writing
        // for the whole session, and Delete because it is free to move the file
        // out from under a reader. Anything stricter opens every fixture
        // happily and fails against the only case that matters.
        using var journal = new FileStream(
            FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

        journal.Seek(_offset, SeekOrigin.Begin);

        // CopyTo rather than reading Length minus the offset, because a stream
        // seeked past its own end copies nothing instead of asking for a
        // negative number of bytes.
        using var appended = new MemoryStream();
        journal.CopyTo(appended);

        // Where the stream stopped, rather than the offset plus how much came
        // back. One of those two can drift from the file and the other cannot.
        _offset = journal.Position;

        return Lines(Encoding.UTF8.GetString(appended.ToArray()));
    }

    /// <summary>
    /// Ordinal by file name, not by last-write time. The name carries the
    /// session's start time and part number in fixed-width fields, so Elite's
    /// own ordering is already in it and sorting the names reproduces it.
    ///
    /// <para>
    /// Last-write time does not survive being moved around: a corpus of
    /// journals checked into <c>tests/</c> arrives with whatever timestamps the
    /// checkout gave it, and any third-party tool that opens an old journal can
    /// move that file's.
    /// </para>
    /// </summary>
    private static string NewestJournalIn(string directory)
    {
        string[] journals = Directory.GetFiles(directory, JournalFilePattern);

        string? newest = journals
            .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal)
            .LastOrDefault();

        // A watcher that quietly followed nothing would look exactly like a
        // Commander sitting still, which is a different answer and an untrue
        // one.
        return newest ?? throw new FileNotFoundException(
            $"No Elite journal matching '{JournalFilePattern}' in '{directory}'.");
    }

    /// <summary>
    /// Elite terminates every line with CRLF, including the last one, so text
    /// that ends at a terminator splits to a trailing empty piece that is not a
    /// line.
    ///
    /// <para>
    /// Text that ends anywhere else is a line the game has not finished writing
    /// yet, and it comes back here as though it were whole. That is what
    /// <c>JournalWatcher_OnAPartialLineWrite_WaitsRatherThanParsingGarbage</c>
    /// exists to fix, and it is deliberately not fixed here.
    /// </para>
    /// </summary>
    private static string[] Lines(string text)
    {
        string[] pieces = text.Split('\n');
        int count = pieces[^1].Length == 0 ? pieces.Length - 1 : pieces.Length;

        return [.. pieces.Take(count).Select(piece => piece.TrimEnd('\r'))];
    }
}
