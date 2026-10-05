using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MightAndMagic7.Import.Collision;
using MightAndMagic7.Import.Events;
using MightAndMagic7.Import.Lod;
using MightAndMagic7.Import.Maps;
using MightAndMagic7.Import.Packs;
using MightAndMagic7.Import.Tables;
using MightAndMagic7.Import.World;

namespace MightAndMagic7.Import.Tool;

/// <summary>The tables pack's documents: classes, skills, spells, monsters and how they fight, hostility, items, potions, quests, and services.</summary>
internal static partial class PackWriter
{
    private static int WriteClasses(string packDirectory, Mm7Tables tables)
    {
        List<(string Id, Action<Utf8JsonWriter> Write)> entries = [];
        foreach (ClassRecord rank in tables.Classes.Ranks)
        {
            entries.Add((rank.Name, writer =>
            {
                writer.WriteString("description", rank.Description);
                writer.WriteString("baseClass", rank.BaseClass);
                writer.WriteNumber("rank", rank.Rank);
            }));
        }

        return WriteDocument(packDirectory, "classes.json", "classes", "class", entries);
    }

    private static int WriteSkills(string packDirectory, Mm7Tables tables)
    {
        List<(string Id, Action<Utf8JsonWriter> Write)> entries = [];
        foreach (SkillRecord skill in tables.Skills.Skills)
        {
            entries.Add((skill.Name, writer =>
            {
                writer.WriteString("description", skill.Description);
                writer.WriteString("normal", skill.Normal);
                writer.WriteString("expert", skill.Expert);
                writer.WriteString("master", skill.Master);
                writer.WriteString("grandMaster", skill.GrandMaster);
            }));
        }

        return WriteDocument(packDirectory, "skills.json", "skills", "skill", entries);
    }

    private static int WriteSpells(string packDirectory, Mm7Tables tables)
    {
        List<(string Id, Action<Utf8JsonWriter> Write)> entries = [];
        foreach (SpellRecord spell in tables.Spells.Spells)
        {
            entries.Add((spell.Id.ToString(CultureInfo.InvariantCulture), writer =>
            {
                writer.WriteString("school", spell.School);
                writer.WriteNumber("level", spell.Level);
                writer.WriteString("name", spell.Name);
                writer.WriteString("resist", spell.Resist);
                writer.WriteString("shortName", spell.ShortName);
                writer.WriteString("description", spell.Description);
                writer.WriteString("normal", spell.Normal);
                writer.WriteString("expert", spell.Expert);
                writer.WriteString("master", spell.Master);
                writer.WriteString("grandMaster", spell.GrandMaster);
            }));
        }

        return WriteDocument(packDirectory, "spells.json", "spells", "spell", entries);
    }

