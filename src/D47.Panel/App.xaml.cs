using System;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Resources;
using System.Windows.Threading;

using D47.Data;

using D47.Composition;
using D47.GameOverlay;
using D47.Help;
using D47.Render;
using D47.VrOverlay;

using Forms = System.Windows.Forms;

namespace D47.Panel;

/// <summary>
/// The application. It owns the notification-area icon, and that ownership is
/// the point rather than an implementation detail: the icon has to outlive
/// every window, because with the panel and both overlays out of sight it is
/// the only evidence Directive 47 is running at all.
///
/// <para>
/// It is also the composition root, which it became when the game overlay
/// turned out to be a second surface for the same answer. The panel used to
/// build its own; two surfaces building their own would be two answers that can
/// disagree.
/// </para>
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
    /// The product's name, wherever Windows shows it back to the Commander.
    ///
    /// <para>
    /// It is the icon's tooltip, and so also the icon's accessible name — one
    /// without a tooltip is anonymous to automation and to a screen reader
    /// alike. It is also the caption of the dialog a second copy puts up, where
    /// anything else would be a window nobody recognizes.
    /// </para>
    /// </summary>
    private const string ProductName = "Directive 47";

    /// <summary>
    /// What a second copy says before it goes.
    ///
    /// <para>
    /// It says something rather than exiting quietly, because silence is the
    /// failure this was filed for: a duplicate launch that just runs produces
    /// two tray icons, two stacked overlays and a hotkey that appears dead, and
    /// nothing anywhere says why. The second sentence is there because the
    /// running copy may have no window on screen at all — the icon is the only
    /// sign of it, and somebody who did not know that has just proved it by
    /// starting the application again.
    /// </para>
    /// </summary>
    private const string AlreadyRunning =
        "Directive 47 is already running.\n\n"
        + "Look for its icon in the notification area, beside the clock.";

    /// <summary>
    /// The one deliberate way out. Closing the panel hides it, so exiting has
    /// to be a different gesture or the two become the same reflex.
    /// </summary>
    private const string ExitItem = "Exit";
    private const string SettingsItem = "Settings";

    /// <summary>
    /// Where a newer Directive 47 comes from: this project's own releases.
    /// </summary>
    private const string Releases = "https://github.com/retiring-studios/directive-47";

    /// <summary>
    /// Whether a release marked prerelease counts as an update.
    ///
    /// <para>
    /// True for now, and it has a revisit trigger. Everything before 1.0.0 is
    /// published as a prerelease, so an updater ignoring them has nothing to
    /// find until the MVP ships and the whole feature is inert in the meantime.
    /// Once 1.0.0 is out, "latest" starts meaning something somebody stood
    /// behind, and that is the moment this should become false.
    /// </para>
    /// </summary>
    private const bool Prereleases = true;

    /// <summary>
    /// How long to let the shell finish closing its overflow flyout before
    /// asking for the foreground. Long enough to be after it, short enough that
    /// nobody perceives it as a delay.
    /// </summary>
    private static readonly TimeSpan ShellFinishesUp = TimeSpan.FromMilliseconds(300);

    /// <summary>
    /// Where everything that carried on without something writes it down.
    ///
    /// <para>
    /// Built first and handed to everything that composes, rather than reached
    /// for as a static. What records an absence is then visible in every
    /// signature that can produce one, which is what makes forgetting to pass it
    /// a compile error instead of a habit.
    /// </para>
    /// </summary>
    private readonly Log _log = Log.InApplicationData();

    private SingleInstance? _theOnlyCopy;
    private Forms.NotifyIcon? _trayIcon;
    private Forms.ContextMenuStrip? _trayMenu;
    private SettingsStore? _settings;
    private MainWindow? _panel;
    private Zoom? _zoom;
    private Overlay? _overlay;
    private Headset? _headset;

    /// <summary>
    /// The voice loop and everything it holds — the microphone, the turn, the
    /// bus it reports on, the log listening to that bus, and the cues.
    /// </summary>
    ///
    /// <remarks>
    /// One field rather than five. See <see cref="Conversation"/> for why: they
    /// arrived one story at a time and the composition root was accumulating a
    /// field and a disposal for each.
    /// </remarks>
    private Conversation? _conversation;

    private ChosenHotkey? _hotkey;
    private PushToTalk? _pushToTalk;
    private IGrabChrome? _chrome;
    private Grabbing? _grabbing;
    private ForegroundWatcher? _foreground;
    private Updates? _updates;

    /// <summary>
    /// Claims the right to be the running copy, and then puts the icon in the
    /// notification area, before any window is shown.
    ///
    /// <para>
    /// Not the first thing that runs any more. <see cref="Program"/> is, and the
    /// updater's own errands are handled and exited there — before there is an
    /// application to be the only copy of.
    /// </para>
    /// </summary>
    /// <param name="e">The startup arguments.</param>
    [SuppressMessage(
        "Globalization",
        "CA1303:Do not pass literals as localized parameters",
        Justification =
            "NotifyIcon.Text, the menu item's text, and MessageBox's text and caption are all "
            + "marked localizable. The product's name is not, and the rest is \"Exit\" plus two "
            + "sentences behind a resource table nobody has decided to build — localizing this "
            + "application at all is an open question, and a table holding three strings would "
            + "be machinery standing in for that decision.")]
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // First, and before anything that would be visible. Everything below
        // this line is a thing a second copy must not do — an icon it put up on
        // the way to saying goodbye is the second icon this exists to prevent.
        _theOnlyCopy = SingleInstance.TryClaim();

        if (_theOnlyCopy is null)
        {
            MessageBox.Show(
                AlreadyRunning, ProductName, MessageBoxButton.OK, MessageBoxImage.Information);

            Shutdown();
            return;
        }

        _trayMenu = new Forms.ContextMenuStrip();
        _trayMenu.Items.Add(SettingsItem, null, (_, _) => ShowSettings());
        _trayMenu.Items.Add(ExitItem, null, (_, _) => Shutdown());

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = TrayIcon(),
            Text = ProductName,
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

        // Before the surfaces, because what they show includes whether there is
        // a newer version and a surface built without one would have to be told
        // afterwards. Three surfaces being told afterwards is three chances for
        // one of them to miss it.
        //
        // Which means the check happens before the panel appears. It is a
        // network call, so on an installed copy that is however long GitHub
        // takes — see the pull request; making it not block startup is real work
        // and is not this story.
        _updates = Updates.From(new VelopackUpdates(Releases, Prereleases), _log.Warning);
        _updates.Look();

        var presented = new Presentation
        {
            Answer = Compose(),
            UpdateWaiting = _updates.Waiting,
        };

        // One store, opened once and shared. Two would be two copies of the
        // same file in memory, and the moment either of them writes, the other
        // is holding what the file used to say.
        //
        // Before the panel rather than after it, which it used to be. The panel
        // opens at the size it was last left drawing at, so the zoom has to have
        // been read before there is a window to apply it to — a window built
        // first would open at life size and jump.
        var remembered = DataStore.Open(_log.Warning);

        // The second file, and the migration between them. Settings used
        // to live in the one above, so a build installed before this ran
        // has a hotkey, an opacity and a zoom sitting in the wrong place —
        // and every run calls this, because the one that matters is
        // whichever run happens to be the first after the upgrade.
        var settings = SettingsStore.Open(Settings.Known, _log.Warning);

        SettingsStore.MigrateFrom(remembered, settings, Settings.Known);

        // Explicitly, rather than through StartupUri, because the window now
        // takes what it shows as an argument and StartupUri can only call a
        // parameterless constructor.
        var zoom = new Zoom(settings, _log.Warning);
        var panel = new MainWindow(presented, zoom, _updates);

        MainWindow = panel;

        _settings = settings;
        _zoom = zoom;
        _panel = panel;

        panel.SettingsAsked += ShowSettings;
        MainWindow.Show();

        _overlay = Overlay.From(
            new GameOverlayFactory(), presented, remembered, settings, _log.Warning);
        _overlay.Show();

        // Asked on every machine, most of which have no SteamVR. Whether there
        // is a runtime to put a quad in has nothing to do with whether the game
        // or the desktop can host the other surfaces, so it is asked here beside
        // them rather than behind a condition — and a machine without one writes
        // down that it had none and carries on.
        //
        // On this thread, which is the one WPF gave us and is single-threaded
        // apartment. Building the render to get pixels out of it needs that, and
        // a factory called from anywhere else would fail for a reason nothing
        // here would explain.
        _headset = Headset.From(new HeadsetOverlayFactory(), presented, _log.Warning);
        _headset.Show();

        WatchForHands();
        FollowTheForeground();
        ClaimTheHotkey(settings);
        ClaimPushToTalk(settings);
    }

    /// <summary>
    /// Puts the grab chrome around the headset overlay and keeps it showing what
    /// the Commander's hands are on.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// Only where there is a quad to frame. No SteamVR means no headset overlay,
    /// which means nothing to point at — so this is absent for the same reason
    /// and does not need its own sentence about it.
    /// </para>
    /// <para>
    /// The panel's quad comes from the overlay rather than being worked out
    /// again here. The chrome has to line up with what the compositor actually
    /// drew, and a second derivation is the thing that would silently stop
    /// matching.
    /// </para>
    /// </remarks>
    private void WatchForHands()
    {
        if (_headset?.Placed is not { } panel)
        {
            return;
        }

        _chrome = HeadsetOverlayFactory.Around(panel);

        if (_chrome is null)
        {
            return;
        }

        _grabbing = Grabbing.Watching(_chrome, _log.Warning);
    }

    /// <summary>
    /// Keeps the overlay out of the mouse's way while Elite is in front, and in
    /// it when Elite is not.
    /// </summary>
    private void FollowTheForeground()
    {
        // Asked once before anything changes. The game is usually already in
        // front when Directive 47 starts — it has to be running for there to be
        // an overlay at all — and waiting for a change would leave the overlay
        // eating clicks until the Commander alt-tabbed for some other reason.
        _overlay?.ForegroundIsNow(ForegroundWatcher.InFrontNow());

        _foreground = ForegroundWatcher.Watch(window => _overlay?.ForegroundIsNow(window));
    }

    /// <summary>
    /// Claims whichever combination the Commander chose, and carries on without
    /// it if something else already has it.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// No combination appears here any more. Which one it is comes out of the
    /// store, defaulting to the <c>Ctrl</c>+<c>Alt</c>+<c>D</c> the application
    /// has always used, and changing it is
    /// <see cref="ChosenHotkey.Rebind"/> rather than a restart.
    /// </para>
    /// <para>
    /// Losing the combination is still not worth refusing to start over — the
    /// overlay is shown at startup, the panel works, and voice will eventually
    /// toggle it too. It is recorded, because an absence nobody wrote down is
    /// indistinguishable from a defect, and a Commander pressing a key that
    /// does nothing has no other way to find out why.
    /// </para>
    /// <para>
    /// On the UI thread, which is where the message loop that dispatches
    /// <c>WM_HOTKEY</c> is. A registration belongs to the thread that made it,
    /// so everything done to it afterwards has to come back here.
    /// </para>
    /// </remarks>
    /// <summary>
    /// Shows the settings page, or puts it away if it is already up.
    /// </summary>
    ///
    /// <remarks>
    /// One way in, called by the gear in the panel's chrome and by the tray
    /// menu, and the door a voice command joins when there is a voice loop to
    /// say it into — see
    /// [#18](https://github.com/retiring-studios/directive-47/issues/18)'s
    /// <c>Setting_WhenChangedByVoice_IsVisibleInThePanel</c>. Three routes to
    /// the same page is fine; three implementations of showing it is not.
    /// </remarks>
    private void ShowSettings()
    {
        if (_panel is not { } panel || _settings is not { } settings)
        {
            return;
        }

        panel.Show();

        panel.Showing(panel.ShowingSettings
            ? null
            : SettingsPage.For(settings, Apply));
    }

    /// <summary>
    /// What a changed setting means to an application that is already running.
    /// </summary>
    ///
    /// <remarks>
    /// Here rather than on the store or the page, because this is the one place
    /// that holds every consumer. The store keeps no list of who cares and the
    /// page knows nothing about a zoom or an overlay; both would have to learn
    /// it, and it is already known here.
    ///
    /// <para>
    /// Written down first, then applied. A change that took effect and was not
    /// kept is a setting that reverts on the next launch for no reason the
    /// Commander can see.
    /// </para>
    /// </remarks>
    /// <param name="setting">Which setting changed.</param>
    /// <param name="value">What it changed to.</param>
    private void Apply(string setting, string value)
    {
        if (_settings is not { } settings)
        {
            return;
        }

        if (value.Length == 0)
        {
            settings.Forget(setting);
        }
        else
        {
            settings.Write(setting, value);
        }

        if (setting == Zoom.HowBig)
        {
            _zoom?.SaidToBe(value, _log.Warning);
        }
        else if (setting == Overlay.HowSeeThrough)
        {
            _overlay?.SeeThroughSaidToBe(value, _log.Warning);
        }
        else if (setting == ChosenHotkey.WhatItIsCalled
            && Combination.TryParse(value, out Combination? wanted))
        {
            // Unwrapping is what writes the reason down, so a combination
            // somebody else already owns leaves a mark rather than nothing. The
            // hotkey in force does not change, which is the right answer: the
            // Commander still has the one that works.
            _hotkey?.Rebind(wanted).Or(_log.Warning);
        }
    }

    /// <param name="settings">What the Commander chose.</param>
    private void ClaimTheHotkey(SettingsStore settings) =>
        _hotkey = ChosenHotkey.From(settings, _log.Warning, () => _overlay?.Toggle());

    /// <summary>
    /// Claims the combination the Commander holds to talk, and builds the turn
    /// that holding it runs.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// Stand-ins behind all four contracts, which is what stage B of
    /// [#1](https://github.com/retiring-studios/directive-47/issues/1) is: the
    /// shape wired up and observable a wave before there is a microphone, a
    /// model or a voice. Each of the three stories that follow is a swap here
    /// and nothing else.
    /// </para>
    /// <para>
    /// Losing the combination costs the same as losing the overlay's and is
    /// handled the same way — recorded, and the application carries on. Every
    /// other surface still works, and a Commander pressing a dead key has no
    /// other way to find out why.
    /// </para>
    /// </remarks>
    /// <param name="settings">What the Commander chose.</param>
    private void ClaimPushToTalk(SettingsStore settings)
    {
        _conversation = Conversation.Assembled(_log.Warning);

        _pushToTalk = PushToTalk.From(
            settings, _log.Warning, () => _conversation?.Held(), RunTheTurn);
    }

    /// <summary>
    /// Runs the rest of the turn, off the thread the key came up on.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// The release arrives on the UI thread, because that is where the message
    /// loop is, and "no stage blocks the UI thread" is an invariant of
    /// [#7](https://github.com/retiring-studios/directive-47/issues/7). So the
    /// turn is started rather than awaited, and what would have been an
    /// unobserved exception is written down instead.
    /// </para>
    /// <para>
    /// The catch is now a backstop rather than the mechanism.
    /// <c>Turn.Released</c> handles a provider falling over itself — it publishes
    /// <c>Failed</c> and returns to Idle, and the subscription in
    /// <see cref="ClaimPushToTalk"/> is what writes that down. What is left here
    /// catches anything the turn could throw around the outside of that, which
    /// should be nothing and is not worth ending the application over if it ever
    /// stops being nothing.
    /// </para>
    /// </remarks>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification =
            "Nothing is above this to catch anything. The turn runs on a task nobody awaits, "
            + "so an exception that escapes is unobserved rather than handled, and by stage C "
            + "the four contracts reach a microphone, a network and somebody else's service. "
            + "None of those failing is worth ending the application over.")]
    private void RunTheTurn()
    {
        if (_conversation is not { } conversation)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await conversation.Released(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception failed)
            {
                _log.Warning($"The turn ended badly: {failed.Message}");
            }
        });
    }

    /// <summary>
    /// What every surface shows.
    ///
    /// <para>
    /// Help, because help is the only capability that exists and it needs no
    /// microphone, no network and no game. Which capabilities there are is
    /// <see cref="Composed.Capabilities"/>'s to say, not this method's — this
    /// used to construct its own registry, and so did the test fixture and the
    /// parity test, and the three agreed only by coincidence.
    /// </para>
    /// </summary>
    private static Answer Compose() =>
        new()
        {
            Descriptor = HelpCapability.Descriptor,
            Result = new HelpCapability(Composed.Capabilities).Answer(),
        };


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
        // First, and before anything is torn down. The updater is told to wait
        // for this process and then apply, so the earlier in the shutdown it is
        // told the more of its minute is left — and it does nothing at all
        // unless the Commander accepted something.
        _updates?.Exiting();

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

        // Gives the combination back. Windows would release it when the process
        // ends anyway; doing it here is what keeps a crash-free exit from
        // leaving anything behind.
        _hotkey?.Dispose();
        _hotkey = null;

        _pushToTalk?.Dispose();
        _pushToTalk = null;

        // After the key that drives it, so nothing can start a turn against a
        // loop that is being given back. What is inside it and in what order is
        // the loop's own business.
        _conversation?.Dispose();
        _conversation = null;

        // The hook holds a pointer to a callback of ours. Leaving it installed
        // past our own lifetime is how a shutdown turns into a crash.
        _foreground?.Dispose();
        _foreground = null;

        // Before the quads, and it waits for its thread. A watcher still running
        // would be painting a chrome overlay that had just been given back to
        // the compositor.
        _grabbing?.Dispose();
        _grabbing = null;

        // The quads are the compositor's, and they outlive us unless they are
        // given back — rectangles left floating in somebody's cockpit with
        // nothing running to close them, and a slot held in SteamVR's list of
        // applications. Both of them, because the chrome is a second overlay.
        _chrome?.Dispose();
        _chrome = null;

        _headset?.Dispose();
        _headset = null;

        // Last, so that nothing another copy would collide with is still up
        // when the name becomes claimable again.
        _theOnlyCopy?.Dispose();
        _theOnlyCopy = null;
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

        // The file's 128 and 256 frames are PNG-compressed, and GDI+ cannot read
        // those — Icon.ToBitmap throws ArgumentOutOfRangeException on one rather
        // than drawing a blank. SmallIconSize grows with display scaling, so past
        // 400% this asks for more than 64 and selects one of them.
        //
        // No clamp, deliberately. NotifyIcon hands Shell_NotifyIcon the HICON,
        // and Windows builds that, decoding PNG frames perfectly well; nothing
        // here goes near GDI+. Guarding a path this does not take would be code
        // for a failure that cannot happen. ApplicationIconTests measures both
        // halves of that claim so it stops being a claim.
        return new Icon(stream, Forms.SystemInformation.SmallIconSize);
    }
}
