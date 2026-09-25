using MightAndMagic7.Import.Events;
using MightAndMagic7.Import.Lod;
using MightAndMagic7.Import.Maps;
using MightAndMagic7.Import.Tables;

namespace MightAndMagic7.Import.Packs;

/// <summary>One container a place holds, with where it stands and what it holds.</summary>
/// <remarks>
/// <para>
/// A chest record stores no position, so the position here is derived and the record says what from: the
/// mean of the bounding-box centres of the map faces whose event opens this chest, which is the donor's
/// own reading (<c>src/Engine/Objects/Chest.cpp:370-407</c>). The number of faces and how far they spread
/// from that mean are written beside it, because a mean of faces that are nowhere near each other is a
/// number that would pass for a position — the donor calls that a wormhole chest and refuses to place it,
/// and this emitter does the same instead of inventing a point between two rooms.
/// </para>
/// <para>
/// The two trap numbers travel with the container rather than with the place because they are what a use
/// of the container asks about, and because a target's answer is read from the placement it stands on:
/// a ruleset never sees a place's own fields, so a number it must have travels with the thing that needs
/// it. They are the place's own row of the per-map table, copied unchanged.
/// </para>
/// </remarks>
/// <param name="PlaceId">The place the container stands in.</param>
/// <param name="FileName">The map file the place was decoded from, for a report.</param>
/// <param name="ChestIndex">The container's index in the map's own container array, which is the record's index in the delta.</param>
/// <param name="X">The container's derived position along the place's first axis.</param>
/// <param name="Y">The container's derived position along the place's second axis.</param>
/// <param name="Z">The container's derived position in height.</param>
/// <param name="SourceFaceCount">How many faces of the map open this container.</param>
/// <param name="FaceSpread">How far the furthest of those faces' centres stands from the derived position.</param>
/// <param name="Chest">The chest record itself, with its type, flags, and item references.</param>
/// <param name="TrapDifficulty">The place's own trap difficulty, copied from the per-map table.</param>
/// <param name="TrapDamageDice">The place's own trap damage, as a count of twenty-sided dice, copied from the per-map table.</param>
public sealed record PlaceChestPlacement(
    int PlaceId,
    string FileName,
    int ChestIndex,
    double X,
    double Y,
    double Z,
    int SourceFaceCount,
    double FaceSpread,
    MapChest Chest,
    int TrapDifficulty,
    int TrapDamageDice);

/// <summary>One sprite object a place holds.</summary>
/// <remarks>
/// Unlike a chest a sprite object stores its own position, so nothing here is derived: the record is
/// emitted as it stands, and whether the object is anything a party can reach for is decided from what it
/// contains rather than from what it is drawn as.
/// </remarks>
/// <param name="PlaceId">The place the object stands in.</param>
/// <param name="FileName">The map file the place was decoded from, for a report.</param>
/// <param name="Object">The object itself.</param>
public sealed record PlaceSpriteObjectPlacement(int PlaceId, string FileName, MapSpriteObject Object);

/// <summary>One container the import could not place, with the reason it could not.</summary>
/// <param name="PlaceId">The place whose event names the container.</param>
/// <param name="FileName">The map file the place was decoded from.</param>
/// <param name="ChestIndex">The container's index in the map's own array, or null when the event names none.</param>
/// <param name="Code">A short stable code for the kind of refusal.</param>
/// <param name="Reason">Why the container cannot be placed, in terms a person can act on.</param>
public sealed record PlaceContainerRefusal(int PlaceId, string FileName, int? ChestIndex, string Code, string Reason);

/// <summary>The trap numbers one place's own row of the per-map table declares.</summary>
/// <param name="Difficulty">The place's disarm difficulty, which a trap's check is made against.</param>
/// <param name="DamageDice">How many twenty-sided dice the place's traps roll for damage.</param>
public readonly record struct PlaceTrapNumbers(int Difficulty, int DamageDice);

