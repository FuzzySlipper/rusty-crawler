using System.Globalization;
using MightAndMagic7.Import.Events;
using MightAndMagic7.Import.Maps;
using MightAndMagic7.Import.Tables;

namespace MightAndMagic7.Import.Packs;

/// <summary>One building row the importer turns into a service definition.</summary>
/// <remarks>
/// <para>
/// Everything here is the building table's own column, carried unchanged and named after the column it came
/// from, with the map the row stands on resolved from the table's map id to the place the map table gives
/// that id. A row that names a service but leaves its multiplier, its hours, or its stock interval empty
/// carries that absence as a null: a weapon shop and a training hall do not have the same columns filled,
/// and inventing a number for the empty one would be inventing content.
/// </para>
/// <para>
/// <b>What is deliberately not here.</b> Which operations a counter offers, what its shelves hold, what it
/// teaches, what it charges, and what its cures remove are this game's answers about a kind rather than
/// columns of the table, so they belong to the ruleset and are not written into the pack. The pack carries
/// what the operator's data carries, and the ruleset decides what it means.
/// </para>
/// </remarks>
/// <param name="BuildingId">The building's id, which is the row's own <c>#</c> column and the pack entry's id.</param>
/// <param name="TypeSequence">The building's position among buildings of its own type, as the table states it.</param>
/// <param name="Kind">The table's own type string, which content calls the service kind.</param>
/// <param name="Name">The building's name, as a person reads it on the sign.</param>
/// <param name="Proprietor">The proprietor's name, empty when the row names nobody.</param>
/// <param name="Title">The proprietor's title, empty when the row states none.</param>
/// <param name="MapId">The map the building stands on, which is the region its door is walked into.</param>
/// <param name="PlaceId">The place the map id resolves to, as the pack's places document names it.</param>
/// <param name="PriceMultiplier">The shop's price multiplier, absent on rows that carry none.</param>
/// <param name="SkillPriceMultiplier">The shop's skill and spell price multiplier, absent on rows that are not shops.</param>
/// <param name="StockIntervalDays">Days between stock refreshes, absent on rows with no stock.</param>
/// <param name="TrainingCapText">The training cap column exactly as the table stores it, empty when it stores none.</param>
/// <param name="TrainingCap">The training cap when the column is a number, null when it is not.</param>
/// <param name="OpenHour">The hour the building opens, absent on rows that state no hours.</param>
/// <param name="ClosedHour">The hour the building closes, absent on rows that state no hours.</param>
/// <param name="SourceRow">The row's position in the table, so a reader can follow a definition back to it.</param>
public sealed record PlaceServiceDefinition(
    int BuildingId,
    int? TypeSequence,
    string Kind,
    string Name,
    string Proprietor,
    string Title,
    int MapId,
    string PlaceId,
    double? PriceMultiplier,
    double? SkillPriceMultiplier,
    int? StockIntervalDays,
    string TrainingCapText,
    int? TrainingCap,
    int? OpenHour,
    int? ClosedHour,
    int SourceRow);

