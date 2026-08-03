using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace D47.TestSupport;

/// <summary>
/// Reads back what a real control actually put on screen.
///
/// <para>
/// Asserting on a view model instead would prove the view model and nothing
/// about the rendering, which is the thing those tests exist to check. Both the
/// panel's render and the overlay's window are asked the same question, which is
/// why this was written twice before it was written here.
/// </para>
/// </summary>
public static class VisualTree
{
    /// <summary>
    /// Every line of text under an element that is actually on screen, top to
    /// bottom.
    /// </summary>
    ///
    /// <remarks>
    /// Visibility is respected, and it has to be. A collapsed element is still
    /// in the visual tree, so a walk that ignores it reports words nobody can
    /// see — which is what happened when the overlay's chrome arrived: the drag
    /// bar's own label turned up in what the overlay was said to be presenting,
    /// while the bar was collapsed and invisible.
    /// </remarks>
    /// <param name="root">Where to start looking.</param>
    /// <returns>The lines, in tree order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="root"/> is null.</exception>
    public static IReadOnlyList<string> TextIn(DependencyObject root)
    {
        ArgumentNullException.ThrowIfNull(root);

        List<string> lines = [];
        Collect(root, lines);

        return lines;
    }

    /// <summary>
    /// Walks down, stopping wherever the screen would.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// Asked of <see cref="UIElement.Visibility"/> rather than of
    /// <c>IsVisible</c>, which is the obvious property and the wrong one here.
    /// <c>IsVisible</c> is false for anything not connected to a presentation
    /// source, and the panel's render is laid out on its own without ever being
    /// put in a window — so that version reports nothing at all and every test
    /// using it passes by finding no text to disagree with. The two copies of
    /// this walk differed in exactly this, and only the one running against a
    /// real shown window could afford the simpler check.
    /// </para>
    /// <para>
    /// Whole subtrees are skipped rather than each element judged on its own,
    /// because that is what a collapsed parent does to its children on screen.
    /// A per-element check would report a visible label inside an invisible
    /// panel.
    /// </para>
    /// </remarks>
    private static void Collect(DependencyObject node, List<string> lines)
    {
        int children = VisualTreeHelper.GetChildrenCount(node);

        for (int child = 0; child < children; child++)
        {
            DependencyObject descendant = VisualTreeHelper.GetChild(node, child);

            if (descendant is UIElement { Visibility: not Visibility.Visible })
            {
                continue;
            }

            if (descendant is TextBlock block)
            {
                lines.Add(block.Text);
            }

            Collect(descendant, lines);
        }
    }
}