/// <summary>What one import's container derivation produced, over every map it decoded.</summary>
/// <param name="Chests">Every chest an event face places, in place and container order.</param>
/// <param name="SpriteObjects">Every sprite object the deltas carry, in place and record order.</param>
/// <param name="Refusals">Every container the derivation could not place, with its reason.</param>
/// <param name="UnplacedRecords">
/// How many chest records no face of their own place opens. A delta stores the runtime's whole container
/// array whether or not a map uses it, so an unopened record is a slot rather than a container and is
/// counted here instead of being placed.
/// </param>
public sealed record PlaceContainerSummary(
    IReadOnlyList<PlaceChestPlacement> Chests,
    IReadOnlyList<PlaceSpriteObjectPlacement> SpriteObjects,
    IReadOnlyList<PlaceContainerRefusal> Refusals,
    int UnplacedRecords)
{
    /// <summary>An import that derived no containers at all, such as one that decoded no maps.</summary>
    public static PlaceContainerSummary Empty { get; } = new([], [], [], 0);

    /// <summary>How many places hold a container a face opens.</summary>
    public int PlaceCount => Chests.Select(chest => chest.PlaceId).Distinct().Count();

    /// <summary>How many containers the writer emits.</summary>
    public int ContainerCount => Chests.Count;

    /// <summary>How many sprite objects the writer emits.</summary>
    public int SpriteObjectCount => SpriteObjects.Count;

    /// <summary>How many containers hold anything at all.</summary>
    public int StockedCount => Chests.Count(chest => chest.Chest.Items.Count > 0);

    /// <summary>How many containers are trapped, as their own record states it.</summary>
    public int TrappedCount => Chests.Count(chest => chest.Chest.IsTrapped);

    /// <summary>How many item references the containers hold between them.</summary>
    public int ItemReferenceCount => Chests.Sum(chest => chest.Chest.Items.Count);

    /// <summary>How many of those references ask for a random item rather than naming one.</summary>
    public int RandomItemReferenceCount => Chests.Sum(chest => chest.Chest.Items.Count(item => item.IsRandom));

    /// <summary>How many sprite objects hold an item a party could take.</summary>
    public int StockedSpriteObjectCount => SpriteObjects.Count(placement => placement.Object.ContainingItemId != 0);
}

/// <summary>
/// Derives a place's containers from the faces that open them, and its loose items from the objects the
/// delta carries.
/// </summary>
/// <remarks>
/// <para>
/// <b>Where a container's position comes from.</b> A chest record is contents and state, not a place: the
/// donor positions a chest from the faces whose event is an <c>OpenChest</c> naming it, taking each face's
/// bounding-box centre and averaging them (<c>src/Engine/Objects/Chest.cpp:370-407</c>). This emitter does
/// the same, from the map faces and the map's own event program, and refuses a chest whose faces are
/// spread further than the donor's own 256-unit limit rather than averaging a point between two rooms.
/// </para>
/// <para>
/// <b>What it does not decide.</b> A container's items are emitted exactly as the record stores them,
/// including the negative identifiers that ask for a random item, and its flag word is emitted raw. What a
/// chest's flags mean, what an identifier resolves to, and what opening one yields are answers about this
/// game, and they belong to the ruleset that reads the placement.
/// </para>
/// <para>
/// <b>Sprite objects are not containers.</b> A shipped delta's initial objects are the items lying in the
/// map, each holding one item, and they are emitted as the objects they are; a party that reaches for one
/// is searching a pile, which is the same transfer as a chest's and not a second mechanism. Objects that
/// hold nothing are emitted too, so the map's own state is not silently trimmed; nothing answers for them.
/// </para>
/// </remarks>
public static class PlaceContainerEmitter
{
    /// <summary>
    /// How far a container's opening faces may stand from their mean before the container is refused, in
    /// place units. It is the donor's own limit for a chest that is not where its faces are
    /// (OpenEnroth <c>src/Engine/Objects/Chest.cpp:398-400</c>, half an outdoor tile).
    /// </summary>
    public const double FaceSpreadLimit = 256;

