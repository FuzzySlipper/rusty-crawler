using System.Globalization;
using System.Text.Json;
using PartyRpg.Kit;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Conversation;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Quests;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.World;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>Knight bouts are ordinary quests over creatures created by the existing population.</summary>
internal sealed class MightAndMagic7Arena
{
    internal const string BoutField = "arenaBout";
    internal const string SlotField = "arenaSlot";
    private const string Prefix = "arena:";
    private readonly Dictionary<string, Bout> _bouts;
    private readonly IReadOnlyList<(string Row, int Level)> _monsters;
    private readonly Func<SessionWorld?> _world;
    private readonly Func<PartyQuests?> _quests;
    private readonly Func<CombatState?> _fight;
    private readonly int _knightOpponents;

    internal sealed record Challenger(string Row, PlacePose Pose);
    internal sealed record Bout(string Id, PlaceId Place, string Person, int GoldPerLevel, IReadOnlyList<Challenger> Challengers, IReadOnlyList<PlacePose> Feet);
    private sealed record Terms(Bout Bout, int Attempt, int Level, IReadOnlyList<Challenger> Challengers);

    private MightAndMagic7Arena(Dictionary<string, Bout> bouts, IReadOnlyList<(string Row, int Level)> monsters,
        Func<SessionWorld?> world, Func<PartyQuests?> quests, Func<CombatState?> fight, int knightOpponents)
    {
        _bouts = bouts; _monsters = monsters; _world = world; _quests = quests; _fight = fight; _knightOpponents = knightOpponents;
    }

    internal static MightAndMagic7Arena? Read(ContentCatalog? content, Func<SessionWorld?>? world = null, Func<PartyQuests?>? quests = null, Func<CombatState?>? fight = null)
    {
        if (content is null) return null;
        var places = content.Entries("place").Select(x => x.Entry).ToDictionary(x => x.Id);
        var people = content.Entries("person").Select(x => x.Entry.Id).ToHashSet();
        var monsters = content.Entries("monster").Select(x => (Row: x.Entry.Id, Level: x.Entry.GetInt32("level") ?? 1)).ToArray();
        Dictionary<string, Bout> bouts = new(StringComparer.Ordinal);
        List<ContentValidationIssue> issues = [];
        foreach (var (pack, document, entry) in content.Entries("arena-bout"))
        {
            string place = entry.GetId("place"), person = entry.GetId("person");
            int reward = entry.GetInt32("goldPerLevel") ?? -1;
            List<Challenger> opponents = [];
            foreach (JsonElement stated in entry.GetArray("challengers"))
            {
                string row = ContentEntry.ReadId(stated, "monster");
                double? x = ContentEntry.ReadDouble(stated, "x"), y = ContentEntry.ReadDouble(stated, "y"), z = ContentEntry.ReadDouble(stated, "z");
                if (!monsters.Any(m => m.Row == row) || x is null || y is null || z is null ||
                    !double.IsFinite(x.Value) || !double.IsFinite(y.Value) || !double.IsFinite(z.Value))
                {
                    issues.Add(new("arena-challenger-unknown", $"arena '{entry.Id}' states an unknown creature or no finite feet position", pack.PackId, document.DocumentId));
                    continue;
                }
                opponents.Add(new(row, new PlacePose(x.Value, y.Value, z.Value, 0, 0)));
            }
            if (entry.Id.Contains(':') || !places.ContainsKey(place) || !people.Contains(person) || reward < 0 || opponents.Count == 0 || bouts.ContainsKey(entry.Id))
            {
                issues.Add(new("arena-bout-incomplete", $"arena '{entry.Id}' repeats an identity or names no place, official, nonnegative reward or challengers", pack.PackId, document.DocumentId));
                continue;
            }
            bouts.Add(entry.Id, new(entry.Id, new(place), person, reward, opponents, []));
        }
        if (issues.Count > 0) throw new ContentValidationException("Arena content cannot state a complete bout.", issues);

        // The imported map's own speak-npc step supplies the official. The compiled default adapts the
        // donor's Knight count/reward to ten opponents and deterministic selection, not a second fight.
        if (bouts.Count == 0 && people.Contains("npc-300"))
        {
            foreach (var (pack, document, place) in content.Entries("place").Where(p => p.Entry.GetString("environment") == "ARENA"))
            {
                List<PlacePose> feet = [];
                foreach (JsonElement stated in place.GetArray("arenaChallengerFeet"))
                {
                    double? x = ContentEntry.ReadDouble(stated, "x"), y = ContentEntry.ReadDouble(stated, "y"), z = ContentEntry.ReadDouble(stated, "z");
                    if (x is not null && y is not null && z is not null && double.IsFinite(x.Value) && double.IsFinite(y.Value) && double.IsFinite(z.Value))
                        feet.Add(new(x.Value, y.Value, z.Value, 0, 0));
                }
                if (feet.Count < MightAndMagic7Tuning.Read(content).Whole(MightAndMagic7Tuning.ArenaKnightOpponents))
                {
                    issues.Add(new("arena-feet-missing", $"arena '{place.Id}' does not state finite feet positions for its challengers", pack.PackId, document.DocumentId));
                    continue;
                }
                bouts.Add("knight-" + place.Id, new("knight-" + place.Id, new(place.Id), "npc-300",
                    MightAndMagic7Tuning.Read(content).Whole(MightAndMagic7Tuning.ArenaKnightGold), [], feet));
            }
        }
        if (issues.Count > 0) throw new ContentValidationException("Arena content cannot place its challengers.", issues);
        return new(bouts, monsters, world ?? (() => null), quests ?? (() => null), fight ?? (() => null),
            MightAndMagic7Tuning.Read(content).Whole(MightAndMagic7Tuning.ArenaKnightOpponents));
    }