    private static int WriteMonsters(string packDirectory, Mm7Tables tables)
    {
        List<(string Id, Action<Utf8JsonWriter> Write)> entries = [];
        foreach (MonsterRecord monster in tables.Monsters.Monsters)
        {
            entries.Add((monster.Id.ToString(CultureInfo.InvariantCulture), writer =>
            {
                writer.WriteString("name", monster.Name);

                // The table's own internal name is what the game itself finds a row by when it creates a
                // creature no map places — a summoned elemental is asked for as "Elemental Light A" (OpenEnroth
                // src/Engine/Objects/Actor.cpp:4145-4156) — so it is carried beside the name a person reads.
                string internalName = monster.Fields.Count > 2 ? monster.Fields[2].Trim() : string.Empty;
                if (internalName.Length > 0) writer.WriteString("internalName", internalName);
                // Arena candidates exclude timid inhabitants and special rows. The original checks
                // Wimp AI and the special-monster identity boundary (MonsterEnumFunctions.cpp:118-129).
                // Our normalized reading recognises the table's z-prefixed special internal names.
                writer.WriteBoolean("arenaEligible", monster.AiType != "Wimp" && !internalName.StartsWith("z", StringComparison.OrdinalIgnoreCase));
                writer.WriteNumber("level", monster.Level);
                writer.WriteNumber("hitPoints", monster.HitPoints);
                writer.WriteNumber("armorClass", monster.ArmorClass);
                writer.WriteNumber("experience", monster.Experience);
                writer.WriteNumber("hostility", monster.Hostility);
                writer.WriteNumber("speed", monster.Speed);
                writer.WriteNumber("recovery", monster.Recovery);
                writer.WriteString("aiType", monster.AiType);
                writer.WriteString("movement", monster.Movement);
                writer.WriteString("fly", monster.Fly);
                WriteOptionalString(writer, "treasure", monster.Treasure);

                // The cell as it stands is kept beside what it states, so the numbers a fight draws from can
                // be checked against the bytes they came from; a creature that drops nothing states a cell of
                // zero and a roll of nothing rather than no roll at all.
                writer.WriteStartObject("treasureRoll");
                writer.WriteNumber("chance", monster.TreasureRoll.Chance);
                writer.WriteNumber("goldRolls", monster.TreasureRoll.GoldRolls);
                writer.WriteNumber("goldSides", monster.TreasureRoll.GoldSides);
                writer.WriteNumber("level", monster.TreasureRoll.Level);
                if (monster.TreasureRoll.Kind.Length > 0) writer.WriteString("kind", monster.TreasureRoll.Kind);
                if (monster.TreasureRoll.Skill.Length > 0) writer.WriteString("skill", monster.TreasureRoll.Skill);
                writer.WriteEndObject();
                WriteCombat(writer, monster.Combat);
            }));
        }

        return WriteDocument(packDirectory, "monsters.json", "monsters", "monster", entries);
    }

    /// <summary>
    /// Writes what every kind of monster thinks of every other kind and of the party, as the shipped
    /// matrix states it.
    /// </summary>
    /// <remarks>
    /// One entry per kind, carrying the bands it holds toward the kinds the header names, in the header's
    /// own column order. The party's own row and column are written with the rest: the party's row is what
    /// a creature fighting for the party reads its targets from, and the party's column is what a kind
    /// thinks of the party, so neither is an empty cell a reader would have to special-case.
    /// </remarks>
    private static int WriteHostility(string packDirectory, Mm7Tables tables)
    {
        HostilityTable matrix = tables.Hostility;
        int row = 0;
        // The header is an entry of its own so the pack names which kind every column index is: a band is
        // written against the column it was read from, and a reader that wanted to print one as a name
        // rather than as a number has the data's own order to print it from.
        List<(string Id, Action<Utf8JsonWriter> Write)> entries =
        [
            ("kinds", Kinds),
        ];

        void Kinds(Utf8JsonWriter writer)
        {
            writer.WriteStartArray("columns");
            foreach (string column in matrix.Columns) writer.WriteStringValue(column);
            writer.WriteEndArray();
        }

        foreach (HostilityRow feelings in matrix.Rows)
        {
            // The kind is the row's own position, which is how the donor reads the matrix: a row's number is
            // the monster type it is about, and the header is only names. Matching by name would leave every
            // row whose spelling the header does not repeat — twenty-four of them, in the shipped file —
            // without a kind at all.
            int kind = row++;
            entries.Add((feelings.Kind, writer =>
            {
                writer.WriteNumber("kind", kind);

                // Only the bands the data states are written, against the column index they were read from,
                // and a band the file leaves out is friendly: the donor's own reader fills every relation
                // with friendly before it reads a cell
                // (OpenEnroth src/Engine/Tables/HostilityTable.cpp:17-18), so an omitted cell and a stated
                // zero are one fact in the data's own terms.
                writer.WriteStartObject("hostility");
                for (int column = 0; column < matrix.Columns.Count; column++)
                {
                    if (feelings.BandAt(column) is not { } band || band == 0) continue;
                    writer.WriteNumber(column.ToString(CultureInfo.InvariantCulture), band);
                }

                writer.WriteEndObject();
            }));
        }

        return WriteDocument(packDirectory, "hostility.json", "hostility", "hostility", entries);
    }

