using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Automation;

using D47.TestSupport;

namespace D47.Tier3.Tests.GameOverlay;

/// <summary>
/// Directive 47 itself, started as a process and driven from outside the way
/// anything else on the machine would drive it.
///
/// <para>
/// Every other test in this project builds an overlay window in-process and
/// asks Windows about it. This starts the real executable, which is a different
/// question and the only way to ask two of them: whether the composition root
/// actually wires the parts together, and whether the overlay leaves the
/// foreground alone. The second one needs a process that <em>holds</em> the
/// foreground to lose it — a test runner holds nothing, so an overlay built to
/// steal focus could not have stolen it here even if it tried.
/// </para>
///
/// <para>
/// Disposable, and the disposal is the point: a test that fails an assertion
/// must not leave a transparent, always-on-top window over somebody's game, or
/// a second copy of the application running.
/// </para>
/// </summary>
internal sealed class RunningApplication : IDisposable
{
    /// <summary>
    /// What the panel's window is called, and what the overlay's is. Both are
    /// set in XAML and both are what a screen reader would announce, so finding
    /// them this way is finding them the way anything else on the machine would.
    /// </summary>
    private const string PanelWindow = "Directive 47";

    /// <inheritdoc cref="PanelWindow"/>
    private const string OverlayWindow = "Directive 47 overlay";

    /// <summary>
    /// Long enough for a self-contained WPF application to start on a machine
    /// that is also running a game.
    /// </summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(20);

    /// <summary>
    /// How long to wait for the overlay after the panel is up. Shorter, because
    /// by then the process is running and the composition root shows the overlay
    /// on the next few lines.
    /// </summary>
    private static readonly TimeSpan LongEnoughForTheOverlay = TimeSpan.FromSeconds(10);

    private readonly Process _process;
    private bool _disposed;

    private RunningApplication(Process process)
    {
        _process = process;
    }

    /// <summary>
    /// Starts Directive 47 and waits until its panel is on the desktop.
    /// </summary>
    ///
    /// <remarks>
    /// A copy already running is checked for first and named as such. The
    /// application claims single-instance and a second copy says so and exits,
    /// so without this the failure arrives as "exited before presenting a
    /// window" — true, and about six steps upstream of the actual problem.
    /// </remarks>
    /// <returns>The running application, which the caller owns and must dispose.</returns>
    /// <exception cref="InvalidOperationException">
    /// A copy is already running, the executable is not beside the tests, or it
    /// started and never presented a window. The message says which.
    /// </exception>
    internal static RunningApplication Launch()
    {
        RefuseIfAlreadyRunning();

        var running = new RunningApplication(TheApplication.Start());

        try
        {
            running.Panel = running.WaitForWindow(PanelWindow, Patience);
            running.Overlay = running.WaitForWindow(OverlayWindow, LongEnoughForTheOverlay);

            return running;
        }
        catch
        {
            running.Dispose();
            throw;
        }
    }

    /// <summary>
    /// The panel's window, as the operating system knows it.
    /// </summary>
    internal IntPtr Panel { get; private set; }

    /// <summary>
    /// The overlay's window, as the operating system knows it.
    /// </summary>
    internal IntPtr Overlay { get; private set; }

    /// <summary>
    /// Every line of text a window presents, in tree order — what a screen
    /// reader would read out, rather than what the visual tree was built from.
    ///
    /// <para>
    /// Asked of both surfaces by the same method on purpose. "The panel and the
    /// overlay show the same render" is only worth asserting if both answers
    /// came from the same question.
    /// </para>
    /// </summary>
    /// <param name="window">Which window to read.</param>
    /// <returns>The lines it presents.</returns>
    internal static IReadOnlyList<string> TextIn(IntPtr window)
    {
        var element = AutomationElement.FromHandle(window);

        AutomationElementCollection texts = element.FindAll(
            TreeScope.Descendants,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text));

        List<string> lines = [];

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
    /// Ends the application. Safe to call when it has already gone, because a
    /// failing test disposes on its way out and the reason it failed may well be
    /// that the application died.
    /// </summary>
    ///
    /// <remarks>
    /// Killed rather than asked to exit. The deliberate way out is Exit on the
    /// tray icon's context menu, and driving a shell context menu to end a test
    /// is a great deal of machinery whose failure modes would be reported as
    /// this test's. The cost is a notification-area icon the shell keeps drawing
    /// until something hovers over it.
    /// </remarks>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        TheApplication.Kill(_process);
        _process.Dispose();
    }

    /// <summary>
    /// Stops before starting anything if the Commander's own copy is up.
    /// </summary>
    private static void RefuseIfAlreadyRunning()
    {
        Process[] already = Process.GetProcessesByName(TheApplication.Name);

        try
        {
            if (already.Length > 0)
            {
                throw new InvalidOperationException(
                    "Directive 47 is already running, and it allows only one copy — so this test "
                    + "would start a second, be told so, and watch it exit. Close the running one "
                    + "from its notification-area icon and run this again.");
            }
        }
        finally
        {
            foreach (Process copy in already)
            {
                copy.Dispose();
            }
        }
    }

    /// <summary>
    /// Waits for one of the application's windows to reach the desktop, and
    /// takes its handle.
    /// </summary>
    ///
    /// <remarks>
    /// Handles rather than automation elements, because what this project asks
    /// of a window is where it is in the stack and whether Windows considers it
    /// foreground — questions for the operating system rather than for the
    /// automation tree. The one thing read through automation, the text on a
    /// surface, starts from the handle again.
    /// </remarks>
    /// <param name="name">What the window is called.</param>
    /// <param name="within">How long to wait.</param>
    /// <returns>The window's handle.</returns>
    /// <exception cref="InvalidOperationException">It never appeared.</exception>
    private IntPtr WaitForWindow(string name, TimeSpan within) =>
        new(TheApplication.WaitForWindow(_process, name, within, Screen.DescribeStack)
            .Current.NativeWindowHandle);
}
