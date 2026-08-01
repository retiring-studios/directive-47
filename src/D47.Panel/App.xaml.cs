using System;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Resources;
using System.Windows.Threading;

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

    /// <summary>
    /// How long to let the shell finish closing its overflow flyout before
    /// asking for the foreground. Long enough to be after it, short enough that
    /// nobody perceives it as a delay.
    /// </summary>
    private static readonly TimeSpan ShellFinishesUp = TimeSpan.FromMilliseconds(300);

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

        // Left button only. The right one belongs to the menu, and MouseClick
        // reports which was pressed where Click does not.
        _trayIcon.MouseClick += (_, clicked) =>
        {
            if (clicked.Button == Forms.MouseButtons.Left)
            {
                RestorePanel();
            }
        };
    }

    /// <summary>
    /// Brings the panel back, from wherever it went.
    ///
    /// <para>
    /// The way back out of a one-way door. Deliberately not "show it if it is
    /// hidden": a minimized window is still visible as far as WPF is concerned,
    /// so that version does nothing at all for the case a Commander is most
    /// likely to hit, and they click the icon again and again.
    /// </para>
    ///
    /// <para>
    /// The same window, never a new one. Rebuilding it would look right and
    /// silently discard whatever state it was hidden with.
    /// </para>
    /// </summary>
    private void RestorePanel()
    {
        if (MainWindow is not { } panel)
        {
            return;
        }

        panel.Show();

        if (panel.WindowState == WindowState.Minimized)
        {
            panel.WindowState = WindowState.Normal;
        }

        // Deliberately after a pause, and deliberately not also immediately.
        // Asking straight away does not work and looks like it should: the
        // shell is still finishing its own interaction when the click reaches
        // us, so it takes the foreground back afterwards. Measured, not
        // guessed — with the immediate call alone, the foreground window after
        // a tray click was Shell_TrayWnd every time.
        IntPtr handle = new WindowInteropHelper(panel).Handle;
        var settled = new DispatcherTimer { Interval = ShellFinishesUp };

        settled.Tick += (_, _) =>
        {
            settled.Stop();
            TakeForeground(handle);
        };

        settled.Start();
    }

    /// <summary>
    /// Makes the window the foreground one, past the protection that normally
    /// stops a background process stealing focus.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// <c>Activate</c> and a plain <c>SetForegroundWindow</c> are both refused
    /// here. Windows grants the foreground to whoever received the last input,
    /// and the click went to the shell — measured, not assumed: when this was
    /// missing, the foreground window after a tray click was
    /// <c>Shell_TrayWnd</c>.
    /// </para>
    /// <para>
    /// Attaching to the foreground thread's input queue for the length of the
    /// call is the documented way through, and what tray applications generally
    /// do. It is a hack in the sense that it works around a protection, but the
    /// protection is against a background process stealing focus unbidden, and
    /// this is a Commander clicking the icon whose entire purpose is to bring
    /// the panel back.
    /// </para>
    /// </remarks>
    /// <param name="window">The window to bring to the front.</param>
    private static void TakeForeground(IntPtr window)
    {
        IntPtr inFront = GetForegroundWindow();
        uint holdingIt = GetWindowThreadProcessId(inFront, out _);
        uint ours = GetCurrentThreadId();

        if (inFront == IntPtr.Zero || holdingIt == ours)
        {
            SetForegroundWindow(window);
            return;
        }

        AttachThreadInput(ours, holdingIt, true);

        try
        {
            SetForegroundWindow(window);
        }
        finally
        {
            AttachThreadInput(ours, holdingIt, false);
        }
    }

    // System32 rather than the default probing order: user32 and kernel32 are
    // operating system libraries, and naming where they come from is what stops
    // a DLL of the same name beside the executable being loaded instead.
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachThreadInput(uint attaching, uint to, bool attach);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetCurrentThreadId();

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
