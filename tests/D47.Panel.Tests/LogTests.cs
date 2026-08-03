using System.IO;
using System.Threading.Tasks;

using Shouldly;

using Xunit;

namespace D47.Panel.Tests;

/// <summary>
/// The application log — somewhere for a swallowed failure to leave a mark.
///
/// <para>
/// Untested until now, which was the sharpest thing in
/// [#104](https://github.com/retiring-studios/directive-47/issues/104): the line
/// it writes is the guard against forgetting, and nothing asserted that it got
/// written. Every fact here runs in CI with no desktop, in a folder of its own.
/// </para>
/// </summary>
public class LogTests
{
    [Fact]
    public void Log_WhenSomethingIsRecorded_IsThereToBeRead()
    {
        using var folder = new TemporaryFolder();

        var log = Log.At(folder.File("directive-47.log"));

        log.Warning("Could not claim Ctrl+Alt+D.");

        folder.Read("directive-47.log").ShouldContain("Could not claim Ctrl+Alt+D.");
    }

    [Fact]
    public void Log_WhenTheFolderIsNotThereYet_MakesIt()
    {
        using var folder = new TemporaryFolder();

        // The real one lives under a folder nothing creates at startup, because
        // a folder made for a file nobody writes is litter on every machine.
        var log = Log.At(Path.Combine(folder.Path, "not", "made", "yet", "directive-47.log"));

        Should.NotThrow(() => log.Warning("something"));

        File.Exists(Path.Combine(folder.Path, "not", "made", "yet", "directive-47.log"))
            .ShouldBeTrue();
    }

    [Fact]
    public void Log_WhenItOutgrowsItsCap_KeepsTheOldOneBeside()
    {
        using var folder = new TemporaryFolder();

        var log = Log.At(folder.File("directive-47.log"));

        Fill(log, "the older thing");
        log.Warning("the newer thing");

        folder.Read("directive-47.previous.log").ShouldContain(
            "the older thing",
            customMessage: "what was written before the roll is the recent past, not litter");

        folder.Read("directive-47.log").ShouldContain(
            "the newer thing",
            customMessage: "and the current file is the one still being written to");
    }

    [Fact]
    public void Log_HoweverLongItRuns_NeverKeepsMoreThanTwoFiles()
    {
        using var folder = new TemporaryFolder();

        var log = Log.At(folder.File("directive-47.log"));

        // Three rolls, so that anything keeping a numbered history would have
        // four files by now and this would say so.
        Fill(log, "first");
        Fill(log, "second");
        Fill(log, "third");

        folder.Files().ShouldBe(["directive-47.log", "directive-47.previous.log"]);
    }

    [Fact]
    public void Log_WhenItCannotWriteAtAll_DoesNotTakeTheApplicationDown()
    {
        using var folder = new TemporaryFolder();

        // A folder where the file should be. Every call site is a startup path
        // whose whole purpose is to survive, so a logger that throws is worse
        // than one that loses a line.
        Directory.CreateDirectory(folder.File("directive-47.log"));

        var log = Log.At(folder.File("directive-47.log"));

        Should.NotThrow(() => log.Warning("something worth knowing"));
    }

    [Fact]
    public async Task Log_WhenSeveralThingsRecordAtOnce_LosesNone()
    {
        using var folder = new TemporaryFolder();

        var log = Log.At(folder.File("directive-47.log"));

        // The reason there is a lock at all. One process now
        // ([#107](https://github.com/retiring-studios/directive-47/issues/107)),
        // but a startup that composes several things records from whichever
        // thread each of them happens to be on.
        await Task.WhenAll(Writing(log, 0), Writing(log, 1), Writing(log, 2), Writing(log, 3));

        string written = folder.Read("directive-47.log");

        for (int writer = 0; writer < 4; writer++)
        {
            for (int line = 0; line < 25; line++)
            {
                written.ShouldContain($"writer {writer} line {line}");
            }
        }
    }

    [Fact]
    public void Log_WhenItIsTheApplicationsOwn_SitsBesideTheStore()
    {
        Log.InApplicationData().Location.ShouldBe(
            ApplicationData.File("directive-47.log"),
            customMessage: "the log and the store were supposed to settle this once, together");
    }

    private static Task Writing(Log log, int writer) => Task.Run(() =>
    {
        for (int line = 0; line < 25; line++)
        {
            log.Warning($"writer {writer} line {line}");
        }
    });

    /// <summary>
    /// Writes until the log has outgrown its cap, ending with a line that says
    /// which round it was, so a test can tell one roll's contents from another's.
    /// </summary>
    private static void Fill(Log log, string what)
    {
        log.Warning(what);

        // A few enormous lines rather than thousands of ordinary ones. Each
        // call opens the file, so reaching a megabyte a kilobyte at a time makes
        // this test slow for no gain — the cap is the subject, and how it was
        // reached is not.
        string bulk = new('.', 100_000);

        for (int written = 0; written < 11; written++)
        {
            log.Warning(bulk);
        }
    }
}