    /// <summary>Derives every place's containers, and records every one that has none.</summary>
    /// <param name="maps">The decoded maps, keyed by the place id the map table gives them.</param>
    /// <param name="programs">Every event program the installation carries, which is where a container opening is.</param>
    /// <param name="traps">
    /// Every place's trap numbers, from its own row of the per-map table. A place that holds containers and
    /// has no numbers here is a defect rather than a container with no traps, because writing zeroes would
    /// state that its chests are harmless.
    /// </param>
    /// <exception cref="ArgumentNullException">The map lookup, the programs, or the trap numbers are null.</exception>
    /// <exception cref="Lod.LodFormatException">A place with containers has no trap numbers.</exception>
    public static PlaceContainerSummary Emit(
        IReadOnlyDictionary<int, DecodedMap> maps,
        IReadOnlyList<EvtProgram> programs,
        IReadOnlyDictionary<int, PlaceTrapNumbers> traps)
    {
        ArgumentNullException.ThrowIfNull(maps);
        ArgumentNullException.ThrowIfNull(programs);
        ArgumentNullException.ThrowIfNull(traps);

        Dictionary<string, EvtProgram> byStem = Programs(programs);
        List<PlaceChestPlacement> chests = [];
        List<PlaceSpriteObjectPlacement> spriteObjects = [];
        List<PlaceContainerRefusal> refusals = [];
        int unplaced = 0;
        foreach ((int placeId, DecodedMap decoded) in maps.OrderBy(entry => entry.Key))
        {
            string fileName = decoded.FileName;
            if (decoded.Delta is not { } delta)
            {
                refusals.Add(new PlaceContainerRefusal(
                    placeId,
                    fileName,
                    null,
                    "delta-not-decoded",
                    $"Place {placeId} was decoded without its delta, so the containers it carries were never read."));
                continue;
            }

            // An object's own position is the record's, so nothing about it depends on the map's events: a
            // place emits the items lying in it whether or not its program was read.
            foreach (MapSpriteObject held in delta.SpriteObjects)
            {
                spriteObjects.Add(new PlaceSpriteObjectPlacement(placeId, fileName, held));
            }

            // A container's position comes from the faces that open it, which is a fact about the map's
            // event program; a place whose program the import does not hold has no face that can be
            // matched to a container, and saying so is better than placing nothing quietly.
            if (!byStem.TryGetValue(Stem(fileName), out EvtProgram? program))
            {
                refusals.Add(new PlaceContainerRefusal(
                    placeId,
                    fileName,
                    null,
                    "program-not-found",
                    $"No event program named after '{fileName}' is in the installation, so nothing states which of place {placeId}'s faces opens a container."));
                unplaced += delta.ChestCount;
                continue;
            }

            Dictionary<int, List<(double X, double Y, double Z)>> facesByChest = OpeningFaces(program, decoded);
            if (!traps.TryGetValue(placeId, out PlaceTrapNumbers place))
            {
                throw new LodFormatException(
                    $"Place {placeId} ('{fileName}') holds containers and no trap numbers, so its traps could only be written as harmless.");
            }

            // Every record of a map's container array is walked, whether or not an event names it, so the
            // report can say how many of them are slots rather than containers.
            foreach (MapChest chest in delta.Chests)
            {
                if (!facesByChest.TryGetValue(chest.Index, out List<(double X, double Y, double Z)>? points) || points.Count == 0)
                {
                    unplaced++;
                    continue;
                }

                (double x, double y, double z) = Mean(points);
                double spread = points.Max(point => Distance(point, (x, y, z)));
                if (spread > FaceSpreadLimit)
                {
                    refusals.Add(new PlaceContainerRefusal(
                        placeId,
                        fileName,
                        chest.Index,
                        "faces-spread",
                        $"The {points.Count} faces opening container {chest.Index} of place {placeId} spread {spread:0.#} units from their mean, past the {FaceSpreadLimit:0} a container's faces may spread: their mean is not a place the container stands."));
                    continue;
                }

                chests.Add(new PlaceChestPlacement(placeId, fileName, chest.Index, x, y, z, points.Count, spread, chest, place.Difficulty, place.DamageDice));
            }

            // A chest id an event names but the delta has no record for is a broken reference rather than a
            // container, and it is named so the operator can see which map and which id.
            foreach ((int chestIndex, List<(double X, double Y, double Z)> points) in facesByChest)
            {
                if (chestIndex < delta.ChestCount) continue;
                refusals.Add(new PlaceContainerRefusal(
                    placeId,
                    fileName,
                    chestIndex,
                    "record-missing",
                    $"{points.Count} face(s) of place {placeId} open container {chestIndex}, and the delta carries {delta.ChestCount} containers, so there is nothing to open."));
            }

        }

        return new PlaceContainerSummary(chests, spriteObjects, refusals, unplaced);
    }

    /// <summary>The event programs by the map file stem they belong to.</summary>
    /// <remarks>
    /// An event program is named after the map it belongs to, so a program's own name is what pairs it with
    /// a decoded map. Two programs claiming one stem would make every container's position a coin toss, so
    /// that is a defect of the installation rather than something to resolve here.
    /// </remarks>
    private static Dictionary<string, EvtProgram> Programs(IReadOnlyList<EvtProgram> programs)
    {
        Dictionary<string, EvtProgram> byStem = new(StringComparer.OrdinalIgnoreCase);
        foreach (EvtProgram program in programs)
        {
            string stem = Stem(program.Name);
            if (!byStem.TryAdd(stem, program))
            {
                throw new Lod.LodFormatException(
                    $"'{program.Name}' and '{byStem[stem].Name}' are both the event program of map '{stem}', so a container's position cannot be attributed to one of them.");
            }
        }

        return byStem;
    }

