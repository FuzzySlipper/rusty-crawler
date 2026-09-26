using System.Globalization;
using PartyRpg.Kit.Alchemy;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Time;
using PartyRpg.Rulesets.MightAndMagic7;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Host.Tests;

/// <summary>
/// Alchemy through the whole product: the shipped potion table's own mixtures read into this game's recipe
/// table, a reagent and a bottle combined through the party's one shared pack, the rung of Alchemy a result
/// asks for judged against the character's own mastery, the consequence of a mixture this game states as
/// incompatible, and a potion drunk as a casting whose source is the item.
/// </summary>
/// <remarks>
/// <para>
/// What this proves is the trip rather than the mechanism: the operator's potion rows are read into this
/// game's own answers, the mixtures they state become the recipes the kit's workflow carries out, and a potion
/// enters the one effect path a spell uses with its own strength as the level the effect is read at. Nothing
/// here is a second effect table: the potion rows below are the shipped ids, and the reading each one is drunk
/// as is the same row of the same table the spells are read from.
/// </para>
/// <para>
/// The donor citations are the readings' own: the four mastery bands that gate a mixture
/// (<c>OpenEnroth/src/GUI/UI/UIPopup.cpp:2092-2112</c>), the strength a reagent's own power and the mixer's
/// skill add up to (<c>:2267</c>), and what a burst costs (<c>:2118-2131</c>).
/// </para>
/// </remarks>
public sealed class AlchemyPolicyTests
{
    /// <summary>The reagent this suite's content declares: the shipped berries' own row.</summary>
    private static readonly ItemDefinitionId Berries = new("200");

    /// <summary>The bottle this suite's content declares, which is what a reagent is mixed into.</summary>
    private static readonly ItemDefinitionId Bottle = new("220");

    /// <summary>The red potion this suite's content declares: the shipped first real potion's own row.</summary>
    private static readonly ItemDefinitionId CureWounds = new("222");

    /// <summary>The haste potion this suite's content declares: a compound potion that asks for a rung.</summary>
    private static readonly ItemDefinitionId Haste = new("228");

    /// <summary>The black potion whose own row states nothing but a burst against everything below it.</summary>
    private static readonly ItemDefinitionId StoneToFlesh = new("262");

    [Fact]
    public void The_potion_rows_are_read_as_mixtures_a_recipe_table_asks_a_rung_for_and_a_potion_carries_a_spell()
    {
        (MightAndMagic7Alchemy alchemy, MightAndMagic7Spells spells) = Read();

        // Content states what a potion's own mixture asks for, which the shipped table does not carry: the
        // first three potions need nothing, and the compound ones ask for the second rung.
        Assert.Equal(0, alchemy.TierOf(CureWounds).Value);
        Assert.Equal(2, alchemy.TierOf(Haste).Value);
        Assert.Equal(4, alchemy.TierOf(StoneToFlesh).Value);

        // The reagent's own recipe is the row's own words — "+ Bottle = Red Potion +1" — resolved through the
        // potion rows' own colour descriptions, and it comes out at the reagent's stated power.
        PotionMixture recipe = alchemy.Catalog.Find(Berries, Bottle)
            ?? throw new InvalidOperationException("The berries' own row states the bottle recipe, so the catalog must carry it.");
        Assert.True(recipe.Produces);
        Assert.Equal(CureWounds, recipe.Outcome.Result);
        Assert.Equal(1, recipe.Power);

        // A potion is an item that carries the effect this game states for its row, and drinking it is a
        // casting whose source is that item rather than a spell the character learned.
        SpellItemReading reading = spells.Reading(CureWounds)!.Value;
        Assert.True(reading.ConsumedByUse);
        Assert.Equal(MightAndMagic7Potions.EffectId(222), reading.Spell);

        // The bottle and the reagents carry no effect of their own: they are what a potion is made of.
        Assert.Null(spells.Reading(Bottle));
        Assert.Null(spells.Reading(Berries));
    }

