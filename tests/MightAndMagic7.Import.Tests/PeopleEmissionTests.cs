using System.Text.Json;
using MightAndMagic7.Import.Lod;
using MightAndMagic7.Import.Maps;
using MightAndMagic7.Import.Packs;
using MightAndMagic7.Import.Tables;
using MightAndMagic7.Import.Tool;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Sessions;
using Xunit;

namespace MightAndMagic7.Import.Tests;

/// <summary>
/// The people a shipped installation turns out to carry: who the tables place, who a map's own records
/// place, and what the import does with everybody it cannot carry.
/// </summary>
/// <remarks>
/// The fixture is synthetic, so every count here is a fact about the reader rather than about the
/// operator's data: one actor in the open, two people the building table places, one of them in a building
/// nothing was placed for, one topic the table gates on an errand, and one it states with no answer at all.
/// What the operator's own installation carries is reported by <c>mm7import people</c>.
/// </remarks>
public sealed class PeopleEmissionTests
{
    private static readonly ContentLayout Layout = new("content-packs", "imports", "bundles");

    [Fact]
    public void An_actor_that_names_an_NPC_row_is_a_person_and_a_monster_is_not()
    {
        // The fixture's delta carries two actors: the first is one of the game's people, the second a
        // nameless creature. What tells them apart is the identity the record names, which the NPC table's
        // own row numbers are — not the name the record spells, which the shipped data fills with the
        // creature's kind.
        MapDelta delta = Delta(withPerson: true);
        Assert.Equal(2, delta.ActorCount);
        Assert.Equal(1, delta.PersonCount);

        MapActor person = delta.Actors[0];
        Assert.True(person.IsPerson);
        Assert.Equal(1, person.NpcId);
        Assert.Equal(0, person.MonsterId);
        Assert.Equal("Tester One", person.Name);
        Assert.Equal(new MapPoint(320, -640, 64), person.Position);
        Assert.Equal(512, person.YawAngle);
        Assert.Equal(20, person.HitPoints);

        MapActor creature = delta.Actors[1];
        Assert.False(creature.IsPerson);
        Assert.Equal(0, creature.NpcId);
        Assert.Contains("monster", creature.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void The_people_tables_and_the_maps_records_become_the_people_a_place_holds()
    {
        string installRoot = SyntheticInstallation.Create(withMaps: true, withServices: true, withPeople: true);
        try
        {
            LodInstall install = LodInstall.Open(installRoot);
            Mm7Tables tables = Mm7Tables.Read(install);
            IReadOnlyDictionary<int, DecodedMap> maps = DecodeAll(install);
            PlaceServiceSummary services = PlaceServiceEmitter.Emit(
                tables.Services,
                tables,
                Events.EvtProgram.ReadAll(install),
                maps);
            PlacePeopleSummary people = PlacePeopleEmitter.Emit(tables.People, maps, services);

            // The NPC table's own rows become people, whatever they are placed for: the table is who exists,
            // and a person with nothing to say is still somebody the party can meet.
            Assert.Equal(3, tables.People.Npcs.Count);
            Assert.Equal(3, people.PersonCount);
            Assert.Equal(2, tables.People.PlacedCount);
            Assert.Equal(2, tables.People.PlacedHouseCount);

            // One of them stands where a map's own actor record puts them, which is the only source of a
            // position for somebody standing in the open.
            // Every region shares the fixture's one outdoor delta, so the one person it places stands in
            // each of the thirteen; what the reader proves here is who they are and where they stand.
            Assert.Equal(13, people.PlacementCount);
            Assert.Equal(1, people.PlacedPersonCount);
            PlacePersonPlacement standing = people.Placements[0];
            Assert.Equal(1, standing.PlaceId);
            Assert.Equal("npc-1", standing.PersonId);
            Assert.Equal("person-0", standing.PlacementId);
            Assert.Equal(320, standing.X);
            Assert.Equal(-640, standing.Y);
            Assert.Equal(64, standing.Z);
            Assert.Equal(512, standing.Yaw);
            Assert.Equal(0, standing.SourceActorIndex);
            Assert.Equal("Tester One", standing.SourceActorName);

            // Two of them are placed in buildings by the table's own column, and the import knows which of
            // those buildings it managed to place a door for: the other one is a remainder with a reason.
            Assert.Equal(2, people.ResidentCount);
            Assert.Equal(1, people.ReachableHouseholdCount);
            Assert.Equal(1, people.UnreachableResidentCount);
            PlaceHousehold reached = Assert.Single(people.Households, household => household.PlacementId.Length > 0);
            Assert.Equal(["npc-1"], reached.PersonIds);
            Assert.Equal(98, reached.BuildingId);
            Assert.NotEqual(0, reached.PlaceId);
            Assert.Contains("service-", reached.PlacementId, StringComparison.Ordinal);

            PlacePeopleRefusal unreachable = Assert.Single(
                people.Refusals,
                refusal => string.Equals(refusal.Code, "house-without-a-door", StringComparison.Ordinal));
            Assert.Contains("building 999", unreachable.Subject, StringComparison.Ordinal);
            Assert.Contains("No face of the building's map", unreachable.Reason, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(installRoot, recursive: true);
        }
    }

    [Fact]
    public void A_person_carries_the_greetings_and_topics_the_tables_give_them()
    {
        PlacePeopleSummary people = Derive(withPerson: true);
        PlacePerson one = Assert.Single(people.People, person => person.Id == "npc-1");

        // The name, the portrait, and both greetings come from the tables, and the two greetings stay apart
        // because the game says which meeting each is for.
        Assert.Equal("Tester One", one.Name);
        Assert.Equal("709", one.Portrait);
        Assert.Equal("Well met, travellers.", one.Greeting);
        Assert.Equal("Back again, are you?", one.GreetingAgain);
        Assert.Equal(98, one.House);
        Assert.Equal(2, one.DialogueEvents);

        // What the person can be asked about is the topic table's own rows for them, with the first answer
        // the text table gives and the count of the answers the row named.
        PlacePersonTopic topic = Assert.Single(one.Topics);
        Assert.Equal("topic-1", topic.Id);
        Assert.Equal("The contest", topic.Label);
        Assert.Equal("The first to bring the items wins.", topic.Text);
        Assert.Equal(1, topic.TextCount);
        Assert.Equal(0, topic.Requires);

        // A topic the table states with no answer at all is not something anybody can say, so it is refused
        // with its own reason rather than emitted as a choice that does nothing.
        Assert.Contains(
            people.Refusals,
            refusal => string.Equals(refusal.Code, "topic-without-text", StringComparison.Ordinal));
        Assert.DoesNotContain(people.People, person => person.Topics.Any(item => item.Id == "topic-3"));
    }

    [Fact]
    public void A_written_pack_carries_the_people_and_the_product_loads_them()
    {
        string installRoot = SyntheticInstallation.Create(withMaps: true, withServices: true, withPeople: true);
        string root = Path.Combine(Path.GetTempPath(), $"mm7-people-{Guid.NewGuid():N}");
        try
        {
            PackWriteResult written = PackWriter.Write(LodInstall.Open(installRoot), Path.Combine(root, "imports"));
            Assert.Equal(3, written.People.PersonCount);

            // The fixture's thirteen regions share one delta payload, so the one person it places stands in
            // every one of them: the count is the maps the record was read for, not a count of people.
            Assert.Equal(13, written.People.PlacementCount);

            WriteBundle(root, written.PackIds);
            ContentBootstrapResult bootstrap = ContentBootstrap.Load(new FileContentSource(root), Layout, "imported");
            Assert.True(bootstrap.IsValid, string.Join("; ", bootstrap.Issues.Select(issue => issue.ToString())));
            ContentCatalog catalog = bootstrap.Catalog.RequireValid();

            // The people are one document of the pack the tables are written into, and the places name them:
            // a placement carries the identities standing there, and every one of them resolves.
            Assert.Equal(3, catalog.Entries(PlacePeopleEmitter.PersonDefinitionKind).Count());
            (_, _, ContentEntry person) = catalog.Entries(PlacePeopleEmitter.PersonDefinitionKind)
                .Single(entry => string.Equals(entry.Entry.Id, "npc-1", StringComparison.Ordinal));
            Assert.Equal("Tester One", person.GetString("name"));
            Assert.Equal("Well met, travellers.", person.GetString("greeting"));
            Assert.Equal(1, person.GetArray("topics").Count);
            Assert.Equal("The contest", ContentEntry.ReadString(person.GetArray("topics")[0], "label"));

            // The map's own record is a placement of its own, and the building's people are written into the
            // building's placement, so a counter and the people behind it are one thing to walk up to.
            JsonElement standing = Placements(catalog)
                .First(element => ContentEntry.ReadString(element, "kind") == "person"
                    && ContentEntry.ReadId(element, "id") == "person-0"
                    && ContentEntry.ReadString(element, "actorName") == "Tester One");
            Assert.Equal("person-0", ContentEntry.ReadId(standing, "id"));
            Assert.Equal("npc-1", Assert.Single(standing.GetProperty("people").EnumerateArray()).GetString());
            Assert.Equal(320, standing.GetProperty("x").GetDouble());

            JsonElement building = Assert.Single(
                Placements(catalog).Where(element => string.Equals(
                    ContentEntry.ReadString(element, "kind"),
                    PlacePeopleEmitter.PersonPlacementKind,
                    StringComparison.Ordinal) == false && Holds(people: element)));
            Assert.Contains("service-", ContentEntry.ReadId(building, "id"), StringComparison.Ordinal);
            Assert.Equal("npc-1", Assert.Single(building.GetProperty("people").EnumerateArray()).GetString());
        }
        finally
        {
            Directory.Delete(installRoot, recursive: true);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Two_runs_over_the_same_installation_emit_the_same_people()
    {
        PlacePeopleSummary first = Derive(withPerson: true);
        PlacePeopleSummary second = Derive(withPerson: true);

        Assert.Equal(first.PersonCount, second.PersonCount);
        Assert.Equal(
            first.People.Select(person => $"{person.Id}|{person.Name}|{person.Greeting}|{person.Topics.Count}"),
            second.People.Select(person => $"{person.Id}|{person.Name}|{person.Greeting}|{person.Topics.Count}"));
        Assert.Equal(
            first.Placements.Select(placement => $"{placement.PlaceId}|{placement.PlacementId}|{placement.X}|{placement.Y}|{placement.Z}"),
            second.Placements.Select(placement => $"{placement.PlaceId}|{placement.PlacementId}|{placement.X}|{placement.Y}|{placement.Z}"));
        Assert.Equal(
            first.Refusals.Select(refusal => $"{refusal.Code}|{refusal.Subject}"),
            second.Refusals.Select(refusal => $"{refusal.Code}|{refusal.Subject}"));
    }

    /// <summary>Derives the people one synthetic installation carries, without writing a pack.</summary>
    private static PlacePeopleSummary Derive(bool withPerson)
    {
        string installRoot = SyntheticInstallation.Create(withMaps: true, withServices: true, withPeople: withPerson);
        try
        {
            LodInstall install = LodInstall.Open(installRoot);
            Mm7Tables tables = Mm7Tables.Read(install);
            IReadOnlyDictionary<int, DecodedMap> maps = DecodeAll(install);
            return PlacePeopleEmitter.Emit(
                tables.People,
                maps,
                PlaceServiceEmitter.Emit(tables.Services, tables, Events.EvtProgram.ReadAll(install), maps));
        }
        finally
        {
            Directory.Delete(installRoot, recursive: true);
        }
    }

    private static MapDelta Delta(bool withPerson)
    {
        LodPayload level = new(new LodEntry("out01.odm", 0, 0), MapDecoderTests.OutdoorPayload(), LodPayloadKind.Verbatim);
        LodPayload delta = new(
            new LodEntry("out01.ddm", 0, 0),
            MapDecoderTests.OutdoorDeltaPayload(withPerson),
            LodPayloadKind.Verbatim);
        return Assert.IsType<MapDelta>(MapDecoder.DecodeOutdoor(level, delta).Delta);
    }

    private static IReadOnlyDictionary<int, DecodedMap> DecodeAll(LodInstall install)
    {
        MapDecodeReport report = MapDecoder.DecodeAll(install);
        Assert.Equal(0, report.FailureCount);
        Dictionary<int, DecodedMap> maps = [];
        foreach (MapDecodeOutcome outcome in report.Decoded)
        {
            if (outcome.Decoded is not null) maps[outcome.Map.Id] = outcome.Decoded;
        }

        return maps;
    }

    /// <summary>Every placement of every place the written packs declare.</summary>
    private static IEnumerable<JsonElement> Placements(ContentCatalog catalog) =>
        catalog.Entries("place").SelectMany(entry => entry.Entry.GetArray("placements"));

    /// <summary>Whether a placement carries people, which is what puts somebody behind a counter.</summary>
    private static bool Holds(JsonElement people) =>
        people.ValueKind == JsonValueKind.Object &&
        people.TryGetProperty(PlacePeopleEmitter.PlacementPeopleField, out JsonElement value) &&
        value.ValueKind == JsonValueKind.Array &&
        value.GetArrayLength() > 0;

    private static void WriteBundle(string root, IReadOnlyList<string> packIds)
    {
        string directory = Path.Combine(root, "bundles", "imported");
        Directory.CreateDirectory(directory);
        File.WriteAllText(
            Path.Combine(directory, "bundle.json"),
            $$"""
            {
              "schemaVersion": 1,
              "bundleId": "imported",
              "ruleset": "mightandmagic7",
              "contentPacks": [{{string.Join(", ", packIds.Select(id => $"\"{id}\""))}}],
              "description": "the packs an import wrote"
            }
            """);
    }
}
