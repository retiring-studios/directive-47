using System;

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

/// <summary>
/// A stage of a turn threw, and the turn gave up on it.
/// </summary>
///
/// <remarks>
/// <para>
/// An event rather than a sixth <see cref="TurnState"/>. A state is what the
/// Commander is waiting on, and nobody waits on a failure — it is a thing that
/// happened, and what follows it is <see cref="TurnState.Idle"/> like the end of
/// any other turn. A state would also have asked every surface a question with
/// no answer: how long does it stay there.
/// </para>
/// <para>
/// It carries the exception rather than a message made from one. What reads this
/// today writes a line to the log and wants the message; a crash report or a
/// diagnostics page wants the type and the stack, and neither can be got back
/// once this has been flattened to a string.
/// </para>
/// </remarks>
/// <param name="During">The state the turn was in when it gave up.</param>
/// <param name="Why">What went wrong.</param>
public sealed record Failed(TurnState During, Exception Why) : TurnEvent;
