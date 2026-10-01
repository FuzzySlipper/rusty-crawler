using System.Globalization;
using System.Text.Json;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.World;
using Rusty.Engine;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// This game's answer about the encounters a level's spawn records ask for: which graded variant each creature
/// is, how many stand on the field, and where each stands.
/// </summary>
/// <remarks>
/// <para>
/// <b>What content carries.</b> The importer writes one placement of kind <c>encounter</c> per actor spawn
/// record: the encounter slot it names (one to twelve), the grade when the record's own number fixes one, the
/// slot's kind, difficulty and count range, the record's group, attributes and radius, and the monster rows the
/// kind's graded variants are. It makes no choice. What a record asks for is the donor's reading
/// (OpenEnroth <c>src/Engine/Objects/Actor.cpp:4218-4273</c>): encounters one to three are the map's three
/// slots with the grade and the count both drawn, four to twelve are those slots graded A, B, or C, each
/// putting exactly one creature on the field.
/// </para>
/// <para>
/// <b>What this decides, and how.</b> A random slot's count is drawn uniformly from the slot's own range, both
/// ends included (<c>Actor.cpp:4226-4238</c>: the minimum plus a draw below the span), and a slot that states no
/// range puts one creature on the field. Each creature of a random slot draws its own grade from the donor's
/// odds for the slot's difficulty (<c>Actor.cpp:63</c>, <c>word_4E8152</c>, read per creature at
/// <c>Actor.cpp:4291-4311</c>; the difficulty is capped at five, <c>Actor.cpp:4273</c>). The donor applies no
/// grade at a difficulty of zero and then finds no row by the bare kind; this game reads that difficulty as
/// even odds across the three grades instead, which no shipped map states and which is ours.
/// </para>
/// <para>
/// <b>Every draw is keyed by the place and the spawn record.</b> The count is drawn under
/// <c>&lt;place&gt;/&lt;spawn&gt;</c> and each creature's grade under that key and its own unit, from the engine's
/// keyed random service under this game's fixed seed and scope. The same content therefore resolves to the same
/// creatures on every read, every visit, every restore, and after every load — a save carries no copy of what
/// was resolved, and needs none.
/// </para>
/// <para>
/// <b>Where the creatures stand.</b> The first stands on the record's own point and the rest on a circle of the
/// record's own radius. The donor stacks them at the point exactly (<c>Actor.cpp:4336-4342</c> computes an offset
/// and never uses it), and a place whose actors have no renderer or radius here would otherwise hold several
/// creatures that cannot be told apart; the spread is ours.
/// </para>
/// <para>
/// <b>A level's own creatures are placed through the same seam.</b> The importer also writes one placement of kind
/// <c>actor</c> per actor record a map's delta carries that names no person: the monster row it is, where it stands
/// and faces, its group and attributes, and its index in the level's own actor array. The donor loads that array
/// before its spawn records add theirs (OpenEnroth <c>src/Engine/Graphics/Indoor.cpp:907-922</c>), and reloads it
/// when a place respawns (<c>Indoor.cpp:310-319</c>), so it is what a first visit and every restore hold. Such a
/// placement stands as exactly one creature, at the record's point and facing, keeping the actor array's own field
/// and index — the number a map event counting one creature's dead names it by (<c>Actor.cpp:2811-2834</c>) — and
/// draws nothing. A record the level holds hidden (<c>"hidden": true</c>, the donor's <c>Disabled</c> state) stands
/// as nothing: the donor neither shows nor runs it until something clears its bit (<c>Actor.cpp:124-136</c>), and no
/// shipped event step does.
/// </para>
/// <para>
/// <b>A product that cannot draw resolves only what needs no draw.</b> Without the engine's random service a
/// graded record still puts its one creature on the field, and a random one puts nothing, which is reported
/// rather than filled from an invented source. A resolution is remembered per placement, so the population and
/// a rule that counts what a place holds read one answer.
/// </para>
/// </remarks>
internal sealed class MightAndMagic7Spawns : IPlacementExpansion
{
    /// <summary>The placement kind a spawn record's encounter is written under.</summary>
    internal const string EncounterPlacementKind = "encounter";

