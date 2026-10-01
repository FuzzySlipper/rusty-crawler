using System.Text.Json;
using MightAndMagic7.Import.Lod;
using MightAndMagic7.Import.Maps;
using MightAndMagic7.Import.Packs;
using MightAndMagic7.Import.Tables;
using MightAndMagic7.Import.Tool;
using Xunit;

namespace MightAndMagic7.Import.Tests;

/// <summary>
/// The creatures a level is built holding: the actor records of a map's delta that name no person, placed under the
/// index the level numbers them by.
/// </summary>
/// <remarks>
/// The fixture's region delta carries two actors — a person first, then a creature of monster row four in group
/// three — and every region shares it, so each of the thirteen regions holds the one creature. What the operator's
/// own installation carries is checked by <c>mm7import verify</c>.
/// </remarks>
public sealed class CreatureEmissionTests
{
    [Fact]
    public void A_maps_own_creature_record_is_placed_with_its_row_its_group_and_its_index_and_a_person_is_not()
    {
        (Mm7Tables tables, IReadOnlyDictionary<int, DecodedMap> maps, string installRoot) = Decode(withPeople: true, hiddenCreature: false);
        try
        {
            PlaceCreatureSummary creatures = PlaceCreatures.Emit(tables, maps);

            // Two actors per region, one of them a person: the person is the people's, the creature is placed here.
            Assert.Equal(SyntheticInstallation.Regions, creatures.ActorRecords);
            Assert.Equal(SyntheticInstallation.Regions, creatures.CreatureCount);
            Assert.Equal(SyntheticInstallation.Regions, creatures.PopulatedPlaces);
            Assert.Empty(creatures.Refusals);
            Assert.Equal(0, creatures.HiddenCount);

            PlaceCreaturePlacement first = creatures.Placements[0];
            Assert.Equal(1, first.PlaceId);
            Assert.Equal("actor-1", first.PlacementId);
            Assert.Equal(1, first.SourceActorIndex);
            Assert.Equal(4, first.MonsterId);
            Assert.Equal("Monster 4", first.MonsterName);
            Assert.Equal(3, first.Group);
            Assert.Equal((-960d, 1280d, 32d), (first.X, first.Y, first.Z));
            Assert.Equal(1024, first.Yaw);
            Assert.Equal(30, first.HitPoints);
            Assert.False(first.Hidden);
        }
        finally
        {
            Directory.Delete(installRoot, recursive: true);
        }
    }

