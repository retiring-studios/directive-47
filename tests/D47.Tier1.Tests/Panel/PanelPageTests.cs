using System.Windows.Media;

using D47.Panel;
using D47.Render;
using D47.TestSupport;

using Shouldly;

using Xunit;

namespace D47.Tier1.Tests.Panel;

/// <summary>
/// What the panel does when the page in it changes.
///
/// <para>
/// In process, against a real window, the same way <c>PanelSizeTests</c> asks
/// what size it opened at. Every fact here is a property of the window rather
/// than a judgement about how it looks — so a person reading them off a screen
/// is a person doing a machine's work, which is exactly what happened on the
/// first pass through this page and what these exist to stop happening again.
/// </para>
/// </summary>
[Collection(CompiledXaml.Collection)]
public class PanelPageTests
{
    [Fact]
    public void Panel_WhenTheSettingsPageGoesIn_IsTheSizeOfThePageRatherThanTheRender()
    {
        (double render, double page) = OpenedPanel.Opened((panel, _) =>
        {
            double before = panel.Height;

            panel.Showing(APage());

            return (before, panel.Height);
        });

        // A window still the size of the render is a window with a scrollbar
        // around three rows, which is what the first pass through this found.
        page.ShouldNotBe(render);
    }

    [Fact]
    public void Panel_WhenTheSettingsPageComesOut_IsTheSizeOfTheRenderAgain()
    {
        (double before, double after) = OpenedPanel.Opened((panel, _) =>
        {
            double render = panel.Height;

            panel.Showing(APage());
            panel.Showing(null);

            return (render, panel.Height);
        });

        // Back, not merely different. Going in and out must not walk the window
        // down the screen a page at a time.
        after.ShouldBe(before, tolerance: 1.0);
    }

    [Fact]
    public void Panel_WhateverPageIsInIt_DrawsItOnTheRendersBackground()
    {
        Color behind = OpenedPanel.Opened((panel, _) =>
        {
            panel.Showing(APage());

            return ((SolidColorBrush)panel.SurfaceBackground).Color;
        });

        // The window names no colour of its own — PanelBackgroundTests says so
        // and #100 is why. What carries it is the surface a page sits on, and it
        // is the render's brush rather than a second spelling of the same hex.
        behind.ShouldBe(Palette.Background.Color);
    }

    [Fact]
    public void Panel_AtAZoom_AppliesItToTheSettingsPageAndNotOnlyToTheRender()
    {
        double scaled = OpenedPanel.Opened((panel, zoom) =>
        {
            zoom.In();
            panel.Showing(APage());

            return ((ScaleTransform)panel.OnDisplay!.LayoutTransform).ScaleX;
        });

        // The page is a page in the panel, so the panel's zoom is its zoom. It
        // scaled the render by name, which left the settings page as the one
        // thing in the window a Commander could not make bigger.
        scaled.ShouldBeGreaterThan(1.0);
    }

    private static SettingsPage APage()
    {
        using var folder = new TemporaryStore();

        return SettingsPage.For(folder.Settings(), (_, _) => { });
    }
}
