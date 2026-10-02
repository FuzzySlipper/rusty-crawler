using System.Text.Json;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.World;
using PartyRpg.Testing;
using Rusty.Engine;
using Xunit;
using Xunit.Abstractions;
using static PartyRpg.Rulesets.MightAndMagic7.Tests.SpellEffectPolicyTests;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

/// <summary>
/// What a creature an encounter or a map event's summoning stands thinks of the party: what its kind thinks of the
/// party in the shipped matrix, at that band's distance — not its row's own band, which the donor overwrites with
/// friendly when it stands the creature.
/// </summary>
/// <remarks>
/// The rule is the donor's (OpenEnroth <c>src/Engine/Objects/Actor.cpp:4331-4334</c> for the spawn,
/// <c>:2097-2116</c> and <c>:2122-2166</c> for the target choice, <c>src/Engine/Evt/EvtInterpreter.cpp:77-99</c> for
/// the summoning), cited where the ruleset states it. Every row here states a non-zero band, as every one of the
/// operator's monster rows does, so a creature that stays out of the fight is one the matrix keeps out of it.
/// </remarks>
public sealed class SpawnedHostilityPolicyTests(ITestOutputHelper output)
{
    [Fact]
    public void A_spawned_kind_the_matrix_keeps_friendly_stays_peaceful_until_attacked_and_a_hostile_kind_attacks_at_its_band()
    {
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(Field());
        using IGameSession session = Casting(context, ui);
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        session.Update(RulesetTestContext.Update(1, 1));
        MightAndMagic7Combat policy = Fight(context, live.Party!);

        // The guard an encounter stands is of a kind the party's row of the matrix leaves friendly, so it starts no
        // fight three hundred units from the party, though its own row states the longest band.
        Combatant guard = Creature(live, "monster-1-0");
        Assert.False(policy.NatureOf(guard.Subject).AttacksOnSight);
        Assert.Equal(CombatSide.Neutral, guard.Side);

        // The goblin an encounter stands is of a kind the party's row names at band two, so it notices the party at
        // that band's distance (2560) rather than its row's (10240), and is in the fight from four hundred units.
        Combatant goblin = Creature(live, "monster-2-0");
        Hostility goblinNature = policy.NatureOf(goblin.Subject);
        Assert.True(goblinNature.AttacksOnSight);
        Assert.Equal(2560, goblinNature.NoticeRange);
        Assert.Equal(CombatSide.Opposition, goblin.Side);

        // A goblin four thousand units off is the party's enemy by nature and not yet in the fight, which its row's
        // own band would have put it in.
        Combatant distant = Creature(live, "monster-3-0");
        Assert.True(policy.NatureOf(distant.Subject).AttacksOnSight);
        Assert.Equal(CombatSide.Neutral, distant.Side);

        // A map event's summoning stands its creatures through the same reading of the slot, and the fight reads them
        // the same way: the summoned guard starts nothing, the summoned goblin is in the fight.
        foreach ((string id, string kind, int monster, double x) in new[] { ("event-guard", "Guard", 13, 500d), ("event-goblin", "Goblin", 7, 600d) })
        {
            using JsonDocument slot = JsonDocument.Parse(Slot(kind, monster));
            IReadOnlyList<PlacementDefinition> summoned = MightAndMagic7Spawns.Summoned(id, slot.RootElement, 1, ((int)x, 0, 0), group: 0, rolls: null, out string? unresolved);
            Assert.Null(unresolved);
            live.World!.Population.Summon(Assert.Single(summoned));
        }

        session.Update(RulesetTestContext.Update(2, 1));
        Assert.Equal(CombatSide.Neutral, Creature(live, "event-guard-0").Side);
        Assert.False(policy.NatureOf(Creature(live, "event-guard-0").Subject).AttacksOnSight);
        Assert.Equal(CombatSide.Opposition, Creature(live, "event-goblin-0").Side);

        // Attacking the peaceful guard is what makes it an enemy, and it stays one.
        Assert.True(live.Combat!.Provoke(guard.Id));
        session.Update(RulesetTestContext.Update(3, 1));
        Assert.Equal(CombatSide.Opposition, Creature(live, "monster-1-0").Side);
    }