    [Fact]
    public void A_hidden_record_is_placed_marked_hidden_and_an_unknown_row_or_a_dead_record_is_refused_by_name()
    {
        (Mm7Tables tables, IReadOnlyDictionary<int, DecodedMap> maps, string installRoot) = Decode(withPeople: true, hiddenCreature: true);
        try
        {
            // The level holds it hidden — the donor's Disabled state — and the pack says so rather than dropping it.
            PlaceCreatureSummary creatures = PlaceCreatures.Emit(tables, maps);
            Assert.Equal(SyntheticInstallation.Regions, creatures.HiddenCount);
            Assert.Equal(0, creatures.StandingCount);
            PlaceCreaturePlacement hidden = creatures.Placements[0];
            Assert.True(hidden.Hidden);
            Assert.Equal(PlaceCreatures.HiddenAiState, hidden.AiState);

            // A record naming a row the table does not carry, and one whose state says it is dead, are refused per
            // place and record with the reason.
            PlaceCreatureSummary refused = PlaceCreatures.Emit(tables, new Dictionary<int, DecodedMap>
            {
                [1] = Region(MapDecoderTests.OutdoorDeltaPayload(withPerson: true, creatureMonster: 999)),
                [2] = Region(MapDecoderTests.OutdoorDeltaPayload(withPerson: true, creatureAiState: 5)),
            });
            Assert.Empty(refused.Placements);
            Assert.Equal(["actor-monster-unknown", "actor-not-standing"], refused.Refusals.Select(refusal => refusal.Code));
            Assert.Contains("place 1", refused.Refusals[0].Subject, StringComparison.Ordinal);
            Assert.Contains("actor 1", refused.Refusals[0].Subject, StringComparison.Ordinal);
            Assert.Contains("999", refused.Refusals[0].Reason, StringComparison.Ordinal);
            Assert.Contains("place 2", refused.Refusals[1].Subject, StringComparison.Ordinal);
            Assert.Contains("dead", refused.Refusals[1].Reason, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(installRoot, recursive: true);
        }
    }

    [Fact]
    public void The_written_place_carries_the_creature_under_the_actor_arrays_own_field_and_index()
    {
        string installRoot = SyntheticInstallation.Create(withMaps: true, withPeople: true);
        string root = Path.Combine(Path.GetTempPath(), $"mm7-creatures-{Guid.NewGuid():N}");
        try
        {
            string imports = Path.Combine(root, "imports");
            PackWriteResult written = PackWriter.Write(LodInstall.Open(installRoot), imports);
            Assert.Equal(SyntheticInstallation.Regions, written.Creatures.CreatureCount);

            using JsonDocument places = JsonDocument.Parse(File.ReadAllText(Path.Combine(imports, "mm7-tables", "places.json")));
            JsonElement first = places.RootElement.GetProperty("entries")[0];
            Assert.Equal(1, first.GetProperty("placementCounts").GetProperty("actor").GetInt32());
            JsonElement actor = Assert.Single(
                first.GetProperty("placements").EnumerateArray(),
                placement => placement.GetProperty("kind").GetString() == PlaceCreatures.PlacementKind);
            Assert.Equal("actor-1", actor.GetProperty("id").GetString());
            Assert.Equal("actors", actor.GetProperty("sourceField").GetString());
            Assert.Equal(1, actor.GetProperty("sourceIndex").GetInt32());
            Assert.Equal(4, actor.GetProperty("monster").GetInt32());
            Assert.Equal(3, actor.GetProperty("group").GetInt32());
            Assert.Equal(1024, actor.GetProperty("yaw").GetInt32());
            Assert.Equal("actor-record", actor.GetProperty("positionSource").GetString());
            Assert.False(actor.TryGetProperty("hidden", out _));

            // The person the same delta carries is still a person placement of its own, under its own index.
            JsonElement person = Assert.Single(
                first.GetProperty("placements").EnumerateArray(),
                placement => placement.GetProperty("kind").GetString() == PlacePeopleEmitter.PersonPlacementKind);
            Assert.Equal(0, person.GetProperty("sourceIndex").GetInt32());
        }
        finally
        {
            Directory.Delete(installRoot, recursive: true);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>One region decoded over the given delta.</summary>
    private static DecodedMap Region(byte[] delta) =>
        MapDecoder.DecodeOutdoor(
            new LodPayload(new LodEntry("out01.odm", 0, 0), MapDecoderTests.OutdoorPayload(), LodPayloadKind.Verbatim),
            new LodPayload(new LodEntry("out01.ddm", 0, 0), delta, LodPayloadKind.Verbatim));

    private static (Mm7Tables Tables, IReadOnlyDictionary<int, DecodedMap> Maps, string Root) Decode(bool withPeople, bool hiddenCreature)
    {
        string installRoot = SyntheticInstallation.Create(withMaps: true, withPeople: withPeople, hiddenCreature: hiddenCreature);
        LodInstall install = LodInstall.Open(installRoot);
        MapDecodeReport report = MapDecoder.DecodeAll(install);
        Assert.Equal(0, report.FailureCount);
        Dictionary<int, DecodedMap> maps = report.Decoded
            .Where(outcome => outcome.Decoded is not null)
            .ToDictionary(outcome => outcome.Map.Id, outcome => outcome.Decoded!);
        return (Mm7Tables.Read(install), maps, installRoot);
    }
}
