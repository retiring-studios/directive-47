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
}
