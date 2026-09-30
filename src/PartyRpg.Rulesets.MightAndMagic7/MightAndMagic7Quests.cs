using System.Globalization;
using System.Text.Json;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Conversation;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Promotion;
using PartyRpg.Kit.Quests;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>What a town hall's bounty is worth this month: the beast, what the hall pays, and the errand.</summary>
/// <remarks>
/// The notice the counter posts and the errand the keeper offers are two readings of one fact, so they are
/// composed together here: a hall cannot advertise one beast and take a contract on another.
/// </remarks>
/// <param name="Beast">The monster row's own name, as the place's encounter row states it.</param>
/// <param name="Reward">What the hall pays for it, which the donor computes as a hundred times its level.</param>
/// <param name="Quest">The errand, or null when the hall posts a notice with no contract behind it.</param>
internal readonly record struct BountyTerms(string Beast, int Reward, QuestDefinition? Quest);

/// <summary>
/// This game's quests: the errands the shipped quest table states, and the deeds they ask for.
/// </summary>
/// <remarks>
/// <para>
/// <b>The shipped table states an errand's words; everything a player does about it is ours.</b> The
/// operator's <c>QUESTS.TXT</c> is 512 rows of a quest bit, the note the journal shows, and an authoring
/// column, and the 28 rows between bits 18 and 55 are the promotion errands this game's ranks ask for
/// (<c>docs/research/mm7-data-inventory.md</c>, <i>Quests</i>; the bits are the ones
/// <see cref="MightAndMagic7Promotions"/> names). What no row anywhere states is who gives an errand, what
/// it asks the party to do, what it pays, or what finishing it leaves — the original keeps all of that in
/// its map event programs, which this build does not run. Those four things are therefore authored here,
/// row by row, over the shipped words, which are carried as the errand's own note.
/// </para>
/// <para>
/// <b>An errand's giver is its rank's giver, not a second table.</b> Every one of the 17 promotion errands is
/// turned in to the person the shipped topic table names as the rank's giver — bit 35 to Frederick Org
/// (<c>npc-43</c>), bit 19 to William Lasker (<c>npc-15</c>), bit 48 to Halfgild Wynac (<c>npc-49</c>), and
/// so on — so the giver is read from the ladder rather than written again here. One fact, one table.
/// </para>
/// <para>
/// <b>What an errand asks is the strongest thing this build can judge, and what it cannot judge is stated
/// rather than faked.</b> A shipped errand names a place — "Castle Navan", "Watchtower 6", "the three
/// stonehenge monoliths in Tatalia, the Evenmorn Islands, and Avlee" — and the world knows that place, so
/// standing in it is what the objective reads. Where the errand's words name a creature, the objective
/// counts its deaths; where they say <em>all</em> of a kind, the count is every one the place's own
/// placements hold. What no owner reports — a weight moved, a code cracked, an altar defaced — is stated as
/// the errand's residue, so a player reads what the original asked for beside what this game judges, and no
/// objective is invented that nothing could ever satisfy.
/// </para>
/// <para>
/// <b>A finished errand leaves the record the conversation already reads.</b> The topic table's own
/// requirement column gates a person's topic on a quest bit (<c>npctopic</c>'s <c>requires</c>), which this
/// game reads as the party-carried flag <c>errand:&lt;bit&gt;</c>; a turn-in writes exactly that record, so
/// the gate a topic waits on and the record an errand leaves are one identity rather than two.
/// </para>
/// <para>
/// <b>The town hall's board is the one errand nobody has to author.</b> The donor draws a huntable beast
/// from the place's own encounter row and pays a hundred times its level, refreshing monthly
/// (OpenEnroth <c>src/GUI/UI/Houses/TownHall.cpp:135-176</c>). Nothing is drawn here: the beast is chosen by
/// the month the clock stands in, so the same month at the same hall is the same contract, and the errand
/// the keeper offers is resolved from its own identity — <c>bounty:&lt;placement&gt;:&lt;year&gt;-&lt;month&gt;</c> —
/// rather than kept anywhere.
/// </para>
/// </remarks>
internal sealed class MightAndMagic7Quests : IQuestRule
{
    /// <summary>The definition kind a shipped quest row is declared under.</summary>
    internal const string QuestDefinitionKind = "quest";


    /// <summary>The field a placement names the monster row it stands for under.</summary>
    internal const string MonsterField = "monster";




    private readonly TuningProfile _tuning;
    private readonly Dictionary<string, QuestDefinition> _byId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<QuestDefinition>> _byGiver = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _placesByName;
    private readonly Dictionary<string, string> _placeOfPlacement;
    private readonly Dictionary<string, IReadOnlyList<string>> _encountersOfPlace;
    private readonly Dictionary<string, Dictionary<string, int>> _placedOfPlace;
    private readonly Dictionary<string, string> _monstersByName;
    private readonly Dictionary<string, int> _monsterLevels;
    private readonly Dictionary<string, string> _itemsByName;
    private readonly IReadOnlyList<string> _notes;

