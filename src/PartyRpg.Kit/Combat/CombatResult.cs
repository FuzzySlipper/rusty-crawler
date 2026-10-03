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
/// <b>An accepted attack is not a hit.</b> The initiation says what was attempted and what it cost;
/// <see cref="Resolution"/> says what it did — whether it landed, what harm it left, and what condition
/// followed. The two travel together on one result so a reader never sees an outcome beside the shape of an
/// earlier swing, and a fight whose ruleset resolves nothing carries an initiation with no outcome rather
/// than a hit nothing answered.
/// </para>
/// </remarks>
public sealed record CombatResult
{
    private CombatResult(
        CombatantId actor,
        string actorName,
        Refusal? refusal,
        string message,
        AttackInitiation? initiated,
        CombatResolution? resolution)
    {
        Actor = actor;
        ActorName = actorName;
        Refusal = refusal;
        Message = message;
        Initiated = initiated;
        Resolution = resolution;
    }

    /// <summary>The combatant that was ordered.</summary>
    public CombatantId Actor { get; }

    /// <summary>What that combatant is called, as the ruleset named it; empty when nobody has that identity.</summary>
    public string ActorName { get; }

    /// <summary>Whether the actor acted.</summary>
    public bool IsApplied => Refusal is null;

    /// <summary>Why the actor did not act, or null when it did.</summary>
    public Refusal? Refusal { get; }

    /// <summary>The refusal's own code, empty when the actor acted.</summary>
    public string Code => Refusal?.Code ?? string.Empty;

    /// <summary>What happened, in a sentence a person reads.</summary>
    public string Message { get; }

    /// <summary>What the attack was, or null when the actor did not act.</summary>
    public AttackInitiation? Initiated { get; }

    /// <summary>What the attack did, or null when the actor did not act or nothing answered what it does.</summary>
    public CombatResolution? Resolution { get; }

    /// <summary>The actor attacked; this is what the attempt was, what it cost, and what it did.</summary>
    /// <param name="initiation">The attack the fight initiated.</param>
    /// <param name="resolution">
    /// What the attack did, or null when the ruleset answered nothing about what attacks do. A fight with no
    /// resolution still reports the attempt, because an attack that was made is a fact about the fight
    /// whether or not anything can say what it was worth.
    /// </param>
    /// <returns>The result.</returns>
    /// <exception cref="ArgumentNullException">No initiation was supplied.</exception>
    public static CombatResult Applied(AttackInitiation initiation, CombatResolution? resolution = null)
    {
        ArgumentNullException.ThrowIfNull(initiation);
        // The sentence a player reads names how the attack was made and the recovery it costs in the game seconds the
        // fight's panel counts; the ability an attack used is the initiation's own fact, not a word for a player.
        string how = AttackKinds.WireName(initiation.Kind);
        string attempt = initiation.HasTarget
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"{initiation.ActorName} attacks {initiation.TargetName} ({how}) and recovers for {initiation.Recovery.TotalSeconds:0.0}s.")
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{initiation.ActorName} attacks nothing in reach ({how}) and recovers for {initiation.Recovery.TotalSeconds:0.0}s.");
        string message = resolution is null ? attempt : $"{attempt} {resolution.Message}";
        return new CombatResult(initiation.Actor, initiation.ActorName, refusal: null, message, initiation, resolution);
    }

    /// <summary>The actor did not act, and this is why.</summary>
    /// <param name="actor">The combatant that was ordered.</param>
    /// <param name="actorName">What that combatant is called, empty when nobody has that identity.</param>
    /// <param name="refusal">Why the actor did not act.</param>
    /// <returns>The result.</returns>
    /// <exception cref="ArgumentNullException">No refusal was given.</exception>
    public static CombatResult Refused(CombatantId actor, string? actorName, Refusal refusal)
    {
        ArgumentNullException.ThrowIfNull(refusal);
        return new CombatResult(actor, actorName ?? string.Empty, refusal, refusal.Message, initiated: null, resolution: null);
    }

    /// <inheritdoc />
    public override string ToString() => IsApplied ? Message : $"{Actor}: {Code} — {Message}";
}