    private static int WriteItems(string packDirectory, Mm7Tables tables)
    {
        List<(string Id, Action<Utf8JsonWriter> Write)> entries = [];
        foreach (ItemRecord item in tables.Items.Items)
        {
            entries.Add((item.Id.ToString(CultureInfo.InvariantCulture), writer =>
            {
                writer.WriteString("name", item.Name);
                writer.WriteString("unidentifiedName", item.UnidentifiedName);
                writer.WriteNumber("value", item.Value);
                writer.WriteString("equipStat", item.EquipStat);
                writer.WriteString("skillGroup", item.SkillGroup);

                // The two tags the treasure rules compare, read from the table's own words here so nothing
                // downstream has to know how the shipped file spells an equipment column.
                writer.WriteString("type", ItemVocabulary.KindOf(item.EquipStat));
                writer.WriteString("skill", ItemVocabulary.SkillOf(item.SkillGroup));

                // What the item weighs at each treasure level, which is what a random item of that level
                // draws from. An item the shipped table does not weigh carries no weights at all rather than
                // six zeroes, so "never drawn" and "not stated" stay different facts.
                if (tables.RandomItems.Rows.FirstOrDefault(row => row.Id == item.Id) is { Id: > 0 } weighed)
                {
                    writer.WriteStartArray("lootWeights");
                    for (int level = 1; level <= RandomItemsTable.Levels; level++) writer.WriteNumberValue(weighed.ChanceAt(level));
                    writer.WriteEndArray();
                }

                writer.WriteString("damageDice", item.DamageDice);
                writer.WriteString("damageModifier", item.DamageModifier);

                // A book's, a scroll's, and a wand's row all state the spell they carry in the item table's
                // own damage column, as the letter S and the spell's global id: item 400 is the book of
                // "Torch Light" and carries S1, item 300 the scroll of the same spell, item 135 the Wand of
                // Fire with S2, and item 498 the book of "Souldrinker" with S99 (the shipped table's own
                // spelling, which the donor's three lookups turn into a spell id by position —
                // OpenEnroth src/Engine/Objects/ItemEnumFunctions.cpp:282-292, spellForSpellbook,
                // spellForScroll, and spellForWand, each over a table generated from the item table). That
                // spelling is a source-format quirk, so the join is written out here as a field of its own
                // rather than left for a reader to parse, and only for a row the spell table declares.
                if (ItemSpell(item, tables.Spells) is { } taught)
                {
                    writer.WriteString("spell", taught.ToString(CultureInfo.InvariantCulture));
                }

                writer.WriteString("material", item.Material);
                writer.WriteString("picture", item.Picture);
                if (tables.MessageScrolls.Texts.TryGetValue(item.Id, out string? text))
                {
                    writer.WriteString("readingText", text);
                    writer.WriteString("readingSource", tables.MessageScrolls.Table.Source.EntryName);
                }
                WriteOptionalNumber(writer, "spriteIndex", item.SpriteIndex == 0 ? null : item.SpriteIndex);
            }));
        }

        return WriteDocument(packDirectory, "items.json", "items", "item", entries);
    }

