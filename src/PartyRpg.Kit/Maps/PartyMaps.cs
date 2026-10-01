using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Maps;

/// <summary>
/// What one party has seen of one place, and the place's own map it saw it on.
/// </summary>
/// <remarks>
/// <para>
/// The territory is a set of cells, one bit each, over the grid the place's own map states — so a place's
/// whole map is what the party could ever see of it, and what it has seen is a subset of that rather than a
/// list that grows with how long it walked. A save records the grid and those bits, which is why the same
/// walk read back draws the same map.
/// </para>
/// <para>
/// <b>A reset of the place touches none of this.</b> The territory is the party's own memory and is keyed by
/// the party rather than held in the place's state: what the world restores is a population, and a party that
/// mapped a place before its creatures came back has mapped it after.
/// </para>
/// </remarks>
public sealed class MapTerritory
{
    private readonly bool[] _seen;

    internal MapTerritory(PlaceMap map)
    {
        Map = map;
        _seen = new bool[map.Grid.Cells];
    }

    /// <summary>The place's own map, which is what the party's cells are cells of.</summary>
    public PlaceMap Map { get; }

    /// <summary>The place this territory is of.</summary>
    public PlaceId Place => Map.Place;

    /// <summary>The grid the place is mapped on.</summary>
    public MapGrid Grid => Map.Grid;

    /// <summary>How many cells of the place the party has seen.</summary>
    public int SeenCount { get; private set; }

    /// <summary>Whether the party has seen one cell.</summary>
    /// <param name="cell">The cell to read.</param>
    /// <returns>Whether that square of the place is on the party's map.</returns>
    public bool IsSeen(MapCell cell) => Grid.Contains(cell) && _seen[cell.Index(Grid.Columns)];

    /// <summary>Whether the party has seen the cell at one index of the grid's row-major order.</summary>
    /// <param name="index">The cell's index.</param>
    /// <returns>Whether that square of the place is on the party's map.</returns>
    public bool IsSeen(int index) => index >= 0 && index < _seen.Length && _seen[index];

    /// <summary>Whether the party has seen any of the place at all.</summary>
    public bool AnySeen => SeenCount > 0;

    /// <summary>Records that the party has seen one cell.</summary>
    /// <returns>Whether this is the first time, which is what makes it news.</returns>
    internal bool Reveal(MapCell cell)
    {
        if (!Grid.Contains(cell)) return false;
        int index = cell.Index(Grid.Columns);
        if (_seen[index]) return false;
        _seen[index] = true;
        SeenCount++;
        return true;
    }

    /// <summary>Every cell the party has seen, in the grid's own row-major order.</summary>
    /// <returns>The cells.</returns>
    public IEnumerable<MapCell> Seen()
    {
        for (int index = 0; index < _seen.Length; index++)
        {
            if (_seen[index]) yield return new MapCell(index % Grid.Columns, index / Grid.Columns);
        }
    }
}

/// <summary>
/// The one owner of what a party has mapped: the cells it has seen of every place it has walked, keyed by
/// the place and bounded by the place's own map.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the party's knowledge of the world's shape, and it is not the world's state.</b> It lives
/// beside the knowledge owner rather than inside it because it is keyed by place and shaped by a grid, and
/// the notes are deliberately neither: a note is a dated fact about a thing, its identity is the kind, the
/// subject, and the place, and a place's thousand seen cells are not a thousand facts. What the two share is
/// the property that matters here — both are the party's, both outlive a place the world restores, and both
/// are carried by a save as the party's own record rather than as a reading of the world.
/// </para>
/// <para>
/// <b>Walking is what fills it, and what a step can add is bounded twice.</b> A sweep reveals the cell the
/// party stands in and the cells around it that the engine's own collision admits a sight line to, nearest
/// first; the game states how far that reaches and how many cells one sweep may add, and the place's own map
/// states how many cells there are to see at all. A long walk therefore fills a finite map rather than
/// growing one.
/// </para>
/// <para>
/// <b>Learning is not an event and is not dated.</b> Nothing here writes a note or a journal line: a map is a
/// state a party is in rather than a thing that happened at a moment, so a party that walks a corridor twice
/// has one corridor on its map, and the owner keeps no clock for the same reason it keeps no history.
/// </para>
/// </remarks>
public sealed class PartyMaps
{
    /// <summary>How many places one party's maps keep.</summary>
    /// <remarks>
    /// A structural bound rather than a tuning value: the operator's world holds 76 places, so a party that
    /// has mapped more than this has had a very long game and the place it has not looked at in the longest
    /// time is the one whose map falls off. It is a count of places and not of cells, because the cells of one
    /// place are already bounded by that place's own map.
    /// </remarks>
    public const int MaxPlaces = 256;

