using MightAndMagic7.Import.Events;
using MightAndMagic7.Import.Tables;

namespace MightAndMagic7.Import.World;

/// <summary>One decoded map move, with the maps it connects resolved.</summary>
/// <param name="SourceEvtName">The event program the move came from.</param>
/// <param name="SourceMapId">The map that program belongs to, absent for the global program.</param>
/// <param name="DestinationMapId">The destination map, or null when the move stays on the source map.</param>
/// <param name="DestinationMapFile">The destination the instruction named, exactly as stored.</param>
/// <param name="X">Destination X coordinate.</param>
/// <param name="Y">Destination Y coordinate.</param>
/// <param name="Z">Destination Z coordinate.</param>
/// <param name="Yaw">Destination facing.</param>
/// <param name="Pitch">Destination pitch.</param>
/// <param name="HouseId">The house the move arrives at, when it arrives at one.</param>
/// <param name="ExitPicture">The exit picture shown while arriving.</param>
/// <param name="EventId">The event the instruction belongs to.</param>
/// <param name="Step">The instruction's step inside its event.</param>
public readonly record struct PlaceLink(
    string SourceEvtName,
    int? SourceMapId,
    int? DestinationMapId,
    string DestinationMapFile,
    uint X,
    uint Y,
    uint Z,
    uint Yaw,
    uint Pitch,
    byte HouseId,
    byte ExitPicture,
    ushort EventId,
    byte Step)
{
    /// <summary>Whether the move stays on the map that issued it.</summary>
    public bool IsWithinMap => DestinationMapId is null;
}

/// <summary>
/// The world's link structure, recovered from the per-map event programs rather than authored by
/// hand. Each map's program names the file it moves to, so the graph is a fact of the data.
/// </summary>
public sealed class PlaceGraph
{
    private PlaceGraph(
        PlaceLink[] links,
        PlaceLink[] withinMapMoves,
        int moveInstructionCount,
        int exitInstructionCount,
        IReadOnlyDictionary<int, int> outbound,
        IReadOnlyDictionary<int, int> inbound,
        IReadOnlyList<string> programsWithoutAMap)
    {
        Links = links;
        WithinMapMoves = withinMapMoves;
        MoveInstructionCount = moveInstructionCount;
        ExitInstructionCount = exitInstructionCount;
        OutboundPerMap = outbound;
        InboundPerMap = inbound;
        ProgramsWithoutAMap = programsWithoutAMap;
    }

    /// <summary>Every move that connects two different maps, including the global program's arrivals.</summary>
    public IReadOnlyList<PlaceLink> Links { get; }

    /// <summary>How many links came from a map's own program.</summary>
    public int LinksFromMapPrograms => Links.Count(link => link.SourceMapId is not null);

    /// <summary>How many links came from the global program, which belongs to no map.</summary>
    public int LinksFromGlobalProgram => Links.Count(link => link.SourceMapId is null);

    /// <summary>Every move that only repositions the party on its current map.</summary>
    public IReadOnlyList<PlaceLink> WithinMapMoves { get; }

    /// <summary>How many map moves the programs carry, whether or not they change map.</summary>
    public int MoveInstructionCount { get; }

    /// <summary>How many exit instructions the programs carry. These are entrances and doors, not map links.</summary>
    public int ExitInstructionCount { get; }

    /// <summary>How many links leave each map.</summary>
    public IReadOnlyDictionary<int, int> OutboundPerMap { get; }

    /// <summary>How many links arrive at each map.</summary>
    public IReadOnlyDictionary<int, int> InboundPerMap { get; }

    /// <summary>Programs that belong to no map, such as the global program.</summary>
    public IReadOnlyList<string> ProgramsWithoutAMap { get; }

    /// <summary>
    /// Whether a move stays on the map that issued it. The table writes the placeholder <c>0</c> where
    /// a destination file name would go, so a move within a map is identified by an empty destination
    /// or that placeholder rather than by an empty string alone.
    /// </summary>
    public static bool IsWithinMap(string destinationMapFile) =>
        destinationMapFile.Length == 0 || destinationMapFile == "0";

    /// <summary>
    /// Builds the graph from every event program and the map table. A destination that does not name
    /// a known map fails the build: dropping it would leave a map unreachable with no record of why.
    /// </summary>
    public static PlaceGraph Build(IEnumerable<EvtProgram> programs, MapStatsTable maps)
    {
        ArgumentNullException.ThrowIfNull(maps);
        return Build(programs, maps.FileStemIndex);
    }

    /// <summary>Builds the graph from every event program and a map-file lookup.</summary>
    public static PlaceGraph Build(IEnumerable<EvtProgram> programs, IReadOnlyDictionary<string, int> mapByStem)
    {
        ArgumentNullException.ThrowIfNull(programs);
        ArgumentNullException.ThrowIfNull(mapByStem);

        List<PlaceLink> links = [];
        List<PlaceLink> withinMap = [];
        List<string> unresolvedDestinations = [];
        List<string> programsWithoutAMap = [];
        int exitInstructions = 0;
        int moves = 0;

        foreach (EvtProgram program in programs)
        {
            string programStem = Path.GetFileNameWithoutExtension(program.Name);
            int? sourceMapId = mapByStem.TryGetValue(programStem, out int found) ? found : null;
            if (sourceMapId is null) programsWithoutAMap.Add(program.Name);
            foreach (EvtInstruction instruction in program.Instructions)
            {
                if (instruction.Opcode == EvtOpcodes.Exit) exitInstructions++;
                if (!instruction.TryReadMoveToMap(out MoveToMapInstruction move)) continue;
                moves++;

                int? destination = null;
                if (!IsWithinMap(move.DestinationMapFile))
                {
                    string destinationStem = Path.GetFileNameWithoutExtension(move.DestinationMapFile);
                    if (!mapByStem.TryGetValue(destinationStem, out int destinationId))
                    {
                        unresolvedDestinations.Add($"{program.Name}: event {instruction.EventId} -> '{move.DestinationMapFile}'");
                        continue;
                    }

                    // A program that names its own map is repositioning inside it, whatever the marker:
                    // the party does not leave and come back.
                    destination = destinationId == sourceMapId ? null : destinationId;
                }

                PlaceLink link = new(
                    program.Name,
                    sourceMapId,
                    destination,
                    move.DestinationMapFile,
                    move.X,
                    move.Y,
                    move.Z,
                    move.Yaw,
                    move.Pitch,
                    move.HouseId,
                    move.ExitPicture,
                    instruction.EventId,
                    instruction.Step);
                if (destination is null) withinMap.Add(link);
                else links.Add(link);
            }
        }

        if (unresolvedDestinations.Count > 0)
        {
            throw new Lod.LodFormatException(
                $"{unresolvedDestinations.Count} map moves name a destination that is not a known map: " +
                string.Join("; ", unresolvedDestinations.Take(10)) +
                (unresolvedDestinations.Count > 10 ? "; ..." : string.Empty));
        }

        Dictionary<int, int> outbound = [];
        Dictionary<int, int> inbound = [];
        foreach (PlaceLink link in links)
        {
            // The global program's moves belong to no map, so they are arrivals without a departure.
            if (link.SourceMapId is int source) outbound[source] = outbound.GetValueOrDefault(source) + 1;
            int destination = link.DestinationMapId!.Value;
            inbound[destination] = inbound.GetValueOrDefault(destination) + 1;
        }

        return new PlaceGraph([.. links], [.. withinMap], moves, exitInstructions, outbound, inbound, [.. programsWithoutAMap]);
    }
}
