using System.Collections.Generic;
using System.Windows;

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
        StaThread.Run(() =>
        {
            var view = new CapabilityView { DataContext = answer };

            view.Measure(Surface);
            view.Arrange(new Rect(Surface));
            view.UpdateLayout();

            return VisualTree.TextIn(view);
        });
}
