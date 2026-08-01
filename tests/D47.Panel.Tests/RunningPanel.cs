using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Automation;

namespace D47.Panel.Tests;

/// <summary>
/// The panel, actually running, driven from outside the way anything else on
/// the machine would drive it. Every other test in this project builds a visual
/// tree in-process and inspects what it built; this reaches a real window
/// through UI Automation and reads what the application presents.
///
/// <para>
/// The distinction is not academic. The first thing this found was a window
/// announcing the <c>ToString()</c> of a model object as its accessible name,
/// while four in-process tests confirmed the right text was on screen.
/// </para>
///
/// <para>
/// Disposable, and the disposal is the point: a test that fails an assertion
/// must not leave a window on the desktop or a process on the build agent.
/// </para>
/// </summary>
internal sealed class RunningPanel : IDisposable
{
    private const string WindowName = "Directive 47";
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(20);

    /// <summary>
    /// How far down to walk when describing the tree. Deep enough to reach the
    /// text inside a templated list, shallow enough that a failure message stays
    /// readable — the tree below this is rarely what went wrong.
    /// </summary>
    private const int DeepEnoughToDiagnose = 6;

    private readonly Process _process;
    private bool _disposed;

    private RunningPanel(Process process, AutomationElement window)
    {
        _process = process;
        Window = window;
    }

    /// <summary>
    /// The panel's top-level window, once automation can see it.
    /// </summary>
    internal AutomationElement Window { get; }

    /// <summary>
    /// The operating system's id for the running panel, so a test can verify
    /// teardown against the process table rather than against this class's own
    /// bookkeeping. A harness that reports its own success is not evidence.
    /// </summary>
    internal int ProcessId { get; private set; }

    /// <summary>
    /// Starts the panel and waits until its window is reachable.
    /// </summary>
    /// <returns>The running panel, which the caller owns and must dispose.</returns>
    /// <exception cref="InvalidOperationException">
    /// The panel did not start, or started but never presented a window that
    /// automation could find. The message says which.
    /// </exception>
    internal static RunningPanel Launch()
    {
        string exe = Path.Combine(AppContext.BaseDirectory, "D47.Panel.exe");

        if (!File.Exists(exe))
        {
            throw new InvalidOperationException(
                $"The panel executable should sit beside the tests, at {exe}.");
        }

        Process process = Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false })
            ?? throw new InvalidOperationException("The panel did not start.");

        try
        {
            AutomationElement window = WaitForWindow(process);
            return new RunningPanel(process, window) { ProcessId = process.Id };
        }
        catch
        {
            Kill(process);
            throw;
        }
    }

    /// <summary>
    /// Every line of text the window presents, in tree order — what a screen
    /// reader would read out, rather than what the visual tree was built from.
    /// </summary>
    internal IReadOnlyList<string> VisibleText()
    {
        List<string> lines = [];

        AutomationElementCollection texts = Window.FindAll(
            TreeScope.Descendants,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text));

        for (int index = 0; index < texts.Count; index++)
        {
            string name = texts[index].Current.Name;

            if (!string.IsNullOrWhiteSpace(name))
            {
                lines.Add(name);
            }
        }

        return lines;
    }

    /// <summary>
    /// Closes the window the way the operating system would, rather than by
    /// killing the process. What the application does in response is the thing
    /// under test.
    /// </summary>
    internal void CloseWindow()
    {
        if (Window.TryGetCurrentPattern(WindowPattern.Pattern, out object pattern))
        {
            ((WindowPattern)pattern).Close();
            return;
        }

        throw new InvalidOperationException(
            $"The window does not support closing.{Environment.NewLine}{Describe()}");
    }

    /// <summary>
    /// Takes the window off the screen, as far as the application can currently
    /// be taken off the screen.
    ///
    /// <para>
    /// Minimized, not hidden, and the difference matters. Hiding to the tray is
    /// <see href="https://github.com/retiring-studios/directive-47/issues/68">#68</see>,
    /// and until it lands this is the strongest form of "no visual surface" the
    /// panel has. When it lands, the caller changes and this stays.
    /// </para>
    /// </summary>
    internal void MinimizeWindow()
    {
        if (Window.TryGetCurrentPattern(WindowPattern.Pattern, out object pattern))
        {
            ((WindowPattern)pattern).SetWindowVisualState(WindowVisualState.Minimized);
            return;
        }

        throw new InvalidOperationException(
            $"The window does not support minimizing.{Environment.NewLine}{Describe()}");
    }

    /// <summary>
    /// Waits for the application to exit, so a test can assert on what closing
    /// the window actually did.
    /// </summary>
    /// <param name="within">How long to wait.</param>
    /// <returns>Whether it exited in time.</returns>
    internal bool WaitForExit(TimeSpan within) => _process.WaitForExit((int)within.TotalMilliseconds);

    /// <summary>
    /// The automation tree as text. Every failure message in this project's
    /// automation tests carries one, because "element not found" without the
    /// tree tells you nothing about why.
    /// </summary>
    internal string Describe()
    {
        var description = new StringBuilder();
        description.AppendLine(CultureInfo.InvariantCulture, $"Automation tree under \"{WindowName}\":");
        Walk(Window, description, depth: 1);
        return description.ToString();
    }

    /// <summary>
    /// Ends the application. Safe to call when it has already exited, because a
    /// failing test disposes on its way out and the reason it failed may well be
    /// that the app died.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Kill(_process);
        _process.Dispose();
    }

    private static AutomationElement WaitForWindow(Process process)
    {
        AutomationElement? root = AutomationElement.RootElement
            ?? throw new InvalidOperationException(
                "UI Automation has no root element. This machine has no interactive desktop.");

        // By name *and* by process. The name alone was enough while one test
        // class launched panels one at a time; the moment a second class did it
        // too, a test could be handed the other one's window, close it, and
        // then wait for its own process to exit forever.
        var condition = new AndCondition(
            new PropertyCondition(AutomationElement.NameProperty, WindowName),
            new PropertyCondition(AutomationElement.ProcessIdProperty, process.Id));

        DateTime deadline = DateTime.UtcNow + Patience;

        while (DateTime.UtcNow < deadline)
        {
            AutomationElement? window = root.FindFirst(TreeScope.Children, condition);

            if (window is not null)
            {
                return window;
            }

            process.Refresh();

            if (process.HasExited)
            {
                throw new InvalidOperationException(
                    $"The panel exited with code {process.ExitCode} before presenting a window.");
            }

            Thread.Sleep(250);
        }

        throw new InvalidOperationException(
            $"No window named \"{WindowName}\" appeared within {Patience.TotalSeconds:0} seconds. "
            + $"The process was {(process.HasExited ? "gone" : "still running")}.");
    }

    private static void Walk(AutomationElement element, StringBuilder into, int depth)
    {
        if (depth > DeepEnoughToDiagnose)
        {
            return;
        }

        AutomationElementCollection children = element.FindAll(
            TreeScope.Children, Condition.TrueCondition);

        for (int index = 0; index < children.Count; index++)
        {
            AutomationElement child = children[index];

            into.AppendLine(
                CultureInfo.InvariantCulture,
                $"{new string(' ', depth * 2)}{child.Current.ControlType.ProgrammaticName} "
                + $"\"{child.Current.Name}\"");

            Walk(child, into, depth + 1);
        }
    }

    private static void Kill(Process process)
    {
        process.Refresh();

        if (process.HasExited)
        {
            return;
        }

        process.Kill(entireProcessTree: true);
        process.WaitForExit(5000);
    }
}
