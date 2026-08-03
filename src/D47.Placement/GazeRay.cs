using System.Numerics;

namespace D47.Placement;

/// <summary>
/// Where the Commander is looking: a point to look from, and a direction to
/// look in.
/// </summary>
///
/// <remarks>
/// The direction is not required to be unit length. What a tracking runtime
/// hands back is whatever it hands back, and normalising at the boundary of
/// this library rather than asking every caller to do it is the difference
/// between a distance in metres and a distance in multiples of an arbitrary
/// vector.
/// </remarks>
/// <param name="From">Where the eyes are.</param>
/// <param name="Towards">Which way they are pointed.</param>
public readonly record struct GazeRay(Vector3 From, Vector3 Towards);