    private MightAndMagic7Quests(
        IReadOnlyList<string> notes,
        Dictionary<string, string> placesByName,
        Dictionary<string, string> placeOfPlacement,
        Dictionary<string, IReadOnlyList<string>> encounters,
        Dictionary<string, Dictionary<string, int>> placed,
        Dictionary<string, string> monstersByName,
        Dictionary<string, int> monsterLevels,
        Dictionary<string, string> itemsByName,
        TuningProfile tuning)
    {
        _tuning = tuning;
        _notes = notes;
        _placesByName = placesByName;
        _placeOfPlacement = placeOfPlacement;
        _encountersOfPlace = encounters;
        _placedOfPlace = placed;
        _monstersByName = monstersByName;
        _monsterLevels = monsterLevels;
        _itemsByName = itemsByName;
    }

    /// <inheritdoc />
    public IReadOnlyList<QuestDefinition> Definitions => [.. _byId.Values];

    /// <summary>What reading the shipped table and the authored rows noticed, for a report.</summary>
    internal IReadOnlyList<string> Notes => _notes;

    /// <summary>How many errands this game states over the shipped quest bits.</summary>
    internal int ErrandCount { get; private set; }

    /// <summary>How many people the errands are turned in to.</summary>
    internal int ErrandGiverCount { get; private set; }

    /// <summary>The bits this game states an errand for, in the shipped table's own order.</summary>
    internal IReadOnlyList<string> ErrandBits { get; private set; } = [];

