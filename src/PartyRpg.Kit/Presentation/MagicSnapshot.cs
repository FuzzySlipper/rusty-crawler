using System.Globalization;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Skills;
using PartyRpg.Kit.Time;

namespace PartyRpg.Kit.Magic;

/// <summary>Something a spell whose aim names no actor may be pointed at, as the effect path offered it.</summary>
/// <remarks>
/// Published per spell rather than once for the session, because what a spell may name depends on the spell
/// and on where the party has been: a portal reaches the places it has visited, and the list changes as it
/// travels. A row with no aims is a spell that names nothing at all.
/// </remarks>
/// <param name="Aim">The identity a casting echoes back to choose this.</param>
/// <param name="Name">What a person reads for it.</param>
/// <param name="Kind">What sort of thing it is, as the game's own word.</param>
public sealed record SpellAimSnapshot(string Aim, string Name, string Kind)
{
    /// <summary>Writes one thing a spell may be pointed at when its aim names no actor.</summary>
    /// <param name="builder">The projection being built.</param>
    /// <returns>The row's node.</returns>
    internal uint Write(UiValueBuilder builder) =>
        builder.Object(
            ("aim", builder.String(Aim)),
            ("name", builder.String(Name)),
            ("kind", builder.String(Kind)));
}

/// <summary>One effect a spell has left running, as the panel shows it.</summary>
/// <param name="Effect">The effect identity the spell left, which the panel shows and never interprets.</param>
/// <param name="Magnitude">The magnitude it acts at.</param>
/// <param name="EndsAt">When the clock ends it, formatted for a person, empty when nothing states an end.</param>
public sealed record SpellRunningSnapshot(string Effect, int Magnitude, string EndsAt)
{
    /// <summary>Writes one effect running on the band.</summary>
    /// <param name="builder">The projection being built.</param>
    /// <returns>The row's node.</returns>
    internal uint Write(UiValueBuilder builder) =>
        builder.Object(
            ("effect", builder.String(Effect)),
            ("magnitude", builder.Number(Magnitude)),
            ("endsAt", builder.String(EndsAt)));
}

/// <summary>One effect a spell has left running on one character, as the panel shows it.</summary>
/// <remarks>
/// The row names the character because that is what makes a per-character ward visible at all: a panel that
/// listed the effect without saying whose it was would leave a player unable to tell a blessing they cast on
/// one member from one the whole party carries.
/// </remarks>
/// <param name="Member">The member's durable identity, which the row belongs to.</param>
/// <param name="Name">What the member is called.</param>
/// <param name="Effect">The effect identity the spell left, which the panel shows and never interprets.</param>
/// <param name="Magnitude">The magnitude it acts at.</param>
/// <param name="EndsAt">When the clock ends it, formatted for a person, empty when nothing states an end.</param>
public sealed record SpellMemberRunningSnapshot(string Member, string Name, string Effect, int Magnitude, string EndsAt)
{
    /// <summary>Writes one effect running on one character, naming them.</summary>
    /// <param name="builder">The projection being built.</param>
    /// <returns>The row's node.</returns>
    internal uint Write(UiValueBuilder builder) =>
        builder.Object(
            ("member", builder.String(Member)),
            ("name", builder.String(Name)),
            ("effect", builder.String(Effect)),
            ("magnitude", builder.Number(Magnitude)),
            ("endsAt", builder.String(EndsAt)));
}

