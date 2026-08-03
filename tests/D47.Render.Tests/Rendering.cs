using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
    /// One render at a time across the whole assembly.
    /// </summary>
    ///
    /// <remarks>
    /// xUnit runs test classes in parallel, so without this two threads call
    /// <c>Application.LoadComponent</c> for the same compiled XAML at once — and
    /// the part of WPF that reads it keeps one list of open streams per resource
    /// with no lock around it. It fails as an index out of range inside
    /// <c>PackagePart</c>, nowhere near anything this project wrote, and only
    /// sometimes. Two classes built views and it stayed hidden; the third made
    /// it show up about one run in three.
    /// </remarks>
    private static readonly Lock OneAtATime = new();

    /// <summary>
    /// Renders an answer through <see cref="CapabilityView"/> and returns every
    /// line of text the result actually put on screen, top to bottom.
    /// </summary>
    internal static IReadOnlyList<string> LinesFor(Answer answer) =>
        Rendered(() => VisualTree.TextIn(LaidOut(answer)));

    /// <summary>
    /// The face every line of the rendered answer is actually set in, top to
    /// bottom, as the render itself asked for it.
    /// </summary>
    internal static IReadOnlyList<Face> FacesFor(Answer answer) =>
        Rendered(() => LabelsIn(LaidOut(answer))
            .Select(label => Face.Of(
                new Typeface(label.FontFamily, label.FontStyle, label.FontWeight, label.FontStretch)))
            .ToList());

    /// <summary>
    /// The color the render actually filled its surface with.
    /// </summary>
    internal static Color? BackgroundOf(Answer answer) =>
        Rendered(() => ColorOf(LaidOut(answer).Background));

    /// <summary>
    /// The color every line of the rendered answer is actually written in, top
    /// to bottom, as the render itself resolved it.
    /// </summary>
    internal static IReadOnlyList<Color?> ForegroundsFor(Answer answer) =>
        Rendered(() => LabelsIn(LaidOut(answer))
            .Select(label => ColorOf(label.Foreground))
            .ToList());

    /// <summary>
    /// The face the rendered answer's own font family resolves to at some other
    /// weight — what emphatic text will get when there is any.
    /// </summary>
    internal static Face FaceAt(Answer answer, FontWeight weight) =>
        Rendered(() =>
        {
            TextBlock label = LabelsIn(LaidOut(answer))[0];

            return Face.Of(
                new Typeface(label.FontFamily, label.FontStyle, weight, label.FontStretch));
        });

    /// <summary>
    /// Runs something on a thread WPF will talk to, and only one of those at a
    /// time.
    /// </summary>
    ///
    /// <remarks>
    /// The lock is held for the whole operation rather than around building the
    /// view alone. The embedded font is a second resource read out of the same
    /// package, and it is read while the tree is being measured rather than
    /// while it is being built.
    /// </remarks>
    /// <typeparam name="T">What the reading works out.</typeparam>
    /// <param name="reading">Building a tree and reading it back.</param>
    /// <returns>Whatever it worked out.</returns>
    private static T Rendered<T>(Func<T> reading)
    {
        lock (OneAtATime)
        {
            return StaThread.Run(reading);
        }
    }

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
    /// The one color a brush paints, or nothing at all when it does not paint
    /// one.
    /// </summary>
    ///
    /// <remarks>
    /// Nothing is a real answer rather than a failure, and it is the answer that
    /// matters: a brush that never resolved is null, and a null brush paints
    /// nothing without complaining. Reading the color off the built tree is the
    /// only version of the question that case can fail.
    /// </remarks>
    private static Color? ColorOf(Brush? brush) =>
        brush is SolidColorBrush solid ? solid.Color : null;

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
