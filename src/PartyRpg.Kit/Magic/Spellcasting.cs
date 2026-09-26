using System.Globalization;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Magic;

/// <summary>One casting a screen asked for: which member casts, which spell, and what it is aimed at.</summary>
/// <remarks>
/// The member is the party's own index rather than a durable identity, exactly as a service lesson's member
/// and a skill raise's are: the screen was shown the party in its order and names the row it drew, and the
/// session resolves that row against the party it holds inside the same update. The spell is content's own
/// identity, and the target is the identity the projection published for it — a fight's combatant, in the
/// form the panel was handed — or empty when the spell's aim names nobody.
/// </remarks>
/// <param name="Member">The caster's place in the party, counted from zero.</param>
/// <param name="Spell">The spell the screen named, as content names it.</param>
/// <param name="Target">What the spell is aimed at, or empty when its aim names nobody.</param>
public readonly record struct SpellCastRequest(int Member, SpellId Spell, string Target);

/// <summary>What one casting did, or why nothing was cast, as a report a panel can show.</summary>
/// <remarks>
/// A result always says who cast, what was cast, what it cost, and what it was aimed at, so a caller can
/// read it unconditionally. A refusal changes nothing at all: no point is spent, no effect is applied, and
/// the spellbook and the fight are exactly as they were. A cast that landed carries the effect owner's own
/// outcome, including the case where this build expresses nothing for the effect — which is reported as a
/// cast that changed nothing rather than hidden.
/// </remarks>
public sealed record SpellCastResult
{
    private SpellCastResult(
        bool isCast,
        string code,
        string message,
        int member,
        string caster,
        SpellId spell,
        string spellName,
        int cost,
        string target,
        SpellApplicationOutcome? outcome)
    {
        IsCast = isCast;
        Code = code;
        Message = message;
        Member = member;
        Caster = caster;
        Spell = spell;
        SpellName = spellName;
        Cost = cost;
        Target = target;
        Outcome = outcome;
    }

    /// <summary>Whether the spell was cast.</summary>
    public bool IsCast { get; }

    /// <summary>The refusal's own code, empty when the spell was cast.</summary>
    public string Code { get; }

    /// <summary>What happened, in a sentence a person reads.</summary>
    public string Message { get; }

    /// <summary>The caster's place in the party, counted from zero.</summary>
    public int Member { get; }

    /// <summary>What the caster is called, empty when the party has no such member.</summary>
    public string Caster { get; }

    /// <summary>The spell that was named.</summary>
    public SpellId Spell { get; }

    /// <summary>What the spell is called, empty when nothing declares it.</summary>
    public string SpellName { get; }

    /// <summary>What the casting cost in spell points, zero when it was refused.</summary>
    public int Cost { get; }

    /// <summary>What the spell was aimed at, empty when its aim named nobody.</summary>
    public string Target { get; }

    /// <summary>What applying the effect did, or null when nothing was cast.</summary>
    public SpellApplicationOutcome? Outcome { get; }

    /// <summary>The spell was cast, and this is what applying its effect did.</summary>
    /// <param name="caster">What the caster is called.</param>
    /// <param name="member">The caster's place in the party.</param>
    /// <param name="spell">The spell that was cast.</param>
    /// <param name="cost">What it cost.</param>
    /// <param name="target">What it was aimed at, empty when its aim named nobody.</param>
    /// <param name="outcome">What applying the effect did.</param>
    /// <returns>The result.</returns>
    /// <exception cref="ArgumentNullException">No outcome was supplied.</exception>
    public static SpellCastResult Cast(
        string caster,
        int member,
        SpellDefinition spell,
        int cost,
        string target,
        SpellApplicationOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        return new SpellCastResult(
            isCast: true,
            code: outcome.Code,
            message: string.Create(
                CultureInfo.InvariantCulture,
                $"{caster} casts {spell.Name} for {cost} spell point(s): {outcome.Message}"),
            member,
            caster,
            spell.Id,
            spell.Name,
            cost,
            target,
            outcome);
    }

