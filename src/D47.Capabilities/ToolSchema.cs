namespace D47.Capabilities;

/// <summary>
/// What the model is told about a capability so it can call it. Rides the
/// cached system prefix, so it must be identical from one turn to the next.
/// </summary>
public sealed record ToolSchema
{
    /// <summary>
    /// The tool name the model calls, in the snake_case those APIs expect.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// What the tool does, written for the model rather than for a person.
    /// </summary>
    public required string Description { get; init; }
}
