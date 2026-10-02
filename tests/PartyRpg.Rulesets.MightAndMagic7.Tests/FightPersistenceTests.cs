using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using Rusty.Engine;
using Xunit;
using static PartyRpg.Rulesets.MightAndMagic7.Tests.ItemMagicPolicyTests;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

public sealed class FightPersistenceTests
{
    [Fact]
    public void An_omitted_resident_is_named_before_load_can_remove_it()
    {
        var (context, ui) = RulesetTestContext.Create(new InMemoryPersistenceService(), Content());
        using IGameSession session = Casting(context, ui, combat: true);
        SessionSave saved = MightAndMagic7Ruleset.Instance.Save(session);
        CreatureCombatSave resident = Assert.Single(saved.Combat.Creatures);
        SessionSave bad = Replace(saved, saved.Combat with { Creatures = [] });
        ContentCatalog content = ContentCatalogLoader.Load(RulesetTestContext.Content(context), ContentLayout.Under(RulesetTestContext.ContentDirectory)).RequireValid();
        SessionSaveException error = Assert.Throws<SessionSaveException>(() => MightAndMagic7Persistence.RequireLoadable(bad, content));
        Assert.Contains(error.Problems, p => p.Code == SaveCodes.SaveCreatureMissing && p.Subject.Contains(resident.Placement.Id, StringComparison.Ordinal));
        Assert.Single(((MightAndMagic7Session)session).World!.Population.Entities);
    }

    [Fact]
    public void An_explicitly_absent_resident_keeps_the_saved_visit_empty()
    {
        InMemoryPersistenceService persistence = new();
        var (context, ui) = RulesetTestContext.Create(persistence, Content());
        using IGameSession session = Casting(context, ui, combat: true);
        var live = (MightAndMagic7Session)session;
        PlacePopulationEntity resident = Assert.Single(live.World!.Population.Entities);
        // The same canonical removal a hidden group applies; the subsequent visit would populate it again.
        live.World.Population.Dismiss(resident);
        SessionSave saved = MightAndMagic7Ruleset.Instance.Save(session);
        Assert.Empty(saved.Combat.Creatures);
        Assert.Equal(resident.Content, Assert.Single(saved.Combat.AbsentResidents));
        var (againContext, againUi) = RulesetTestContext.Create(persistence, Content());
        using IGameSession resumed = MightAndMagic7Ruleset.Instance.ResumeSession(RulesetTestContext.RulesetContext(againContext, againUi, combat: true));
        resumed.Start();
        Assert.Empty(((MightAndMagic7Session)resumed).World!.Population.Entities);
        Assert.Equal(saved.Combat.AbsentResidents, MightAndMagic7Ruleset.Instance.Save(resumed).Combat.AbsentResidents);
    }

