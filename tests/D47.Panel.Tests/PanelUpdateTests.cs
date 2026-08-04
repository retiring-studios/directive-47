using System.Windows;
using System.Windows.Controls;

using D47.Render;
using D47.TestSupport;

using Shouldly;

using Xunit;

namespace D47.Panel.Tests;

/// <summary>
/// The two controls the panel offers when there is a newer version.
///
/// <para>
/// The notice itself is the render's and reaches all three surfaces —
/// <c>NoticeTests</c> in D47.Render.Tests is where that is asserted. What is
/// here is the pair of controls that answer it, which are the panel's alone:
/// Elite captures the cursor and the motion controllers belong to the game, so
/// a button in the render would work on one surface and be dead on another.
/// Answering in the headset is voice, and arrives with the loop.
/// </para>
///
/// <para>
/// In process and off the desktop tier. The window is built and never shown —
/// what is being asked is what it decided to put in its own strip, which it
/// decides while it is being constructed.
/// </para>
/// </summary>
[Collection(CompiledXaml.Collection)]
public class PanelUpdateTests
{
    [Fact]
    public void Panel_WhenAnUpdateIsWaiting_OffersBothAnswers()
    {
        (Visibility offered, string take, string leave) = InAPanelShowing("0.2.0",
            panel => (panel.AboutTheUpdate.Visibility,
                      (string)panel.TakeIt.Content,
                      (string)panel.LeaveIt.Content));

        offered.ShouldBe(Visibility.Visible);

        // Named for what pressing them does rather than for yes and no. "Install
        // on exit" is the whole of the promise this feature makes — that
        // accepting does not interrupt the session it was accepted in.
        take.ShouldBe("Install on exit");
        leave.ShouldBe("Not now");
    }

    [Fact]
    public void Panel_WhenNothingIsWaiting_OffersNothingToAnswer()
    {
        // Every ordinary run on a current copy. A pair of buttons about an
        // update nobody has is furniture that means nothing, and the strip is
        // chrome — anything left in it competes with the render.
        Visibility offered = InAPanelShowing(null, panel => panel.AboutTheUpdate.Visibility);

        offered.ShouldBe(Visibility.Collapsed);
    }

    [Fact]
    public void Panel_WhenTheUpdateIsAccepted_TellsTheUpdaterAndStopsAsking()
    {
        var updater = new UpdateSources.Offering("0.2.0");

        Visibility afterwards = InAPanelShowing("0.2.0", updater, panel =>
        {
            panel.TakeIt.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            return panel.AboutTheUpdate.Visibility;
        });

        updater.Fetched.ShouldBeTrue("accepting fetches, so the slow part is over before the exit");
        afterwards.ShouldBe(
            Visibility.Collapsed,
            "a question still on screen after it was answered is one nobody can tell they answered");
    }

    [Fact]
    public void Panel_WhenTheUpdateIsDeclined_TellsTheUpdaterAndStopsAsking()
    {
        var updater = new UpdateSources.Offering("0.2.0");

        Visibility afterwards = InAPanelShowing("0.2.0", updater, panel =>
        {
            panel.LeaveIt.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            return panel.AboutTheUpdate.Visibility;
        });

        updater.Fetched.ShouldBeFalse("nothing declined is worth downloading");
        afterwards.ShouldBe(Visibility.Collapsed);
    }

    private static T InAPanelShowing<T>(string? waiting, System.Func<MainWindow, T> asking) =>
        InAPanelShowing(waiting, new UpdateSources.Offering(), asking);

    /// <summary>
    /// Builds a panel showing what is given, asks it something, and takes it
    /// away. Never shown: what it decided to put in its strip is decided as it
    /// is built.
    /// </summary>
    private static T InAPanelShowing<T>(
        string? waiting, IUpdateSource updater, System.Func<MainWindow, T> asking)
    {
        using var folder = new TemporaryStore();
        string file = folder.File;

        return StaThread.Run(() =>
        {
            var updates = Updates.From(updater, Ignored);
            updates.Look();

            var panel = new MainWindow(
                new Presentation { Answer = Fixtures.HelpsAnswer(), UpdateWaiting = waiting },
                new Zoom(Store.OpenAt(file, Ignored), Ignored),
                updates);

            return asking(panel);
        });
    }

    private static void Ignored(string complaint)
    {
        // A store in a folder of its own and an updater that answers have
        // nothing to complain about.
    }
}
