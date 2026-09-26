using System.Text.Json;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Loot;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.World;
using PartyRpg.Rulesets.MightAndMagic7;
using Xunit;

namespace PartyRpg.Host.Tests;

/// <summary>
/// This game's treasure rules, read from content by the real ruleset: what a monster row drops, what a
/// container's random reference resolves to, and what a death leaves lying where it fell.
/// </summary>
/// <remarks>
/// <para>
/// The content here is written in the shape the importer emits — an item carries the weights its row of the
/// shipped random-item table states, the tags its two vocabulary columns state, and the material its rarity
/// column spells out, and a monster row carries the numbers its treasure cell states. The rules are the
/// ruleset's own: the donor's order, the donor's fallbacks, and the donor's level table, so a case that
/// disagrees with any of them fails here rather than in play.
/// </para>
/// <para>
/// The numbers a case asserts are the shipped ones — the item table's per-level weights and the monster
/// table's own cells — because "the treasure comes from the data" is the claim being checked.
/// </para>
/// </remarks>
public sealed class LootPolicyTests
{
    /// <summary>The item ids this suite's table declares, so a case names what it expects rather than an id.</summary>
    private const string CrudeLongsword = "1";

    private const string Cloak = "2";

    private const string Gem = "3";

    private const string Sword = "7";

    private const string BeyondTheRange = "540";

    /// <summary>A cell of the shipped table: five percent, fifteen six-sided dice of coin, and a second-level sword.</summary>
    /// <remarks>
    /// The shape is the pack's, not the importer's: this suite declares content the way the writer emits it,
    /// so what it exercises is the ruleset's reading rather than the table format.
    /// </remarks>
    private readonly record struct Cell(int Chance, int Rolls, int Sides, int Level, string Kind = "", string Skill = "");

    [Fact]
    public void A_monster_rows_own_treasure_cell_is_what_a_death_rolls()
    {
        // The cell states coin dice unconditionally and an item on a chance: a creature with this cell always
        // drops between fifteen and ninety coins, and drops a sword only when its chance comes in.
        MightAndMagic7Loot loot = Loot(Treasure(chance: 100, rolls: 15, sides: 6, level: 2, skill: "sword"));
        LootYield found = loot.Death(Body("monster", "beast", monster: 4), Rolls("death/7/monster:beast/1"));
        Assert.InRange(found.Coins, 15, 90);
        Assert.Equal(Sword, Assert.Single(found.Items).Definition.Value);

        // A chance of nothing never yields the item, and the coin still comes: the two are separate
        // statements in the cell and separate answers here.
        MightAndMagic7Loot never = Loot(Treasure(chance: 0, rolls: 2, sides: 6, level: 2, skill: "sword"));
        LootYield coins = never.Death(Body("monster", "beast", monster: 4), Rolls("death/7/monster:beast/1"));
        Assert.Empty(coins.Items);
        Assert.InRange(coins.Coins, 2, 12);
    }

    [Fact]
    public void A_cell_that_asks_for_a_kind_of_thing_gets_that_kind_and_a_cell_that_asks_for_anything_gets_the_pool()
    {
        // The row's kind narrows the level's pool to what content tagged that way, which is the whole of what
        // the cell's last word means. A request for a sword never yields the cloak, whatever the weights say.
        MightAndMagic7Loot loot = Loot(Treasure(chance: 100, rolls: 0, sides: 0, level: 1, skill: "sword"));
        for (int draw = 0; draw < 8; draw++)
        {
            LootItem item = Assert.Single(loot.Death(Body("monster", "beast", monster: 4), Rolls($"death/7/monster:beast/{draw}")).Items);
            Assert.Equal(Sword, item.Definition.Value);
        }

        // The same level asked by another kind is another pool: only the cloak is tagged a cloak, and only at
        // the second level, so the request is answered by the cloak alone.
        MightAndMagic7Loot cloaks = Loot(Treasure(chance: 100, rolls: 0, sides: 0, level: 2, kind: "cloak"));
        Assert.Equal(Cloak, Assert.Single(cloaks.Death(Body("monster", "beast", monster: 4), Rolls("death/7/monster:beast/1")).Items).Definition.Value);
    }