    /// <summary>
    /// Writes the shipped potion table: every reagent, the bottle, the catalyst, and every potion, each with
    /// what combining it with another row makes and what discovery that records.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The mixture matrix is written as an object keyed by the other row's id rather than as one entry per
    /// pair, because that is the shape the shipped table states it in and because a matrix of a thousand
    /// pairs would be a thousand documents' worth of rows for fifty rows of facts. A cell is the id of what
    /// the pair makes, the word <c>none</c> for a pair that does nothing, or <c>burst:n</c> for a pair that
    /// goes off at strength n, which is the donor's own reading of a cell
    /// (<c>OpenEnroth</c> <c>src/Engine/Tables/ItemTable.cpp:252-278</c>).
    /// </para>
    /// <para>
    /// <b>Two fields are ours and are written as ours.</b> <c>tier</c> is the rung of the mixing skill the row
    /// requires, which the shipped table does not carry and which the donor's four id bands state
    /// (<c>OpenEnroth</c> <c>src/GUI/UI/UIPopup.cpp:2092-2112</c>); and the catalyst's own mixtures are written
    /// out from the rule the donor applies before it reads the matrix at all
    /// (<c>src/GUI/UI/UIPopup.cpp:2075-2080</c>). Both are stated in
    /// [`docs/research/mm7-map-formats.md`](../../../docs/research/mm7-map-formats.md) so a reader of the pack
    /// knows which cells came from the table and which from its executable.
    /// </para>
    /// </remarks>
    private static int WritePotions(string packDirectory, Mm7Tables tables)
    {
        List<(string Id, Action<Utf8JsonWriter> Write)> entries = [];
        foreach (PotionRecord potion in tables.Potions.Rows)
        {
            entries.Add((potion.Id.ToString(CultureInfo.InvariantCulture), writer =>
            {
                writer.WriteString("name", potion.Name);
                writer.WriteString("description", potion.Description);
                writer.WriteString("effect", potion.Effect);
                writer.WriteString("kind", potion.Kind);

                // The row's own colour composition, which is what a potion's mixture of two potions is read
                // against in the game this approximates; a row that states none carries none.
                if (potion.Units.Any(unit => unit != 0))
                {
                    writer.WriteStartArray("units");
                    foreach (int unit in potion.Units) writer.WriteNumberValue(unit);
                    writer.WriteEndArray();
                }

                // The rung the row's own mixture requires. Ours: see the remarks above.
                writer.WriteNumber("tier", potion.Tier);
                WriteOptionalNumber(writer, "power", potion.Power == 0 ? null : potion.Power);

                if (potion.Mixtures.Count > 0)
                {
                    writer.WriteStartObject("mixtures");
                    foreach ((int other, string outcome) in potion.Mixtures.OrderBy(pair => pair.Key))
                    {
                        writer.WriteString(other.ToString(CultureInfo.InvariantCulture), outcome);
                    }

                    writer.WriteEndObject();
                }

                if (potion.Notes.Count > 0)
                {
                    writer.WriteStartObject("notes");
                    foreach ((int other, int note) in potion.Notes.OrderBy(pair => pair.Key))
                    {
                        writer.WriteNumber(other.ToString(CultureInfo.InvariantCulture), note);
                    }

                    writer.WriteEndObject();
                }
            }));
        }

        return WriteDocument(packDirectory, "potions.json", "potions", "potion", entries);
    }

    /// <summary>The spell an item's own reference column names, or null when the row carries none.</summary>
    /// <remarks>
    /// <para>
    /// The reference is the shipped table's spelling — the letter <c>S</c> and the spell's id — and it is read
    /// only for a row that is one of the three things the donor reads a spell for: a book, a spell scroll, or
    /// a wand. The three share the column and the spelling, which is why one join serves all of them and why
    /// the kind each is read as travels separately in the item's own <c>type</c> tag.
    /// </para>
    /// <para>
    /// A row whose reference names no spell the spell table declares is left without the field rather than
    /// given a number nothing answers.
    /// </para>
    /// </remarks>
    private static int? ItemSpell(ItemRecord item, SpellTable spells)
    {
        // The equipment words the shipped table uses for the three kinds that carry a spell, read through the
        // same vocabulary the pack's own kind tag comes from so the two cannot drift.
        string kind = ItemVocabulary.KindOf(item.EquipStat);
        if (!string.Equals(kind, ItemVocabulary.Book, StringComparison.Ordinal) &&
            !string.Equals(kind, ItemVocabulary.SpellScroll, StringComparison.Ordinal) &&
            !string.Equals(kind, ItemVocabulary.Wand, StringComparison.Ordinal))
        {
            return null;
        }

        string reference = item.DamageDice.Trim();
        if (reference.Length < 2 || !reference.StartsWith("S", StringComparison.OrdinalIgnoreCase)) return null;
        return int.TryParse(reference[1..], NumberStyles.None, CultureInfo.InvariantCulture, out int id) &&
            spells.Spells.Any(spell => spell.Id == id)
            ? id
            : null;
    }

