using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace D47.Panel;

/// <summary>
/// One system-wide key combination, and what to do when it arrives.
///
/// <para>
/// <c>RegisterHotKey</c> rather than a low-level keyboard hook. Windows does the
/// matching and posts a single message; Directive 47 never sees any other
/// keystroke. The hook would serve push-to-talk's hold-and-release as well, but
/// it is also the API keyloggers use, inside an unsigned single-file exe, for a
/// feature that today needs one key press. Wave 2 can pay for that when it has
/// a reason to.
/// </para>
///
/// <para>
/// Lives in <c>D47.Panel</c> rather than a project of its own. It is Tier 1 —
/// registering a combination and receiving the message needs a desktop and
/// nothing else — and projects here split by tier, never by capability. The
/// revisit trigger is a second consumer that is not the application.
/// </para>
/// </summary>
internal sealed class Hotkey : IDisposable
{
    private const int WmHotkey = 0x0312;

    /// <summary>
    /// <c>ERROR_HOTKEY_ALREADY_REGISTERED</c>. The one failure that means the
    /// machine, rather than us.
    /// </summary>
    private const int AlreadySpokenFor = 1409;

    /// <summary>
    /// The high bit of what <c>GetAsyncKeyState</c> returns: the key is down
    /// right now.
    /// </summary>
    private const short Down = unchecked((short)0x8000);

    /// <summary>
    /// Identifies the registration to the window that owns it. There is one per
    /// window here, so the number never has to be allocated.
    /// </summary>
    private const int TheOnlyOne = 1;

    /// <summary>
    /// What <c>RegisterHotKey</c> calls the modifiers. Its own numbers, not
    /// WPF's <see cref="ModifierKeys"/>, which is why <see cref="ToNative"/>
    /// exists rather than a cast.
    /// </summary>
    private const uint Alt = 0x0001;
    private const uint Control = 0x0002;
    private const uint Shift = 0x0004;
    private const uint Windows = 0x0008;

    /// <summary>
    /// A top-level window with none of the bits that would put it on screen —
    /// no <c>WS_VISIBLE</c>, no anything. It exists only to be posted to.
    /// </summary>
    private const int Invisible = 0;

    /// <summary>
    /// How often to look at whether the key has come up yet.
    ///
    /// <para>
    /// Short enough that a Commander tapping the combination twice has let go
    /// well before the next look — a tap and its release take a person the best
    /// part of a tenth of a second, and this is a fifth of that. Long enough
    /// that holding the key down is not a busy loop.
    /// </para>
    /// </summary>
    private static readonly TimeSpan BetweenLooks = TimeSpan.FromMilliseconds(20);

    private readonly HwndSource _messages;
    private readonly Action _pressed;
    private readonly Action _released;
    private readonly HeldKey _held = new();
    private readonly int _virtualKey;
    private readonly DispatcherTimer _watchingForRelease;
    private bool _disposed;

    private Hotkey(HwndSource messages, Action pressed, Action released, int virtualKey)
    {
        _messages = messages;
        _pressed = pressed;
        _released = released;
        _virtualKey = virtualKey;

        _watchingForRelease = new DispatcherTimer { Interval = BetweenLooks };
        _watchingForRelease.Tick += (_, _) => LookForRelease();
    }

