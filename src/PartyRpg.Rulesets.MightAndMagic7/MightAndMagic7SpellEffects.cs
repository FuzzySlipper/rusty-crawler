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
internal sealed class MightAndMagic7SpellEffects : ISpellEffectRule, ISpellAimRule, IPartySightRule, IRunningSpellEffects, IMemberSpellEffects, IGameTimeObserver
{
    private readonly MightAndMagic7Spells _spells;
    private readonly GameClock? _clock;
    private readonly Func<SessionWorld?> _world;
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
    /// <exception cref="ArgumentNullException">No spell table was supplied.</exception>
    internal MightAndMagic7SpellEffects(MightAndMagic7Spells spells, GameClock? clock = null, Func<SessionWorld?>? world = null)
    {
        _spells = spells ?? throw new ArgumentNullException(nameof(spells));
        _clock = clock;
        _world = world ?? (() => null);
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
    public SpellRefusal? Judge(SpellApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);
        if (application.Fight is { } fight && fight.Find(application.CasterId) is { } caster)
        {
            if (!caster.IsReady)
            {
                return SpellRefusal.CannotAct(
                    caster.Name,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"it is still recovering, with {caster.Recovery.Milliseconds}ms of game time left before it may act again"));
            }

            if (fight.IsDown(caster)) return SpellRefusal.CannotAct(caster.Name, "what is acting on them leaves them unable to cast");
        }