    private Terms? Read(QuestId quest)
    {
        if (!quest.Value.StartsWith(Prefix, StringComparison.Ordinal)) return null;
        string[] words = quest.Value[Prefix.Length..].Split(':');
        if (words.Length != 3 || !_bouts.TryGetValue(words[0], out Bout? bout) ||
            !int.TryParse(words[1], NumberStyles.None, CultureInfo.InvariantCulture, out int attempt) || attempt < 1 ||
            !int.TryParse(words[2], NumberStyles.None, CultureInfo.InvariantCulture, out int level) || level < 1 ||
            (long)level * bout.GoldPerLevel > int.MaxValue) return null;
        IReadOnlyList<Challenger> challengers = bout.Challengers;
        if (challengers.Count == 0)
        {
            int low = Math.Clamp(level / 2, 2, 100), high = (int)Math.Clamp(2L * level, 2, 100);
            string[] rows = _monsters.Where(m => m.Level >= low && m.Level <= high).OrderBy(m => m.Row, StringComparer.Ordinal).Select(m => m.Row).ToArray();
            if (rows.Length == 0) return null;
            List<Challenger> selected = [];
            int count = _knightOpponents;
            for (int slot = 0; slot < count; slot++)
            {
                selected.Add(new(rows[slot % rows.Length], bout.Feet[slot * bout.Feet.Count / count]));
            }
            challengers = selected;
        }
        return new(bout, attempt, level, challengers);
    }

    internal QuestDefinition? Definition(QuestId quest)
    {
        if (Read(quest) is not { } terms) return null;
        return new(quest, $"Knight arena bout {terms.Attempt}", terms.Bout.Person,
            terms.Challengers.Select((_, slot) => new QuestObjective($"opponent-{slot}", QuestObjectiveKind.Kill,
                slot.ToString(CultureInfo.InvariantCulture), 1, $"Defeat arena opponent {slot + 1}", place: terms.Bout.Place.Value)).ToArray(),
            new QuestRewards(coins: terms.Level * terms.Bout.GoldPerLevel,
                records: [new QuestRewardRecord(MightAndMagic7Deeds.ArenaWins, 1, accumulate: true)]),
            note: "Defeat this bout's challengers, then return to the arena official for payment. A new bout can be taken after settlement.");
    }

    internal QuestDefinition? Offered(ConversationContext context)
    {
        if (context.Party is not { } party || _quests() is not { } quests) return null;
        Bout? bout = _bouts.Values.FirstOrDefault(b => b.Person == context.Speaker && b.Place == context.Place);
        if (bout is null) return null;
        var held = quests.Instances.Select(i => (Instance: i, Terms: Read(i.Quest))).Where(x => x.Terms?.Bout.Id == bout.Id).ToArray();
        if (held.LastOrDefault(x => x.Instance.Stage != QuestStage.TurnedIn).Instance is { } pending) return Definition(pending.Quest);
        int attempt = checked((held.Length == 0 ? 0 : held.Max(x => x.Terms!.Attempt)) + 1);
        int level = party.Members.Max(m => m.Progression.Level);
        return Definition(new(Prefix + bout.Id + ":" + attempt.ToString(CultureInfo.InvariantCulture) + ":" + level.ToString(CultureInfo.InvariantCulture)));
    }

    internal Refusal? CanAccept(QuestAcceptance acceptance)
    {
        if (Read(acceptance.Definition.Id) is not { } terms) return null;
        if (_world() is not { } world || world.Place != terms.Bout.Place || world.Population.Place != world.Place)
            return new("arena-not-present", "The party must stand in this arena before taking its bout.");
        if (_fight()?.IsEngaged == true)
            return new("arena-fight-pending", "Finish the resident fight before taking an arena bout.");
        if (_quests()?.Instances.Any(i => i.Quest != acceptance.Definition.Id && Read(i.Quest) is not null && i.Stage == QuestStage.Accepted) == true)
            return new("arena-bout-pending", "Finish the party's current arena bout before taking another.");
        return null;
    }

