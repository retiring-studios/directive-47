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

    private Forms.NotifyIcon? _trayIcon;

    /// <summary>
    /// Puts the icon in the notification area, before any window is shown.
    /// </summary>
    /// <param name="e">The startup arguments.</param>
    [SuppressMessage(
        "Globalization",
        "CA1303:Do not pass literals as localized parameters",
        Justification =
            "NotifyIcon.Text is marked localizable and the product's name is not. "
            + "\"Directive 47\" reads the same in every language, and a resource "
            + "table holding one proper noun would be machinery standing in for a "
            + "decision nobody has made about localizing this application at all.")]
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = TrayIcon(),
            Text = Tooltip,
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
        if (_trayIcon is null)
        {
            return;
        }

        // Visible first. Disposing alone does remove it, but only once the
        // native handle is actually released, and the window between the two
        // is exactly where a ghost lives.
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _trayIcon = null;
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
