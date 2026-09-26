using System.Globalization;
using MightAndMagic7.Import.Maps;
using MightAndMagic7.Import.Tables;

namespace MightAndMagic7.Import.Packs;

/// <summary>One creature standing in a place, emitted from the spawn record that puts it there.</summary>
/// <remarks>
/// <para>
/// A creature is not what the shipped record says: the record names one of the map's three encounter
/// slots, the grade is drawn at runtime from the map's difficulty odds, and the count is drawn from the
/// slot's own range. What this emitter carries is one deterministic reading of all three, with every
/// part of that reading recorded beside the creature so the operator can see what was chosen and why.
/// </para>
/// <para>
/// The position is the spawn record's own point. A spawn that puts several creatures on the field puts
/// the first at the point and spreads the rest on a circle of the record's own radius: the donor stacks
/// them at the point exactly (<c>OpenEnroth src/Engine/Objects/Actor.cpp:4333-4341</c>, which computes an
/// offset and never uses it), and a place whose actors have no renderer or radius here would otherwise
/// hold several creatures that cannot be told apart.
/// </para>
/// </remarks>
/// <param name="PlaceId">The place the creature stands in.</param>
/// <param name="PlacementId">
/// The placement's identity within the place. It is prefixed with the placement kind the packs write a
/// creature under, which is the contract the ruleset reads a creature from.
/// </param>
/// <param name="MonsterId">The monster table row the creature is, which content names in its <c>monster</c> field.</param>
/// <param name="MonsterName">The row's name, so a report reads without joining the table.</param>
/// <param name="X">Where the creature stands along the place's first axis.</param>
/// <param name="Y">Where the creature stands along the place's second axis.</param>
/// <param name="Z">Where the creature stands in height.</param>
/// <param name="Yaw">Which way the creature faces, in the game's own angle units.</param>
/// <param name="SourceSpawnIndex">The spawn record's index in its map's own spawn array.</param>
/// <param name="EncounterIndex">The slot number the record names: one to twelve.</param>
/// <param name="Grade">The graded variant the creature is: <c>A</c>, <c>B</c>, or <c>C</c>.</param>
/// <param name="Quantity">How many creatures the record put on the field, which this placement is one of.</param>
/// <param name="Unit">Which of that many this placement is, counted from zero.</param>
/// <param name="Group">The spawn record's own group, zero when it belongs to none.</param>
/// <param name="Attributes">The spawn record's own attribute bits, kept as the record states them.</param>
/// <param name="Radius">The radius the record states around its point.</param>
/// <param name="AppearMin">The fewest creatures the slot states, zero when it states no count.</param>
/// <param name="AppearMax">The most creatures the slot states, zero when it states no count.</param>
/// <param name="GradeDrawn">Whether the grade came from the map's difficulty odds rather than the record's own slot.</param>
/// <param name="CountDrawn">Whether the count came from the slot's range rather than the donor's fixed one.</param>
public sealed record PlaceCreaturePlacement(
    int PlaceId,
    string PlacementId,
    int MonsterId,
    string MonsterName,
    double X,
    double Y,
    double Z,
    double Yaw,
    int SourceSpawnIndex,
    int EncounterIndex,
    string Grade,
    int Quantity,
    int Unit,
    uint Group,
    int Attributes,
    int Radius,
    int AppearMin,
    int AppearMax,
    bool GradeDrawn,
    bool CountDrawn);

/// <summary>One spawn record, or one whole place, the import could put no creature on the field for.</summary>
/// <param name="Code">A short stable code for the kind of refusal.</param>
/// <param name="Subject">What the refusal is about, so a reader can find it.</param>
/// <param name="Reason">Why no creature was emitted, in terms a person can act on.</param>
public sealed record PlaceCreatureRefusal(string Code, string Subject, string Reason);

/// <summary>What one import's creature derivation produced.</summary>
/// <param name="Placements">Every creature emitted, in place and spawn order.</param>
/// <param name="Refusals">Every record nothing was emitted for, with its reason.</param>
/// <param name="SpawnRecords">How many spawn records the decoded maps carry altogether.</param>
/// <param name="ActorSpawns">How many of them ask the level for an actor, which is what a creature can come from.</param>
/// <param name="TreasureSpawns">How many of them ask the level for treasure instead.</param>
/// <param name="Notes">What the read noticed about the data, for a report.</param>
public sealed record PlaceCreatureSummary(
    IReadOnlyList<PlaceCreaturePlacement> Placements,
    IReadOnlyList<PlaceCreatureRefusal> Refusals,
    int SpawnRecords,
    int ActorSpawns,
    int TreasureSpawns,
    IReadOnlyList<string> Notes)
{
    /// <summary>An import that read no maps, so nothing was derived.</summary>
    public static PlaceCreatureSummary Empty { get; } = new([], [], 0, 0, 0, []);

    /// <summary>How many creatures the import puts on the field.</summary>
    public int CreatureCount => Placements.Count;

    /// <summary>How many places hold at least one creature.</summary>
    public int PopulatedPlaces => Placements.Select(placement => placement.PlaceId).Distinct().Count();

    /// <summary>How many distinct monster rows are placed.</summary>
    public int DistinctMonsters => Placements.Select(placement => placement.MonsterId).Distinct().Count();

    /// <summary>The creatures each place holds, keyed by place id.</summary>
    public IReadOnlyDictionary<int, int> PerPlace
    {
        get
        {
            SortedDictionary<int, int> counts = [];
            foreach (PlaceCreaturePlacement placement in Placements)
            {
                counts[placement.PlaceId] = counts.GetValueOrDefault(placement.PlaceId) + 1;
            }

            return counts;
        }
    }

    /// <summary>Why creatures were not emitted, counted by the reason's own code.</summary>
    public IReadOnlyDictionary<string, int> RefusalCodes
    {
        get
        {
            SortedDictionary<string, int> counts = new(StringComparer.Ordinal);
            foreach (PlaceCreatureRefusal refusal in Refusals)
            {
                counts[refusal.Code] = counts.GetValueOrDefault(refusal.Code) + 1;
            }

            return counts;
        }
    }
}

