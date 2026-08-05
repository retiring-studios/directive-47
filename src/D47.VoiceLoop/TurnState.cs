namespace D47.VoiceLoop;

/// <summary>
/// Where a turn has got to.
///
/// <para>
/// The surfaces render from these and from nothing else, which is why they are
/// named here rather than described in each surface's own words. A state is what
/// the Commander is waiting on, not what the code is doing — so there is no
/// state for "about to", and none for anything that finishes too fast to be
/// worth waiting through.
/// </para>
/// </summary>
public enum TurnState
{
    /// <summary>
    /// Nothing in flight. Where a turn starts and where every turn ends,
    /// including one that failed.
    /// </summary>
    Idle,

    /// <summary>
    /// The key is held and the microphone is open.
    /// </summary>
    Listening,

    /// <summary>
    /// The key is up and what was said is being turned into words.
    /// </summary>
    Transcribing,

    /// <summary>
    /// The words have gone to the model and the reply has not arrived.
    /// </summary>
    Thinking,

    /// <summary>
    /// The reply is being spoken.
    /// </summary>
    Speaking,
}
