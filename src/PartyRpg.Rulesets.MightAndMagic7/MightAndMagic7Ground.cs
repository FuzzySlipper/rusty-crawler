using System.Text.Json;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Maps;
using PartyRpg.Kit.World;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>One place's normalized ground words, read on the existing content grid.</summary>
internal sealed class MightAndMagic7Ground(MapGrid grid, string[] terrains, byte[] kinds)
{
    internal string? At(PlacePose pose)
    {
        // MapGrid clamps for exploration. Camping outside the finer ground falls back to the place,
        // rather than inventing that the nearest mapped square is under the party.
        if (pose.X < grid.OriginX || pose.Y < grid.OriginY
            || pose.X >= grid.OriginX + grid.Width || pose.Y >= grid.OriginY + grid.Depth) return null;
        return terrains[kinds[grid.CellAt(pose.X, pose.Y).Index(grid.Columns)]];
    }

    internal static MightAndMagic7Ground? Read(ContentEntry place, Func<string, bool> known, Action<string> defect)
    {
        if (!place.Payload.TryGetProperty("ground", out JsonElement payload)) return null;
        if (payload.ValueKind != JsonValueKind.Object)
        {
            defect("ground must be a grid object, so the camp's price can be read at its pose");
            return null;
        }
        ContentEntry entry = new(place.Id, payload);
        JsonElement[] origin = [.. entry.GetArray("origin")];
        if (origin.Length != 2 || origin[0].ValueKind != JsonValueKind.Number || origin[1].ValueKind != JsonValueKind.Number || !origin[0].TryGetDouble(out double x) || !origin[1].TryGetDouble(out double y))
        {
            defect("ground origin must name two coordinates");
            return null;
        }
        try
        {
            MapGrid grid = new(x, y, entry.GetDouble("cellSize") ?? 0, entry.GetInt32("columns") ?? 0, entry.GetInt32("rows") ?? 0);
            if ((long)grid.Columns * grid.Rows > MapGrid.MaxCells)
            {
                defect($"ground holds more than {MapGrid.MaxCells} cells, so its content cannot be bounded");
                return null;
            }
            string[] terrains = [.. entry.GetArray("terrains").Select(word => word.ValueKind == JsonValueKind.String ? word.GetString()! : "")];
            if (terrains.Length == 0 || terrains.Length > 256 || terrains.Any(word => !known(word)))
            {
                defect("ground terrains must name one to 256 grounds this game prices");
                return null;
            }
            byte[] kinds = Convert.FromHexString(entry.GetString("kinds"));
            if (kinds.Length != grid.Cells || kinds.Any(kind => kind >= terrains.Length))
            {
                defect("ground must name a terrain for every grid cell, with no unknown terrain index");
                return null;
            }
            return new MightAndMagic7Ground(grid, terrains, kinds);
        }
        catch (Exception error) when (error is ArgumentOutOfRangeException or FormatException)
        {
            defect($"ground cannot be read: {error.Message}");
            return null;
        }
    }
}
