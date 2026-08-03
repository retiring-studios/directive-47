using System;
using System.IO;
using System.Linq;
using System.Text;

using Shouldly;

using Xunit;

namespace D47.Journal.Tests;

/// <summary>
/// Elite writes one journal per session into a directory it shares with a
/// handful of other files, and appends to that journal for as long as the
/// Commander plays. The watcher's whole job here is to work out which file that
/// is and to keep reporting what has been added to it.
///
/// <para>
/// Every fixture is written by the test as the bytes Elite writes — UTF-8, no
/// byte order mark, CRLF after every line — so the shape being read is stated
/// here rather than left to a checked-in file whose line endings git would
/// normalize on the way past.
/// </para>
///
/// <para>
/// Nothing here sleeps or polls. Appends are made by the test and read back on
/// the next call, so a run either passes or fails on what the code does rather
/// than on how fast it did it.
/// </para>
/// </summary>
public sealed class JournalWatcherTests : IDisposable
{
    private const string Fileheader =
        """{ "timestamp":"2026-08-02T20:00:06Z", "event":"Fileheader", "part":1, "language":"English/UK", "Odyssey":true, "gameversion":"4.4.0.3", "build":"r330683/r0 " }""";

    private const string Commander =
        """{ "timestamp":"2026-08-02T20:00:11Z", "event":"Commander", "FID":"F0000001", "Name":"Jameson" }""";

    private const string LoadGame =
        """{ "timestamp":"2026-08-02T20:00:12Z", "event":"LoadGame", "Commander":"Jameson", "Horizons":true, "Odyssey":true, "Ship":"Krait_MkII", "ShipID":7, "GameMode":"Solo", "Credits":1234567, "Loan":0 }""";

    private const string FsdJump =
        """{ "timestamp":"2026-08-02T20:01:44Z", "event":"FSDJump", "StarSystem":"Shinrarta Dezhra", "SystemAddress":3932277478106, "JumpDist":21.451, "FuelUsed":1.204, "FuelLevel":30.796 }""";

    private const string Docked =
        """{ "timestamp":"2026-08-02T20:22:17Z", "event":"Docked", "StationName":"Jameson Memorial", "StationType":"Orbis", "StarSystem":"Shinrarta Dezhra" }""";

    /// <summary>
    /// A Commander name outside ASCII, which the journal carries as UTF-8 like
    /// everything else in it. Here so that something in the corpus would come
    /// back mangled if the bytes were decoded as anything else.
    /// </summary>
    private const string ReceiveText =
        """{ "timestamp":"2026-08-02T20:14:02Z", "event":"ReceiveText", "From":"CMDR Ærndís", "Message":"o7", "Channel":"player" }""";

    private const string NonAsciiName = "Ærndís";

    /// <summary>
    /// Four journal names in the order Elite would have written them. The
    /// session start time and the part number are the whole of what separates
    /// one from the next, which is what makes these names and not labels.
    /// </summary>
    private const string PreviousSession = "Journal.2026-07-18T115746.01.log";
    private const string EarlierSameDay = "Journal.2026-08-02T160014.01.log";
    private const string CurrentSession = "Journal.2026-08-02T171717.01.log";
    private const string CurrentSessionContinued = "Journal.2026-08-02T171717.02.log";

    private readonly string _directory = Directory.CreateTempSubdirectory("d47-journal-").FullName;

    [Fact]
    public void JournalWatcher_GivenAJournalDirectory_FollowsTheNewestFile()
    {
        // Written oldest-name-last, so a watcher that took whatever the
        // directory happened to hand back first would take the wrong one.
        Write(CurrentSession, Fileheader, FsdJump);
        Write(EarlierSameDay, Fileheader);
        Write(PreviousSession, Fileheader);

        var watcher = new JournalWatcher(_directory);

        Path.GetFileName(watcher.FilePath).ShouldBe(CurrentSession);
        watcher.LinesAlreadyPresent.ShouldBe([Fileheader, FsdJump]);
    }

    [Fact]
    public void JournalWatcher_WhenAnOlderSessionWasTouchedLast_StillFollowsTheNewestSession()
    {
        // The session's start time is in the name, so that is what says which
        // journal is the current one. Last-write time does not: a corpus
        // checked into tests/ arrives with whatever timestamps the checkout
        // gave it, and any tool that opens an old journal moves its.
        string newest = Write(CurrentSession, Fileheader);
        string older = Write(PreviousSession, Fileheader);

        File.SetLastWriteTimeUtc(newest, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(older, new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc));

        var watcher = new JournalWatcher(_directory);

        Path.GetFileName(watcher.FilePath).ShouldBe(CurrentSession);
    }

    [Fact]
    public void JournalWatcher_WhenASessionContinuesIntoASecondFile_FollowsTheLaterPart()
    {
        // A long session rolls into a new part under the same start time. The
        // part number is the only thing that separates the two names.
        Write(CurrentSession, Fileheader);
        Write(CurrentSessionContinued, Fileheader, Docked);

        var watcher = new JournalWatcher(_directory);

        Path.GetFileName(watcher.FilePath).ShouldBe(CurrentSessionContinued);
    }

