using System.Numerics;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Input;
using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Testing;
using Rusty.Engine;
using Xunit;
using static PartyRpg.Rulesets.MightAndMagic7.Tests.SpellEffectPolicyTests;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

/// <summary>
/// The spells whose effect is a reading the fight takes or a quantity the clock moves: a shield a missile meets, a
/// reflection a blow comes back through, a regeneration the clock's own advances give out, and the scores a day of
/// the gods and an hour of power raise.
/// </summary>
/// <remarks>
/// Every case casts a shipped spell by its own id through the product's own session, as the other spell suite does,
/// and reads the result from the fight's own answers or the member's own pool rather than from the effect path. The
/// numbers are the donor's, cited where the ruleset states them.
/// </remarks>
public sealed class SpellReadingPolicyTests
{
    [Fact]
    public void A_shield_halves_a_creatures_missile_against_the_character_it_was_cast_on()
    {
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(Readings());
        using IGameSession session = Casting(context, ui);
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        MightAndMagic7Combat policy = Fight(context, live.Party!);
        CombatSubject beast = Attacker(live, policy);
        CombatSubject caster = SubjectOf(live, policy, "Aelina");
        CombatSubject other = SubjectOf(live, policy, "Borin");
        Assert.Equal(1, policy.PlanOf(beast, caster, AttackKind.Ranged).Divisor);

        // The donor halves a monster projectile against a shielded character (OpenEnroth
        // src/Engine/Objects/Character.cpp:5987-6009); this game's table aims the spell at its caster.
        Cast(session, ui, 1, "17", string.Empty);
        Assert.Equal("cast", Magic(ui).Field("outcome").AsString());
        Assert.Equal(2, policy.PlanOf(beast, caster, AttackKind.Ranged).Divisor);

        // A blow in hand is not a missile, and the member the spell did not land on is not shielded.
        Assert.Equal(1, policy.PlanOf(beast, caster, AttackKind.Melee).Divisor);
        Assert.Equal(1, policy.PlanOf(beast, other, AttackKind.Ranged).Divisor);
    }

