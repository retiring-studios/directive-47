using System;
using System.Numerics;

using D47.Placement;

using Shouldly;

using Xunit;

namespace D47.Tier0.Tests.Placement;

/// <summary>
/// Putting the overlay where the Commander is looking, given a ray to look
/// along. No runtime, no headset, no voice — this is the arithmetic half of
/// anchoring, and it depends on none of them.
/// </summary>
public class AnchorTests
{
    /// <summary>
    /// Floating point, so exact equality is not the question being asked. A
    /// tenth of a millimetre is far below anything a headset could show and far
    /// above the error in a few multiplications.
    /// </summary>
    private const float CloseEnough = 0.0001f;

    [Fact]
    public void Anchor_GivenAGazeRay_IsAlongIt()
    {
        // The happy path, which docs/decisions.md says is a criterion rather
        // than something assumed around the edge cases.
        var gaze = new GazeRay(new Vector3(0, 0, 0), new Vector3(0, 0, -1));

        Vector3 anchor = Anchor.Along(gaze, 2.0f);

        anchor.X.ShouldBe(0, CloseEnough);
        anchor.Y.ShouldBe(0, CloseEnough);
        anchor.Z.ShouldBe(-2.0f, CloseEnough);
    }

    [Fact]
    public void Anchor_WhenTheHeadIsNotAtTheOrigin_ComesFromTheRayRatherThanAFixedOffset()
    {
        // The criterion's second sentence, and the one that would still pass if
        // somebody quietly returned a constant. A Commander in a cockpit is
        // never at the origin of anything.
        var gaze = new GazeRay(new Vector3(1, 2, 3), new Vector3(0, 0, -1));

        Vector3 anchor = Anchor.Along(gaze, 2.0f);

        anchor.X.ShouldBe(1, CloseEnough);
        anchor.Y.ShouldBe(2, CloseEnough);
        anchor.Z.ShouldBe(1.0f, CloseEnough);
    }

    [Fact]
    public void Anchor_GivenADirectionThatIsNotUnitLength_IsStillTheDistanceAsked()
    {
        // A pose out of a tracking runtime is not promised to arrive
        // normalised, and a direction twice as long must not put the overlay
        // twice as far away. Distance is metres, not multiples of whatever
        // vector turned up.
        var stretched = new GazeRay(Vector3.Zero, new Vector3(0, 0, -50));

        Vector3 anchor = Anchor.Along(stretched, 2.0f);

        anchor.Length().ShouldBe(2.0f, CloseEnough);
        anchor.Z.ShouldBe(-2.0f, CloseEnough);
    }

    [Fact]
    public void Anchor_LookingDiagonally_IsTheDistanceAwayAlongThatDiagonal()
    {
        // Nothing axis-aligned, because axis-aligned cases pass against code
        // that has confused a component for a length.
        var gaze = new GazeRay(Vector3.Zero, new Vector3(1, 1, 0));

        Vector3 anchor = Anchor.Along(gaze, 3.0f);

        anchor.Length().ShouldBe(3.0f, CloseEnough);
        anchor.X.ShouldBe(anchor.Y, CloseEnough);
    }

    [Fact]
    public void Anchor_ForADirectionWithNoLength_FailsLoudlyRatherThanGuessing()
    {
        // There is no "along" a ray that points nowhere. Normalising it yields
        // NaN, which would travel silently into a transform and put the overlay
        // somewhere nobody can find — including, on some runtimes, nowhere
        // visible at all. Loud, in the house style: an unanswerable question
        // throws rather than returning something shaped like an answer.
        var nowhere = new GazeRay(Vector3.Zero, Vector3.Zero);

        Should.Throw<ArgumentException>(() => Anchor.Along(nowhere, 2.0f));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    public void Anchor_ForADistanceThatIsNotPositive_FailsLoudlyRatherThanGuessing(float distance)
    {
        // Zero puts the overlay inside the Commander's head and a negative one
        // puts it behind them, and neither is a thing anyone asked for. This is
        // the argument-checking that stops "put it there" meaning "lose it".
        var gaze = new GazeRay(Vector3.Zero, new Vector3(0, 0, -1));

        Should.Throw<ArgumentOutOfRangeException>(() => Anchor.Along(gaze, distance));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void Anchor_ForADistanceThatIsNotFinite_FailsLoudlyRatherThanGuessing(float distance)
    {
        // Added during the refactor pass, which found the guard for this with
        // no test on it. It is not covered by the not-positive check above and
        // could not be: every comparison against NaN is false, so NaN is
        // neither negative nor zero and walks straight through
        // ThrowIfNegativeOrZero.
        var gaze = new GazeRay(Vector3.Zero, new Vector3(0, 0, -1));

        Should.Throw<ArgumentOutOfRangeException>(() => Anchor.Along(gaze, distance));
    }

    [Fact]
    public void Anchor_ForARayThatIsNotFinite_FailsLoudlyRatherThanGuessing()
    {
        // A dropped tracking frame can hand back NaN. It is not an error the
        // arithmetic can recover from and it is not one it should pass on: an
        // infinite anchor is an overlay that vanishes, which reads as a bug in
        // the overlay rather than in the pose that caused it.
        var lost = new GazeRay(new Vector3(float.NaN, 0, 0), new Vector3(0, 0, -1));

        Should.Throw<ArgumentException>(() => Anchor.Along(lost, 2.0f));
    }
}
