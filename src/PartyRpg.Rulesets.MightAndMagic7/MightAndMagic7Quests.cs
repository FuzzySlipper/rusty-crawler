using System.Globalization;
using System.Text.Json;
using PartyRpg.Kit;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Conversation;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Promotion;
using PartyRpg.Kit.Quests;
using PartyRpg.Kit.Sessions;
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
/// Imported promotion quests run their actual map and conversation events. Their quest-bit journal
/// notes are not separate turn-in quests: merely reaching a place must never finish its event's deed.
/// Authored packs may state complete readings with the existing objective and condition kinds.
/// Town-hall bounties and arena bouts are composed by their owning policies.
/// </remarks>
internal sealed class MightAndMagic7Quests : IQuestRule, IQuestAcceptanceRule, IQuestNotesRule
{
    /// <summary>The definition kind a shipped quest row is declared under.</summary>
    internal const string QuestDefinitionKind = "quest";


    /// <summary>The field a placement names the monster row it stands for under.</summary>
    internal const string MonsterField = "monster";




    private readonly Dictionary<string, QuestNote> _eventNotes = new(StringComparer.Ordinal);

    /// <summary>Journal text whose imported event bit is active; the event remains its only writer.</summary>
    public IReadOnlyList<QuestNote> NotesFor(PartyEntity party) =>
        [.. _eventNotes.Where(pair => party.Records.Has(ErrandRecord(pair.Key))).Select(pair => pair.Value)];

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
    internal MightAndMagic7Arena? Arena { get; set; }

    public Refusal? CanAccept(QuestAcceptance acceptance) => Arena?.CanAccept(acceptance);
    public void Accepted(QuestAcceptance acceptance) => Arena?.Accepted(acceptance);

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

    /// <summary>Reads this game's quests over the content the product loaded.</summary>
    /// <remarks>
    /// The shipped words are read from the quest document the importer writes, and a bit the pack does not
    /// carry is reported rather than stated without its note: an errand whose own words are missing would
    /// leave a player with an objective and no story.
    /// </remarks>
    /// <param name="catalog">The validated content, or null when no bundle supplied any.</param>
    /// <param name="promotions">This game's ranks, which name every errand's giver and its shipped bit.</param>
    /// <param name="spawns">
    /// This game's answer about the encounters content states, whose creatures an errand that asks for every one
    /// of a kind in a place counts. It is the same resolution the population makes, so the errand and the world
    /// agree about how many there are. A caller that composed none gets one composed here without a random
    /// service, which resolves only the encounters that need no draw.
    /// </param>
    /// <returns>This game's quests, or null when no content was loaded.</returns>
    internal static MightAndMagic7Quests? Read(
        ContentCatalog? catalog,
        MightAndMagic7Promotions? promotions,
        MightAndMagic7Spawns? spawns = null,
        Func<SessionWorld?>? world = null, Func<PartyQuests?>? journal = null, Func<CombatState?>? fight = null)
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
            // reading is a journal note rather than an independently offered errand.
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

        // The creatures a spawn record's encounter resolves to are counted as the population places them: the
        // same keyed resolution, remembered per placement, so "every one in that place" is the number the party
        // finds there.
        foreach ((string placeId, Dictionary<string, int> resolved) in (spawns ?? MightAndMagic7Spawns.Compose(catalog, random: null)).Count(catalog))
        {
            if (!placed.TryGetValue(placeId, out Dictionary<string, int>? byRow)) placed[placeId] = byRow = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach ((string row, int count) in resolved) byRow[row] = byRow.GetValueOrDefault(row) + count;
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
        quests.Arena = MightAndMagic7Arena.Read(catalog, world, journal, fight);
        quests.ReadAuthored(authored, words, issues);
        if (issues.Count > 0)
        {
            throw new ContentValidationException(
                $"This game's quests cannot be read: {issues[0].Message}",
                issues);
        }

        foreach (var (id, text) in words)
        {
            if (quests._byId.ContainsKey(id)) continue;
            var rank = catalog.Entries("promotion").Select(row => row.Entry).FirstOrDefault(entry => entry.GetString("quest") == id);
            string to = rank.GetString("to");
            // The person who set the quest and the remote speaker who grants its rank can differ.
            // Journal attribution reads the initial giver; promotion requirements retain the granting speaker.
            string giver = rank.GetString("questGiver");
            quests._eventNotes[id] = new(id, to.Length > 0 ? $"{to} promotion" : "Quest", text, giver);
        }
        quests.ErrandCount = quests._byId.Count;
        quests.ErrandGiverCount = quests._byGiver.Count;
        notes.Add($"{words.Count} journal notes and {quests.ErrandCount} authored quests over {quests.ErrandGiverCount} givers.");
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

            bool accumulate = false;
            if (stated.TryGetProperty("accumulate", out JsonElement counting))
            {
                if (counting.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    defect("quest-reward-count-invalid", $"quest '{entry.Id}' must state accumulate as true or false for record '{record}'.");
                    paid = false;
                    continue;
                }
                accumulate = counting.GetBoolean();
            }
            records.Add(new QuestRewardRecord(record, Math.Max(1, (int)(ContentEntry.ReadDouble(stated, "amount") ?? 1)), accumulate));
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
        return Arena?.Definition(quest) ?? BountyDefinition(quest.Value)?.Quest;
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
        if (Arena?.Definition(request.Definition.Id) is not null) return Arena.Counts(request);

        // A creature a spell created is not one the place held: an elemental the party called up is not a kill an
        // errand asked for, and a body stood back up was counted when it fell the first time (ours).
        if (MightAndMagic7Summons.IsSummoned(request.Body)) return 0;
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
            new QuestRewards(coins: terms.Reward, records: [new QuestRewardRecord(MightAndMagic7Deeds.Bounties, terms.Reward, accumulate: true)]),
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

}