/// <summary>
/// Puts the creatures a level's spawn records ask for on the field, deterministically and with
/// provenance.
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
/// <b>What this emitter reads deterministically.</b> A record whose index names a graded slot takes
/// that grade and one creature. A record naming a random slot takes the grade the map's own difficulty
/// odds favour — the largest of the donor's three weights for that difficulty
/// (<c>src/Engine/Objects/Actor.cpp:63</c>, <c>word_4E8152</c>, read at
/// <c>Actor.cpp:4291-4311</c>) — and the fewest creatures the slot states, which is the floor the data
/// gives rather than an average it never states. Both readings travel with the creature, and the count
/// it was read from travels with it too, so a report can say exactly what was chosen.
/// </para>
/// <para>
/// <b>The monster row is joined by internal name.</b> A slot names a kind (<c>Dragonfly</c>) and the
/// monster table carries the graded variants (<c>Dragonfly A</c>), so the row is the one whose own
/// internal-name column equals the slot's name plus the grade. A slot whose row the table does not
/// carry, a slot the map leaves empty, and an index outside the twelve are refusals named per record
/// rather than creatures invented to fill them.
/// </para>
/// </remarks>
public static class PlaceCreatures
{
    /// <summary>The spawn record's object reference for an actor, which is what a creature comes from.</summary>
    private const int ActorObjectType = 3;

    /// <summary>The grades a slot's variants are named by, in the donor's own order.</summary>
    private static readonly string[] Grades = ["A", "B", "C"];

    /// <summary>
    /// The donor's grade odds: three weights per difficulty, one for each graded variant.
    /// </summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/Objects/Actor.cpp:63</c> — <c>word_4E8152</c>, indexed as
    /// <c>3 * monsterCategoryOddsSet</c> where the set is the map's own <c>Dif</c> column plus any
    /// modifier, capped at five (<c>Actor.cpp:4288-4289</c>). Difficulty zero is stated by no shipped
    /// map and would leave every grade equally unlikely, so it is read as the flat row the donor's own
    /// zero row would give.
    /// </remarks>
    private static readonly int[][] GradeOdds =
    [
        [1, 1, 1],
        [90, 8, 2],
        [70, 20, 10],
        [50, 30, 20],
        [30, 40, 30],
        [10, 50, 40],
    ];