    [Fact]
    public void Pain_reflection_turns_a_creatures_blow_back_onto_it_through_the_fight()
    {
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(MeleeReachReadings());
        using IGameSession session = Casting(context, ui);
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        Cast(session, ui, 1, "95", string.Empty);
        Assert.Equal("cast", Magic(ui).Field("outcome").AsString());

        // A fight over the session's own world and party, with rolls to draw: the creature swings at the caster.
        TestRandomService random = new();
        MightAndMagic7Combat policy = Fight(context, live.Party!, random);
        CombatState fight = new(Capabilities.Combat(policy), live.Party!, live.World);
        fight.Step();
        Combatant beast = fight.Combatants.First(combatant => !combatant.Subject.IsMember);
        Combatant caster = fight.Combatants.First(combatant => combatant.Subject.Member?.Profile.Name == "Aelina");
        fight.Observe(new ClockAdvance(default, default, GameDuration.FromSeconds(60), PeriodCrossings.None, []));
        CreatureHealth health = CreatureHealth.Find(beast.Subject.Entity!.Actor)!;
        int before = health.Current;

        CombatResult struck = fight.Order(new AttackOrder(beast.Id, AttackKind.Melee, caster.Id));

        // What the caster took comes back onto the creature through its own resistance to the same kind of harm,
        // which this creature does not resist (OpenEnroth src/Engine/Objects/Character.cpp:5875-5900).
        CombatResolution resolved = struck.Resolution!;
        Assert.True(resolved.Damage > 0);
        Assert.Equal(resolved.Damage, resolved.Reflected);
        Assert.Equal(before - resolved.Damage, health.Current);
        Assert.Contains("turned back onto", resolved.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_regeneration_gives_health_back_for_every_five_minutes_the_clock_passes_until_its_deadline()
    {
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(Readings());
        using IGameSession session = Casting(context, ui);
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        PartyMember wounded = live.Party!.Members[1];
        wounded.Resources.TakeDamage(350);
        Assert.Equal(50, wounded.Resources.HitPoints.Current);

        // An expert regeneration is worth five hit points every five minutes and lasts an hour a level of the
        // school (OpenEnroth src/Engine/Spells/CastSpellInfo.cpp:744-766, src/Engine/Engine.cpp:1236, 1398-1401):
        // three levels of body are three hours.
        Cast(session, ui, 1, "71", Target(wounded));
        Assert.Equal("cast", Magic(ui).Field("outcome").AsString());

        // An hour is twelve five-minute boundaries.
        Advance(session, 1);
        Assert.Equal(50 + (12 * 5), wounded.Resources.HitPoints.Current);

        // Five more hours pass, and only the two the regeneration still ran for give anything back.
        Advance(session, 5);
        Assert.Equal(50 + (36 * 5), wounded.Resources.HitPoints.Current);
        Assert.Equal(0, Magic(ui).Field("memberRunning").Length());
    }

    [Fact]
    public void A_day_of_the_gods_raises_every_score_the_fight_reads_for_every_member()
    {
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(Readings());
        using IGameSession session = Casting(context, ui);
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        MightAndMagic7Combat policy = Fight(context, live.Party!);
        PartyMember knight = live.Party!.Members[1];
        CombatSubject beast = Attacker(live, policy);
        CombatSubject borin = SubjectOf(live, policy, "Borin");
        DamageRoll bare = policy.PlanOf(borin, beast, AttackKind.Melee).Damage;
        Assert.Equal(13, policy.ActualAttribute(knight, MightAndMagic7Combat.MightAttribute));

        // Grand master light at two levels: five a level plus ten, to all seven scores, for five hours a level
        // (OpenEnroth src/Engine/Spells/CastSpellInfo.cpp:2450-2475, read at Character.cpp:2360-2387).
        Cast(session, ui, 1, "83", string.Empty);
        Assert.Equal("cast", Magic(ui).Field("outcome").AsString());
        Assert.Equal(20d, Running(Magic(ui), "spell.day-of-the-gods").Field("magnitude").AsNumber());
        Assert.Equal(13 + 20, policy.ActualAttribute(knight, MightAndMagic7Combat.MightAttribute));
        Assert.Equal(9 + 20, policy.ActualAttribute(knight, MightAndMagic7Combat.LuckAttribute));

        // Might is what a blow adds, by the donor's own table.
        DamageRoll raised = policy.PlanOf(borin, beast, AttackKind.Melee).Damage;
        Assert.Equal(
            MightAndMagic7AttributeBonus.Of(33) - MightAndMagic7AttributeBonus.Of(13),
            raised.Bonus - bare.Bonus);
    }

    [Fact]
    public void An_hour_of_power_blesses_every_character_and_withholds_its_haste_from_a_weak_party()
    {
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(Readings());
        using IGameSession session = Casting(context, ui);
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        MightAndMagic7Combat policy = Fight(context, live.Party!);
        CombatSubject beast = Attacker(live, policy);
        CombatSubject borin = SubjectOf(live, policy, "Borin");
        GameDuration slow = policy.RecoveryAfter(borin, AttackKind.Melee);

        // A weak character keeps the haste from the whole party, as the donor's own hour of power does
        // (OpenEnroth src/Engine/Spells/CastSpellInfo.cpp:2560-2590); everything else lands.
        live.Party!.Members[1].Conditions.Apply(new ActiveCondition(MightAndMagic7Conditions.Weak));
        Cast(session, ui, 1, "86", string.Empty);
        ProjectedNode magic = Magic(ui);
        Assert.Equal("cast", magic.Field("outcome").AsString());
        Assert.Contains("withheld", magic.Field("message").AsString(), StringComparison.Ordinal);
        Assert.Equal(7d, MemberRunning(magic, live.Party.Members[0].Id.ToString(), "spell.bless").Field("magnitude").AsNumber());
        Assert.Equal(7d, MemberRunning(magic, live.Party.Members[1].Id.ToString(), "spell.bless").Field("magnitude").AsNumber());
        Assert.Equal(7d, Running(magic, "spell.heroism").Field("magnitude").AsNumber());
        Assert.Equal(7d, Running(magic, "spell.armour").Field("magnitude").AsNumber());
        Assert.Equal(2, policy.PlanOf(beast, borin, AttackKind.Ranged).Divisor);
        Assert.Equal(slow.Milliseconds, policy.RecoveryAfter(borin, AttackKind.Melee).Milliseconds);

        // Once nobody is weak, the haste lands with the rest and takes the donor's twenty-five ticks off an action.
        live.Party.Members[1].Conditions.Clear(MightAndMagic7Conditions.Weak);
        Cast(session, ui, 2, "86", string.Empty);
        Assert.Equal(25d, Running(Magic(ui), "spell.haste").Field("magnitude").AsNumber());
        Assert.True(policy.RecoveryAfter(borin, AttackKind.Melee).Milliseconds < slow.Milliseconds);
    }

    [Fact]
    public void A_boost_and_a_shield_drunk_from_a_bottle_are_read_where_the_fight_reads_them()
    {
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(Readings());
        using IGameSession session = Casting(context, ui);
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        MightAndMagic7Combat policy = Fight(context, live.Party!);
        PartyMember drinker = live.Party!.Members[0];
        CombatSubject beast = Attacker(live, policy);
        CombatSubject aelina = SubjectOf(live, policy, "Aelina");

        // Three times the potion's strength to the score, for thirty minutes a point (OpenEnroth
        // src/Engine/Objects/Character.cpp:3174-3212); a bottle the scenario packed is of the first strength.
        Drink(session, 1, 240);
        Assert.Equal("cast", Magic(ui).Field("outcome").AsString());
        Assert.Equal(9 + 3, policy.ActualAttribute(drinker, MightAndMagic7Combat.MightAttribute));
        Assert.Equal(13, policy.ActualAttribute(live.Party.Members[1], MightAndMagic7Combat.MightAttribute));

        // The shield potion is the donor's character shield, the same one the spell leaves (Character.cpp:3144-3149).
        Drink(session, 2, 232);
        Assert.Equal("cast", Magic(ui).Field("outcome").AsString());
        Assert.Equal(2, policy.PlanOf(beast, aelina, AttackKind.Ranged).Divisor);
    }

    [Fact]
    public void A_divine_intervention_ages_its_caster_and_age_is_read_into_the_scores_and_given_back_by_a_potion()
    {
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(Readings());
        using IGameSession session = Casting(context, ui);
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        MightAndMagic7Combat policy = Fight(context, live.Party!);
        PartyMember caster = live.Party!.Members[0];
        PartyMember knight = live.Party.Members[1];

        // The donor's price for a divine intervention is ten years on the caster (OpenEnroth
        // src/Engine/Spells/CastSpellInfo.cpp:2603-2607), which the save carries as the caster's own age.
        Cast(session, ui, 1, "88", string.Empty);
        Assert.Equal("cast", Magic(ui).Field("outcome").AsString());
        Assert.Equal(10, caster.Progression.AgeOffset);
        Assert.Equal(MightAndMagic7Ageing.StartingAge + 10, MightAndMagic7Ageing.AgeOf(caster, clock: null));

        // Past a hundred, the donor's ageing table takes a quarter off might and adds half to intellect
        // (OpenEnroth src/Engine/Objects/Character.cpp:222-232, 729-765).
        knight.Progression.Age(80);
        Assert.Equal(13 * 75 / 100, policy.ActualAttribute(knight, MightAndMagic7Combat.MightAttribute));
        Assert.Equal(9 * 150 / 100, policy.ActualAttribute(knight, MightAndMagic7Combat.IntellectAttribute));
        Assert.Equal(9, policy.ActualAttribute(knight, MightAndMagic7Combat.LuckAttribute));

        // A potion of rejuvenation gives the drinker back every year something aged them (Character.cpp:3297-3299).
        Drink(session, 2, 271);
        Assert.Equal("cast", Magic(ui).Field("outcome").AsString());
        Assert.Equal(0, caster.Progression.AgeOffset);
    }

    [Fact]
    public void A_divine_intervention_is_cast_three_times_a_day_and_the_count_clears_when_the_day_turns()
    {
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(Readings());
        using IGameSession session = Casting(context, ui);
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        PartyMember caster = live.Party!.Members[0];

        // The donor allows each character three a day (OpenEnroth src/Engine/Spells/CastSpellInfo.cpp:2592), and each
        // one ages its caster ten years (:2603-2607).
        for (ulong cast = 1; cast <= 3; cast++)
        {
            Cast(session, ui, cast, "88", string.Empty);
            Assert.Equal("cast", Magic(ui).Field("outcome").AsString());
        }

        Assert.Equal(30, caster.Progression.AgeOffset);

        // A fourth is refused before anything is spent: no points, no years.
        int points = caster.Resources.SpellPoints.Current;
        Cast(session, ui, 4, "88", string.Empty);
        ProjectedNode refused = Magic(ui);
        Assert.Equal("refused", refused.Field("outcome").AsString());
        Assert.Equal(MightAndMagic7Codes.SpellDailyLimit, refused.Field("code").AsString());
        Assert.Equal(points, caster.Resources.SpellPoints.Current);
        Assert.Equal(30, caster.Progression.AgeOffset);

        // The count is the caster's own, kept in the party's records so a save carries it.
        Assert.Contains(live.Party.Records.All, record => record.Name.StartsWith("spell.per-day.88.", StringComparison.Ordinal) && record.Count == 3);

        // A whole day of game time crosses the donor's three o'clock once (src/Engine/Engine.cpp:1036-1081): the day
        // has turned, the caster may cast it again, and the earlier day's count is gone from the record.
        Advance(session, 24);
        Cast(session, ui, 200, "88", string.Empty);
        Assert.Equal("cast", Magic(ui).Field("outcome").AsString());
        Assert.Equal(40, caster.Progression.AgeOffset);
        Assert.Single(live.Party.Records.All, record => record.Name.StartsWith("spell.per-day.88.", StringComparison.Ordinal));
        Assert.Contains(live.Party.Records.All, record => record.Name.StartsWith("spell.per-day.88.", StringComparison.Ordinal) && record.Count == 1);
    }

    [Fact]
    public void A_pure_potion_raises_its_score_for_good_once_in_a_characters_life()
    {
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(Readings());
        using IGameSession session = Casting(context, ui);
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        PartyMember drinker = live.Party!.Members[0];

        // Fifty to the score, once (OpenEnroth src/Engine/Objects/Character.cpp:3282-3295).
        Drink(session, 1, 264);
        Assert.Equal("cast", Magic(ui).Field("outcome").AsString());
        Assert.Equal(13 + 50, drinker.Attributes[MightAndMagic7Combat.LuckAttribute]);

        // A second bottle is drunk and does nothing, which the donor's own record of that potion decides.
        Drink(session, 2, 264);
        Assert.Equal("cast", Magic(ui).Field("outcome").AsString());
        Assert.Contains("already", Magic(ui).Field("message").AsString(), StringComparison.Ordinal);
        Assert.Equal(13 + 50, drinker.Attributes[MightAndMagic7Combat.LuckAttribute]);
        Assert.DoesNotContain(live.Party.Items, item => item.Definition.Value == "264");
    }

    [Fact]
    public void A_jump_leaps_from_where_the_party_stands_and_a_feather_fall_is_carried_by_the_party()
    {
        ScriptedSpatialService spatial = new();
        (ProductCreateContext context, RecordingUiService ui) =
            RulesetTestContext.Create(persistence: null, spatial, new ScriptedContentService(), Readings());
        // A session that walks: the party's mover steps every admitted update, which is what takes a leap.
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            RulesetTestContext.RulesetContext(context, ui) with
            {
                Cast = new CastIntentNames(Declared.UiActionContract),
                Movement = new MovementIntentNames("forward", "back", "strafe-left", "strafe-right", "turn-left", "turn-right", "jump"),
            });
        session.Start();
        session.Update(RulesetTestContext.Update(1, 1));
        float jump = spatial.Steps[^1].Config.Vertical.JumpSpeed;

        // The donor's jump spell throws the party up at a thousand where its own jump is five times ninety-six
        // (OpenEnroth src/Engine/Spells/CastSpellInfo.cpp:1111-1121, src/Engine/Graphics/Outdoor.cpp:1193-1197): the
        // next step the mover takes is a jump at that multiple of the party's own.
        Cast(session, ui, 2, "16", string.Empty);
        Assert.Equal("cast", Magic(ui).Field("outcome").AsString());
        session.Update(RulesetTestContext.Update(3, 1));
        CharacterStepRequest leapt = spatial.Steps[^1];
        Assert.True(leapt.Command.JumpPressed);
        Assert.Equal(jump * 1000f / 480f, leapt.Config.Vertical.JumpSpeed, 3);

        // The step after is the party's own again.
        session.Update(RulesetTestContext.Update(4, 1));
        Assert.Equal(jump, spatial.Steps[^1].Config.Vertical.JumpSpeed);

        // A feather fall is carried by the party, which this game's fall rule reads (Outdoor.cpp:1426).
        Cast(session, ui, 5, "13", string.Empty);
        Assert.Equal("cast", Magic(ui).Field("outcome").AsString());
        Assert.Equal(1d, Running(Magic(ui), "spell.feather-fall").Field("magnitude").AsNumber());
    }

    [Fact]
    public void A_flight_lets_the_party_rise_hover_and_sink_while_its_caster_pays_for_the_time_in_the_air()
    {
        ScriptedSpatialService spatial = new()
        {
            // The double flies a flying party by a hundred units a step in the direction it asks, and a sink meets the
            // ground at once, which is where the engine would report a contact underneath it.
            StepEnds = request => request.Command.Movement.Mode == CharacterMovementMode.Flying
                ? request.Position + new Vector3(0, 100 * request.Command.Movement.VerticalIntent, 0)
                : request.Position,
            Answer = (request, receipt) => request.Command.Movement is { Mode: CharacterMovementMode.Flying, VerticalIntent: < 0 }
                ? receipt with { Contact = default(CharacterContact) with { Present = true, Kind = CharacterContactKind.Ground } }
                : receipt,
        };
        (ProductCreateContext context, RecordingUiService ui) =
            RulesetTestContext.Create(persistence: null, spatial, new ScriptedContentService(), Flyers());
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            RulesetTestContext.RulesetContext(context, ui) with
            {
                Cast = new CastIntentNames(Declared.UiActionContract),
                Movement = new MovementIntentNames("forward", "back", "strafe-left", "strafe-right", "turn-left", "turn-right", "jump", "ascend", "descend"),
            });
        session.Start();
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        PartyMember caster = live.Party!.Members[0];
        session.Update(RulesetTestContext.Update(1, 1, RulesetTestContext.Digital("ascend", InputEdge.Held)));

        // Without a flight a rise asks for nothing: the party walks.
        Assert.Equal(CharacterMovementMode.Walking, spatial.Steps[^1].Command.Movement.Mode);

        // The flight is carried by its caster at what each five minutes in the air costs them: one point at master
        // (OpenEnroth src/Engine/Spells/CastSpellInfo.cpp:1154-1171, src/Engine/Engine.cpp:1286-1296).
        Cast(session, ui, 2, "21", string.Empty);
        Assert.Equal("cast", Magic(ui).Field("outcome").AsString());
        Assert.True(caster.Effects.Has(new EffectId("spell.fly")));
        Assert.Equal(1, caster.Effects.MagnitudeOf(new EffectId("spell.fly")));
        int points = caster.Resources.SpellPoints.Current;

        // Standing on the ground with the flight running costs nothing.
        Advance(session, 1);
        Assert.Equal(points, caster.Resources.SpellPoints.Current);

        // A held rise takes off: the mover asks the engine's flying mode at four times the walk.
        session.Update(RulesetTestContext.Update(3, 1, RulesetTestContext.Digital("ascend", InputEdge.Held)));
        CharacterStepRequest rising = spatial.Steps[^1];
        Assert.Equal(CharacterMovementMode.Flying, rising.Command.Movement.Mode);
        Assert.Equal(1f, rising.Command.Movement.VerticalIntent);
        Assert.Equal(384f * 4, rising.Command.Movement.Speed);
        Assert.Equal(100, live.World!.Party.PlacePose.Z, 3);
        Assert.Equal("flying", ProjectedNode.Of(ui.Latest().Value).Field("movement").Field("motion").AsString());

        // In the air the party stands on nothing, and the panel names the flight as what keeps everybody off the ground.
        Assert.Equal(string.Empty, Footing(ui).Field("ground").AsString());
        Assert.False(Footing(ui).Field("harmful").AsBoolean());
        Assert.Equal("Fly", Footing(ui).Field("shelters").Item(0).Field("name").AsString());
        Assert.True(Footing(ui).Field("shelters").Item(0).Field("everybody").AsBoolean());

        // Letting go hovers, and an hour in the air is twelve five-minute boundaries the caster pays for.
        Advance(session, 1);
        Assert.Equal(CharacterMovementMode.Flying, spatial.Steps[^1].Command.Movement.Mode);
        Assert.Equal(0f, spatial.Steps[^1].Command.Movement.VerticalIntent);
        Assert.Equal(points - 12, caster.Resources.SpellPoints.Current);

        // A held sink comes down; meeting the ground lands the party, and it walks again.
        session.Update(RulesetTestContext.Update(200, 1, RulesetTestContext.Digital("descend", InputEdge.Held)));
        Assert.Equal(-1f, spatial.Steps[^1].Command.Movement.VerticalIntent);
        Assert.False(live.World.Mover!.Flying);
        session.Update(RulesetTestContext.Update(201, 1));
        Assert.Equal(CharacterMovementMode.Walking, spatial.Steps[^1].Command.Movement.Mode);
        Assert.Equal("grounded", ProjectedNode.Of(ui.Latest().Value).Field("movement").Field("motion").AsString());
        Assert.Equal("ordinary", Footing(ui).Field("ground").AsString());
        Assert.Equal(0, Footing(ui).Field("shelters").Count());

        // A caster drained dry holds nobody up: the flight is still carried, and a rise asks for nothing.
        caster.Resources.TrySpendSpellPoints(caster.Resources.SpellPoints.Current);
        session.Update(RulesetTestContext.Update(202, 1, RulesetTestContext.Digital("ascend", InputEdge.Held)));
        Assert.Equal(CharacterMovementMode.Walking, spatial.Steps[^1].Command.Movement.Mode);
    }