    internal void Accepted(QuestAcceptance acceptance)
    {
        if (Read(acceptance.Definition.Id) is null) return;
        StandRemaining(acceptance.Definition.Id, acceptance.Party);
    }

    internal int Counts(QuestKillRequest death)
    {
        if (Read(death.Definition.Id) is not { } terms || death.Place != terms.Bout.Place ||
            death.Body.Source.GetString(BoutField) != death.Definition.Id.Value ||
            death.Body.Source.GetInt32(SlotField) is not { } slot || slot < 0 || slot >= terms.Challengers.Count) return 0;
        return slot.ToString(CultureInfo.InvariantCulture) == death.Objective.Target &&
            death.Body.Source.GetId(MightAndMagic7Combat.MonsterField) == terms.Challengers[slot].Row ? 1 : 0;
    }

    internal IEnumerable<SaveProblem> Problems(SessionSave save)
    {
        HashSet<(string Bout, int Slot)> seen = [];
        foreach (CreatureCombatSave creature in save.Combat.Creatures)
        {
            if (creature.Origin is not { } origin || ContentEntry.ReadString(origin, "positionSource") != "summoned-by-arena") continue;
            string bout = ContentEntry.ReadString(origin, BoutField);
            int slot = (int)(ContentEntry.ReadDouble(origin, SlotField) ?? -1);
            Terms? terms = bout.Length == 0 ? null : Read(new(bout));
            QuestInstanceSave? held = save.Quests.Instances.FirstOrDefault(i => i.Quest.Value == bout);
            if (terms is null || held is null || held.Stage == "offered" || slot < 0 || slot >= terms.Challengers.Count ||
                terms.Bout.Place != save.Combat.Place || creature.Kind != terms.Challengers[slot].Row || !seen.Add((bout, slot)))
                yield return new("save-arena-opponent-unknown", creature.Placement.Id, "the creature does not belong to one opponent of a taken bout in this place");
            else if ((creature.Health <= 0) != held.Progress.Any(p => p.Objective == $"opponent-{slot}" && p.Count > 0))
                yield return new("save-arena-opponent-contradictory", creature.Placement.Id, "the bout's defeated opponent disagrees with the creature's health");
        }
    }

    internal ConversationAnswer Return(QuestId quest, PartyEntity party)
    {
        if (Read(quest) is not { } terms || _world() is not { } world || world.Place != terms.Bout.Place ||
            _quests()?.Instance(quest) is not { Stage: QuestStage.Accepted })
            return new("The party has no unfinished bout to return to in this arena.");
        int created = StandRemaining(quest, party);
        return new(created > 0 ? $"Return to the Knight bout: {created} remaining challenger(s) stand ready." : "This bout's challengers are already here, or its victory is ready to settle.");
    }

    private int StandRemaining(QuestId quest, PartyEntity party)
    {
        Terms terms = Read(quest)!;
        SessionWorld world = _world()!;
        QuestInstance? instance = _quests()?.Instance(quest);
        int created = 0;
        for (int slot = 0; slot < terms.Challengers.Count; slot++)
        {
            if (instance?.Recorded($"opponent-{slot}") > 0 || world.Population.Entities.Any(e =>
                e.Placement.Source.GetString(BoutField) == quest.Value && e.Placement.Source.GetInt32(SlotField) == slot)) continue;
            Challenger challenger = terms.Challengers[slot];
            string id = "arena-" + MightAndMagic7Summons.NextIdentity(party).ToString(CultureInfo.InvariantCulture);
            using MemoryStream buffer = new();
            using (Utf8JsonWriter writer = new(buffer))
            {
                writer.WriteStartObject();
                writer.WriteString("id", id);
                writer.WriteString("kind", MightAndMagic7Combat.CreaturePlacementKind);
                writer.WriteString(MightAndMagic7Combat.MonsterField, challenger.Row);
                writer.WriteNumber("x", challenger.Pose.X);
                writer.WriteNumber("y", challenger.Pose.Y);
                writer.WriteNumber("z", challenger.Pose.Z);
                writer.WriteString("positionSource", "summoned-by-arena");
                writer.WriteString(BoutField, quest.Value);
                writer.WriteNumber(SlotField, slot);
                writer.WriteEndObject();
            }
            using JsonDocument doc = JsonDocument.Parse(buffer.ToArray());
            world.Population.Summon(PlacePopulationContent.Definition(new(MightAndMagic7Combat.CreaturePlacementKind, id), doc.RootElement.Clone()));
            created++;
        }
        return created;
    }
}
