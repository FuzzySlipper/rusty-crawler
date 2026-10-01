using System.Globalization;
using MightAndMagic7.Import.Maps;
using MightAndMagic7.Import.Tables;

namespace MightAndMagic7.Import.Packs;

/// <summary>One graded variant of an encounter slot's kind: the monster table row a grade names.</summary>
/// <param name="Grade">The grade, <c>A</c>, <c>B</c>, or <c>C</c>.</param>
/// <param name="MonsterId">The monster table row whose internal name is the slot's kind plus the grade.</param>
/// <param name="MonsterName">The row's name, so a report reads without joining the table.</param>
public sealed record PlaceEncounterVariant(string Grade, int MonsterId, string MonsterName);

/// <summary>One actor spawn record, read into the encounter it asks the level for.</summary>
/// <remarks>
/// <para>
/// This is what the shipped record says and nothing more: which of the map's encounter slots it names, whether
/// the record fixes the grade or leaves it to the map's odds, the slot's kind and difficulty and count range, and
/// the monster rows the kind's graded variants are. Which grade a creature is and how many stand on the field
/// are the game's choice, made by the ruleset when a place is populated, so they are not here.
/// </para>
/// </remarks>
/// <param name="PlaceId">The place the record belongs to.</param>
/// <param name="PlacementId">The placement's identity within the place, prefixed with the kind it is written under.</param>
/// <param name="X">The record's own point along the place's first axis.</param>
/// <param name="Y">The record's own point along the place's second axis.</param>
/// <param name="Z">The record's own point in height.</param>
/// <param name="SourceSpawnIndex">The spawn record's index in its map's own spawn array.</param>
/// <param name="EncounterIndex">The slot number the record names: one to twelve.</param>
/// <param name="Slot">Which of the map's three encounter slots that number reads: one to three.</param>
/// <param name="FixedGrade">The grade the record's own number fixes, or null when it leaves the grade to the map's odds.</param>
/// <param name="MonsterKind">The slot's kind, the internal name its graded variants extend.</param>
/// <param name="Difficulty">The slot's own difficulty column, which the donor reads grade odds at.</param>
/// <param name="AppearMin">The fewest creatures the slot states, zero when it states no count.</param>
/// <param name="AppearMax">The most creatures the slot states, zero when it states no count.</param>
/// <param name="Group">The spawn record's own group, zero when it belongs to none.</param>
/// <param name="Attributes">The spawn record's own attribute bits, kept as the record states them.</param>
/// <param name="Radius">The radius the record states around its point.</param>
/// <param name="Variants">The graded variants the monster table carries for the kind, in grade order.</param>
public sealed record PlaceEncounterPlacement(
    int PlaceId,
    string PlacementId,
    double X,
    double Y,
    double Z,
    int SourceSpawnIndex,
    int EncounterIndex,
    int Slot,
    string? FixedGrade,
    string MonsterKind,
    int Difficulty,
    int AppearMin,
    int AppearMax,
    uint Group,
    int Attributes,
    int Radius,
    IReadOnlyList<PlaceEncounterVariant> Variants)
{
    /// <summary>Whether the record leaves the grade to the map's odds rather than fixing it.</summary>
    public bool GradeDrawn => FixedGrade is null;

    /// <summary>Whether the slot states a count range the game draws the number of creatures from.</summary>
    /// <remarks>
    /// A record that fixes its grade puts exactly one creature on the field (OpenEnroth
    /// <c>src/Engine/Objects/Actor.cpp:4220</c>, <c>NumToSpawn = 1</c>, drawn only for the three random cases),
    /// so its slot's range is provenance and not a count.
    /// </remarks>
    public bool CountDrawn => GradeDrawn && AppearMin > 0 && AppearMax >= AppearMin;

    /// <summary>The fewest creatures this record can put on the field.</summary>
    public int FewestCreatures => CountDrawn ? AppearMin : 1;

    /// <summary>The most creatures this record can put on the field.</summary>
    public int MostCreatures => CountDrawn ? AppearMax : 1;
}