    private readonly IMapRule _rule;
    private readonly IPlaceMapSource _maps;
    private readonly Dictionary<PlaceId, MapTerritory> _territories = [];
    private readonly List<PlaceId> _order = [];
    private readonly List<PlaceId> _recent = [];

    /// <summary>Creates the owner over the game's own reading and the places' own maps.</summary>
    /// <param name="rule">What this game states about its automap.</param>
    /// <param name="maps">Where each place's own map comes from.</param>
    /// <param name="save">What a save recorded, or null for a party that has mapped nothing.</param>
    /// <exception cref="ArgumentNullException">The rule or the map source is missing.</exception>
    public PartyMaps(IMapRule rule, IPlaceMapSource maps, MapSave? save = null)
    {
        _rule = rule ?? throw new ArgumentNullException(nameof(rule));
        _maps = maps ?? throw new ArgumentNullException(nameof(maps));
        if (save is null) return;
        foreach (MapTerritorySave recorded in save.Places) Restore(recorded);
    }

    /// <summary>What this game states about its automap.</summary>
    public IMapRule Rule => _rule;

    /// <summary>Where each place's own map comes from.</summary>
    public IPlaceMapSource Maps => _maps;

    /// <summary>
    /// The change stamp this owner took when the maps it keeps last changed, or when it was made: a reader that
    /// kept what it built beside it reads the owner again only when it has moved (<see cref="ChangeStamp"/>).
    /// </summary>
    public long Stamp { get; private set; } = ChangeStamp.Next();

    /// <summary>Every place the party holds a map of, in the order it first saw each.</summary>
    public IReadOnlyList<MapTerritory> Territories => [.. _order.Select(place => _territories[place])];

    /// <summary>How many places the party holds a map of.</summary>
    public int PlacesMapped => _territories.Count;

    /// <summary>The map the party holds of one place, or null when it has seen none of it.</summary>
    /// <param name="place">The place to read.</param>
    /// <returns>The party's map of it.</returns>
    public MapTerritory? Find(PlaceId place) =>
        _territories.TryGetValue(place, out MapTerritory? territory) ? territory : null;

    /// <summary>
    /// Records what the party can see from where it stands, and returns whether anything new was seen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The sweep runs when the party's own cell changes rather than on every step: a party standing still, or
    /// shifting inside one square of the place, has already seen everything it can see from there, so the
    /// work a step costs does not depend on how long the key is held. A party that has not entered a mapped
    /// cell of a place content states no map for records nothing at all, and says so by answering false.
    /// </para>
    /// <para>
    /// <b>The sight line is the engine's own collision.</b> What admits a cell is the same scene the party
    /// walks in — the caller hands in the answer — so a wall, a closed door, and a floor between two storeys
    /// stop the party's view for exactly the reason they stop its walk. A caller that has no collision to ask
    /// hands in nothing, and then only the cell the party stands in is revealed: a product with no spatial
    /// service has a map of where it has been rather than one of what it could see.
    /// </para>
    /// </remarks>
    /// <param name="place">The place the party is in.</param>
    /// <param name="pose">Where it stands and faces there.</param>
    /// <returns>Whether the party's map of the place changed.</returns>
    public bool Observe(PlaceId place, PlacePose pose)
    {
        if (_maps.For(place) is not { } map) return false;

        MapTerritory territory = _territories.TryGetValue(place, out MapTerritory? held)
            ? held
            : Open(map);
        MapCell here = map.Grid.CellAt(pose.X, pose.Y);
        Mark(place);

        // Nothing new can be seen from a square the party has already looked out of, so the sweep is skipped
        // rather than repeated: this is what keeps a held key's cost constant instead of per-step.
        if (territory.IsSeen(here)) return false;

        bool changed = territory.Reveal(here);
        changed = Sweep(territory, pose) || changed;
        if (changed) Stamp = ChangeStamp.Next();
        return changed;
    }

