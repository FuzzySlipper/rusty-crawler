using System.Globalization;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using Rusty.Engine;
using Rusty.Engine.Entities;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// The one resolution path: an accepted attack is resolved into a hit or a miss, damage inside what the
/// ruleset stated, what the target's resistance took off it, what condition followed, and whether the target
/// went down — through the same path whatever kind of attack it was.
/// </summary>
/// <remarks>
/// Every number here is this suite's own, which is the point of the seam: the kit holds no hit chance, no
/// dice, no resistance, and no threshold, so a test states its own and demands that the same mechanism serve
/// it. What the mechanism must do is fixed — ask for the plan, roll it, apply it to whoever owns the target's
/// health, record what it did, and reach the panel — and none of that depends on whose numbers they are.
/// </remarks>
public sealed class CombatResolutionTests
{
    private static readonly ContentLayout Layout = new("packs", "imports", "bundles");
    private static readonly PlaceId Hall = new("1");
    private static readonly RaceId TestRace = new("testfolk");
    private static readonly ClassId Fighter = new("fighter");
    private static readonly DamageKindId Steel = new("steel");
    private static readonly ConditionId Poisoned = new("poisoned");
    private static readonly ConditionId Unconscious = new("unconscious");
    private static readonly ConditionId Dead = new("dead");
    private static readonly AttributeId Endurance = new("endurance");

    /// <summary>What this suite's swings cost, so an order is never refused while it is being proved.</summary>
    private static readonly GameDuration Swing = GameDuration.FromSeconds(1);

    [Fact]
    public void Additional_damage_uses_each_kind_s_resistance_and_a_hit_observer_sees_the_settled_health_once()
    {
        DamageKindId heat = new("heat");
        DamageKindId cold = new("cold");
        Rules rules = new(damage: DamageRoll.Flat(2))
        {
            Additional = [new(heat, DamageRoll.Flat(6)), new(cold, DamageRoll.Flat(4))],
            PartResistances = new Dictionary<DamageKindId, Resistance> { [heat] = Resistance.Of(1), [cold] = Resistance.Immune },
        };
        using SessionWorld world = World(rules, out PartyEntity party, creatureAt: 60);
        using (party)
        {
            Arrive(world);
            HitObserver observer = new();
            CombatState fight = new(Capabilities.Combat(rules) with { Hits = [observer] }, party, world, TestClock.Create());
            fight.Step();
            Combatant actor = fight.Combatants.Single(combatant => combatant.Subject.IsMember);
            Combatant target = fight.Combatants.Single(combatant => !combatant.Subject.IsMember);
            Assert.True(fight.Order(new AttackOrder(actor.Id, AttackKind.Melee, target.Id)).IsApplied);
            CombatResolution hit = fight.LastResolution!;
            Assert.Equal(5, hit.Damage);
            Assert.Equal(35, hit.TargetHitPoints);
            Assert.Equal([new CombatDamagePart(heat, 6, 3), new CombatDamagePart(cold, 4, 0)], hit.Additional);
            Assert.Contains("3 heat", hit.Message);
            Assert.Contains("0 cold", hit.Message);
            CombatHit settled = Assert.Single(observer.Hits);
            Assert.Equal(5, settled.Damage);
            Assert.Equal(35, observer.TargetHealth);
        }
    }

    private sealed class HitObserver : ICombatHitObserver
    {
        internal List<CombatHit> Hits { get; } = [];
        internal int TargetHealth { get; private set; }
        public void Observe(CombatHit hit)
        {
            Hits.Add(hit);
            TargetHealth = CreatureHealth.Find(hit.Target.Entity!.Actor)!.Current;
        }
    }

    [Fact]
    public void A_hit_chance_lands_the_attack_and_a_miss_chance_does_not()
    {
        // The same attack, twice, with nothing different but the chance the ruleset stated: one rolls under
        // it and lands, the other rolls over it and misses. The roll itself is the suite's, so the boundary
        // is exact rather than probable.
        Rules certain = new(hitChance: HitChance.Of(5000), roll: 4999);
        CombatResolution hit = Resolve(certain, out SessionWorld world, out PartyEntity party);
        using (world)
        using (party)
        {
            Assert.True(hit.Hit);
            Assert.Equal(4999, hit.HitRoll);
            Assert.True(hit.Damage > 0);
        }

        Rules doubtful = new(hitChance: HitChance.Of(5000), roll: 5000);
        CombatResolution miss = Resolve(doubtful, out SessionWorld missed, out PartyEntity untouched);
        using (missed)
        using (untouched)
        {
            Assert.False(miss.Hit);
            Assert.Equal(0, miss.Damage);
            Assert.Equal(HitChance.Of(5000), miss.Chance);
        }
    }