    [Fact]
    public void A_flight_is_refused_under_a_roof_before_it_is_paid_for()
    {
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(Content(
            spells: Flyers().Single(document => document.Path.EndsWith("spells.json", StringComparison.Ordinal)),
            party: Flyers().Single(document => document.Path.EndsWith("party.json", StringComparison.Ordinal)),
            places: ($"{RulesetTestContext.ContentDirectory}/content-packs/world/places.json",
                """
                {
                  "documentId": "places",
                  "definitionKind": "place",
                  "entries": [
                    { "id": "1", "kind": "interior", "name": "Cave", "respawnDays": 1,
                      "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ] },
                    { "id": "2", "kind": "region", "name": "Shore", "respawnDays": 1,
                      "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ] }
                  ]
                }
                """)));
        using IGameSession session = Casting(context, ui);
        PartyMember caster = ((MightAndMagic7Session)session).Party!.Members[0];
        int points = caster.Resources.SpellPoints.Current;

        // The donor refuses flight indoors (OpenEnroth src/Engine/Spells/CastSpellInfo.cpp:1156-1160); here nothing is paid.
        Cast(session, ui, 1, "21", string.Empty);
        Assert.Equal("refused", Magic(ui).Field("outcome").AsString());
        Assert.Equal(MightAndMagic7Codes.SpellIndoors, Magic(ui).Field("code").AsString());
        Assert.Equal(points, caster.Resources.SpellPoints.Current);
        Assert.False(caster.Effects.Has(new EffectId("spell.fly")));
    }