    /// <summary>Reads the party's own maps into the product's one current save schema.</summary>
    /// <returns>The maps as a save records them.</returns>
    public MapSave Capture() => new([.. _order.Select(place => MapTerritorySave.Record(_territories[place]))]);

    /// <summary>
    /// Reveals the cells around the party that the place's own collision admits a sight line to.
    /// </summary>
    /// <remarks>
    /// The cells are walked in rings outwards from the party's own cell, so the bound on one sweep always
    /// trims the far end of what the party could see rather than an arbitrary half of it. Each candidate is
    /// asked at the party's own height: what stops a walking party's view is what stands on the ground
    /// between it and the ground ahead.
    /// </remarks>
    private bool Sweep(MapTerritory territory, PlacePose pose)
    {
        MapGrid grid = territory.Grid;
        MapCell here = grid.CellAt(pose.X, pose.Y);
        int radius = Math.Max(0, _rule.SightRadius);
        int limit = Math.Max(0, _rule.MaxCellsPerSweep);
        int added = 0;
        bool changed = false;
        for (int ring = 1; ring <= radius && added < limit; ring++)
        {
            for (int row = here.Row - ring; row <= here.Row + ring && added < limit; row++)
            {
                for (int column = here.Column - ring; column <= here.Column + ring && added < limit; column++)
                {
                    // Only the ring's own edge is new: everything inside it was asked on an earlier ring.
                    if (Math.Abs(row - here.Row) != ring && Math.Abs(column - here.Column) != ring) continue;
                    MapCell cell = new(column, row);
                    if (!grid.Contains(cell) || territory.IsSeen(cell)) continue;

                    // The sight line is asked at the party's own height: what stops a walking party's view is
                    // what stands on the ground between it and the ground ahead, and the rule answers with the
                    // collision the party itself walks in.
                    if (!_rule.Sees(pose, pose with { X = grid.CentreX(column), Y = grid.CentreY(row) })) continue;
                    changed |= territory.Reveal(cell);
                    added++;
                }
            }
        }

        return changed;
    }

    /// <summary>Opens a place's map for a party that has seen none of it, keeping the whole set bounded.</summary>
    private MapTerritory Open(PlaceMap map)
    {
        MapTerritory territory = new(map);
        _territories[map.Place] = territory;
        _order.Add(map.Place);
        _recent.Add(map.Place);
        Stamp = ChangeStamp.Next();

        // The oldest map falls off, exactly as the oldest note does: a set that only ever grows is the leak
        // the bound exists to prevent, and a place a party has not looked at in the longest time is the one it
        // would have to walk back to.
        while (_recent.Count > MaxPlaces)
        {
            PlaceId oldest = _recent[0];
            _recent.RemoveAt(0);
            _territories.Remove(oldest);
            _order.Remove(oldest);
        }

        return territory;
    }

    /// <summary>Notes that the party has just looked at one place's map.</summary>
    private void Mark(PlaceId place)
    {
        _recent.Remove(place);
        _recent.Add(place);
    }

    /// <summary>Reads one recorded territory back, over the place's own map as content states it today.</summary>
    /// <remarks>
    /// <para>
    /// The bits were written against the grid the save records, so they are read on that grid: which cell a
    /// bit is depends on where the grid starts and how wide a cell is, and reading them against another grid
    /// would put the party's walk somewhere it never went. What the party saw is then placed on the place's
    /// map by where it stood rather than by which index it was, so a place whose own map content has since
    /// redrawn keeps the ground the party actually saw — and ground the place's map no longer covers lands in
    /// the cell nearest to it rather than nowhere.
    /// </para>
    /// <para>
    /// A place content no longer carries a map for is kept as the record it is, over the grid it was seen on:
    /// a map the party drew is a fact about the party, so it survives the world's own places changing.
    /// </para>
    /// </remarks>
    private void Restore(MapTerritorySave recorded)
    {
        PlaceId place = new(recorded.Place);
        MapGrid grid = recorded.Grid;
        PlaceMap over = _maps.For(place) ?? new PlaceMap(place, grid, new byte[grid.Cells]);
        MapTerritory territory = new(over);
        foreach (int index in recorded.SeenIndices())
        {
            MapCell cell = new(index % grid.Columns, index / grid.Columns);
            territory.Reveal(over.Grid.CellAt(grid.CentreX(cell.Column), grid.CentreY(cell.Row)));
        }

        _territories[place] = territory;
        _order.Add(place);
        _recent.Add(place);
    }
}
