using System.Globalization;

namespace PartyRpg.Kit.Maps;

/// <summary>One square of a place's own map: which column and which row of the grid it is.</summary>
/// <remarks>
/// A cell is a position on a grid rather than an identity anything mints, so it is a pair of numbers and
/// nothing else: the same cell read twice is the same cell, and a save records the grid and the bits that
/// were seen rather than a list of cells that would have to be kept in step with it.
/// </remarks>
/// <param name="Column">Which column of the grid, counted from its first.</param>
/// <param name="Row">Which row of the grid, counted from its first.</param>
public readonly record struct MapCell(int Column, int Row)
{
    /// <summary>Which bit of a row-major grid this cell is.</summary>
    /// <param name="columns">How many columns the grid has.</param>
    /// <returns>The cell's index.</returns>
    public int Index(int columns) => (Row * columns) + Column;

    /// <inheritdoc />
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Column},{Row}");
}

/// <summary>
/// The grid one place is mapped on: where its first cell starts, how wide a cell is, and how many there
/// are in each direction.
/// </summary>
/// <remarks>
/// <para>
/// <b>The grid belongs to the place and is carried with the map, not derived from where the party stands.</b>
/// A cell is therefore a fact about the place rather than about a visit: the party's cell is arithmetic over
/// its pose and this grid, two visits compute the same cell, and a save can record which cells were seen
/// without recording where anybody walked.
/// </para>
/// <para>
/// <b>Where the grid comes from is content's and the game's, and this type only holds it.</b> A region's own
/// map carries a cell per terrain tile at the map's own pitch; an interior's is finer and is the game's
/// reading of the level. Both arrive here as the same four numbers, so nothing in this layer knows which kind
/// of place it is drawing.
/// </para>
/// <para>
/// <b>A grid is bounded, which is what bounds what a party can discover.</b> The count is refused above
/// <see cref="MaxCells"/>, at the moment the map is read rather than at the moment a party walks: a place
/// whose map would be an unbounded grid is a content defect that names itself, not a save that grows with
/// every step.
/// </para>
/// </remarks>
/// <param name="OriginX">Where the grid's first column starts along the place's first axis.</param>
/// <param name="OriginY">Where the grid's first row starts along the place's second axis.</param>
/// <param name="CellSize">How wide one cell is, in the place's own units.</param>
/// <param name="Columns">How many cells the grid has along the first axis.</param>
/// <param name="Rows">How many cells the grid has along the second axis.</param>
/// <exception cref="ArgumentOutOfRangeException">A size is not a positive number, or the grid is too large.</exception>
public readonly record struct MapGrid(double OriginX, double OriginY, double CellSize, int Columns, int Rows)
{
    /// <summary>How many cells one place's grid may hold.</summary>
    /// <remarks>
    /// A structural bound rather than a tuning value: the operator's largest region is 128 by 128 cells of its
    /// own terrain and its largest interior is 322 by 341 at the pitch the importer rasterises it with, so a
    /// place whose grid is larger than this is a place nothing could draw or save in reasonable space. It is a
    /// count of cells and not a span of game time, because what makes the grid expensive is how many cells a
    /// party could ever have to remember.
    /// </remarks>
    public const int MaxCells = 262_144;

    /// <summary>Where the grid's first column starts along the place's first axis.</summary>
    public double OriginX { get; } = Finite(OriginX, nameof(OriginX));

    /// <summary>Where the grid's first row starts along the place's second axis.</summary>
    public double OriginY { get; } = Finite(OriginY, nameof(OriginY));

    /// <summary>How wide one cell is, in the place's own units.</summary>
    public double CellSize { get; } = Positive(CellSize, nameof(CellSize));

    /// <summary>How many cells the grid has along the first axis.</summary>
    public int Columns { get; } = Count(Columns, nameof(Columns));

    /// <summary>How many cells the grid has along the second axis.</summary>
    public int Rows { get; } = Count(Rows, nameof(Rows));

    /// <summary>How many cells the whole grid holds.</summary>
    public int Cells => Columns * Rows;

    /// <summary>How far the grid reaches along the first axis.</summary>
    public double Width => CellSize * Columns;

    /// <summary>How far the grid reaches along the second axis.</summary>
    public double Depth => CellSize * Rows;

    /// <summary>Which cell a point of the place falls in, clamped to the grid's own edges.</summary>
    /// <remarks>
    /// Clamping rather than refusing is deliberate: a pose a place's own map does not cover is a content
    /// disagreement rather than a reason to stop mapping, and a party standing past its map's edge belongs to
    /// the nearest cell that exists rather than to none at all. The clamp is stated here because it is the
    /// only place a pose becomes a cell.
    /// </remarks>
    /// <param name="x">A position along the place's first axis.</param>
    /// <param name="y">A position along the place's second axis.</param>
    /// <returns>The cell that position falls in.</returns>
    public MapCell CellAt(double x, double y) => new(Clamp((int)Math.Floor((x - OriginX) / CellSize), Columns),
        Clamp((int)Math.Floor((y - OriginY) / CellSize), Rows));

    /// <summary>Whether a cell is one of this grid's.</summary>
    /// <param name="cell">The cell to test.</param>
    /// <returns>Whether the grid holds it.</returns>
    public bool Contains(MapCell cell) =>
        cell.Column >= 0 && cell.Column < Columns && cell.Row >= 0 && cell.Row < Rows;

    /// <summary>Where a cell's own centre stands along the place's first axis.</summary>
    /// <param name="column">The cell's column.</param>
    /// <returns>The centre's position.</returns>
    public double CentreX(int column) => OriginX + ((column + 0.5) * CellSize);

    /// <summary>Where a cell's own centre stands along the place's second axis.</summary>
    /// <param name="row">The cell's row.</param>
    /// <returns>The centre's position.</returns>
    public double CentreY(int row) => OriginY + ((row + 0.5) * CellSize);

    /// <inheritdoc />
    public override string ToString() => string.Create(
        CultureInfo.InvariantCulture,
        $"{Columns}x{Rows} at ({OriginX:0.##},{OriginY:0.##}) cell {CellSize:0.##}");

    private static int Clamp(int value, int count) => value < 0 ? 0 : value >= count ? count - 1 : value;

    private static double Finite(double value, string name) =>
        double.IsFinite(value)
            ? value
            : throw new ArgumentOutOfRangeException(name, value, "A grid starts at a point, so its origin is a number.");

    private static double Positive(double value, string name) =>
        double.IsFinite(value) && value > 0
            ? value
            : throw new ArgumentOutOfRangeException(name, value, "A grid's cell is a size, so it is wider than nothing.");

    private static int Count(int value, string name)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(name, value, "A grid holds at least one cell in each direction.");
        }

        return value;
    }
}