/// <summary>One spawn record, or one whole place, the import could emit no encounter for.</summary>
/// <param name="Code">A short stable code for the kind of refusal.</param>
/// <param name="Subject">What the refusal is about, so a reader can find it.</param>
/// <param name="Reason">Why nothing was emitted, in terms a person can act on.</param>
public sealed record PlaceEncounterRefusal(string Code, string Subject, string Reason);

/// <summary>What one of a map's twelve encounter numbers reads as: the slot, the grade it fixes, and the slot's facts.</summary>
/// <remarks>
/// A spawn record names one of these numbers, and so does a map event that summons monsters (OpenEnroth
/// <c>src/Engine/Evt/EvtInterpreter.cpp:77-99</c> builds a spawn record of its own and hands it to the same
/// <c>SpawnEncounter</c>), so both are read through this one reading.
/// </remarks>
/// <param name="Encounter">The number: one to twelve.</param>
/// <param name="Slot">Which of the map's three encounter slots it reads: one to three.</param>
/// <param name="FixedGrade">The grade the number fixes, or null when it leaves the grade to the map's odds.</param>
/// <param name="MonsterKind">The slot's kind, the internal name its graded variants extend.</param>
/// <param name="Difficulty">The slot's own difficulty column, which the donor reads grade odds at.</param>
/// <param name="AppearMin">The fewest creatures the slot states, zero when it states no count.</param>
/// <param name="AppearMax">The most creatures the slot states, zero when it states no count.</param>
/// <param name="Variants">The graded variants the monster table carries for the kind, in grade order.</param>
public sealed record PlaceEncounterSlot(
    int Encounter,
    int Slot,
    string? FixedGrade,
    string MonsterKind,
    int Difficulty,
    int AppearMin,
    int AppearMax,
    IReadOnlyList<PlaceEncounterVariant> Variants);

/// <summary>What one import's encounter reading produced.</summary>
/// <param name="Placements">Every encounter emitted, in place and spawn order.</param>
/// <param name="Refusals">Every record nothing was emitted for, with its reason.</param>
/// <param name="SpawnRecords">How many spawn records the decoded maps carry altogether.</param>
/// <param name="ActorSpawns">How many of them ask the level for an actor, which is what a creature can come from.</param>
/// <param name="TreasureSpawns">How many of them ask the level for treasure instead.</param>
/// <param name="Notes">What the read noticed about the data, for a report.</param>
public sealed record PlaceEncounterSummary(
    IReadOnlyList<PlaceEncounterPlacement> Placements,
    IReadOnlyList<PlaceEncounterRefusal> Refusals,
    int SpawnRecords,
    int ActorSpawns,
    int TreasureSpawns,
    IReadOnlyList<string> Notes)
{
    /// <summary>An import that read no maps, so nothing was read.</summary>
    public static PlaceEncounterSummary Empty { get; } = new([], [], 0, 0, 0, []);

    /// <summary>How many encounters the import emits: one per actor spawn record whose slot and kind it could read.</summary>
    public int EncounterCount => Placements.Count;

    /// <summary>How many places hold at least one encounter.</summary>
    public int PopulatedPlaces => Placements.Select(placement => placement.PlaceId).Distinct().Count();

    /// <summary>How many encounters leave their grade to the map's odds.</summary>
    public int DrawnGrades => Placements.Count(placement => placement.GradeDrawn);

    /// <summary>How many encounters leave their count to the slot's range.</summary>
    public int DrawnCounts => Placements.Count(placement => placement.CountDrawn);

    /// <summary>The fewest creatures the emitted encounters can resolve to, the floor of every slot's range.</summary>
    public int FewestCreatures => Placements.Sum(placement => placement.FewestCreatures);

    /// <summary>The most creatures the emitted encounters can resolve to, the ceiling of every slot's range.</summary>
    public int MostCreatures => Placements.Sum(placement => placement.MostCreatures);

    /// <summary>How many distinct monster rows the encounters can resolve to.</summary>
    public int DistinctMonsters => Placements
        .SelectMany(placement => placement.GradeDrawn
            ? placement.Variants
            : placement.Variants.Where(variant => variant.Grade == placement.FixedGrade))
        .Select(variant => variant.MonsterId)
        .Distinct()
        .Count();

    /// <summary>The encounters each place holds, keyed by place id.</summary>
    public IReadOnlyDictionary<int, int> PerPlace
    {
        get
        {
            SortedDictionary<int, int> counts = [];
            foreach (PlaceEncounterPlacement placement in Placements)
            {
                counts[placement.PlaceId] = counts.GetValueOrDefault(placement.PlaceId) + 1;
            }

            return counts;
        }
    }

    /// <summary>Why encounters were not emitted, counted by the reason's own code.</summary>
    public IReadOnlyDictionary<string, int> RefusalCodes
    {
        get
        {
            SortedDictionary<string, int> counts = new(StringComparer.Ordinal);
            foreach (PlaceEncounterRefusal refusal in Refusals)
            {
                counts[refusal.Code] = counts.GetValueOrDefault(refusal.Code) + 1;
            }

            return counts;
        }
    }
}

