using System.Globalization;
using MightAndMagic7.Import.Maps;
using MightAndMagic7.Import.Tables;

namespace MightAndMagic7.Import.Packs;

/// <summary>One creature a map's own actor record stands in its place, as the content pack carries it.</summary>
/// <remarks>
/// <para>
/// This is the record and nothing chosen: the monster row it is, where it stands and faces, its group, its
/// attribute bits and AI state, and its index in the delta's own array — the number a map event counting one
/// creature's death names it by (OpenEnroth <c>src/Engine/Objects/Actor.cpp:2811-2834</c>, which indexes the level's
/// actor array). The ruleset stands it as a creature when the place is populated.
/// </para>
/// <para>
/// <b>A hidden record is carried, not dropped.</b> A record whose AI state is nineteen, or that carries the attribute
/// bit <c>0x10000</c>, is one the level holds without showing or running it until something clears the bit: the
/// donor's state is <c>Disabled</c>, a region's load turns every record carrying the bit to it
/// (OpenEnroth <c>src/Engine/Graphics/Outdoor.cpp:617-618</c>), only clearing the bit stands it again
/// (<c>src/Engine/Objects/Actor.cpp:124-136</c>, <c>3823-3851</c>), and MMExtension names both <c>Invisible</c>
/// (<c>Scripts/Core/ConstAndBits.lua:125,156</c>). Over the operator's install 90 interior records state nineteen
/// and 91 records of the second region carry the bit at state zero; none carries a group, and no shipped event step
/// clears the bit, so all 181 stay hidden. Reading the bit alone as hidden in an interior is ours — the donor's
/// interior load does not read it (<c>src/Engine/Graphics/Indoor.cpp:979-998</c>) — and no shipped record tests the
/// difference. It is written with <see cref="PlaceCreaturePlacement.Hidden"/> set, so the pack states what the map
/// holds and the ruleset decides that a hidden creature does not stand.
/// </para>
/// </remarks>
/// <param name="PlaceId">The place the record belongs to.</param>
/// <param name="PlacementId">The placement's identity within the place, prefixed with the kind it is written under.</param>
/// <param name="X">Where the creature stands along the place's first axis.</param>
/// <param name="Y">Where the creature stands along the place's second axis.</param>
/// <param name="Z">Where the creature stands in height.</param>
/// <param name="Yaw">Which way the creature faces, in the game's own angle units.</param>
/// <param name="SourceActorIndex">The actor record's index in the delta's own array.</param>
/// <param name="SourceActorName">The name the record carries, empty when it states none.</param>
/// <param name="MonsterId">The monster row the record's own monster info names.</param>
/// <param name="MonsterName">The row's name, so a report reads without joining the table.</param>
/// <param name="HitPoints">The record's stored hit points, which the donor replaces with the row's on load.</param>
/// <param name="Group">The record's own group, zero when it belongs to none.</param>
/// <param name="Attributes">The record's attribute bits, as the record stores them.</param>
/// <param name="AiState">The record's AI state, as the record stores it.</param>
/// <param name="SectorId">The sector the record names, meaningful indoors and zero outdoors.</param>
/// <param name="UniqueNameIndex">The record's unique-name index, non-zero when the creature has a name of its own.</param>
/// <param name="Hidden">Whether the record is one the level holds hidden until something reveals it.</param>
public sealed record PlaceCreaturePlacement(
    int PlaceId,
    string PlacementId,
    double X,
    double Y,
    double Z,
    int Yaw,
    int SourceActorIndex,
    string SourceActorName,
    int MonsterId,
    string MonsterName,
    int HitPoints,
    int Group,
    int Attributes,
    int AiState,
    int SectorId,
    int UniqueNameIndex,
    bool Hidden);

/// <summary>One actor record the import could place no creature for, with the reason.</summary>
/// <param name="Code">A short stable code for the kind of refusal.</param>
/// <param name="Subject">What the refusal is about, so a reader can find the record.</param>
/// <param name="Reason">Why nothing was placed, in terms a person can act on.</param>
public sealed record PlaceCreatureRefusal(string Code, string Subject, string Reason);

/// <summary>What one import's reading of the maps' own creatures produced.</summary>
/// <param name="Placements">Every creature placed, in place and record order.</param>
/// <param name="Refusals">Every record nothing was placed for, with its reason.</param>
/// <param name="ActorRecords">How many actor records the decoded deltas carry that are not people.</param>
/// <param name="Notes">What the read noticed about the data, for a report.</param>
public sealed record PlaceCreatureSummary(
    IReadOnlyList<PlaceCreaturePlacement> Placements,
    IReadOnlyList<PlaceCreatureRefusal> Refusals,
    int ActorRecords,
    IReadOnlyList<string> Notes)
{
    /// <summary>An import that read no maps, so nothing was read.</summary>
    public static PlaceCreatureSummary Empty { get; } = new([], [], 0, []);

    /// <summary>How many creatures the maps' own records place.</summary>
    public int CreatureCount => Placements.Count;

    /// <summary>How many of them the level holds hidden until something reveals them.</summary>
    public int HiddenCount => Placements.Count(placement => placement.Hidden);

    /// <summary>How many of them stand when the place is first entered.</summary>
    public int StandingCount => CreatureCount - HiddenCount;

    /// <summary>How many places hold at least one of them.</summary>
    public int PopulatedPlaces => Placements.Select(placement => placement.PlaceId).Distinct().Count();

    /// <summary>How many of them carry a name of their own.</summary>
    public int NamedCount => Placements.Count(placement => placement.UniqueNameIndex != 0);

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
}