        // A spell this build cannot aim is refused before it is paid for, and the refusal names what the spell
        // would act on and who owns the way to name one. A spell that was paid for and then changed nothing
        // would be the worst of both: the points are gone and the player was told nothing.
        SpellReading reading = _spells.ReadingOf(application.Spell);
        if (reading.Unaimable)
        {
            return SpellRefusal.TargetUnavailable(application.Spell.Name, reading.Missing, reading.Receiver);
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
    public void Observe(ClockAdvance advance)
    {
        ArgumentNullException.ThrowIfNull(advance);
        _running?.Observe(advance);
    }

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
        IReadOnlyList<PartyMember> members = Members(application);
        if (reading.Healing == HealingMode.None || members.Count == 0)
        {
            return Unexpressed(application, "no member to give health to");
        }

        int level = MightAndMagic7Spells.SkillLevelOf(application.Caster, application.Spell);
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

        int level = Math.Max(1, MightAndMagic7Spells.SkillLevelOf(application.Caster, application.Spell));
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
        if (_world() is not { } world) return Unexpressed(application, "no world to travel through");
        if (reading.Travel == TravelShape.None || reading.Travel == TravelShape.Movement)
        {
            return Unexpressed(application, "a way of moving this build's mover does not have");
        }

        if (reading.Travel == TravelShape.Beacon && application.TargetName.Length == 0)
        {
            // Setting a beacon is the one travel act that moves nobody: the place the party stands in is
            // written into its own carried state, and every earlier beacon is dropped because the donor's
            // beacon is one place rather than a list.
            foreach (EffectId held in Beacons(application.Party)) application.Party.Effects.Remove(held);
            application.Party.Effects.Apply(new PartyEffect(SpellEffectIds.Beacon(world.Place), 1));
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
    /// Nothing is revealed that the world does not already hold: the places the party's own state says it has
    /// been to, what stands in the place it is in, and how far off the nearest of it is. Each spell's row
    /// states what it looks over — the whole map's known places, everything alive here, or who is here by name
    /// — and one path reads all three from the same world.
    /// </remarks>
    private SpellApplicationOutcome Detect(SpellApplication application)
    {
        SpellReading reading = _spells.ReadingOf(application.Spell);
        if (reading.Detection == DetectionScope.None || _world() is not { } world)
        {
            return Unexpressed(application, "no world to report over");
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

        return SpellApplicationOutcome.Expressed(
            application.Spell.Effect,
            $"{application.Spell.Name}: {string.Join("; ", lines)}.",
            facts);
    }

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

        if (reading.Buff is not { } buff || Ledger is not { } running)
        {
            return Unexpressed(application, "a utility this build cannot apply to that target");
        }

        (int power, GameDuration lasts) = Worth(application, buff.Power, buff.Lasts);
        IReadOnlyList<PartyMember> carries = reading.OnMember ? Carriers(application) : [];
        if (reading.OnMember && carries.Count == 0)
        {
            return Unexpressed(application, "no character to carry an effect the table aims at one");
        }

        if (carries.Count == 0)
        {
            running.Start(buff.Effect, power, lasts);
        }
        else
        {
            foreach (PartyMember member in carries) running.StartOn(member, buff.Effect, power, lasts);
        }

        string who = carries.Count == 1 ? carries[0].Profile.Name : "the party";
        return Expressed(
            application,
            $"{who} carries it at {power} until the clock reaches the end of {Describe(lasts)}",
            [
                new SpellEffectFact("effect", buff.Effect.Value),
                new SpellEffectFact("power", power.ToString(CultureInfo.InvariantCulture)),
                new SpellEffectFact("until", Moment(application, lasts)),
                .. carries.Select(member => new SpellEffectFact("carried", member.Profile.Name)),
            ]);
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
    private static (int Power, GameDuration Lasts) Worth(
        SpellApplication application,
        Func<int, int, int> power,
        Func<int, int, GameDuration> lasts)
    {
        int level = MightAndMagic7Spells.SkillLevelOf(application.Caster, application.Spell);
        int mastery = MightAndMagic7Spells.MasteryOf(application.Caster, application.Spell);
        return (power(level, mastery), lasts(level, mastery));
    }

    /// <summary>Whether a travel spell's aim names something the party can actually reach.</summary>
    private SpellRefusal? TravelRefusalOf(SpellApplication application)
    {
        SpellReading reading = _spells.ReadingOf(application.Spell);
        if (reading.Travel is TravelShape.None or TravelShape.Movement) return null;
        if (_world() is not { } world)
        {
            return SpellRefusal.NoValidTarget(application.Spell.Name, "no world stands around the party");
        }

        if (reading.Travel == TravelShape.Beacon && application.TargetName.Length == 0)
        {
            // Setting a beacon names no place: the party sets it where it stands.
            return null;
        }

        if (application.TargetName.Length == 0)
        {
            return SpellRefusal.NoTarget(application.Spell.Name, SpellTargetings.WireName(application.Spell.Targeting));
        }

        string named = application.TargetName;
        if (reading.Travel == TravelShape.Beacon)
        {
            PlaceId? beacon = BeaconPlace();
            if (beacon is null)
            {
                return SpellRefusal.NoValidTarget(application.Spell.Name, "the party has set no beacon to recall");
            }

            if (!string.Equals(beacon.Value.Value, named, StringComparison.Ordinal))
            {
                return SpellRefusal.NoValidTarget(application.Spell.Name, named);
            }

            return null;
        }

        PlaceDefinition? place = world.Graph.Find(new PlaceId(named));
        if (place is null) return SpellRefusal.NoValidTarget(application.Spell.Name, named);
        if (!world.Places.StateOf(place.Id).Visited)
        {
            return SpellRefusal.NoValidTarget(application.Spell.Name, $"{place.Name}, which the party has never been to");
        }

        return place.EntryPoints.Count == 0
            ? SpellRefusal.NoValidTarget(application.Spell.Name, $"{place.Name}, which states nowhere to arrive")
            : null;
    }

    /// <summary>What the party's own carried state says the beacon stands in, or null when none is set.</summary>
    private PlaceId? BeaconPlace()
    {
        if (_running is not { } ledger) return null;
        foreach (EffectId held in Beacons(ledger.Party))
        {
            if (SpellEffectIds.BeaconPlace(held) is { } place) return place;
        }

        return null;
    }

    /// <summary>Every beacon the party carries, in the order they were applied.</summary>
    private static IReadOnlyList<EffectId> Beacons(PartyEntity party)
    {
        List<EffectId> beacons = [];
        foreach (PartyEffect effect in party.Effects.Active)
        {
            if (SpellEffectIds.BeaconPlace(effect.Effect) is not null) beacons.Add(effect.Effect);
        }

        return beacons;
    }

    /// <summary>Everything alive in the party's place, with how far off the nearest of it stands.</summary>
    private static (string Name, double Distance)? Nearest(SessionWorld world, IReadOnlyList<PlacePopulationEntity> alive)
    {
        string name = string.Empty;
        double nearest = double.MaxValue;
        foreach (PlacePopulationEntity entity in alive)
        {
            string placed = entity.Placement?.Source.GetString("name") ?? entity.Placement?.Content.Id ?? string.Empty;
            double dx = entity.Pose.X - world.Party.PlacePose.X;
            double dy = entity.Pose.Y - world.Party.PlacePose.Y;
            double dz = entity.Pose.Z - world.Party.PlacePose.Z;
            double distance = Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
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
    private static bool LaidOut(PartyMember member) =>
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
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{ends.Year:0000}-{ends.Month:00}-{ends.Day:00} {ends.Hour:00}:{ends.Minute:00}");
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