    [Fact]
    public void JournalWatcher_GivenTheOtherFilesEliteWritesAlongside_FollowsTheJournal()
    {
        // Every one of these sits in the real directory next to the journals,
        // and all but the lock file sort after "Journal." — so "the newest
        // file" taken literally is Status.json, every time.
        Write(CurrentSession, Fileheader);
        Write("Status.json", """{ "timestamp":"2026-08-02T20:22:44Z", "event":"Status", "Flags":16777240 }""");
        Write("Cargo.json", """{ "event":"Cargo", "Vessel":"Ship", "Count":0, "Inventory":[] }""");
        Write("NavRoute.json", """{ "event":"NavRoute", "Route":[] }""");
        Write("ShipLocker.json", """{ "event":"ShipLocker", "Items":[] }""");
        Write("Shipyard.json", """{ "event":"Shipyard", "MarketID":128666762 }""");
        Write("Outfitting.json", """{ "event":"Outfitting", "MarketID":128666762 }""");
        Write("edmc-journal-lock.txt", "Path: not a journal");

        var watcher = new JournalWatcher(_directory);

        Path.GetFileName(watcher.FilePath).ShouldBe(CurrentSession);
    }

    [Fact]
    public void JournalWatcher_WhenItOpens_DistinguishesTheLinesAlreadyThereFromWhatArrivesAfter()
    {
        string journal = Write(CurrentSession, Fileheader, Commander, LoadGame);

        var watcher = new JournalWatcher(_directory);

        watcher.LinesAlreadyPresent.ShouldBe([Fileheader, Commander, LoadGame]);
        watcher.ReadAppendedLines().ShouldBeEmpty();

        Append(journal, FsdJump);

        watcher.ReadAppendedLines().ShouldBe([FsdJump]);
        watcher.LinesAlreadyPresent.ShouldBe([Fileheader, Commander, LoadGame]);
    }

    [Fact]
    public void JournalWatcher_OnASecondRead_ReturnsOnlyWhatArrivedSinceTheFirst()
    {
        string journal = Write(CurrentSession, Fileheader);
        var watcher = new JournalWatcher(_directory);

        Append(journal, FsdJump);
        watcher.ReadAppendedLines().ShouldBe([FsdJump]);

        Append(journal, ReceiveText, Docked);
        watcher.ReadAppendedLines().ShouldBe([ReceiveText, Docked]);

        watcher.ReadAppendedLines().ShouldBeEmpty();
    }

    [Fact]
    public void JournalWatcher_OnAppendedLines_ReturnsEachLineExactlyAsEliteWroteIt()
    {
        // The terminator is not part of the line, and a name outside ASCII is
        // still the name it was written as. Both are properties of the bytes
        // Elite writes rather than of anything this test does.
        string journal = Write(CurrentSession, Fileheader);
        var watcher = new JournalWatcher(_directory);

        Append(journal, ReceiveText);

        string line = watcher.ReadAppendedLines().ShouldHaveSingleItem();
        line.ShouldBe(ReceiveText);
        line.ShouldContain(NonAsciiName);
        line.ShouldNotContain("\r");
    }

    [Fact]
    public void JournalWatcher_WhileTheGameStillHasTheFileOpen_ReadsItAnyway()
    {
        // Elite holds its journal open for writing for the whole session, which
        // is the only time any of this matters. An open that does not share
        // write access fails outright against a running game and passes against
        // every fixture, so this is the test that catches it.
        string journal = PathTo(CurrentSession);
        using var elite = new FileStream(
            journal, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        WriteTo(elite, Fileheader);

        var watcher = new JournalWatcher(_directory);

        watcher.LinesAlreadyPresent.ShouldBe([Fileheader]);

        WriteTo(elite, FsdJump);

        watcher.ReadAppendedLines().ShouldBe([FsdJump]);
    }

    [Fact]
    public void JournalWatcher_WhenTheNewestJournalIsStillEmpty_ReportsNoLinesRatherThanFailing()
    {
        Write(CurrentSession);

        var watcher = new JournalWatcher(_directory);

        watcher.LinesAlreadyPresent.ShouldBeEmpty();
        watcher.ReadAppendedLines().ShouldBeEmpty();
    }

    [Fact]
    public void JournalWatcher_GivenADirectoryWithNoJournal_FailsLoudlyRatherThanFollowingNothing()
    {
        // Following nothing quietly reads as "the Commander is not doing
        // anything", which is a different answer and an untrue one.
        Write("Status.json", """{ "event":"Status", "Flags":0 }""");

        Should.Throw<FileNotFoundException>(() => new JournalWatcher(_directory));
    }

    [Fact]
    public void JournalWatcher_GivenADirectoryThatIsNotThere_FailsLoudly()
    {
        string missing = Path.Combine(_directory, "Frontier Developments");

        Should.Throw<DirectoryNotFoundException>(() => new JournalWatcher(missing));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void JournalWatcher_GivenNoDirectoryAtAll_FailsLoudly(string directory)
    {
        Should.Throw<ArgumentException>(() => new JournalWatcher(directory));
    }

    /// <summary>
    /// The temp directory each test gets to itself, so no two tests can see
    /// each other's journals however xUnit schedules them.
    /// </summary>
    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
        GC.SuppressFinalize(this);
    }

    private string PathTo(string name) => Path.Combine(_directory, name);

    private string Write(string name, params string[] lines)
    {
        string path = PathTo(name);
        Append(path, lines);

        return path;
    }

    /// <summary>
    /// Appends lines the way Elite does: UTF-8 with no byte order mark, CRLF
    /// after every line including the last, and the file left readable and
    /// writable by anything else that has it open.
    /// </summary>
    private static void Append(string path, params string[] lines)
    {
        using var stream = new FileStream(
            path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        WriteTo(stream, lines);
    }

    private static void WriteTo(FileStream stream, params string[] lines)
    {
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        stream.Write(utf8.GetBytes(string.Concat(lines.Select(line => line + "\r\n"))));
        stream.Flush();
    }
}
