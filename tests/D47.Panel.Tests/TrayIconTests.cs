using System;

using Shouldly;

using Xunit;

namespace D47.Panel.Tests;

/// <summary>
/// The notification-area icon, asserted against the real notification area
/// rather than against our own bookkeeping.
///
/// <para>
/// This is the one thing the panel presents that is deliberately outside
/// parity: Windows draws the tray icon, so there is no render of ours to
/// project onto the game overlay or into the headset. That is the boundary of
/// what parity can apply to, not a carve-out from it — see
/// <c>docs/decisions.md</c>.
/// </para>
///
/// <para>
/// Slower than the rest of this project, because reading the hidden icons means
/// opening the shell's overflow flyout and putting it away again.
/// </para>
/// </summary>
[Collection(Desktop.Collection)]
public class TrayIconTests
{
    private const string Tooltip = "Directive 47";

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(15);

    private readonly ITestOutputHelper _output;

    public TrayIconTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void TrayIcon_WhileTheAppRuns_IsPresentEvenWithEveryVisualSurfaceHidden()
    {
        using var panel = RunningPanel.Launch();

        // Every visual surface, today, is one window. The game overlay and the
        // VR overlay do not exist yet, and hiding rather than minimizing
        // arrives with #68 — at which point this line changes and the assertion
        // below does not.
        panel.MinimizeWindow();

        WaitForIcon(present: true, "the tray icon is the only evidence the app is running");
    }

    [Fact]
    public void TrayIcon_WhenTheAppExits_LeavesNoGhostBehind()
    {
        using var panel = RunningPanel.Launch();

        WaitForIcon(present: true, "the icon has to arrive before its leaving means anything");

        // Closed, not killed. A killed process leaves its icon on the taskbar
        // until something hovers over it, which is the ghost this is about —
        // so proving the icon goes needs the application to have been given the
        // chance to take it away.
        panel.CloseWindow();

        panel.WaitForExit(TimeSpan.FromSeconds(10)).ShouldBeTrue(
            "closing the window should end the application today");

        WaitForIcon(present: false, "a tray icon outliving its application is a ghost");
    }

    [Fact]
    public void NotificationArea_IsReadable_OnWhateverMachineTheseTestsRunOn()
    {
        // The harness's own claim, checked separately from the story's. If the
        // notification area cannot be read at all, both tests above fail for a
        // reason that has nothing to do with the panel, and this says so.
        string description = NotificationArea.Describe();
        _output.WriteLine(description);

        description.ShouldStartWith("Notification area, on show:");
    }

    /// <summary>
    /// Waits for the icon to arrive or to go, and says what the whole
    /// notification area held if it does not.
    ///
    /// <para>
    /// A method rather than a message passed to <c>ShouldBeTrue</c>, because
    /// Shouldly builds that message eagerly: every passing assertion was
    /// opening and closing the shell's overflow flyout to describe a failure
    /// that had not happened.
    /// </para>
    /// </summary>
    /// <param name="present">What to wait for.</param>
    /// <param name="because">Why it matters, for the failure message.</param>
    private static void WaitForIcon(bool present, string because)
    {
        if (NotificationArea.WaitUntil(Tooltip, present, Patience))
        {
            return;
        }

        string expected = present ? "to be there" : "to be gone";

        throw new ShouldAssertException(
            $"Waited {Patience.TotalSeconds:0} seconds for \"{Tooltip}\" {expected}: "
            + $"{because}.{Environment.NewLine}{NotificationArea.Describe()}");
    }
}
