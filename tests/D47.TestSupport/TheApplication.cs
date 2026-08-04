using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows.Automation;

namespace D47.TestSupport;

/// <summary>
/// Directive 47 as a process on this machine: where it is, starting it, waiting
/// for one of its windows, and ending it.
///
/// <para>
/// Deliberately only that much. Two test projects drive the running application
/// and they want different things from it — one takes automation elements and
/// moves the window about, the other takes raw handles for two windows and reads
/// text out of them. Those differences are what each project is for, so they
/// stay where they are. What is here is the part that was written twice and is
/// the same both times, and is the part where a subtle mistake does not announce
/// itself.
/// </para>
///
/// <para>
/// Naming a harness that later test projects conform to is a general conclusion,
/// which <c>docs/decisions.md</c> puts with the maintainer. He chose the plumbing
/// and not the harness, so nothing here decides how a test talks to a window.
/// </para>
/// </summary>
public static class TheApplication
{
    /// <summary>
    /// What the application is called on disk: the process to look for, and —
    /// with an extension — the file to start. One spelling, so that renaming it
    /// cannot leave a caller half-right.
    /// </summary>
    public const string Name = "D47.Panel";

    /// <summary>
    /// How often to look again while waiting for a window. Short enough not to
    /// dominate a test's running time, long enough not to hammer the automation
    /// tree.
    /// </summary>
    private static readonly TimeSpan BetweenLooks = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// How long to give a killed process to actually go.
    /// </summary>
    private static readonly TimeSpan LongEnoughToDie = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Where the application under test is, checked before anything tries to
    /// start it.
    /// </summary>
    ///
    /// <remarks>
    /// Separate from starting it because a missing executable reported as "it did
    /// not start" sends the reader looking at the application. It is also asked
    /// on its own by tests that never launch anything — the icon tests read the
    /// resources out of the file.
    /// </remarks>
    /// <returns>The full path to the application.</returns>
    /// <exception cref="InvalidOperationException">
    /// It was not built, or was not copied beside the tests.
    /// </exception>
    public static string Executable()
    {
        string exe = Path.Combine(AppContext.BaseDirectory, Name + ".exe");

        if (!File.Exists(exe))
        {
            throw new InvalidOperationException(
                $"Directive 47 should sit beside these tests, at {exe}.");
        }

        return exe;
    }

    /// <summary>
    /// Starts the application.
    /// </summary>
    ///
    /// <remarks>
    /// <c>UseShellExecute = false</c> so the process is a child of the test run
    /// and can be killed with its whole tree. Nothing is waited for here; what
    /// counts as started differs by caller.
    /// </remarks>
    /// <returns>The process, which the caller owns and must end.</returns>
    /// <exception cref="InvalidOperationException">
    /// It is not beside the tests, or the operating system started nothing.
    /// </exception>
    public static Process Start()
    {
        var started = Process.Start(
            new ProcessStartInfo(Executable()) { UseShellExecute = false });

        return started ?? throw new InvalidOperationException("Directive 47 did not start.");
    }

    /// <summary>
    /// Waits for one of the application's windows to reach the desktop.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// By name <em>and</em> by process id. Matching on name alone was a latent
    /// bug in both harnesses until two test classes ran at once — a test could be
    /// handed the other one's window, close it, and then wait for its own process
    /// to exit forever. It would also match the Commander's own copy.
    /// </para>
    /// <para>
    /// A process that exits while being waited for is reported as that, with its
    /// exit code, rather than as a timeout. The two failures read identically
    /// from the outside and have nothing else in common.
    /// </para>
    /// </remarks>
    /// <param name="process">The application, already started.</param>
    /// <param name="name">What the window is called.</param>
    /// <param name="within">How long to wait.</param>
    /// <param name="alsoSay">
    /// Extra diagnosis to append if it never appears — the state of the desktop,
    /// say. Called only on failure, because reading it costs something.
    /// </param>
    /// <returns>The window, once automation can see it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// There is no desktop, the process exited first, or the window never came.
    /// </exception>
    public static AutomationElement WaitForWindow(
        Process process,
        string name,
        TimeSpan within,
        Func<string>? alsoSay = null)
    {
        ArgumentNullException.ThrowIfNull(process);

        AutomationElement root = AutomationElement.RootElement
            ?? throw new InvalidOperationException(
                "UI Automation has no root element. This machine has no interactive desktop.");

        var condition = new AndCondition(
            new PropertyCondition(AutomationElement.NameProperty, name),
            new PropertyCondition(AutomationElement.ProcessIdProperty, process.Id));

        DateTime deadline = DateTime.UtcNow + within;

        while (DateTime.UtcNow < deadline)
        {
            if (root.FindFirst(TreeScope.Children, condition) is { } window)
            {
                return window;
            }

            process.Refresh();

            if (process.HasExited)
            {
                throw new InvalidOperationException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Directive 47 exited with code {process.ExitCode} before presenting a "
                        + $"window named \"{name}\"."));
            }

            Thread.Sleep(BetweenLooks);
        }

        string said = string.Create(
            CultureInfo.InvariantCulture,
            $"No window named \"{name}\" appeared within {within.TotalSeconds:0} seconds. "
            + $"The process was {(process.HasExited ? "gone" : "still running")}.");

        throw new InvalidOperationException(
            alsoSay is null ? said : said + Environment.NewLine + alsoSay());
    }

    /// <summary>
    /// Ends the application, with its whole process tree.
    /// </summary>
    ///
    /// <remarks>
    /// Safe to call when it has already gone, because a failing test disposes on
    /// its way out and the reason it failed may well be that the application
    /// died.
    /// </remarks>
    /// <param name="process">The application to end.</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is null.</exception>
    public static void Kill(Process process)
    {
        ArgumentNullException.ThrowIfNull(process);

        process.Refresh();

        if (process.HasExited)
        {
            return;
        }

        process.Kill(entireProcessTree: true);
        process.WaitForExit((int)LongEnoughToDie.TotalMilliseconds);
    }
}