    [Fact]
    public void An_events_ambush_resumes_its_drawn_creatures_and_new_uses_issue_new_identities()
    {
        var content = Content().Select(file => (file.Path, Text: file.Text
            .Replace("\"x\": 500", "\"x\": 9000", StringComparison.Ordinal)
            .Replace("\"placements\": [", "\"placements\": [ { \"id\": \"bell\", \"kind\": \"fixture\", \"eventId\": 7, \"name\": \"Bell\", \"x\": 100, \"y\": 0, \"z\": 0 },", StringComparison.Ordinal)
            .Replace("\"documents\": [", "\"documents\": [ { \"path\": \"events.json\", \"documentId\": \"events\", \"definitionKind\": \"place-event\" },", StringComparison.Ordinal)))
            .Append(($"{RulesetTestContext.ContentDirectory}/content-packs/world/events.json", """
                { "documentId": "events", "definitionKind": "place-event", "entries": [
                  { "id": "1.7", "place": "1", "event": 7, "raised": true, "stepped": false, "timed": false,
                    "steps": [
                      { "step": 0, "op": "summon-monsters", "amount": 2, "group": 3, "x": 400, "y": 0, "z": 0,
                        "summons": { "encounter": 4, "grade": "A", "monsterKind": "Beast", "difficulty": 3,
                          "variants": [ { "grade": "A", "monster": 7, "monsterName": "A beast" } ] } },
                      { "step": 1, "op": "exit" } ] } ] }
                """)).ToArray();
        InMemoryPersistenceService persistence = new();
        UseIntentNames use = new(Declared.UseIntent, Declared.UiActionContract);
        var (context, ui) = RulesetTestContext.Create(persistence, content);
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(RulesetTestContext.RulesetContext(context, ui, combat: true) with { Use = use });
        session.Start();
        session.Update(RulesetTestContext.Update(1, 1, RulesetTestContext.Digital(Declared.UseIntent)));
        var live = (MightAndMagic7Session)session;
        Assert.True(live.World!.LastInteraction!.IsApplied, live.World.LastInteraction.Refusal?.Message);
        SessionSave saved = MightAndMagic7Ruleset.Instance.Save(session);
        CreatureCombatSave[] ambush = saved.Combat.Creatures.Where(c => c.Summoned).ToArray();
        Assert.Equal(2, ambush.Length);
        Assert.All(ambush, c => Assert.Equal("7", c.Kind));
        var (againContext, againUi) = RulesetTestContext.Create(persistence, content);
        using IGameSession resumed = MightAndMagic7Ruleset.Instance.ResumeSession(RulesetTestContext.RulesetContext(againContext, againUi, combat: true) with { Use = use });
        resumed.Start();
        var again = (MightAndMagic7Session)resumed;
        CreatureCombatSave[] rebuilt = MightAndMagic7Ruleset.Instance.Save(resumed).Combat.Creatures.Where(c => c.Summoned).ToArray();
        Assert.Equal(ambush.Select(c => (c.Placement, c.Kind, c.Pose, c.Health)), rebuilt.Select(c => (c.Placement, c.Kind, c.Pose, c.Health)));
        resumed.Update(RulesetTestContext.Update(1, 1, RulesetTestContext.Digital(Declared.UseIntent)));
        Assert.True(again.World!.LastInteraction!.IsApplied, again.World.LastInteraction.Refusal?.Message);
        var created = again.World.Population.Entities.Where(e => e.IsSummoned).ToArray();
        Assert.Equal(4, created.Length);
        Assert.Equal(4, created.Select(e => e.Content).Distinct().Count());
        MightAndMagic7Ruleset.Instance.Save(resumed);
    }