    [Fact]
    public void A_level_that_offers_nothing_the_cell_asked_for_gives_the_donors_own_fallback()
    {
        // The shipped table's own answer for a level with nothing to give: a crude longsword, which is item
        // one of the item table (OpenEnroth src/Engine/Tables/ItemTable.cpp:347).
        MightAndMagic7Loot loot = Loot(Treasure(chance: 100, rolls: 0, sides: 0, level: 1, kind: "ring"));
        LootItem item = Assert.Single(loot.Death(Body("monster", "beast", monster: 4), Rolls("death/7/monster:beast/1")).Items);
        Assert.Equal(CrudeLongsword, item.Definition.Value);
        Assert.Equal("Crude Longsword", loot.NameOf(item.Definition));
    }

    [Fact]
    public void A_creature_whose_row_states_no_treasure_leaves_nothing()
    {
        // Thirty-seven of the shipped rows state a literal zero. A death with no cell — or a cell of nothing
        // — yields nothing at all, which is a fact the panel has to be able to state.
        MightAndMagic7Loot loot = Loot(Treasure(chance: 0, rolls: 0, sides: 0, level: 0));
        LootYield nothing = loot.Death(Body("monster", "beast", monster: 4), Rolls("death/7/monster:beast/1"));
        Assert.True(nothing.IsEmpty);
        Assert.Equal(0, nothing.Coins);
        Assert.Empty(nothing.Items);
    }

    [Fact]
    public void A_containers_random_reference_is_remapped_through_the_places_own_danger_level()
    {
        // A reference asks for a level and the place answers with what that level becomes there: a third-level
        // request in a place of danger zero is a first- or second-level one (OpenEnroth
        // src/Engine/Objects/Item.cpp:760-783). A party therefore finds better loot in the same chest in a
        // more dangerous place.
        MightAndMagic7Loot loot = Loot(Treasure(chance: 0, rolls: 0, sides: 0, level: 0));

        // A quiet place remaps the third level to the first or the second, which is the sword and the cloak.
        HashSet<string> quiet = [];
        for (int draw = 0; draw < 24; draw++)
        {
            LootYield found = loot.Reference(level: 3, placeLevel: 0, Findings($"container/7/container:0/0/{draw}"));
            foreach (LootItem item in found.Items) quiet.Add(item.Definition.Value);
        }

        Assert.NotEmpty(quiet);
        Assert.Subset(new HashSet<string> { Sword, Cloak }, quiet);

        // A dangerous place remaps the same request to the third or the fourth level, which content weighs
        // with items the quiet place never reaches.
        HashSet<string> dangerous = [];
        for (int draw = 0; draw < 24; draw++)
        {
            LootYield found = loot.Reference(level: 3, placeLevel: 3, Findings($"container/7/container:0/0/{draw}"));
            foreach (LootItem item in found.Items) dangerous.Add(item.Definition.Value);
        }

        // A dangerous place remaps the same request upwards, into a level whose pool is the gem alone: the
        // same reference found in a more dangerous place is worth more.
        Assert.NotEmpty(dangerous);
        Assert.Subset(new HashSet<string> { Gem }, dangerous);
        Assert.Empty(quiet.Intersect(dangerous));
    }

    [Fact]
    public void A_seventh_level_reference_hands_over_one_of_the_tables_own_artifacts()
    {
        // The seventh level is the donor's guaranteed artifact rather than a weighted item, and the pool is
        // the artifacts the shipped table will hand out (OpenEnroth src/Engine/Objects/ItemEnums.h:977-978):
        // the item marked special beyond that range is never drawn, which is what the table's own material
        // column alone would have included.
        MightAndMagic7Loot loot = Loot(Treasure(chance: 0, rolls: 0, sides: 0, level: 0));
        Assert.Equal(2, loot.ArtifactCount);

        for (int draw = 0; draw < 8; draw++)
        {
            LootYield found = loot.Reference(level: 7, placeLevel: 6, Rolls($"container/7/container:0/0/{draw}"));
            LootItem artifact = Assert.Single(found.Items);
            Assert.Contains(artifact.Definition.Value, new[] { "500", "501" });
            Assert.Contains(loot.NameOf(artifact.Definition), new[] { "Puck", "Iron Feather" });
            Assert.NotEqual(BeyondTheRange, artifact.Definition.Value);
        }
    }

