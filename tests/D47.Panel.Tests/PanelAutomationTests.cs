using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Automation;

using Shouldly;

using Xunit;

namespace D47.Panel.Tests;

/// <summary>
/// The panel, launched for real and read the way a screen reader or an
/// automation client would read it. Everything else in this project inspects a
/// visual tree it built itself, which is blind to how the running application
/// presents itself to the outside.
///
/// <para>
/// That blindness is not hypothetical. This test began as a spike and
/// immediately found that the window announced the <c>ToString()</c> of an
/// <c>Answer</c> record as its accessible name while four visual-tree tests
/// happily confirmed the right text was on screen.
/// </para>
///
/// <para>
/// Raw <c>System.Windows.Automation</c> rather than FlaUI. It ships with WPF and
/// has been enough so far; FlaUI wraps these same APIs and is a package, so it
/// waits until the ergonomics actually hurt — see the notes on the automation
/// task.
/// </para>
///
/// <para>
/// Reports what it could see rather than only passing or failing, because "no
/// window" and "no desktop" are different failures with different fixes.
/// </para>
/// </summary>
public class PanelAutomationTests
{
    private readonly ITestOutputHelper _output;

    public PanelAutomationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Panel_WhenRunning_IsReachableByAutomationUnderItsOwnName()
    {
        string exe = Path.Combine(AppContext.BaseDirectory, "D47.Panel.exe");
        File.Exists(exe).ShouldBeTrue($"the panel exe should sit beside the tests, at {exe}");

        using Process panel = Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false })
            ?? throw new InvalidOperationException("the panel did not start");

        try
        {
            // Win32 first. If there is a real window handle, the window exists,
            // and anything UI Automation cannot see afterwards is UIA's problem
            // rather than the desktop's.
            IntPtr handle = IntPtr.Zero;

            for (int attempt = 0; attempt < 30 && handle == IntPtr.Zero; attempt++)
            {
                panel.Refresh();
                handle = panel.MainWindowHandle;

                if (handle == IntPtr.Zero)
                {
                    Thread.Sleep(500);
                }
            }

            _output.WriteLine($"process alive       : {!panel.HasExited}");
            _output.WriteLine($"win32 window handle : {handle}");
            _output.WriteLine($"win32 window title  : \"{panel.MainWindowTitle}\"");

            AutomationElement? onMta = Probe("MTA (default test thread)");
            AutomationElement? onSta = null;
            OnAStaThread(() => onSta = Probe("STA thread"));

            _output.WriteLine($"found on MTA        : {onMta is not null}");
            _output.WriteLine($"found on STA        : {onSta is not null}");

            handle.ShouldNotBe(IntPtr.Zero, "the panel never produced a window handle");
            (onMta ?? onSta).ShouldNotBeNull("UI Automation could not see a window that Win32 can");
        }
        finally
        {
            if (!panel.HasExited)
            {
                panel.Kill(entireProcessTree: true);
                panel.WaitForExit(5000);
            }
        }
    }

    private AutomationElement? Probe(string label)
    {
        _output.WriteLine($"--- {label} ---");

        AutomationElement? root = AutomationElement.RootElement;

        if (root is null)
        {
            _output.WriteLine("  root element      : <null>");
            return null;
        }

        _output.WriteLine($"  root element      : \"{root.Current.Name}\" ({root.Current.ClassName})");

        AutomationElementCollection children = root.FindAll(TreeScope.Children, Condition.TrueCondition);
        _output.WriteLine($"  top-level windows : {children.Count}");

        List<string> tops = [];

        for (int child = 0; child < children.Count; child++)
        {
            tops.Add($"\"{children[child].Current.Name}\" ({children[child].Current.ClassName})");
        }

        foreach (string top in tops)
        {
            _output.WriteLine($"      {top}");
        }

        return root.FindFirst(
            TreeScope.Children,
            new PropertyCondition(AutomationElement.NameProperty, "Directive 47"));
    }

    private static void OnAStaThread(Action action)
    {
        Exception? failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
#pragma warning disable CA1031
            catch (Exception error)
            {
                failure = error;
            }
#pragma warning restore CA1031
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