    [Fact]
    public void Water_ground_drowns_the_party_standing_in_it_and_water_breathing_or_a_water_walk_spares_it()
    {
        // The double reports the ground under the party at its feet, on the staged water square, whose corners the place's
        // geometry names as water exactly as the importer writes it.
        ScriptedSpatialService spatial = new()
        {
            Answer = (request, receipt) => receipt with
            {
                Ground = default(CharacterGround) with { Present = true, Point = new Vector3(receipt.Transform.Translation.X, 0, receipt.Transform.Translation.Z) },
            },
        };
        (ProductCreateContext context, RecordingUiService ui) =
            RulesetTestContext.Create(persistence: null, spatial, new ScriptedContentService(), Waters());
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            RulesetTestContext.RulesetContext(context, ui) with
            {
                Cast = new CastIntentNames(Declared.UiActionContract),
                Movement = new MovementIntentNames("forward", "back", "strafe-left", "strafe-right", "turn-left", "turn-right", "jump"),
                Rest = new RestIntentNames(
                    Declared.RestIntent,
                    Declared.CampIntent,
                    Declared.WaitUntilDawnIntent,
                    Declared.WaitAnHourIntent,
                    Declared.WaitFiveMinutesIntent,
                    Declared.UiActionContract),
            });
        session.Start();
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        PartyMember aelina = live.Party!.Members[0];
        PartyMember borin = live.Party.Members[1];
        session.Update(RulesetTestContext.Update(1, 1));
        Assert.Equal("water", live.World!.Mover!.Footing?.Id);