    [Fact]
    public void The_same_reference_under_the_same_key_resolves_to_the_same_loot()
    {
        // Generation is keyed by the container and the slot, so a search the party could not carry away is
        // the same search when they come back for it rather than a reroll.
        MightAndMagic7Loot loot = Loot(Treasure(chance: 0, rolls: 0, sides: 0, level: 0));
        LootYield first = loot.Reference(level: 3, placeLevel: 2, Findings("container/7/container:0/3"));
        LootYield again = loot.Reference(level: 3, placeLevel: 2, Findings("container/7/container:0/3"));
        Assert.Equal(first.Coins, again.Coins);
        Assert.Equal(first.Items, again.Items);

        // A different slot is a different draw, which this lot shows by yielding something else.
        LootYield other = loot.Reference(level: 3, placeLevel: 2, Findings("container/7/container:0/4"));
        Assert.True(other.Coins != first.Coins || !other.Items.SequenceEqual(first.Items));
    }

    [Fact]
    public void A_death_leaves_a_body_holding_what_its_row_states_and_searching_it_takes_that()
    {
        // The whole path the product takes: the fight reports what it read as down, this game generates the
        // death's loot once and holds it on the body, and the search hands it through the container
        // mechanism's own transfer.
        MightAndMagic7Loot loot = Loot(Treasure(chance: 100, rolls: 3, sides: 6, level: 1, kind: string.Empty, skill: "sword"));
        CorpseGround ground = new();
        MightAndMagic7Corpses corpses = new(ground, loot);

        IReadOnlyList<Corpse> bodies = corpses.Observe(
            new PlaceId("7"),
            [new FallenCreature(Body("monster", "beast", monster: 4), new PlacePose(120, 8, 0, 0, 0), "A beast")]);

        Corpse body = Assert.Single(bodies);
        Assert.Equal(120, body.Pose.X, 3);
        LootYield held = Assert.IsType<LootYield>(ground.Held(body));
        Assert.InRange(held.Coins, 3, 18);
        Assert.Equal(Sword, Assert.Single(held.Items).Definition.Value);

        // A second reading of the same place does not roll again: the body keeps what its death left.
        corpses.Observe(new PlaceId("7"), [new FallenCreature(body.Body, body.Pose, "A beast")]);
        Assert.Equal(held, ground.Held(body));

        // The body is offered as the same kind of target a chest is, and searching it hands over the item and
        // the coin through the same outcome the container mechanism applies.
        InteractionTargetDefinition described = Assert.IsType<InteractionTargetDefinition>(
            corpses.Describe(new InteractionTargetRequest(new PlaceId("7"), body.Body, string.Empty)));
        Assert.Equal(MightAndMagic7Containers.TargetKind, described.Kind.Value);
        Assert.Equal("The body of A beast", described.Name);
        Assert.Equal(InteractionVerb.Search, described.Verb);

        using PartyEntity party = Party();
        InteractionContext context = new(new PlaceId("7"), body.Body, described, party, null);
        InteractionOutcome searched = corpses.Search(described, context);
        Assert.True(searched.IsApplied);
        Assert.Equal(MightAndMagic7Containers.SearchedState, searched.State);
        Assert.Equal(Sword, Assert.Single(searched.Items).Definition.Value);
        Assert.Equal(held.Coins, searched.Gain.Coins);
        Assert.Contains("Sword", searched.Message, StringComparison.Ordinal);
        Assert.Contains("gold", searched.Message, StringComparison.Ordinal);

        // Emptied once is emptied: the state word the search recorded is what the second attempt reads.
        InteractionOutcome again = corpses.Search(described with { State = searched.State }, context);
        Assert.False(again.IsApplied);
        Assert.Equal("container-emptied", again.Refusal?.Code);
    }

