using System;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Input;
using System.Windows.Interop;

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
    /// Identifies the registration to the window that owns it. There is one per
    /// window here, so the number never has to be allocated.
    /// </summary>
    private const int TheOnlyOne = 1;



    private const uint Alt = 0x0001;
    private const uint Control = 0x0002;
    private const uint Shift = 0x0004;
    private const uint Windows = 0x0008;

    /// <summary>
    /// A top-level window with none of the bits that would put it on screen —
    /// no <c>WS_VISIBLE</c>, no anything. It exists only to be posted to.
    /// </summary>
    private const int Invisible = 0;

    private readonly HwndSource _messages;
    private readonly Action _pressed;
    private readonly RepeatGuard _held;
    private bool _disposed;

    private Hotkey(HwndSource messages, Action pressed, RepeatGuard held)
    {
        _messages = messages;
        _pressed = pressed;
        _held = held;
    }

    /// <summary>
    /// Claims a combination.
    /// </summary>
    ///
    /// <remarks>
    /// Must be called on a thread with a running message loop, because that is
    /// what dispatches the message Windows posts.
    /// </remarks>
    /// <param name="modifiers">The modifiers to hold.</param>
    /// <param name="key">The key to press.</param>
    /// <param name="pressed">What to do when it arrives.</param>
    /// <param name="time">
    /// Where now comes from, for telling a press apart from the same one still
    /// being held. Defaults to the system clock.
    /// </param>
    /// <returns>The registration, which the caller owns and must dispose.</returns>
    /// <exception cref="InvalidOperationException">
    /// The combination could not be claimed — most often because another
    /// application already owns it. Thrown rather than swallowed: a hotkey that
    /// silently does nothing is indistinguishable from one that is broken, and
    /// the Commander presses it repeatedly either way.
    /// </exception>
    internal static Hotkey Register(
        ModifierKeys modifiers,
        Key key,
        Action pressed,
        TimeProvider? time = null)
    {
        var messages = new HwndSource(
            new HwndSourceParameters("Directive 47 hotkeys") { WindowStyle = Invisible });

        var hotkey = new Hotkey(messages, pressed, new RepeatGuard(time ?? TimeProvider.System));
        messages.AddHook(hotkey.OnMessage);

        // Deliberately without MOD_NOREPEAT, which Windows offers for exactly
        // this and which was the first implementation. The flag also makes the
        // hotkey invisible to synthesized input — with it, an injected press
        // produces no message at all, not merely no repeats — so it bought
        // correct behaviour at the price of never being able to test that the
        // hotkey arrives. RepeatGuard does the same job where a test can see it.
        if (RegisterHotKey(
            messages.Handle,
            TheOnlyOne,
            ToNative(modifiers),
            KeyInterop.VirtualKeyFromKey(key)))
        {
            return hotkey;
        }

        int failure = Marshal.GetLastWin32Error();
        hotkey.Dispose();

        throw new InvalidOperationException(
            string.Create(
                CultureInfo.InvariantCulture,
                $"Could not claim {Describe(modifiers, key)}. Another application is most "
                + $"likely already using it."),
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

        if (_held.Allows())
        {
            _pressed();
        }

        return IntPtr.Zero;
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

    /// <summary>
    /// The combination, spelled the way a person writes it.
    /// </summary>
    ///
    /// <remarks>
    /// Built rather than taken from <c>ModifierKeys.ToString()</c>, which
    /// spells the flags in the enum's own order — "Alt, Control, Shift" — and
    /// would put the message's wording at the mercy of how somebody declared
    /// an enum.
    /// </remarks>
    /// <param name="modifiers">The modifiers held.</param>
    /// <param name="key">The key pressed.</param>
    /// <returns>Something like <c>Ctrl+Alt+D</c>.</returns>
    private static string Describe(ModifierKeys modifiers, Key key)
    {
        var described = new StringBuilder();

        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            described.Append("Ctrl+");
        }

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            described.Append("Alt+");
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            described.Append("Shift+");
        }

        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            described.Append("Win+");
        }

        return described.Append(key).ToString();
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
}
