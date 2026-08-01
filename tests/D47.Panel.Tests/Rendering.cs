using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace D47.Panel.Tests;

/// <summary>
/// Builds a real visual tree and reads back what it says. Asserting on a view
/// model instead would prove the view model and nothing about the rendering,
/// which is the thing these tests exist to check.
/// </summary>
internal static class Rendering
{
    private static readonly Size Surface = new(1920, 1080);

    /// <summary>
    /// Renders an answer through <see cref="CapabilityView"/> and returns every
    /// line of text the result actually put on screen, top to bottom.
    /// </summary>
    internal static IReadOnlyList<string> LinesFor(Answer answer)
    {
        IReadOnlyList<string> lines = [];

        OnAStaThread(() =>
        {
            var view = new CapabilityView { DataContext = answer };

            view.Measure(Surface);
            view.Arrange(new Rect(Surface));
            view.UpdateLayout();

            List<string> found = [];
            Collect(view, found);
            lines = found;
        });

        return lines;
    }

    /// <summary>
    /// WPF will not build a visual tree on a thread that is not STA, and the
    /// test runner's threads are not.
    /// </summary>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification =
            "Marshalling a failure back to the calling thread is the whole job. "
            + "Rethrowing inside the catch would tear down the process instead of "
            + "failing the test, and narrowing the type would swallow whatever it "
            + "did not name.")]
    private static void OnAStaThread(Action action)
    {
        Exception? failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception error)
            {
                failure = error;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private static void Collect(DependencyObject node, List<string> lines)
    {
        int children = VisualTreeHelper.GetChildrenCount(node);

        for (int child = 0; child < children; child++)
        {
            DependencyObject descendant = VisualTreeHelper.GetChild(node, child);

            if (descendant is TextBlock block)
            {
                lines.Add(block.Text);
            }

            Collect(descendant, lines);
        }
    }
}
