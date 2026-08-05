using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

using D47.Data;
using D47.Panel;
using D47.Render;
using D47.TestSupport;

namespace D47.Tier1.Tests.Panel;

/// <summary>
/// A panel actually on the screen, asked something, and taken away again.
///
/// <para>
/// Shown rather than merely built, which is what makes the answers worth having:
/// an unshown window has the size somebody asked for and no client area, so
/// every question put to one is answered by the number the code wrote down. One
/// small window, opened without taking the foreground and gone before the test
/// returns.
/// </para>
/// </summary>
internal static class OpenedPanel
{
    /// <summary>
    /// Opens a panel around help's answer with a zoom of its own, asks it
    /// something, and closes it.
    /// </summary>
    ///
    /// <remarks>
    /// The zoom is handed back rather than kept, because most of what is worth
    /// asking is what the window did after it moved. Closing goes through the
    /// dispatcher rather than through <c>Close</c>: the panel turns a close into
    /// a hide, so a test that called it would leave the window behind.
    /// </remarks>
    /// <typeparam name="T">Whatever the asking works out.</typeparam>
    /// <param name="asking">What to ask of the open panel.</param>
    /// <returns>Whatever the asking worked out.</returns>
    internal static T Opened<T>(Func<MainWindow, Zoom, T> asking)
    {
        using var folder = new TemporaryStore();
        string file = folder.File;

        return StaThread.Run(() =>
        {
            var zoom = new Zoom(
                    SettingsStore.OpenAt(file, Settings.Known, Ignored), Ignored);
            var panel = new MainWindow(
                    Presentation.Of(Fixtures.HelpsAnswer()),
                    zoom,
                    Updates.From(new UpdateSources.Offering(), Ignored))
                {
                    ShowActivated = false,
                };

            try
            {
                panel.Show();

                return asking(panel, zoom);
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
    }

    /// <summary>
    /// How much room the render was given, read off the window's own tree rather
    /// than worked back from the window's size.
    /// </summary>
    /// <param name="panel">The open panel.</param>
    /// <returns>The size the render was laid out at.</returns>
    internal static Size RoomTheRenderGot(MainWindow panel)
    {
        CapabilityView render = Descendant<CapabilityView>(panel)
            ?? throw new InvalidOperationException("The panel is showing no render.");

        return new Size(render.ActualWidth, render.ActualHeight);
    }

    /// <summary>
    /// How much of the window the first line of the render actually covers.
    /// </summary>
    ///
    /// <remarks>
    /// <c>TransformToAncestor</c> rather than <c>ActualWidth</c>, because the
    /// scale belongs to the transform above the line and computing it here would
    /// be asserting the number the test exists to check. The same reasoning, and
    /// the same shape, as the overlay's own scaling test.
    /// </remarks>
    /// <param name="panel">The open panel.</param>
    /// <returns>The size the first line covers, in the window's own coordinates.</returns>
    internal static Size LineOnTheWindow(MainWindow panel)
    {
        // Inside the render, not inside the window. The zoom strip along the
        // bottom has a label of its own and is declared first, so a walk from
        // the window measures the strip saying "100%" and reports that the zoom
        // did nothing.
        CapabilityView render = Descendant<CapabilityView>(panel)
            ?? throw new InvalidOperationException("The panel is showing no render.");

        TextBlock line = Descendant<TextBlock>(render)
            ?? throw new InvalidOperationException(
                "The render is showing no text, so there is nothing to have scaled.");

        return line
            .TransformToAncestor(panel)
            .TransformBounds(new Rect(0, 0, line.ActualWidth, line.ActualHeight))
            .Size;
    }

    /// <summary>
    /// How much of the window the whole render actually covers.
    /// </summary>
    ///
    /// <remarks>
    /// Not the same question as <see cref="RoomTheRenderGot"/>, and the
    /// difference is the whole of the zoom. A control under a layout transform
    /// reports the size it was laid out at, which is the size <em>before</em> the
    /// scale — so a render drawn to fill a 600 pixel window at 300% honestly
    /// reports 200. This is what reaches the screen.
    /// </remarks>
    /// <param name="panel">The open panel.</param>
    /// <returns>The size the render covers, in the window's own coordinates.</returns>
    internal static Size RenderOnTheWindow(MainWindow panel)
    {
        CapabilityView render = Descendant<CapabilityView>(panel)
            ?? throw new InvalidOperationException("The panel is showing no render.");

        return render
            .TransformToAncestor(panel)
            .TransformBounds(new Rect(0, 0, render.ActualWidth, render.ActualHeight))
            .Size;
    }

    /// <summary>
    /// The scrolling region the render sits in.
    /// </summary>
    /// <param name="panel">The open panel.</param>
    /// <returns>The region.</returns>
    internal static ScrollViewer ScrollingRegion(MainWindow panel) =>
        Descendant<ScrollViewer>(panel)
            ?? throw new InvalidOperationException(
                "The panel has nothing to scroll with, so anything that does not fit is simply "
                + "not visible.");

    /// <summary>
    /// The first element of a kind under something, or nothing at all.
    /// </summary>
    /// <typeparam name="T">The kind being looked for.</typeparam>
    /// <param name="node">Where to start looking.</param>
    /// <returns>The first match, or <see langword="null"/>.</returns>
    internal static T? Descendant<T>(DependencyObject node)
        where T : DependencyObject
    {
        int children = VisualTreeHelper.GetChildrenCount(node);

        for (int child = 0; child < children; child++)
        {
            DependencyObject descendant = VisualTreeHelper.GetChild(node, child);

            if (descendant is T found)
            {
                return found;
            }

            if (Descendant<T>(descendant) is { } deeper)
            {
                return deeper;
            }
        }

        return null;
    }

    private static void Ignored(string complaint)
    {
        // A store in a folder of its own has nothing to complain about, and a
        // test that asserted on the silence would be asserting the absence of a
        // message it never asked for.
    }
}
