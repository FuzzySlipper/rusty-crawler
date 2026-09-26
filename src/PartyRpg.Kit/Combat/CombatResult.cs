using System.Globalization;

namespace PartyRpg.Kit.Combat;

/// <summary>
/// What one order to attack did, or why the actor did not act, as a report a panel can show.
/// </summary>
/// <remarks>
/// <para>
/// A result always says who was ordered and what came of it, so a caller can read it unconditionally and
/// never has to infer what happened from the branch it took. A refusal changes nothing at all: the actor's
/// recovery is untouched, no initiation is recorded, and the target is exactly as it was.
/// </para>
/// <para>
/// <b>A refusal names the rule it broke.</b> An actor still recovering, an identity no combatant has, and a
/// target on the actor's own side are three different facts a player acts on differently, and one sentence
/// saying "the attack did not happen" would hide which of them it was.
/// </para>
/// <para>
/// <b>An accepted attack is not a hit.</b> The initiation says what was attempted and what it cost; whether
/// it lands, and what it does, is resolution and belongs to the ruleset. An applied result therefore carries
/// no damage, no condition, and no death — this stone paces attacks, and nothing yet answers what they do.
/// </para>
/// </remarks>
public sealed record CombatResult
{
    private CombatResult(
        CombatantId actor,
        string actorName,
        bool isApplied,
        string code,
        string message,
        AttackInitiation? initiated)
    {
        Actor = actor;
        ActorName = actorName;
        IsApplied = isApplied;
        Code = code;
        Message = message;
        Initiated = initiated;
    }

    /// <summary>The combatant that was ordered.</summary>
    public CombatantId Actor { get; }

    /// <summary>What that combatant is called, as the ruleset named it; empty when nobody has that identity.</summary>
    public string ActorName { get; }

    /// <summary>Whether the actor acted.</summary>
    public bool IsApplied { get; }

    /// <summary>The refusal's own code, empty when the actor acted.</summary>
    public string Code { get; }

    /// <summary>What happened, in a sentence a person reads.</summary>
    public string Message { get; }

    /// <summary>What the attack was, or null when the actor did not act.</summary>
    public AttackInitiation? Initiated { get; }

    /// <summary>The actor attacked; this is what the attempt was and what it cost.</summary>
    /// <param name="initiation">The attack the fight initiated.</param>
    /// <returns>The result.</returns>
    /// <exception cref="ArgumentNullException">No initiation was supplied.</exception>
    public static CombatResult Applied(AttackInitiation initiation)
    {
        ArgumentNullException.ThrowIfNull(initiation);
        string message = initiation.HasTarget
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"{initiation.ActorName} attacks {initiation.TargetName} ({AttackKinds.WireName(initiation.Kind)}) and must recover {initiation.Recovery.Milliseconds}ms of game time.")
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{initiation.ActorName} attacks nothing in reach ({AttackKinds.WireName(initiation.Kind)}) and must recover {initiation.Recovery.Milliseconds}ms of game time.");
        return new CombatResult(initiation.Actor, initiation.ActorName, isApplied: true, string.Empty, message, initiation);
    }

    /// <summary>The actor did not act, and this is why.</summary>
    /// <param name="actor">The combatant that was ordered.</param>
    /// <param name="actorName">What that combatant is called, empty when nobody has that identity.</param>
    /// <param name="code">The refusal's own code.</param>
    /// <param name="message">Why the actor did not act, in a sentence a person reads.</param>
    /// <returns>The result.</returns>
    /// <exception cref="ArgumentException">The code or the message is missing, so the refusal could not be told from another.</exception>
    public static CombatResult Refused(CombatantId actor, string? actorName, string code, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new CombatResult(actor, actorName ?? string.Empty, isApplied: false, code, message, initiated: null);
    }

    /// <inheritdoc />
    public override string ToString() => IsApplied ? Message : $"{Actor}: {Code} — {Message}";
}