/// <summary>One counter standing in a place: where it is reached, and which building it serves.</summary>
/// <remarks>
/// <para>
/// The building table states which map a building is on and nothing about where in that map its door is.
/// The door is a fact of the map: the donor hangs a house's event on the faces of the building's own model
/// and opens the house when the party clicks one (OpenEnroth <c>src/Engine/Evt/EvtEnums.h:9</c>,
/// <c>EVENT_SpeakInHouse</c>, and <c>src/Engine/Evt/EvtInstruction.cpp:905-907</c>, which reads the house
/// id the face's event names). This emitter reads the same fact out of the operator's own maps: the faces
/// whose event is a <c>SpeakInHouse</c> naming this building, and the point the party stands at to use it.
/// </para>
/// <para>
/// <b>The point is chosen and said so.</b> The original needs no point — the player clicks the model — and
/// this product reaches a counter by standing near it, so a point has to be picked. A shop's hanging sign
/// is what a player of the original walks up to and clicks, so a face of the model the map calls a sign
/// wins; failing that the face textured as the building's door; failing that the lowest face the event
/// stands on. Which one it was, and how many faces the event covers, travel with the placement, so a
/// reader can see that the position is the map's geometry and which part of it was taken.
/// </para>
/// <para>
/// The radius the party must be inside is not here: how close a counter is approached is this game's
/// interaction policy, and the ruleset owns it.
/// </para>
/// </remarks>
/// <param name="BuildingId">The building the counter serves.</param>
/// <param name="PlaceId">The place the counter stands in.</param>
/// <param name="PlacementKind">The placement kind, which is <c>service</c> for a counter and <c>residence</c> for a household.</param>
/// <param name="Fixture">The table's own type string, so a residence says what the row called it.</param>
/// <param name="Name">The building's name, which a household answers the door by.</param>
/// <param name="Proprietor">Who the table names in the building, empty when it names nobody.</param>
/// <param name="X">The chosen point along the place's first axis.</param>
/// <param name="Y">The chosen point along the place's second axis.</param>
/// <param name="Z">The chosen point in height.</param>
/// <param name="EventId">The event the faces raise, which is the house's event in its own map's program.</param>
/// <param name="SourceFaceIndex">The chosen face's index among the map's faces, or the first of them when several were averaged.</param>
/// <param name="SourceModelIndex">The owning model's index, or -1 for an interior face, which has none.</param>
/// <param name="SourceModelName">The owning model's name, empty for an interior face.</param>
/// <param name="SourceTexture">The chosen face's texture name, so a reader can see what was picked.</param>
/// <param name="PositionSource">Where the point came from: <c>sign-face-centroid</c>, <c>door-face-centroid</c>, or <c>lowest-face-centroid</c>.</param>
/// <param name="HeightSource">
/// Where its height came from: the map's own ground at the point, or the chosen face's lowest corner when
/// the map states no ground under it — an interior.
/// </param>
/// <param name="FaceCount">How many faces of the place raise this building's event.</param>
public sealed record PlaceServicePlacement(
    int BuildingId,
    int PlaceId,
    string PlacementKind,
    string Fixture,
    string Name,
    string Proprietor,
    double X,
    double Y,
    double Z,
    int EventId,
    int SourceFaceIndex,
    int SourceModelIndex,
    string SourceModelName,
    string SourceTexture,
    string PositionSource,
    string HeightSource,
    int FaceCount);

/// <summary>One building row the import emitted no placement for, with the reason.</summary>
/// <param name="BuildingId">The building's id.</param>
/// <param name="Type">The table's type string, empty on the rows the table reserves.</param>
/// <param name="Name">The building's name, empty on the rows the table reserves.</param>
/// <param name="MapId">The map the row names, absent when it names none.</param>
/// <param name="Code">A short stable code for the kind of refusal.</param>
/// <param name="Reason">Why nothing was placed, in terms a person can act on.</param>
public sealed record PlaceServiceRefusal(
    int BuildingId,
    string Type,
    string Name,
    int? MapId,
    string Code,
    string Reason);

