using System;

using Shouldly;

using Xunit;

using Rect = System.Windows.Rect;

namespace D47.Tier1.Tests.Panel;

/// <summary>
/// The way back. Hiding the panel is a one-way door without this, and the tray
/// icon is decoration.
/// </summary>
public class TrayActivationTests : DesktopTest
{
    private const string Tooltip = "Directive 47";

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private readonly ITestOutputHelper _output;

    public TrayActivationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void TrayIcon_WhenActivated_RestoresThePanel()
    {
        using var panel = RunningPanel.Launch();

        panel.CloseWindow();
        panel.WaitForWindowToGo(Patience).ShouldBeTrue(
            "the panel has to be away before bringing it back means anything");

        NotificationArea.Activate(Tooltip);

        panel.WaitForWindowToReturn(Patience).ShouldBeTrue(
            "activating the tray icon should bring the panel back");
    }

    [Fact]
    public void TrayIcon_WhenActivatedWhileShown_BringsThePanelForward()
    {
        using var panel = RunningPanel.Launch();

        // Minimized rather than hidden, because that is the case a restore
        // written as "if it is not visible, show it" gets wrong. A minimized
        // window is still visible by that test, so nothing happens and the
        // Commander clicks the icon again and again.
        panel.MinimizeWindow();
        Eventually.True(() => panel.IsMinimized, Patience).ShouldBeTrue(
            "the panel should be minimized before the click");

        NotificationArea.Activate(Tooltip);

        Eventually.True(() => !panel.IsMinimized, Patience).ShouldBeTrue(
            "activating the tray icon should un-minimize the panel");

        if (!Eventually.True(() => panel.IsForeground, Patience))
        {
            throw new ShouldAssertException(
                "Activating the tray icon should bring the panel in front, and it did not."
                + $"{Environment.NewLine}{Desktop.DescribeForeground()}");
        }
    }

    [Fact]
    public void TrayIcon_WhenItRestoresThePanel_BringsBackTheOneThatWasHidden()
    {
        using var panel = RunningPanel.Launch();

        // Somewhere the panel would never land by itself, so that coming back
        // to it cannot be a coincidence. This is the assertion that fails if a
        // restore is written as "new MainWindow().Show()", which looks right
        // and throws away everything the Commander had done.
        var moved = new Rect(320, 240, 700, 520);
        panel.MoveAndResize(moved);

        Rect asHidden = panel.Bounds;
        _output.WriteLine($"Hidden at {asHidden}.");

        panel.CloseWindow();
        panel.WaitForWindowToGo(Patience).ShouldBeTrue("the panel should hide");

        NotificationArea.Activate(Tooltip);
        panel.WaitForWindowToReturn(Patience).ShouldBeTrue("the panel should come back");

        Rect asRestored = panel.Bounds;
        _output.WriteLine($"Restored at {asRestored}.");

        asRestored.ShouldBe(asHidden);
    }
}
