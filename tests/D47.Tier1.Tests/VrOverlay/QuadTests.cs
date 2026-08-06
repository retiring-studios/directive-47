using System.Numerics;

using D47.Placement;
using D47.VrOverlay;

using Shouldly;

using Valve.VR;

using Xunit;

namespace D47.Tier1.Tests.VrOverlay;

/// <summary>
/// Where the quad goes, said in OpenVR's layout.
///
/// <para>
/// Needs no headset and no runtime: this is arithmetic on a struct, and the
/// only reason it ever lived beside the compositor is that the number used to
/// be frozen into the adapter.
/// </para>
/// </summary>
public class QuadTests
{
    private const float CloseEnough = 0.0001f;

    /// <summary>
    /// The placement the adapter used to carry as a literal: a metre and a half
    /// forward of the seated origin, square to the view.
    /// </summary>
    private static Pose ForwardOfTheCommander =>
        new(new Vector3(0, 0, -1.5f), Quaternion.Identity);

    [Fact]
    public void Quad_WhereTheAdapterUsedToFreezeIt_IsTheSameMatrixAsBefore()
    {
        // The regression guard. This is exactly the literal that was deleted, so
        // if working the number out rather than freezing it changes the number,
        // this says so rather than somebody noticing in a headset.
        HmdMatrix34_t where = Quad.At(ForwardOfTheCommander);

        where.m0.ShouldBe(1, CloseEnough);
        where.m5.ShouldBe(1, CloseEnough);
        where.m10.ShouldBe(1, CloseEnough);

        where.m3.ShouldBe(0, CloseEnough);
        where.m7.ShouldBe(0, CloseEnough);
        where.m11.ShouldBe(-1.5f, CloseEnough, "a metre and a half forward");
    }

    [Fact]
    public void Quad_AtAPlaceThatIsTurned_PutsThePointWhereThePlacementSays()
    {
        // The one that matters, and the one an identity rotation cannot ask.
        //
        // .NET's Matrix4x4 is row-vector — a point is multiplied on the left,
        // and the translation lives in M41..M43. OpenVR is column-vector: the
        // matrix goes on the left and the translation is the last *column*. So
        // the conversion is a transpose of the rotation, and a transpose is
        // invisible while the rotation is the identity. Get it backwards and
        // every square-on placement looks perfect while every turned one is
        // mirrored.
        //
        // Asserted by taking a point in the overlay's own frame through both
        // routes and requiring the same answer. The expected side is worked out
        // from the pose directly rather than from the production matrix, because
        // an expectation that runs the code under test is not an expectation.
        var turned = new Pose(
            new Vector3(0.4f, 1.2f, -2f),
            Quaternion.CreateFromYawPitchRoll(0.6f, -0.25f, 0.15f));

        var corner = new Vector3(0.3f, -0.2f, 0.1f);

        HmdMatrix34_t where = Quad.At(turned);

        Vector3 openVrSays = Apply(where, corner);
        Vector3 placementSays = Vector3.Transform(corner, turned.Orientation) + turned.Position;

        openVrSays.X.ShouldBe(placementSays.X, CloseEnough, "the quad is turned the wrong way");
        openVrSays.Y.ShouldBe(placementSays.Y, CloseEnough, "the quad is turned the wrong way");
        openVrSays.Z.ShouldBe(placementSays.Z, CloseEnough, "the quad is turned the wrong way");
    }

    [Fact]
    public void APoseTaken_AndReadBack_IsTheSamePose()
    {
        // Reading a controller is the same transpose the other way round, and it
        // fails the same way: invisible while the thing is square to the world,
        // wrong the moment it is turned. A round trip is what says the two halves
        // agree, and a rotation on all three axes is what stops it agreeing by
        // symmetry.
        var held = new Pose(
            new Vector3(-0.7f, 1.1f, -0.4f),
            Quaternion.CreateFromYawPitchRoll(-0.9f, 0.35f, 1.2f));

        Pose read = Quad.From(Quad.At(held));

        read.Position.X.ShouldBe(held.Position.X, CloseEnough);
        read.Position.Y.ShouldBe(held.Position.Y, CloseEnough);
        read.Position.Z.ShouldBe(held.Position.Z, CloseEnough);

        // Compared by where they send a point rather than component by
        // component, because q and -q are the same rotation and a test that
        // insisted otherwise would fail on an equally correct answer.
        var somewhere = new Vector3(0.2f, -0.5f, 0.9f);

        var asRead = Vector3.Transform(somewhere, read.Orientation);
        var asHeld = Vector3.Transform(somewhere, held.Orientation);

        asRead.X.ShouldBe(asHeld.X, CloseEnough, "the pose came back turned the wrong way");
        asRead.Y.ShouldBe(asHeld.Y, CloseEnough, "the pose came back turned the wrong way");
        asRead.Z.ShouldBe(asHeld.Z, CloseEnough, "the pose came back turned the wrong way");
    }

    [Fact]
    public void APoseRead_FromAnEmptySlot_LooksPerfectlyValid_WhichIsTheTrap()
    {
        // Every field zero is what an unconnected device's slot holds, and the
        // runtime hands back a full array whether or not the devices in it
        // exist. This is what such a slot turns into.
        //
        // It is not an error and nothing downstream will call it one:
        // Quaternion.CreateFromRotationMatrix on an all-zero matrix picks the
        // largest diagonal, finds sqrt(1), and produces a unit rotation — so
        // Turn.Of normalises it happily and Pointing answers as if a real
        // controller were sitting at the origin facing backwards.
        //
        // Written down because it decides where the check goes: bPoseIsValid and
        // bDeviceIsConnected have to be read in the adapter, before this is
        // called. There is no later layer that will catch it.
        Pose read = Quad.From(default);

        read.Position.ShouldBe(Vector3.Zero);

        Should.NotThrow(() => Pointing.At(read, new Board(read, 0.5f, 0.3f)));
    }

    [Fact]
    public void Quad_AtAPlace_PutsItsOriginWhereThePlacementWasPut()
    {
        // The translation on its own, which is the half of it a reader checks
        // first when the overlay turns up somewhere unexpected.
        var somewhere = new Pose(
            new Vector3(-0.7f, 1.6f, -2.4f),
            Quaternion.CreateFromYawPitchRoll(1.1f, 0.2f, -0.4f));

        Vector3 origin = Apply(Quad.At(somewhere), Vector3.Zero);

        origin.X.ShouldBe(somewhere.Position.X, CloseEnough);
        origin.Y.ShouldBe(somewhere.Position.Y, CloseEnough);
        origin.Z.ShouldBe(somewhere.Position.Z, CloseEnough);
    }

    /// <summary>
    /// Takes a point through OpenVR's matrix the way the runtime would: the
    /// matrix on the left, the point as a column.
    /// </summary>
    /// <param name="where">The transform OpenVR was given.</param>
    /// <param name="point">A point in the overlay's own frame.</param>
    /// <returns>Where that point lands in tracking space.</returns>
    private static Vector3 Apply(HmdMatrix34_t where, Vector3 point) =>
        new(
            (where.m0 * point.X) + (where.m1 * point.Y) + (where.m2 * point.Z) + where.m3,
            (where.m4 * point.X) + (where.m5 * point.Y) + (where.m6 * point.Z) + where.m7,
            (where.m8 * point.X) + (where.m9 * point.Y) + (where.m10 * point.Z) + where.m11);
}