    [Fact]
    public void Mixing_a_reagent_into_a_bottle_at_the_rung_it_asks_for_puts_the_potion_into_the_pack()
    {
        (MightAndMagic7Alchemy alchemy, _) = Read();
        using PartyEntity party = Party();
        PotionMixing mixing = new(party, alchemy.Catalog, alchemy);

        // The scenario's own pack: two reagents and two bottles, which is what the recipe needs.
        ItemInstance berries = First(party, Berries);
        ItemInstance bottle = First(party, Bottle);
        int before = party.Inventory.Count;
        int alchemyLevel = party.Members[0].Skills.LevelOf(alchemy.Skill);

        MixingResult result = mixing.Mix(new MixingRequest(0, berries.Id, bottle.Id));

        Assert.True(result.IsMixed);
        Assert.Equal("potion", result.Outcome);
        Assert.Equal(CureWounds.Value, result.Result);

        // The strength is the donor's own arithmetic: the mixer's level in Alchemy plus the power the
        // reagent's row states (OpenEnroth src/GUI/UI/UIPopup.cpp:2267).
        Assert.Equal(alchemyLevel + 1, result.Power);
        ItemInstance made = party.Items.Single(item => item.Definition == CureWounds);
        Assert.Equal(alchemyLevel + 1, made.State.Potency);

        // Both ingredients left the pack, the potion came in through the same acquisition path a pickup and a
        // purchase take, and the pack holds one instance fewer than it did: two things became one.
        Assert.Null(party.FindItem(berries.Id));
        Assert.Null(party.FindItem(bottle.Id));
        Assert.True(made.Custody.IsInSharedInventory);
        Assert.Equal(before - 1, party.Inventory.Count);
    }

    [Fact]
    public void A_rung_the_mixer_has_not_reached_is_refused_in_this_games_own_words_and_a_burst_costs_what_it_states()
    {
        (MightAndMagic7Alchemy alchemy, _) = Read();
        using PartyEntity party = Party();
        PotionMixing mixing = new(party, alchemy.Catalog, alchemy);

        // The knight holds no Alchemy at all, so a compound potion is out of reach: the refusal names the rung
        // the mixture asks for, the rung the character stands at, and what would raise it. The donor lets an
        // under-skilled mixture explode instead (OpenEnroth src/GUI/UI/UIPopup.cpp:2092-2112); this game
        // refuses it by name before anything is spent, and keeps the explosion for the pairs the table itself
        // states as incompatible.
        ItemInstance woundPotion = Take(party, CureWounds);
        ItemInstance haste = Take(party, Haste);
        MixingResult refused = mixing.Mix(new MixingRequest(1, woundPotion.Id, haste.Id));

        Assert.False(refused.IsMixed);
        Assert.Equal("mixture-mastery-too-low", refused.Code);
        Assert.Contains("Alchemy", refused.Message, StringComparison.Ordinal);
        Assert.Contains("raises the rung", refused.Message, StringComparison.Ordinal);
        Assert.NotNull(party.FindItem(woundPotion.Id));
        Assert.NotNull(party.FindItem(haste.Id));

        // A pair the table states a burst for is attempted and goes off: both ingredients are destroyed and
        // the mixing character takes the donor's own harm for the strength that row states — ten to twenty at
        // the first strength (OpenEnroth src/GUI/UI/UIPopup.cpp:2118-2121).
        ItemInstance stone = Take(party, StoneToFlesh);
        int before = party.Members[0].Resources.HitPoints.Current;
        MixingResult burst = mixing.Mix(new MixingRequest(0, haste.Id, stone.Id));

        Assert.True(burst.IsMixed);
        Assert.Equal("burst", burst.Outcome);
        Assert.Equal(1, burst.Burst);
        Assert.InRange(burst.Harm, 10, 20);
        Assert.Equal(before - burst.Harm, party.Members[0].Resources.HitPoints.Current);
        Assert.Null(party.FindItem(haste.Id));
        Assert.Null(party.FindItem(stone.Id));

        // The deepest band is not a wound at all: the donor eradicates the character who set it off, and the
        // condition lands on their own state exactly as a cure lands there.
        ItemInstance black = Take(party, StoneToFlesh);
        MixingResult eradicated = mixing.Mix(new MixingRequest(0, woundPotion.Id, black.Id));

        Assert.True(eradicated.IsMixed);
        Assert.Equal(4, eradicated.Burst);
        Assert.Equal("Eradicated", eradicated.Condition);
        Assert.True(party.Members[0].Conditions.Has(MightAndMagic7Conditions.Eradicated));
        Assert.Equal(before - burst.Harm, party.Members[0].Resources.HitPoints.Current);
    }

