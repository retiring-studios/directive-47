using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using D47.TestSupport;

namespace D47.Render.Tests;

/// <summary>
/// Builds a real visual tree and reads back what it says. Asserting on a view
/// model instead would prove the view model and nothing about the rendering,
/// which is the thing these tests exist to check.
///
/// <para>
/// What is left here is the part that is about <see cref="CapabilityView"/>. The
/// thread it needs and the walk that reads it back were the same in two test
/// projects and now live in <c>D47.TestSupport</c>.
/// </para>
/// </summary>
internal static class Rendering
{
    private static readonly Size Surface = new(1920, 1080);

    /// <summary>
    /// Renders an answer through <see cref="CapabilityView"/> and returns every
    /// line of text the result actually put on screen, top to bottom.
    /// </summary>
    internal static IReadOnlyList<string> LinesFor(Answer answer) =>
        StaThread.Run(() => VisualTree.TextIn(LaidOut(answer)));

    /// <summary>
    /// The face every line of the rendered answer is actually set in, top to
    /// bottom, as the render itself asked for it.
    /// </summary>
    internal static IReadOnlyList<Face> FacesFor(Answer answer) =>
        StaThread.Run(() => LabelsIn(LaidOut(answer))
            .Select(label => Face.Of(
                new Typeface(label.FontFamily, label.FontStyle, label.FontWeight, label.FontStretch)))
            .ToList());

    /// <summary>
    /// The face the rendered answer's own font family resolves to at some other
    /// weight — what emphatic text will get when there is any.
    /// </summary>
    internal static Face FaceAt(Answer answer, FontWeight weight) =>
        StaThread.Run(() =>
        {
            TextBlock label = LabelsIn(LaidOut(answer))[0];

            return Face.Of(
                new Typeface(label.FontFamily, label.FontStyle, weight, label.FontStretch));
        });

    /// <summary>
    /// A view of the answer, measured and arranged, on the thread that will read
    /// it back. A control cannot outlive the thread that built it, so nothing
    /// here hands one out.
    /// </summary>
    private static CapabilityView LaidOut(Answer answer)
    {
        var view = new CapabilityView { DataContext = answer };

        view.Measure(Surface);
        view.Arrange(new Rect(Surface));
        view.UpdateLayout();

        return view;
    }

    /// <summary>
    /// Every label under an element, in tree order.
    /// </summary>
    ///
    /// <remarks>
    /// Deliberately not the visibility-aware walk in <see cref="VisualTree"/>,
    /// and not a copy of it either. That one answers what a reader can see,
    /// where reporting a collapsed element's words is a wrong answer. This one
    /// answers what the render set the type in, where an element nobody can see
    /// is set in the same font as the rest and cannot turn a wrong font into a
    /// pass.
    /// </remarks>
    private static List<TextBlock> LabelsIn(DependencyObject root)
    {
        List<TextBlock> labels = [];

        for (int child = 0; child < VisualTreeHelper.GetChildrenCount(root); child++)
        {
            DependencyObject descendant = VisualTreeHelper.GetChild(root, child);

            if (descendant is TextBlock label)
            {
                labels.Add(label);
            }

            labels.AddRange(LabelsIn(descendant));
        }

        return labels;
    }
}