    /// <summary>The placement kind a level's own creature record is written under.</summary>
    internal const string ActorPlacementKind = "actor";

    /// <summary>The field that marks a level's own creature record as one the level holds hidden.</summary>
    internal const string HiddenField = "hidden";

    /// <summary>The seed every draw of this game's spawns is made under.</summary>
    /// <remarks>The engine's random service takes an explicit seed and reads no wall clock, so the product states one.</remarks>
    internal const ulong RollSeed = 0x5C1E_7A11_5EE9_0005;

    /// <summary>The scope every spawn draw lives under, so no other owner's draw can share one.</summary>
    internal const string RollScope = "mm7.spawn";

    /// <summary>The grades a kind's variants are named by, in the donor's own order.</summary>
    private static readonly string[] Grades = ["A", "B", "C"];

    /// <summary>
    /// The donor's grade odds: three weights per difficulty, one for each graded variant (OpenEnroth
    /// <c>src/Engine/Objects/Actor.cpp:63</c>). Row zero is ours — the donor's own zero row applies no grade.
    /// </summary>
    private static readonly int[][] GradeOdds =
    [
        [1, 1, 1],
        [90, 8, 2],
        [70, 20, 10],
        [50, 30, 20],
        [30, 40, 30],
        [10, 50, 40],
    ];

    private readonly IRandomService? _random;
    private readonly Dictionary<(PlaceId Place, string Placement), IReadOnlyList<PlacementDefinition>> _resolved = [];
    private readonly List<string> _unresolved = [];

    private MightAndMagic7Spawns(IRandomService? random) => _random = random;

    /// <summary>What resolving has left unplaced, one sentence per encounter, for a report.</summary>
    internal IReadOnlyList<string> Unresolved => _unresolved;

    /// <summary>Composes this game's spawn answers over the content the product loaded.</summary>
    /// <param name="catalog">The validated content, when the product loaded any.</param>
    /// <param name="random">The engine's keyed random service, or null when the product has no engine.</param>
    /// <returns>The answers.</returns>
    /// <exception cref="ContentValidationException">
    /// An encounter names a variant row the monster table does not carry, or a grade that is not one of the three;
    /// every problem is named.
    /// </exception>
    internal static MightAndMagic7Spawns Compose(ContentCatalog? catalog, IRandomService? random)
    {
        if (catalog is not null) Validate(catalog);
        return new MightAndMagic7Spawns(random);
    }

    /// <inheritdoc />
    public IReadOnlyList<PlacementDefinition>? Expand(PlaceId place, PlacementDefinition placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        if (string.Equals(placement.Content.Kind, ActorPlacementKind, StringComparison.Ordinal)) return Stand(placement.Source.Payload);
        if (!string.Equals(placement.Content.Kind, EncounterPlacementKind, StringComparison.Ordinal)) return null;
        return Resolve(place, placement.Content.Id, placement.Source.Payload);
    }