    /// <summary>Reads this game's quests over the content the product loaded.</summary>
    /// <remarks>
    /// The shipped words are read from the quest document the importer writes, and a bit the pack does not
    /// carry is reported rather than stated without its note: an errand whose own words are missing would
    /// leave a player with an objective and no story.
    /// </remarks>
    /// <param name="catalog">The validated content, or null when no bundle supplied any.</param>
    /// <param name="promotions">This game's ranks, which name every errand's giver and its shipped bit.</param>
    /// <returns>This game's quests, or null when no content was loaded.</returns>
    internal static MightAndMagic7Quests? Read(ContentCatalog? catalog, MightAndMagic7Promotions? promotions)
    {
        if (catalog is null) return null;
        List<string> notes = [];
        List<(ContentEntry Entry, Action<string, string> Defect)> authored = [];
        Dictionary<string, string> words = [];
        List<ContentValidationIssue> issues = [];
        foreach ((LoadedPack pack, ContentDocument document, ContentEntry entry) in catalog.Entries(QuestDefinitionKind))
        {
            string text = entry.GetString("text");
            if (text.Length > 0) words[entry.Id] = text;

            // A pack may state an errand of its own, and everything about it: what it asks, what it pays, who
            // gives it, and what a turn-in leaves. A shipped row carries only words, so a row without a
            // reading is a note rather than an errand, and the errands this game compiles over the promotion
            // bits are stated below.
            void Defect(string code, string message) =>
                issues.Add(new ContentValidationIssue(code, message, pack.PackId, document.DocumentId));
            authored.Add((entry, Defect));
        }

        Dictionary<string, string> placesByName = [];
        Dictionary<string, string> placeOfPlacement = [];
        Dictionary<string, IReadOnlyList<string>> encounters = [];
        Dictionary<string, Dictionary<string, int>> placed = [];
        foreach ((LoadedPack pack, ContentDocument document, ContentEntry entry) in catalog.Entries(PlaceGraphLoader.PlaceDefinitionKind))
        {
            string name = entry.GetString("name");
            if (name.Length > 0) placesByName[name] = entry.Id;
            foreach (JsonElement placement in entry.GetArray(PlacePopulationContent.PlacementsField))
            {
                string id = ContentEntry.ReadId(placement, PlacePopulationContent.IdField);
                if (id.Length > 0) placeOfPlacement[id] = entry.Id;
                if (!string.Equals(ContentEntry.ReadString(placement, PlacePopulationContent.KindField), "monster", StringComparison.Ordinal)) continue;
                string row = ContentEntry.ReadId(placement, MonsterField);
                if (row.Length == 0) continue;
                if (!placed.TryGetValue(entry.Id, out Dictionary<string, int>? byRow)) placed[entry.Id] = byRow = new Dictionary<string, int>(StringComparer.Ordinal);
                byRow[row] = byRow.GetValueOrDefault(row) + 1;
            }

            List<string> slots = [];
            foreach (JsonElement slot in entry.GetArray("monsters"))
            {
                if (slot.ValueKind == JsonValueKind.String && slot.GetString() is { Length: > 0 } beast) slots.Add(beast);
            }

            if (slots.Count > 0) encounters[entry.Id] = slots;
        }

        Dictionary<string, string> itemsByName = new(StringComparer.Ordinal);
        foreach ((_, _, ContentEntry entry) in catalog.Entries(MightAndMagic7Containers.ItemDefinitionKind))
        {
            string name = entry.GetString("name");
            if (name.Length > 0) itemsByName[name] = entry.Id;
        }

        Dictionary<string, string> monstersByName = new(StringComparer.Ordinal);
        Dictionary<string, int> levels = new(StringComparer.Ordinal);
        foreach ((_, _, ContentEntry entry) in catalog.Entries(MightAndMagic7Combat.MonsterDefinitionKind))
        {
            string name = entry.GetString("name");
            if (name.Length == 0) continue;
            monstersByName[name] = entry.Id;
            levels[name] = Math.Max(1, entry.GetInt32("level") ?? 1);
        }

        MightAndMagic7Quests quests = new(notes, placesByName, placeOfPlacement, encounters, placed, monstersByName, levels, itemsByName, MightAndMagic7Tuning.Read(catalog));
        quests.ReadAuthored(authored, words, issues);
        if (issues.Count > 0)
        {
            throw new ContentValidationException(
                $"This game's quests cannot be read: {issues[0].Message}",
                issues);
        }

        List<string> bits = [];
        foreach (ErrandReading reading in Errands())
        {
            string bit = reading.Bit.ToString(CultureInfo.InvariantCulture);
            PromotionRank? rank = promotions?.Ladder.Ranks.FirstOrDefault(
                candidate => BitsOf(candidate).Contains(bit, StringComparer.Ordinal));
            if (rank is null)
            {
                notes.Add($"the shipped errand {bit} belongs to no rank this game states, so it is not offered by anybody.");
                continue;
            }

            List<QuestObjective> objectives = [];
            foreach (ErrandObjective stated in reading.Objectives)
            {
                if (string.Equals(stated.Aim, "reach", StringComparison.Ordinal))
                {
                    if (!placesByName.TryGetValue(stated.Target, out string? place))
                    {
                        notes.Add($"the errand {bit} asks for '{stated.Target}', which no place this world carries is called, so that objective is not stated.");
                        continue;
                    }

                    objectives.Add(new QuestObjective(
                        $"reach-{objectives.Count.ToString(CultureInfo.InvariantCulture)}",
                        QuestObjectiveKind.Reach,
                        place,
                        label: $"Reach {stated.Target}"));
                    continue;
                }

                if (!placesByName.TryGetValue(stated.Place, out string? where))
                {
                    notes.Add($"the errand {bit} counts '{stated.Target}' in '{stated.Place}', which no place this world carries is called, so that objective is not stated.");
                    continue;
                }

                if (!monstersByName.TryGetValue(stated.Target, out string? row))
                {
                    notes.Add($"the errand {bit} counts '{stated.Target}', which the monster table does not carry, so that objective is not stated.");
                    continue;
                }

                // A count of zero means "every one the place holds", which is what the shipped words say when
                // they say all of a kind: the number is read from the place's own placements rather than
                // written here, so the errand and the world cannot disagree about how many there are.
                int count = stated.Count > 0
                    ? stated.Count
                    : placed.GetValueOrDefault(where, []).GetValueOrDefault(row);
                if (count < 1)
                {
                    notes.Add($"the errand {bit} asks for every '{stated.Target}' in '{stated.Place}' and that place holds none, so that objective is not stated.");
                    continue;
                }

                objectives.Add(new QuestObjective(
                    $"kill-{objectives.Count.ToString(CultureInfo.InvariantCulture)}",
                    QuestObjectiveKind.Kill,
                    row,
                    count,
                    stated.Count > 0
                        ? $"Bring down {count.ToString(CultureInfo.InvariantCulture)} × {stated.Target}"
                        : $"Bring down every {stated.Target} in {stated.Place}",
                    place: where));
            }

            if (objectives.Count == 0)
            {
                notes.Add($"the errand {bit} cannot be stated: nothing it asks for is something this build can read.");
                continue;
            }

            string note = words.TryGetValue(bit, out string? shipped) ? shipped : string.Empty;
            if (note.Length == 0)
            {
                notes.Add($"the shipped quest table carries no words for bit {bit}, so the errand is stated with the objectives this game reads and no story.");
            }

            QuestDefinition definition = new(
                new QuestId(bit),
                reading.Name,
                rank.Giver,
                objectives,
                new QuestRewards(quests._tuning.Whole(MightAndMagic7Tuning.ErrandExperience), quests._tuning.Whole(MightAndMagic7Tuning.ErrandCoins)),
                record: ErrandRecord(bit),
                note: note,
                residue: reading.Residue);

            quests._byId[definition.Id.Value] = definition;
            if (!quests._byGiver.TryGetValue(rank.Giver, out List<QuestDefinition>? given)) quests._byGiver[rank.Giver] = given = [];
            given.Add(definition);
            bits.Add(bit);
        }

        quests.ErrandCount = bits.Count;
        quests.ErrandGiverCount = quests._byGiver.Count;
        quests.ErrandBits = bits;
        notes.Add(string.Create(
            CultureInfo.InvariantCulture,
            $"The shipped quest table carries {words.Count} rows with words; {bits.Count} of them are the promotion errands this game states, over {quests._byGiver.Count} people who give them."));
        return quests;
    }