        // The panel reads the same footing, that it harms the party every thirty seconds, the boundary the next harm lands
        // at, and that nothing spares anybody yet.
        ProjectedNode footing = Footing(ui);
        Assert.Equal("water", footing.Field("ground").AsString());
        Assert.True(footing.Field("harmful").AsBoolean());
        Assert.Equal(30d, footing.Field("every").AsNumber());
        Assert.InRange(footing.Field("nextHarmIn").AsNumber(), double.Epsilon, 30d);
        Assert.Equal(0, footing.Field("shelters").Count());

        // Two real seconds standing in water are sixty game seconds: every thirty of them each character loses a tenth
        // of what they can take (OpenEnroth src/Engine/Engine.cpp:1083-1099).
        (int Aelina, int Borin) before = (aelina.Resources.HitPoints.Current, borin.Resources.HitPoints.Current);
        session.Update(RulesetTestContext.Update(2, 120));
        int times = (before.Borin - borin.Resources.HitPoints.Current) / (borin.Resources.HitPoints.Maximum / 10);
        Assert.InRange(times, 1, 3);
        Assert.Equal(before.Borin - (times * (borin.Resources.HitPoints.Maximum / 10)), borin.Resources.HitPoints.Current);
        Assert.Equal(before.Aelina - (times * (aelina.Resources.HitPoints.Maximum / 10)), aelina.Resources.HitPoints.Current);

