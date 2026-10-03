using System.Globalization;
using System.Text.Json;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.World;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// What this game's map events change about how a place looks, and the names they keep it under on the interaction
/// ledger: a face cog hidden or shown, a face cog retextured, a decoration cog shown or hidden, a decoration cog given
/// another look.
/// </summary>
/// <remarks>
/// <para>
/// The donor changes these on the live level: <c>setTexture</c> gives every face of a cog another bitmap,
/// <c>setFacesBit</c> sets or clears <c>FACE_IsInvisible</c> on them, and <c>setDecorationSprite</c> shows or hides
/// every decoration of a cog and may give it another decoration-list row (OpenEnroth <c>src/Engine/Engine.cpp:948-1006</c>).
/// </para>
/// <para>
/// Here every change is a value on the place's interaction ledger, so it is saved with the session and a place
/// re-entered or resumed draws it again. The donor keeps face attributes and decoration visibility in its map delta but
/// not a retextured face or a decoration's new row (<c>src/Engine/Snapshots/CompositeSnapshots.cpp:323-329</c>); keeping
/// those too is ours, so a lever pulled stays pulled.
/// </para>
/// <para>
/// The interior-light step is passed over by decision: every light the shipped levels carry has a radius of zero,
/// which the donor's sector lighting reads as reaching nothing (<c>src/Engine/Graphics/Lighting.cpp:133-151</c>), so
/// turning one on or off changes nothing a player sees in the donor either.
/// </para>
/// </remarks>
internal sealed class MightAndMagic7Switches
{
    private const string HiddenPrefix = "face-hidden:";
    private const string TexturePrefix = "face-texture:";
    private const string ShownPrefix = "decoration-shown:";
    private const string LookPrefix = "decoration-look:";

    /// <summary>A map decoration's cog, which a sprite step names, as the importer writes it.</summary>
    private const string CogField = "cog";

    /// <summary>A map decoration's level flags, as the importer writes them.</summary>
    private const string FlagsField = "flags";

    /// <summary><c>LEVEL_DECORATION_INVISIBLE</c> (OpenEnroth <c>src/Engine/Objects/Decoration.h:18</c>).</summary>
    private const int InvisibleDecoration = 0x20;

    private readonly Dictionary<PlaceId, Dictionary<string, int>> _materials = [];
    private readonly Dictionary<string, int> _decorations = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, (int Height, int Radius)> _sizes = [];

    /// <summary>A reading of content that names no place materials and no decoration rows.</summary>
    internal static MightAndMagic7Switches None { get; } = new();

    /// <summary>Reads each place's material list from its render entry and each decoration row's name from its look.</summary>
    internal static MightAndMagic7Switches Read(ContentCatalog? catalog)
    {
        MightAndMagic7Switches switches = new();
        if (catalog is null) return switches;
        foreach ((_, _, ContentEntry entry) in catalog.Entries(MightAndMagic7Scene.RenderDefinitionKind))
        {
            Dictionary<string, int> named = new(StringComparer.OrdinalIgnoreCase);
            int index = 0;
            foreach (JsonElement material in entry.Payload.GetProperty("materials").EnumerateArray())
            {
                string name = material.TryGetProperty("named", out JsonElement value) ? value.GetString() ?? string.Empty : string.Empty;
                if (name.Length > 0 && material.TryGetProperty("surface", out JsonElement surface) && surface.GetString() == "face")
                    named.TryAdd(name, index);
                index++;
            }

            switches._materials[new PlaceId(entry.Id)] = named;
        }

        foreach ((_, _, ContentEntry entry) in catalog.Entries(MightAndMagic7Scene.LookDefinitionKind))
        {
            if (!entry.Id.StartsWith("decoration-", StringComparison.Ordinal) || !entry.Payload.TryGetProperty("name", out JsonElement name)) continue;
            if (int.TryParse(entry.Id.AsSpan("decoration-".Length), NumberStyles.None, CultureInfo.InvariantCulture, out int row))
            {
                switches._decorations.TryAdd(name.GetString() ?? string.Empty, row);
                if (entry.Payload.TryGetProperty("height", out JsonElement height) && entry.Payload.TryGetProperty("radius", out JsonElement radius))
                    switches._sizes[row] = (height.GetInt32(), radius.GetInt32());
            }
        }

        return switches;
    }

