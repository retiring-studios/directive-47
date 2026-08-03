using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Windows.Automation;

namespace D47.Panel.Tests;

/// <summary>
/// A second copy of Directive 47, launched on purpose at a machine that already
/// has one running.
///
/// <para>
/// Deliberately not a <see cref="RunningPanel"/>. That harness starts the
/// application and waits for the window named "Directive 47", which is exactly
/// what a second copy must never present — so borrowing it would mean waiting
/// twenty seconds for the right behaviour to fail to happen, and reporting it as
/// a panel that would not start.
/// </para>
///
/// <para>
/// Disposable, and the disposal matters more here than usual: a second copy
/// sitting on a modal dialog is a process that will never exit on its own, and a
/// failed assertion must not leave one holding the desktop.
/// </para>
/// </summary>
internal sealed class SecondInstance : IDisposable
{
    /// <summary>
    /// The window class Windows gives a dialog box, and so the class of the
    /// message box a second copy puts up.
    ///
    /// <para>
    /// Matched on rather than trusting whatever top-level window the process
    /// happens to own. Without it, a second copy that wrongly went on to show
    /// the panel would be found here and asked what it said, and the failure
    /// would read as wrong wording rather than as a whole application that
    /// should not exist.
    /// </para>
    /// </summary>
    private const string DialogClass = "#32770";

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(20);

    private readonly Process _process;
    private bool _disposed;

    private SecondInstance(Process process)
    {
        _process = process;
    }

    /// <summary>
    /// Starts another copy and returns immediately, without waiting for it to
    /// present anything. What it presents is the thing under test.
    /// </summary>
    /// <returns>The second copy, which the caller owns and must dispose.</returns>
    /// <exception cref="InvalidOperationException">It did not start at all.</exception>
    internal static SecondInstance Launch()
    {
        Process process = Process.Start(
                new ProcessStartInfo(RunningPanel.Executable()) { UseShellExecute = false })
            ?? throw new InvalidOperationException("The second copy did not start.");

        return new SecondInstance(process);
    }

    /// <summary>
    /// Waits until the second copy has said something.
    ///
    /// <para>
    /// For a caller whose assertion is about the machine while the dialog is
    /// still up rather than about the words on it. A modal dialog is what holds
    /// the second copy open, so this is also how a test gets a stable moment to
    /// look at anything else.
    /// </para>
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// It never put a dialog up. The message says what it did instead.
    /// </exception>
    internal void WaitUntilItHasSaidSomething() => _ = WaitForDialog();

    /// <summary>
    /// What the second copy told the Commander, once it has said anything —
    /// the dialog's caption and every line of text in it.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// It never put a dialog up. The message says what it did instead.
    /// </exception>
    internal string WhatItSaid()
    {
        AutomationElement dialog = WaitForDialog();
        var said = new StringBuilder(dialog.Current.Name);

        AutomationElementCollection texts = dialog.FindAll(
            TreeScope.Descendants,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text));

        for (int index = 0; index < texts.Count; index++)
        {
            said.AppendLine().Append(texts[index].Current.Name);
        }

        return said.ToString();
    }

    /// <summary>
    /// Presses the dialog's one button, the way a Commander would.
    ///
    /// <para>
    /// Found by being the only button rather than by being called "OK". The
    /// caption on that button comes from Windows and is translated with it, and
    /// a harness that only works on an English machine is a harness that reports
    /// a missing dialog when it is really a missing language.
    /// </para>
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// There was no dialog, or nothing on it to press.
    /// </exception>
    internal void Acknowledge()
    {
        AutomationElement dialog = WaitForDialog();

        AutomationElement button = dialog.FindFirst(
                TreeScope.Children,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button))
            ?? throw new InvalidOperationException(
                $"The dialog has no button to press, so there is no way out of it."
                + $"{Environment.NewLine}{Describe()}");

        if (!button.TryGetCurrentPattern(InvokePattern.Pattern, out object pattern))
        {
            throw new InvalidOperationException(
                $"\"{button.Current.Name}\" cannot be invoked."
                + $"{Environment.NewLine}{Describe()}");
        }

        ((InvokePattern)pattern).Invoke();
    }

    /// <summary>
    /// How many buttons the dialog offers. One is the decision; more than one
    /// would be a choice nobody agreed to give.
    ///
    /// <para>
    /// The dialog's own children, not its descendants. Windows puts a close
    /// button in the title bar of every dialog and automation reports it as a
    /// button like any other, so counting descendants counts two and always
    /// would — measured, not assumed. The title bar is a child of the dialog
    /// and its close button is a child of *that*, which is what makes one
    /// generation the whole distinction.
    /// </para>
    /// </summary>
    internal int Buttons()
    {
        AutomationElement dialog = WaitForDialog();

        return dialog.FindAll(
            TreeScope.Children,
            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button)).Count;
    }

    /// <summary>
    /// Waits for the second copy to end.
    /// </summary>
    /// <param name="within">How long to wait.</param>
    /// <returns>Whether it exited in time.</returns>
    internal bool WaitForExit(TimeSpan within) =>
        _process.WaitForExit((int)within.TotalMilliseconds);

    /// <summary>
    /// Every top-level window this copy owns, named and classed. A dialog that
    /// did not appear says nothing about why; a panel window in its place says
    /// everything.
    /// </summary>
    internal string Describe()
    {
        var description = new StringBuilder();

        _process.Refresh();

        description.AppendLine(
            CultureInfo.InvariantCulture,
            $"The second copy is {(_process.HasExited ? "gone" : "still running")}. Its "
            + $"top-level windows:");

        AutomationElement? root = AutomationElement.RootElement;

        AutomationElementCollection? windows = root?.FindAll(
            TreeScope.Children,
            new PropertyCondition(AutomationElement.ProcessIdProperty, _process.Id));

        if (windows is null || windows.Count == 0)
        {
            description.AppendLine("  (none)");
            return description.ToString();
        }

        for (int index = 0; index < windows.Count; index++)
        {
            AutomationElement window = windows[index];

            try
            {
                description.AppendLine(
                    CultureInfo.InvariantCulture,
                    $"  class=\"{window.Current.ClassName}\" name=\"{window.Current.Name}\"");
            }
            catch (ElementNotAvailableException)
            {
                // It closed while being described — which is what a second copy
                // that is exiting does. This runs only when something has
                // already gone wrong, and throwing here would replace the real
                // failure with an unrelated one.
                description.AppendLine("  (a window that closed while being read)");
            }
        }

        return description.ToString();
    }

    /// <summary>
    /// Ends the second copy. Safe to call when it has already gone, which is
    /// what a passing test leaves behind.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _process.Refresh();

        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
            _process.WaitForExit(5000);
        }

        _process.Dispose();
    }

    private AutomationElement WaitForDialog()
    {
        AutomationElement? dialog = null;

        var condition = new AndCondition(
            new PropertyCondition(AutomationElement.ProcessIdProperty, _process.Id),
            new PropertyCondition(AutomationElement.ClassNameProperty, DialogClass));

        Eventually.True(
            () => (dialog = AutomationElement.RootElement?.FindFirst(TreeScope.Children, condition))
                is not null,
            Patience);

        return dialog
            ?? throw new InvalidOperationException(
                $"No dialog appeared within {Patience.TotalSeconds:0} seconds. A second copy is "
                + $"supposed to say it is already running and close."
                + $"{Environment.NewLine}{Describe()}");
    }
}