/// <summary>One item the party holds that carries a spell, as the panel shows it.</summary>
/// <remarks>
/// A scroll and a wand are the same row because they are the same fact — an item, the spell it carries, and
/// what is left of it — and what differs is how using it spends the item, which the row states rather than
/// the panel inferring it. The charges are read from the party's own item state against the game's own
/// capacity, so a wand's row and a wand's attack cannot disagree about how full it is.
/// </remarks>
/// <param name="Item">The instance's durable identity, which a use command echoes back.</param>
/// <param name="Name">What a person reads for it.</param>
/// <param name="Kind">How using it spends it: <c>consumed</c> for an item a use uses up, <c>charged</c> for one that holds uses.</param>
/// <param name="Spell">The spell it carries, as content names it.</param>
/// <param name="SpellName">What that spell is called.</param>
/// <param name="Targeting">
/// What the spell it carries is aimed at, as the wire spells it, which is what tells a screen whether using
/// the item needs a target and which actors it may offer — read from the game's own reading of the spell
/// rather than from whether some member happens to know it.
/// </param>
/// <param name="Charges">How many uses it holds now; zero for an item a use uses up.</param>
/// <param name="ChargesMax">How many uses its kind holds when full; zero for an item a use uses up.</param>
/// <param name="Wielded">Whether a member has it equipped, which is what an item that holds charges needs.</param>
/// <param name="Member">The member wearing it, empty when nobody does.</param>
/// <param name="TargetSide">
/// Which side of a fight the actor its spell names stands on — <c>party</c> or <c>opposition</c>, or <c>any</c> when
/// it may name an actor of either side — or empty when the spell names nobody, so a screen offers the actors a use may name without pairing an aim with a side.
/// </param>
public sealed record SpellItemSnapshot(
    string Item,
    string Name,
    string Kind,
    string Spell,
    string SpellName,
    string Targeting,
    int Charges,
    int ChargesMax,
    bool Wielded,
    string Member,
    string TargetSide = "")
{
    /// <summary>Writes one item the party carries that holds a spell.</summary>
    /// <param name="builder">The projection being built.</param>
    /// <param name="magic">The block the item is listed in, which says who could use it and on what.</param>
    /// <returns>The row's node.</returns>
    internal uint Write(UiValueBuilder builder, MagicSnapshot magic) =>
        builder.Object(
            ("item", builder.String(Item)),
            ("name", builder.String(Name)),
            ("kind", builder.String(Kind)),
            ("spell", builder.String(Spell)),
            ("spellName", builder.String(SpellName)),
            ("targeting", builder.String(Targeting)),
            ("charges", builder.Number(Charges)),
            ("chargesMax", builder.Number(ChargesMax)),
            ("wielded", builder.Boolean(Wielded)),
            ("member", builder.String(Member)),
            ("targetSide", builder.String(TargetSide)),
            // An item is used by a member on what its spell names, so it is offered while there is somebody
            // to use it and something on its side to name.
            ("canUse", builder.Boolean(magic.Members.Count > 0 && magic.Aimable(TargetSide))));
}

/// <summary>One reading a cast left behind, as the panel shows it.</summary>
/// <param name="Name">What the reading is about.</param>
/// <param name="Value">What it reads as.</param>
public sealed record SpellFactSnapshot(string Name, string Value)
{
    /// <summary>Writes one named reading of the state the last casting changed.</summary>
    /// <param name="builder">The projection being built.</param>
    /// <returns>The row's node.</returns>
    internal uint Write(UiValueBuilder builder) =>
        builder.Object(
            ("name", builder.String(Name)),
            ("value", builder.String(Value)));
}

/// <summary>One spell as a panel shows it: what it is, what it costs its caster, and what it is aimed at.</summary>
/// <remarks>
/// A row is published for every spell the member knows, with the price this caster's own mastery gives it
/// rather than a base number the screen would have to adjust: what a casting costs is the ruleset's answer
/// for that member, and a panel that multiplied anything itself would be a second copy of the rule. The
/// rung the spell asks for is published as the game's own word beside the rung the member holds, so a
/// refusal reads in the game's vocabulary.
/// </remarks>
/// <param name="Spell">Which spell the row is, as content names it.</param>
/// <param name="Name">What the spell is called.</param>
/// <param name="School">Which school of magic it belongs to.</param>
/// <param name="Tier">What the rung of the school's ladder the spell asks for is called.</param>
/// <param name="TierRung">That rung's number, which a screen compares with nothing.</param>
/// <param name="Cost">What one casting costs this member in spell points.</param>
/// <param name="Targeting">What the spell is aimed at, as the wire spells it.</param>
/// <param name="Effect">The effect identity the spell carries, which the panel shows and never interprets.</param>
/// <param name="Aims">What the spell may be pointed at, empty when its aim names no such thing.</param>
/// <param name="TargetSide">
/// Which side of a fight the actor the spell names stands on — <c>party</c> or <c>opposition</c>, or <c>any</c> when
/// it may name an actor of either side — or empty when the spell names nobody, so a screen offers the actors a casting may name without pairing an aim with a side.
/// </param>
public sealed record SpellRowSnapshot(
    string Spell,
    string Name,
    string School,
    string Tier,
    int TierRung,
    int Cost,
    string Targeting,
    string Effect,
    IReadOnlyList<SpellAimSnapshot> Aims,
    string TargetSide = "")
{
    /// <summary>Writes one spell a member knows: what it costs that caster and what it is aimed at.</summary>
    /// <param name="builder">The projection being built.</param>
    /// <param name="magic">The block the spell is listed in, whose targets say whether it has anybody to name.</param>
    /// <returns>The row's node.</returns>
    internal uint Write(UiValueBuilder builder, MagicSnapshot magic) =>
        builder.Object(
            ("spell", builder.String(Spell)),
            ("name", builder.String(Name)),
            ("school", builder.String(School)),
            ("tier", builder.String(Tier)),
            ("tierRung", builder.Number(TierRung)),
            ("cost", builder.Number(Cost)),
            ("targeting", builder.String(Targeting)),
            ("effect", builder.String(Effect)),
            ("aims", builder.Array([.. Aims.Select(aim => aim.Write(builder))])),
            ("targetSide", builder.String(TargetSide)),
            // A spell that must name an actor, has nobody on its side to name, and has nothing else it may
            // be pointed at is one a casting would be refused on before a point was spent.
            ("canCast", builder.Boolean(magic.Aimable(TargetSide) || Aims.Count > 0)));
}