    /// <summary>
    /// Claims a combination.
    /// </summary>
    ///
    /// <remarks>
    /// Must be called on a thread with a running message loop, because that is
    /// what dispatches the message Windows posts.
    /// </remarks>
    /// <param name="combination">The combination to claim.</param>
    /// <param name="pressed">What to do when it arrives.</param>
    /// <param name="released">
    /// What to do when the key comes up, for a caller that holds the combination
    /// rather than tapping it. Omitted by the overlay's toggle, which cares only
    /// that the combination arrived.
    /// </param>
    /// <returns>
    /// The registration, which the caller owns and must dispose, or an absence
    /// carrying what to say about it when another application already owns the
    /// combination.
    ///
    /// <para>
    /// Absent for that one named reason and nothing else. Another application
    /// holding a key is a fact about the machine Directive 47 is running on,
    /// the same shape as a machine that cannot composite — so it is an absence
    /// to carry on from, not a failure to die of. Every other way this can fail
    /// throws, because a defect of ours reported as somebody else's hotkey is a
    /// defect nobody ever finds.
    /// </para>
    /// <para>
    /// The words come from here rather than from the caller, and that is the
    /// point of handing them back. The application used to write its own
    /// sentence naming <c>Ctrl</c>+<c>Alt</c>+<c>D</c> — a second copy of a
    /// value it had passed in, free to disagree with what was actually
    /// attempted. Described from the argument, it cannot.
    /// </para>
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="combination"/> is null.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The combination could not be claimed for any other reason.
    /// </exception>
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification =
            "Ownership passes to the caller, which is what the return documents and what "
            + "App and every test do. The analyzer recognised that while the registration "
            + "was handed back directly and stopped when it began travelling inside a "
            + "Perhaps, which it cannot see through. Both failure paths still dispose "
            + "explicitly before returning, and the successful one is the caller's from "
            + "the moment it is returned.")]
    internal static Perhaps<Hotkey> TryRegister(
        Combination combination, Action pressed, Action? released = null)
    {
        ArgumentNullException.ThrowIfNull(combination);

        var messages = new HwndSource(
            new HwndSourceParameters("Directive 47 hotkeys") { WindowStyle = Invisible });

        var hotkey = new Hotkey(
            messages,
            pressed,
            released ?? DoNothing,
            KeyInterop.VirtualKeyFromKey(combination.Key));

        messages.AddHook(hotkey.OnMessage);

        // Deliberately without MOD_NOREPEAT, which Windows offers for exactly
        // this and which was the first implementation. The flag also makes the
        // hotkey invisible to synthesized input — with it, an injected press
        // produces no message at all, not merely no repeats — so it bought
        // correct behaviour at the price of never being able to test that the
        // hotkey arrives. HeldKey does the same job where a test can see it.
        if (RegisterHotKey(
            messages.Handle,
            TheOnlyOne,
            ToNative(combination.Modifiers),
            KeyInterop.VirtualKeyFromKey(combination.Key)))
        {
            return Perhaps<Hotkey>.Of(hotkey);
        }

        int failure = Marshal.GetLastWin32Error();
        hotkey.Dispose();

        if (failure == AlreadySpokenFor)
        {
            return Perhaps<Hotkey>.Absent(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Could not claim {combination} — another application already "
                    + $"owns it. The overlay cannot be toggled by hotkey this session."));
        }

        throw new InvalidOperationException(
            string.Create(
                CultureInfo.InvariantCulture,
                $"Could not claim {combination}, and not because something else owns it."),
            new Win32Exception(failure));
    }

    /// <summary>
    /// Gives the combination back.
    /// </summary>
    ///
    /// <remarks>
    /// On the thread that claimed it. A registration belongs to the thread that
    /// made it, so unregistering from anywhere else quietly does nothing.
    /// </remarks>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _watchingForRelease.Stop();
        UnregisterHotKey(_messages.Handle, TheOnlyOne);
        _messages.RemoveHook(OnMessage);
        _messages.Dispose();
    }

    private IntPtr OnMessage(IntPtr window, int message, IntPtr key, IntPtr pressed, ref bool handled)
    {
        if (message != WmHotkey || key.ToInt32() != TheOnlyOne)
        {
            return IntPtr.Zero;
        }

        // Handled either way. A repeat is still ours, and letting it fall
        // through to whatever else is hooked would be handing on a message
        // nobody else asked for.
        handled = true;

        if (!_held.Allows())
        {
            return IntPtr.Zero;
        }

        _pressed();

        // Nothing tells us the key came up — RegisterHotKey reports presses and
        // nothing else — so the only way to know is to look. The watching stops
        // the moment it sees the key up, so this runs for as long as a finger
        // rests on a key and no longer.
        _watchingForRelease.Start();

        return IntPtr.Zero;
    }

    /// <summary>
    /// Ends the wait once the key is up, so the next press counts as one.
    /// </summary>
    ///
    /// <remarks>
    /// Only the key is watched, not the modifiers. Holding <c>Ctrl</c> and
    /// <c>Alt</c> down while tapping the key is two presses and should be,
    /// which is exactly how somebody toggles something twice in a hurry.
    /// </remarks>
    private void LookForRelease()
    {
        if ((GetAsyncKeyState(_virtualKey) & Down) != 0)
        {
            return;
        }

        _watchingForRelease.Stop();
        _held.LetGo();
        _released();
    }

    /// <summary>
    /// What a registration that only cares about the press does when the key
    /// comes up.
    ///
    /// <para>
    /// The overlay's toggle is exactly that, and giving it a callback to ignore
    /// would be asking every caller to say it is not interested. The watching for
    /// release still happens either way — it is what re-arms the claim — so this
    /// costs nothing beyond a call that returns.
    /// </para>
    /// </summary>
    private static void DoNothing()
    {
    }

    private static uint ToNative(ModifierKeys modifiers)
    {
        uint native = 0;

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            native |= Alt;
        }

        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            native |= Control;
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            native |= Shift;
        }

        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            native |= Windows;
        }

        return native;
    }

    // System32 rather than the default probing order: user32 is an operating
    // system library, and naming where it comes from is what stops a DLL of the
    // same name beside the executable being loaded instead.
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, int key);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr window, int id);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern short GetAsyncKeyState(int key);
}