    /// <summary>The creature a level's own actor record stands as: itself, or nothing when the level holds it hidden.</summary>
    /// <remarks>
    /// The record is the answer rather than a request, so nothing is drawn and nothing is remembered: the same
    /// content stands the same creature on every read. It keeps the record's field and index, so a count of one
    /// creature's dead finds it by the number the level gives it, and the creature's own identity is that number
    /// too, which no encounter's creature can share.
    /// </remarks>
    /// <param name="actor">The actor placement as content states it.</param>
    /// <returns>The one creature placement, or none for a hidden record.</returns>
    internal static IReadOnlyList<PlacementDefinition> Stand(JsonElement actor)
    {
        if (Hidden(actor)) return [];
        string index = ContentEntry.ReadId(actor, "sourceIndex");
        string id = $"monster-actor-{index}";
        using MemoryStream buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString(PlacePopulationContent.IdField, id);
            writer.WriteString(PlacePopulationContent.KindField, MightAndMagic7Combat.CreaturePlacementKind);

            // Every field the record states travels as it is — its field and index, its point and facing, its row
            // and group — and the actor placement it came from is named beside them, so the creature reads exactly
            // as the record wrote it and a report can follow it back.
            foreach (JsonProperty property in actor.EnumerateObject())
            {
                if (property.NameEquals(PlacePopulationContent.IdField) || property.NameEquals(PlacePopulationContent.KindField)) continue;
                property.WriteTo(writer);
            }

            writer.WriteString("actorPlacement", ContentEntry.ReadId(actor, PlacePopulationContent.IdField));
            writer.WriteEndObject();
        }

