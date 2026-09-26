using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Magic;

/// <summary>One casting handed to the effect path: the spell, who cast it, and what it was aimed at.</summary>
/// <remarks>
/// <para>
/// This is the whole of what the casting mechanism says to whatever applies a spell's effect, and it is
/// deliberately everything that decision needs rather than a pre-digested answer: the definition as this
/// game read it, the caster with its own state, the fight the party is in when it is in one, and the actor
/// the spell was aimed at. What the effect is worth is not here — the spell's identity is, and the effect
/// owner reads its own tables.
/// </para>
/// <para>
/// <b>The fight travels with the application because a spell's harm is a fight's business.</b> A damaging
/// spell applied to a creature is an attack of the spell kind, resolved by the same path a swing and a shot
/// take; handing the fight over is what lets the effect owner order that attack through the fight's own
/// gated entry rather than resolving it beside the fight. A session with no fight hands nothing, and an
/// effect that needs one says so.
/// </para>
/// </remarks>
/// <param name="Party">The party the caster belongs to, with the state an effect may act on.</param>
/// <param name="Caster">The member who cast the spell.</param>
/// <param name="CasterId">The caster's identity inside the fight, when the session is playing one.</param>
/// <param name="Spell">The spell as this game read it.</param>
/// <param name="Fight">The fight the party is in, or null when the session is playing none.</param>
/// <param name="Target">The actor the spell was aimed at, or null when its aim names nobody.</param>
/// <param name="TargetName">
/// What the casting named: the actor's name when the aim names one, or the screen's own word for what a spell
/// with no actor aim acts on — a place a portal reaches, a thing a hand moves — and empty when nothing was
/// named at all. Only the effect owner can judge such a word, which is why it travels here unread.
/// </param>
/// <param name="Source">
/// The item the casting took its spell from, or null when it came from the caster's own spellbook.
/// </param>
/// <remarks>
/// <para>
/// <b>What carried the spell travels with the casting because an item may state the strength.</b> A spell cast
/// from a spellbook is made at the caster's own mastery of its school and nothing else can say otherwise, but
/// a thing that carries a spell can also state how strong it is — the game's own potions are exactly that —
/// and the effect owner is the only place that reading is applied. Handing the instance over is what lets a
/// game read a strength the item states instead of inventing one from a character who may hold none of the
/// school at all.
/// </para>
/// <para>
/// It is the instance rather than a number because what an item states is the item's own business: a game
/// reads its own state — a potion's strength, a wand's remaining charges — where its effect is applied, so
/// the mechanism never learns which kinds of thing carry a strength.
/// </para>
/// </remarks>
public sealed record SpellApplication(
    PartyEntity Party,
    PartyMember Caster,
    CombatantId CasterId,
    SpellDefinition Spell,
    CombatState? Fight,
    CombatantId? Target,
    string TargetName,
    ItemInstance? Source = null);

/// <summary>What applying a spell's effect did, as the effect owner reports it.</summary>
/// <remarks>
/// <para>
/// The outcome is a value rather than a changed world, so the casting workflow can publish exactly what
/// happened without learning a single effect: whether this build expressed anything for the spell at all,
/// which effect identity it was handed, the fight's own record when the effect went through the fight, and
/// a sentence a person reads.
/// </para>
/// <para>
/// <b>An unexpressed effect is a fact, not a failure.</b> A spell this build has no implementation for is
/// still learned, still paid for, and still delivered here; the difference is that nothing in the world
/// changed, and this says so in the spell's own identity rather than reporting a cast that quietly did
/// nothing. That is the seam's whole purpose: what a spell does arrives behind it without the casting
/// mechanism changing.
/// </para>
/// </remarks>
public sealed record SpellApplicationOutcome
{
    private SpellApplicationOutcome(
        bool expressed,
        string effect,
        string code,
        string message,
        CombatResult? attack,
        IReadOnlyList<SpellEffectFact> facts)
    {
        IsExpressed = expressed;
        Effect = effect;
        Code = code;
        Message = message;
        Attack = attack;
        Facts = facts;
    }

    /// <summary>Whether this build expressed anything for the spell's effect.</summary>
    public bool IsExpressed { get; }

    /// <summary>The effect identity the spell carried, as the ruleset named it.</summary>
    public string Effect { get; }

    /// <summary>The outcome's own code, which is what a test and a diagnostic compare.</summary>
    public string Code { get; }

    /// <summary>What applying the effect did, in a sentence a person reads.</summary>
    public string Message { get; }

    /// <summary>The fight's own record of the attack the effect was applied as, or null when none was made.</summary>
    public CombatResult? Attack { get; }