/// <summary>
/// Reads the creatures a map's own actor records stand in its place: the monsters a level is built holding, beside
/// the encounters its spawn records ask for and the people its other records are.
/// </summary>
/// <remarks>
/// <para>
/// <b>What the array is.</b> A delta carries the level's whole actor array (<c>Actor_MM7</c>, OpenEnroth
/// <c>src/Engine/Snapshots/EntitySnapshots.h:764-809</c>), and the shipped deltas in <c>games.lod</c> are what the
/// original loads on a first visit, before its spawn records add their creatures after them
/// (<c>src/Engine/Graphics/Indoor.cpp:907-922</c>). An actor naming an NPC row is one of the game's people and is
/// placed by <see cref="PlacePeopleEmitter"/>; every other record is a creature, and is placed here under the
/// same index, so one array is read once into two kinds and its numbering is kept.
/// </para>
/// <para>
/// <b>What is refused.</b> A record naming a monster row the table does not carry has nothing to say how it fights,
/// and a record whose AI state says it is dying, dead or removed (states four, five and eleven,
/// <c>src/Engine/Objects/ActorEnums.h:56-77</c>) is a creature the level no longer holds; both are named per place
/// and record rather than written. The shipped data has neither.
/// </para>
/// <para>
/// <b>What is not read.</b> The record's stored hit points are carried as provenance: the donor resets a creature
/// that can act to its row's own on load (<c>src/Engine/Objects/Actor.cpp:2899-2925</c>), which is also what this
/// product's fight reads. The unique-name index is carried as the record states it; the names it indexes are not
/// imported.
/// </para>
/// </remarks>
public static class PlaceCreatures
{
    /// <summary>The placement kind a map's own creature record is written under.</summary>
    public const string PlacementKind = "actor";

    /// <summary>The attribute bit a hidden record carries (the donor's <c>ACTOR_UNKNOW11</c>).</summary>
    public const int HiddenAttribute = 0x0001_0000;

    /// <summary>The AI state a hidden record carries (the donor's <c>Disabled</c>).</summary>
    public const int HiddenAiState = 19;

    /// <summary>The AI states that say a record is no longer standing in the level: dying, dead and removed.</summary>
    private static readonly int[] GoneStates = [4, 5, 11];

    /// <summary>Reads every place's own creatures from the actor records its decoded delta carries.</summary>
    /// <param name="tables">The rule tables, whose monster table names each record's row.</param>
    /// <param name="maps">The decoded maps, keyed by the place id the map table gives them.</param>
    /// <returns>Every creature placed, and every record nothing was placed for with its reason.</returns>
    public static PlaceCreatureSummary Emit(Mm7Tables tables, IReadOnlyDictionary<int, DecodedMap> maps)
    {
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(maps);

        Dictionary<int, MonsterRecord> rows = [];
        foreach (MonsterRecord row in tables.Monsters.Monsters) rows.TryAdd(row.Id, row);

        List<PlaceCreaturePlacement> placements = [];
        List<PlaceCreatureRefusal> refusals = [];
        int records = 0;
        foreach ((int placeId, DecodedMap map) in maps.OrderBy(entry => entry.Key))
        {
            if (map.Delta is not { } delta) continue;
            foreach (MapActor actor in delta.Actors)
            {
                if (actor.IsPerson) continue;
                records++;
                string subject = string.Create(CultureInfo.InvariantCulture, $"place {placeId} ('{map.FileName}') actor {actor.Index}");
                if (!rows.TryGetValue(actor.MonsterId, out MonsterRecord row))
                {
                    refusals.Add(new PlaceCreatureRefusal(
                        "actor-monster-unknown",
                        subject,
                        string.Create(CultureInfo.InvariantCulture, $"The record names monster row {actor.MonsterId}, which the monster table does not carry, so nothing says how the creature fights.")));
                    continue;
                }

                if (GoneStates.Contains(actor.AiState))
                {
                    refusals.Add(new PlaceCreatureRefusal(
                        "actor-not-standing",
                        subject,
                        string.Create(CultureInfo.InvariantCulture, $"The record's AI state is {actor.AiState}, which is a creature dying, dead or removed rather than one the level holds.")));
                    continue;
                }

                placements.Add(new PlaceCreaturePlacement(
                    placeId,
                    $"{PlacementKind}-{actor.Index.ToString(CultureInfo.InvariantCulture)}",
                    actor.Position.X,
                    actor.Position.Y,
                    actor.Position.Z,
                    actor.YawAngle,
                    actor.Index,
                    actor.Name,
                    row.Id,
                    row.Name,
                    actor.HitPoints,
                    actor.Group,
                    actor.Attributes,
                    actor.AiState,
                    actor.SectorId,
                    actor.UniqueNameIndex,
                    actor.AiState == HiddenAiState || (actor.Attributes & HiddenAttribute) != 0));
            }
        }

        List<string> notes = [];
        if (records > 0)
        {
            PlaceCreatureSummary counted = new([.. placements], [], records, []);
            notes.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"{records} actor records name no person: {counted.CreatureCount} creatures were placed into {counted.PopulatedPlaces} places, {counted.HiddenCount} of them hidden until something reveals them, and {refusals.Count} records were refused."));
        }

        return new PlaceCreatureSummary([.. placements], [.. refusals], records, [.. notes]);
    }
}