    [Fact]
    public void A_potion_drunk_lands_its_effect_through_the_one_effect_path_at_the_strength_it_was_made()
    {
        (MightAndMagic7Alchemy alchemy, MightAndMagic7Spells spells) = Read();
        using PartyEntity party = Party();
        GameClock clock = Clock();
        MightAndMagic7SpellEffects effects = new(spells, clock, () => null);
        Spellcasting casting = new(party, spells, effects, fight: null);

        // A potion the party itself mixed: the strength it came out at is what its effect is read at, which is
        // the donor's own reading of a potion (its duration is thirty minutes a point of strength,
        // OpenEnroth src/Engine/Objects/Character.cpp:3084-3085).
        PotionMixing mixing = new(party, alchemy.Catalog, alchemy);
        ItemInstance berries = First(party, Berries);
        ItemInstance bottle = First(party, Bottle);
        Assert.True(mixing.Mix(new MixingRequest(0, berries.Id, bottle.Id)).IsMixed);
        ItemInstance potion = party.Items.Single(item => item.Definition == CureWounds);
        int strength = potion.State.Potency;
        int wounded = party.Members[0].Resources.HitPoints.Current;
        party.Members[0].TakeDamage(25);

        SpellCastResult drunk = casting.Cast(new SpellCastRequest(
            0,
            MightAndMagic7Potions.EffectId(222),
            Target: string.Empty,
            Item: potion.Id));

        Assert.True(drunk.IsCast);
        Assert.True(drunk.Outcome!.IsExpressed);

        // The health it gave back came through the member's own pool, at ten plus the potion's strength —
        // the donor's own cure (OpenEnroth src/Engine/Objects/Character.cpp:3091-3093) — and the potion paid
        // for the casting: no spell point was spent and the instance left the pack.
        Assert.Equal(wounded - 25 + 10 + strength, party.Members[0].Resources.HitPoints.Current);
        Assert.Equal(party.Members[0].Resources.SpellPoints.Maximum, party.Members[0].Resources.SpellPoints.Current);
        Assert.DoesNotContain(party.Items, item => item.Id == potion.Id);

        // A potion whose effect lasts leaves a per-character effect with a deadline on the one clock, which is
        // what makes a haste from a bottle the same state a haste from a spell is.
        ItemInstance hastePotion = party.CreateItem(Haste, new ItemState(isIdentified: true, potency: 4), stackCount: 1);
        Assert.True(party.AcquireItem(hastePotion).Admitted);
        SpellCastResult hasted = casting.Cast(new SpellCastRequest(
            0,
            MightAndMagic7Potions.EffectId(228),
            Target: string.Empty,
            Item: hastePotion.Id));

        Assert.True(hasted.IsCast);
        IMemberSpellEffects onMembers = effects;
        Assert.Equal(5, onMembers.MagnitudeOn(party.Members[0], SpellEffectIds.Haste));
        RunningSpellEffect running = Assert.Single(onMembers.RunningOnMembers);
        Assert.Equal(party.Members[0].Id, running.Member);
        Assert.Equal(SpellEffectIds.Haste, running.Effect);

        // Thirty minutes a point of strength, off the clock the session runs on: the effect ends in the very
        // advance that reaches its deadline.
        // Thirty minutes a point of strength: the deadline is the moment the clock was at plus that long, and
        // the ledger is the only thing that knows the arithmetic, so the advance below is what proves it.
        Assert.Equal(new GameDate(1168, 1, 1, 11, 0), running.EndsAt);
        effects.Observe(clock.Advance(GameDuration.FromMinutes((30 * 4) - 1)));
        Assert.Equal(5, onMembers.MagnitudeOn(party.Members[0], SpellEffectIds.Haste));
        effects.Observe(clock.Advance(GameDuration.FromMinutes(1)));
        Assert.Equal(0, onMembers.MagnitudeOn(party.Members[0], SpellEffectIds.Haste));
        Assert.Empty(onMembers.RunningOnMembers);
    }