    /// <summary>
    /// What the effect changed, as named readings of the state it changed, in the order it changed them.
    /// </summary>
    /// <remarks>
    /// The sentence says what happened and these say what the world now holds — a pool after the healing,
    /// the condition that was lifted, the place the party arrived in, the moment a light ends. They are
    /// published so a panel shows a cast's outcome from state rather than from the message's wording, and so
    /// a test can compare a number without parsing prose.
    /// </remarks>
    public IReadOnlyList<SpellEffectFact> Facts { get; }

    /// <summary>The effect was applied, and this is what it did.</summary>
    /// <param name="effect">The effect identity the spell carried.</param>
    /// <param name="message">What it did, in a sentence.</param>
    /// <param name="facts">What it changed, as readings of the state it changed.</param>
    /// <param name="attack">The fight's own record, when the effect was applied as an attack.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="ArgumentException">The effect or the message is blank.</exception>
    public static SpellApplicationOutcome Expressed(
        string effect,
        string message,
        IReadOnlyList<SpellEffectFact>? facts = null,
        CombatResult? attack = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effect);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new SpellApplicationOutcome(expressed: true, effect, "spell-effect-applied", message, attack, facts ?? []);
    }

    /// <summary>
    /// Nothing was expressed for the effect: the spell was cast and this build has no behaviour for it yet.
    /// </summary>
    /// <param name="effect">The effect identity the spell carried.</param>
    /// <param name="message">What a person should read about it.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="ArgumentException">The effect or the message is blank.</exception>
    public static SpellApplicationOutcome Unexpressed(string effect, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effect);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new SpellApplicationOutcome(expressed: false, effect, "spell-effect-unexpressed", message, attack: null, facts: []);
    }

    /// <inheritdoc />
    public override string ToString() => $"{Code}: {Message}";
}

/// <summary>
/// Where a spell goes once it has been learned, paid for, and aimed: the one application point every effect
/// is expressed behind.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is a seam, not a set of effects.</b> The casting mechanism resolves the spell, judges the
/// caster's mastery and pool, spends the points, resolves the aim, and hands the casting here; what the
/// spell then does is this interface's answer and nothing else's. The categories of effect a spell can have
/// — harm, healing, a resistance, a condition, light, travel, detection, and the utilities — are
/// implementations behind this seam, which is why the mechanism never grows a branch per effect and never
/// learns an effect's name.
/// </para>
/// <para>
/// <b>Every category is applied behind it, and each through the owner that holds the state it changes.</b>
/// Harm is an attack of the spell kind through the fight's own gated entry, so a spell's numbers, its target's
/// resistance, the condition it leaves, and the recovery it costs are the same mechanism a swing and a shot
/// use; health is given through the member's own pool; conditions are lifted and left through the member's own
/// condition state; a ward or a utility is a party-carried effect with a deadline on the one clock, read by
/// whichever answer it changes; a light is read against that clock's daylight; travel goes through the world's
/// own transition path; and a detection reports over the places and the population the world holds. A spell
/// whose effect this build expresses nothing for is still cast: the outcome says which effect it carried and
/// that nothing changed, rather than reporting a cast that silently did nothing.
/// </para>
/// <para>
/// <b>What the effect path offers beyond applying.</b> A game may also answer what a spell with no actor aim
/// may be pointed at (<see cref="ISpellAimRule"/>), what the party sees by (<see cref="IPartySightRule"/>),
/// and what effects spells have left running (<see cref="IRunningSpellEffects"/>) — all optional, all read by
/// the projection, and none of them a rule the casting mechanism learns.
/// </para>
/// <para>
/// <b>Judged before anything is paid.</b> <see cref="Judge"/> is asked first, because whether the caster may
/// act, whether the aim resolves, and whether the spell acts on something this build can aim at are facts
/// that must stop a cast before its points are spent; a cast the party paid for and that then could not be
/// carried out would be the worst of both.
/// </para>
/// </remarks>
public interface ISpellEffectRule
{
    /// <summary>Whether this casting may be carried out at all, asked before any spell point is spent.</summary>
    /// <param name="application">The casting, resolved but not yet paid for.</param>
    /// <returns>The refusal, or null when the casting may go ahead.</returns>
    SpellRefusal? Judge(SpellApplication application);

    /// <summary>Applies a casting the party has paid for.</summary>
    /// <param name="application">The casting, resolved and paid for.</param>
    /// <returns>What applying the effect did.</returns>
    SpellApplicationOutcome Apply(SpellApplication application);
}