    /// <summary>
    /// Every face that opens a container, grouped by the container it opens.
    /// </summary>
    /// <remarks>
    /// A face opens a container when the event it raises holds an <c>OpenChest</c> instruction naming it.
    /// One event may name more than one container — the shipped programs open one chest or another
    /// depending on an earlier step — and the donor positions every container such an event names, so both
    /// are placed at the same face here too rather than one of them being dropped.
    /// </remarks>
    private static Dictionary<int, List<(double X, double Y, double Z)>> OpeningFaces(EvtProgram program, DecodedMap map)
    {
        Dictionary<ushort, List<int>> openedByEvent = [];
        foreach (EvtInstruction instruction in program.Instructions)
        {
            if (!instruction.TryReadOpenChest(out OpenChestInstruction open)) continue;
            if (!openedByEvent.TryGetValue(instruction.EventId, out List<int>? opened)) openedByEvent[instruction.EventId] = opened = [];
            if (!opened.Contains(open.ContainerId)) opened.Add(open.ContainerId);
        }

        Dictionary<int, List<(double X, double Y, double Z)>> byChest = [];
        if (openedByEvent.Count == 0) return byChest;

        foreach ((int _, MapFace face, int _, string _) in MapFaceList.Flatten(map))
        {
            if (face.EventId == 0) continue;
            if (!openedByEvent.TryGetValue((ushort)face.EventId, out List<int>? opened)) continue;
            (double X, double Y, double Z) centre = MapFaceList.BoxCentre(face);
            foreach (int chestIndex in opened)
            {
                if (!byChest.TryGetValue(chestIndex, out List<(double X, double Y, double Z)>? points)) byChest[chestIndex] = points = [];
                points.Add(centre);
            }
        }

        return byChest;
    }

    /// <summary>The mean of the faces that open one container, in a fixed order so it is reproducible.</summary>
    private static (double X, double Y, double Z) Mean(List<(double X, double Y, double Z)> points)
    {
        double x = 0;
        double y = 0;
        double z = 0;
        foreach ((double px, double py, double pz) in points)
        {
            x += px;
            y += py;
            z += pz;
        }

        return (x / points.Count, y / points.Count, z / points.Count);
    }

    private static double Distance((double X, double Y, double Z) point, (double X, double Y, double Z) from)
    {
        double dx = point.X - from.X;
        double dy = point.Y - from.Y;
        double dz = point.Z - from.Z;
        return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }

    private static string Stem(string fileName) => Path.GetFileNameWithoutExtension(fileName);
}

/// <summary>
/// Reads every place's trap numbers from the per-map table's own columns.
/// </summary>
/// <remarks>
/// <para>
/// The table's column 9 is a map's disarm difficulty and column 10 is how many twenty-sided dice its traps
/// roll, in the donor's own reading (OpenEnroth <c>src/Engine/Tables/MapTable.cpp:74-75</c>,
/// <c>tokens[9]</c> and <c>tokens[10]</c>). Both are read here rather than from the typed per-map record
/// because a container is their only consumer: the trap belongs to the container's own use, so the numbers
/// travel with the container instead of widening every reader of the map table.
/// </para>
/// <para>
/// The row is found by the map id in the table's own first column rather than by position, so a table whose
/// rows were reordered cannot silently give one place another's traps.
/// </para>
/// </remarks>
public static class PlaceTrapNumbersTable
{
    /// <summary>Reads every place's trap numbers, keyed by the place's id.</summary>
    /// <param name="tables">The tables the places were read from.</param>
    /// <exception cref="ArgumentNullException">The tables are null.</exception>
    /// <exception cref="LodFormatException">A map row cannot be read, or two rows claim one place id.</exception>
    public static IReadOnlyDictionary<int, PlaceTrapNumbers> Read(Mm7Tables tables)
    {
        ArgumentNullException.ThrowIfNull(tables);
        Dictionary<int, PlaceTrapNumbers> numbers = [];
        foreach (TabularRow row in tables.Maps.Table.Rows)
        {
            int id = TableValue.Integer(tables.Maps.Table, row, 0, "#");
            PlaceTrapNumbers place = new(
                TableValue.Integer(tables.Maps.Table, row, 9, "0-20 (disarm)"),
                TableValue.Integer(tables.Maps.Table, row, 10, "0-10 (trap damage)"));
            if (!numbers.TryAdd(id, place))
            {
                throw new LodFormatException(
                    $"{tables.Maps.Table.Source}: place {id} has more than one row, so its traps have two difficulties.");
            }
        }

        return numbers;
    }
}
