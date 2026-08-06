using System.Numerics;

using D47.Placement;

using Valve.VR;

namespace D47.VrOverlay;

/// <summary>
/// Where the overlay's quad goes, in the shape OpenVR wants it.
///
/// <para>
/// The adapter's whole job at this seam: <c>D47.Placement</c> decides where,
/// and this says the same thing in the runtime's own layout. Nothing here
/// chooses a position.
/// </para>
/// </summary>
public static class Quad
{
    /// <summary>
    /// The overlay's transform, given where it was placed.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// The rotation is transposed on the way across, and that is a difference in
    /// convention rather than a fiddle. <c>Matrix4x4</c> is row-vector — a point
    /// multiplies on the left and the translation sits in the last row. OpenVR is
    /// column-vector: its matrix goes on the left and the translation is the last
    /// column. So a basis vector that is a row here is a column there.
    /// </para>
    /// <para>
    /// Invisible while the placement is square to the view, which is exactly how
    /// a frozen identity matrix hid it for as long as it did. Get it backwards
    /// and every unturned overlay looks perfect while every turned one is
    /// mirrored, so the test that matters uses a rotation.
    /// </para>
    /// </remarks>
    /// <param name="placed">Where the overlay was put.</param>
    /// <returns>The same placement, as OpenVR's 3x4 matrix.</returns>
    public static HmdMatrix34_t At(Pose placed)
    {
        Matrix4x4 transform = WorldLocked.At(placed);

        return new HmdMatrix34_t
        {
            m0 = transform.M11, m1 = transform.M21, m2 = transform.M31, m3 = transform.M41,
            m4 = transform.M12, m5 = transform.M22, m6 = transform.M32, m7 = transform.M42,
            m8 = transform.M13, m9 = transform.M23, m10 = transform.M33, m11 = transform.M43,
        };
    }

    /// <summary>
    /// Where something tracked is, read back out of the runtime's own layout.
    /// </summary>
    ///
    /// <remarks>
    /// <para>
    /// The other direction, and the one a controller arrives in. <see cref="At"/>
    /// says where to put a quad; this says where a hand already is, so that
    /// <c>D47.Placement</c> can be asked what it is pointing at without ever
    /// meeting an <c>HmdMatrix34_t</c>.
    /// </para>
    /// <para>
    /// The same transpose, read the other way. Getting it backwards is invisible
    /// while a controller is held square to the world and wrong the moment it is
    /// turned — which is the trap the round trip through <see cref="At"/> is
    /// tested against, and the same one that hid a frozen identity matrix here
    /// for as long as it did.
    /// </para>
    /// </remarks>
    /// <param name="tracked">The runtime's transform for a tracked device.</param>
    /// <returns>Where it is, and which way it faces.</returns>
    public static Pose From(HmdMatrix34_t tracked)
    {
        var transform = new Matrix4x4(
            tracked.m0, tracked.m4, tracked.m8, 0,
            tracked.m1, tracked.m5, tracked.m9, 0,
            tracked.m2, tracked.m6, tracked.m10, 0,
            tracked.m3, tracked.m7, tracked.m11, 1);

        return new Pose(transform.Translation, Quaternion.CreateFromRotationMatrix(transform));
    }
}