    [Fact]
    public void A_body_whose_row_states_a_zero_leaves_a_body_that_holds_nothing()
    {
        // The thirty-seven rows that state nothing still leave a body: what the party killed is lying there,
        // and searching it says so rather than offering loot the row never promised.
        MightAndMagic7Loot loot = Loot(Treasure(chance: 0, rolls: 0, sides: 0, level: 0));
        CorpseGround ground = new();
        MightAndMagic7Corpses corpses = new(ground, loot);
        Corpse body = Assert.Single(corpses.Observe(
            new PlaceId("7"),
            [new FallenCreature(Body("monster", "beast", monster: 4), PlacePose.Origin, "A beast")]));

        Assert.True(ground.Held(body)?.IsEmpty);
        InteractionTargetDefinition described = Assert.IsType<InteractionTargetDefinition>(
            corpses.Describe(new InteractionTargetRequest(new PlaceId("7"), body.Body, string.Empty)));
        using PartyEntity party = Party();
        InteractionOutcome searched = corpses.Search(described, new InteractionContext(new PlaceId("7"), body.Body, described, party, null));
        Assert.True(searched.IsApplied);
        Assert.Empty(searched.Items);
        Assert.Contains("holds nothing", searched.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_chest_holding_a_random_reference_is_answered_by_the_loot_owner_rather_than_refused()
    {
        // The container mechanism's own search path, with a record that holds the donor's negative reference:
        // the reference is answered through the place's danger level, and what it yields enters the same
        // outcome every container uses.
        MightAndMagic7Loot loot = Loot(Treasure(chance: 0, rolls: 0, sides: 0, level: 0));
        using PartyEntity party = Party();
        PlacementDefinition chest = Placement(
            "container",
            "container-0",
            """
            { "id": "container-0", "kind": "container", "x": 100, "y": 0, "z": 0, "flags": 0, "mapTreasureLevel": 0,
              "contents": [ { "slot": 0, "item": -3 } ] }
            """);
        InteractionTargetDefinition target = new(
            new InteractionTargetKind(MightAndMagic7Containers.TargetKind),
            "A chest",
            InteractionVerb.Search,
            reach: 512);
        InteractionContext context = new(new PlaceId("7"), chest, target, party, null);

        InteractionOutcome searched = MightAndMagic7Containers.Search(target, context, loot);
        Assert.True(searched.IsApplied);
        Assert.Equal(MightAndMagic7Containers.SearchedState, searched.State);
        Assert.Contains(searched.Items, yield => yield.Definition.Value is Sword or Cloak);
    }

    [Fact]
    public void A_chest_with_a_random_reference_and_no_generator_is_refused_by_name()
    {
        // A session whose ruleset reads no item weights cannot answer a reference, and says which one it
        // could not answer rather than emptying the chest of invented contents.
        using PartyEntity party = Party();
        PlacementDefinition chest = Placement(
            "container",
            "container-0",
            """
            { "id": "container-0", "kind": "container", "x": 100, "y": 0, "z": 0, "flags": 0,
              "contents": [ { "slot": 0, "item": -4 } ] }
            """);
        InteractionTargetDefinition target = new(
            new InteractionTargetKind(MightAndMagic7Containers.TargetKind),
            "A chest",
            InteractionVerb.Search,
            reach: 512);

        InteractionOutcome refused = MightAndMagic7Containers.Search(target, new InteractionContext(new PlaceId("7"), chest, target, party, null), loot: null);
        Assert.False(refused.IsApplied);
        Assert.Equal("container-contents-unresolved", refused.Refusal?.Code);
        Assert.Contains("treasure level 4", refused.Message, StringComparison.Ordinal);
    }

    private static Cell Treasure(int chance, int rolls, int sides, int level, string kind = "", string skill = "") =>
        new(chance, rolls, sides, level, kind, skill);

    /// <summary>This game's loot over a small table written in the shape the importer emits.</summary>
    /// <remarks>
    /// The items carry the weights the shipped random-item table states for their ids: the longsword weighs
    /// at the first three levels, and the cloak at the second and the fourth. The third item is outside the
    /// donor's spawnable-artifact range and lighter than the two the donor hands out, so a level-seven
    /// reference that read the material column alone would be visible here.
    /// </remarks>
    private static MightAndMagic7Loot Loot(Cell treasure) =>
        MightAndMagic7Loot.Compose(
            Catalog(
                Item(CrudeLongsword, "Crude Longsword", "single-handed", "sword", "steel", "[0,0,0,0,0,0]"),
                Item(Cloak, "Cloak", "cloak", "misc", "cloth", "[0,10,0,0,0,0]"),
                Item(Gem, "Gem", "gem", "misc", "crystal", "[0,0,10,10,0,0]"),
                Item(Sword, "Sword", "single-handed", "sword", "steel", "[10,5,0,0,0,0]"),
                Item("500", "Puck", "single-handed", "sword", "Artifact", "[0,0,0,0,0,0]"),
                Item("501", "Iron Feather", "single-handed", "sword", "Artifact", "[0,0,0,0,0,0]"),
                Item(BeyondTheRange, "A special beyond the range", "single-handed", "sword", "Artifact", "[0,0,0,0,0,0]"),
                Monster(4, "A beast", treasure)),
            random: Keyed());

    private static LootRolls Rolls(string key) => new(Keyed(), seed: 9, scope: "test.loot", key);

    /// <summary>The same rolls a case names its own key for.</summary>
    private static LootRolls Findings(string key) => Rolls(key);

    /// <summary>
    /// The engine's keyed randomness, answered as the engine answers it: the same scope and key always draw
    /// the same value, and a different key draws a different one. A container's finding is answered above the
    /// band it yields nothing in, so a reference is exercised as the things it yields rather than as draws
    /// that all say nothing.
    /// </summary>
    private static TestRandomService Keyed() => new()
    {
        Answer = request =>
        {
            if (request.Key.Contains("/finding/", StringComparison.Ordinal) && request.Minimum == 1 && request.Maximum == 100) return 80;

            ulong hash = 14695981039346656037UL;
            foreach (byte value in System.Text.Encoding.UTF8.GetBytes($"{request.Scope}|{request.Key}"))
            {
                hash = (hash ^ value) * 1099511628211UL;
            }

            return request.Minimum + (long)(hash % (ulong)(request.Maximum - request.Minimum + 1));
        },
    };

    /// <summary>A placement standing in a place, as content declares one.</summary>
    private static PlacementDefinition Body(string kind, string id, int monster) =>
        Placement(kind, id, $$"""{ "id": "{{id}}", "kind": "{{kind}}", "monster": {{monster}}, "x": 100, "y": 0, "z": 0 }""");

    private static PlacementDefinition Placement(string kind, string id, string json) =>
        new(new PlacementContentId(kind, id), "placements", 0, PlacePose.Origin, new ContentEntry(id, JsonDocument.Parse(json).RootElement));

    private static PartyEntity Party() =>
        new PartyEntityFactory().Create(new PartyCreation(
            [new MemberCreation(new PartyMemberSeed(
                "Tester",
                new RaceId("Human"),
                new ClassId("Knight"),
                [new AttributeScore(new AttributeId("Might"), 13)],
                skills: [],
                spells: [],
                experience: 0,
                level: 1,
                skillPoints: 0,
                classRank: 1,
                conditions: [],
                hitPoints: ResourcePool.Full(40),
                spellPoints: ResourcePool.Full(0)))],
            coins: 0,
            foodPortions: 2,
            ProvisionUnit.Portions,
            reputation: 0,
            fame: 0));

    /// <summary>One item entry, in the shape the importer writes one.</summary>
    private static string Item(string id, string name, string kind, string skill, string material, string weights) =>
        $$"""
        { "id": "{{id}}", "name": "{{name}}", "type": "{{kind}}", "skill": "{{skill}}", "material": "{{material}}", "lootWeights": {{weights}} }
        """;

    /// <summary>One monster entry, in the shape the importer writes one.</summary>
    private static string Monster(int id, string name, Cell treasure) =>
        $$"""
        { "id": "{{id}}", "name": "{{name}}", "hostility": 2, "recovery": 100,
          "treasureRoll": { "chance": {{treasure.Chance}}, "goldRolls": {{treasure.Rolls}}, "goldSides": {{treasure.Sides}}, "level": {{treasure.Level}}{{Kind(treasure)}}{{Skill(treasure)}} } }
        """;

    private static string Kind(Cell treasure) =>
        treasure.Kind.Length > 0 ? $", \"kind\": \"{treasure.Kind}\"" : string.Empty;

    private static string Skill(Cell treasure) =>
        treasure.Skill.Length > 0 ? $", \"skill\": \"{treasure.Skill}\"" : string.Empty;

    /// <summary>A catalog holding the item and monster documents a loot reading needs.</summary>
    private static ContentCatalog Catalog(params string[] entries)
    {
        List<string> items = [];
        List<string> monsters = [];
        foreach (string entry in entries)
        {
            (entry.Contains("\"lootWeights\"", StringComparison.Ordinal) ? items : monsters).Add(entry);
        }

        return ContentCatalogLoader.Load(
            new InMemoryContentSource()
                .Add(
                    "packs/tables/pack.json",
                    """
                    {
                      "schemaVersion": 1,
                      "packId": "tables",
                      "kind": "definitions",
                      "provenance": { "description": "test content" },
                      "documents": [
                        { "path": "items.json", "documentId": "items", "definitionKind": "item" },
                        { "path": "monsters.json", "documentId": "monsters", "definitionKind": "monster" }
                      ]
                    }
                    """)
                .Add("packs/tables/items.json", $$"""{ "documentId": "items", "definitionKind": "item", "entries": [ {{string.Join(",", items)}} ] }""")
                .Add("packs/tables/monsters.json", $$"""{ "documentId": "monsters", "definitionKind": "monster", "entries": [ {{string.Join(",", monsters)}} ] }"""),
            new ContentLayout("packs", "imports", "bundles")).RequireValid();
    }

    /// <summary>
    /// Content staged in memory, so this suite declares the tables it reads without touching the file system.
    /// </summary>
    private sealed class InMemoryContentSource : IContentSource
    {
        private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);

        internal InMemoryContentSource Add(string path, string text)
        {
            _files[path] = text;
            return this;
        }

        public IReadOnlyList<string> ListDirectories(string relativePath)
        {
            string prefix = Normalize(relativePath);
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (string path in _files.Keys)
            {
                if (!path.StartsWith(prefix, StringComparison.Ordinal)) continue;
                string remainder = path[prefix.Length..];
                int separator = remainder.IndexOf('/', StringComparison.Ordinal);
                if (separator > 0) names.Add(remainder[..separator]);
            }

            return [.. names.Order(StringComparer.Ordinal)];
        }

        public IReadOnlyList<string> ListFiles(string relativePath)
        {
            string prefix = Normalize(relativePath);
            return [.. _files.Keys
                .Where(path => path.StartsWith(prefix, StringComparison.Ordinal))
                .Select(path => path[prefix.Length..])
                .Where(remainder => remainder.Length > 0 && !remainder.Contains('/', StringComparison.Ordinal))
                .Order(StringComparer.Ordinal)];
        }

        public bool FileExists(string relativePath) => _files.ContainsKey(relativePath);

        public string ReadText(string relativePath) =>
            _files.TryGetValue(relativePath, out string? text) ? text : throw new FileNotFoundException(relativePath);

        private static string Normalize(string relativePath) =>
            relativePath.Length == 0 ? string.Empty : relativePath.TrimEnd('/') + "/";
    }
}
