using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

using D47.TestSupport;

using Shouldly;

using Xunit;

namespace D47.Render.Tests;

/// <summary>
/// The shared walker that reads back what a control actually put on screen.
///
/// <para>
/// It has its own facts because it had its own defect. The same walk existed in
/// two test projects and the copies disagreed: the overlay's checked visibility,
/// the render's did not. That check was added in
/// [#111](https://github.com/retiring-studios/directive-47/pull/111), when a
/// collapsed drag bar's own label turned up in what the overlay was said to be
/// presenting — and it landed in one copy only. A helper that reports words
/// nobody can see makes every test using it a little bit untrue.
/// </para>
///
/// <para>
/// Here rather than in a test project of the support project's own. This is the
/// consumer whose copy was wrong, it already builds visual trees in process, and
/// a fourth project to hold four facts expresses a distinction the solution does
/// not have.
/// </para>
///
/// <para>
/// Every fact reads the tree on the thread that built it and brings back only
/// strings. A control cannot outlive its thread, so returning one and asserting
/// on it here would fail for a reason that has nothing to do with the subject.
/// </para>
/// </summary>
public class VisualTreeTests
{
    /// <summary>
    /// Room enough that nothing wraps or clips. The number is arbitrary and
    /// deliberately not the render's own surface size — nothing here is about
    /// how much space there is.
    /// </summary>
    private static readonly Size RoomEnough = new(1920, 1080);

    [Fact]
    public void VisualTree_ReadsTheWordsThatAreThere_InTheOrderTheyAreIn()
    {
        TextIn(("first", Visibility.Visible), ("second", Visibility.Visible))
            .ShouldBe(["first", "second"]);
    }

    [Fact]
    public void VisualTree_WhenSomethingIsCollapsed_DoesNotReportWordsNobodyCanSee()
    {
        TextIn(("on screen", Visibility.Visible), ("away", Visibility.Collapsed)).ShouldBe(
            ["on screen"],
            customMessage:
                "a collapsed element is still in the visual tree, so a walk that ignores "
                + "visibility reports words nobody can see");
    }

    [Fact]
    public void VisualTree_WhenSomethingIsHidden_DoesNotReportItEither()
    {
        // Hidden and collapsed differ in whether the space is kept, and in
        // nothing that matters here: neither is readable.
        TextIn(("on screen", Visibility.Visible), ("away", Visibility.Hidden))
            .ShouldBe(["on screen"]);
    }

    [Fact]
    public void VisualTree_WhenAParentIsCollapsed_TakesItsChildrenWithIt()
    {
        // The case a per-element check gets right only by accident. A collapsed
        // parent takes its children with it on screen, so a walk that asks each
        // element on its own would report a visible label inside an invisible
        // panel.
        IReadOnlyList<string> lines = StaThread.Run(() =>
        {
            var inner = new StackPanel { Visibility = Visibility.Collapsed };
            inner.Children.Add(new TextBlock { Text = "inside something collapsed" });

            var outer = new StackPanel();
            outer.Children.Add(new TextBlock { Text = "on screen" });
            outer.Children.Add(inner);

            Lay(outer);

            return VisualTree.TextIn(outer);
        });

        lines.ShouldBe(["on screen"]);
    }

    /// <summary>
    /// Lays out a stack of labels and reads it back, all on a thread WPF will
    /// talk to.
    /// </summary>
    private static IReadOnlyList<string> TextIn(params (string Text, Visibility Shown)[] labels) =>
        StaThread.Run(() =>
        {
            var stack = new StackPanel();

            foreach ((string text, Visibility shown) in labels)
            {
                stack.Children.Add(new TextBlock { Text = text, Visibility = shown });
            }

            Lay(stack);

            return VisualTree.TextIn(stack);
        });

    private static void Lay(FrameworkElement element)
    {
        element.Measure(RoomEnough);
        element.Arrange(new Rect(RoomEnough));
        element.UpdateLayout();
    }
}