    /// <summary>The place material a bitmap name draws as on a face, or null when the place's render entry lists none.</summary>
    internal int? Material(PlaceId place, string texture) =>
        _materials.TryGetValue(place, out Dictionary<string, int>? named) && named.TryGetValue(texture, out int index) ? index : null;

    /// <summary>How tall a decoration of a row stands and how far it reaches out, in place units, or null when content carries no look for it.</summary>
    internal (int Height, int Radius)? DecorationSize(int row) => _sizes.TryGetValue(row, out var size) ? size : null;

    /// <summary>The decoration-list row a decoration name is, or null when content carries no look of that name.</summary>
    internal int? Decoration(string name) => _decorations.TryGetValue(name, out int row) ? row : null;

    /// <summary>
    /// Whether a decoration is hidden now: as its level marks it, until a sprite step on its cog shows or hides it. A hidden
    /// decoration is neither drawn nor offered as something to use (OpenEnroth <c>src/Engine/Graphics/Renderer/BaseRenderer.cpp:161-163</c>).
    /// </summary>
    /// <param name="placement">The decoration.</param>
    /// <param name="values">Its place's ledger values.</param>
    internal static bool IsHidden(PlacementDefinition placement, IReadOnlyDictionary<string, long> values)
    {
        int cog = placement.Source.GetInt32(CogField) ?? 0;
        if (cog != 0 && values.TryGetValue(ShownKey(cog), out long shown)) return shown == 0;
        return ((placement.Source.GetInt32(FlagsField) ?? 0) & InvisibleDecoration) != 0;
    }

    /// <summary>The decoration-list row a sprite step gave a decoration's cog, or null when none has.</summary>
    internal static int? LookOf(PlacementDefinition placement, IReadOnlyDictionary<string, long> values) =>
        (placement.Source.GetInt32(CogField) ?? 0) is var cog and not 0 && values.TryGetValue(LookKey(cog), out long row) ? Index(row) : null;

    /// <summary>The material a switch's faces draw with, kept under its name, or null when none is or the value is no index.</summary>
    internal static int? MaterialOf(int cog, IReadOnlyDictionary<string, long> values) =>
        values.TryGetValue(TextureKey(cog), out long material) ? Index(material) : null;

    /// <summary>A kept value read as a list index, or null when it is not one.</summary>
    private static int? Index(long value) => value is >= 0 and <= int.MaxValue ? (int)value : null;

    /// <summary>
    /// Judges one value a save says a place keeps under a switch's name: whether the name is a switch's, and when it is,
    /// what is wrong with the value, if anything — a cog of zero, a visibility other than 0 or 1, a material the place's
    /// render entry does not list, a decoration row content carries no look for.
    /// </summary>
    /// <returns>Null when the name is not a switch's; otherwise the problem, or an empty string for none.</returns>
    internal string? Judge(PlaceId place, string key, long value)
    {
        foreach ((string prefix, Func<long, string?> check) in new (string, Func<long, string?>)[]
        {
            (HiddenPrefix, value => value is 0 or 1 ? null : "a face cog is hidden (1) or shown (0)"),
            (ShownPrefix, value => value is 0 or 1 ? null : "a decoration cog is shown (1) or hidden (0)"),
            (TexturePrefix, value => Index(value) is { } material && _materials.TryGetValue(place, out Dictionary<string, int>? named)
                && named.ContainsValue(material) ? null : "a retextured face cog draws a face material the place's render entry lists"),
            (LookPrefix, value => Index(value) is { } row && _sizes.ContainsKey(row) ? null : "a decoration cog takes a decoration row content carries a look for"),
        })
        {
            if (!key.StartsWith(prefix, StringComparison.Ordinal)) continue;
            if (!int.TryParse(key.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int cog) || cog == 0)
                return "a cog is named by a nonzero number";
            return check(value) ?? string.Empty;
        }

        return null;
    }

    internal static string HiddenKey(int cog) => HiddenPrefix + cog.ToString(CultureInfo.InvariantCulture);

    internal static string TextureKey(int cog) => TexturePrefix + cog.ToString(CultureInfo.InvariantCulture);

    internal static string ShownKey(int cog) => ShownPrefix + cog.ToString(CultureInfo.InvariantCulture);

    internal static string LookKey(int cog) => LookPrefix + cog.ToString(CultureInfo.InvariantCulture);
}
