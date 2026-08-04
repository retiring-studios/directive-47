using System;
using System.Linq;
using System.Numerics;

using Shouldly;

using Xunit;

namespace D47.Placement.Tests;

/// <summary>
/// World-locked, which is the only way the overlay is placed: bolted to the
/// cockpit rather than pinned to the Commander's face.
///
/// <para>
/// Most of what used to be here was about the handover between two modes, and
/// went when the second mode did. What is left is small on purpose —
/// <c>WorldLocked.At</c> is a pure function of where the overlay was put, and
/// there is no longer anything for it to disagree with.
/// </para>
/// </summary>
public class WorldLockedTests
{
    private const float CloseEnough = 0.0001f;

    [Fact]
    public void Placement_OffersNoModeOtherThanWorldLocked()
    {
        // The decision, made falsifiable. Head-locked was built and then taken
        // out: the overlay is meant to read as part of the ship's HUD rather
        // than the visor's, so following the view was never what was wanted.
        //
        // Asked of the assembly rather than of a call, because what is being
        // asserted is an absence, and an absence has nothing to call. Without
        // this the decision lives only in prose, and a mode nobody wants is
        // exactly the sort of thing that gets helpfully added back.
        typeof(WorldLocked).Assembly.GetExportedTypes()
            .Select(type => type.Name)
            .ShouldNotContain(
                "HeadLocked",
                "placement has one mode, and it is world-locked");
    }

    [Fact]
    public void WorldLocked_AtAPlace_PutsTheOverlayThere()
    {
        // The happy path. A world-locked overlay is wherever it was bolted, and
        // the transform says so without anybody having to ask where a head is.
        var bolted = new Pose(new Vector3(1, 2, -3), Quaternion.Identity);

        Matrix4x4 overlay = WorldLocked.At(bolted);

        overlay.Translation.X.ShouldBe(1, CloseEnough);
        overlay.Translation.Y.ShouldBe(2, CloseEnough);
        overlay.Translation.Z.ShouldBe(-3, CloseEnough);
    }

    [Fact]
    public void WorldLocked_AtAPlaceWithAnOrientation_FacesTheWayItWasBolted()
    {
        // Written when the switching tests were taken out, because they were
        // the only thing asserting the overlay's facing as well as its position.
        // Without this, a transform that put the overlay in exactly the right
        // place pointing the wrong way would pass everything else in this file.
        var turned = Quaternion.CreateFromYawPitchRoll(0.7f, -0.3f, 0.1f);

        var bolted = new Pose(new Vector3(1, 2, -3), turned);

        Matrix4x4 overlay = WorldLocked.At(bolted);

        // The absolute of the dot product, because a quaternion and its negative
        // are the same rotation and which one comes back is not this test's
        // business.
        MathF.Abs(Quaternion.Dot(Quaternion.CreateFromRotationMatrix(overlay), turned))
            .ShouldBe(1, CloseEnough, "the overlay should face the way it was bolted");
    }

    [Fact]
    public void WorldLocked_ForAPoseThatIsNotFinite_FailsLoudlyRatherThanGuessing()
    {
        var lost = new Pose(new Vector3(float.NaN, 0, 0), Quaternion.Identity);

        Should.Throw<ArgumentException>(() => WorldLocked.At(lost));
    }
}