    /// <summary>
    /// Reads every errand a pack states for itself, with everything about it written down.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A shipped row is words: the operator's table states what the journal shows and nothing about what the
    /// errand asks or pays. A pack that wants an errand of its own states a <c>reading</c> beside its words,
    /// and this reads that reading: the giver, what each objective asks for by the name content carries it
    /// under, the conditions that gate the offer and the completion, and what a turn-in pays.
    /// </para>
    /// <para>
    /// <b>Everything is resolved against the same content the world and the monster table are.</b> A place is
    /// named as content names it, a creature by its row's name, and an item by its definition's name, so a
    /// pack cannot state an errand about something the game does not carry; a reading that names one is a
    /// defect the load names rather than an objective nothing could ever satisfy.
    /// </para>
    /// </remarks>
    private void ReadAuthored(
        List<(ContentEntry Entry, Action<string, string> Defect)> authored,
        Dictionary<string, string> words,
        List<ContentValidationIssue> issues)
    {
        foreach ((ContentEntry entry, Action<string, string> defect) in authored)
        {
            if (!entry.Has("reading")) continue;
            if (_byId.ContainsKey(entry.Id))
            {
                defect("quest-identity-reused", $"quest '{entry.Id}' is declared more than once, so which errand a party took would be ambiguous.");
                continue;
            }

            string giver = ReadingString(entry, "giver");
            if (giver.Length == 0)
            {
                defect("quest-giver-missing", $"quest '{entry.Id}' states a reading without a giver, so nobody could offer or receive it.");
                continue;
            }

            List<QuestObjective> objectives = [];
            bool sound = true;
            foreach (JsonElement stated in Reading(entry, "objectives"))
            {
                string kind = ContentEntry.ReadString(stated, "kind");
                string target = ContentEntry.ReadId(stated, "target");
                string label = ContentEntry.ReadString(stated, "label");
                int count = Math.Max(1, (int)(ContentEntry.ReadDouble(stated, "count") ?? 1));
                string place = ContentEntry.ReadString(stated, "place");
                string person = ContentEntry.ReadId(stated, "person");
                string id = ContentEntry.ReadId(stated, "id");

                switch (kind)
                {
                    case "kill":
                    {
                        if (!_monstersByName.TryGetValue(target, out string? row))
                        {
                            defect("quest-objective-unknown-creature", $"quest '{entry.Id}' counts '{target}', which the monster table does not carry.");
                            sound = false;
                            break;
                        }

                        string where = string.Empty;
                        if (place.Length > 0)
                        {
                            if (!_placesByName.TryGetValue(place, out string? found))
                            {
                                defect("quest-objective-unknown-place", $"quest '{entry.Id}' counts '{target}' in '{place}', which no place this world carries is called.");
                                sound = false;
                                break;
                            }

                            where = found;
                        }

                        objectives.Add(new QuestObjective(
                            id,
                            QuestObjectiveKind.Kill,
                            row,
                            count,
                            label,
                            place: where));
                        break;
                    }

                    case "retrieve":
                    case "deliver":
                    {
                        if (!_itemsByName.TryGetValue(target, out string? item))
                        {
                            defect("quest-objective-unknown-item", $"quest '{entry.Id}' asks for '{target}', which the item table does not carry.");
                            sound = false;
                            break;
                        }

                        string to = string.Empty;
                        if (string.Equals(kind, "deliver", StringComparison.Ordinal))
                        {
                            if (person.Length == 0)
                            {
                                defect("quest-delivery-unnamed", $"quest '{entry.Id}' delivers '{target}' to nobody, so whether it was carried there could never be judged.");
                                sound = false;
                                break;
                            }

                            to = PersonMet(person);
                        }

                        objectives.Add(new QuestObjective(
                            id,
                            string.Equals(kind, "retrieve", StringComparison.Ordinal) ? QuestObjectiveKind.Retrieve : QuestObjectiveKind.Deliver,
                            item,
                            count,
                            label,
                            person: to));
                        break;
                    }

                    case "reach":
                    {
                        if (!_placesByName.TryGetValue(target, out string? placeId))
                        {
                            defect("quest-objective-unknown-place", $"quest '{entry.Id}' asks for '{target}', which no place this world carries is called.");
                            sound = false;
                            break;
                        }

                        objectives.Add(new QuestObjective(id, QuestObjectiveKind.Reach, placeId, count, label));
                        break;
                    }

                    case "talk":
                        objectives.Add(new QuestObjective(id, QuestObjectiveKind.Talk, PersonMet(target), count, label));
                        break;

                    case "flag":
                        objectives.Add(new QuestObjective(id, QuestObjectiveKind.Flag, target, count, label));
                        break;

                    default:
                        defect("quest-objective-unknown-kind", $"quest '{entry.Id}' states an objective of kind '{kind}', which is not one this build reads.");
                        sound = false;
                        break;
                }
            }

            if (!sound) continue;
            if (objectives.Count == 0)
            {
                defect("quest-objectives-missing", $"quest '{entry.Id}' asks for nothing, so a party could never finish it.");
                continue;
            }

            QuestDefinition definition = new(
                new QuestId(entry.Id),
                entry.GetString("name") is { Length: > 0 } name ? name : entry.Id,
                giver,
                objectives,
                Rewards(entry, defect, out bool paid),
                Conditions(entry, "offer"),
                Conditions(entry, "completion"),
                ReadingString(entry, "record") is { Length: > 0 } record ? record : ErrandRecord(entry.Id),
                entry.GetString("note") is { Length: > 0 } note ? note : words.GetValueOrDefault(entry.Id, string.Empty),
                ReadingString(entry, "residue"));
            if (!paid) continue;

            _byId[entry.Id] = definition;
            if (!_byGiver.TryGetValue(giver, out List<QuestDefinition>? given)) _byGiver[giver] = given = [];
            given.Add(definition);
        }
    }