/// <summary>
/// Reads the encounters a level's spawn records ask for, with provenance and without choosing anything.
/// </summary>
/// <remarks>
/// <para>
/// <b>What a shipped spawn record holds.</b> A record is twenty-four bytes: a position, a radius, an
/// object reference, an index, an attribute bitfield, and a group
/// (<c>OpenEnroth src/Engine/Snapshots/EntitySnapshots.h:945-953</c>). The object reference is what kind
/// of thing the level spawns: two is an object or a treasure and three is an actor
/// (<c>src/Engine/Pid.h:12</c>), which is the only kind a creature can come from. The index is not a
/// monster row: it is one of the map's twelve encounter slots, which the donor reads as three kinds by
/// four grades (<c>src/Engine/Objects/Actor.cpp:4226-4252</c>) — cases zero to two are the map's three
/// slots with the grade and the count both drawn, cases three to five are those slots graded A, six to
/// eight graded B, and nine to eleven graded C, each of which puts exactly one creature on the field.
/// The map table states, per slot, the kind of monster (its internal name), the difficulty its grade
/// odds are read at, and the range of creatures it spawns
/// (<c>src/Engine/Tables/MapTable.cpp:80-89</c>).
/// </para>
/// <para>
/// <b>What this reader decides: nothing about play.</b> Which grade a random slot's creature is and how many a
/// random slot puts on the field are the game's choices, and the ruleset makes them when it populates the place.
/// This reader carries the record's reading — the slot, the fixed grade when the record fixes one, the slot's
/// kind, difficulty and range — and the source join the choice needs: the monster rows the kind's graded
/// variants are, found by the monster table's own internal-name column.
/// </para>
/// <para>
/// <b>The monster row is joined by internal name.</b> A slot names a kind (<c>Dragonfly</c>) and the
/// monster table carries the graded variants (<c>Dragonfly A</c>), so a variant's row is the one whose own
/// internal-name column equals the slot's name plus the grade. A slot the map leaves empty, an index outside
/// the twelve, and a record none of whose possible variants the table carries are refusals named per record
/// rather than encounters that could resolve to nothing.
/// </para>
/// </remarks>
public static class PlaceEncounters
{
    /// <summary>The spawn record's object reference for an actor, which is what a creature comes from.</summary>
    private const int ActorObjectType = 3;

    /// <summary>The grades a slot's variants are named by, in the donor's own order.</summary>
    private static readonly string[] Grades = ["A", "B", "C"];