/// <summary>One spell on a school's page of a member's spellbook, learned or not.</summary>
/// <param name="Spell">Which spell, as content names it.</param>
/// <param name="Name">What it is called.</param>
/// <param name="Tier">The game's word for the rung of the school it asks for.</param>
/// <param name="Known">Whether the member has learned it.</param>
/// <param name="Cost">What one casting would cost this member.</param>
/// <param name="Refusal">Why this member could not cast it now — not learned, too low a rung, too few points — or null.</param>
public sealed record SpellPageEntrySnapshot(string Spell, string Name, string Tier, bool Known, int Cost, Refusal? Refusal)
{
    internal uint Write(UiValueBuilder builder) =>
        builder.Object(
            ("spell", builder.String(Spell)),
            ("name", builder.String(Name)),
            ("tier", builder.String(Tier)),
            ("known", builder.Boolean(Known)),
            ("cost", builder.Number(Cost)),
            ("refusalCode", builder.String(Refusal?.Code ?? string.Empty)),
            ("refusal", builder.String(Refusal?.Message ?? string.Empty)));
}

/// <summary>One school's page of a member's spellbook: the rung the member holds and every spell the school teaches.</summary>
/// <param name="School">The school, as the catalog names it.</param>
/// <param name="Held">The game's word for the rung of the school the member holds.</param>
/// <param name="Spells">Every spell of the school, in the catalog's order.</param>
public sealed record SpellPageSnapshot(string School, string Held, IReadOnlyList<SpellPageEntrySnapshot> Spells)
{
    internal uint Write(UiValueBuilder builder) =>
        builder.Object(
            ("school", builder.String(School)),
            ("held", builder.String(Held)),
            ("spells", builder.Array([.. Spells.Select(spell => spell.Write(builder))])));
}

/// <summary>One member's spellbook and what casting from it costs, as the panel shows it.</summary>
/// <param name="Index">The member's place in the party, counted from zero, which a cast control names.</param>
/// <param name="Member">The member's durable identity.</param>
/// <param name="Name">What the member is called.</param>
/// <param name="Class">The member's class, as content names it.</param>
/// <param name="SpellPoints">How many spell points the member has left.</param>
/// <param name="SpellPointsMax">How many the member's class, level, and scores add up to.</param>
/// <param name="QuickSpell">The spell in the member's quick slot, empty when the slot holds none.</param>
/// <param name="QuickSpellName">What that spell is called, empty when the slot holds none.</param>
/// <param name="Spells">The spells the member knows, in the order they were learned.</param>
public sealed record SpellMemberSnapshot(
    int Index,
    string Member,
    string Name,
    string Class,
    int SpellPoints,
    int SpellPointsMax,
    string QuickSpell,
    string QuickSpellName,
    IReadOnlyList<SpellRowSnapshot> Spells)
{
    /// <summary>
    /// The member's spellbook by school: a page for every school they hold a rung of or know a spell from, each
    /// listing every spell the school teaches with whether it is learned and what would stop this member casting it.
    /// </summary>
    public IReadOnlyList<SpellPageSnapshot> Pages { get; init; } = [];

    /// <summary>Writes one member's spellbook.</summary>
    /// <param name="builder">The projection being built.</param>
    /// <param name="magic">The block the member is listed in.</param>
    /// <returns>The row's node.</returns>
    internal uint Write(UiValueBuilder builder, MagicSnapshot magic) =>
        builder.Object(
            ("index", builder.Number(Index)),
            ("member", builder.String(Member)),
            ("name", builder.String(Name)),
            ("class", builder.String(Class)),
            ("spellPoints", builder.Number(SpellPoints)),
            ("spellPointsMax", builder.Number(SpellPointsMax)),
            ("quickSpell", builder.String(QuickSpell)),
            ("quickSpellName", builder.String(QuickSpellName)),
            ("spells", builder.Array([.. Spells.Select(spell => spell.Write(builder, magic))])),
            ("pages", builder.Array([.. Pages.Select(page => page.Write(builder))])));
}

