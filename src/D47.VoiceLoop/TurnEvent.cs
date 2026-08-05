namespace D47.VoiceLoop;

/// <summary>
/// Something worth telling a surface about.
///
/// <para>
/// A closed set of records rather than a bag of loose keys. The predecessor
/// project published dictionaries with a <c>type</c> string in them, which is
/// the right shape for Python feeding a browser over JSON and the wrong one
/// here: it discards the checking every other contract in this repository leans
/// on, and a surface reading a key nobody publishes any more fails at runtime on
/// somebody else's machine rather than in the build.
/// </para>
/// </summary>
public abstract record TurnEvent;

/// <summary>
/// A turn has entered a state.
/// </summary>
///
/// <remarks>
/// The only event there is today, and the reason the base above exists anyway:
/// the journal and status watchers of Wave 3 publish on this bus too, and
/// <c>#72</c>'s live log reads more than transitions. Adding a case is additive;
/// widening a dictionary is not.
/// </remarks>
/// <param name="State">The state just entered.</param>
public sealed record Entered(TurnState State) : TurnEvent;
