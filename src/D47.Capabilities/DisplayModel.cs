namespace D47.Capabilities;

/// <summary>
/// How a capability's answer is shown — the <em>shape</em>, declared once on
/// the descriptor, not the values. Text, lists and key-value readouts cover
/// most of what anyone alt-tabs for; a capability needing something the model
/// cannot express may supply its own view instead, at the price of supplying
/// one for every surface.
///
/// <para>
/// Kept deliberately small. Without that price on the escape hatch, this grows
/// a Chart concept, then a Map concept, and becomes a UI framework nobody chose
/// to write.
/// </para>
/// </summary>
public abstract record DisplayModel
{
    private protected DisplayModel()
    {
    }
}

/// <summary>
/// An ordered list of lines. Reads the same spoken aloud as it does rendered.
/// </summary>
public sealed record ListDisplay : DisplayModel;
