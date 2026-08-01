using D47.Capabilities;

namespace D47.Panel;

/// <summary>
/// One capability's answer, paired with the declaration that says how to show
/// it. The descriptor carries the shape and the result carries the values, so a
/// surface needs both and neither on its own is renderable.
///
/// <para>
/// Lives here rather than in <c>D47.Capabilities</c> because the contract
/// assembly does not reference its consumers. It moves when a second surface
/// needs it, which is the same move this whole project is expecting.
/// </para>
/// </summary>
internal sealed record Answer
{
    /// <summary>
    /// The declaration the answer conforms to.
    /// </summary>
    public required CapabilityDescriptor Descriptor { get; init; }

    /// <summary>
    /// What the capability returned this time.
    /// </summary>
    public required CapabilityResult Result { get; init; }
}
