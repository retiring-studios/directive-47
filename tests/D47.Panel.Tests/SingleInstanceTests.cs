using System;

using Shouldly;

using Xunit;

namespace D47.Panel.Tests;

/// <summary>
/// What happens when Directive 47 is started at a machine that is already
/// running it.
///
/// <para>
/// The failure this is designed against is silence, and it was found by being
/// bitten by it. Two copies from the same folder produced two tray icons, two
/// overlays stacked exactly on top of each other, and a hotkey that appeared
/// dead — pressing it hid the first copy's overlay and revealed the second's
/// underneath, so nothing seemed to happen at all. The log said the combination
/// belonged to another application, which was true and sent the reader looking
/// at their other software. See
/// [#107](https://github.com/retiring-studios/directive-47/issues/107).
/// </para>
///
/// <para>
/// Every assertion here is about the second copy, because the first one is
/// supposed to be untouched by any of it.
/// </para>
/// </summary>
public class SingleInstanceTests : DesktopTest
{
    private const string Tooltip = "Directive 47";

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(15);

    [Fact]
    public void Application_WhenAlreadyRunning_SaysSoAndCloses()
    {
        using var first = RunningPanel.Launch();
        using var second = SecondInstance.Launch();

        second.WhatItSaid().ShouldContain(
            "already running",
            customMessage:
                "a second copy that closes without saying why is the silence this was filed for");

        second.Buttons().ShouldBe(
            1,
            customMessage:
                "the dialog acknowledges, and offering a second button offers a choice nobody "
                + "agreed to");

        second.Acknowledge();

        second.WaitForExit(Patience).ShouldBeTrue(
            $"a second copy has to close once acknowledged.{Environment.NewLine}{second.Describe()}");

        first.WaitForExit(TimeSpan.FromSeconds(2)).ShouldBeFalse(
            "the copy that was already running is the one that keeps running");
    }

    [Fact]
    public void Application_WhenAlreadyRunning_PutsNoSecondIconInTheNotificationArea()
    {
        using var first = RunningPanel.Launch();

        NotificationArea.WaitUntil(Tooltip, present: true, Patience).ShouldBeTrue(
            $"the first copy's icon has to arrive before a second one means anything."
            + $"{Environment.NewLine}{NotificationArea.Describe()}");

        using var second = SecondInstance.Launch();

        // Asserted while the dialog is still up, which is what makes this a
        // question and not a race. A second copy sitting on a modal dialog is a
        // process that will not exit until it is told to — so an icon it put up
        // on the way is there to be found for as long as the test cares to look.
        second.WaitUntilItHasSaidSomething();

        NotificationArea.Count(Tooltip).ShouldBe(
            1,
            customMessage:
                $"two identical icons are two answers to \"is it running\", and the icon is the "
                + $"only sign of that there is.{Environment.NewLine}{NotificationArea.Describe()}");

        second.Acknowledge();
    }
}
