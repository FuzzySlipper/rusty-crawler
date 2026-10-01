using PartyRpg.Kit;
using System.Globalization;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// What this game does with a spell once the party has paid for it: the one application point behind the
/// casting mechanism, and this game's answer for each of the design's eight effect categories.
/// </summary>
/// <remarks>
/// <para>
/// <b>Eight categories, one application path each, no path per spell.</b> A spell's identity selects its
/// category from this game's own table and the numbers its row states; what applies it is the category's own
/// path, and every spell of a category goes down the same one. Harm is resolved by the fight's own gated
/// entry, health is given through the member's own pool, conditions through the member's own condition state,
/// wards and utilities through the party-carried effects the fight's readings consult, light through a
/// deadline on the session's one clock, travel through the world's own transition path, and detection through
/// the places and population the world holds. Nothing here compares a spell's identity to anything.
/// </para>
/// <para>
/// <b>Where a spell cannot be applied, the row says so.</b> A spell whose reading names what is missing is
/// reported as cast with nothing changed in the world — never faked, never silently dropped — and the
/// coverage report is generated from those same rows, so what this path does and what the report claims
/// cannot disagree.
/// </para>
/// <para>
/// <b>Asked before it is paid for.</b> <see cref="Judge"/> answers the facts that must stop a cast before its
/// points are spent: whether the fight's own gates leave the caster able to act, and whether a travel spell
/// named a place the party can actually reach.
/// </para>
/// </remarks>
internal sealed class MightAndMagic7SpellEffects : ISpellEffectRule, ISpellAimRule, IPartySightRule, IRunningSpellEffects, IMemberSpellEffects, IGameTimeObserver, IDeadlineOwner
{
    private readonly MightAndMagic7Spells _spells;
    private readonly GameClock? _clock;
    private readonly Func<SessionWorld?> _world;
    private readonly Func<MightAndMagic7Combat?> _combat;
    private RunningSpellEffects? _running;

    /// <summary>Creates this game's effect path over its own spell table.</summary>
    /// <param name="spells">This game's magic, which states what each spell does and rolls.</param>
    /// <param name="clock">
    /// The session's one clock, which a duration is registered against. Without one an effect lasts until it
    /// is removed, which is the honest answer for a product that keeps no time.
    /// </param>
    /// <param name="world">
    /// The world the party stands in, read when a spell travels or reports. It is a provider rather than the
    /// world itself because this path is composed before the session's world exists on the path that creates
    /// its party, and because a product without content has no world to move anybody through.
    /// </param>
    /// <param name="combat">
    /// This game's fight policy, read when a spell takes hold of a creature: what the creature is immune to and
    /// whether it is undead are its own row's answers. A provider for the same reason the world is one.
    /// </param>
    /// <exception cref="ArgumentNullException">No spell table was supplied.</exception>
    internal MightAndMagic7SpellEffects(
        MightAndMagic7Spells spells,
        GameClock? clock = null,
        Func<SessionWorld?>? world = null,
        Func<MightAndMagic7Combat?>? combat = null)
    {
        _spells = spells ?? throw new ArgumentNullException(nameof(spells));
        _clock = clock;
        _world = world ?? (() => null);
        _combat = combat ?? (() => null);
    }

    /// <summary>The effects spells have left running, once a caster has left one.</summary>
    /// <remarks>
    /// The ledger is created on first use because it is the party's state and the party may not exist when
    /// this path is composed — the path that creates its party composes the world and the party together,
    /// after the session holds the magic. Nothing about a casting before then could have left an effect.
    /// </remarks>
    private RunningSpellEffects? Ledger => _running;

    /// <inheritdoc />
    /// <remarks>
    /// A session standing in no fight has nothing that could refuse a caster: there is no recovery being
    /// paced and no creature laying anybody out, so a casting out of combat is judged by the mechanism
    /// alone. A caster the fight holds is judged by the fight's own two gates, exactly as an order is.
    /// </remarks>
    public Refusal? Judge(SpellApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);
        if (application.Fight is { } fight && fight.Find(application.CasterId) is { } caster)
        {
            if (!caster.IsReady)
            {
                return SpellRefusals.CannotAct(
                    caster.Name,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"it is still recovering, with {caster.Recovery.Milliseconds}ms of game time left before it may act again"));
            }