/// <summary>One passage a counter sells: where it goes, how long it takes, and the link that takes it.</summary>
/// <remarks>
/// The destinations are the places that keep a counter of the same kind, which is a rule this importer
/// states rather than a table the operator's data carries: the original keeps its coach and boat routes in
/// the executable, and the donor records them as tables keyed by house (OpenEnroth
/// <c>src/GUI/UI/Houses/Transport.cpp:38-78</c>, thirty-five routes of one to seven days). This import
/// states the simpler network the shipped world supports — every coach reaches every town that keeps a
/// coach, every boat every port that keeps a boat — and takes the days and the arrival from this import's
/// own data rather than copying the donor's route table. Where the fare arrives is the destination place's
/// own arrival point, so an arrival is the map's data and not a number chosen here.
/// </remarks>
/// <param name="ServiceId">The counter that sells the passage.</param>
/// <param name="FromPlace">The place the passage leaves.</param>
/// <param name="ToPlace">The place the passage arrives at.</param>
/// <param name="DestinationName">What the destination is called, for a report and a panel.</param>
/// <param name="Days">How many game days the passage takes.</param>
/// <param name="ArrivalPoint">The destination's own arrival point the passage lands at, empty when it lands at a stored pose.</param>
/// <param name="X">The stored arrival position along the destination's first axis, unused when an arrival point is named.</param>
/// <param name="Y">The stored arrival position along the destination's second axis.</param>
/// <param name="Z">The stored arrival position in height.</param>
/// <param name="Yaw">The stored arrival facing.</param>
/// <param name="Pitch">The stored arrival pitch.</param>
/// <param name="LinkId">The travel link's own entry id, which is what the world's transition is named by.</param>
public sealed record PlaceFare(
    int ServiceId,
    int FromPlace,
    int ToPlace,
    string DestinationName,
    int Days,
    string ArrivalPoint,
    double X,
    double Y,
    double Z,
    double Yaw,
    double Pitch,
    string LinkId);