    [Fact]
    public void Damage_lands_inside_the_bounds_the_ruleset_stated()
    {
        // Two six-sided dice and a bonus of three: the least is five and the most is fifteen, and the roll
        // the fight records is inside that whatever the dice came up.
        for (int die = 1; die <= 6; die++)
        {
            Rules rules = new(damage: new DamageRoll(dice: 2, sides: 6, bonus: 3), face: die);
            CombatResolution resolution = Resolve(rules, out SessionWorld world, out PartyEntity party);
            using (world)
            using (party)
            {
                Assert.True(resolution.Hit);
                Assert.InRange(resolution.Rolled, 5, 15);
                Assert.InRange(resolution.Damage, 5, 15);
                Assert.Equal(Steel, resolution.DamageKind);
            }
        }

        // A roll with a floor produces the floor even when the dice and the bonus come to less, which is how
        // a game states that a landed blow always does something.
        Rules floored = new(damage: new DamageRoll(dice: 1, sides: 2, bonus: -5, floor: 1), face: 1, resist: Resistance.Of(0));
        CombatResolution soft = Resolve(floored, out SessionWorld softWorld, out PartyEntity softParty);
        using (softWorld)
        using (softParty)
        {
            Assert.Equal(1, soft.Rolled);
            Assert.Equal(1, soft.Damage);
        }
    }