    /// <summary>The record the conversation leaves of meeting somebody, which a talk or delivery reads.</summary>
    private static string PersonMet(string person) =>
        person.StartsWith(MightAndMagic7Identities.MetFlagPrefix, StringComparison.Ordinal)
            ? person
            : $"{MightAndMagic7Identities.MetFlagPrefix}{person}";

    /// <summary>One string field of an entry's own reading, or empty when it states none.</summary>
    private static string ReadingString(ContentEntry entry, string field) =>
        entry.Payload.ValueKind == JsonValueKind.Object &&
        entry.Payload.TryGetProperty("reading", out JsonElement reading) &&
        reading.ValueKind == JsonValueKind.Object &&
        reading.TryGetProperty(field, out JsonElement value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    /// <summary>One reading's own array field, as the array an element walk uses.</summary>
    private static IReadOnlyList<JsonElement> Reading(ContentEntry entry, string field) =>
        entry.Payload.ValueKind == JsonValueKind.Object &&
        entry.Payload.TryGetProperty("reading", out JsonElement reading) &&
        reading.ValueKind == JsonValueKind.Object &&
        reading.TryGetProperty(field, out JsonElement value) &&
        value.ValueKind == JsonValueKind.Array
            ? [.. value.EnumerateArray()]
            : [];

    /// <summary>What an authored reading pays, or the defect that stopped it being read.</summary>
    private static QuestRewards Rewards(ContentEntry entry, Action<string, string> defect, out bool paid)
    {
        paid = true;
        List<QuestRewardItem> items = [];
        foreach (JsonElement stated in Reading(entry, "items"))
        {
            string item = ContentEntry.ReadId(stated, "item");
            items.Add(new QuestRewardItem(item, Math.Max(1, (int)(ContentEntry.ReadDouble(stated, "count") ?? 1))));
        }

        List<QuestRewardRecord> records = [];
        foreach (JsonElement stated in Reading(entry, "records"))
        {
            string record = ContentEntry.ReadId(stated, "record");
            if (record.Length == 0)
            {
                defect("quest-reward-unnamed", $"quest '{entry.Id}' pays a record that names nothing.");
                paid = false;
                continue;
            }

            records.Add(new QuestRewardRecord(record, Math.Max(1, (int)(ContentEntry.ReadDouble(stated, "amount") ?? 1))));
        }

        return new QuestRewards((long)ReadingNumber(entry, "experience"), (int)ReadingNumber(entry, "coins"), items, records);
    }

    /// <summary>One number field of an entry's own reading, or zero when it states none.</summary>
    private static double ReadingNumber(ContentEntry entry, string field) =>
        entry.Payload.ValueKind == JsonValueKind.Object &&
        entry.Payload.TryGetProperty("reading", out JsonElement reading) &&
        reading.ValueKind == JsonValueKind.Object &&
        reading.TryGetProperty(field, out JsonElement value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetDouble(out double number)
            ? number
            : 0;

    /// <summary>The conditions an authored reading states, as the conversation's own vocabulary reads them.</summary>
    private static IReadOnlyList<ConversationCondition> Conditions(ContentEntry entry, string field)
    {
        List<ConversationCondition> conditions = [];
        foreach (JsonElement stated in Reading(entry, field))
        {
            string kind = ContentEntry.ReadString(stated, "kind");
            string name = ContentEntry.ReadId(stated, "name");
            if (name.Length == 0 || MightAndMagic7Conversation.ConditionKind(kind) is not { } parsed) continue;
            conditions.Add(new ConversationCondition(
                parsed,
                parsed == ConversationConditionKind.Errand ? MightAndMagic7Quests.ErrandRecord(name) : name,
                Math.Max(1, (int)(ContentEntry.ReadDouble(stated, "amount") ?? 1)),
                ContentEntry.ReadString(stated, "label")));
        }

        return conditions;
    }

    /// <summary>The party-carried record a finished errand leaves, as the conversation's own gate reads it.</summary>
    /// <remarks>
    /// The prefix is the conversation's, because that is where the requirement was already written down: a
    /// topic the shipped table gates on a quest bit waits for exactly this record, so the errand and the
    /// topic cannot disagree about what finishing it means.
    /// </remarks>
    /// <param name="bit">The shipped quest bit, as the table states it.</param>
    /// <returns>The record's identity.</returns>
    internal static string ErrandRecord(string bit) => $"{MightAndMagic7Identities.ErrandFlagPrefix}{bit}";

    /// <inheritdoc />
    public QuestDefinition? Definition(QuestId quest)
    {
        if (_byId.TryGetValue(quest.Value, out QuestDefinition? stated)) return stated;
        return BountyDefinition(quest.Value)?.Quest;
    }

    /// <summary>Every errand one person gives, in the order this game states them.</summary>
    /// <param name="person">The person's identity among the people the world carries.</param>
    /// <returns>The errands they offer, which is empty for everybody who offers none.</returns>
    internal IReadOnlyList<QuestDefinition> GivenBy(string person) =>
        person.Length > 0 && _byGiver.TryGetValue(person, out List<QuestDefinition>? given) ? given : [];

    /// <inheritdoc />
    /// <remarks>
    /// A kill objective names the monster table's own row, which the reading resolved from the name the row
    /// states, and the death is read against the placement's own <c>monster</c> field: the row a creature
    /// stands for is content's fact, so matching them is a comparison of identities rather than of names.
    /// </remarks>
    public int Counts(QuestKillRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Objective.Kind != QuestObjectiveKind.Kill) return 0;
        string row = request.Body.Source.GetId(MonsterField);
        return row.Length > 0 && string.Equals(row, request.Objective.Target, StringComparison.Ordinal) ? 1 : 0;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Every kind is read from something the world actually carries — a party-carried flag, the party's
    /// standing, a member's class or race, the one clock, and an errand's own record — which is the same
    /// reading the conversation makes of a topic's conditions, so a quest and a topic that wait for the same
    /// thing cannot disagree about whether it holds.
    /// </remarks>
    public bool Holds(QuestConditionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return MightAndMagic7Conversation.Holds(request.Condition, request.Party, request.Clock);
    }

    /// <summary>What a town hall posts this month for the place its counter stands in, or null when none does.</summary>
    /// <param name="place">The place the hall stands in.</param>
    /// <param name="now">The date the clock stands at, which decides which beast the month's bounty is on.</param>
    /// <returns>The beast and what the hall pays, or null when the place's encounter row names none.</returns>
    internal BountyTerms? Bounty(PlaceId place, GameDate now)
    {
        if (!_encountersOfPlace.TryGetValue(place.Value, out IReadOnlyList<string>? beasts) || beasts.Count == 0) return null;
        string beast = beasts[(Math.Max(1, now.Month) - 1) % beasts.Count];
        int reward = _tuning.Whole(MightAndMagic7Tuning.BountyPerLevel) * _monsterLevels.GetValueOrDefault(beast, 1);
        return new BountyTerms(beast, reward, null);
    }

    /// <summary>The errand a hall's keeper offers this month, as its own identity states it.</summary>
    /// <remarks>
    /// The identity carries everything the errand needs and nothing else: the counter's placement, which is
    /// the keeper the errand is taken from and the place its encounter row is read from, and the year and
    /// month, which decide the beast and what it pays. Nothing is kept anywhere, so a save that records the
    /// identity can resolve the same errand after a month has turned.
    /// </remarks>
    /// <param name="placement">The counter's placement identity, as the world carries it.</param>
    /// <param name="now">The date the party is asking on.</param>
    /// <returns>The errand's identity, or empty when the counter's place posts no bounty.</returns>
    internal string BountyQuest(string placement, GameDate now)
    {
        if (placement.Length == 0) return string.Empty;
        if (!_placeOfPlacement.TryGetValue(placement, out string? place)) return string.Empty;
        if (Bounty(new PlaceId(place), now) is null) return string.Empty;
        return new BountyIdentity(placement, now.Year, now.Month).Value;
    }

    /// <summary>The errand a town hall's bounty identity names, or null when it names none.</summary>
    private BountyTerms? BountyDefinition(string quest)
    {
        if (BountyIdentity.Read(quest) is not { } bounty) return null;
        string placement = bounty.Placement;
        if (!_placeOfPlacement.TryGetValue(placement, out string? place)) return null;
        if (Bounty(new PlaceId(place), new GameDate(bounty.Year, bounty.Month, 1)) is not { } terms) return null;

        // A bounty whose beast the monster table does not carry cannot be counted, and an errand nobody could
        // ever finish is not stated at all — the hall posts a notice with nothing behind it, which is the
        // honest state of a place whose encounter row names something no row describes.
        if (!_monstersByName.TryGetValue(terms.Beast, out string? row)) return terms;

        QuestDefinition definition = new(
            new QuestId(quest),
            $"{terms.Beast} bounty",
            $"{MightAndMagic7Identities.KeeperIdPrefix}{placement}",
            [
                new QuestObjective(
                    "kill-0",
                    QuestObjectiveKind.Kill,
                    row,
                    1,
                    $"Bring down a {terms.Beast}"),
            ],
            new QuestRewards(coins: terms.Reward),
            // A hall posts its notice for a party the town regards well enough to know, which is this game's
            // own reading: the donor's town halls hand their bounty to anybody who asks
            // (<c>src/GUI/UI/Houses/TownHall.cpp:135-176</c>), and nothing there reads a standing. The
            // threshold is the band this game's standing table opens at, so what a person will say about the
            // party and what a hall will put its way open together. It is stated as an ordinary offer
            // condition, which is the same vocabulary the conversation judges a topic's availability in, so
            // the notice's own topic disappears and comes back with the standing rather than being special
            // cased at either end.
            offerConditions:
            [
                new ConversationCondition(
                    ConversationConditionKind.Reputation,
                    "reputation",
                    MightAndMagic7Standing.WellRegarded,
                    "the party's standing"),
            ],
            note: string.Create(
                CultureInfo.InvariantCulture,
                $"This month's bounty is on a {terms.Beast}: the hall pays {terms.Reward} coin(s) for proof of the kill."),
            residue: "where the beast is brought down is nobody's business but the hall's, so the errand counts the kill wherever it happens");

        return terms with { Quest = definition };
    }

    /// <summary>The shipped bits one rank's errand names, which is what ties an errand row to its giver.</summary>
    private static IReadOnlyList<string> BitsOf(PromotionRank rank)
    {
        List<string> bits = [];
        foreach (PromotionRequirement requirement in rank.Requirements)
        {
            if (requirement.Kind == PromotionRequirementKind.Award &&
                requirement.Name.StartsWith(MightAndMagic7Identities.ErrandFlagPrefix, StringComparison.Ordinal))
            {
                bits.Add(requirement.Name[MightAndMagic7Identities.ErrandFlagPrefix.Length..]);
            }
        }

        return bits;
    }

    /// <summary>One authored errand objective, before the identities it names are resolved against content.</summary>
    /// <param name="Aim">What kind of thing it is: <c>reach</c> for a place, <c>kill</c> for a creature.</param>
    /// <param name="Target">The place's or the creature's own name, as the shipped errand and content state it.</param>
    /// <param name="Place">For a kill, the place it must happen in.</param>
    /// <param name="Count">How many, or zero for every one the place's own placements hold.</param>
    private readonly record struct ErrandObjective(string Aim, string Target, string Place = "", int Count = 0);

    /// <summary>One authored errand: the shipped bit it states, and what this game reads it as asking.</summary>
    /// <param name="Bit">The shipped quest table's own bit.</param>
    /// <param name="Name">What this game calls the errand.</param>
    /// <param name="Objectives">What it asks, over names content carries.</param>
    /// <param name="Residue">What the shipped words ask for that this game does not judge, or empty.</param>
    private readonly record struct ErrandReading(int Bit, string Name, ErrandObjective[] Objectives, string Residue = "");

    /// <summary>
    /// Every errand this game states, in the shipped quest table's bit order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The bits, the errands, and the givers are the shipped table's and the shipped topic table's; the
    /// names and what each row asks for are ours, and every place and creature named here is content's own
    /// name for it, resolved when the packs are read. The rows are ordered by bit, which is the order the
    /// operator's table states them in, so a report of this table reads in one order rather than two.
    /// </para>
    /// <para>
    /// <b>The residue is where the original's own deed is not this build's.</b> "Move the weight from the top
    /// of the tower to the bottom", "crack the code", "purify the altar", "sabotage the lift", "rescue
    /// Alice": each is an event program acting on a map this build does not run, so the errand states the
    /// journey its words name — the place the deed happens in — and names what it cannot judge. Nothing here
    /// invents an objective nothing could satisfy.
    /// </para>
    /// </remarks>
    private static ErrandReading[] Errands() =>
    [
        // Thief → Rogue (npc-15, William Lasker). The vase half is the item the same rank asks for, so the
        // errand states the journey the shipped words begin with and leaves the treasure to the rank.
        new(18, "The vase of Lord Markham's Manor",
            [new("reach", "Lord Markham's Manor")],
            "the vase itself belongs to the promotion this errand is for, which asks the party to carry it."),

        // Rogue → Spy (npc-15). "Move the weight from the top of Watchtower 6 to the bottom of the tower."
        new(19, "The weight in Watchtower 6",
            [new("reach", "Watchtower 6")],
            "the weight is moved by the watchtower's own event program, which this build does not run: the errand is judged by standing in the tower the shipped words name."),

        // Rogue → Assassin (npc-16). The trinket half is the item the rank asks for.
        new(21, "Silence Lady Carmine",
            [new("reach", "Celeste")],
            "the deed itself is the rank's own proof item, which the promotion asks the party to bring back."),

        // Paladin → Crusader (npc-17). A named dragon no placement carries.
        new(22, "Wromthrax the Heartless",
            [new("reach", "Tatalia")],
            "Wromthrax is placed by the original's own map records rather than by a spawn record this build places, so the errand is judged by the region the shipped words name."),

        // Crusader → Hero (npc-17). "Rescue Alice Hargreaves from William's Tower."
        new(24, "Alice Hargreaves in William's Tower",
            [new("reach", "William Setag's Tower")],
            "rescuing Alice is the tower's own event program, which this build does not run: the errand is judged by standing in the tower the shipped words name."),

        // Crusader → Villain (npc-18). "Capture Alice Hargreaves … and return her to William's Tower."
        new(26, "Alice Hargreaves, taken and delivered",
            [new("reach", "Castle Gryphonheart"), new("reach", "William Setag's Tower")],
            "the capture and the delivery are the castle's and the tower's own event programs, which this build does not run: the errand is judged by standing where the shipped words say."),

        // Monk → Initiate (npc-38). "Find the lost meditation spot in the Dwarven Barrows."
        new(27, "The meditation spot",
            [new("reach", "The Barrow Downs")],
            "the barrow and the spot inside it are placed by the original's own records, so the errand is judged by the barrow downs the shipped words name."),

        // Initiate → Master (npc-38). "Kill the High Priest of Baa."
        new(28, "The High Priest of Baa",
            [new("reach", "The Temple of Baa")],
            "the High Priest is an actor the original's own map records place and this build does not, so the errand is judged by standing in the temple the shipped words name."),

        // Initiate → Ninja (npc-39). "Crack the code … discover the tomb's location, enter it."
        new(29, "The Tomb of Ashwar Nog'Nogoth",
            [new("reach", "The School of Sorcery"), new("reach", "The Hidden Tomb")],
            "cracking the code is the school's own event program, which this build does not run: the errand is judged by standing where the code is read and in the tomb it names."),

        // Archer → Warrior Mage (npc-41). "Sabotage the lift in the Red Dwarf Mines."
        new(31, "The lift in the Red Dwarf Mines",
            [new("reach", "The Red Dwarf Mines")],
            "the lift is the mine's own event program, which this build does not run: the errand is judged by standing in the mine the shipped words name."),

        // Cavalier → Champion (npc-42) is the arena count the ladder keeps as a record, so no errand here.
        // Cavalier → Black Knight (npc-43). "Destroy all the undead in the Haunted House."
        new(34, "The undead of the Haunted Mansion",
            [
                new("kill", "Ghast", "The Haunted Mansion"),
                new("kill", "Shade", "The Haunted Mansion"),
                new("kill", "Wight", "The Haunted Mansion"),
            ],
            "the count is every one of each kind the house's own placements hold, because the shipped words say all of them."),

        // Knight → Cavalier (npc-43). "Raid the Elven Treasury at Castle Navan."
        new(35, "The Elven Treasury at Castle Navan",
            [new("reach", "Castle Navan")],
            "the treasury is the castle's own event program, which this build does not run: the errand is judged by standing in the castle the shipped words name."),

        // Hunter → Ranger Lord (npc-44). "Calm the trees … by speaking to the Oldest Tree."
        new(36, "The trees of the Tularean Forest",
            [new("reach", "The Tularean Forest")],
            "the Oldest Tree is a person the original places by its own map records and this build does not, so the errand is judged by standing in the forest the shipped words name."),

        // Ranger → Hunter (npc-45). "Solve the secret of the Faerie Mound in Avlee and speak to the Faerie King."
        new(37, "The secret of the Faerie Mound",
            [new("reach", "Avlee")],
            "the mound and its king are placed by the original's own records rather than by any this build places, so the errand is judged by the region the shipped words name."),

        // Cleric → Priest (npc-47). "Find the lost pirate map in the Tidewater Caverns."
        new(43, "The lost pirate map",
            [new("reach", "The Tidewater Caverns")],
            "the map is the caverns' own event program, which this build does not run: the errand is judged by standing in the caverns the shipped words name."),

        // Priest → Priest of the Light (npc-46). "Purify the Altar of Evil in the Temple of the Moon."
        new(42, "The Altar of Evil",
            [new("reach", "The Temple of the Moon")],
            "purifying the altar is the temple's own event program, which this build does not run: the errand is judged by standing in the temple the shipped words name."),

        // Priest → Priest of the Dark (npc-47). "Deface the Altar of Good in the Temple of the Sun."
        new(44, "The Altar of Good",
            [new("reach", "Grand Temple of the Sun")],
            "defacing the altar is the temple's own event program, which this build does not run: the errand is judged by standing in the temple the shipped words name."),

        // Druid → Great Druid (npc-50). "Visit the three stonehenge monoliths in Tatalia, the Evenmorn
        // Islands, and Avlee." Three places, which the shipped words name as three and the world carries.
        new(49, "The three stonehenge monoliths",
            [new("reach", "Tatalia"), new("reach", "Evenmorn Island"), new("reach", "Avlee")],
            "a monolith is a decoration rather than a place, so each is judged by the region the shipped words put it in."),

        // Great Druid → Arch Druid (npc-50). "Retrieve the bones … and place them in the Barrow Downs."
        new(54, "The Dwarf King's bones",
            [new("reach", "The Barrow Downs")],
            "the bones are an item the shipped item table does not carry under that name, so the errand is judged by the place the shipped words say to lay them in."),
    ];
}