/// <summary>What one import's service derivation produced, over every building row.</summary>
/// <param name="Services">Every enterable counter, in building order.</param>
/// <param name="Placements">Every placement emitted, in building order.</param>
/// <param name="Refusals">Every building row nothing was placed for, with its reason.</param>
/// <param name="Fares">Every passage a stable or a dock sells, in counter and destination order.</param>
public sealed record PlaceServiceSummary(
    IReadOnlyList<PlaceServiceDefinition> Services,
    IReadOnlyList<PlaceServicePlacement> Placements,
    IReadOnlyList<PlaceServiceRefusal> Refusals,
    IReadOnlyList<PlaceFare> Fares)
{
    /// <summary>An import that derived no services, such as one that decoded no maps.</summary>
    public static PlaceServiceSummary Empty { get; } = new([], [], [], []);

    /// <summary>How many counters the writer emits.</summary>
    public int ServiceCount => Services.Count;

    /// <summary>How many placements the writer emits, counters and households together.</summary>
    public int PlacementCount => Placements.Count;

    /// <summary>How many counters stand in a place.</summary>
    public int CounterCount => Placements.Count(placement => string.Equals(placement.PlacementKind, PlaceServiceEmitter.ServicePlacementKind, StringComparison.Ordinal));

    /// <summary>How many households the writer emits, which are the rows that are not counters.</summary>
    public int ResidenceCount => Placements.Count(placement => string.Equals(placement.PlacementKind, PlaceServiceEmitter.ResidencePlacementKind, StringComparison.Ordinal));

    /// <summary>How many building rows nothing was placed for.</summary>
    public int RefusalCount => Refusals.Count;

    /// <summary>How many passages are on sale.</summary>
    public int FareCount => Fares.Count;

    /// <summary>How many places carry a counter.</summary>
    public int PlaceCount => Placements.Select(placement => placement.PlaceId).Distinct().Count();

    /// <summary>How many counters of one kind the writer emits.</summary>
    /// <param name="kind">The table's own type string.</param>
    public int CountKind(string kind) =>
        Services.Count(service => string.Equals(service.Kind, kind, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Turns the building table into the counters a place holds and the passages a stable or a dock sells.
/// </summary>
/// <remarks>
/// <para>
/// <b>What the table gives and what the map gives.</b> The table gives a building's kind, name,
/// proprietor, map, multipliers, stock interval, training cap, and hours; the map gives where in that map
/// its door is, as the event faces the donor opens a house from. This emitter joins the two and emits one
/// service definition per row that has both, a placement per row whose faces exist, and a refusal naming
/// every row that gets neither — so the count of enterable counters is a number this import states rather
/// than one a reader has to derive.
/// </para>
/// <para>
/// <b>Rows that are not counters are emitted as what they are.</b> The table's remaining rows are
/// households, castles, and dungeon mouths. A household is placed as a residence carrying the table's own
/// type, name, and proprietor, so a party that walks up to it meets the resident rather than a shop that
/// sells nothing; a dungeon mouth or a castle door is a travel link the place graph already carries, and
/// is refused here naming that link rather than being placed as a fixture the party could not use.
/// </para>
/// <para>
/// The emission is deterministic: rows are walked in table order, a place's faces in their own order, and
/// every choice among them is made by a rule stated here rather than by a search order.
/// </para>
/// </remarks>
public static class PlaceServiceEmitter
{
    /// <summary>The house type strings this import serves as counters, which list is the table's own.</summary>
    public static IReadOnlyList<string> RecognizedKinds => ServiceTable.RecognizedTypes;

    /// <summary>The placement kind a counter stands in a place as.</summary>
    public const string ServicePlacementKind = "service";

    /// <summary>The placement kind a household stands in a place as.</summary>
    public const string ResidencePlacementKind = "residence";

    /// <summary>The service kind a coach stands under, which is the network of land routes.</summary>
    public const string StableKind = "Stables";

    /// <summary>The service kind a boat stands under, which is the network of sea routes.</summary>
    public const string BoatKind = "Boats";

    /// <summary>The height source of a counter standing on a region's own ground.</summary>
    public const string TerrainHeightSource = "terrain-under-the-point";

    /// <summary>The height source of a counter in an interior, whose point takes the chosen face's bottom.</summary>
    public const string FaceHeightSource = "chosen-face-bottom";

    /// <summary>How many game days a coach journey takes. Ours, not the donor's: the donor's own routes run one to seven days.</summary>
    public const int CoachDays = 2;

    /// <summary>How many game days a sea passage takes.</summary>
    public const int BoatDays = 3;

    /// <summary>
    /// The donor's instruction that opens a house, whose operand is the house id
    /// (OpenEnroth <c>src/Engine/Evt/EvtEnums.h:9</c>, <c>EVENT_SpeakInHouse = 2</c>).
    /// </summary>
    public const byte SpeakInHouseOpcode = 2;

    /// <summary>The arrival point a fare prefers, which is the one a map states for the party's own start.</summary>
    public const string PartyStartPoint = "Party Start";

    /// <summary>Derives every counter, household, and passage from the building table and the maps.</summary>
    /// <param name="services">The building table, read from the installation.</param>
    /// <param name="tables">Every table the import read, which is where map names come from.</param>
    /// <param name="programs">Every event program the installation carries, which is where a house's event is.</param>
    /// <param name="maps">The decoded maps, keyed by the place id the map table gives them.</param>
    /// <exception cref="ArgumentNullException">The table, the tables, the programs, or the maps are null.</exception>
    public static PlaceServiceSummary Emit(
        ServiceTable services,
        Mm7Tables tables,
        IReadOnlyList<EvtProgram> programs,
        IReadOnlyDictionary<int, DecodedMap> maps)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(programs);
        ArgumentNullException.ThrowIfNull(maps);

        Dictionary<string, EvtProgram> byStem = Programs(programs);
        Dictionary<int, string> placeNames = tables.Maps.Maps.ToDictionary(map => map.Id, map => map.Name);
        Dictionary<int, string> placeFiles = tables.Maps.Maps.ToDictionary(map => map.Id, map => map.FileName);
        Dictionary<int, HashSet<int>> houseEvents = HouseEvents(maps, byStem);
        Dictionary<int, List<Candidate>> candidates = Candidates(maps, houseEvents);

        List<PlaceServiceDefinition> definitions = [];
        List<PlaceServicePlacement> placements = [];
        List<PlaceServiceRefusal> refusals = [];
        Dictionary<int, int> sourceRows = [];
        for (int row = 0; row < services.Buildings.Count; row++)
        {
            ServiceRecord building = services.Buildings[row];
            sourceRows[building.Id] = row;
        }

        foreach (ServiceRecord building in services.Buildings)
        {
            bool recognized = RecognizedKinds.Contains(building.Type, StringComparer.OrdinalIgnoreCase);
            string fixture = building.Type;
            string placementKind = recognized ? ServicePlacementKind : ResidencePlacementKind;

            if (building.Name.Length == 0 && building.Type.Length == 0)
            {
                refusals.Add(Refuse(building, "unused-row", "The table reserves this row: it names neither a type nor a building, so it is an id rather than a place."));
                continue;
            }

            if (IsPlaceholder(building.Name))
            {
                refusals.Add(Refuse(
                    building,
                    recognized ? "placeholder-service-row" : "placeholder-row",
                    $"The row is named '{building.Name}', which is the table's own word for a row it reserves rather than a building, so it is not placed."));
                continue;
            }

            if (building.MapId is not int mapId || mapId == 0 || !maps.ContainsKey(mapId))
            {
                refusals.Add(Refuse(
                    building,
                    "no-map",
                    "The row names no map this import decoded, so there is no place its door could stand in."));
                continue;
            }

            if (!candidates.TryGetValue(building.Id, out List<Candidate>? faces) || faces.Count == 0)
            {
                refusals.Add(Refuse(
                    building,
                    IsTravelMarker(building.Type) ? "travel-marker" : "no-signing-face",
                    NoFaceReason(building, mapId, placeNames, placeFiles)));
                continue;
            }

            (Candidate chosen, IReadOnlyList<Candidate> used, string positionSource) = Choose(faces);
            double x = used.Average(face => face.X);
            double y = used.Average(face => face.Y);

            // The point's height is the ground the party walks on rather than the height of the face that
            // was chosen for it: a shop's sign hangs two or three hundred units above the street and a
            // door's middle is half a door up, and a party reaches a counter by standing on the ground.
            // A region states that ground in its own height map, which is what the collision and the mover
            // are built from; an interior states no ground under a point, so the chosen face's own lowest
            // corner is the closest thing its geometry says.
            (double z, string heightSource) = Ground(maps[mapId], x, y, chosen);

            placements.Add(new PlaceServicePlacement(
                building.Id,
                mapId,
                placementKind,
                fixture,
                building.Name,
                building.Proprietor,
                x,
                y,
                z,
                chosen.EventId,
                chosen.FaceIndex,
                chosen.ModelIndex,
                chosen.ModelName,
                chosen.Texture,
                positionSource,
                heightSource,
                faces.Count));

            if (!recognized) continue;

            definitions.Add(new PlaceServiceDefinition(
                building.Id,
                building.TypeSequence,
                building.Type,
                building.Name,
                building.Proprietor,
                building.Title,
                mapId,
                mapId.ToString(CultureInfo.InvariantCulture),
                building.PriceMultiplier,
                building.SkillPriceMultiplier,
                building.StockIntervalDays,
                building.MaximumTrainableLevelText,
                building.MaximumTrainableLevel,
                building.OpenHour,
                building.ClosedHour,
                sourceRows[building.Id]));
        }

        IReadOnlyList<PlaceFare> fares = Fares(definitions, placements, placeNames, maps);
        return new PlaceServiceSummary(definitions, placements, refusals, fares);
    }

    /// <summary>
    /// How high a counter's point stands, and which of the map's own facts said so.
    /// </summary>
    /// <remarks>
    /// A region carries a height map, and the point's own cell of it is the ground the party stands on
    /// there; that is the same ground the mover and the collision are built from, so a counter and the
    /// party that walks up to it agree about where the street is. An interior holds no such map: its
    /// geometry is its ground, and the lowest corner of the face the point was read from is the closest
    /// statement of it that needs no search through the level.
    /// </remarks>
    private static (double Height, string Source) Ground(DecodedMap map, double x, double y, Candidate chosen)
    {
        if (map is not OutdoorMap outdoor) return (chosen.LowestZ, FaceHeightSource);

        // The world-to-cell mapping is the map's own: five hundred and twelve units to a cell, a hundred and
        // twenty-eight cells to a side, centred on the origin with the second axis running the other way.
        // The cell is the one the point itself falls in, and not the one a face of the sign does: a sign
        // model spans cells, and a mountain map steps hundreds of units between two of them.
        int cellX = (int)Math.Round((x / 512.0) + 64.5);
        int cellY = (int)Math.Round(63.5 - (y / 512.0));
        return (outdoor.TerrainHeightAt(cellX, cellY), TerrainHeightSource);
    }

    /// <summary>Whether a row's name is the table's own word for a row it reserves rather than a building.</summary>
    /// <param name="name">The row's name.</param>
    public static bool IsPlaceholder(string name) =>
        name.Contains("Placeholder", StringComparison.OrdinalIgnoreCase);

    /// <summary>The reason a row with a map has no face to stand on, in terms a person can act on.</summary>
    private static string NoFaceReason(
        ServiceRecord building,
        int mapId,
        IReadOnlyDictionary<int, string> placeNames,
        IReadOnlyDictionary<int, string> placeFiles)
    {
        if (IsTravelMarker(building.Type))
        {
            return $"The row is a travel marker ('{building.Type}'), which this import carries as a door the place graph already moves the party through rather than as a counter; the graph's own links name the house id {building.Id}.";
        }

        string place = placeNames.TryGetValue(mapId, out string? name) ? name : mapId.ToString(CultureInfo.InvariantCulture);
        string file = placeFiles.TryGetValue(mapId, out string? mapFile) ? mapFile : "an unknown file";
        return $"No face of place {mapId} ('{place}', '{file}') raises an event that opens house {building.Id}, so nothing in the map tells the party the building is there.";
    }

    /// <summary>Whether a row's type is a door the travel graph carries rather than a building.</summary>
    /// <param name="type">The table's type string.</param>
    public static bool IsTravelMarker(string type) =>
        type.Contains("Ent", StringComparison.OrdinalIgnoreCase)
        || type.Contains("Entrance", StringComparison.OrdinalIgnoreCase)
        || string.Equals(type, "Throne", StringComparison.OrdinalIgnoreCase);

    /// <summary>The event each house is opened by, per place, from the maps' own programs.</summary>
    /// <remarks>
    /// An event can open two houses: the donor's own programs carry an event whose first step opens one
    /// house and whose later step opens another. The face raises the event and the event runs in order, so
    /// the house a face opens is the one its lowest step names; attributing the face to both would place one
    /// building's counter twice, on two rows, which is exactly the kind of wrong counter this derivation
    /// exists to avoid.
    /// </remarks>
    private static Dictionary<int, HashSet<int>> HouseEvents(
        IReadOnlyDictionary<int, DecodedMap> maps,
        IReadOnlyDictionary<string, EvtProgram> byStem)
    {
        Dictionary<int, HashSet<int>> events = [];
        foreach ((int placeId, DecodedMap map) in maps.OrderBy(entry => entry.Key))
        {
            string stem = Stem(map.FileName);
            if (!byStem.TryGetValue(stem, out EvtProgram? program)) continue;
            Dictionary<ushort, (byte Step, int House)> opening = [];
            foreach (EvtInstruction instruction in program.Instructions)
            {
                if (instruction.Opcode != SpeakInHouseOpcode) continue;
                if (instruction.Operands.Length < 4) continue;
                uint house = BitConverter.ToUInt32(instruction.Operands.Span[..4]);
                if (house == 0 || house > int.MaxValue) continue;
                if (opening.TryGetValue(instruction.EventId, out (byte Step, int House) first) && first.Step <= instruction.Step) continue;
                opening[instruction.EventId] = (instruction.Step, (int)house);
            }

            foreach ((ushort eventId, (byte _, int house)) in opening)
            {
                if (!events.TryGetValue(house, out HashSet<int>? ids)) events[house] = ids = [];
                ids.Add(eventId);
            }
        }

        return events;
    }

    /// <summary>
    /// Every face that raises a building's event, keyed by the building, in place and face order.
    /// </summary>
    /// <remarks>
    /// A house id is an index into one table for the whole game, so a face only belongs to the house when
    /// the event it raises in its own map's program names that house: the same event id means different
    /// things in different maps.
    /// </remarks>
    private static Dictionary<int, List<Candidate>> Candidates(
        IReadOnlyDictionary<int, DecodedMap> maps,
        Dictionary<int, HashSet<int>> houseEvents)
    {
        Dictionary<int, List<Candidate>> candidates = [];
        foreach ((int placeId, DecodedMap map) in maps.OrderBy(entry => entry.Key))
        {
            foreach ((int faceIndex, MapFace face, int modelIndex, string modelName) in MapFaceList.Flatten(map))
            {
                if (face.EventId == 0) continue;

                // A face with no corners states no position at all, which the shipped maps do carry among the
                // faces of a building's model. It is not a candidate for the point a party walks up to: it
                // has no middle and no bottom, and averaging nothing would be a position this importer
                // invented.
                if (face.Vertices.Count == 0) continue;
                foreach ((int house, HashSet<int> events) in houseEvents)
                {
                    if (!events.Contains(face.EventId)) continue;
                    if (!candidates.TryGetValue(house, out List<Candidate>? list)) candidates[house] = list = [];
                    list.Add(new Candidate(placeId, faceIndex, modelIndex, modelName, face));
                }
            }
        }

        return candidates;
    }

    /// <summary>
    /// Picks the point a counter is reached at, and says which rule picked it.
    /// </summary>
    /// <remarks>
    /// The sign wins because that is what a player of the original walks up to and clicks; the door
    /// texture is the next honest reading of the same geometry — a building's door in this game's maps is
    /// textured with the model's own door texture, whose names end in the letter the donor's textures use
    /// for a door (<c>Hhm3d</c>, <c>Hhp1d</c>, <c>Her1d</c>, <c>trimD</c>); and a building with neither is
    /// reached at the lowest face its event covers, which is the part of it nearest the ground the party
    /// walks on.
    /// </remarks>
    private static (Candidate Chosen, IReadOnlyList<Candidate> Used, string Source) Choose(List<Candidate> faces)
    {
        List<Candidate> signs = [.. faces.Where(face => face.ModelName.Contains("sign", StringComparison.OrdinalIgnoreCase))];
        if (signs.Count > 0) return (signs[0], signs, "sign-face-centroid");

        List<Candidate> doors = [.. faces.Where(face => LooksLikeDoor(face.Texture))];
        if (doors.Count > 0) return (doors[0], doors, "door-face-centroid");

        Candidate lowest = faces[0];
        foreach (Candidate face in faces)
        {
            if (face.LowestZ < lowest.LowestZ) lowest = face;
        }

        return (lowest, [lowest], "lowest-face-centroid");
    }

    /// <summary>Whether a face's texture is this game's own door texture.</summary>
    private static bool LooksLikeDoor(string texture) =>
        texture.Length > 1 && (texture.EndsWith('d') || texture.EndsWith('D'));

    /// <summary>Every passage the stables and docks of the world sell, in counter and destination order.</summary>
    private static IReadOnlyList<PlaceFare> Fares(
        IReadOnlyList<PlaceServiceDefinition> services,
        IReadOnlyList<PlaceServicePlacement> placements,
        IReadOnlyDictionary<int, string> placeNames,
        IReadOnlyDictionary<int, DecodedMap> maps)
    {
        Dictionary<int, int> placeOf = [];
        foreach (PlaceServicePlacement placement in placements)
        {
            placeOf[placement.BuildingId] = placement.PlaceId;
        }

        List<PlaceFare> fares = [];
        foreach (string kind in new[] { StableKind, BoatKind })
        {
            int days = string.Equals(kind, StableKind, StringComparison.Ordinal) ? CoachDays : BoatDays;
            List<PlaceServiceDefinition> network = [.. services.Where(service => string.Equals(service.Kind, kind, StringComparison.OrdinalIgnoreCase) && placeOf.ContainsKey(service.BuildingId))];
            foreach (PlaceServiceDefinition service in network)
            {
                int from = placeOf[service.BuildingId];
                HashSet<int> seen = [];
                foreach (PlaceServiceDefinition destination in network)
                {
                    int to = placeOf[destination.BuildingId];
                    if (to == from || !seen.Add(to)) continue;
                    if (!maps.TryGetValue(to, out DecodedMap? map)) continue;
                    (string point, double x, double y, double z, double yaw, double pitch) = Arrival(map);
                    fares.Add(new PlaceFare(
                        service.BuildingId,
                        from,
                        to,
                        placeNames.TryGetValue(to, out string? name) ? name : to.ToString(CultureInfo.InvariantCulture),
                        days,
                        point,
                        x,
                        y,
                        z,
                        yaw,
                        pitch,
                        string.Create(CultureInfo.InvariantCulture, $"fare-{service.BuildingId}-{to}")));
                }
            }
        }

        return fares;
    }

    /// <summary>
    /// Where a passage arrives: the destination's own arrival point, or its stored start point.
    /// </summary>
    /// <remarks>
    /// A fare lands where the destination's own data says a party arrives rather than at a coordinate this
    /// importer chose. The point named for a party's own start is preferred, and a place that names none
    /// hands over its first arrival point, which is still the map's own data; a place that names none at
    /// all arrives at its origin, and the fare's arrival is then reported as stored rather than named.
    /// </remarks>
    private static (string Point, double X, double Y, double Z, double Yaw, double Pitch) Arrival(DecodedMap map)
    {
        MapEntryPoint? start = map.EntryPoints.FirstOrDefault(point => string.Equals(point.Name, PartyStartPoint, StringComparison.OrdinalIgnoreCase))
            ?? map.EntryPoints.FirstOrDefault();
        return start is null
            ? (string.Empty, 0, 0, 0, 0, 0)
            : (start.Name, start.Position.X, start.Position.Y, start.Position.Z, start.YawAngle, 0);
    }

    private static PlaceServiceRefusal Refuse(ServiceRecord building, string code, string reason) =>
        new(building.Id, building.Type, building.Name, building.MapId, code, reason);

    /// <summary>The event programs, keyed by the file stem they belong to.</summary>
    private static Dictionary<string, EvtProgram> Programs(IReadOnlyList<EvtProgram> programs)
    {
        Dictionary<string, EvtProgram> byStem = new(StringComparer.OrdinalIgnoreCase);
        foreach (EvtProgram program in programs)
        {
            string stem = Stem(program.Name);
            if (!byStem.TryAdd(stem, program))
            {
                throw new Lod.LodFormatException(
                    $"{program.Name}: two event programs are named after the stem '{stem}', so a map's faces cannot be matched to one of them.");
            }
        }

        return byStem;
    }

    private static string Stem(string fileName) => Path.GetFileNameWithoutExtension(fileName);

    /// <summary>One face that raises a building's event, with what was read from it.</summary>
    private sealed record Candidate(int PlaceId, int FaceIndex, int ModelIndex, string ModelName, MapFace Face)
    {
        /// <summary>The face's centroid along the place's first axis.</summary>
        public double X => Face.Vertices.Average(vertex => (double)vertex.X);

        /// <summary>The face's centroid along the place's second axis.</summary>
        public double Y => Face.Vertices.Average(vertex => (double)vertex.Y);

        /// <summary>The face's centroid in height.</summary>
        public double Z => Face.Vertices.Average(vertex => (double)vertex.Z);

        /// <summary>The lowest point of the face, which is how the last fallback picks a part of a building.</summary>
        public double LowestZ => Face.Vertices.Min(vertex => (double)vertex.Z);

        /// <summary>The event the face raises.</summary>
        public int EventId => Face.EventId;

        /// <summary>The face's texture name.</summary>
        public string Texture => Face.TextureName;
    }
}
