using System.ComponentModel;
using System.Windows;

using D47.Capabilities;
using D47.Help;
using D47.Render;

namespace D47.Panel;

/// <summary>
/// The panel window. It hosts one <see cref="CapabilityView"/> and hands it the
/// answer to show; it does not know what is in that answer.
///
/// <para>
/// Help is what it shows, because help is the only capability that exists and
/// it needs no microphone, no network and no game. The registry is built here
/// for now — a composition root that knows about every capability arrives with
/// the second one.
/// </para>
/// </summary>
internal partial class MainWindow : Window
{
    /// <summary>
    /// Creates the window and shows help's top-level answer.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();

        var registry = new CapabilityRegistry(HelpCapability.Descriptor);

        View.DataContext = new Answer
        {
            Descriptor = HelpCapability.Descriptor,
            Result = new HelpCapability(registry).Answer(),
        };

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