    /// <summary>The definition kind a place's own map event is declared under.</summary>
    internal const string PlaceEventDefinitionKind = "place-event";

    /// <summary>The definition kind a row of the discovery table is declared under.</summary>
    internal const string DiscoveryDefinitionKind = "discovery";

    /// <summary>The definition kind a line of the history table is declared under.</summary>
    internal const string HistoryDefinitionKind = "history-line";

    /// <summary>
    /// Writes the map events the places' fixtures raise, and the timers that keep what those fixtures give,
    /// each with its normalized steps.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An entry's identity is the place and the event number, which is what a fixture placement names: a
    /// placement says which event its use raises and this document says what the event's steps are, once,
    /// however many faces raise it. Every step carries the field names <see cref="PlaceEventStep"/> states,
    /// and only the ones its instruction has, so a reader can tell a step whose operands were read from one
    /// whose were not.
    /// </para>
    /// <para>
    /// Nothing here says what a step does in play. The words name the donor's own instructions and variables,
    /// and the ruleset decides which of them it interprets and refuses the rest by name.
    /// </para>
    /// </remarks>
    private static int WritePlaceEvents(string packDirectory, PlaceFixtureSummary fixtures)
    {
        List<(string Id, Action<Utf8JsonWriter> Write)> entries = [];
        foreach (PlaceEvent placeEvent in fixtures.Events)
        {
            entries.Add((placeEvent.Id, writer =>
            {
                writer.WriteString("place", placeEvent.PlaceId.ToString(CultureInfo.InvariantCulture));
                writer.WriteNumber("event", placeEvent.EventId);
                WriteOptionalString(writer, "label", placeEvent.Label);
                writer.WriteBoolean("raised", placeEvent.Raised);
                if (placeEvent.Stepped) writer.WriteBoolean("stepped", true);
                if (placeEvent.Housed) writer.WriteBoolean("house", true);
                writer.WriteBoolean("timed", placeEvent.Triggered);
                writer.WriteString("mapFile", placeEvent.FileName);
                WriteSteps(writer, placeEvent.Steps);
            }));
        }

        return WriteDocument(packDirectory, "place-events.json", "place-events", PlaceEventDefinitionKind, entries);
    }

    /// <summary>The definition kind an event of the global program is declared under.</summary>
    internal const string GlobalEventDefinitionKind = "global-event";

    /// <summary>
    /// Writes the global program's events — what a person's topic runs — in the steps a place's events are written in.
    /// </summary>
    /// <remarks>
    /// An entry's identity is the event's number, which is also the number of the topic that raises it; whether a
    /// topic raises it is written beside it (<c>topic</c>), and every other event is carried too, because a topic
    /// change of any program can make a slot raise it later.
    /// </remarks>
    private static int WriteGlobalEvents(string packDirectory, GlobalEventSummary globals)
    {
        HashSet<int> raised = [.. globals.TopicRaised];
        List<(string Id, Action<Utf8JsonWriter> Write)> entries = [];
        foreach (GlobalEvent globalEvent in globals.Events)
        {
            entries.Add((globalEvent.Id, writer =>
            {
                writer.WriteNumber("event", globalEvent.EventId);
                writer.WriteBoolean("topic", raised.Contains(globalEvent.EventId));
                WriteSteps(writer, globalEvent.Steps);
            }));
        }

        return WriteDocument(packDirectory, "global-events.json", "global-events", GlobalEventDefinitionKind, entries);
    }