        // Nobody stops in water: a wait is refused by name.
        session.Update(RulesetTestContext.Update(3, 1, RulesetTestContext.Digital(Declared.WaitAnHourIntent)));
        Assert.Equal("refused", ProjectedNode.Of(ui.Latest().Value).Field("rest").Field("outcome").AsString());
        Assert.Equal(MightAndMagic7Codes.RestInWater, ProjectedNode.Of(ui.Latest().Value).Field("rest").Field("code").AsString());

        // Borin drinks water breathing: the water no longer harms him, and still harms Aelina.
        ItemInstance bottle = live.Party.Items.First(item => item.Definition.Value == "235");
        session.Update(RulesetTestContext.Update(
            4,
            1,
            RulesetTestContext.Payload($$"""{"action":"party.cast","member":1,"spell":"{{MightAndMagic7Potions.EffectId(235).Value}}","target":"","item":{{bottle.Id}}}""")));
        Assert.True(borin.Effects.Has(new EffectId("spell.water-breathing")));
        aelina.Resources.RestoreHitPoints(aelina.Resources.HitPoints.Maximum);
        before = (aelina.Resources.HitPoints.Current, borin.Resources.HitPoints.Current);
        session.Update(RulesetTestContext.Update(5, 120));
        Assert.Equal(before.Borin, borin.Resources.HitPoints.Current);
        Assert.True(aelina.Resources.HitPoints.Current < before.Aelina);
        footing = Footing(ui);
        Assert.True(footing.Field("harmful").AsBoolean());
        Assert.Equal(1, footing.Field("shelters").Count());
        Assert.Equal("Water Breathing", footing.Field("shelters").Item(0).Field("name").AsString());
        Assert.Equal("Borin", footing.Field("shelters").Item(0).Field("memberName").AsString());
        Assert.False(footing.Field("shelters").Item(0).Field("everybody").AsBoolean());

        // Aelina, a master, walks the party over the water for an hour a level: nobody drowns, and an hour standing on it
        // costs her a spell point every
        // twenty minutes (src/Engine/Engine.cpp:1297-1309, src/Application/GameConfig.h:248-250).
        aelina.Resources.RestoreHitPoints(aelina.Resources.HitPoints.Maximum);
        Cast(session, ui, 6, "27", string.Empty);
        Assert.Equal("cast", Magic(ui).Field("outcome").AsString());
        Assert.Equal(1, aelina.Effects.MagnitudeOf(new EffectId("spell.water-walk")));
        before = (aelina.Resources.HitPoints.Current, borin.Resources.HitPoints.Current);
        int points = aelina.Resources.SpellPoints.Current;
        Advance(session, 1);
        Assert.Equal(before.Aelina, aelina.Resources.HitPoints.Current);
        Assert.Equal(before.Borin, borin.Resources.HitPoints.Current);
        Assert.Equal(points - 3, aelina.Resources.SpellPoints.Current);