    /// <summary>Emits every place's creatures from the spawn records its decoded map carries.</summary>
    /// <param name="tables">The rule tables, which state each map's encounter slots and each monster row's name.</param>
    /// <param name="maps">The decoded maps, whose spawn records are what asks for a creature.</param>
    /// <returns>Every creature emitted, and every record nothing was emitted for with its reason.</returns>
    public static PlaceCreatureSummary Emit(Mm7Tables tables, IReadOnlyDictionary<int, DecodedMap> maps)
    {
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(maps);

        // The internal name is how a slot's kind and a graded variant meet, so the join is built once
        // from the monster table's own column rather than searched per record. Two rows can share a
        // trimmed name — the shipped table carries three copies each of four arena beasts — and the
        // lowest id is taken, which is the first row the table states.
        Dictionary<string, MonsterRecord> rowsByInternalName = new(StringComparer.OrdinalIgnoreCase);
        foreach (MonsterRecord monster in tables.Monsters.Monsters)
        {
            string internalName = monster.Fields.Count > 2 ? monster.Fields[2].Trim() : string.Empty;
            if (internalName.Length == 0) continue;
            if (!rowsByInternalName.ContainsKey(internalName)) rowsByInternalName[internalName] = monster;
        }

        List<PlaceCreaturePlacement> placements = [];
        List<PlaceCreatureRefusal> refusals = [];
        List<string> notes = [];
        int spawns = 0;
        int actors = 0;
        int treasures = 0;
        int spanning = 0;

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
                if (encounter < 1 || encounter > 12)
                {
                    refusals.Add(new PlaceCreatureRefusal(
                        "spawn-encounter-unknown",
                        Subject(map, spawn),
                        $"spawn {spawn.Index} names encounter {encounter}, and a level's encounter slots are one to twelve."));
                    continue;
                }

                int slotIndex = (encounter - 1) % 3;
                int gradeIndex = (encounter - 1) / 3;
                EncounterSlot slot = map.Slots[slotIndex];
                if (slot.IsEmpty)
                {
                    refusals.Add(new PlaceCreatureRefusal(
                        "spawn-encounter-empty",
                        Subject(map, spawn),
                        $"spawn {spawn.Index} names encounter slot {slotIndex + 1} of '{map.Name}', which states no monster."));
                    continue;
                }

                bool gradeDrawn = gradeIndex == 0;
                string grade = gradeDrawn ? Favourite(slot.Difficulty) : Grades[gradeIndex - 1];
                if (!rowsByInternalName.TryGetValue($"{slot.Monster.Trim()} {grade}", out MonsterRecord row))
                {
                    refusals.Add(new PlaceCreatureRefusal(
                        "spawn-monster-unknown",
                        Subject(map, spawn),
                        $"spawn {spawn.Index} names encounter slot {slotIndex + 1} of '{map.Name}', which spawns '{slot.Monster.Trim()} {grade}', and the monster table carries no such row."));
                    continue;
                }

                bool countDrawn = gradeDrawn && slot.HasCount;
                int quantity = countDrawn ? slot.Minimum : 1;
                if (quantity > 1) spanning++;
                for (int unit = 0; unit < quantity; unit++)
                {
                    (double x, double y) = Spread(spawn, quantity, unit);
                    placements.Add(new PlaceCreaturePlacement(
                        map.Id,
                        $"monster-{spawn.Index.ToString(CultureInfo.InvariantCulture)}-{unit.ToString(CultureInfo.InvariantCulture)}",
                        row.Id,
                        row.Name,
                        x,
                        y,
                        spawn.Position.Z,
                        // A spawn record states no facing: the donor drops the monster on its point and
                        // leaves its heading to the model, so a creature faces the place's own zero until
                        // something turns it.
                        0,
                        spawn.Index,
                        encounter,
                        grade,
                        quantity,
                        unit,
                        spawn.Group,
                        spawn.Attributes,
                        spawn.Radius,
                        slot.Minimum,
                        slot.Maximum,
                        gradeDrawn,
                        countDrawn));
                }
            }
        }

        if (spawns > 0)
        {
            notes.Add(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{spawns} spawn records were read: {actors} ask the level for an actor and {treasures} ask it for treasure. {placements.Count} creatures were emitted into {placements.Select(placement => placement.PlaceId).Distinct().Count()} places, and {refusals.Count} records were refused."));
        }

        if (spanning > 0)
        {
            notes.Add(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{spanning} spawn records state a random encounter slot, whose count the donor draws from the slot's own range; this import takes the floor of that range, which is the fewest the data states."));
        }

        if (placements.Any(placement => placement.Attributes != 0))
        {
            notes.Add("Some spawn records state attribute bits; they are carried on the creature as the record states them.");
        }

        return new PlaceCreatureSummary([.. placements], [.. refusals], spawns, actors, treasures, [.. notes]);
    }

    /// <summary>
    /// Where one of a spawn's creatures stands: the record's own point for the first, and a point on a
    /// circle of the record's own radius for the rest.
    /// </summary>
    /// <remarks>
    /// The offset is derived from the creature's position in its record's own sequence, so a record that
    /// puts several creatures on the field puts them in the same spots every import. A record whose
    /// radius is zero states no spread and stacks its creatures, which is exactly what the donor does
    /// with every record.
    /// </remarks>
    private static (double X, double Y) Spread(MapSpawnPoint spawn, int quantity, int unit)
    {
        if (unit == 0 || quantity <= 1 || spawn.Radius <= 0) return (spawn.Position.X, spawn.Position.Y);
        double angle = 2 * Math.PI * unit / quantity;
        return (
            spawn.Position.X + (spawn.Radius * Math.Cos(angle)),
            spawn.Position.Y + (spawn.Radius * Math.Sin(angle)));
    }

    /// <summary>The graded variant the map's own difficulty odds favour, ties going to the earlier grade.</summary>
    private static string Favourite(int difficulty)
    {
        int[] odds = difficulty >= 1 && difficulty < GradeOdds.Length ? GradeOdds[difficulty] : GradeOdds[0];
        int best = 0;
        for (int index = 1; index < odds.Length; index++)
        {
            if (odds[index] > odds[best]) best = index;
        }

        return Grades[best];
    }

    /// <summary>What a refusal is about, so an operator can find the record it names.</summary>
    private static string Subject(MapStatsRecord map, MapSpawnPoint spawn) =>
        string.Create(CultureInfo.InvariantCulture, $"place {map.Id} '{map.Name}' spawn {spawn.Index}");
}
