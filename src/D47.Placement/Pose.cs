using System.Numerics;

namespace D47.Placement;

/// <summary>
/// Where something tracked is, and which way it is facing.
/// </summary>
///
/// <remarks>
/// The orientation is not required to be unit length. A quaternion out of a
/// tracking runtime drifts off it over a session, and normalising at the
/// boundary of this library is the difference between an overlay that stays put
/// and one that creeps away the longer somebody plays.
/// </remarks>
/// <param name="Position">Where it is.</param>
/// <param name="Orientation">Which way it is facing.</param>
public readonly record struct Pose(Vector3 Position, Quaternion Orientation);