    /// <summary>Nothing was cast, and this is why.</summary>
    /// <param name="member">The caster's place in the party.</param>
    /// <param name="caster">What the caster is called, empty when the party has no such member.</param>
    /// <param name="spell">The spell that was named.</param>
    /// <param name="spellName">What the spell is called, empty when nothing declares it.</param>
    /// <param name="refusal">Why nothing was cast.</param>
    /// <returns>The result.</returns>
    /// <exception cref="ArgumentNullException">No refusal was supplied.</exception>
    public static SpellCastResult Refused(
        int member,
        string caster,
        SpellId spell,
        string spellName,
        SpellRefusal refusal)
    {
        ArgumentNullException.ThrowIfNull(refusal);
        return new SpellCastResult(
            isCast: false,
            refusal.Code,
            refusal.Message,
            member,
            caster,
            spell,
            spellName,
            cost: 0,
            target: string.Empty,
            outcome: null);
    }

    /// <inheritdoc />
    public override string ToString() => IsCast ? Message : $"{Code}: {Message}";
}

/// <summary>
/// The one workflow that casts a spell: resolve the caster and the spell, judge the school's mastery for
/// that spell's tier, resolve the aim, ask the effect path whether the casting may go ahead, pay the spell
/// points through the caster's own pool, and hand the casting to the effect path.
/// </summary>
/// <remarks>
/// <para>
/// <b>Exploration and combat are one workflow.</b> Nothing here asks which mode the session is in: the same
/// resolution, the same mastery judgement, the same payment, and the same application point serve a spell
/// cast in a street and one cast in the middle of a fight. What differs is what the effect owner can see —
/// a fight, when the session is playing one — and that is the effect owner's business rather than a branch
/// here.
/// </para>
/// <para>
/// <b>Nothing is paid before the casting is known to be possible.</b> The mastery, the aim, and the
/// caster's own ability to act are all judged before a point is spent, so a refused cast leaves the pool
/// exactly as it stood. The payment goes through the member's own pool — the resource owner the party
/// already has — and never through a purse or a counter of the kit's own.
/// </para>
/// <para>
/// <b>One caster, not the whole party.</b> A casting names the member who casts it, because its cost, its
/// mastery requirement, and its quick-spell slot are that character's: the mechanism has no notion of an
/// active character and does not need one to ask one member's own state a question.
/// </para>
/// </remarks>
public sealed class Spellcasting
{
    private readonly PartyEntity _party;
    private readonly ISpellRule _rule;
    private readonly ISpellEffectRule? _effects;
    private readonly CombatState? _fight;
    private readonly Func<SkillTier, string> _rungName;