    [ImportedFact("hostility.json")]
    public void Over_the_operators_install_the_matrix_decides_which_spawned_kinds_start_fights()
    {
        ContentCatalog catalog = ImportedContent.Load();
        MightAndMagic7Hostility matrix = MightAndMagic7Hostility.Read(catalog);
        Dictionary<string, int> kindOf = [];
        foreach ((_, _, ContentEntry row) in catalog.Entries(MightAndMagic7Combat.MonsterDefinitionKind))
        {
            kindOf[row.Id] = row.GetInt32(MightAndMagic7Combat.HostilityKindField) ?? MightAndMagic7Combat.NoHostilityKind;
        }

        // Every kind an encounter placement or a map event's summoning names, with how many of each name it.
        SortedDictionary<string, (int Kind, int Encounters, int Summonings)> kinds = new(StringComparer.Ordinal);
        void Count(JsonElement slot, bool summoning)
        {
            string name = ContentEntry.ReadString(slot, "monsterKind");
            int kind = slot.TryGetProperty("variants", out JsonElement variants) && variants.GetArrayLength() > 0
                ? kindOf.GetValueOrDefault(ContentEntry.ReadId(variants[0], MightAndMagic7Combat.MonsterField), MightAndMagic7Combat.NoHostilityKind)
                : MightAndMagic7Combat.NoHostilityKind;
            (int _, int encounters, int summonings) = kinds.GetValueOrDefault(name);
            kinds[name] = (kind, encounters + (summoning ? 0 : 1), summonings + (summoning ? 1 : 0));
        }

        foreach ((_, _, ContentEntry place) in catalog.Entries(PlaceGraphLoader.PlaceDefinitionKind))
        {
            foreach (JsonElement placement in place.GetArray(PlacePopulationContent.PlacementsField))
            {
                if (ContentEntry.ReadString(placement, PlacePopulationContent.KindField) == MightAndMagic7Spawns.EncounterPlacementKind) Count(placement, summoning: false);
            }
        }

        foreach ((_, _, ContentEntry mapEvent) in catalog.Entries("place-event"))
        {
            foreach (JsonElement step in mapEvent.GetArray("steps"))
            {
                if (step.TryGetProperty("summons", out JsonElement summons) && summons.ValueKind == JsonValueKind.Object) Count(summons, summoning: true);
            }
        }

        int peaceful = 0;
        int hostile = 0;
        foreach ((string name, (int kind, int encounters, int summonings)) in kinds)
        {
            int band = matrix.TowardParty(kind);
            if (band == 0) peaceful++;
            else hostile++;
            output.WriteLine($"{name}\tkind {kind}\tband {band}\tencounters {encounters}\tsummonings {summonings}");
        }

        output.WriteLine($"peaceful kinds {peaceful} ({kinds.Values.Where(value => matrix.TowardParty(value.Kind) == 0).Sum(value => value.Encounters)} encounters, {kinds.Values.Where(value => matrix.TowardParty(value.Kind) == 0).Sum(value => value.Summonings)} summonings); " +
            $"hostile kinds {hostile} ({kinds.Values.Where(value => matrix.TowardParty(value.Kind) != 0).Sum(value => value.Encounters)} encounters, {kinds.Values.Where(value => matrix.TowardParty(value.Kind) != 0).Sum(value => value.Summonings)} summonings)");

        // The shipped matrix keeps some spawned kinds friendly to the party and names others, so both readings are
        // exercised by the operator's own encounters rather than by these cases alone.
        Assert.True(peaceful > 0, "some spawned kind is friendly to the party in the shipped matrix");
        Assert.True(hostile > 0, "some spawned kind is the party's enemy in the shipped matrix");
    }