    /// <summary>Writes an event's normalized steps, each with only the fields its instruction carries.</summary>
    private static void WriteSteps(Utf8JsonWriter writer, IReadOnlyList<PlaceEventStep> steps)
    {
        writer.WriteStartArray("steps");
        foreach (PlaceEventStep step in steps)
        {
            writer.WriteStartObject();
            writer.WriteNumber("step", step.Step);
            writer.WriteString("op", step.Op);
            WriteOptionalNumber(writer, "opcode", step.Opcode);
            WriteOptionalString(writer, "variable", step.Variable);
            WriteOptionalString(writer, "which", step.Which);
            WriteOptionalNumber(writer, "index", step.Index);
            WriteOptionalNumber(writer, "code", step.Code);
            WriteOptionalNumber(writer, "value", step.Value);
            WriteOptionalNumber(writer, "target", step.Target);
            if (step.Targets is { } targets)
            {
                writer.WriteStartArray("targets");
                foreach (int target in targets) writer.WriteNumberValue(target);
                writer.WriteEndArray();
            }

            WriteOptionalNumber(writer, "textId", step.TextId);
            if (step.Text is { } text) writer.WriteString("text", text);
            WriteOptionalString(writer, "who", step.Who);
            WriteOptionalNumber(writer, "member", step.Member);
            WriteOptionalString(writer, "kind", step.Kind);
            WriteOptionalNumber(writer, "amount", step.Amount);
            WriteOptionalString(writer, "period", step.Period);
            WriteOptionalNumber(writer, "hour", step.Hour);
            WriteOptionalNumber(writer, "minute", step.Minute);
            WriteOptionalNumber(writer, "halfMinutes", step.HalfMinutes);
            WriteOptionalNumber(writer, "door", step.Door);
            WriteOptionalString(writer, "action", step.Action);
            WriteOptionalNumber(writer, "level", step.Level);
            WriteOptionalString(writer, "itemKind", step.ItemKind);
            WriteOptionalString(writer, "itemSkill", step.ItemSkill);
            WriteOptionalNumber(writer, "item", step.Item);
            WriteOptionalNumber(writer, "spell", step.Spell);
            WriteOptionalString(writer, "mastery", step.Mastery);
            WriteOptionalNumber(writer, "rank", step.Rank);
            WriteOptionalNumber(writer, "person", step.Person);
            WriteOptionalNumber(writer, "raises", step.Raises);
            WriteOptionalNumber(writer, "house", step.House);
            WriteOptionalNumber(writer, "greeting", step.Greeting);
            WriteOptionalNumber(writer, "group", step.Group);
            if (step.Name is { } name) writer.WriteString("name", name);
            WriteOptionalNumber(writer, "light", step.Light);
            WriteOptionalNumber(writer, "flag", step.Flag);
            if (step.On is { } on) writer.WriteBoolean("on", on);
            WriteOptionalString(writer, "link", step.Link);
            WriteOptionalNumber(writer, "toPlace", step.ToPlace);
            WriteOptionalString(writer, "travel", step.Travel);
            if (step.WithinPlace is true) writer.WriteBoolean("withinPlace", true);
            WriteOptionalNumber(writer, "x", step.X);
            WriteOptionalNumber(writer, "y", step.Y);
            WriteOptionalNumber(writer, "z", step.Z);
            WriteOptionalNumber(writer, "yaw", step.Yaw);
            WriteOptionalNumber(writer, "encounter", step.Encounter);
            WriteOptionalNumber(writer, "uniqueName", step.UniqueName);
            if (step.Summons is { } summons)
            {
                // The slot a summoning names, written as an encounter placement states its slot, so the ruleset
                // resolves the creatures through the one reading it resolves a spawn record's with.
                writer.WriteStartObject("summons");
                writer.WriteNumber("encounter", summons.Encounter);
                writer.WriteNumber("slot", summons.Slot);
                if (summons.FixedGrade is { } grade) writer.WriteString("grade", grade);
                writer.WriteString("monsterKind", summons.MonsterKind);
                writer.WriteNumber("difficulty", summons.Difficulty);
                writer.WriteNumber("appearMin", summons.AppearMin);
                writer.WriteNumber("appearMax", summons.AppearMax);
                writer.WriteStartArray("variants");
                foreach (PlaceEncounterVariant variant in summons.Variants)
                {
                    writer.WriteStartObject();
                    writer.WriteString("grade", variant.Grade);
                    writer.WriteNumber("monster", variant.MonsterId);
                    writer.WriteString("monsterName", variant.MonsterName);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    /// <summary>Writes the discovery table: every note a party can keep, by the number a map event sets.</summary>
    private static int WriteDiscoveries(string packDirectory, Mm7Tables tables)
    {
        List<(string Id, Action<Utf8JsonWriter> Write)> entries = [];
        foreach (DiscoveryRecord row in tables.Discoveries.Rows)
        {
            entries.Add((row.Number.ToString(CultureInfo.InvariantCulture), writer =>
            {
                writer.WriteString("text", row.Text);
                writer.WriteString("category", row.Category);
            }));
        }

        return WriteDocument(packDirectory, "discoveries.json", "discoveries", DiscoveryDefinitionKind, entries);
    }

    /// <summary>Writes the history table: every line a party's history book can hold, by the slot a map event writes.</summary>
    private static int WriteHistory(string packDirectory, Mm7Tables tables)
    {
        List<(string Id, Action<Utf8JsonWriter> Write)> entries = [];
        foreach (HistoryRecord row in tables.History.Rows)
        {
            entries.Add((row.Slot.ToString(CultureInfo.InvariantCulture), writer =>
            {
                writer.WriteString("text", row.Text);
                if (row.Title.Length > 0) writer.WriteString("title", row.Title);
            }));
        }

        return WriteDocument(packDirectory, "history.json", "history", HistoryDefinitionKind, entries);
    }

    private static int WriteQuests(string packDirectory, Mm7Tables tables)
    {
        List<(string Id, Action<Utf8JsonWriter> Write)> entries = [];
        foreach (QuestRecord quest in tables.Quests.Quests)
        {
            entries.Add((quest.Bit.ToString(CultureInfo.InvariantCulture), writer =>
            {
                writer.WriteString("text", quest.Text);
                WriteOptionalString(writer, "notes", quest.Notes);
                WriteOptionalString(writer, "owner", quest.Owner);
            }));
        }

        return WriteDocument(packDirectory, "quests.json", "quests", "quest", entries);
    }

    /// <summary>
    /// Writes the counters the building table describes, keyed by the building's own id.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every column written here is the table's, named after the column it came from, and the fields a row
    /// leaves empty are left out rather than filled with a number nobody stated: a temple has no stock
    /// interval and a house has no multiplier, and a reader that finds no field can say so.
    /// </para>
    /// <para>
    /// <b>What the definitions do not carry.</b> Which operations a counter offers, what its shelves hold,
    /// what it teaches, and what it charges are this game's answers about a kind, so the ruleset supplies
    /// them; the pack carries the operator's data and the provenance that says which row it came from.
    /// </para>
    /// </remarks>
    private static int WriteServices(string packDirectory, PlaceServiceSummary services)
    {
        List<(string Id, Action<Utf8JsonWriter> Write)> entries = [];
        foreach (PlaceServiceDefinition service in services.Services)
        {
            entries.Add((service.BuildingId.ToString(CultureInfo.InvariantCulture), writer =>
            {
                writer.WriteString("kind", service.Kind);
                writer.WriteString("name", service.Name);
                if (service.Proprietor.Length > 0) writer.WriteString("proprietor", service.Proprietor);
                if (service.Title.Length > 0) writer.WriteString("title", service.Title);
                writer.WriteNumber("mapId", service.MapId);
                writer.WriteString("place", service.PlaceId);
                WriteOptionalNumber(writer, "typeSequence", service.TypeSequence);
                WriteOptionalNumber(writer, "openHour", service.OpenHour);
                WriteOptionalNumber(writer, "closedHour", service.ClosedHour);
                WriteOptionalNumber(writer, "priceMultiplier", service.PriceMultiplier);
                WriteOptionalNumber(writer, "skillPriceMultiplier", service.SkillPriceMultiplier);
                WriteOptionalNumber(writer, "stockIntervalDays", service.StockIntervalDays);
                if (service.TrainingCap is int cap) writer.WriteNumber("trainingCap", cap);
                if (service.TrainingCapText.Length > 0) writer.WriteString("trainingCapText", service.TrainingCapText);
                writer.WriteString("source", "2DEvents.txt");
                writer.WriteNumber("sourceRow", service.SourceRow);
            }));
        }

        return WriteDocument(packDirectory, "services.json", "services", "service", entries);
    }

    /// <summary>
    /// Writes how a monster fights as named, typed fields: its two attacks, its spells, its resistances, what
    /// its blow leaves, and the hostility kind it belongs to.
    /// </summary>
    /// <remarks>
    /// Where each of these sits in the table's row and how each cell is spelled is this importer's knowledge,
    /// so the pack carries none of it: a reader gets a kind, dice, a chance, and a count, and a renamed field
    /// fails a test rather than a position shifting under a reader nobody told.
    /// </remarks>
    private static void WriteCombat(Utf8JsonWriter writer, MonsterCombatRecord combat)
    {
        writer.WriteNumber("hostilityKind", combat.HostilityKind);
        WriteAttack(writer, "attack", combat.Attack, chance: null);
        WriteAttack(writer, "secondAttack", combat.SecondAttack, combat.SecondAttackChance);
        WriteSpell(writer, "firstSpell", combat.FirstSpell);
        WriteSpell(writer, "secondSpell", combat.SecondSpell);

        writer.WriteStartObject("resistances");
        foreach ((string kind, int value) in combat.Resistances) writer.WriteNumber(kind, value);
        writer.WriteEndObject();
        writer.WriteStartArray("immunities");
        foreach (string kind in combat.Immunities) writer.WriteStringValue(kind);
        writer.WriteEndArray();

        if (combat.SpecialAttack is { } special)
        {
            writer.WriteStartObject("specialAttack");
            writer.WriteString("kind", special.Kind);
            if (special.Strength > 0) writer.WriteNumber("strength", special.Strength);
            writer.WriteNumber("times", special.Times);
            writer.WriteEndObject();
        }
    }

    private static void WriteAttack(Utf8JsonWriter writer, string name, MonsterAttackCell attack, int? chance)
    {
        writer.WriteStartObject(name);
        if (chance is { } percent) writer.WriteNumber("chance", percent);
        if (attack.Kind.Length > 0) writer.WriteString("kind", attack.Kind);
        if (attack.Dice is { } dice)
        {
            writer.WriteStartObject("dice");
            writer.WriteNumber("count", dice.Count);
            writer.WriteNumber("sides", dice.Sides);
            writer.WriteNumber("bonus", dice.Bonus);
            writer.WriteEndObject();
        }

        if (attack.Missile.Length > 0) writer.WriteString("missile", attack.Missile);
        writer.WriteEndObject();
    }

    private static void WriteSpell(Utf8JsonWriter writer, string name, MonsterSpellCell? spell)
    {
        if (spell is not { } cast) return;
        writer.WriteStartObject(name);
        writer.WriteNumber("chance", cast.Chance);
        writer.WriteString("name", cast.Name);
        writer.WriteString("mastery", cast.Mastery);
        writer.WriteNumber("skill", cast.Skill);
        writer.WriteEndObject();
    }
}
