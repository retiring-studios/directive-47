using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace D47.Panel;

/// <summary>
/// Says which window Windows has just put in front.
///
/// <para>
/// A hook rather than a timer. Windows announces this the moment it happens;
/// asking repeatedly would be a poll that is either slower than an alt-tab or
/// busy for no reason, and the answer matters within one frame of a Commander
/// clicking back into the game.
/// </para>
///
/// <para>
/// Lives in <c>D47.Panel</c> for the same reason <see cref="Hotkey"/> does. It
/// is Tier 1 — a desktop and nothing else — and projects here split by tier
/// rather than by capability.
/// </para>
/// </summary>
[SuppressMessage(
    "Usage",
    "CA2216:Disposable types should declare finalizer",
    Justification =
        "A finalizer would unhook from the finalizer thread, and UnhookWinEvent has to be "
        + "called from the thread that installed the hook. So the finalizer this rule asks for "
        + "would be wrong rather than merely unnecessary. Windows releases the hook when the "
        + "process exits, which is the only path Dispose does not already cover.")]
internal sealed class ForegroundWatcher : IDisposable
{
    private const uint ForegroundChanged = 0x0003;

    /// <summary>
    /// The callback is ours and runs on our own message loop, rather than being
    /// injected into whichever process happens to come forward.
    /// </summary>
    private const uint OutOfContext = 0x0000;

    private readonly Action<IntPtr> _changed;

    /// <summary>
    /// Held deliberately. Windows keeps only a native function pointer, so a
    /// delegate that exists nowhere else is collected and the hook starts
    /// calling into freed memory — the classic way this API goes wrong, and it
    /// fails long after the mistake rather than at it.
    /// </summary>
    private readonly WinEventProc _callback;

    private IntPtr _hook;
    private bool _disposed;

    private ForegroundWatcher(Action<IntPtr> changed)
    {
        _changed = changed;
        _callback = OnWinEvent;
    }

    /// <summary>
    /// Starts watching.
    /// </summary>
    ///
    /// <remarks>
    /// Must be called on a thread with a running message loop, because an
    /// out-of-context hook is delivered through it.
    /// </remarks>
    /// <param name="changed">What to do when the foreground changes.</param>
    /// <returns>The watch, which the caller owns and must dispose.</returns>
    /// <exception cref="InvalidOperationException">Windows refused the hook.</exception>
    internal static ForegroundWatcher Watch(Action<IntPtr> changed)
    {
        var watcher = new ForegroundWatcher(changed);

        watcher._hook = SetWinEventHook(
            ForegroundChanged,
            ForegroundChanged,
            IntPtr.Zero,
            watcher._callback,
            0,
            0,
            OutOfContext);

        if (watcher._hook == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                "Windows would not hook the foreground event, so the overlay cannot know when "
                + "Elite comes forward or goes away.");
        }

        return watcher;
    }

    /// <summary>
    /// Whatever is in front right now, for deciding where things stand before
    /// anything has changed.
    /// </summary>
    internal static IntPtr InFrontNow() => GetForegroundWindow();

    /// <summary>
    /// Stops watching.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_hook != IntPtr.Zero)
        {
            UnhookWinEvent(_hook);
            _hook = IntPtr.Zero;
        }
    }

    private void OnWinEvent(
        IntPtr hook,
        uint what,
        IntPtr window,
        int self,
        int child,
        uint thread,
        uint when)
    {
        if (what == ForegroundChanged && window != IntPtr.Zero)
        {
            _changed(window);
        }
    }

    private delegate void WinEventProc(
        IntPtr hook,
        uint what,
        IntPtr window,
        int self,
        int child,
        uint thread,
        uint when);

    // System32 rather than the default probing order: user32 is an operating
    // system library, and naming where it comes from is what stops a DLL of the
    // same name beside the executable being loaded instead.
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr SetWinEventHook(
        uint from,
        uint to,
        IntPtr module,
        WinEventProc callback,
        uint process,
        uint thread,
        uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWinEvent(IntPtr hook);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr GetForegroundWindow();
}
