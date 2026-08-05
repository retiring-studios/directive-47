using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

using D47.Panel;
using D47.TestSupport;

using Shouldly;

using Xunit;

namespace D47.Tier1.Tests.Panel;

/// <summary>
/// The strip along the bottom of the panel: out, what the zoom is, and in.
///
/// <para>
/// A visible affordance as well as a hidden gesture. Ctrl and the wheel is what
/// most people reach for and it is also something nobody discovers by looking —
/// a Commander who has never used it has no way to find out the panel zooms at
/// all, and one who has zoomed by accident has no way to see that they did or
/// what to do about it.
/// </para>
/// </summary>
[Collection(CompiledXaml.Collection)]
public class PanelZoomStripTests
{
    [Fact]
    public void Panel_ForItsZoom_ShowsWhatItIsAndAWayToChangeIt()
    {
        (string atLifeSize, string afterIn, string afterOut) = OpenedPanel.Opened((panel, _) =>
        {
            string first = Reading(panel);

            Press(panel, "Bigger");
            string bigger = Reading(panel);

            Press(panel, "Smaller");

            return (first, bigger, Reading(panel));
        });

        atLifeSize.ShouldBe(
            "100%",
            customMessage: "the strip should say where the zoom is before anything has moved it");

        afterIn.ShouldBe("110%", customMessage: "the strip's own control should zoom the panel");

        afterOut.ShouldBe("100%", customMessage: "and take it back again");
    }

    [Fact]
    public void Panel_AtTheEndsOfItsZoom_StopsOfferingTheDirectionItCannotGo()
    {
        // A control that stays enabled at the end of the range is a control that
        // does nothing when it is pressed, and the Commander cannot tell that
        // from one that is broken.
        (bool inAtTheTop, bool outAtTheTop, bool inAtTheBottom) = OpenedPanel.Opened(
            (panel, zoom) =>
            {
                while (zoom.CanGoIn)
                {
                    zoom.In();
                }

                panel.UpdateLayout();

                (bool up, bool down) = (Offered(panel, "Bigger"), Offered(panel, "Smaller"));

                while (zoom.CanGoOut)
                {
                    zoom.Out();
                }

                panel.UpdateLayout();

                return (up, down, Offered(panel, "Bigger"));
            });

        inAtTheTop.ShouldBeFalse("there is nothing bigger than the biggest level");
        outAtTheTop.ShouldBeTrue("but there is always a way back down from it");
        inAtTheBottom.ShouldBeTrue("and a way back up from the bottom");
    }

    /// <summary>
    /// What the strip currently says the zoom is.
    /// </summary>
    private static string Reading(MainWindow panel) =>
        Named<TextBlock>(panel, "Reading").Text;

    /// <summary>
    /// Whether one of the strip's controls is being offered.
    /// </summary>
    private static bool Offered(MainWindow panel, string named) =>
        Named<ButtonBase>(panel, named).IsEnabled;

    /// <summary>
    /// Presses one of the strip's controls, the way a click would.
    /// </summary>
    private static void Press(MainWindow panel, string named)
    {
        Named<ButtonBase>(panel, named).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

        panel.UpdateLayout();
    }

    /// <summary>
    /// The named part of the strip, or a failure that says which one was missing
    /// rather than a null reference somewhere further on.
    /// </summary>
    private static T Named<T>(MainWindow panel, string name)
        where T : FrameworkElement =>
        panel.FindName(name) as T
            ?? throw new Xunit.Sdk.XunitException(
                $"The panel has no \"{name}\" in its zoom strip, so there is nothing to read or "
                + "press.");
}