    /// <summary>Creates the casting workflow over one party.</summary>
    /// <param name="party">The party whose members cast.</param>
    /// <param name="rule">This game's answers about its own magic.</param>
    /// <param name="effects">
    /// Where a casting goes once it is paid for, or null when this session composes no effect path. A
    /// session without one still resolves and refuses by name; it cannot cast, and says so rather than
    /// spending a point on a spell nothing would apply.
    /// </param>
    /// <param name="fight">The fight the party is in, or null when it is in none.</param>
    /// <param name="rungName">
    /// What this game calls one rung of a skill's ladder, or null to read a rung as its number. A spell's
    /// tier is a rung of its school's ladder, so a refusal that names it reads in the game's own words when
    /// the skill owner supplies them.
    /// </param>
    /// <exception cref="ArgumentNullException">No party or no spell policy was supplied.</exception>
    public Spellcasting(
        PartyEntity party,
        ISpellRule rule,
        ISpellEffectRule? effects = null,
        CombatState? fight = null,
        Func<SkillTier, string>? rungName = null)
    {
        _party = party ?? throw new ArgumentNullException(nameof(party));
        _rule = rule ?? throw new ArgumentNullException(nameof(rule));
        _effects = effects;
        _fight = fight;
        _rungName = rungName ?? (tier => tier.Value.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>This game's answers about its own magic, which the panel reads through this owner.</summary>
    public ISpellRule Rule => _rule;

    /// <summary>The party whose members cast.</summary>
    public PartyEntity Party => _party;

    /// <summary>The fight every casting is judged against, or null when the party is in none.</summary>
    public CombatState? Fight => _fight;

    /// <summary>
    /// Where a casting goes once it is paid for, or null when this session composes no effect path.
    /// </summary>
    /// <remarks>
    /// It is published because the effect path is the only owner that knows what a spell may be pointed at
    /// when its aim names no actor — the places a portal reaches, the things a hand may move — and because
    /// the effects spells have left running are its state. A reader asks it rather than keeping a second copy
    /// of either answer.
    /// </remarks>
    public ISpellEffectRule? Effects => _effects;

    /// <summary>What the last casting did, or why it did nothing.</summary>
    public SpellCastResult? Last { get; private set; }

    /// <summary>How many spell points the caster would pay for one casting right now.</summary>
    /// <param name="caster">The member who would cast.</param>
    /// <param name="spell">The spell being priced.</param>
    /// <returns>What the casting costs, never negative.</returns>
    /// <exception cref="ArgumentNullException">No member was supplied.</exception>
    public int CostFor(PartyMember caster, SpellDefinition spell)
    {
        ArgumentNullException.ThrowIfNull(caster);
        return _rule.CostFor(caster, spell);
    }

    /// <summary>Casts one spell for one member, or refuses by name and changes nothing.</summary>
    /// <param name="request">Who casts, what they cast, and what it is aimed at.</param>
    /// <returns>What the casting did, or why nothing was cast.</returns>
    public SpellCastResult Cast(SpellCastRequest request)
    {
        if (request.Member < 0 || request.Member >= _party.Members.Count)
        {
            return Record(SpellCastResult.Refused(
                request.Member,
                caster: string.Empty,
                request.Spell,
                spellName: string.Empty,
                SpellRefusal.NoSuchMember(request.Member)));
        }

        PartyMember caster = _party.Members[request.Member];
        SpellDefinition spell = _rule.Catalog.Read(request.Spell);
        if (!_rule.Catalog.Declares(request.Spell))
        {
            return Record(SpellCastResult.Refused(request.Member, caster.Profile.Name, request.Spell, spellName: string.Empty, SpellRefusal.Unknown(request.Spell.Value)));
        }

        // Knowing a spell and being allowed to cast it are different questions, and the answer names which
        // one blocked the casting: a spell the character never learned is refused before its mastery or its
        // price is even considered.
        if (!caster.Spells.Knows(request.Spell))
        {
            return Record(SpellCastResult.Refused(request.Member, caster.Profile.Name, request.Spell, spell.Name, SpellRefusal.NotKnown(caster.Profile.Name, spell.Name)));
        }

        SkillTier held = caster.Skills.TierOf(spell.SchoolSkill);
        if (held.Value < spell.Tier.Value)
        {
            return Record(SpellCastResult.Refused(
                request.Member,
                caster.Profile.Name,
                request.Spell,
                spell.Name,
                SpellRefusal.MasteryTooLow(caster.Profile.Name, spell.Name, _rungName(spell.Tier), _rungName(held))));
        }

        int cost = _rule.CostFor(caster, spell);
        int available = caster.Resources.SpellPoints.Current;
        if (available < cost)
        {
            return Record(SpellCastResult.Refused(request.Member, caster.Profile.Name, request.Spell, spell.Name, SpellRefusal.NotEnoughPoints(caster.Profile.Name, spell.Name, cost, available)));
        }

        CombatantId casterId = CombatantId.Of(caster.Id);
        if (!Resolve(spell, request.Target, caster, casterId, out CombatantId? target, out string targetName, out SpellRefusal? refused))
        {
            return Record(SpellCastResult.Refused(request.Member, caster.Profile.Name, request.Spell, spell.Name, refused!));
        }

        SpellApplication application = new(_party, caster, casterId, spell, _fight, target, targetName);
        if (_effects is not { } effects)
        {
            return Record(SpellCastResult.Refused(
                request.Member,
                caster.Profile.Name,
                request.Spell,
                spell.Name,
                SpellRefusal.NoEffectPath()));
        }

        // The effect owner judges first: whether the caster may act at all and whether the casting can be
        // carried out are facts that must stop the cast before its points are spent.
        if (effects.Judge(application) is { } judged)
        {
            return Record(SpellCastResult.Refused(request.Member, caster.Profile.Name, request.Spell, spell.Name, judged));
        }

        if (!caster.Resources.TrySpendSpellPoints(cost))
        {
            // The pool answered the same question a moment ago, so this is a state that changed between two
            // readings inside one call: it is reported rather than charged, and nothing is applied.
            return Record(SpellCastResult.Refused(
                request.Member,
                caster.Profile.Name,
                request.Spell,
                spell.Name,
                SpellRefusal.NotEnoughPoints(caster.Profile.Name, spell.Name, cost, caster.Resources.SpellPoints.Current)));
        }

        SpellApplicationOutcome outcome = effects.Apply(application);
        return Record(SpellCastResult.Cast(caster.Profile.Name, request.Member, spell, cost, targetName, outcome));
    }

    /// <summary>
    /// Resolves what a casting was aimed at, or says why the aim names nothing this session can offer.
    /// </summary>
    /// <remarks>
    /// The aim is the definition's own answer and the target is the identity the projection published, so a
    /// screen names what it drew and the workflow judges it against the spell: an aim that names nobody
    /// takes no target, one that names the caster takes the caster, and one that names a member of a side
    /// takes an actor of that side and nothing else. A target on the wrong side is refused as an invalid
    /// aim rather than turned on the party or healed an enemy.
    /// </remarks>
    private bool Resolve(
        SpellDefinition spell,
        string named,
        PartyMember caster,
        CombatantId casterId,
        out CombatantId? target,
        out string targetName,
        out SpellRefusal? refused)
    {
        target = null;
        targetName = string.Empty;
        refused = null;
        switch (spell.Targeting)
        {
            case SpellTargeting.None:
            case SpellTargeting.Party:
                // A spell whose aim names no actor may still be told what it acts on — the place a portal
                // reaches, the thing a hand moves — and that word travels to the effect owner, which is the
                // only thing that can judge it. It is carried unread, exactly as the spell's effect identity
                // is: the mechanism has no list of destinations and no notion of a thing.
                targetName = named;
                return true;

            case SpellTargeting.Caster:
                target = casterId;
                targetName = _fight?.Find(casterId)?.Name ?? caster.Profile.Name;
                return true;

            case SpellTargeting.Ally:
            {
                if (named.Length == 0)
                {
                    refused = SpellRefusal.NoTarget(spell.Name, SpellTargetings.WireName(spell.Targeting));
                    return false;
                }

                foreach (PartyMember member in _party.Members)
                {
                    if (!string.Equals(CombatantId.Of(member.Id).ToString(), named, StringComparison.Ordinal)) continue;
                    target = CombatantId.Of(member.Id);
                    targetName = member.Profile.Name;
                    return true;
                }

                refused = SpellRefusal.NoValidTarget(spell.Name, named);
                return false;
            }

            default:
            {
                if (named.Length == 0)
                {
                    refused = SpellRefusal.NoTarget(spell.Name, SpellTargetings.WireName(spell.Targeting));
                    return false;
                }

                // A spell aimed at an opponent is aimed at a creature the fight holds: a member of the
                // party named for such a spell is not a target that can be turned on, and a place with no
                // fight in it has nobody to aim at.
                if (_fight is { } fight)
                {
                    foreach (Combatant combatant in fight.Combatants)
                    {
                        if (combatant.Subject.Member is not null) continue;
                        if (!string.Equals(combatant.Id.ToString(), named, StringComparison.Ordinal)) continue;
                        target = combatant.Id;
                        targetName = combatant.Name;
                        return true;
                    }
                }

                refused = SpellRefusal.NoValidTarget(spell.Name, named);
                return false;
            }
        }
    }

    private SpellCastResult Record(SpellCastResult result)
    {
        Last = result;
        return result;
    }
}