/// <summary>One actor a casting may be aimed at, as the fight and the party stand now.</summary>
/// <remarks>
/// The identity published here is the one a cast command echoes back, and the side is what a screen matches
/// a spell's aim against: a spell aimed at an opponent offers the opposition's rows and nothing else, so the
/// panel chooses nothing and the workflow still judges the aim it is handed.
/// </remarks>
/// <param name="Target">The identity a cast command names.</param>
/// <param name="Name">What the actor is called.</param>
/// <param name="Side">Which side the actor is on, as the wire spells it: <c>party</c> or <c>opposition</c>.</param>
public sealed record SpellTargetSnapshot(string Target, string Name, string Side)
{
    /// <summary>Writes one actor a casting could name, with the side it is on.</summary>
    /// <param name="builder">The projection being built.</param>
    /// <returns>The row's node.</returns>
    internal uint Write(UiValueBuilder builder) =>
        builder.Object(
            ("target", builder.String(Target)),
            ("name", builder.String(Name)),
            ("side", builder.String(Side)));
}

/// <summary>What the party can cast, what it may aim at, and what the last casting did.</summary>
/// <remarks>
/// <para>
/// This is the catalog read through the owner: each member's spellbook with the price this caster's mastery
/// gives each spell, the pool the casting spends, the quick slot, and the actors a casting may name. A
/// session whose ruleset answered no magic policy, or one that holds no party, publishes <see cref="None"/>
/// and the panel says so rather than showing a spellbook nothing could cast from.
/// </para>
/// <para>
/// The last casting is published here rather than beside the fight, because it is this block's own event: it
/// spent spell points and handed a spell to the effect path, and a panel that showed the pool without saying
/// what left it would leave a player checking the wrong number.
/// </para>
/// </remarks>
/// <param name="Available">Whether the session holds a spell policy and a party to cast from.</param>
/// <param name="Members">The party's members and their spellbooks, in the party's own order.</param>
/// <param name="Targets">Every actor a casting could name, the party's own members included.</param>
/// <param name="Outcome">What the last casting was: <c>none</c>, or the outcome's own code.</param>
/// <param name="Member">The caster's place in the party, zero before any casting.</param>
/// <param name="Caster">What the caster is called, empty before any casting.</param>
/// <param name="Spell">The spell the last casting named, empty before any casting.</param>
/// <param name="Cost">What it cost in spell points, zero when it was refused or none happened.</param>
/// <param name="Target">What it was aimed at, empty when its aim named nobody.</param>
/// <param name="Effect">The effect identity that was handed over, empty before any casting.</param>
/// <param name="Code">The last outcome's code, empty before any casting.</param>
/// <param name="Message">What the last casting reported, empty before anybody has cast.</param>
/// <param name="Facts">What the last casting changed, as readings of the state it changed.</param>
/// <param name="Running">The effects spells have left running on the party, in the order they were applied.</param>
/// <param name="Sight">What the party sees by now, as the wire spells it, empty when nothing states it.</param>
/// <param name="MemberRunning">
/// The effects spells have left running on the party's own characters, in the order they were applied, each
/// naming the character it is on. This is the block that makes a ward cast on one member visible as theirs.
/// </param>
/// <param name="Items">
/// The items the party carries that hold a spell — a scroll it can read and a wand it can fire — with what is
/// left of each, in the order the pack holds them. A session whose ruleset reads no spells for its items
/// publishes none.
/// </param>
/// <param name="Source">What carried the last casting, empty when it came from the caster's own spellbook.</param>
public sealed record MagicSnapshot(
    bool Available,
    IReadOnlyList<SpellMemberSnapshot> Members,
    IReadOnlyList<SpellTargetSnapshot> Targets,
    string Outcome,
    int Member,
    string Caster,
    string Spell,
    int Cost,
    string Target,
    string Effect,
    string Code,
    string Message,
    IReadOnlyList<SpellFactSnapshot> Facts,
    IReadOnlyList<SpellRunningSnapshot> Running,
    string Sight,
    IReadOnlyList<SpellMemberRunningSnapshot> MemberRunning,
    IReadOnlyList<SpellItemSnapshot> Items,
    string Source)
{
    /// <summary>No magic: nobody's spellbook is readable and nothing can be cast.</summary>
    public static MagicSnapshot None => new(
        Available: false,
        Members: [],
        Targets: [],
        Outcome: "none",
        Member: 0,
        Caster: string.Empty,
        Spell: string.Empty,
        Cost: 0,
        Target: string.Empty,
        Effect: string.Empty,
        Code: string.Empty,
        Message: string.Empty,
        Facts: [],
        Running: [],
        Sight: string.Empty,
        MemberRunning: [],
        Items: [],
        Source: string.Empty);

    /// <summary>Reads every member's magic out of the casting owner, or none when it holds no policy.</summary>
    /// <param name="casting">The session's casting workflow, or null when it composes none.</param>
    /// <returns>The magic the panel shows, or <see cref="None"/> when there is nothing to read.</returns>
    public static MagicSnapshot From(Spellcasting? casting)
    {
        if (casting is not { } owner) return None;

        // The effect path is asked for the two things only it knows: what a spell may be pointed at when its
        // aim names no actor, and what it has left running. A path that answers neither publishes a spellbook
        // whose spells are all aimed at actors or nobody, which is what a game with no travel states.
        ISpellAimRule? aims = owner.Magic.Aim;
        List<SpellMemberSnapshot> members = [];
        for (int index = 0; index < owner.Party.Members.Count; index++)
        {
            PartyMember member = owner.Party.Members[index];
            List<SpellRowSnapshot> spells = [];
            foreach (SpellId spell in member.Spells.Known)
            {
                SpellDefinition definition = owner.Rule.Catalog.Read(spell);
                spells.Add(new SpellRowSnapshot(
                    spell.Value,
                    definition.Name,
                    definition.School,
                    GameNames.Tier(owner.Names, definition.Tier),
                    definition.Tier.Value,
                    owner.CostFor(member, definition),
                    SpellTargetings.WireName(definition.Targeting),
                    definition.Effect,
                    RowAims(aims, definition),
                    SideOf(definition.Targeting)));
            }

            SpellId? quick = member.Spells.QuickSpell;
            members.Add(new SpellMemberSnapshot(
                index,
                member.Id.ToString(),
                member.Profile.Name,
                member.Profile.Class.Value,
                member.Resources.SpellPoints.Current,
                owner.Rule.SpellPointCapacity(member),
                quick?.Value ?? string.Empty,
                quick is { } chosen ? owner.Rule.Catalog.Read(chosen).Name : string.Empty,
                spells)
            {
                Pages = Pages(owner, member),
            });
        }

        List<SpellTargetSnapshot> targets = [];
        if (owner.Fight is { } fight)
        {
            foreach (Combatant combatant in fight.Combatants)
            {
                // A combatant the fight has read as down is still offered: a body is a thing a spell can be
                // aimed at, and the workflow refuses an aim it cannot carry out rather than the panel
                // hiding a target the state still holds.
                targets.Add(new SpellTargetSnapshot(
                    combatant.Id.ToString(),
                    combatant.Name,
                    Presentation.SessionProjection.WireName(combatant.Side == CombatSide.Party ? CombatSide.Party : CombatSide.Opposition)));
            }
        }
        else
        {
            foreach (PartyMember member in owner.Party.Members)
            {
                targets.Add(new SpellTargetSnapshot(CombatantId.Of(member.Id).ToString(), member.Profile.Name, Presentation.SessionProjection.WireName(CombatSide.Party)));
            }
        }

        List<SpellFactSnapshot> facts = [];
        if (owner.Last?.Outcome is { } outcome)
        {
            foreach (SpellEffectFact fact in outcome.Facts) facts.Add(new SpellFactSnapshot(fact.Name, fact.Value));
        }

        List<SpellRunningSnapshot> running = [];
        if (owner.Magic.Running is { } ledger)
        {
            foreach (RunningSpellEffect effect in ledger.Running)
            {
                running.Add(new SpellRunningSnapshot(effect.Effect.Value, effect.Magnitude, Moment(effect.EndsAt)));
            }
        }

        // What a spell has left on each character, read from the same ledger: a ward cast on one member is
        // that member's row, which is how a player sees that the rest of the band is unaffected.
        List<SpellMemberRunningSnapshot> memberRunning = [];
        if (owner.Magic.Members is { } onMembers)
        {
            foreach (RunningSpellEffect effect in onMembers.RunningOnMembers)
            {
                if (effect.Member is not { } id) continue;
                string name = owner.Party.TryMember(id, out PartyMember? on) && on is not null ? on.Profile.Name : id.ToString();
                memberRunning.Add(new SpellMemberRunningSnapshot(id.ToString(), name, effect.Effect.Value, effect.Magnitude, Moment(effect.EndsAt)));
            }
        }

        // The items the party carries that hold a spell: a scroll it could read and a wand it could fire, each
        // with how much of it is left. The reading is the game's own rows and the count is the party's item
        // state, so nothing here decides what an item is worth or how full it is.
        List<SpellItemSnapshot> items = [];
        if (owner.Magic.Items is { } spellItems)
        {
            foreach (ItemInstance item in owner.Party.Items)
            {
                if (spellItems.Reading(item.Definition) is not { } reading) continue;
                SpellDefinition carried = owner.Rule.Catalog.Read(reading.Spell);
                int left = reading.ConsumedByUse ? 0 : Math.Max(0, (item.State.ChargeCapacity ?? reading.Charges) - item.State.ChargesSpent);
                string worn = item.Custody.IsEquipped && owner.Party.TryMember(item.Custody.Member, out PartyMember? wearer) && wearer is not null
                    ? wearer.Profile.Name
                    : string.Empty;
                items.Add(new SpellItemSnapshot(
                    item.Id.ToString(),
                    GameNames.Item(owner.Names, item.Definition),
                    reading.ConsumedByUse ? "consumed" : "charged",
                    reading.Spell.Value,
                    carried.Name,
                    SpellTargetings.WireName(carried.Targeting),
                    left,
                    item.State.ChargeCapacity ?? reading.Charges,
                    item.Custody.IsEquipped,
                    worn,
                    SideOf(carried.Targeting)));
            }
        }

        string sight = owner.Magic.Sight is { } light ? PartySights.WireName(light.Sight) : string.Empty;
        if (owner.Last is { } last)
        {
            return new MagicSnapshot(
                Available: true,
                members,
                targets,
                Outcome: last.IsCast ? "cast" : "refused",
                Member: last.Member,
                Caster: last.Caster,
                Spell: last.Spell.Value,
                Cost: last.Cost,
                Target: last.Target,
                Effect: last.Outcome?.Effect ?? string.Empty,
                Code: last.Code,
                Message: last.Message,
                Facts: facts,
                Running: running,
                Sight: sight,
                MemberRunning: memberRunning,
                Items: items,
                Source: last.Source);
        }

        return new MagicSnapshot(
            Available: true,
            members,
            targets,
            Outcome: "none",
            Member: 0,
            Caster: string.Empty,
            Spell: string.Empty,
            Cost: 0,
            Target: string.Empty,
            Effect: string.Empty,
            Code: string.Empty,
            Message: string.Empty,
            Facts: facts,
            Running: running,
            Sight: sight,
            MemberRunning: memberRunning,
            Items: items,
            Source: string.Empty);
    }

    /// <summary>What one spell may be pointed at right now, empty when its aim names no such thing.</summary>
    private static IReadOnlyList<SpellAimSnapshot> RowAims(ISpellAimRule? aims, SpellDefinition spell)
    {
        if (aims is null) return [];
        List<SpellAimSnapshot> offered = [];
        foreach (SpellAim aim in aims.AimsOf(spell)) offered.Add(new SpellAimSnapshot(aim.Aim, aim.Name, aim.Kind));
        return offered;
    }

    /// <summary>
    /// The side a spell's named target stands on, as the wire spells it: <see cref="AnySide"/> when it may name an
    /// actor of either side, and empty when it names nobody.
    /// </summary>
    private static string SideOf(SpellTargeting targeting) =>
        targeting == SpellTargeting.Either
            ? AnySide
            : SpellTargetings.Side(targeting) is { } side ? Presentation.SessionProjection.WireName(side) : string.Empty;

    /// <summary>The wire's word for a spell that may name an actor of either side, which every listed target matches.</summary>
    internal const string AnySide = "any";

    /// <summary>Writes a moment on the calendar for a person, empty when nothing states one.</summary>
    private static string Moment(GameDate? at) => at is { } moment
        ? moment.MinuteText
        : string.Empty;

    /// <summary>Writes the magic block: each member's spellbook, what a casting costs, and what the last one did.</summary>
    /// <remarks>
    /// Every row and every target is sent whole so the screen decides nothing: which spells a member knows,
    /// what each costs that caster, what each is aimed at, what it may be pointed at when its aim names no
    /// actor, and which actors a casting could name with the side each is on. What the last casting changed
    /// is published as named readings of the state it changed, and what spells have left running is published
    /// with the moment each one lapses, so the panel shows a cast's outcome and the party's wards from state
    /// rather than from the wording of a message.
    /// </remarks>
    /// <param name="builder">The projection being built.</param>
    /// <returns>The block's node.</returns>
    internal uint Write(UiValueBuilder builder) =>
        builder.Object(
            ("available", builder.Boolean(Available)),
            ("members", builder.Array([.. Members.Select(member => member.Write(builder, this))])),
            ("targets", builder.Array([.. Targets.Select(target => target.Write(builder))])),
            ("outcome", builder.String(Outcome)),
            ("member", builder.Number(Member)),
            ("caster", builder.String(Caster)),
            ("spell", builder.String(Spell)),
            ("cost", builder.Number(Cost)),
            ("target", builder.String(Target)),
            ("effect", builder.String(Effect)),
            ("code", builder.String(Code)),
            ("message", builder.String(Message)),
            ("source", builder.String(Source)),
            ("facts", builder.Array([.. Facts.Select(fact => fact.Write(builder))])),
            ("running", builder.Array([.. Running.Select(effect => effect.Write(builder))])),
            // What runs on each character rather than on the band, each row naming them: this is what makes a
            // ward cast on one member visible as theirs while the other members' rows stay empty.
            ("memberRunning", builder.Array([.. MemberRunning.Select(effect => effect.Write(builder))])),
            // The items the party carries that hold a spell: what each is, what it carries, and how much of it
            // is left. A panel shows them and sends back the instance identity it was handed, so using one asks
            // the product about the item it drew rather than about a row number.
            ("items", builder.Array([.. Items.Select(item => item.Write(builder, this))])),
            ("sight", builder.String(Sight)));

    /// <summary>
    /// Whether a casting aimed as the side says has somebody to name: always, when it names nobody; when any target is
    /// listed, when it may name either side; and otherwise when an actor on that side is among the targets this block
    /// lists.
    /// </summary>
    internal bool Aimable(string side) =>
        side.Length == 0 ||
        (string.Equals(side, AnySide, StringComparison.Ordinal) ? Targets.Count > 0 : Targets.Any(target => string.Equals(target.Side, side, StringComparison.Ordinal)));

    /// <summary>
    /// A member's spellbook by school, in the catalog's order of schools: a school is a page when the member holds a
    /// rung of its skill or has learned one of its spells, and each page lists the whole school with the casting
    /// workflow's own readiness for this member.
    /// </summary>
    private static IReadOnlyList<SpellPageSnapshot> Pages(Spellcasting owner, PartyMember member)
    {
        List<SpellPageSnapshot> pages = [];
        foreach (string school in owner.Rule.Catalog.Schools())
        {
            if (!owner.Rule.InSpellbook(school)) continue;
            IReadOnlyList<SpellDefinition> taught = owner.Rule.Catalog.OfSchool(school);
            if (taught.Count == 0) continue;
            SkillTier held = member.Skills.TierOf(taught[0].SchoolSkill);
            bool knowsAny = taught.Any(spell => member.Spells.Knows(spell.Id));
            if (held.Value <= 0 && !knowsAny) continue;
            pages.Add(new SpellPageSnapshot(
                school,
                GameNames.Tier(owner.Names, held),
                [.. taught.Select(spell => new SpellPageEntrySnapshot(
                    spell.Id.Value,
                    spell.Name,
                    GameNames.Tier(owner.Names, spell.Tier),
                    member.Spells.Knows(spell.Id),
                    owner.CostFor(member, spell),
                    owner.Readiness(member, spell)))]));
        }

        return pages;
    }
}
