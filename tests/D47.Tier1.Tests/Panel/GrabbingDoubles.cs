using System.Collections.Generic;
using System.Numerics;
using System.Threading;

using D47.Placement;
using D47.Render;
using D47.VrOverlay;

namespace D47.Tier1.Tests.Panel;

/// <summary>
/// The three things a grab is watched through, for the two test classes that
/// watch it.
///
/// <para>
/// Shared because they were written twice. <c>GrabbingTests</c> covers the
/// looking and <c>GrabbingTheBarTests</c> covers the dragging, and each grew its
/// own hand, its own overlay and its own chrome — seven doubles across two files
/// with three overlapping pairs, one of them a strict subset of the other.
/// </para>
///
/// <para>
/// Locked rather than volatile. Everything crossing between a test and the
/// watching thread is a struct, and <c>Volatile</c> does not take those.
/// </para>
/// </summary>
internal sealed class HandThatMoves : IControllers
{
    private readonly Lock _guard = new();

    private Pose? _at;

    private HandThatMoves(Pose? at)
    {
        _at = at;
    }

    /// <summary>
    /// A controller the runtime can see, wherever the test put it.
    /// </summary>
    internal static HandThatMoves At(Vector3 where) =>
        new(new Pose(where, Quaternion.Identity));

    /// <summary>
    /// Both controllers on the desk, which is most of a session.
    /// </summary>
    internal static HandThatMoves Asleep() => new(null);

    internal void MoveTo(Vector3 moved)
    {
        lock (_guard)
        {
            _at = new Pose(moved, Quaternion.Identity);
        }
    }

    /// <summary>
    /// Stops being tracked, which is a controller put down mid-drag.
    /// </summary>
    internal void Vanish()
    {
        lock (_guard)
        {
            _at = null;
        }
    }

    public IReadOnlyList<Pose> Tracked() => At(0) is { } at ? [at] : [];

    public Pose? At(uint device)
    {
        lock (_guard)
        {
            return _at;
        }
    }
}

/// <summary>
/// A headset overlay that is nothing but where it was last put, and how many
/// times it was put there.
/// </summary>
///
/// <remarks>
/// Counting rather than throwing on an unexpected move. <c>Grabbing</c> catches
/// everything its loop throws and writes it down, so a double that threw would
/// turn "it moved when it should not have" into a log line nobody asserted on.
/// </remarks>
internal sealed class OverlayThatMoves : IHeadsetOverlay
{
    private readonly Lock _guard = new();

    private Board _placed;
    private int _moves;
    private int _resizes;

    internal OverlayThatMoves(Vector3 where)
    {
        _placed = new Board(new Pose(where, Quaternion.Identity), 0.5f, 0.3f);
    }

    /// <summary>
    /// Somewhere plausible, for tests that do not care where it is.
    /// </summary>
    internal static OverlayThatMoves Somewhere() => new(new Vector3(0, 0, -1.5f));

    public bool IsVisible => true;

    public Board Placed
    {
        get
        {
            lock (_guard)
            {
                return _placed;
            }
        }
    }

    /// <summary>
    /// How many times it was asked to move. Nought is the assertion for every
    /// gesture that is not a drag.
    /// </summary>
    internal int Moves => Volatile.Read(ref _moves);

    /// <summary>
    /// How many times it was asked to change size. Nought is the assertion for
    /// every gesture that is not a corner pull.
    /// </summary>
    internal int Resizes => Volatile.Read(ref _resizes);

    public void MoveTo(Pose where)
    {
        Interlocked.Increment(ref _moves);

        lock (_guard)
        {
            _placed = _placed with { Where = where };
        }
    }

    public void ResizeTo(Board board)
    {
        Interlocked.Increment(ref _resizes);

        lock (_guard)
        {
            _placed = board;
        }
    }

    public void Show()
    {
    }

    public void Hide()
    {
    }

    public void Paint(Presentation presented)
    {
    }

    public void Dispose()
    {
    }
}

/// <summary>
/// Chrome that reports whatever the test tells it to, and remembers being asked.
/// </summary>
internal sealed class ChromeThatReports : IGrabChrome
{
    private readonly Lock _guard = new();

    private Grip _reported;
    private Board? _framed;
    private int _followed;

    internal ChromeThatReports(Grip reported)
    {
        _reported = reported;
    }

    /// <summary>
    /// A laser resting on something with no trigger pulled, which is what
    /// pointing looks like.
    /// </summary>
    internal static ChromeThatReports PointingAt(Grabbed under) =>
        new(new Grip(under, Held: false, Hand: 0));

    internal int Followed => Volatile.Read(ref _followed);

    /// <summary>
    /// Where the chrome was last told the panel is, or nothing if it never was.
    /// </summary>
    internal Board? Framed
    {
        get
        {
            lock (_guard)
            {
                return _framed;
            }
        }
    }

    internal void Reports(Grip next)
    {
        lock (_guard)
        {
            _reported = next;
        }
    }

    public void Showing(Grabbed lit, Shown shown)
    {
    }

    public Grip Follow()
    {
        Interlocked.Increment(ref _followed);

        lock (_guard)
        {
            return _reported;
        }
    }

    public void Frames(Board panel)
    {
        lock (_guard)
        {
            _framed = panel;
        }
    }

    public void Dispose()
    {
    }
}
