using System;
using System.Threading;

namespace D47.Panel;

/// <summary>
/// The claim that makes this copy of Directive 47 the only one.
///
/// <para>
/// A second copy is confusing rather than obvious in every direction: two
/// identical tray icons with no way to tell them apart, two overlays stacked so
/// exactly that hiding one reveals the other, and one hotkey owned by whichever
/// started first — so the copy that logs the refusal is the one that lost, and
/// the log describes a machine problem that is really a duplicate launch. It
/// gets worse with every wave, and harder to recognize as the cause. See
/// [#107](https://github.com/retiring-studios/directive-47/issues/107).
/// </para>
///
/// <para>
/// A named mutex rather than the hotkey registration, a lock file, or a look
/// through the process table. Windows closes the handle when the process ends —
/// cleanly, killed, or crashed — and the kernel object goes with it, so there is
/// never a stale claim to detect or clean up. A lock file needs staleness
/// detection, which is a second mechanism to get right guarding a case this one
/// does not have. Reading the process table is racy between the look and the
/// launch, and cannot tell our copy from anything else sharing the name.
/// </para>
///
/// <para>
/// The hotkey deserves its own sentence, because it looks like a free answer
/// and is not. Directive 47 deliberately survives losing
/// <c>Ctrl</c>+<c>Alt</c>+<c>D</c> to another application
/// ([#103](https://github.com/retiring-studios/directive-47/issues/103)), and
/// making the registration the claim would turn that survivable absence into a
/// refusal to start at all — for any application on the machine, not only for
/// another copy of this one.
/// </para>
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    /// <summary>
    /// The name of the claim.
    ///
    /// <para>
    /// <c>Local\</c> is the session namespace, which in practice is one copy per
    /// logged-in Windows user: two accounts logged in at once each get their
    /// own, which is the only "two Commanders" case that means anything — two
    /// accounts are two journals, two game installs, two of everything below us
    /// as well. <c>Global\</c> would be one per machine and would deny the
    /// second account a copy for no reason we have.
    /// </para>
    ///
    /// <para>
    /// Spelled out rather than left to the default. Unprefixed names already
    /// land in the session namespace, so the prefix changes nothing about what
    /// happens — it changes whether the next reader has to know that to see
    /// which of the two this is, and the scope was a decision rather than an
    /// accident.
    /// </para>
    ///
    /// <para>
    /// Reversed-domain rather than a guid, so that whoever finds it in Process
    /// Explorer at two in the morning can tell what owns it.
    /// </para>
    /// </summary>
    private const string Name = @"Local\retiring-studios.directive-47";

    private readonly Mutex _claim;

    private SingleInstance(Mutex claim)
    {
        _claim = claim;
    }

    /// <summary>
    /// Claims the right to be the running copy.
    /// </summary>
    /// <returns>
    /// The claim, which the caller owns and must dispose for as long as the
    /// application runs, or <see langword="null"/> when this machine is already
    /// running Directive 47 for this user.
    /// </returns>
    internal static SingleInstance? TryClaim()
    {
        // Ownership is deliberately not taken, and the argument is not
        // cosmetic: what makes a second copy lose is that the *name* already
        // exists, which the kernel decides atomically as it creates the object.
        // Nothing here ever waits on the mutex, so owning it would buy nothing
        // and cost the thread affinity that comes with having to release it —
        // ReleaseMutex throws from any thread but the one that acquired it, and
        // a shutdown path is not somewhere to discover that.
        var claim = new Mutex(initiallyOwned: false, Name, out bool ours);

        if (ours)
        {
            return new SingleInstance(claim);
        }

        // Closed at once. A handle held open by the copy that is about to exit
        // keeps the name alive past its usefulness, and the window where that
        // matters is exactly the one an installer restarting the application
        // would land in.
        claim.Dispose();

        return null;
    }

    /// <summary>
    /// Gives the claim up, so the next launch is the running copy.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// Windows would do this when the process ends either way. Doing it here is
    /// what keeps a clean exit from depending on that — the same reason the
    /// hotkey is unregistered rather than left to teardown.
    /// </para>
    /// <para>
    /// No guard against being called twice, unlike <see cref="Hotkey"/> beside
    /// it. That one has real work to undo and would undo it again; this hands
    /// off to a safe handle, which is already idempotent, so a flag here would
    /// be a guard with nothing behind it.
    /// </para>
    /// </remarks>
    public void Dispose() => _claim.Dispose();
}