    [Fact]
    public void A_resistant_target_takes_less_and_an_immune_one_takes_none()
    {
        // The same blow against three targets: one that resists nothing, one that resists as the ruleset
        // says, and one that is immune. What the target resists is read for the attack's own kind, and the
        // harm that lands is what the ruleset's arithmetic made of it.
        Rules bare = new(damage: new DamageRoll(dice: 1, sides: 4), face: 4);
        CombatResolution plain = Resolve(bare, out SessionWorld plainWorld, out PartyEntity plainParty);
        using (plainWorld)
        using (plainParty)
        {
            Assert.Equal(4, plain.Damage);
            Assert.Equal("0", plain.Resistance.ToString());
        }

        Rules tough = new(damage: new DamageRoll(dice: 1, sides: 4), face: 4, resist: Resistance.Of(30));
        CombatResolution resisted = Resolve(tough, out SessionWorld toughWorld, out PartyEntity toughParty);
        using (toughWorld)
        using (toughParty)
        {
            // This suite's ruleset halves a resisted blow once, which is the reading being honoured rather
            // than any particular arithmetic of the kit's.
            Assert.Equal(4, resisted.Rolled);
            Assert.Equal(2, resisted.Damage);
            Assert.Equal("30", resisted.Resistance.ToString());
        }

        Rules warded = new(damage: new DamageRoll(dice: 1, sides: 4), face: 4, resist: Resistance.Immune);
        CombatResolution immune = Resolve(warded, out SessionWorld wardedWorld, out PartyEntity wardedParty);
        using (wardedWorld)
        using (wardedParty)
        {
            Assert.True(immune.Hit);
            Assert.Equal(4, immune.Rolled);
            Assert.Equal(0, immune.Damage);
            Assert.Equal("immune", immune.Resistance.ToString());
            Assert.Contains("immune", immune.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void A_condition_a_hit_leaves_is_applied_reported_and_can_be_cleared()
    {
        // What a bite leaves is the ruleset's answer: the fight applies it through the member's own
        // conditions, names what inflicted it, and reports it on the result. Nothing is edited silently, and
        // what was applied can be read back and cleared by whoever owns that removal.
        Rules venomous = new(condition: Poisoned);
        CombatResolution resolution = MemberResolve(venomous, out SessionWorld world, out PartyEntity party, out PartyMember victim);
        using (world)
        using (party)
        {
            Assert.True(resolution.Hit);
            Assert.NotNull(resolution.Condition);
            Assert.Equal(Poisoned, resolution.Condition!.Condition);
            Assert.Equal("the bite of a test creature", resolution.Condition.Source);
            Assert.True(victim.Conditions.Has(Poisoned));
            Assert.Contains("poisoned", resolution.Message, StringComparison.Ordinal);

            // The stated removal path: the same conditions owner a temple's cure and a night's rest write to.
            Assert.True(victim.Conditions.Clear(Poisoned));
            Assert.False(victim.Conditions.Has(Poisoned));
        }
    }

    [Fact]
    public void A_condition_that_stops_an_actor_acting_is_refused_by_name_and_costs_nothing()
    {
        // A condition's effect is the ruleset's answer too: an actor it lays out may not act, is told so by
        // name, and owes no recovery for an attack it never made.
        Rules rules = new(incapacitated: true);
        using SessionWorld world = World(rules, out PartyEntity party, creatureAt: 100);
        using (party)
        {
            Arrive(world);
            CombatState combat = Fight(world, party, rules);
            combat.Step();
            Combatant member = combat.Combatants.First(combatant => combatant.Side == CombatSide.Party);
            CombatResult refused = combat.Order(new AttackOrder(member.Id, AttackKind.Melee, combat.Opposition[0].Id));

            Assert.False(refused.IsApplied);
            Assert.Equal("incapacitated", refused.Code);
            Assert.True(member.IsReady);
        }
    }

    [Fact]
    public void Harm_that_empties_a_members_pool_leaves_the_condition_their_own_health_gives()
    {
        // Three stages out of one pool: emptied into unconsciousness, emptied deeper into death, and a
        // member who is still in the party and in the fight with a condition rather than a removal. The
        // thresholds are this suite's ruleset's, and the kit's mechanism is what applies them.
        Rules rules = new(deathThreshold: 6, damage: new DamageRoll(dice: 1, sides: 4), face: 4);
        using SessionWorld world = World(rules, out PartyEntity party, creatureAt: 100, hitPoints: 4, pool: 4);
        using (party)
        {
            Arrive(world);
            CombatState combat = Fight(world, party, rules);
            combat.Step();
            Combatant beast = combat.Opposition[0];
            Combatant first = combat.Combatants.First(combatant => combatant.Side == CombatSide.Party);

            // The creature strikes the first member down: the pool empties and the wound leaves them out.
            CombatResult ordered = combat.Order(new AttackOrder(beast.Id, AttackKind.Melee, first.Id));
            Assert.True(ordered.IsApplied);
            PartyMember struck = party.Member(first.Id.Subject());
            Assert.Equal(0, struck.Resources.HitPoints.Current);
            Assert.True(struck.Conditions.Has(Unconscious));
            Assert.True(combat.IsDown(first));

            // Out of the fight is not out of the party: the member is still a combatant, still published, and
            // still carries their identity.
            Assert.Contains(combat.Combatants, combatant => combatant.Id == first.Id);
            Assert.Single(party.Members);
            Assert.Contains(party.Members, member => member.Id == struck.Id);

            // Harm below empty accumulates rather than starting again: one more blow leaves the member four
            // points under, which is not yet past what their endurance is worth, so they are still only
            // unconscious. The creature must recover between blows, which is the one pacing advancing.
            combat.Observe(Advance(Swing.Milliseconds));
            combat.Order(new AttackOrder(beast.Id, AttackKind.Melee, first.Id));
            Assert.Equal(4, struck.Resources.Deficit);
            Assert.True(struck.Conditions.Has(Unconscious));

            // A third blow crosses the threshold, and the condition ladders from unconsciousness to death.
            combat.Observe(Advance(Swing.Milliseconds));
            combat.Order(new AttackOrder(beast.Id, AttackKind.Melee, first.Id));
            Assert.Equal(8, struck.Resources.Deficit);
            Assert.True(struck.Conditions.Has(Dead), struck.Conditions.Active.Count.ToString(CultureInfo.InvariantCulture));
            Assert.False(struck.Conditions.Has(Unconscious));

            // The way back is a removal like any other: what a cure clears is the condition, and the member
            // who was dead can act again once it is gone.
            Assert.True(struck.Conditions.Clear(Dead));
            Assert.True(combat.IsDown(first) is false);
        }
    }

    [Fact]
    public void An_attack_at_nothing_resolves_nothing_and_still_costs_its_recovery()
    {
        // An actor with nothing in reach has acted: there is no resolution because there was nothing to
        // resolve against, and the recovery is still what it cost.
        Rules rules = new();
        using SessionWorld world = World(rules, out PartyEntity party, creatureAt: 9000);
        using (party)
        {
            world.ArriveAt(Hall, Pose());
            world.Populate();
            CombatState combat = Fight(world, party, rules);
            combat.Step();
            Combatant member = combat.Combatants.First(combatant => combatant.Side == CombatSide.Party);
            CombatResult result = combat.Order(new AttackOrder(member.Id, AttackKind.Melee, null));

            Assert.True(result.IsApplied);
            Assert.Null(result.Resolution);
            Assert.Null(combat.LastResolution);
            Assert.False(member.IsReady);
        }
    }

    [Fact]
    public void The_same_seed_and_the_same_state_produce_the_same_damage()
    {
        // Randomness is drawn from the engine's service under a key that names the attack, so two identical
        // fights with the same seed resolve identically and a different seed does not.
        SeededRandom one = new(seed: 99);
        Rules rules = new(random: one, hitChance: HitChance.Always);
        CombatResolution first = Resolve(rules, out SessionWorld firstWorld, out PartyEntity firstParty);
        using (firstWorld)
        using (firstParty)
        {
            SeededRandom again = new(seed: 99);
            Rules repeated = new(random: again, hitChance: HitChance.Always);
            CombatResolution second = Resolve(repeated, out SessionWorld secondWorld, out PartyEntity secondParty);
            using (secondWorld)
            using (secondParty)
            {
                Assert.Equal(first.HitRoll, second.HitRoll);
                Assert.Equal(first.Rolled, second.Rolled);
                Assert.Equal(first.Damage, second.Damage);
                Assert.True(one.Draws > 0);
            }

            SeededRandom other = new(seed: 100);
            Rules different = new(random: other, hitChance: HitChance.Always);
            CombatResolution third = Resolve(different, out SessionWorld thirdWorld, out PartyEntity thirdParty);
            using (thirdWorld)
            using (thirdParty)
            {
                Assert.NotEqual(first.Rolled, third.Rolled);
            }
        }
    }

    [Fact]
    public void A_death_is_reported_once_at_the_blow_that_caused_it_however_long_the_body_lies()
    {
        Rules rules = new(hitChance: HitChance.Always, damage: DamageRoll.Flat(50));
        using SessionWorld world = World(rules, out PartyEntity party, creatureAt: 100, hitPoints: 4);
        using PartyEntity owned = party;
        Arrive(world);
        List<CreatureDeath> heard = [];
        CombatState combat = new(Capabilities.Combat(rules) with { Deaths = [new Heard(heard)] }, party, world, TestClock.Create());
        combat.Step();
        Combatant member = combat.Combatants.First(combatant => combatant.Side == CombatSide.Party);
        Combatant beast = combat.Opposition[0];

        CombatResult blow = combat.Order(new AttackOrder(member.Id, AttackKind.Melee, beast.Id));
        Assert.True(blow.Resolution!.TargetDown);

        // The fight goes on reading the place for as long as the party stands there, and the body lies in it
        // all that time: the death was one moment, and it was heard once, with the creature's own placement.
        for (int update = 0; update < 5; update++)
        {
            combat.Step();
            combat.Observe(Advance(60_000));
        }

        CreatureDeath death = Assert.Single(heard);
        Assert.Equal(Hall, death.Place);
        Assert.Equal(beast.Subject.Placement!.Content, death.Placement.Content);
        Assert.Equal(beast.Name, death.Name);
    }

    [Fact]
    public void A_creature_has_its_health_and_its_own_position_from_the_moment_it_is_placed()
    {
        // No fight has been composed, let alone read the place: the population is what gives a creature what
        // its row states it can take, so a trap or a spell that reaches it first finds a creature, not nothing.
        using SessionWorld world = World(new Rules(), out PartyEntity party, creatureAt: 100, hitPoints: 40);
        using PartyEntity owned = party;
        Arrive(world);
        PlacePopulationEntity creature = Assert.Single(world.Population.Entities, entity => entity.Content.Kind == "creature");

        CreatureHealth health = Assert.IsType<CreatureHealth>(CreatureHealth.Find(creature.Actor));
        Assert.Equal(40, health.Maximum);
        Assert.Equal(40, health.Current);

        // Where it stands is its own, starting where the placement put it and moving only when it is moved.
        Assert.Equal(creature.Placement.Pose, creature.Pose);
        creature.MoveTo(new PlacePose(250, 0, 0, 0, 0));
        Assert.Equal(250, creature.Pose.X, 3);
        Assert.Equal(100, creature.Placement.Pose.X, 3);
    }

    /// <summary>Records every death a fight reports, in the order it reports them.</summary>
    [Fact]
    public void A_defence_divides_a_blow_and_a_reflection_turns_harm_back_onto_the_attacker()
    {
        // A creature swings at a member who carries a defence the ruleset states as a halving and a reflection of
        // three: the fight divides what the dice rolled before resistance and lands what comes back on the
        // creature's own health.
        Rules rules = new(damage: new DamageRoll(dice: 1, sides: 8), face: 8, divisor: 2, reflect: 3);
        CombatResolution struck = MemberResolve(rules, out SessionWorld world, out PartyEntity party, out PartyMember victim);
        using (world)
        using (party)
        {
            Assert.Equal(8, struck.Rolled);
            Assert.Equal(2, struck.Divisor);
            Assert.Equal(4, struck.Damage);
            Assert.Equal(16, victim.Resources.HitPoints.Current);
            Assert.Equal(3, struck.Reflected);
            Assert.False(struck.ActorDown);
            Assert.Contains("divided the 8 rolled by 2", struck.Message, StringComparison.Ordinal);
            Assert.Contains("3 is turned back onto a test creature", struck.Message, StringComparison.Ordinal);
            Assert.Equal(1, CreatureHealth.Find(world.Population.Entities[0].Actor)!.Current);
        }

        // A reflection that empties the creature takes it down, and the fight says so.
        Rules sharper = new(damage: new DamageRoll(dice: 1, sides: 8), face: 8, reflect: 4);
        CombatResolution felled = MemberResolve(sharper, out SessionWorld again, out PartyEntity band, out _);
        using (again)
        using (band)
        {
            Assert.True(felled.ActorDown);
            Assert.True(CreatureHealth.Find(again.Population.Entities[0].Actor)!.IsDown);
        }
    }

    [Fact]
    public void What_a_spell_leaves_on_a_creature_is_its_own_and_runs_out_with_the_fights_clock()
    {
        Rules rules = new();
        using SessionWorld world = World(rules, out PartyEntity party, creatureAt: 100);
        using (party)
        {
            Arrive(world);
            CombatState combat = Fight(world, party, rules);
            combat.Step();
            Combatant beast = combat.Opposition[0];

            // A placed creature carries its effects from the moment it is placed, empty until something leaves one.
            CreatureEffects effects = CreatureEffects.Find(beast.Subject.Entity!.Actor)!;
            Assert.False(effects.Any);
            EffectId held = new("held");
            effects.Apply(held, magnitude: 2, GameDuration.FromSeconds(10));
            Assert.Equal(2, effects.MagnitudeOf(held));

            // The fight's own advances count it down, and the advance that reaches its length ends it; meanwhile the
            // fight names it among what a save would drop.
            combat.Observe(Advance(9_000));
            Assert.True(effects.Has(held));
            Assert.Contains(combat.UnsavedFight(), left => left.Subject == "spelled");
            combat.Observe(Advance(1_000));
            Assert.False(effects.Has(held));

            // An act that is not a blow can still hold a creature back and put it into the fight.
            long owed = beast.Recovery.Milliseconds;
            Assert.True(combat.Delay(beast.Id, GameDuration.FromSeconds(3)));
            Assert.Equal(owed + 3_000, beast.Recovery.Milliseconds);
            Assert.True(combat.Provoke(beast.Id));
            Assert.False(combat.Provoke(combat.Combatants.First(combatant => combatant.Side == CombatSide.Party).Id));
        }
    }

    [Fact]
    public void An_act_against_a_creature_turns_those_the_game_says_stand_with_it_and_nobody_else()
    {
        Rules rules = new(peaceful: true);
        using SessionWorld world = World(
            rules,
            out PartyEntity party,
            creatureAt: 100,
            others: """
                ,
                { "id": "kin", "kind": "creature", "x": 200, "y": 0, "z": 0, "monster": "7", "hitPoints": 40, "kin": "hall" },
                { "id": "stranger", "kind": "creature", "x": 300, "y": 0, "z": 0, "monster": "7", "hitPoints": 40, "kin": "road" }
                """);
        using (party)
        {
            Arrive(world);
            CombatState combat = Fight(world, party, rules);
            combat.Step();
            Combatant Creature(string id) => combat.Combatants.First(combatant => combatant.Subject.Placement?.Content.Id == id);
            Combatant member = combat.Combatants.First(combatant => combatant.Subject.IsMember);
            Assert.All(
                combat.Combatants.Where(combatant => !combatant.Subject.IsMember),
                creature => Assert.Equal(CombatSide.Neutral, creature.Side));

            // The party's blow at one creature is remembered against it and against its kin, and the fight keeps
            // both on every later read; the creature of another kin is left as it stood.
            Assert.True(combat.Order(new AttackOrder(member.Id, AttackKind.Melee, Creature("beast").Id)).IsApplied);
            combat.Step();
            Assert.Equal(CombatSide.Opposition, Creature("beast").Side);
            Assert.Equal(CombatSide.Opposition, Creature("kin").Side);
            Assert.Equal(CombatSide.Neutral, Creature("stranger").Side);

            // The fight's other provocation, an act that is not a blow, reaches the same way.
            Assert.True(combat.Provoke(Creature("stranger").Id));
            combat.Step();
            Assert.Equal(CombatSide.Opposition, Creature("stranger").Side);
        }
    }

    [Fact]
    public void A_creatures_attack_does_not_make_its_neutral_victim_or_kin_enemies_of_the_party()
    {
        Rules rules = new(peaceful: true, damage: DamageRoll.Flat(2));
        using SessionWorld world = World(
            rules,
            out PartyEntity party,
            creatureAt: 100,
            others: """
                ,
                { "id": "kin", "kind": "creature", "x": 200, "y": 0, "z": 0, "monster": "7", "hitPoints": 40, "kin": "hall" },
                { "id": "attacker", "kind": "creature", "x": 300, "y": 0, "z": 0, "monster": "7", "hitPoints": 40, "kin": "road" }
                """);
        using (party)
        {
            Arrive(world);
            CombatState combat = Fight(world, party, rules);
            combat.Step();
            Combatant Creature(string id) => combat.Combatants.Single(actor => actor.Subject.Placement?.Content.Id == id);
            Combatant victim = Creature("beast");
            Combatant attacker = Creature("attacker");
            Assert.True(combat.Provoke(attacker.Id));

            // The AI uses the same order gate as the party. Its blow still harms the victim,
            // but neither the victim nor its kin has a reason to turn against the party.
            Assert.True(combat.Order(new AttackOrder(attacker.Id, AttackKind.Melee, victim.Id)).IsApplied);
            Assert.Equal(38, combat.Vitals(victim).Current);
            combat.Step();
            Assert.Equal(CombatSide.Neutral, victim.Side);
            Assert.Equal(CombatSide.Neutral, Creature("kin").Side);
            Assert.False(combat.IsHostile(victim));
            Assert.False(combat.IsHostile(Creature("kin")));
            Assert.Equal(attacker.Id, Assert.Single(combat.Opposition).Id);

            // A later party attack really is party provocation and still reaches the kin.
            Combatant member = combat.Combatants.Single(actor => actor.Subject.IsMember);
            Assert.True(combat.Order(new AttackOrder(member.Id, AttackKind.Melee, victim.Id)).IsApplied);
            combat.Step();
            Assert.Equal(CombatSide.Opposition, victim.Side);
            Assert.Equal(CombatSide.Opposition, Creature("kin").Side);
        }
    }

    [Fact]
    public void A_creature_standing_with_the_party_is_on_its_side_whatever_the_party_did_to_it()
    {
        Rules rules = new(allied: true);
        using SessionWorld world = World(rules, out PartyEntity party, creatureAt: 100);
        using (party)
        {
            Arrive(world);
            CombatState combat = Fight(world, party, rules);
            combat.Step();
            Combatant beast = combat.Combatants.First(combatant => !combatant.Subject.IsMember);
            Combatant member = combat.Combatants.First(combatant => combatant.Subject.IsMember);

            // What made it stand with the party outranks a provocation: it is an ally, nothing is fighting the party,
            // and an order to turn either of them on the other is refused by name.
            Assert.True(combat.Provoke(beast.Id));
            combat.Step();
            Assert.Equal(CombatSide.Ally, beast.Side);
            Assert.False(combat.IsEngaged);
            Assert.Equal(CombatCodes.FriendlyTarget, combat.Order(new AttackOrder(member.Id, AttackKind.Melee, beast.Id)).Refusal!.Code);
            Assert.Equal(CombatCodes.FriendlyTarget, combat.Order(new AttackOrder(beast.Id, AttackKind.Melee, member.Id)).Refusal!.Code);

            // The party's own act aims at nothing standing with it.
            Assert.All(combat.Engage(), result => Assert.Null(result.Initiated?.Target));
        }
    }

    private sealed class Heard(List<CreatureDeath> heard) : ICreatureDeathObserver
    {
        public void Died(CreatureDeath death) => heard.Add(death);
    }

    /// <summary>Resolves one party attack against a creature of a staged world, and hands back what it did.</summary>
    private static CombatResolution Resolve(Rules rules, out SessionWorld world, out PartyEntity party)
    {
        world = World(rules, out party, creatureAt: 100, hitPoints: 40);
        Arrive(world);
        CombatState combat = Fight(world, party, rules);
        combat.Step();
        Combatant member = combat.Combatants.First(combatant => combatant.Side == CombatSide.Party);
        CombatResult result = combat.Order(new AttackOrder(member.Id, AttackKind.Melee, combat.Opposition[0].Id));
        Assert.True(result.IsApplied);
        Assert.NotNull(result.Resolution);
        return result.Resolution!;
    }

    /// <summary>Resolves one creature attack against a member, and hands back what it did and who took it.</summary>
    private static CombatResolution MemberResolve(Rules rules, out SessionWorld world, out PartyEntity party, out PartyMember victim)
    {
        world = World(rules, out party, creatureAt: 100, hitPoints: 4);
        Arrive(world);
        CombatState combat = Fight(world, party, rules);
        combat.Step();
        Combatant beast = combat.Opposition[0];
        Combatant member = combat.Combatants.First(combatant => combatant.Side == CombatSide.Party);
        victim = party.Member(member.Id.Subject());
        CombatResult result = combat.Order(new AttackOrder(beast.Id, AttackKind.Melee, member.Id));
        Assert.True(result.IsApplied);
        Assert.NotNull(result.Resolution);
        return result.Resolution!;
    }

    /// <summary>A party of one member, with the health rule this suite states.</summary>
    private static PartyEntity Party(Rules rules, int pool = 20)
    {
        MemberCreation member = new(new PartyMemberSeed(
            "Victim",
            TestRace,
            Fighter,
            [
                new AttributeScore(Endurance, 10),
            ],
            skills: [],
            spells: [],
            experience: 0,
            level: 1,
            skillPoints: 0,
            classRank: 1,
            conditions: [],
            hitPoints: ResourcePool.Full(pool),
            spellPoints: ResourcePool.Full(5)));

        return new PartyEntityFactory(health: rules.Health).Create(new PartyCreation([member], 0, 0, ProvisionUnit.Portions, 0, 0));
    }

    /// <summary>The fight this suite exercises: the kit's state over the suite's own rule.</summary>
    private static CombatState Fight(SessionWorld world, PartyEntity party, Rules rules) =>
        new(Capabilities.Combat(rules), party, world, TestClock.Create());

    private static PlacePose Pose() => new(0, 0, 0, 0, 0);

    /// <summary>Puts the party in the hall and populates it, as one admitted update of the session does.</summary>
    private static void Arrive(SessionWorld world)
    {
        world.ArriveAt(Hall, Pose());
        world.Populate();
    }

    /// <summary>One advance of the clock, as the session hands it to the mechanisms that keep time.</summary>
    private static ClockAdvance Advance(long milliseconds) => new(
        new GameDate(1168, 1, 1, 9, 0, 0),
        new GameDate(1168, 1, 1, 9, 0, 1),
        GameDuration.FromMilliseconds(milliseconds),
        PeriodCrossings.None,
        []);

    /// <summary>A hall holding one creature inside its notice range, and a party to fight it.</summary>
    private static SessionWorld World(Rules rules, out PartyEntity party, double creatureAt, int hitPoints = 40, int pool = 20, string others = "")
    {
        party = Party(rules, pool);
        return World(party, creatureAt, hitPoints, others);
    }

    private static SessionWorld World(PartyEntity party, double creatureAt, int hitPoints, string others = "")
    {
        ContentCatalog catalog = ContentCatalogLoader.Load(
            new InMemoryContentSource()
                .Add(
                    "packs/world/pack.json",
                    """
                    {
                      "schemaVersion": 1, "packId": "world", "kind": "definitions",
                      "provenance": { "description": "test content" },
                      "documents": [ { "path": "places.json", "documentId": "places", "definitionKind": "place" } ]
                    }
                    """)
                .Add(
                    "packs/world/places.json",
                    $$"""
                    {
                      "documentId": "places",
                      "definitionKind": "place",
                      "entries": [
                        { "id": "1", "kind": "region", "name": "Hall", "respawnDays": 7,
                          "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
                          "placements": [
                            { "id": "beast", "kind": "creature", "x": {{creatureAt.ToString(CultureInfo.InvariantCulture)}}, "y": 0, "z": 0, "monster": "7",
                              "hitPoints": {{hitPoints}}, "kin": "hall" }{{others}}
                          ] }
                      ]
                    }
                    """),
            Layout).RequireValid();

        PlaceGraph graph = PlaceGraphLoader.Load(catalog);
        PartyPoseOwner pose = new(new PartyPose(Hall, Pose()), new FacingRule(unitsPerTurn: 2048, minimumPitch: -512, maximumPitch: 512));
        return new SessionWorld(
            graph,
            pose,
            new PlaceStateLedger(graph, PlaceRespawnRule.FromContent()),
            new FreeTravel(),
            TestClock.Create(),
            mover: null,
            diagnostics: null,
            entrances: null,
            clock: TestClock.Create(),
            resources: null,
            partyEntity: party,
            interaction: null,
            schedule: null,
            vitals: Capabilities.PlacementHitPoints);
    }

    /// <summary>
    /// The numbers and readings this suite's fights are resolved with: a chance, dice, a resistance, what a
    /// hit leaves, who may act, what a target can take, and the thresholds a wound is judged against.
    /// </summary>
    private sealed class Rules : ICombatRule, ICombatResolutionRule, ICombatReflectionRule, ICombatProvocationRule
    {
        private readonly IRandomService? _random;
        private readonly int _roll;
        private readonly int _face;
        private readonly int _divisor;
        private readonly int _reflect;
        private readonly bool _allied;
        private readonly bool _peaceful;

        internal Rules(
            IRandomService? random = null,
            HitChance? hitChance = null,
            int roll = 0,
            int face = 1,
            DamageRoll? damage = null,
            Resistance? resist = null,
            ConditionId? condition = null,
            bool incapacitated = false,
            int deathThreshold = 6,
            int divisor = 1,
            int reflect = 0,
            bool allied = false,
            bool peaceful = false)
        {
            _allied = allied;
            _peaceful = peaceful;
            _random = random;
            _divisor = divisor;
            _reflect = reflect;
            _roll = roll;
            _face = face;
            HitChance = hitChance ?? HitChance.Always;
            Damage = damage ?? new DamageRoll(dice: 1, sides: 4);
            Resist = resist ?? Resistance.Of(0);
            Condition = condition;
            Incapacitated = incapacitated;
            Health = new Thresholds(deathThreshold);
        }

        internal HitChance HitChance { get; }

        internal DamageRoll Damage { get; }

        internal Resistance Resist { get; }

        internal ConditionId? Condition { get; }

        internal bool Incapacitated { get; }

        internal ICharacterHealthRule Health { get; }

        public string NameOf(CombatSubject subject) => subject.Member?.Profile.Name ??
            (subject.Placement?.Content.Kind == "creature" ? "a test creature" : "something else");

        public Hostility NatureOf(CombatSubject subject) => subject.Member is not null
            ? Hostility.Peaceful
            : subject.Placement?.Content.Kind == "creature"
                ? _allied ? Hostility.Allied : _peaceful ? Hostility.Peaceful : Hostility.Aggressive(500)
                : Hostility.Inert;

        /// <summary>This suite's alarm: an act against a creature turns every other creature of its stated kin.</summary>
        public bool ProvokedWith(CombatSubject provoked, CombatSubject bystander) =>
            provoked.Placement?.Source.GetString("kin") is { Length: > 0 } kin &&
            string.Equals(bystander.Placement?.Source.GetString("kin"), kin, StringComparison.Ordinal);

        public AttackKind AttackKindFor(CombatSubject subject) => AttackKind.Melee;

        public GameDuration RecoveryAfter(CombatSubject subject, AttackKind kind) => Swing;

        /// <summary>A creature acts the moment it is seen, so a test does not have to wait it out.</summary>
        public GameDuration InitialRecovery(CombatSubject subject, AttackKind kind) => GameDuration.None;

        public double ReachOf(CombatSubject subject, AttackKind kind) => 1000;

        public IAttackRolls? RollsFor(CombatSubject attacker, string key) => _random is { } random
            ? new KeyedRolls(random, seed: 7, "test.attack", key)
            : new Scripted(_roll, _face);

        internal IReadOnlyList<AttackDamagePart>? Additional { get; init; }
        internal IReadOnlyDictionary<DamageKindId, Resistance> PartResistances { get; init; } = new Dictionary<DamageKindId, Resistance>();

        public AttackPlan PlanOf(CombatSubject attacker, CombatSubject target, AttackKind kind) => new(
            HitChance,
            Steel,
            Damage,
            Resist,
            target.Member is not null ? _divisor : 1, Additional);

        /// <summary>This suite's reflection: a member turns a stated amount back onto a creature that hurt them.</summary>
        public int ReflectedOnto(CombatSubject attacker, CombatSubject target, DamageKindId kind, int harm, IAttackRolls rolls) =>
            target.Member is not null && attacker.Member is null ? _reflect : 0;

        /// <summary>This suite's own resistance arithmetic: immunity takes all, resistance halves once.</summary>
        public int DamageAfterResistance(CombatSubject target, DamageKindId kind, int damage, IAttackRolls rolls)
        {
            Resistance resistance = PartResistances.GetValueOrDefault(kind, Resist);
            return resistance.IsImmune ? 0 : resistance.Points > 0 ? damage / 2 : damage;
        }

        public CombatCondition? ConditionOf(CombatSubject attacker, CombatSubject target, DamageKindId kind, IAttackRolls rolls) =>
            Condition is { } condition && target.Member is not null
                ? new CombatCondition(condition, 1, "the bite of a test creature")
                : null;

        /// <summary>This suite's own reading of what stops an actor: being laid out, and this flag.</summary>
        /// <remarks>
        /// Only a member is laid out by the flag: a creature this suite stages has no condition model of its
        /// own, so a fight that treated one as down would count it out of the opposition it is meant to be in.
        /// </remarks>
        public bool CanAct(CombatSubject subject) =>
            subject.Member is not { } member ||
            (!Incapacitated && !member.Conditions.Has(Unconscious) && !member.Conditions.Has(Dead));

        public int HitPointsOf(CombatSubject subject) => subject.Member is { } member
            ? member.Resources.HitPoints.Maximum
            : subject.Placement?.Source.GetInt32("hitPoints") ?? 0;

        /// <summary>The thresholds this suite's wounds are judged against, standing in for a game's own.</summary>
        private sealed class Thresholds(int deathThreshold) : ICharacterHealthRule
        {
            public CharacterCollapse Collapse(PartyMember member, int hitPoints, int deficit)
            {
                if (hitPoints - deficit >= 1) return CharacterCollapse.None;
                return deficit < deathThreshold
                    ? new CharacterCollapse(new ActiveCondition(Unconscious))
                    : new CharacterCollapse(new ActiveCondition(Dead), [Unconscious]);
            }
        }
    }

    /// <summary>The rolls a test states: one value for every purpose, so a boundary is exact.</summary>
    private sealed class Scripted(int roll, int face) : IAttackRolls
    {
        public int Roll(string purpose, int minimum, int maximum)
        {
            if (purpose.StartsWith("hit", StringComparison.Ordinal)) return roll;
            if (purpose.StartsWith("damage", StringComparison.Ordinal)) return face;
            return minimum;
        }
    }

    /// <summary>
    /// The engine's keyed randomness, answered deterministically: the same seed and the same key always draw
    /// the same value, so two identical states can be compared without anything being recorded.
    /// </summary>
    private sealed class SeededRandom(ulong seed) : IRandomService
    {
        /// <summary>How many draws were taken, so a test can tell a roll from a guess.</summary>
        public int Draws { get; private set; }

        /// <inheritdoc />
        public KeyedRngReceipt DrawKeyed(KeyedRngRequest request)
        {
            Draws++;
            ulong hash = 14695981039346656037UL ^ seed;
            foreach (byte value in System.Text.Encoding.UTF8.GetBytes(request.Key))
            {
                hash = (hash ^ value) * 1099511628211UL;
            }

            long span = request.Maximum - request.Minimum + 1;
            return new KeyedRngReceipt(request.Minimum + (long)(hash % (ulong)span));
        }

        /// <inheritdoc />
        public Lcg15Receipt DrawLcg15(Lcg15Request request) => throw new NotSupportedException("Keyed rolls only.");

        /// <inheritdoc />
        public Rng CreateScoped(ScopedRngCreateRequest request) => throw new NotSupportedException("Keyed rolls only.");

        /// <inheritdoc />
        public Rng ForkScoped(ScopedRngForkRequest request) => throw new NotSupportedException("Keyed rolls only.");

        /// <inheritdoc />
        public RngValue NextU64(Rng stream) => throw new NotSupportedException("Keyed rolls only.");

        /// <inheritdoc />
        public RngValue NextBoundedU32(ScopedRngBoundedRequest request) => throw new NotSupportedException("Keyed rolls only.");

        /// <inheritdoc />
        public RngValue NextBool(Rng stream) => throw new NotSupportedException("Keyed rolls only.");
    }
}

/// <summary>Reads the member a combatant identity names, so a test can look at who was hurt.</summary>
internal static class CombatantIdExtensions
{
    /// <summary>The party member identity this combatant identity names.</summary>
    /// <param name="id">The combatant identity.</param>
    internal static PartyMemberId Subject(this CombatantId id) =>
        new(ulong.Parse(id.Value, CultureInfo.InvariantCulture));
}