        // The panel says the water harms nobody now, and that Aelina's walk spares everybody beside Borin's breathing.
        footing = Footing(ui);
        Assert.Equal("water", footing.Field("ground").AsString());
        Assert.False(footing.Field("harmful").AsBoolean());
        Assert.Equal(0d, footing.Field("nextHarmIn").AsNumber());
        ProjectedNode walk = Enumerable.Range(0, footing.Field("shelters").Count())
            .Select(index => footing.Field("shelters").Item(index))
            .Single(shelter => shelter.Field("effect").AsString() == "spell.water-walk");
        Assert.Equal("Water Walk", walk.Field("name").AsString());
        Assert.Equal("Aelina", walk.Field("memberName").AsString());
        Assert.True(walk.Field("everybody").AsBoolean());
    }

    /// <summary>The footing the movement block publishes, from the newest projection.</summary>
    private static ProjectedNode Footing(RecordingUiService ui) =>
        ProjectedNode.Of(ui.Latest().Value).Field("movement").Field("footing");

    /// <summary>
    /// A region whose whole floor is water, a party of a water-walking sorcerer and a knight with a bottle of water
    /// breathing, and the rows those name.
    /// </summary>
    private static (string Path, string Text)[] Waters() => Content(
        spells: ($"{RulesetTestContext.ContentDirectory}/content-packs/world/spells.json",
            """
            {
              "documentId": "spells",
              "definitionKind": "spell",
              "entries": [ { "id": "27", "school": "Water", "level": 5, "name": "Water Walk", "resist": "0" } ]
            }
            """),
        party: ($"{RulesetTestContext.ContentDirectory}/content-packs/world/party.json",
            """
            {
              "documentId": "party",
              "definitionKind": "scenario-party",
              "entries": [
                {
                  "id": "party", "coins": 0, "food": 30, "reputation": 0, "fame": 0,
                  "pack": [ { "item": "235", "count": 1 } ],
                  "members": [
                    { "name": "Aelina", "race": "Elf", "class": "Sorcerer", "level": 30, "hitPoints": 40, "spellPoints": 0,
                      "attributes": [ { "id": "Might", "value": 9 }, { "id": "Intellect", "value": 50 },
                                      { "id": "Personality", "value": 15 }, { "id": "Endurance", "value": 30 },
                                      { "id": "Accuracy", "value": 30 }, { "id": "Speed", "value": 25 },
                                      { "id": "Luck", "value": 13 } ],
                      "skills": [ { "id": "Water", "level": 3, "tier": 3, "pointsSpent": 1 } ],
                      "spells": [ "27" ], "conditions": [] },
                    { "name": "Borin", "race": "Human", "class": "Knight", "level": 1, "hitPoints": 400, "spellPoints": 0,
                      "attributes": [ { "id": "Might", "value": 13 }, { "id": "Intellect", "value": 9 },
                                      { "id": "Personality", "value": 9 }, { "id": "Endurance", "value": 13 },
                                      { "id": "Accuracy", "value": 13 }, { "id": "Speed", "value": 9 },
                                      { "id": "Luck", "value": 9 } ],
                      "skills": [], "spells": [], "conditions": [] }
                  ]
                }
              ]
            }
            """),
        extra:
        [
            ($"{RulesetTestContext.ContentDirectory}/content-packs/world/place-geometry.json",
                """
                {
                  "documentId": "place-geometry",
                  "definitionKind": "place-geometry",
                  "entries": [ { "id": "1", "artifact": { "stated": "by the scripted engine, which reads nothing" }, "navigationRegion": { "minimum": [-4096,0,-4096], "maximum": [4096,0,4096], "cellSize": 128 },
                                 "surfaces": [ { "surface": "water",
                                                 "positions": [ [-4096, 0, -4096], [4096, 0, -4096], [4096, 0, 4096], [-4096, 0, 4096] ],
                                                 "triangles": [ [0, 1, 2], [0, 2, 3] ] } ] } ]
                }
                """,
                """{ "path": "place-geometry.json", "documentId": "place-geometry", "definitionKind": "place-geometry" }"""),
            ($"{RulesetTestContext.ContentDirectory}/content-packs/world/items.json",
                """
                {
                  "documentId": "items",
                  "definitionKind": "item",
                  "entries": [
                    { "id": "235", "name": "Water Breathing", "value": 300, "equipStat": "Bottle", "type": "potion", "skillGroup": "Misc", "skill": "misc", "damageDice": "0", "damageModifier": "1" }
                  ]
                }
                """,
                """{ "path": "items.json", "documentId": "items", "definitionKind": "item" }"""),
            ($"{RulesetTestContext.ContentDirectory}/content-packs/world/potions.json",
                """
                {
                  "documentId": "potions",
                  "definitionKind": "potion",
                  "entries": [
                    { "id": "235", "name": "Water Breathing", "description": "Green Potion", "effect": "Water Breathing", "kind": "potion", "units": [1, 1, 0], "tier": 2, "mixtures": { "235": "none" } }
                  ]
                }
                """,
                """{ "path": "potions.json", "documentId": "potions", "definitionKind": "potion" }"""),
        ]);

    /// <summary>Drinks the first bottle of one potion the party carries, as the panel's own control does.</summary>
    private static void Drink(IGameSession session, ulong step, int potion)
    {
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        string definition = potion.ToString(System.Globalization.CultureInfo.InvariantCulture);
        ItemInstance bottle = live.Party!.Items.First(item => item.Definition.Value == definition);
        string spell = MightAndMagic7Potions.EffectId(potion).Value;
        session.Update(RulesetTestContext.Update(
            step,
            1,
            RulesetTestContext.Payload(
                $$"""{"action":"party.cast","member":0,"spell":"{{spell}}","target":"","item":{{bottle.Id}}}""")));
    }

    /// <summary>This suite's own documents with its caster a master of air, which is what a flight asks of its caster.</summary>
    private static (string Path, string Text)[] Flyers() =>
    [
        .. Readings().Select(document => document.Path.EndsWith("party.json", StringComparison.Ordinal)
            ? (document.Path, document.Text.Replace("""{ "id": "Air", "level": 3, "tier": 2""", """{ "id": "Air", "level": 3, "tier": 3""", StringComparison.Ordinal))
            : document),
    ];

    /// <summary>The spells this suite casts, by their shipped ids.</summary>
    private static (string Path, string Text)[] Readings() => Content(
        spells: ($"{RulesetTestContext.ContentDirectory}/content-packs/world/spells.json",
            """
            {
              "documentId": "spells",
              "definitionKind": "spell",
              "entries": [
                { "id": "13", "school": "Air", "level": 2, "name": "Feather Fall", "resist": "0" },
                { "id": "16", "school": "Air", "level": 5, "name": "Jump", "resist": "0" },
                { "id": "17", "school": "Air", "level": 6, "name": "Shield", "resist": "0" },
                { "id": "21", "school": "Air", "level": 7, "name": "Fly", "resist": "0" },
                { "id": "71", "school": "Body", "level": 5, "name": "Regeneration", "resist": "0" },
                { "id": "83", "school": "Light", "level": 6, "name": "Day of the Gods", "resist": "0" },
                { "id": "86", "school": "Light", "level": 9, "name": "Hour of Power", "resist": "0" },
                { "id": "88", "school": "Light", "level": 11, "name": "Divine Intervention", "resist": "0" },
                { "id": "95", "school": "Dark", "level": 7, "name": "Pain Reflection", "resist": "0" }
              ]
            }
            """),
        party: ($"{RulesetTestContext.ContentDirectory}/content-packs/world/party.json",
            """
            {
              "documentId": "party",
              "definitionKind": "scenario-party",
              "entries": [
                {
                  "id": "party", "coins": 5000, "food": 30, "reputation": 0, "fame": 0,
                  "pack": [ { "item": "232", "count": 1 }, { "item": "240", "count": 1 }, { "item": "264", "count": 2 }, { "item": "271", "count": 1 } ],
                  "members": [
                    { "name": "Aelina", "race": "Elf", "class": "Sorcerer", "level": 30, "hitPoints": 40, "spellPoints": 0,
                      "attributes": [ { "id": "Might", "value": 9 }, { "id": "Intellect", "value": 50 },
                                      { "id": "Personality", "value": 15 }, { "id": "Endurance", "value": 30 },
                                      { "id": "Accuracy", "value": 30 }, { "id": "Speed", "value": 25 },
                                      { "id": "Luck", "value": 13 } ],
                      "skills": [ { "id": "Air", "level": 3, "tier": 2, "pointsSpent": 1 },
                                  { "id": "Body", "level": 3, "tier": 2, "pointsSpent": 1 },
                                  { "id": "Light", "level": 2, "tier": 4, "pointsSpent": 1 },
                                  { "id": "Dark", "level": 4, "tier": 2, "pointsSpent": 1 } ],
                      "spells": [ "13", "16", "17", "21", "71", "83", "86", "88", "95" ], "conditions": [] },
                    { "name": "Borin", "race": "Human", "class": "Knight", "level": 1, "hitPoints": 400, "spellPoints": 0,
                      "attributes": [ { "id": "Might", "value": 13 }, { "id": "Intellect", "value": 9 },
                                      { "id": "Personality", "value": 9 }, { "id": "Endurance", "value": 13 },
                                      { "id": "Accuracy", "value": 13 }, { "id": "Speed", "value": 9 },
                                      { "id": "Luck", "value": 9 } ],
                      "skills": [], "spells": [], "conditions": [] }
                  ]
                }
              ]
            }
            """),
        monster: ($"{RulesetTestContext.ContentDirectory}/content-packs/world/monsters.json",
            $$"""
            {
              "documentId": "monsters",
              "definitionKind": "monster",
              "entries": [
                { "id": "7", "name": "A beast", "hostility": 1, "recovery": 100, "level": 4,
                  "hitPoints": 200, "armorClass": 0, {{MonsterRows.Combat(7, "Fire", "2d6+0")}} }
              ]
            }
            """),
        extra:
        [
            ($"{RulesetTestContext.ContentDirectory}/content-packs/world/items.json",
                """
                {
                  "documentId": "items",
                  "definitionKind": "item",
                  "entries": [
                    { "id": "232", "name": "Shield", "value": 300, "equipStat": "Bottle", "type": "potion", "skillGroup": "Misc", "skill": "misc", "damageDice": "0", "damageModifier": "1" },
                    { "id": "240", "name": "Might Boost", "value": 300, "equipStat": "Bottle", "type": "potion", "skillGroup": "Misc", "skill": "misc", "damageDice": "0", "damageModifier": "1" },
                    { "id": "264", "name": "Pure Luck", "value": 5000, "equipStat": "Bottle", "type": "potion", "skillGroup": "Misc", "skill": "misc", "damageDice": "0", "damageModifier": "1" },
                    { "id": "271", "name": "Rejuvenation", "value": 5000, "equipStat": "Bottle", "type": "potion", "skillGroup": "Misc", "skill": "misc", "damageDice": "0", "damageModifier": "1" }
                  ]
                }
                """,
                """{ "path": "items.json", "documentId": "items", "definitionKind": "item" }"""),
            ($"{RulesetTestContext.ContentDirectory}/content-packs/world/potions.json",
                """
                {
                  "documentId": "potions",
                  "definitionKind": "potion",
                  "entries": [
                    { "id": "232", "name": "Shield", "description": "Purple Potion", "effect": "Cast Shield", "kind": "potion", "units": [1, 0, 1], "tier": 2, "mixtures": { "232": "none" } },
                    { "id": "240", "name": "Might Boost", "description": "Yellow Potion", "effect": "+3 Might", "kind": "potion", "units": [0, 0, 1], "tier": 2, "mixtures": { "240": "none" } },
                    { "id": "264", "name": "Pure Luck", "description": "White Potion", "effect": "+50 Luck", "kind": "potion", "units": [2, 2, 2], "tier": 4, "mixtures": { "264": "none" } },
                    { "id": "271", "name": "Rejuvenation", "description": "White Potion", "effect": "Removes Unnatural Aging", "kind": "potion", "units": [3, 3, 3], "tier": 4, "mixtures": { "271": "none" } }
                  ]
                }
                """,
                """{ "path": "potions.json", "documentId": "potions", "definitionKind": "potion" }"""),
        ]);

    /// <summary>The same reading fixture with the reflection test's creature close enough for an actual melee blow.</summary>
    private static (string Path, string Text)[] MeleeReachReadings() =>
    [
        .. Readings().Select(document => document.Path.EndsWith("places.json", StringComparison.Ordinal)
            ? (document.Path, document.Text.Replace("\"x\": 5000", "\"x\": 200", StringComparison.Ordinal))
            : document),
    ];
}