        using JsonDocument document = JsonDocument.Parse(buffer.ToArray());
        return [PlacePopulationContent.Definition(
            new PlacementContentId(MightAndMagic7Combat.CreaturePlacementKind, id),
            document.RootElement.Clone())];
    }

    /// <summary>Whether a level's own creature record is one the level holds hidden.</summary>
    private static bool Hidden(JsonElement actor) =>
        actor.ValueKind == JsonValueKind.Object &&
        actor.TryGetProperty(HiddenField, out JsonElement hidden) &&
        hidden.ValueKind == JsonValueKind.True;

    /// <summary>The creatures one encounter placement resolves to, the same on every call.</summary>
    /// <param name="place">The place the encounter stands in.</param>
    /// <param name="placementId">The encounter placement's identity.</param>
    /// <param name="encounter">The encounter placement as content states it.</param>
    /// <returns>The creature placements, in unit order; none when nothing could be resolved.</returns>
    internal IReadOnlyList<PlacementDefinition> Resolve(PlaceId place, string placementId, JsonElement encounter)
    {
        if (_resolved.TryGetValue((place, placementId), out IReadOnlyList<PlacementDefinition>? known)) return known;

        EncounterFacts facts = Read(encounter);
        KeyedRolls? rolls = _random is null
            ? null
            : new KeyedRolls(_random, RollSeed, RollScope, $"{place.Value}/{facts.Spawn.ToString(CultureInfo.InvariantCulture)}");

        List<PlacementDefinition> creatures = [];
        int quantity;
        if (!facts.CountDrawn || facts.AppearMin == facts.AppearMax)
        {
            quantity = facts.CountDrawn ? facts.AppearMin : 1;
        }
        else if (rolls is null)
        {
            quantity = 0;
            _unresolved.Add($"place '{place}' encounter '{placementId}' draws its count from {facts.AppearMin} to {facts.AppearMax} and this session has no random service to draw it with, so nothing stands there.");
        }
        else
        {
            quantity = rolls.Under("count").Between(facts.AppearMin, facts.AppearMax);
        }

        for (int unit = 0; unit < quantity; unit++)
        {
            string? grade = facts.FixedGrade ?? DrawGrade(facts.Difficulty, rolls?.Under($"unit-{unit.ToString(CultureInfo.InvariantCulture)}"));
            if (grade is null)
            {
                _unresolved.Add($"place '{place}' encounter '{placementId}' draws each creature's grade from the map's odds and this session has no random service to draw it with, so nothing stands there.");
                break;
            }

            if (!facts.Variants.TryGetValue(grade, out (string Monster, string Name) row))
            {
                _unresolved.Add($"place '{place}' encounter '{placementId}' drew grade {grade} of '{facts.Kind}' and the content carries no such variant, so that creature does not stand there.");
                continue;
            }

            creatures.Add(Creature(
                string.Create(CultureInfo.InvariantCulture, $"monster-{facts.Spawn}-{unit}"),
                placementId,
                "spawn-record",
                facts,
                grade,
                row,
                quantity,
                unit));
        }

        IReadOnlyList<PlacementDefinition> resolved = [.. creatures];
        _resolved[(place, placementId)] = resolved;
        return resolved;
    }

    /// <summary>
    /// The creatures a map event's summoning puts on the field: the slot it names, at its own point, joining its own
    /// group, as many as it says.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The donor builds a spawn record of its own — the event's point, the event's group, a radius of
    /// <see cref="SummonRadius"/> — and hands it to the same reading a level's spawn records get, with the event's count
    /// replacing the slot's own when the event states one (OpenEnroth <c>src/Engine/Evt/EvtInterpreter.cpp:77-99</c>,
    /// <c>src/Engine/Objects/Actor.cpp:4275</c>). So does this: the slot is read as an encounter placement is, a grade
    /// the slot leaves open is drawn per creature from the slot's odds, and a count the event leaves open is drawn from
    /// the slot's range. Faithful, except that the creatures are spread on the circle as an encounter's are and face the
    /// place's zero rather than the party, and a unique name the event gives them is not shown: their row's name is.
    /// </para>
    /// <para>
    /// Unlike an encounter's, a summoning's draws are not remembered: the donor draws again each time the step runs.
    /// </para>
    /// </remarks>
    /// <param name="id">The identity prefix the creatures take, which no other live creature may share.</param>
    /// <param name="slot">The slot the summoning names, as content writes it.</param>
    /// <param name="count">How many the event states, zero for the slot's own range.</param>
    /// <param name="at">The event's point, in the place's own coordinates.</param>
    /// <param name="group">The group the creatures join.</param>
    /// <param name="rolls">The draw, or null when the product has no random service.</param>
    /// <param name="unresolved">Why nothing could be put on the field, or null when something was.</param>
    /// <returns>The creature placements, in unit order.</returns>
    internal static IReadOnlyList<PlacementDefinition> Summoned(
        string id,
        JsonElement slot,
        int count,
        (int X, int Y, int Z) at,
        int group,
        KeyedRolls? rolls,
        out string? unresolved)
    {
        unresolved = null;
        EncounterFacts stated = Read(slot);
        EncounterFacts facts = stated with { Group = group, Radius = SummonRadius, X = at.X, Y = at.Y, Z = at.Z, SourceField = "events" };
        int quantity;
        if (count > 0)
        {
            quantity = count;
        }
        else if (!facts.CountDrawn || facts.AppearMin == facts.AppearMax)
        {
            quantity = facts.CountDrawn ? facts.AppearMin : 1;
        }
        else if (rolls is null)
        {
            unresolved = $"it draws its count from {facts.AppearMin} to {facts.AppearMax} and this session has no random service to draw it with";
            return [];
        }
        else
        {
            quantity = rolls.Under("count").Between(facts.AppearMin, facts.AppearMax);
        }

        List<PlacementDefinition> creatures = [];
        for (int unit = 0; unit < quantity; unit++)
        {
            string? grade = facts.FixedGrade ?? DrawGrade(facts.Difficulty, rolls?.Under($"unit-{unit.ToString(CultureInfo.InvariantCulture)}"));
            if (grade is null)
            {
                unresolved = "it draws each creature's grade from the map's odds and this session has no random service to draw it with";
                return [];
            }

            if (!facts.Variants.TryGetValue(grade, out (string Monster, string Name) row))
            {
                unresolved = $"it drew grade {grade} of '{facts.Kind}' and the content carries no such variant";
                return [];
            }

            creatures.Add(Creature(
                string.Create(CultureInfo.InvariantCulture, $"{id}-{unit}"),
                id,
                "summoned-by-event",
                facts,
                grade,
                row,
                quantity,
                unit));
        }

        return creatures;
    }

    /// <summary>The radius the donor gives the spawn record a summoning builds (OpenEnroth <c>src/Engine/Evt/EvtInterpreter.cpp:86</c>).</summary>
    internal const int SummonRadius = 32;

    /// <summary>
    /// Counts, per place and per monster row, the creatures every encounter content states resolves to and every
    /// level's own creature record stands as.
    /// </summary>
    /// <remarks>
    /// This is the same resolution the population makes — remembered per placement — so an errand that asks for
    /// every creature of a kind in a place counts exactly the creatures the party will find there.
    /// </remarks>
    /// <param name="catalog">The content whose places to read.</param>
    /// <returns>For each place id, the creatures of each monster row its encounters resolve to.</returns>
    internal Dictionary<string, Dictionary<string, int>> Count(ContentCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        Dictionary<string, Dictionary<string, int>> placed = [];
        foreach ((_, _, ContentEntry place) in catalog.Entries(PlaceGraphLoader.PlaceDefinitionKind))
        {
            foreach (JsonElement placement in place.GetArray(PlacePopulationContent.PlacementsField))
            {
                string kind = ContentEntry.ReadString(placement, PlacePopulationContent.KindField);
                bool own = string.Equals(kind, ActorPlacementKind, StringComparison.Ordinal);
                if (!own && !string.Equals(kind, EncounterPlacementKind, StringComparison.Ordinal)) continue;
                string id = ContentEntry.ReadId(placement, PlacePopulationContent.IdField);
                if (id.Length == 0) continue;
                foreach (PlacementDefinition creature in own ? Stand(placement) : Resolve(new PlaceId(place.Id), id, placement))
                {
                    string row = creature.Source.GetId(MightAndMagic7Combat.MonsterField);
                    if (!placed.TryGetValue(place.Id, out Dictionary<string, int>? byRow)) placed[place.Id] = byRow = new Dictionary<string, int>(StringComparer.Ordinal);
                    byRow[row] = byRow.GetValueOrDefault(row) + 1;
                }
            }
        }

        return placed;
    }

    /// <summary>Draws one creature's grade from the odds of a difficulty, or null when there is nothing to draw with.</summary>
    private static string? DrawGrade(int difficulty, KeyedRolls? rolls)
    {
        if (rolls is null) return null;
        int[] odds = GradeOdds[Math.Clamp(difficulty, 0, GradeOdds.Length - 1)];
        int pick = rolls.Weighted(odds.Sum());
        for (int index = 0; index < odds.Length; index++)
        {
            if (pick < odds[index]) return Grades[index];
            pick -= odds[index];
        }

        return Grades[^1];
    }

    /// <summary>Writes one resolved creature as the placement every other reader of a creature reads.</summary>
    /// <remarks>
    /// The creature is a placement of the kind the fight recognises, naming its row under the field the fight
    /// reads; everything else on it is the reading that produced it — the encounter, the grade and where it came
    /// from, the count and where it came from, and the record's group, attributes, radius and range — so a report
    /// can follow it back to the record and see why it is there.
    /// </remarks>
    private static PlacementDefinition Creature(
        string id,
        string placementId,
        string positionSource,
        EncounterFacts facts,
        string grade,
        (string Monster, string Name) row,
        int quantity,
        int unit)
    {
        (double x, double y) = Spread(facts, quantity, unit);
        using MemoryStream buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString(PlacePopulationContent.IdField, id);
            writer.WriteString(PlacePopulationContent.KindField, MightAndMagic7Combat.CreaturePlacementKind);
            writer.WriteString("sourceField", facts.SourceField);
            if (facts.SourceIndex is { } index) writer.WriteNumber("sourceIndex", index);
            writer.WriteNumber("x", x);
            writer.WriteNumber("y", y);
            writer.WriteNumber("z", facts.Z);

            // A spawn record states no facing: the donor drops the monster on its point and leaves its heading
            // to the model, so a creature faces the place's own zero until something turns it.
            writer.WriteNumber("yaw", 0);
            writer.WriteString("positionSource", positionSource);
            writer.WriteString(MightAndMagic7Combat.MonsterField, row.Monster);
            writer.WriteString("monsterName", row.Name);
            writer.WriteString("encounterPlacement", placementId);
            writer.WriteNumber("spawn", facts.Spawn);
            writer.WriteNumber("encounter", facts.Encounter);
            writer.WriteString("grade", grade);
            writer.WriteNumber("quantity", quantity);
            writer.WriteNumber("unit", unit);
            writer.WriteNumber(MightAndMagic7MonsterAi.GroupField, facts.Group);
            writer.WriteNumber("attributes", facts.Attributes);
            writer.WriteNumber("radius", facts.Radius);
            writer.WriteNumber("appearMin", facts.AppearMin);
            writer.WriteNumber("appearMax", facts.AppearMax);
            writer.WriteString("gradeSource", facts.FixedGrade is null ? "difficulty-odds" : "spawn-slot");
            writer.WriteString("countSource", facts.CountDrawn ? "slot-range" : "spawn-slot");
            writer.WriteEndObject();
        }

        using JsonDocument document = JsonDocument.Parse(buffer.ToArray());
        return PlacePopulationContent.Definition(
            new PlacementContentId(MightAndMagic7Combat.CreaturePlacementKind, id),
            document.RootElement.Clone());
    }

    /// <summary>Where one of an encounter's creatures stands: its point for the first, a circle of its radius for the rest.</summary>
    private static (double X, double Y) Spread(EncounterFacts facts, int quantity, int unit)
    {
        if (unit == 0 || quantity <= 1 || facts.Radius <= 0) return (facts.X, facts.Y);
        double angle = 2 * Math.PI * unit / quantity;
        return (facts.X + (facts.Radius * Math.Cos(angle)), facts.Y + (facts.Radius * Math.Sin(angle)));
    }

    /// <summary>Reads an encounter placement's fields.</summary>
    private static EncounterFacts Read(JsonElement encounter)
    {
        Dictionary<string, (string Monster, string Name)> variants = new(StringComparer.Ordinal);
        if (encounter.ValueKind == JsonValueKind.Object
            && encounter.TryGetProperty("variants", out JsonElement list)
            && list.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement variant in list.EnumerateArray())
            {
                string grade = ContentEntry.ReadString(variant, "grade");
                string monster = ContentEntry.ReadId(variant, MightAndMagic7Combat.MonsterField);
                if (grade.Length > 0 && monster.Length > 0) variants[grade] = (monster, ContentEntry.ReadString(variant, "monsterName"));
            }
        }

        int appearMin = Whole(encounter, "appearMin");
        int appearMax = Whole(encounter, "appearMax");
        string stated = ContentEntry.ReadString(encounter, "grade");
        string? fixedGrade = stated.Length > 0 ? stated : null;
        double? sourceIndex = ContentEntry.ReadDouble(encounter, "sourceIndex");
        return new EncounterFacts(
            Whole(encounter, "spawn"),
            Whole(encounter, "encounter"),
            fixedGrade,
            ContentEntry.ReadString(encounter, "monsterKind"),
            Whole(encounter, "difficulty"),
            appearMin,
            appearMax,
            fixedGrade is null && appearMin > 0 && appearMax >= appearMin,
            Whole(encounter, MightAndMagic7MonsterAi.GroupField),
            Whole(encounter, "attributes"),
            Whole(encounter, "radius"),
            ContentEntry.ReadDouble(encounter, "x") ?? 0,
            ContentEntry.ReadDouble(encounter, "y") ?? 0,
            ContentEntry.ReadDouble(encounter, "z") ?? 0,
            ContentEntry.ReadString(encounter, "sourceField"),
            sourceIndex is { } value ? (int)value : null,
            variants);
    }

    /// <summary>Judges every encounter content states against the grades and the monster rows this game reads.</summary>
    private static void Validate(ContentCatalog catalog)
    {
        HashSet<string> monsters = [.. catalog.Entries(MightAndMagic7Combat.MonsterDefinitionKind).Select(entry => entry.Entry.Id)];
        List<ContentValidationIssue> issues = [];
        foreach ((LoadedPack pack, ContentDocument document, ContentEntry place) in catalog.Entries(PlaceGraphLoader.PlaceDefinitionKind))
        {
            foreach (JsonElement placement in place.GetArray(PlacePopulationContent.PlacementsField))
            {
                string kind = ContentEntry.ReadString(placement, PlacePopulationContent.KindField);
                string id = ContentEntry.ReadId(placement, PlacePopulationContent.IdField);
                if (string.Equals(kind, ActorPlacementKind, StringComparison.Ordinal))
                {
                    // A level's own creature names its row and its index in the level's actor array, which is its
                    // identity as a creature; one that names neither could stand as nothing the fight or a count
                    // of the dead could read.
                    string row = ContentEntry.ReadId(placement, MightAndMagic7Combat.MonsterField);
                    if (!monsters.Contains(row))
                    {
                        issues.Add(new ContentValidationIssue(
                            "actor-monster-unknown",
                            $"place '{place.Id}' actor '{id}' names monster '{row}', which no monster row in this content describes.",
                            pack.PackId,
                            document.DocumentId));
                    }

                    if (ContentEntry.ReadId(placement, "sourceIndex").Length == 0)
                    {
                        issues.Add(new ContentValidationIssue(
                            "actor-index-missing",
                            $"place '{place.Id}' actor '{id}' states no 'sourceIndex', which is the number its level gives it and the creature's own identity.",
                            pack.PackId,
                            document.DocumentId));
                    }

                    continue;
                }

                if (!string.Equals(kind, EncounterPlacementKind, StringComparison.Ordinal)) continue;
                EncounterFacts facts = Read(placement);
                if (facts.FixedGrade is { } grade && !Grades.Contains(grade, StringComparer.Ordinal))
                {
                    issues.Add(new ContentValidationIssue(
                        "encounter-grade-unknown",
                        $"place '{place.Id}' encounter '{id}' fixes grade '{grade}', and this game's grades are A, B, and C.",
                        pack.PackId,
                        document.DocumentId));
                }

                if (facts.FixedGrade is { } fixedGrade && !facts.Variants.ContainsKey(fixedGrade))
                {
                    issues.Add(new ContentValidationIssue(
                        "encounter-variant-missing",
                        $"place '{place.Id}' encounter '{id}' fixes grade '{fixedGrade}' of '{facts.Kind}' and states no variant row for it, so no creature could stand there.",
                        pack.PackId,
                        document.DocumentId));
                }

                foreach ((string variantGrade, (string monster, _)) in facts.Variants)
                {
                    if (monsters.Contains(monster)) continue;
                    issues.Add(new ContentValidationIssue(
                        "encounter-monster-unknown",
                        $"place '{place.Id}' encounter '{id}' names monster '{monster}' for grade {variantGrade}, which no monster row in this content describes.",
                        pack.PackId,
                        document.DocumentId));
                }
            }
        }

        if (issues.Count > 0)
        {
            throw new ContentValidationException($"This game's encounters and placed creatures cannot be resolved: {issues[0].Message}", issues);
        }
    }

    /// <summary>A whole-number field of an encounter, zero when it states none.</summary>
    private static int Whole(JsonElement element, string property) =>
        ContentEntry.ReadDouble(element, property) is { } value && value >= int.MinValue && value <= int.MaxValue ? (int)value : 0;

    /// <summary>One encounter placement's fields, as this game reads them.</summary>
    private sealed record EncounterFacts(
        int Spawn,
        int Encounter,
        string? FixedGrade,
        string Kind,
        int Difficulty,
        int AppearMin,
        int AppearMax,
        bool CountDrawn,
        int Group,
        int Attributes,
        int Radius,
        double X,
        double Y,
        double Z,
        string SourceField,
        int? SourceIndex,
        IReadOnlyDictionary<string, (string Monster, string Name)> Variants);
}
