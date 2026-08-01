using System;
using System.Diagnostics;

using Shouldly;

using Xunit;

namespace D47.Panel.Tests;

/// <summary>
/// The panel driven from outside the process, through UI Automation. Every
/// other test in this project builds a visual tree in-process and inspects what
/// it built, which says nothing about how the running application presents
/// itself to a screen reader, to automation, or to anything else on the machine.
///
/// <para>
/// That gap is not hypothetical. The first of these found a window announcing
/// the <c>ToString()</c> of a model object as its accessible name, while four
/// in-process tests happily confirmed the right text was on screen.
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
        using var panel = RunningPanel.Launch();

        panel.Window.Current.Name.ShouldBe("Directive 47");
    }

    [Fact]
    public void Panel_WhenRunning_PresentsTheAnswerItRendered()
    {
        using var panel = RunningPanel.Launch();

        _output.WriteLine(panel.Describe());

        // Help's top level, read back out of the running application rather
        // than out of a visual tree the test built for itself. One group,
        // because help is currently the only registered capability.
        panel.VisibleText().ShouldBe(["Getting around"]);
    }

    [Fact]
    public void Panel_WhenItsWindowIsClosed_ExitsRatherThanLingering()
    {
        // Today closing the window ends the application. #68 changes that to
        // hiding, and this test is where the change becomes visible: it has to
        // be rewritten, deliberately, rather than quietly continuing to pass.
        using var panel = RunningPanel.Launch();

        panel.CloseWindow();

        panel.WaitForExit(TimeSpan.FromSeconds(10)).ShouldBeTrue(
            "closing the window should end the application today");
    }

    [Fact]
    public void Automation_WhenAnAssertionFails_StillLeavesNoProcessBehind()
    {
        // Every other test here holds the panel in a using. So the thing worth
        // proving is that the using unwinds the process when an assertion
        // throws — not that calling Dispose by hand works, which is a different
        // and less interesting claim.
        // The stand-in has its own type on purpose. RunningPanel.Launch throws
        // InvalidOperationException, and a catch wide enough to hold a stand-in
        // failure is wide enough to hide a real one.
        int abandoned = 0;

        try
        {
            using var panel = RunningPanel.Launch();
            abandoned = panel.ProcessId;

            throw new StandInFailure();
        }
        catch (StandInFailure)
        {
            // swallowed on purpose: the unwinding is what is under test
        }

        // Asked of the operating system, not of the harness. A teardown that
        // reports its own success is not evidence that anything was torn down.
        Should.Throw<ArgumentException>(() => Process.GetProcessById(abandoned));
    }

    /// <summary>
    /// Stands in for an assertion failing mid-test. Its own type so the catch
    /// that swallows it cannot also swallow a real failure to launch.
    /// </summary>
    private sealed class StandInFailure : Exception
    {
        internal StandInFailure()
            : base("stand-in for an assertion failing mid-test")
        {
        }

        internal StandInFailure(string message)
            : base(message)
        {
        }

        internal StandInFailure(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    [Fact]
    public void Automation_Describes_WhatTheTreeActuallyContained()
    {
        // "Element not found" without the tree tells you nothing about why, so
        // every failure message in these tests carries one.
        using var panel = RunningPanel.Launch();

        string tree = panel.Describe();
        _output.WriteLine(tree);

        tree.ShouldContain("Directive 47");
        tree.ShouldContain("Getting around");
        tree.ShouldContain("ControlType.Text");
    }
}