    /// <summary>Reads this game's alchemy and magic over this suite's own content, as the session does.</summary>
    private static (MightAndMagic7Alchemy Alchemy, MightAndMagic7Spells Spells) Read()
    {
        (ProductCreateContext context, _) = ProductTestContext.Create(Content());
        ContentCatalog catalog = ContentCatalogLoader.Load(
            new ProductContentSource(context.Content),
            ContentLayout.Under(ProductTestContext.ContentDirectory)).RequireValid();

        MightAndMagic7Skills skills = MightAndMagic7Skills.Read(catalog)
            ?? throw new InvalidOperationException("The content declares skills, so reading them must produce a policy.");
        MightAndMagic7Alchemy alchemy = MightAndMagic7Alchemy.Read(catalog, skills)
            ?? throw new InvalidOperationException("The content declares mixtures, so reading them must produce a table.");
        MightAndMagic7Spells spells = MightAndMagic7Spells.Read(catalog, skills, alchemy)
            ?? throw new InvalidOperationException("The content declares spells, so reading them must produce a table.");
        return (alchemy, spells);
    }

    /// <summary>The party this suite's scenario declares, read the way the session reads it.</summary>
    private static PartyEntity Party()
    {
        (ProductCreateContext context, _) = ProductTestContext.Create(Content());
        ContentCatalog catalog = ContentCatalogLoader.Load(
            new ProductContentSource(context.Content),
            ContentLayout.Under(ProductTestContext.ContentDirectory)).RequireValid();
        return MightAndMagic7Party.Compose(catalog)
            ?? throw new InvalidOperationException("The content declares a scenario party, so composing one must produce it.");
    }

    /// <summary>The pack's own first instance of a definition, which the scenario put there.</summary>
    private static ItemInstance First(PartyEntity party, ItemDefinitionId definition) =>
        party.Items.First(item => item.Definition == definition);

    /// <summary>Takes one item of a definition into the pack and hands the instance back.</summary>
    private static ItemInstance Take(PartyEntity party, ItemDefinitionId definition)
    {
        ItemInstance item = party.CreateItem(definition);
        Assert.True(party.AcquireItem(item).Admitted);
        return item;
    }

    /// <summary>The session's one clock, at this game's own rate and on its own calendar.</summary>
    private static GameClock Clock() => new(
        GameCalendar.TwelveMonthsOfFourWeeks,
        new GameDate(1168, 1, 1, 9, 0),
        new GameTimeScale(30),
        new DaylightWindow(new TimeOfDay(5, 0), new TimeOfDay(21, 0)));