    /// <summary>The creature the session's own fight holds under its placement's own identity.</summary>
    private static Combatant Creature(MightAndMagic7Session live, string id) =>
        live.Combat!.Combatants.FirstOrDefault(combatant => !combatant.Subject.IsMember && combatant.Subject.Placement?.Content.Id == id)
        ?? throw new InvalidOperationException(
            $"No creature '{id}' stands in the fight, which holds: {string.Join(", ", live.Combat.Combatants.Select(combatant => $"{combatant.Name} ({combatant.Subject.Placement?.Content})"))}.");

    /// <summary>One encounter slot graded A, naming one variant row, as the importer writes a spawn record's or a summoning's.</summary>
    private static string Slot(string kind, int monster) =>
        FormattableString.Invariant(
            $$"""
            { "encounter": 4, "slot": 1, "grade": "A", "monsterKind": "{{kind}}", "difficulty": 3, "appearMin": 1, "appearMax": 1,
              "variants": [ { "grade": "A", "monster": {{monster}}, "monsterName": "{{kind}}" } ] }
            """);

    /// <summary>One encounter placement, graded so that it stands exactly one creature and draws nothing.</summary>
    private static string Encounter(int spawn, string kind, int monster, double x) =>
        FormattableString.Invariant(
            $$"""
            { "id": "encounter-{{spawn}}", "kind": "encounter", "sourceField": "spawnPoints", "sourceIndex": {{spawn}}, "x": {{x}}, "y": 0, "z": 0,
              "spawn": {{spawn}}, "encounter": 4, "slot": 1, "grade": "A", "monsterKind": "{{kind}}", "difficulty": 3, "appearMin": 1, "appearMax": 1,
              "group": 0, "attributes": 0, "radius": 64,
              "variants": [ { "grade": "A", "monster": {{monster}}, "monsterName": "{{kind}}" } ] }
            """);

    /// <summary>
    /// A field whose spawn records stand a guard (row 13, kind five) and two goblins (row 7, kind three), and a matrix
    /// whose party row names the goblins at band two and says nothing of the guards; both rows state the longest band.
    /// </summary>
    private static (string Path, string Text)[] Field() => Content(
        monster: ($"{RulesetTestContext.ContentDirectory}/content-packs/world/monsters.json",
            $$"""
            {
              "documentId": "monsters",
              "definitionKind": "monster",
              "entries": [
                { "id": "7", "name": "A goblin", "hostility": 4, "recovery": 100, "level": 4, "speed": 0,
                  "hitPoints": 30, "armorClass": 0, {{MonsterRows.Combat(7, "Phys", "1d2+0")}} },
                { "id": "13", "name": "A guard", "hostility": 4, "recovery": 100, "level": 4, "speed": 0,
                  "hitPoints": 30, "armorClass": 0, {{MonsterRows.Combat(13, "Phys", "1d2+0")}} }
              ]
            }
            """),
        places: ($"{RulesetTestContext.ContentDirectory}/content-packs/world/places.json",
            $$"""
            {
              "documentId": "places",
              "definitionKind": "place",
              "entries": [
                { "id": "1", "kind": "region", "name": "The Field", "respawnDays": 1,
                  "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
                  "placements": [
                    {{Encounter(1, "Guard", 13, 300)}},
                    {{Encounter(2, "Goblin", 7, 400)}},
                    {{Encounter(3, "Goblin", 7, 4000)}}
                  ] },
                { "id": "2", "kind": "interior", "name": "Cave", "respawnDays": 1,
                  "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ] }
              ]
            }
            """),
        extra:
        [
            (MonsterRows.Matrix("world", (MonsterRows.KindOf(7), 2)).Path,
                MonsterRows.Matrix("world", (MonsterRows.KindOf(7), 2)).Text,
                MonsterRows.MatrixDocument),
        ]);
}