    /// <summary>Reads every place's encounters from the spawn records its decoded map carries.</summary>
    /// <param name="tables">The rule tables, which state each map's encounter slots and each monster row's name.</param>
    /// <param name="maps">The decoded maps, whose spawn records are what asks for an encounter.</param>
    /// <returns>Every encounter emitted, and every record nothing was emitted for with its reason.</returns>
    public static PlaceEncounterSummary Emit(Mm7Tables tables, IReadOnlyDictionary<int, DecodedMap> maps)
    {
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(maps);

        EncounterSlotReader slots = new(tables);
        List<PlaceEncounterPlacement> placements = [];
        List<PlaceEncounterRefusal> refusals = [];
        List<string> notes = [];
        int spawns = 0;
        int actors = 0;
        int treasures = 0;

        foreach (MapStatsRecord map in tables.Maps.Maps.OrderBy(map => map.Id))
        {
            if (!maps.TryGetValue(map.Id, out DecodedMap? decoded)) continue;
            foreach (MapSpawnPoint spawn in decoded.SpawnPoints)
            {
                spawns++;
                if (spawn.Type != ActorObjectType)
                {
                    treasures++;
                    continue;
                }

                actors++;
                int encounter = spawn.TreasureLevelOrMonsterIndex;
                if (slots.Read(map, encounter) is not { } read)
                {
                    (string code, string reason) = slots.Refusal(map, encounter);
                    refusals.Add(new PlaceEncounterRefusal(code, Subject(map, spawn), $"spawn {spawn.Index} {reason}"));
                    continue;
                }

                placements.Add(new PlaceEncounterPlacement(
                    map.Id,
                    $"encounter-{spawn.Index.ToString(CultureInfo.InvariantCulture)}",
                    spawn.Position.X,
                    spawn.Position.Y,
                    spawn.Position.Z,
                    spawn.Index,
                    encounter,
                    read.Slot,
                    read.FixedGrade,
                    read.MonsterKind,
                    read.Difficulty,
                    read.AppearMin,
                    read.AppearMax,
                    spawn.Group,
                    spawn.Attributes,
                    spawn.Radius,
                    read.Variants));
            }
        }

        if (spawns > 0)
        {
            PlaceEncounterSummary counted = new([.. placements], [], spawns, actors, treasures, []);
            notes.Add(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{spawns} spawn records were read: {actors} ask the level for an actor and {treasures} ask it for treasure. {placements.Count} encounters were emitted into {counted.PopulatedPlaces} places, and {refusals.Count} records were refused."));
            notes.Add(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{counted.DrawnGrades} encounters leave the grade to the map's odds and {counted.DrawnCounts} leave the count to the slot's range; the ruleset draws both when it populates a place, so these encounters resolve to between {counted.FewestCreatures} and {counted.MostCreatures} creatures."));
        }

        if (placements.Any(placement => placement.Attributes != 0))
        {
            notes.Add("Some spawn records state attribute bits; they are carried on the encounter as the record states them.");
        }

        return new PlaceEncounterSummary([.. placements], [.. refusals], spawns, actors, treasures, [.. notes]);
    }

    /// <summary>Reads one of a map's encounter numbers as the slot it names, or null when it names none this data can resolve.</summary>
    /// <remarks>
    /// The same reading a spawn record's number gets; a map event that summons monsters names its encounter the same
    /// way (the slot plus three times the grade, OpenEnroth <c>src/Engine/Evt/EvtInterpreter.cpp:88</c>).
    /// </remarks>
    /// <param name="tables">The rule tables, which state each map's encounter slots and each monster row's name.</param>
    /// <param name="placeId">The place whose map table row states the slots.</param>
    /// <param name="encounter">The number: one to twelve.</param>
    /// <param name="reason">Why it names nothing, when it does not; empty otherwise.</param>
    /// <returns>The slot, or null.</returns>
    public static PlaceEncounterSlot? Slot(Mm7Tables tables, int placeId, int encounter, out string reason)
    {
        ArgumentNullException.ThrowIfNull(tables);
        EncounterSlotReader slots = new(tables);
        reason = string.Empty;
        foreach (MapStatsRecord map in tables.Maps.Maps)
        {
            if (map.Id != placeId) continue;
            if (slots.Read(map, encounter) is { } read) return read;
            reason = slots.Refusal(map, encounter).Reason;
            return null;
        }

        reason = string.Create(CultureInfo.InvariantCulture, $"names encounter {encounter} of place {placeId}, which the map table does not state.");
        return null;
    }

    /// <summary>What a refusal is about, so an operator can find the record it names.</summary>
    private static string Subject(MapStatsRecord map, MapSpawnPoint spawn) =>
        string.Create(CultureInfo.InvariantCulture, $"place {map.Id} '{map.Name}' spawn {spawn.Index}");

    /// <summary>Reads a map's encounter numbers against the monster table, joined once by internal name.</summary>
    private sealed class EncounterSlotReader
    {
        private readonly Dictionary<string, MonsterRecord> _rows = new(StringComparer.OrdinalIgnoreCase);

        internal EncounterSlotReader(Mm7Tables tables)
        {
            // The internal name is how a slot's kind and a graded variant meet, so the join is built once
            // from the monster table's own column rather than searched per record. Two rows can share a
            // trimmed name — the shipped table carries three copies each of four arena beasts — and the
            // lowest id is taken, which is the first row the table states.
            foreach (MonsterRecord monster in tables.Monsters.Monsters)
            {
                string internalName = monster.Fields.Count > 2 ? monster.Fields[2].Trim() : string.Empty;
                if (internalName.Length == 0) continue;
                _rows.TryAdd(internalName, monster);
            }
        }

        /// <summary>The slot an encounter number reads as, or null when it names none this data resolves.</summary>
        internal PlaceEncounterSlot? Read(MapStatsRecord map, int encounter)
        {
            if (encounter < 1 || encounter > 12) return null;
            int slotIndex = (encounter - 1) % 3;
            int gradeIndex = (encounter - 1) / 3;
            EncounterSlot slot = map.Slots[slotIndex];
            if (slot.IsEmpty) return null;
            string kind = slot.Monster.Trim();
            string? fixedGrade = gradeIndex == 0 ? null : Grades[gradeIndex - 1];
            List<PlaceEncounterVariant> variants = [];
            foreach (string grade in Grades)
            {
                if (_rows.TryGetValue($"{kind} {grade}", out MonsterRecord row)) variants.Add(new PlaceEncounterVariant(grade, row.Id, row.Name));
            }

            bool resolvable = fixedGrade is null ? variants.Count > 0 : variants.Any(variant => variant.Grade == fixedGrade);
            return resolvable
                ? new PlaceEncounterSlot(encounter, slotIndex + 1, fixedGrade, kind, slot.Difficulty, slot.Minimum, slot.Maximum, [.. variants])
                : null;
        }

        /// <summary>Why an encounter number names nothing, as a code and a clause that follows what named it.</summary>
        internal (string Code, string Reason) Refusal(MapStatsRecord map, int encounter)
        {
            if (encounter < 1 || encounter > 12)
            {
                return ("spawn-encounter-unknown", $"names encounter {encounter}, and a level's encounter slots are one to twelve.");
            }

            int slotIndex = (encounter - 1) % 3;
            int gradeIndex = (encounter - 1) / 3;
            EncounterSlot slot = map.Slots[slotIndex];
            if (slot.IsEmpty)
            {
                return ("spawn-encounter-empty", $"names encounter slot {slotIndex + 1} of '{map.Name}', which states no monster.");
            }

            string kind = slot.Monster.Trim();
            return gradeIndex == 0
                ? ("spawn-monster-unknown", $"names encounter slot {slotIndex + 1} of '{map.Name}', which spawns '{kind}', and the monster table carries none of its graded variants.")
                : ("spawn-monster-unknown", $"names encounter slot {slotIndex + 1} of '{map.Name}', which spawns '{kind} {Grades[gradeIndex - 1]}', and the monster table carries no such row.");
        }
    }
}