    /// <summary>
    /// The content this suite reads: the shipped potion ids with the mixtures its own table states, and a
    /// scenario party carrying the reagent and the bottle.
    /// </summary>
    /// <remarks>
    /// The rows are the shipped ids because this game's own potion readings are keyed by them — a potion's row
    /// in <c>POTION.TXT</c> is the same row in the item table, which is how the donor joins the two. The
    /// mixture cells below are the shipped table's own: berries and a bottle make the red potion, the black
    /// potion bursts against everything below it, and a potion mixed with itself does nothing.
    /// </remarks>
    private static (string Path, string Text)[] Content() =>
    [
        ProductTestContext.Bundle("partyrpg-default", "world"),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/pack.json",
            """
            {
              "schemaVersion": 1,
              "packId": "world",
              "kind": "definitions",
              "provenance": { "description": "authored for a test" },
              "documents": [
                { "path": "places.json", "documentId": "places", "definitionKind": "place" },
                { "path": "start.json", "documentId": "start", "definitionKind": "scenario-start" },
                { "path": "party.json", "documentId": "party", "definitionKind": "scenario-party" },
                { "path": "skills.json", "documentId": "skills", "definitionKind": "skill" },
                { "path": "items.json", "documentId": "items", "definitionKind": "item" },
                { "path": "potions.json", "documentId": "potions", "definitionKind": "potion" }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/places.json",
            """
            {
              "documentId": "places",
              "definitionKind": "place",
              "entries": [
                { "id": "1", "kind": "region", "name": "The Alchemist's Shop", "respawnDays": 1,
                  "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ] }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/start.json",
            """
            { "documentId": "start", "definitionKind": "scenario-start", "entries": [ { "id": "start", "place": "1", "entryPoint": "Party Start" } ] }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/skills.json",
            """
            {
              "documentId": "skills",
              "definitionKind": "skill",
              "entries": [ { "id": "Alchemy" }, { "id": "Sword" } ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/items.json",
            """
            {
              "documentId": "items",
              "definitionKind": "item",
              "entries": [
                { "id": "200", "name": "Widowsweep Berries", "value": 1, "equipStat": "Reagent", "type": "reagent", "skillGroup": "Misc", "skill": "misc", "damageDice": "1", "damageModifier": "0" },
                { "id": "220", "name": "Potion Bottle", "value": 1, "equipStat": "Bottle", "type": "potion", "skillGroup": "Misc", "skill": "misc", "damageDice": "0", "damageModifier": "1" },
                { "id": "222", "name": "Cure Wounds", "value": 5, "equipStat": "Bottle", "type": "potion", "skillGroup": "Misc", "skill": "misc", "damageDice": "0", "damageModifier": "1" },
                { "id": "228", "name": "Haste", "value": 150, "equipStat": "Bottle", "type": "potion", "skillGroup": "Misc", "skill": "misc", "damageDice": "0", "damageModifier": "1" },
                { "id": "262", "name": "Stone to Flesh", "value": 2000, "equipStat": "Bottle", "type": "potion", "skillGroup": "Misc", "skill": "misc", "damageDice": "0", "damageModifier": "0" }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/potions.json",
            """
            {
              "documentId": "potions",
              "definitionKind": "potion",
              "entries": [
                { "id": "200", "name": "Widowsweep Berries", "description": "Reagent", "effect": "+ Bottle = Red Potion +1", "kind": "reagent", "tier": 0, "power": 1, "mixtures": { "220": "222" } },
                { "id": "220", "name": "Potion Bottle", "description": "Empty Bottle", "effect": "None", "kind": "bottle", "tier": 0 },
                { "id": "222", "name": "Cure Wounds", "description": "Red Potion", "effect": "Heal 10+skill HP", "kind": "potion", "units": [1, 0, 0], "tier": 0, "mixtures": { "222": "none", "228": "228", "262": "burst:4" } },
                { "id": "228", "name": "Haste", "description": "Red and Orange Potion", "effect": "Cast Haste", "kind": "potion", "units": [2, 0, 1], "tier": 2, "mixtures": { "222": "228", "228": "none", "262": "burst:1" } },
                { "id": "262", "name": "Stone to Flesh", "description": "Black Potion", "effect": "Remove Stone cond", "kind": "potion", "units": [3, 2, 3], "tier": 4, "mixtures": { "222": "burst:4", "228": "burst:1", "262": "none" } }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/party.json",
            """
            {
              "documentId": "party",
              "definitionKind": "scenario-party",
              "entries": [
                {
                  "id": "party", "coins": 100, "food": 6, "reputation": 0, "fame": 0,
                  "pack": [ { "item": "200", "count": 2 }, { "item": "220", "count": 2 } ],
                  "members": [
                    {
                      "name": "Aelina", "race": "Elf", "class": "Sorcerer", "level": 5, "hitPoints": 30, "spellPoints": 20,
                      "attributes": [
                        { "id": "Might", "value": 9 }, { "id": "Intellect", "value": 40 }, { "id": "Personality", "value": 15 },
                        { "id": "Endurance", "value": 20 }, { "id": "Accuracy", "value": 20 }, { "id": "Speed", "value": 25 },
                        { "id": "Luck", "value": 13 }
                      ],
                      "skills": [ { "id": "Alchemy", "level": 3, "tier": 3, "pointsSpent": 6 } ],
                      "spells": [], "conditions": []
                    },
                    {
                      "name": "Borin", "race": "Human", "class": "Knight", "level": 1, "hitPoints": 40, "spellPoints": 0,
                      "attributes": [
                        { "id": "Might", "value": 13 }, { "id": "Intellect", "value": 9 }, { "id": "Personality", "value": 9 },
                        { "id": "Endurance", "value": 13 }, { "id": "Accuracy", "value": 13 }, { "id": "Speed", "value": 9 },
                        { "id": "Luck", "value": 9 }
                      ],
                      "skills": [ { "id": "Sword", "level": 1, "tier": 1, "pointsSpent": 1 } ],
                      "spells": [], "conditions": []
                    }
                  ]
                }
              ]
            }
            """),
    ];
}
