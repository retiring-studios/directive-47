using System;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Resources;

using Forms = System.Windows.Forms;

namespace D47.Panel;

/// <summary>
/// The application. It owns the notification-area icon, and that ownership is
/// the point rather than an implementation detail: the icon has to outlive
/// every window, because with the panel and both overlays out of sight it is
/// the only evidence Directive 47 is running at all.
///
/// <para>
/// The icon comes from WinForms, aliased here rather than imported, because a
/// bare <c>using System.Windows.Forms</c> would make <c>Application</c>
/// ambiguous in a file whose base class is called <c>Application</c>.
/// </para>
///
/// <para>
/// First-run setup and diagnostics still live in their own stories.
/// </para>
/// </summary>
internal sealed partial class App : Application, IDisposable
{
    /// <summary>
    /// The icon's tooltip, and so also its accessible name — an icon without
    /// one is anonymous to automation and to a screen reader alike.
    /// </summary>
    private const string Tooltip = "Directive 47";

    /// <summary>
    /// The one deliberate way out. Closing the panel hides it, so exiting has
    /// to be a different gesture or the two become the same reflex.
    /// </summary>
    private const string ExitItem = "Exit";

    private Forms.NotifyIcon? _trayIcon;
    private Forms.ContextMenuStrip? _trayMenu;

    /// <summary>
    /// Puts the icon in the notification area, before any window is shown.
    /// </summary>
    /// <param name="e">The startup arguments.</param>
    [SuppressMessage(
        "Globalization",
        "CA1303:Do not pass literals as localized parameters",
        Justification =
            "NotifyIcon.Text and the menu item's text are marked localizable. The "
            + "product's name is not, and \"Exit\" is a single word behind a "
            + "resource table nobody has decided to build — localizing this "
            + "application at all is an open question, and a table holding two "
            + "strings would be machinery standing in for that decision.")]
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _trayMenu = new Forms.ContextMenuStrip();
        _trayMenu.Items.Add(ExitItem, null, (_, _) => Shutdown());

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = TrayIcon(),
            Text = Tooltip,
            ContextMenuStrip = _trayMenu,
            Visible = true,
        };
    }

    /// <summary>
    /// Takes the icon away again. Without this the shell keeps drawing it until
    /// something happens to hover over it, which is the ghost icon everyone has
    /// seen and nobody wants to be the cause of.
    /// </summary>
    /// <param name="e">The exit arguments.</param>
    protected override void OnExit(ExitEventArgs e)
    {
        Dispose();
        base.OnExit(e);
    }

    /// <summary>
    /// Removes the icon and releases it.
    /// </summary>
    public void Dispose()
    {
        if (_trayIcon is not null)
        {
            // Visible first. Disposing alone does remove it, but only once the
            // native handle is actually released, and the window between the
            // two is exactly where a ghost lives.
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }

        // Separately, because NotifyIcon does not own the menu it shows.
        _trayMenu?.Dispose();
        _trayMenu = null;
    }

    /// <summary>
    /// The icon, at whatever size this machine draws notification-area icons.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The icon was not built into the application.
    /// </exception>
    private static Icon TrayIcon()
    {
        StreamResourceInfo resource =
            GetResourceStream(new Uri("assets/directive-47.ico", UriKind.Relative))
            ?? throw new InvalidOperationException(
                "assets/directive-47.ico is missing from the application's resources.");

        using Stream stream = resource.Stream;

        return new Icon(stream, Forms.SystemInformation.SmallIconSize);
    }
}