    [Fact]
    public void A_resumed_body_keeps_its_rolled_yield_and_search_incarnation()
    {
        var content = Content().Select(file => (file.Path, Text: file.Text.Replace("\"treasure\": \"0%\"",
            "\"treasureRoll\": { \"chance\": 0, \"goldRolls\": 5, \"goldSides\": 1, \"level\": 2 }", StringComparison.Ordinal))).ToArray();
        InMemoryPersistenceService persistence = new();
        var (context, ui) = RulesetTestContext.Create(persistence, content);
        ((FakeEngineContext)context.Engine).RandomService.Answer = request => request.Key.EndsWith("/hit", StringComparison.Ordinal) ? 0 : null;
        UseIntentNames use = new(Declared.UseIntent, Declared.UiActionContract);
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(RulesetTestContext.RulesetContext(context, ui, combat: true) with { Use = use });
        session.Start();
        var live = (MightAndMagic7Session)session;
        session.Update(RulesetTestContext.Update(1, 1));
        Combatant beast = Assert.Single(live.Combat!.Combatants, a => a.Subject.Entity is not null);
        beast.Subject.Entity!.MoveTo(new PlacePose(100, 0, 0, 0, 0));
        CreatureHealth health = CreatureHealth.Find(beast.Subject.Entity.Actor)!;
        health.Wound(health.Maximum - 1);
        Combatant fighter = live.Combat.Combatants.Single(a => a.Subject.Member?.Id == live.Party!.Members[1].Id);
        live.Combat.Order(new AttackOrder(fighter.Id, AttackKind.Melee, beast.Id));
        Assert.True(health.IsDown);
        SessionSave saved = MightAndMagic7Ruleset.Instance.Save(session);
        CorpseSave body = Assert.Single(saved.Combat.Corpses);
        Assert.Equal(5, body.Held!.Coins);
        ContentCatalog catalog = ContentCatalogLoader.Load(RulesetTestContext.Content(context), ContentLayout.Under(RulesetTestContext.ContentDirectory)).RequireValid();
        SessionSave detachedBody = Replace(saved, saved.Combat with { Corpses = [body with { Pose = body.Pose with { X = body.Pose.X + 10 } }] });
        SessionSaveException contradiction = Assert.Throws<SessionSaveException>(() => MightAndMagic7Persistence.RequireLoadable(detachedBody, catalog));
        Assert.Contains(contradiction.Problems, p => p.Code == SaveCodes.SaveCombatInvalid && p.Subject.Contains(body.Placement.Id, StringComparison.Ordinal));

        var (againContext, againUi) = RulesetTestContext.Create(persistence, content);
        using IGameSession resumed = MightAndMagic7Ruleset.Instance.ResumeSession(RulesetTestContext.RulesetContext(againContext, againUi, combat: true) with { Use = use });
        resumed.Start();
        var again = (MightAndMagic7Session)resumed;
        CorpseSave restored = Assert.Single(MightAndMagic7Ruleset.Instance.Save(resumed).Combat.Corpses);
        Assert.Equal(body.Serial, restored.Serial);
        Assert.Equal(body.Held.Coins, restored.Held!.Coins);
        int coins = again.Party!.Purse.Coins;
        resumed.Update(RulesetTestContext.Update(1, 1, RulesetTestContext.Digital(Declared.UseIntent)));
        Assert.True(again.World!.LastInteraction!.IsApplied);
        Assert.Equal(coins + 5, again.Party.Purse.Coins);
        MightAndMagic7Ruleset.Instance.Save(resumed);

        var (lastContext, lastUi) = RulesetTestContext.Create(persistence, content);
        using IGameSession searched = MightAndMagic7Ruleset.Instance.ResumeSession(RulesetTestContext.RulesetContext(lastContext, lastUi, combat: true) with { Use = use });
        searched.Start();
        searched.Update(RulesetTestContext.Update(1, 1, RulesetTestContext.Digital(Declared.UseIntent)));
        var last = (MightAndMagic7Session)searched;
        Assert.Equal("container-emptied", last.World!.LastInteraction!.Refusal!.Code);
        Assert.Equal(coins + 5, last.Party!.Purse.Coins);
        Assert.Equal(body.Serial, Assert.Single(MightAndMagic7Ruleset.Instance.Save(searched).Combat.Corpses).Serial);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_fight_restores_exact_recovery_provocation_health_effects_and_live_pose(bool turnBased)
    {
        InMemoryPersistenceService persistence = new();
        var (context, ui) = RulesetTestContext.Create(persistence, Content());
        using IGameSession session = Casting(context, ui, combat: true);
        var live = (MightAndMagic7Session)session;
        session.Update(RulesetTestContext.Update(1, 1));
        session.Update(RulesetTestContext.Update(2, 1, RulesetTestContext.Digital(Declared.AttackIntent)));
        Combatant creature = Assert.Single(live.Combat!.Combatants, a => a.Subject.Entity is not null);
        CreatureHealth.Find(creature.Subject.Entity!.Actor)!.Wound(3);
        creature.Subject.Entity.MoveTo(new PlacePose(420, 23, 2, 256, 0));
        CreatureEffects.Find(creature.Subject.Entity.Actor)!.Apply(SpellEffectIds.CreatureSlowed, 2, GameDuration.FromSeconds(40));
        live.Combat.Delay(creature.Id, MightAndMagic7Combat.Ticks(40));
        if (turnBased) { live.Combat.TogglePacing(); live.Combat.Turns.Next(); }
        SessionSave saved = MightAndMagic7Ruleset.Instance.Save(session);
        Assert.True(Assert.Single(saved.Combat.Creatures).Provoked);
        Assert.Contains(saved.Combat.Members, m => m.RecoveryMilliseconds > 0);
        var (againContext, againUi) = RulesetTestContext.Create(persistence, Content());
        using IGameSession resumed = MightAndMagic7Ruleset.Instance.ResumeSession(RulesetTestContext.RulesetContext(againContext, againUi, combat: true) with { Start = SessionStart.Resume });
        resumed.Start();
        var again = (MightAndMagic7Session)resumed;
        SessionSave restored = MightAndMagic7Ruleset.Instance.Save(resumed);
        Assert.Equal(saved.Combat.Members, restored.Combat.Members);
        CreatureCombatSave before = Assert.Single(saved.Combat.Creatures), after = Assert.Single(restored.Combat.Creatures);
        Assert.Equal(before with { Effects = after.Effects }, after);
        Assert.Equal(before.Effects, after.Effects);
        Assert.Equal(saved.Combat.AttacksResolved, restored.Combat.AttacksResolved);
        Assert.Equal(saved.Combat.Pacing, restored.Combat.Pacing);
        Assert.True(again.Combat!.IsEngaged);
        Assert.True(again.Combat.IsProvoked(Assert.Single(again.Combat.Combatants, a => a.Subject.Entity is not null).Id));
        if (turnBased)
        {
            Assert.Equal(saved.Combat.Turns!.Current, restored.Combat.Turns!.Current);
            Assert.Equal(saved.Combat.Turns.Round, restored.Combat.Turns.Round);
            Assert.Equal(saved.Combat.Turns.DueMilliseconds, restored.Combat.Turns.DueMilliseconds);
        }
        again.Owners.Clock!.Advance(GameDuration.FromMilliseconds(100));
        SessionSave advanced = MightAndMagic7Ruleset.Instance.Save(resumed);
        Assert.Equal(Math.Max(0, before.RecoveryMilliseconds - 100), Assert.Single(advanced.Combat.Creatures).RecoveryMilliseconds);
        Assert.Equal(before.Effects[0].RemainingMilliseconds - 100, Assert.Single(advanced.Combat.Creatures).Effects[0].RemainingMilliseconds);
    }

    [Fact]
    public void All_combat_contradictions_are_named_before_a_resume_changes_any_owner()
    {
        var (context, ui) = RulesetTestContext.Create(new InMemoryPersistenceService(), Content());
        using IGameSession session = Casting(context, ui, combat: true);
        var live = (MightAndMagic7Session)session;
        session.Update(RulesetTestContext.Update(1, 1));
        SessionSave saved = MightAndMagic7Ruleset.Instance.Save(session);
        CreatureCombatSave creature = Assert.Single(saved.Combat.Creatures);
        CombatSave malformed = saved.Combat with
        {
            Members = saved.Combat.Members.Select(m => m with { RecoveryMilliseconds = long.MaxValue }).ToArray(),
            Creatures = [creature with { Kind = "no-such-kind", RecoveryMilliseconds = long.MaxValue },
                creature with { Placement = new("monster", "missing-resident") }],
            Pacing = CombatPacing.TurnBased,
            Turns = new TurnSave(1, TurnPhase.Action, new CombatActorSave(Placement: new("monster", "absent-turn")),
                0, 0, long.MaxValue, 0, [], [], [], []),
        };
        SessionSave bad = Replace(saved, malformed);
        ContentCatalog content = ContentCatalogLoader.Load(RulesetTestContext.Content(context), ContentLayout.Under(RulesetTestContext.ContentDirectory)).RequireValid();
        var before = live.Owners.Clock!.Elapsed;
        SessionSaveException error = Assert.Throws<SessionSaveException>(() => MightAndMagic7Persistence.RequireLoadable(bad, content));
        Assert.Contains(error.Problems, p => p.Code == SaveCodes.SaveCreatureKindUnknown);
        Assert.Contains(error.Problems, p => p.Code == SaveCodes.SaveCombatRecoveryInvalid && p.Subject.StartsWith("member:", StringComparison.Ordinal));
        Assert.Contains(error.Problems, p => p.Code == SaveCodes.SaveCombatRecoveryInvalid && p.Subject.Contains("beast", StringComparison.Ordinal));
        Assert.Contains(error.Problems, p => p.Code == SaveCodes.SaveCreatureMissing && p.Subject.Contains("missing-resident", StringComparison.Ordinal));
        Assert.Contains(error.Problems, p => p.Code == SaveCodes.SaveCombatInvalid && p.Subject == "round");
        Assert.Equal(before, live.Owners.Clock!.Elapsed);
        Assert.Equal(creature.Pose, Assert.Single(live.World!.Population.Entities).Pose);
    }

    [Fact]
    public void An_omitted_combat_collection_is_a_named_load_problem()
    {
        var (context, ui) = RulesetTestContext.Create(new InMemoryPersistenceService(), Content());
        using IGameSession session = Casting(context, ui, combat: true);
        SessionSave saved = MightAndMagic7Ruleset.Instance.Save(session);
        SessionSave bad = Replace(saved, saved.Combat with { Creatures = null! });
        ContentCatalog content = ContentCatalogLoader.Load(RulesetTestContext.Content(context), ContentLayout.Under(RulesetTestContext.ContentDirectory)).RequireValid();
        SessionSaveException error = Assert.Throws<SessionSaveException>(() => MightAndMagic7Persistence.RequireLoadable(bad, content));
        Assert.Contains(error.Problems, p => p.Code == SaveCodes.SaveCombatInvalid && p.Subject == "combat");
    }

    private static SessionSave Replace(SessionSave saved, CombatSave fight) => new(saved.Party, saved.Clock, saved.World, saved.Quests, saved.Journal, saved.Knowledge, saved.Maps, fight);
}
