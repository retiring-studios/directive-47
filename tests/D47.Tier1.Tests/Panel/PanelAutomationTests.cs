using System;
using System.Diagnostics;

using Shouldly;

using Xunit;

namespace D47.Tier1.Tests.Panel;

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
public class PanelAutomationTests : DesktopTest
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
    public void Panel_WhenToldToHide_ClosesToTheSystemTrayRatherThanExiting()
    {
        // The rewrite its predecessor asked for. Until #68 this test asserted
        // the opposite — that closing the window ended the application — and it
        // said so, so that the change would land deliberately instead of a
        // passing test quietly starting to mean something else.
        //
        // The panel is convenience. The voice loop is the product, and closing
        // a convenience must not take the product down with it.
        using var panel = RunningPanel.Launch();

        panel.CloseWindow();

        panel.WaitForWindowToGo(TimeSpan.FromSeconds(10)).ShouldBeTrue(
            "the close control should put the window away");

        // Waited out rather than sampled. Asking whether the process is alive
        // the instant the window goes answers yes even when the application is
        // in the middle of shutting down — which is how this test passed
        // against the old exit-on-close behaviour it was written to replace.
        panel.WaitForExit(TimeSpan.FromSeconds(5)).ShouldBeFalse(
            "closing the panel should leave the application running");
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
