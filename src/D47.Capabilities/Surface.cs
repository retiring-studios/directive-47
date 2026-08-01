namespace D47.Capabilities;

/// <summary>
/// Somewhere a capability reaches the Commander. Three visual, one spoken.
///
/// <para>
/// The visual three are one render presented three ways, not three designs —
/// so a capability cannot appear on one and not another. Neither overlay can
/// take pointer input, because games hide and capture the cursor; that is why
/// voice is the universal input and pointer support is a convenience layered
/// on the panel.
/// </para>
/// </summary>
public enum Surface
{
    /// <summary>
    /// Transparent, click-through, above a borderless-windowed game.
    /// </summary>
    GameOverlay,

    /// <summary>
    /// The SteamVR overlay, in the headset.
    /// </summary>
    VrOverlay,

    /// <summary>
    /// An ordinary window outside the game. Convenience, not requirement.
    /// </summary>
    Panel,

    /// <summary>
    /// Spoken aloud.
    /// </summary>
    Voice,
}
