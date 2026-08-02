using System.ComponentModel;
using System.Windows;

using D47.Render;

namespace D47.Panel;

/// <summary>
/// The panel window. It hosts one <see cref="CapabilityView"/> and hands it the
/// answer to show; it does not know what is in that answer, and it no longer
/// decides what the answer is.
///
/// <para>
/// Composing that answer moved to <see cref="App"/> when the game overlay
/// arrived. Two surfaces showing the same answer means one object shown twice,
/// and a window that builds its own is a window that can disagree with the
/// overlay about what the Commander asked.
/// </para>
/// </summary>
internal partial class MainWindow : Window
{
    /// <summary>
    /// Creates the window around an answer to show.
    /// </summary>
    /// <param name="answer">What to render.</param>
    public MainWindow(Answer answer)
    {
        InitializeComponent();

        View.DataContext = answer;

        Closing += HideInsteadOfClosing;
    }

    /// <summary>
    /// Turns the close control into a hide.
    /// </summary>
    ///
    /// <remarks>
    /// The panel is convenience; the voice loop is the product. Closing the
    /// convenience must not take the product down with it, so the window goes
    /// away and the application does not. Exiting on purpose is the tray icon's
    /// menu, which is a different gesture rather than the same one.
    /// </remarks>
    /// <param name="sender">The window being closed.</param>
    /// <param name="e">The cancellable close.</param>
    private void HideInsteadOfClosing(object? sender, CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}