            if (fight.IsDown(caster)) return SpellRefusals.CannotAct(caster.Name, "what is acting on them leaves them unable to cast");
        }

        // A school the caster's own class closes is refused before anything is paid, and the refusal names the
        // choice that closed it: a character who took one alternative of a second promotion holds its school
        // and none of the other, and a spell of the other school that reached their spellbook anyway — a
        // scenario's own party, an imported save of another game's shape — is not one they may cast. A
        // casting from an item is deliberately not judged here: a scroll and a potion carry a spell at the
        // strength they were made, and what their carrier may study is a different question from what the
        // item does when it is spent.
        if (application.Source is null && _spells.ClosedSchool(application.Caster, application.Spell) is { } closed)
        {
            return closed;
        }

        // A spell this build cannot aim is refused before it is paid for, and the refusal names what the spell
        // would act on and who owns the way to name one. A spell that was paid for and then changed nothing
        // would be the worst of both: the points are gone and the player was told nothing.
        SpellReading reading = _spells.ReadingOf(application.Spell);
        if (reading.Unaimable)
        {
            return SpellRefusals.TargetUnavailable(application.Spell.Name, reading.Missing, reading.Receiver);
        }

        // A spell this build applies none of is refused the same way, before anything is spent: a casting
        // that took the points or the item and changed nothing would be the same worst of both.
        if (reading.NotApplied)
        {
            return SpellRefusals.NotApplied(application.Spell.Name, reading.Missing, reading.Receiver);
        }

        // A travel spell is judged where it is aimed, before a point is spent: a portal needs a place the
        // world holds and the party has been to, and a beacon needs one it has set. The same judgement is what
        // a refusal names, so a player who typed a place that is not there is told so for the price of a cast
        // rather than a casting.
        return application.Spell.Effect switch
        {
            SpellEffects.Travel when TravelRefusalOf(application) is { } refused => refused,
            _ => null,
        };
    }

    /// <inheritdoc />
    public SpellApplicationOutcome Apply(SpellApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);

        // The party the casting names is the state every effect here changes, so the ledger is created over
        // it on the first cast. Two sessions cannot share one path: the ledger holds one party's effects. What
        // a character still carries is this game's answer — a member that death, petrification, or eradication
        // has laid out carries nothing a spell left, so their effects end where the ledger next reads or
        // advances rather than standing over a body.
        _running ??= new RunningSpellEffects(application.Party, _clock, member => !LaidOut(member));
        return application.Spell.Effect switch
        {
            SpellEffects.Damage => Harm(application),
            SpellEffects.Healing => Heal(application),
            SpellEffects.Resistance => Ward(application),
            SpellEffects.Condition => Afflict(application),
            SpellEffects.Light => Illuminate(application),
            SpellEffects.Travel => Travel(application),
            SpellEffects.Detection => Detect(application),
            SpellEffects.Utility => Utility(application),
            _ => SpellApplicationOutcome.Unexpressed(
                application.Spell.Effect,
                $"{application.Spell.Name} was cast, and this game has no category named '{application.Spell.Effect}', so nothing in the world changed."),
        };
    }

    /// <inheritdoc />
    /// <remarks>
    /// A portal reaches the places the party has been to, because a place the party has never seen is a place
    /// nobody can picture enough to open a gate to — which is the donor's own rule for its town portal. A
    /// beacon offers the one place the party set it in, and the offer is empty until one is set, which is what
    /// makes a first casting the setting of a beacon.
    /// </remarks>
    public IReadOnlyList<SpellAim> AimsOf(SpellDefinition spell)
    {
        SpellReading reading = _spells.ReadingOf(spell);
        if (reading.Travel == TravelShape.None || _world() is not { } world) return [];

        if (reading.Travel == TravelShape.Beacon)
        {
            return BeaconPlace() is { } beacon
                ? [new SpellAim(beacon.Value, world.Graph.Find(beacon)?.Name ?? beacon.Value, "beacon")]
                : [];
        }

        if (reading.Travel != TravelShape.Portal) return [];

        List<SpellAim> places = [];
        foreach (PlaceDefinition place in world.Graph.Places)
        {
            if (place.Id == world.Place) continue;
            if (!world.Places.StateOf(place.Id).Visited) continue;
            if (place.EntryPoints.Count == 0) continue;
            places.Add(new SpellAim(place.Id.Value, place.Name, "place"));
        }

        return places;
    }

    /// <summary>The effects spells have left running, read from the ledger they were applied through.</summary>
    /// <remarks>
    /// A session with no cast behind it holds no ledger at all, and this then reports nothing rather than an
    /// empty party: what a panel shows before the first casting is that no spell has left anything running.
    /// </remarks>
    public IReadOnlyList<RunningSpellEffect> Running => _running?.Running ?? [];

    /// <summary>Whether an effect a spell applied is still running.</summary>
    /// <param name="effect">The effect to look for.</param>
    public bool IsRunning(EffectId effect) => _running?.IsRunning(effect) == true;

    /// <summary>The effects running on the party's own characters, read from the ledger they were applied through.</summary>
    /// <remarks>
    /// A session with no cast behind it holds no ledger, and this then reports nothing rather than an empty
    /// party: a panel shows that no spell has left anything on anybody.
    /// </remarks>
    public IReadOnlyList<RunningSpellEffect> RunningOnMembers => _running?.RunningOnMembers ?? [];

    /// <summary>What one character carries of one effect, which is what the fight's own readings ask.</summary>
    /// <remarks>
    /// A character the game has laid out carries nothing, and the call ends what a death ended: the ledger is
    /// the one owner that knows which effects belong to which character, so the reading and the ending are the
    /// same question asked once.
    /// </remarks>
    /// <param name="member">The character to read.</param>
    /// <param name="effect">The effect definition to read.</param>
    /// <returns>The magnitude it acts at, or zero when the character does not carry it.</returns>
    public int MagnitudeOn(PartyMember member, EffectId effect) => _running?.MagnitudeOn(member, effect) ?? 0;

    /// <summary>
    /// Leaves a temporary resistance on one character that something other than a spell gave them: a well, a
    /// fountain, an altar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It is the same carried effect a ward leaves, through the same ledger, because the fight reads one
    /// resistance per kind from it (<see cref="SpellEffectIds.Resistance"/>) and a second store of resistances
    /// would be a second answer to that question. The ledger is created over the party here when no casting
    /// has created it yet, exactly as a first casting does.
    /// </para>
    /// <para>
    /// <b>An adaptation, stated.</b> The donor keeps a well's bonus in a field of its own beside a spell's
    /// buff and adds the two (OpenEnroth <c>src/Engine/Objects/Character.cpp:2986-2995</c>, which clears the
    /// bonuses on rest); here the two share one magnitude per kind, so a ward cast afterwards replaces a well's
    /// bonus rather than adding to it, and the bonus ends on the clock rather than at the next rest.
    /// </para>
    /// </remarks>
    /// <param name="party">The party the character belongs to.</param>
    /// <param name="member">The character.</param>
    /// <param name="kind">The kind of harm resisted.</param>
    /// <param name="magnitude">How much is resisted.</param>
    /// <param name="lasts">How long it lasts.</param>
    internal void Resist(PartyEntity party, PartyMember member, DamageKindId kind, int magnitude, GameDuration lasts) =>
        Leave(party, member, SpellEffectIds.Resistance(kind), magnitude, lasts);

    /// <summary>
    /// Leaves a temporary effect on one character that something other than a spell gave them — a resistance,
    /// an attribute's bonus, armour — in the running effect the spell of that name leaves.
    /// </summary>
    /// <remarks>
    /// The same adaptation <see cref="Resist"/> states: one magnitude per effect, so a well's bonus and a spell's
    /// share it, and it ends on the clock rather than at the next rest.
    /// </remarks>
    /// <param name="party">The party the character belongs to.</param>
    /// <param name="member">The character.</param>
    /// <param name="effect">The effect, which a rule of the fight reads.</param>
    /// <param name="magnitude">The magnitude it acts at.</param>
    /// <param name="lasts">How long it lasts.</param>
    internal void Leave(PartyEntity party, PartyMember member, EffectId effect, int magnitude, GameDuration lasts)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(member);
        _running ??= new RunningSpellEffects(party, _clock, carrier => !LaidOut(carrier));
        _running.StartOn(member, effect, magnitude, lasts);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The light is read against the clock's own daylight window, which is the whole of what brightness is in
    /// this kit: in daylight the party sees by the day whatever it carries, in the dark it sees by its own
    /// light if it has one, and otherwise it is in the dark. A session with no clock cannot say that it is
    /// daylight, so it answers only for the light it carries.
    /// </remarks>
    public PartySight Sight
    {
        get
        {
            bool carries = _running?.IsRunning(SpellEffectIds.Light) == true;
            if (_clock is not { } clock) return carries ? PartySight.Light : PartySight.Unstated;
            if (clock.IsDaylight) return PartySight.Daylight;
            return carries ? PartySight.Light : PartySight.Dark;
        }
    }

    /// <summary>Hands one advance of the session's clock to the effects it ends.</summary>
    /// <param name="advance">Where the clock was, where it went, and what it brought due.</param>
    /// <exception cref="ArgumentNullException">No advance was supplied.</exception>
    /// <remarks>
    /// What a running effect does while time passes is read here, before the ledger ends what the advance brought
    /// due: a regeneration gives back what the stretch of time it was still running for is worth, and not a moment
    /// more, whether the clock got there in one long rest or a thousand updates.
    /// </remarks>
    public void Observe(ClockAdvance advance)
    {
        ArgumentNullException.ThrowIfNull(advance);
        Regenerate(advance);
        DrainFlight(advance);
        DrainWaterWalk(advance);
        _running?.Observe(advance);
    }

    /// <summary>
    /// Gives health back to every character a regeneration runs on, once for every five minutes of game time the
    /// advance passed while it ran.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The donor counts the five-minute boundaries between the last time it regenerated and now, and gives each
    /// character with the buff five times its power for every one of them, never past what the character can hold
    /// (<c>OpenEnroth/src/Engine/Engine.cpp:1236</c>, <c>:1398-1401</c>, and <c>Character.cpp:6495-6519</c>); a
    /// character whose health is above nothing again is no longer unconscious (<c>Engine.cpp:1438-1440</c>). This
    /// is that, counted on the calendar's own boundaries so the same stretch of time is worth the same health
    /// however it was cut. The regeneration stops counting at its own deadline. Faithful.
    /// </para>
    /// <para>
    /// A character the game has laid out carries nothing, so the dead are not regenerated: the ledger reports no
    /// effect on them.
    /// </para>
    /// </remarks>
    private void Regenerate(ClockAdvance advance)
    {
        if (_running is not { } running || _clock is not { } clock || !advance.Moved) return;
        foreach (RunningSpellEffect effect in running.RunningOnMembers)
        {
            if (effect.Effect != SpellEffectIds.Regeneration || effect.Member is not { } id) continue;
            if (!running.Party.TryMember(id, out PartyMember? member) || member is null) continue;

            long ticks = clock.Calendar.Boundaries(advance.From, advance.To, RegenerationInterval);
            if (effect.EndsAt is { } ends) ticks = Math.Min(ticks, clock.Calendar.Boundaries(advance.From, ends, RegenerationInterval));
            if (ticks <= 0) continue;

            long given = Math.Min(int.MaxValue, ticks * effect.Magnitude);
            member.Resources.RestoreHitPoints((int)given);
            if (member.Resources.HitPoints.Current > 0) member.Conditions.Clear(MightAndMagic7Conditions.Unconscious);
        }
    }

    /// <summary>
    /// Takes one spell point from whoever holds the party in the air for every five minutes of game time the advance
    /// passed while the party was flying.
    /// </summary>
    /// <remarks>
    /// The donor counts the five-minute boundaries the clock crossed and takes that many points from the flight's caster
    /// while the party is flying, never below nothing, and never at grand master
    /// (<c>OpenEnroth/src/Engine/Engine.cpp:1236</c>, <c>:1286-1294</c>). Here the caster carries the flight, its
    /// magnitude is what each five minutes costs them — nothing at grand master — and the advance is counted on the
    /// calendar's own boundaries up to the flight's own deadline, as a regeneration is. A party standing on the ground
    /// with a flight running pays nothing, as the donor's does. A caster drained dry no longer keeps the party up, which
    /// the flight rule reads at the next step.
    /// </remarks>
    private void DrainFlight(ClockAdvance advance)
    {
        if (_world()?.Mover is not { Flying: true }) return;
        Drain(advance, SpellEffectIds.Fly, FlightDrainInterval);
    }

    /// <summary>
    /// Takes one spell point from whoever keeps the party on its feet over water for every twenty minutes the advance
    /// passed while the party stood on water.
    /// </summary>
    /// <remarks>
    /// The donor drains a water walk's caster only while the party stands on water, never at grand master, every twenty
    /// minutes with its drain fixed to what the spell's own description says and every five without
    /// (<c>OpenEnroth/src/Engine/Engine.cpp:1297-1309</c>, <c>src/Application/GameConfig.h:248-250</c>). This takes the
    /// twenty the description states.
    /// </remarks>
    private void DrainWaterWalk(ClockAdvance advance)
    {
        if (_world()?.Mover?.Footing is not { Id: MightAndMagic7Movement.WaterSurface }) return;
        Drain(advance, SpellEffectIds.WaterWalk, WaterWalkDrainInterval);
    }

    /// <summary>Takes a caster-carried effect's magnitude in spell points for every interval the advance crossed while it ran.</summary>
    private void Drain(ClockAdvance advance, EffectId carried, GameDuration interval)
    {
        if (_running is not { } running || _clock is not { } clock || !advance.Moved) return;
        foreach (RunningSpellEffect effect in running.RunningOnMembers)
        {
            if (effect.Effect != carried || effect.Magnitude <= 0 || effect.Member is not { } id) continue;
            if (!running.Party.TryMember(id, out PartyMember? caster) || caster is null) continue;

            long ticks = clock.Calendar.Boundaries(advance.From, advance.To, interval);
            if (effect.EndsAt is { } ends) ticks = Math.Min(ticks, clock.Calendar.Boundaries(advance.From, ends, interval));
            if (ticks <= 0) continue;

            int drained = (int)Math.Min(caster.Resources.SpellPoints.Current, ticks * effect.Magnitude);
            caster.Resources.TrySpendSpellPoints(drained);
        }
    }

    /// <summary>How often standing on water costs a water walk's caster: every twenty minutes, <c>GameConfig.h:248-250</c>.</summary>
    private static readonly GameDuration WaterWalkDrainInterval = GameDuration.FromMinutes(20);

    /// <summary>
    /// Leaves a flight on its caster, ending any other flight the party carries.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The donor's flight is one party buff naming its caster, lasting an hour a level, and free of its drain at grand
    /// master (<c>OpenEnroth/src/Engine/Spells/CastSpellInfo.cpp:1154-1171</c>). Here the caster carries it, so the
    /// flight and whoever pays for it are one fact: its magnitude is the spell points each five minutes in the air
    /// costs them, one below grand master and nothing at it, and a caster the game lays out carries nothing, so the
    /// party comes down with them. A second casting replaces the first, as the donor's one buff does, so the party
    /// carries one flight and one caster pays.
    /// </para>
    /// <para>
    /// The spell leaves the party able to fly and does not lift it: the party rises when it asks to, which is the
    /// donor's own shape.
    /// </para>
    /// </remarks>
    private SpellApplicationOutcome Fly(SpellApplication application, SessionWorld world)
    {
        int level = _spells.LevelOf(application);
        return OnCaster(
            application,
            SpellEffectIds.Fly,
            GameDuration.FromHours(level),
            $"can hold the party in the air over {world.Graph.Require(world.Place).Name}",
            "every five minutes in the air");
    }

    /// <summary>
    /// Leaves a walk over water on its caster, ending any other the party carries.
    /// </summary>
    /// <remarks>
    /// The donor's water walk is one party buff naming its caster, lasting ten minutes a level at expert and an hour a
    /// level at master and grand master, and free of its drain at grand master
    /// (<c>OpenEnroth/src/Engine/Spells/CastSpellInfo.cpp:1302-1331</c>). Here the caster carries it, as a flight is
    /// carried, and its magnitude is what twenty minutes standing on water costs them.
    /// </remarks>
    private SpellApplicationOutcome WalkOnWater(SpellApplication application)
    {
        int level = _spells.LevelOf(application);
        int mastery = MightAndMagic7Spells.MasteryOf(application.Caster, application.Spell);
        return OnCaster(
            application,
            SpellEffectIds.WaterWalk,
            WardFormulas.FeatherFallLasts(level, mastery),
            "can keep the party on its feet over water",
            "every twenty minutes on water");
    }

    /// <summary>
    /// Leaves a way of moving on its caster, ending it on everybody else: the party carries one, and one caster pays.
    /// </summary>
    /// <remarks>
    /// Its magnitude is the spell points each interval it is used costs the caster: one below grand master and nothing
    /// at it, which is the donor's own exemption for both flight and water walking (<c>Engine.cpp:1286-1309</c>).
    /// </remarks>
    private SpellApplicationOutcome OnCaster(SpellApplication application, EffectId effect, GameDuration lasts, string does, string paidFor)
    {
        if (Ledger is not { } running) return Unexpressed(application, "no party to carry it");
        foreach (PartyMember member in application.Party.Members)
        {
            if (member.Id != application.Caster.Id) running.EndOn(member.Id, effect);
        }

        int mastery = MightAndMagic7Spells.MasteryOf(application.Caster, application.Spell);
        int drain = mastery >= 4 ? 0 : 1;
        running.StartOn(application.Caster, effect, drain, lasts);
        string cost = drain == 0
            ? "for nothing"
            : string.Create(CultureInfo.InvariantCulture, $"for {drain} spell point {paidFor}");
        return Expressed(
            application,
            $"{application.Caster.Profile.Name} {does} {cost}, until the clock reaches the end of {Describe(lasts)}",
            [
                new SpellEffectFact("effect", effect.Value),
                new SpellEffectFact("carried", application.Caster.Profile.Name),
                new SpellEffectFact("drain", drain.ToString(CultureInfo.InvariantCulture)),
                new SpellEffectFact("until", Moment(application, lasts)),
            ]);
    }

    /// <summary>How often a flight costs its caster: every five minutes of game time, <c>Engine.cpp:1236</c>.</summary>
    private static readonly GameDuration FlightDrainInterval = GameDuration.FromMinutes(5);

    /// <summary>How many times the party's own jump the donor's jump spell is: a thousand over five times ninety-six.</summary>
    private const double LeapMultiple = 1000.0 / (5 * 96);

    /// <summary>How often a regeneration gives health back: every five minutes of game time, <c>Engine.cpp:1236</c>.</summary>
    private static readonly GameDuration RegenerationInterval = GameDuration.FromMinutes(5);

    /// <inheritdoc />
    public bool Holds(DeadlineId deadline) => _running?.Holds(deadline) ?? false;

    /// <inheritdoc />
    public bool RebuildsOnLoad(DeadlineId deadline) => _running?.RebuildsOnLoad(deadline) ?? false;

    /// <inheritdoc />
    public string Describe(DeadlineId deadline) => _running?.Describe(deadline) ?? $"deadline {deadline}";

    /// <summary>Harm: the spell's own dice and kind, ordered through the fight's own gated entry.</summary>
    /// <remarks>
    /// This is the category's whole path: the spell is an attack of the spell kind, the ability the order
    /// names is the spell's own content identity, and the fight reads that spell's numbers, lets the target's
    /// resistance take its share, leaves whatever condition a landed hit leaves, charges the caster's recovery,
    /// and resolves the death. A damage spell with no opponent in reach still resolves nothing — a spell is
    /// aimed like any other attack — and says so rather than landing on the nearest thing.
    /// </remarks>
    private SpellApplicationOutcome Harm(SpellApplication application)
    {
        if (application.Target is { } target
            && application.Fight is { } fight
            && _spells.Harm(application.Spell) is not null)
        {
            CombatResult result = fight.Order(new AttackOrder(
                application.CasterId,
                AttackKind.Spell,
                target,
                application.Spell.Id.Value));
            return result.IsApplied
                ? SpellApplicationOutcome.Expressed(application.Spell.Effect, result.Message, facts: [], attack: result)
                : SpellApplicationOutcome.Unexpressed(
                    application.Spell.Effect,
                    $"{application.Spell.Name} was cast and the fight refused the blow it carries: {result.Message}");
        }

        return SpellApplicationOutcome.Unexpressed(
            application.Spell.Effect,
            $"{application.Spell.Name} was cast with no opponent in reach to land on, so nothing was harmed.");
    }

    /// <summary>Healing: hit points given back through the member's own pool.</summary>
    /// <remarks>
    /// The donor's own shapes (<c>OpenEnroth/src/Engine/Spells/CastSpellInfo.cpp:2244-2262</c> for a cure,
    /// <c>:1817-1828</c> for a shared life, <c>:1840-1880</c> for the two raisings, and <c>:2599</c> for a
    /// divine intervention) are read from the spell's own row: an amount, a pooling of the party's health, a
    /// raising of what laid a member out, or every pool filled. A raising is the one shape that must touch
    /// both owners — the pool it fills and the conditions it lifts — because a member with one hit point who
    /// is still dead is a member who was not raised.
    /// </remarks>
    private SpellApplicationOutcome Heal(SpellApplication application)
    {
        SpellReading reading = _spells.ReadingOf(application.Spell);

        // A healing spell that gives health back over a duration is a carried effect rather than an amount: it is
        // landed the way every other carried effect is, and the clock's own advances read it.
        if (reading.Healing == HealingMode.None && Carries(reading)) return Carry(application, reading);
        IReadOnlyList<PartyMember> members = Members(application);
        if (reading.Healing == HealingMode.None || members.Count == 0)
        {
            return Unexpressed(application, "no member to give health to");
        }

        int level = _spells.LevelOf(application);
        int mastery = MightAndMagic7Spells.MasteryOf(application.Caster, application.Spell);
        int amount = reading.HealBase + (reading.HealByMastery ? mastery : 1) * reading.HealPerLevel * level;
        List<SpellEffectFact> facts = [];
        List<string> lines = [];

        switch (reading.Healing)
        {
            case HealingMode.Share:
            {
                // The donor pools what every standing member holds, adds the spell's own share, and hands the
                // mean back to each of them, never past what a member can hold.
                int pool = amount;
                int standing = 0;
                foreach (PartyMember member in members)
                {
                    if (LaidOut(member)) continue;
                    pool += member.Resources.HitPoints.Current;
                    standing++;
                }

                if (standing == 0) return Unexpressed(application, "nobody is standing to share life with");
                int share = pool / standing;
                foreach (PartyMember member in members)
                {
                    if (LaidOut(member)) continue;
                    member.Resources.RestoreHitPoints(Math.Max(0, share - member.Resources.HitPoints.Current));
                    facts.Add(Pool(member));
                }

                lines.Add(string.Create(CultureInfo.InvariantCulture, $"the party's health is shared out at {share} each"));
                break;
            }

            case HealingMode.Fill:
            {
                foreach (PartyMember member in members)
                {
                    member.Resources.RestoreAll();
                    member.Conditions.ClearAll();
                    facts.Add(Pool(member));
                }

                lines.Add("every pool is filled and every condition is lifted");

                // The donor's price for a divine intervention is years: the caster is aged ten, never past a
                // modifier of a hundred and twenty (OpenEnroth src/Engine/Spells/CastSpellInfo.cpp:2603-2607).
                if (reading.AgesCaster > 0)
                {
                    int aged = application.Caster.Progression.Age(reading.AgesCaster, MightAndMagic7Ageing.MostUnnaturalYears);
                    lines.Add(string.Create(CultureInfo.InvariantCulture, $"{application.Caster.Profile.Name} is aged {aged} year(s) for it"));
                    facts.Add(new SpellEffectFact("aged", aged.ToString(CultureInfo.InvariantCulture)));
                }

                break;
            }

            case HealingMode.Raise:
            {
                foreach (PartyMember member in members)
                {
                    // A raising stands a member up at one hit point and lifts what laid them out. The donor
                    // gates this on how long the condition has lasted below grand master; this build's
                    // condition state records no onset, so the raising is unconditional and leaves the
                    // weakness the donor's own cast leaves.
                    member.Resources.RestoreHitPoints(Math.Max(0, 1 - member.Resources.HitPoints.Current));
                    if (reading.Lifts is { } lifts)
                    {
                        foreach (ConditionId condition in lifts) member.Conditions.Clear(condition);
                    }

                    if (reading.Weakness is { } weakness) member.Conditions.Apply(new ActiveCondition(MightAndMagic7Conditions.Weak, weakness));
                    facts.Add(Pool(member));
                }

                lines.Add("the fallen are stood back up at one hit point, and are left weak");
                break;
            }

            default:
            {
                foreach (PartyMember member in members)
                {
                    member.Resources.RestoreHitPoints(amount);
                    facts.Add(Pool(member));
                }

                lines.Add(string.Create(CultureInfo.InvariantCulture, $"{amount} hit point(s) each"));
                break;
            }
        }

        string who = members.Count == 1 ? members[0].Profile.Name : "the party";
        return SpellApplicationOutcome.Expressed(
            application.Spell.Effect,
            $"{application.Spell.Name}: {who} — {string.Join("; ", lines)}.",
            facts);
    }

    /// <summary>Resistance: a ward on the character the casting named, or on the party, as the table aims it.</summary>
    /// <remarks>
    /// <para>
    /// A protection is a carried effect with a deadline on the session's one clock, so a ward lapses on the
    /// road exactly as it does standing still; the fight reads it through the very answers it already asks —
    /// a resistance when it lets the target's share off a blow's harm, and armour class when it prices a
    /// chance to land.
    /// </para>
    /// <para>
    /// <b>Whose ward it is, is the table's own aim.</b> A reading marked as landing on one character is
    /// applied to the member the casting named, under that member's own entry, so the fight reads it for them
    /// and nobody else; a reading aimed at the party is carried by the party, which is where a party-wide buff
    /// already lives. The donor's own casts differ per spell and are cited where the row is stated, and the
    /// coverage report says which is which rather than leaving it to be inferred from the numbers.
    /// </para>
    /// </remarks>
    private SpellApplicationOutcome Ward(SpellApplication application)
    {
        SpellReading reading = _spells.ReadingOf(application.Spell);

        // A protection that is not a resistance or armour — a shield against missiles — is a carried effect of its
        // own identity, landed the way a utility's is.
        if (reading.Ward is null && Carries(reading)) return Carry(application, reading);
        if (reading.Ward is not { } ward || Ledger is not { } running)
        {
            return Unexpressed(application, "no ward this build can raise");
        }

        (int power, GameDuration lasts) = Worth(application, ward.Power, ward.Lasts);
        IReadOnlyList<PartyMember> carries = reading.OnMember ? Carriers(application) : [];
        if (reading.OnMember && carries.Count == 0)
        {
            return Unexpressed(application, "no character to carry a ward the table aims at one");
        }

        if (carries.Count == 0)
        {
            foreach (DamageKindId kind in ward.Kinds) running.Start(SpellEffectIds.Resistance(kind), power, lasts);
            if (ward.Armour) running.Start(SpellEffectIds.Armour, power, lasts);
        }
        else
        {
            foreach (PartyMember member in carries)
            {
                foreach (DamageKindId kind in ward.Kinds)
                {
                    running.StartOn(member, SpellEffectIds.Resistance(kind), power, lasts);
                }

                if (ward.Armour) running.StartOn(member, SpellEffectIds.Armour, power, lasts);
            }
        }

        string warded = ward.Armour
            ? "armour class"
            : string.Join(", ", ward.Kinds.Select(kind => kind.Value));
        string who = carries.Count switch
        {
            0 => "the party",
            1 => carries[0].Profile.Name,
            _ => "the party",
        };
        return Expressed(
            application,
            $"{who} is warded against {warded} at {power}, until the clock reaches the end of {Describe(lasts)}",
            [
                new SpellEffectFact("ward", warded),
                new SpellEffectFact("power", power.ToString(CultureInfo.InvariantCulture)),
                new SpellEffectFact("until", Moment(application, lasts)),
                .. carries.Select(member => new SpellEffectFact("warded", member.Profile.Name)),
            ]);
    }

    /// <summary>
    /// The characters an effect aimed at one member lands on, in the order the party stands in.
    /// </summary>
    /// <remarks>
    /// A spell whose aim names the caster lands on the caster and one aimed at an ally lands on the member the
    /// casting named; a spell aimed at the party names nobody in particular and is carried by the party
    /// instead, which is why this answers nothing for it.
    /// </remarks>
    private static IReadOnlyList<PartyMember> Carriers(SpellApplication application) =>
        application.Spell.Targeting switch
        {
            SpellTargeting.Caster => [application.Caster],
            SpellTargeting.Ally => Members(application),
            _ => [],
        };

    /// <summary>Condition: a condition lifted from a member, or left on a target that is not one.</summary>
    /// <remarks>
    /// Curing is the donor's own path (<c>OpenEnroth/src/Engine/Spells/CastSpellInfo.cpp:2244, 1897-1960</c>):
    /// the spell clears the conditions its row names from the member it is cast on. The donor's non-grand
    /// master gate on how long a condition has lasted is not modelled, because this build's condition state
    /// records no onset; a cure therefore lifts the condition outright, which is what the donor's own grand
    /// master does.
    /// </remarks>
    private SpellApplicationOutcome Afflict(SpellApplication application)
    {
        SpellReading reading = _spells.ReadingOf(application.Spell);

        // A condition aimed at a creature is left on the creature's own state, which the fight's readings and its
        // own decisions consult; a party member's condition is the member's.
        if (reading.OnCreature is { } creature) return AfflictCreatures(application, creature);

        // A reading that leaves a condition rather than lifting one is the drinking of a catalyst, which the
        // donor's own case poisons the drinker with; it lands on the character the casting named, through their
        // own condition state, exactly as a cure lands there.
        if (reading.Leaves is { } leaves)
        {
            List<SpellEffectFact> left = [];
            foreach (PartyMember member in Members(application))
            {
                member.Conditions.Apply(new ActiveCondition(leaves, reading.LeavesSeverity));
                left.Add(new SpellEffectFact("conditions", Conditions(member)));
            }

            return left.Count == 0
                ? Unexpressed(application, "nobody to leave that condition on")
                : SpellApplicationOutcome.Expressed(
                    application.Spell.Effect,
                    $"{application.Spell.Name}: {leaves} is left on {string.Join(", ", Members(application).Select(member => member.Profile.Name))}.",
                    left);
        }

        if (reading.Clears is not { Length: > 0 } clears)
        {
            return Unexpressed(application, "a condition this build cannot leave on that target");
        }

        List<SpellEffectFact> facts = [];
        List<string> lifted = [];
        foreach (PartyMember member in Members(application))
        {
            foreach (ConditionId condition in clears)
            {
                if (!member.Conditions.Clear(condition)) continue;
                lifted.Add($"{condition} from {member.Profile.Name}");
            }

            facts.Add(new SpellEffectFact("conditions", Conditions(member)));
        }

        return lifted.Count == 0
            ? Unexpressed(application, "nobody was suffering what it lifts")
            : SpellApplicationOutcome.Expressed(
                application.Spell.Effect,
                $"{application.Spell.Name}: {string.Join(", ", lifted)} lifted.",
                facts);
    }

    /// <summary>
    /// Leaves a spell's effect on the creatures it takes hold of, on each creature's own state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What takes hold is the creature's own answer.</b> The donor lands a creature spell only on an actor that
    /// is not immune to the spell's kind of harm (<c>Actor::DoesDmgTypeDoDamage</c>, read at each cast in
    /// <c>OpenEnroth/src/Engine/Spells/CastSpellInfo.cpp</c> and <c>src/Engine/Objects/SpriteObject.cpp:1020-1030</c>),
    /// and a turning or a fear only on the undead or on the living (<c>CastSpellInfo.cpp:1752-1758</c>,
    /// <c>:2108-2116</c>). Both are read from the creature's own row, so an immune creature is named as immune and
    /// the casting is still spent, as the donor spends it.
    /// </para>
    /// <para>
    /// <b>What it leaves is read where it applies.</b> The effect is held on the creature
    /// (<see cref="CreatureEffects"/>) and counted down by the fight's own clock advances; the fight's gate, its
    /// recovery, its harm, and the creature's own decisions read it. An act against a creature puts it into the
    /// fight as an attack does, which is the donor's aggressor flag (<c>CastSpellInfo.cpp:565</c>, <c>:602</c>).
    /// </para>
    /// </remarks>
    private SpellApplicationOutcome AfflictCreatures(SpellApplication application, CreatureReading reading)
    {
        if (application.Fight is not { } fight) return Unexpressed(application, "no creature stands in reach to take hold of");
        MightAndMagic7Combat? policy = _combat();
        List<Combatant> taken = [];
        if (reading.Reach == CreatureReach.Named)
        {
            if (application.Target is { } named && fight.Find(named) is { Subject.Entity: not null } one && !fight.IsDown(one))
            {
                taken.Add(one);
            }
        }
        else
        {
            taken.AddRange(fight.Combatants.Where(combatant =>
                combatant.Subject.Entity is not null && !fight.IsDown(combatant) && combatant.Distance <= MassSpellDepth));
        }

        (int power, GameDuration lasts) = reading.Lasts is { } length
            ? Worth(application, reading.Power, length)
            : (Worth(application, reading.Power, (_, _) => GameDuration.None).Power, GameDuration.None);
        List<SpellEffectFact> facts = [];
        List<string> lines = [];
        foreach (Combatant creature in taken)
        {
            CombatSubject subject = creature.Subject;
            bool undead = policy?.IsUndead(subject) == true;
            if (reading.Kind == CreatureKindGate.Undead && !undead) continue;
            if (reading.Kind == CreatureKindGate.Living && undead) continue;
            if (CreatureEffects.Find(subject.Entity!.Actor) is not { } effects) continue;
            if (reading.ResistedBy is { } kind && policy?.ImmuneTo(subject, kind) == true)
            {
                lines.Add($"{creature.Name} is immune to {kind}, and nothing takes hold");
                facts.Add(new SpellEffectFact("immune", creature.Name));
                continue;
            }

            foreach (EffectId ended in reading.Ends ?? []) effects.Remove(ended);
            if (!lasts.IsNone) effects.Apply(reading.Effect, power, lasts);
            if (reading.DelayTicks > 0) fight.Delay(creature.Id, MightAndMagic7Combat.Ticks(reading.DelayTicks));
            if (reading.Provokes) fight.Provoke(creature.Id);
            lines.Add(lasts.IsNone
                ? $"{creature.Name} is {reading.Effect}"
                : $"{creature.Name} is {reading.Effect} at {power} until the clock has run {Describe(lasts)}");
            facts.Add(new SpellEffectFact(reading.Effect.Value, creature.Name));
        }

        if (lines.Count == 0)
        {
            return taken.Count == 0
                ? Unexpressed(application, "no creature it can take hold of stands in reach")
                : Unexpressed(application, "no creature in reach is one it takes hold of");
        }

        return Expressed(application, string.Join("; ", lines), facts);
    }

    /// <summary>
    /// How far from the party a spell that takes hold of every creature in view reaches: the donor's own mass-spell
    /// depth (<c>OpenEnroth/src/Application/GameConfig.h:200</c>, <c>mass_spell_depth</c>).
    /// </summary>
    private const double MassSpellDepth = 4096;

    /// <summary>Light: a light carried by the party, ended by the clock's own daylight window.</summary>
    /// <remarks>
    /// <para>
    /// The donor's torch light is worth more at every rung of mastery and lasts an hour per level of the
    /// school (<c>OpenEnroth/src/Engine/Spells/CastSpellInfo.cpp:328-346</c>). What this build adds is the
    /// daylight window: a light stands in for the sun, so it also ends at the next dawn even when the donor's
    /// own hours would run past it — the shorter of the two is how long a light is worth carrying. That is our
    /// rule over the donor's number and it is stated here rather than hidden in the arithmetic.
    /// </para>
    /// <para>
    /// A session with no clock still carries the light: it lasts until something removes it, and the panel
    /// shows that nothing has said when it ends.
    /// </para>
    /// </remarks>
    private SpellApplicationOutcome Illuminate(SpellApplication application)
    {
        if (Ledger is not { } running) return Unexpressed(application, "no party to carry a light");

        int level = Math.Max(1, _spells.LevelOf(application));
        int mastery = MightAndMagic7Spells.MasteryOf(application.Caster, application.Spell);
        int power = mastery switch
        {
            <= 1 => TorchLightNovice,
            2 => TorchLightExpert,
            _ => TorchLightMaster,
        };

        GameDuration donor = GameDuration.FromHours(level);
        GameDuration lasts = _clock is { } clock ? Min(donor, UntilDawn(clock)) : donor;
        if (lasts.IsNone) lasts = GameDuration.FromMinutes(1);
        running.Start(SpellEffectIds.Light, power, lasts);

        return Expressed(
            application,
            $"the party carries a light of {power} until the clock reaches the end of {Describe(lasts)}",
            [
                new SpellEffectFact("light", power.ToString(CultureInfo.InvariantCulture)),
                new SpellEffectFact("until", Moment(application, lasts)),
                new SpellEffectFact("sight", PartySights.WireName(Sight)),
            ]);
    }

    /// <summary>Travel: a portal taken through the world's own transition path.</summary>
    /// <remarks>
    /// <para>
    /// A portal is a transition of the world's own kind — <see cref="TransitionKind.Portal"/>, which exists
    /// for exactly this — taken through the one path every crossing takes, so the destination's arrival point
    /// is resolved by the graph, the journey is priced by the cost rule, the place is marked visited, and the
    /// party's pose moves through the same owner. Nothing here teleports anybody: a travel spell is a
    /// transition whose issuer is the caster rather than a place.
    /// </para>
    /// <para>
    /// A beacon is the party's own carried state: setting it writes the place into the effects the party
    /// already carries, and recalling reads it back and opens a portal to it. The donor's beacon is one place
    /// and a second casting replaces it, which is what the ledger's one effect per identity gives.
    /// </para>
    /// </remarks>
    private SpellApplicationOutcome Travel(SpellApplication application)
    {
        SpellReading reading = _spells.ReadingOf(application.Spell);

        // A travel spell that leaves a way of moving on the party — a feather fall — is a carried effect the
        // movement rules read, landed the way every carried effect is.
        if (reading.Travel == TravelShape.None && Carries(reading)) return Carry(application, reading);
        if (_world() is not { } world) return Unexpressed(application, "no world to travel through");
        if (reading.Travel == TravelShape.Leap)
        {
            // The donor's jump throws the party up at a thousand where its own jump is five times ninety-six
            // (OpenEnroth src/Engine/Spells/CastSpellInfo.cpp:1111-1121, src/Engine/Graphics/Outdoor.cpp:1193-1197);
            // the mover takes it as that multiple of the party's own jump.
            return world.Mover?.Leap(LeapMultiple) == true
                ? Expressed(application, "the party leaps", [new SpellEffectFact("leap", LeapMultiple.ToString("0.###", CultureInfo.InvariantCulture))])
                : Unexpressed(application, "the party is not standing on anything to leap from");
        }

        if (reading.Travel == TravelShape.Flight) return Fly(application, world);
        if (reading.Travel == TravelShape.WaterWalk) return WalkOnWater(application);
        if (reading.Travel == TravelShape.None) return Unexpressed(application, "a way of moving this build's mover does not have");

        if (reading.Travel == TravelShape.Beacon && application.TargetName.Length == 0)
        {
            // Setting a beacon is the one travel act that moves nobody: the place the party stands in is
            // written into its own carried state, and every earlier beacon is dropped because the donor's
            // beacon is one place rather than a list.
            foreach (string held in Beacons(application.Party)) application.Party.Records.Remove(held);
            application.Party.Records.Mark(SpellEffectIds.Beacon(world.Place));
            return Expressed(
                application,
                $"a beacon is set in {world.Graph.Require(world.Place).Name}",
                [new SpellEffectFact("beacon", world.Place.Value), new SpellEffectFact("place", world.Graph.Require(world.Place).Name)]);
        }

        PlaceId destination = new(application.TargetName);
        PlaceDefinition place = world.Graph.Require(destination);
        PlaceEntryPoint arrival = place.EntryPoints[0];
        PlaceTransition transition = new(
            From: world.Place,
            To: destination,
            PlaceArrival.AtEntryPoint(arrival.Id),
            Source: string.Create(CultureInfo.InvariantCulture, $"spell '{application.Spell.Id.Value}'"));
        PlaceId from = world.Place;
        TransitionResult result = world.Travel(transition, TransitionKind.Portal);
        if (!result.Arrived)
        {
            return Unexpressed(
                application,
                $"{application.Spell.Name} was cast and the world refused the crossing: {result.Refusal?.Message}");
        }

        return Expressed(
            application,
            $"a portal opens from {world.Graph.Require(from).Name} to {place.Name}",
            [
                new SpellEffectFact("from", from.Value),
                new SpellEffectFact("to", destination.Value),
                new SpellEffectFact("place", place.Name),
            ]);
    }

    /// <summary>Detection: a report read from the places and the population the world holds.</summary>
    /// <remarks>
    /// <para>
    /// Nothing is revealed that the world does not already hold: the places the party's own state says it has
    /// been to, what stands in the place it is in, and how far off the nearest of it is. Each spell's row
    /// states what it looks over — the whole map's known places, everything alive here, or who is here by name
    /// — and one path reads all three from the same world.
    /// </para>
    /// <para>
    /// <b>A detection teaches the party nothing durable, so it writes no auto note.</b> What it shows is a
    /// reading of state the world already holds, taken at the moment of the casting: the places the party has
    /// been to are the world's own per-place state and are published in the places the report names, and who is
    /// standing here now is a moment that a note would make false within the hour. The design's own list of what
    /// auto notes hold — potion discoveries, fountain effects, obelisk clues, and odd events
    /// (<c>docs/research/mm7-manual-outline.md</c> p.165 from the manual p.22) — is a list of facts the party
    /// <em>gained</em>, and a detection grants none: the party knew where it had been before it cast, and the
    /// automap fills in as territory is <em>seen</em>, which is the party's own memory rather than a note.
    /// Recording a note per casting would also make the record a log of how often the party looked rather than
    /// of what it learned, which is the opposite of what a note is for.
    /// </para>
    /// <para>
    /// <b>What a detection does leave is its own effect, and the map reads it while it runs.</b> The casting
    /// carries the detection for as long as its row states, and the automap marks what that scope names for
    /// exactly as long as the effect is held — so a lapsed detection reveals nothing, and no mark of it is
    /// written into what the party has mapped. The manual draws an icon for these spells at the automap's own
    /// corner for the same reason (<c>docs/research/mm7-manual-outline.md</c> p.162 from the manual pp.18–20),
    /// and its own line for the reveal is what our reading of each scope follows (p.116 from the manual p.18).
    /// </para>
    /// </remarks>
    private SpellApplicationOutcome Detect(SpellApplication application)
    {
        SpellReading reading = _spells.ReadingOf(application.Spell);
        if (reading.Detection == DetectionScope.None || _world() is not { } world)
        {
            return Unexpressed(application, "no world to report over");
        }

        // The reveal lasts exactly as long as the effect does, which is a deadline on the one clock like every
        // other duration a spell leaves: the automap asks what is running, not what was cast at some point.
        string carries = string.Empty;
        if (reading.DetectionLasts is { } rowLasts && Ledger is { } carried)
        {
            (_, GameDuration duration) = Worth(application, (_, _) => 0, rowLasts);
            carried.Start(SpellEffectIds.Detection(reading.Detection), magnitude: 0, duration);
            carries = string.Create(
                CultureInfo.InvariantCulture,
                $" The map reads it until {Moment(application, duration)}.");
        }

        List<SpellEffectFact> facts = [];
        List<string> lines = [];
        PlaceDefinition here = world.Graph.Require(world.Place);
        IReadOnlyList<PlacePopulationEntity> population = world.Population.Entities;

        if (reading.Detection == DetectionScope.Places)
        {
            int visited = world.Places.States.Count(state => state.Visited);
            int discovered = world.Places.States.Count(state => state.Discovered);
            int creatures = population.Count(entity => IsKind(entity, "monster"));
            int people = population.Count(entity => IsKind(entity, "person"));
            facts.Add(new SpellEffectFact("places.visited", visited.ToString(CultureInfo.InvariantCulture)));
            facts.Add(new SpellEffectFact("places.discovered", discovered.ToString(CultureInfo.InvariantCulture)));
            facts.Add(new SpellEffectFact("places.total", world.Graph.Places.Count.ToString(CultureInfo.InvariantCulture)));
            facts.Add(new SpellEffectFact("here", here.Name));
            facts.Add(new SpellEffectFact("here.creatures", creatures.ToString(CultureInfo.InvariantCulture)));
            facts.Add(new SpellEffectFact("here.people", people.ToString(CultureInfo.InvariantCulture)));
            lines.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"{visited} of {world.Graph.Places.Count} places are known, {discovered} of them by name"));
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"{here.Name} holds {creatures} creature(s) and {people} person(s)"));
        }
        else if (reading.Detection == DetectionScope.Life)
        {
            List<PlacePopulationEntity> alive = [.. population.Where(entity => entity.IsAlive)];
            facts.Add(new SpellEffectFact("here.life", alive.Count.ToString(CultureInfo.InvariantCulture)));
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"{alive.Count} living thing(s) stand in {here.Name}"));
            if (Nearest(world, alive) is { } nearest)
            {
                facts.Add(new SpellEffectFact("nearest", nearest.Name));
                facts.Add(new SpellEffectFact("nearest.distance", ((int)nearest.Distance).ToString(CultureInfo.InvariantCulture)));
                lines.Add(string.Create(CultureInfo.InvariantCulture, $"the nearest is {nearest.Name} at {nearest.Distance:0} units"));
            }
        }
        else
        {
            List<string> names = [];
            foreach (PlacePopulationEntity entity in population.Where(entity => entity.IsAlive))
            {
                string name = entity.Placement?.Source.GetString("name") ?? string.Empty;
                if (name.Length > 0) names.Add(name);
            }

            facts.Add(new SpellEffectFact("here.minds", names.Count.ToString(CultureInfo.InvariantCulture)));
            if (names.Count > 0) facts.Add(new SpellEffectFact("minds", string.Join(", ", names)));
            lines.Add(names.Count == 0
                ? $"nobody in {here.Name} answers"
                : string.Create(CultureInfo.InvariantCulture, $"{names.Count} mind(s) in {here.Name}: {string.Join(", ", names)}"));
        }

        facts.Add(new SpellEffectFact("detection", Scope(reading.Detection)));
        return SpellApplicationOutcome.Expressed(
            application.Spell.Effect,
            $"{application.Spell.Name}: {string.Join("; ", lines)}.{carries}",
            facts);
    }

    /// <summary>What one detection scope looks over, in the words the panel and the automap both read.</summary>
    /// <param name="scope">The scope.</param>
    /// <returns>The word.</returns>
    internal static string Scope(DetectionScope scope) => scope switch
    {
        DetectionScope.Places => "places",
        DetectionScope.Life => "life",
        DetectionScope.Minds => "minds",
        _ => "none",
    };

    /// <summary>Utility: a carried effect on the character the table aims it at or on the party, or the ending of the ones other spells left.</summary>
    /// <remarks>
    /// A buff is the same shape as a ward — a carried effect with a deadline, on one character or on the band
    /// as the table's own aim states — and what reads it is a fight's own answer: a haste is recovery, a
    /// blessing is the chance to land a blow, heroism and hammerhands are what a blow is worth, a fate is the
    /// luck a resistance check and a saving throw read, and invisibility is whether a creature notices the
    /// party at all. A dispelling is the ledger's own ending of what spells left running, on the party and on
    /// its characters alike, which is why it takes nothing a counter sold: a passage and a membership are
    /// carried effects too and are not magic.
    /// </remarks>
    private SpellApplicationOutcome Utility(SpellApplication application)
    {
        SpellReading reading = _spells.ReadingOf(application.Spell);
        if (reading.Dispels)
        {
            if (Ledger is not { } ledger) return Unexpressed(application, "nothing is running to dispel");
            IReadOnlyList<RunningSpellEffect> ended = ledger.EndAll();
            return ended.Count == 0
                ? Unexpressed(application, "nothing a spell left running was acting on the party")
                : SpellApplicationOutcome.Expressed(
                    application.Spell.Effect,
                    $"{application.Spell.Name}: the magic acting on the party ends ({string.Join(", ", ended.Select(effect => effect.Effect.Value))}).",
                    [.. ended.Select(effect => new SpellEffectFact("dispelled", effect.Effect.Value))]);
        }

        // Years given back and a score raised for good are a character's own progression, written on the
        // character the table aims the casting at.
        if (reading.Rejuvenates) return Rejuvenate(application, reading);
        if (reading.ForGood is { } attribute) return RaiseForGood(application, reading, attribute);

        // Spell points given back: a potion's own shape, restored through each carrier's pool, which is the
        // same owner a casting spends from. The pool stops at its own maximum, so an over-full drink is the
        // same clamp the donor's own potion makes.
        if (reading.ManaPerLevel > 0 || reading.ManaBase > 0)
        {
            List<PartyMember> restored = [.. reading.OnMember ? Carriers(application) : Members(application)];
            if (restored.Count == 0) return Unexpressed(application, "nobody to give spell points to");

            int level = _spells.LevelOf(application);
            int points = reading.ManaBase + (reading.ManaPerLevel * level);
            foreach (PartyMember member in restored) member.Resources.RestoreSpellPoints(points);
            return Expressed(
                application,
                $"{string.Join(", ", restored.Select(member => member.Profile.Name))} — {points} spell point(s)",
                [.. restored.Select(member => new SpellEffectFact("spellPoints", member.Resources.SpellPoints.Current.ToString(CultureInfo.InvariantCulture)))]);
        }

        return Carry(application, reading);
    }

    /// <summary>Every year a character was aged beyond their natural age given back.</summary>
    /// <remarks>
    /// The donor's rejuvenation sets the character's age modifier to nothing
    /// (<c>OpenEnroth/src/Engine/Objects/Character.cpp:3297-3299</c>); the natural years the clock has run are not
    /// touched, because nothing unnatural made them. Faithful.
    /// </remarks>
    private static SpellApplicationOutcome Rejuvenate(SpellApplication application, SpellReading reading)
    {
        IReadOnlyList<PartyMember> carries = reading.OnMember ? Carriers(application) : Members(application);
        if (carries.Count == 0) carries = Carriers(application);
        List<SpellEffectFact> facts = [];
        List<string> lines = [];
        foreach (PartyMember member in carries)
        {
            int years = member.Progression.Rejuvenate();
            facts.Add(new SpellEffectFact("rejuvenated", string.Create(CultureInfo.InvariantCulture, $"{member.Profile.Name} {years}")));
            lines.Add(years == 0
                ? $"{member.Profile.Name} had been aged by nothing, and nothing changes"
                : string.Create(CultureInfo.InvariantCulture, $"{member.Profile.Name} is given back {years} year(s)"));
        }

        return Expressed(application, string.Join("; ", lines), facts);
    }

    /// <summary>A score raised for good, once for each character and score.</summary>
    /// <remarks>
    /// The donor adds fifty to the score a pure potion names, once in a character's life, and a second bottle of the
    /// same one does nothing — it is still drunk (<c>OpenEnroth/src/Engine/Objects/Character.cpp:3282-3295</c>). The
    /// once is kept in the party's records under the character and the score, which a save carries. Faithful.
    /// </remarks>
    private static SpellApplicationOutcome RaiseForGood(SpellApplication application, SpellReading reading, AttributeId attribute)
    {
        IReadOnlyList<PartyMember> carries = Carriers(application);
        if (carries.Count == 0) carries = Members(application);
        List<SpellEffectFact> facts = [];
        List<string> lines = [];
        foreach (PartyMember member in carries)
        {
            string once = SpellEffectIds.ForGood(member.Id, attribute);
            if (application.Party.Records.Has(once) || !member.Attributes.TryGet(attribute, out _))
            {
                lines.Add($"{member.Profile.Name} has had this one already, and nothing changes");
                facts.Add(new SpellEffectFact("already", member.Profile.Name));
                continue;
            }

            member.Attributes.Change(attribute, reading.ForGoodBy);
            application.Party.Records.Mark(once);
            lines.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"{member.Profile.Name}'s {attribute} is raised by {reading.ForGoodBy} for good, to {member.Attributes[attribute]}"));
            facts.Add(new SpellEffectFact(attribute.Value, member.Attributes[attribute].ToString(CultureInfo.InvariantCulture)));
        }

        return Expressed(application, string.Join("; ", lines), facts);
    }

    /// <summary>Whether a reading leaves a carried effect at all.</summary>
    private static bool Carries(SpellReading reading) => reading.Buff is not null || reading.Buffs is { Length: > 0 };

    /// <summary>
    /// Lands every carried effect a reading states, each on its own carrier and with its own deadline.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A buff is the same shape as a ward — a carried effect with a deadline, on one character or on the band as the
    /// table's own aim states, or on every character under their own entry where the donor gives each of them one
    /// (an hour of power's blessing) — and what reads it is a fight's own answer.
    /// </para>
    /// <para>
    /// <b>A haste is withheld from the weak.</b> The donor gives no haste while a character it would land on is
    /// weak — the spell is spent and nothing runs (<c>OpenEnroth/src/Engine/Spells/CastSpellInfo.cpp:826-838</c>, and
    /// <c>:2560-2590</c> for an hour of power; the potion at <c>src/Engine/Objects/Character.cpp:3124-3128</c>) — so
    /// a reading marked so is skipped and the outcome says why.
    /// </para>
    /// </remarks>
    private SpellApplicationOutcome Carry(SpellApplication application, SpellReading reading)
    {
        if (Ledger is not { } running) return Unexpressed(application, "a utility this build cannot apply to that target");
        List<BuffReading> buffs = [];
        if (reading.Buff is { } single) buffs.Add(single);
        if (reading.Buffs is { } several) buffs.AddRange(several);
        if (buffs.Count == 0) return Unexpressed(application, "a utility this build cannot apply to that target");

        IReadOnlyList<PartyMember> carries = reading.OnMember ? Carriers(application) : [];
        if (reading.OnMember && carries.Count == 0)
        {
            return Unexpressed(application, "no character to carry an effect the table aims at one");
        }

        List<SpellEffectFact> facts = [];
        List<string> lines = [];
        int landed = 0;
        foreach (BuffReading buff in buffs)
        {
            IReadOnlyList<PartyMember> on = buff.OnEach
                ? [.. application.Party.Members.Where(member => !LaidOut(member))]
                : carries;
            IReadOnlyList<PartyMember> judged = on.Count > 0 ? on : application.Party.Members;
            if (buff.SparesTheWeak && judged.Any(member => member.Conditions.Has(MightAndMagic7Conditions.Weak)))
            {
                lines.Add($"{buff.Effect} is withheld, because a character it would land on is weak");
                facts.Add(new SpellEffectFact("withheld", buff.Effect.Value));
                continue;
            }

            (int power, GameDuration lasts) = Worth(application, buff.Power, buff.Lasts);
            if (on.Count == 0) running.Start(buff.Effect, power, lasts);
            else foreach (PartyMember member in on) running.StartOn(member, buff.Effect, power, lasts);

            string who = on.Count == 1 ? on[0].Profile.Name : buff.OnEach ? "every character" : "the party";
            lines.Add(buffs.Count == 1
                ? $"{who} carries it at {power} until the clock reaches the end of {Describe(lasts)}"
                : $"{who} carries {buff.Effect} at {power} until the clock reaches the end of {Describe(lasts)}");
            facts.Add(new SpellEffectFact("effect", buff.Effect.Value));
            facts.Add(new SpellEffectFact("power", power.ToString(CultureInfo.InvariantCulture)));
            facts.Add(new SpellEffectFact("until", Moment(application, lasts)));
            facts.AddRange(on.Select(member => new SpellEffectFact("carried", member.Profile.Name)));
            landed++;
        }

        return landed == 0
            ? Unexpressed(application, string.Join("; ", lines))
            : Expressed(application, string.Join("; ", lines), facts);
    }

    /// <summary>What a spell's target resolves to, which is the named member or the whole band.</summary>
    /// <remarks>
    /// A spell aimed at one of the party's own members touches that member; a spell aimed at the band, or one
    /// whose aim names nobody, touches everybody. A casting the fight holds resolves its aim against the
    /// caster's own side already, so the identity here is one the party holds.
    /// </remarks>
    private static IReadOnlyList<PartyMember> Members(SpellApplication application)
    {
        if (application.Target is { } target && application.Spell.Targeting == SpellTargeting.Ally)
        {
            foreach (PartyMember member in application.Party.Members)
            {
                if (CombatantId.Of(member.Id) == target) return [member];
            }
        }

        return application.Party.Members;
    }

    /// <summary>What a spell's own numbers are worth at this caster's school level and mastery.</summary>
    private (int Power, GameDuration Lasts) Worth(
        SpellApplication application,
        Func<int, int, int> power,
        Func<int, int, GameDuration> lasts)
    {
        int level = _spells.LevelOf(application);
        int mastery = MightAndMagic7Spells.MasteryOf(application.Caster, application.Spell);
        return (power(level, mastery), lasts(level, mastery));
    }

    /// <summary>Whether a travel spell's aim names something the party can actually reach.</summary>
    private Refusal? TravelRefusalOf(SpellApplication application)
    {
        SpellReading reading = _spells.ReadingOf(application.Spell);
        if (reading.Travel is TravelShape.None or TravelShape.WaterWalk) return null;
        if (_world() is not { } world)
        {
            return SpellRefusals.NoValidTarget(application.Spell.Name, "no world stands around the party");
        }

        // A leap needs ground to leap from: the donor refuses a jump while the party is in the air
        // (OpenEnroth src/Engine/Spells/CastSpellInfo.cpp:1113-1117), and so does this, before anything is paid.
        if (reading.Travel == TravelShape.Leap)
        {
            return world.Mover is { CanLeap: true }
                ? null
                : new Refusal(MightAndMagic7Codes.SpellAirborne, $"{application.Spell.Name} cannot be cast while the party is not standing on anything.");
        }

        // Flight needs the open sky: the donor refuses it indoors (OpenEnroth src/Engine/Spells/CastSpellInfo.cpp:1156-1160),
        // and so does this, before anything is paid.
        if (reading.Travel == TravelShape.Flight)
        {
            PlaceDefinition here = world.Graph.Require(world.Place);
            return here.Kind == PlaceKind.Interior
                ? new Refusal(MightAndMagic7Codes.SpellIndoors, $"{application.Spell.Name} cannot be cast under a roof, and {here.Name} has one.")
                : null;
        }

        if (reading.Travel == TravelShape.Beacon && application.TargetName.Length == 0)
        {
            // Setting a beacon names no place: the party sets it where it stands.
            return null;
        }

        if (application.TargetName.Length == 0)
        {
            return SpellRefusals.NoTarget(application.Spell.Name, SpellTargetings.WireName(application.Spell.Targeting));
        }

        string named = application.TargetName;
        if (reading.Travel == TravelShape.Beacon)
        {
            PlaceId? beacon = BeaconPlace();
            if (beacon is null)
            {
                return SpellRefusals.NoValidTarget(application.Spell.Name, "the party has set no beacon to recall");
            }

            if (!string.Equals(beacon.Value.Value, named, StringComparison.Ordinal))
            {
                return SpellRefusals.NoValidTarget(application.Spell.Name, named);
            }

            return null;
        }

        PlaceDefinition? place = world.Graph.Find(new PlaceId(named));
        if (place is null) return SpellRefusals.NoValidTarget(application.Spell.Name, named);
        // A place the world holds and cannot be reached is a different refusal from a name nothing answers to,
        // so each carries its own code rather than sharing the invalid-target one with a sentence to tell them apart.
        if (!world.Places.StateOf(place.Id).Visited)
        {
            return new Refusal(
                MightAndMagic7Codes.SpellPlaceUnvisited,
                $"{application.Spell.Name} cannot reach {place.Name}, which the party has never been to.");
        }

        return place.EntryPoints.Count == 0
            ? new Refusal(
                MightAndMagic7Codes.SpellPlaceNoArrival,
                $"{application.Spell.Name} cannot reach {place.Name}, which states nowhere to arrive.")
            : null;
    }

    /// <summary>What the party's own carried state says the beacon stands in, or null when none is set.</summary>
    private PlaceId? BeaconPlace()
    {
        if (_running is not { } ledger) return null;
        foreach (string held in Beacons(ledger.Party))
        {
            if (SpellEffectIds.BeaconPlace(held) is { } place) return place;
        }

        return null;
    }

    /// <summary>Every beacon the party has on record, in the order they were set.</summary>
    private static IReadOnlyList<string> Beacons(PartyEntity party) =>
        [.. party.Records.All.Select(record => record.Name).Where(name => SpellEffectIds.BeaconPlace(name) is not null)];

    /// <summary>Everything alive in the party's place, with how far off the nearest of it stands.</summary>
    private static (string Name, double Distance)? Nearest(SessionWorld world, IReadOnlyList<PlacePopulationEntity> alive)
    {
        string name = string.Empty;
        double nearest = double.MaxValue;
        foreach (PlacePopulationEntity entity in alive)
        {
            string placed = entity.Placement?.Source.GetString("name") ?? entity.Placement?.Content.Id ?? string.Empty;
            double distance = entity.Pose.DistanceTo(world.Party.PlacePose);
            if (distance >= nearest) continue;
            nearest = distance;
            name = placed.Length > 0 ? placed : "something";
        }

        return name.Length == 0 ? null : (name, nearest);
    }

    /// <summary>Whether a placement is of one content kind.</summary>
    private static bool IsKind(PlacePopulationEntity entity, string kind) =>
        string.Equals(entity.Placement?.Content.Kind, kind, StringComparison.Ordinal);

    /// <summary>Whether a member is laid out, which is what a raising and a shared life both skip.</summary>
    internal static bool LaidOut(PartyMember member) =>
        member.Conditions.Has(MightAndMagic7Conditions.Dead) ||
        member.Conditions.Has(MightAndMagic7Conditions.Petrified) ||
        member.Conditions.Has(MightAndMagic7Conditions.Eradicated);

    /// <summary>What one member's hit points read as now, for the facts a panel shows.</summary>
    private static SpellEffectFact Pool(PartyMember member) => new(
        "hitPoints",
        string.Create(
            CultureInfo.InvariantCulture,
            $"{member.Profile.Name} {member.Resources.HitPoints.Current}/{member.Resources.HitPoints.Maximum}"));

    /// <summary>What conditions one member carries now, written the way the party's own state spells them.</summary>
    private static string Conditions(PartyMember member) => member.Conditions.Count == 0
        ? string.Empty
        : string.Join(", ", member.Conditions.Active.Select(condition => condition.ToString()));

    /// <summary>When an effect applied now ends, written for a person, empty when nothing has said.</summary>
    private string Moment(SpellApplication application, GameDuration lasts)
    {
        if (_clock is not { } clock) return string.Empty;
        GameDate ends = clock.Calendar.Add(clock.Now, lasts);
        return ends.MinuteText;
    }

    /// <summary>The game time between where the clock stands and the next time its daylight window opens.</summary>
    /// <remarks>
    /// This is the clock's own window read the way the rest mechanism reads it for waiting until dawn, so a
    /// light and a night's sleep agree about when morning is.
    /// </remarks>
    private static GameDuration UntilDawn(GameClock clock)
    {
        GameCalendar calendar = clock.Calendar;
        GameDate now = clock.Now;
        GameDate today = new(now.Year, now.Month, now.Day);
        GameDuration dawn = GameDuration.FromMinutes(clock.Daylight.Dawn.Minutes);
        GameDate opens = calendar.Add(today, dawn);

        // Dawn already passed when the window's own hour and minute are no later than the clock's, because
        // both moments are on the same day of the same month: the next dawn is the one after a whole day.
        if (opens.Hour < now.Hour || (opens.Hour == now.Hour && opens.Minute <= now.Minute))
        {
            opens = calendar.Add(calendar.Add(today, calendar.Days(1)), dawn);
        }

        return calendar.Between(now, opens);
    }

    /// <summary>How long a length of game time is, in the words a person reads.</summary>
    private static string Describe(GameDuration lasts)
    {
        long minutes = lasts.Milliseconds / GameDuration.MillisecondsPerSecond / GameDuration.SecondsPerMinute;
        if (minutes >= GameDuration.MinutesPerHour)
        {
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{minutes / GameDuration.MinutesPerHour}h {minutes % GameDuration.MinutesPerHour}m");
        }

        return minutes > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{minutes}m")
            : string.Create(CultureInfo.InvariantCulture, $"{lasts.Milliseconds}ms");
    }

    /// <summary>The shorter of two lengths of game time.</summary>
    private static GameDuration Min(GameDuration left, GameDuration right) =>
        left.Milliseconds <= right.Milliseconds ? left : right;

    /// <summary>What a spell this build cannot apply reports, naming the gap in its own sentence.</summary>
    private static SpellApplicationOutcome Unexpressed(SpellApplication application, string why) =>
        SpellApplicationOutcome.Unexpressed(
            application.Spell.Effect,
            $"{application.Spell.Name} was cast, and this build expresses nothing for it yet: {why}.");

    /// <summary>What a spell this build applied reports, with the facts behind the sentence.</summary>
    private static SpellApplicationOutcome Expressed(SpellApplication application, string what, IReadOnlyList<SpellEffectFact> facts) =>
        SpellApplicationOutcome.Expressed(
            application.Spell.Effect,
            string.Create(CultureInfo.InvariantCulture, $"{application.Spell.Name}: {what}."),
            facts);

    /// <summary>What one casting of a light is worth at the first rung of mastery.</summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/Spells/CastSpellInfo.cpp:328-346</c>: a torch light is worth two at novice,
    /// three at expert, and four at master and grand master.
    /// </remarks>
    private const int TorchLightNovice = 2;

    /// <summary>What one casting of a light is worth at the second rung of mastery.</summary>
    private const int TorchLightExpert = 3;

    /// <summary>What one casting of a light is worth at the third and fourth rungs of mastery.</summary>
    private const int TorchLightMaster = 4;
}
