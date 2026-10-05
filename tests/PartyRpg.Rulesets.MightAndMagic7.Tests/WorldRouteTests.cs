using System.Text.Json.Nodes;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.World;
using Xunit;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

public sealed class WorldRouteTests
{
    [ImportedFact("places.json")]
    public void The_ordinary_bundle_has_valid_boundary_routes_and_a_grounded_temple_exit()
    {
        var catalog = ImportedContent.Playable();
        var graph = MightAndMagic7World.Graph(catalog);
        var boundaries = PlaceEntranceLoader.Load(catalog, graph).Where(entrance => entrance.Boundary is not null).ToArray();
        Assert.Equal(26, boundaries.Length);
        Assert.All(boundaries, entrance => Assert.Equal(TransitionKind.Walking, entrance.Kind));
        Assert.All(boundaries, entrance => graph.ResolveArrival(entrance.Transition!));
        Assert.Equal(85, graph.Transitions.Count(transition => transition.IsFare));
        var exit = Assert.Single(catalog.Entries("travel-link"), row => row.Entry.GetString("program") == "NWC.EVT" && row.Entry.GetId("eventId") == "501").Entry;
        Assert.Equal("source-position-outside-destination", exit.GetString("arrivalNormalization"));
        var arrival = graph.ResolveArrival(Assert.Single(graph.Transitions, transition => transition.Source == exit.Id));
        Assert.Equal(graph.Require(new("2")).FindEntryPoint("Party Start")!.Pose, arrival);
    }

    [Fact]
    public void A_travel_item_uses_the_world_keeps_its_instance_and_can_leave_after_resume()
    {
        InMemoryPersistenceService persistence = new();
        var (context, ui) = RulesetTestContext.Create(persistence, Content());
        var composition = RulesetTestContext.RulesetContext(context, ui) with { Equip = new(Declared.UiActionContract) };
        using var session = (MightAndMagic7Session)MightAndMagic7Ruleset.Instance.CreateSession(composition);
        session.Start();
        ItemInstance bottle = session.Party!.CreateItem(new("650"));
        Assert.True(session.Party.AcquireItem(bottle).Admitted);
        var time = session.Owners.Clock!.Now;
        var food = session.Party.Food.Portions;
        Assert.True(session.Owners.ItemUses!.Use(0, bottle.Id).Applied);
        Assert.Equal(new PlaceId("2"), session.Owners.World!.Place);
        Assert.Same(bottle, session.Party.FindItem(bottle.Id));
        Assert.Equal(time, session.Owners.Clock.Now);
        Assert.Equal(food, session.Party.Food.Portions);
        Assert.False(session.Owners.ItemUses.Use(0, bottle.Id).Applied);
        MightAndMagic7Ruleset.Instance.Save(session);
        var (resumeContext, resumeUi) = RulesetTestContext.Create(persistence, Content());
        using var resumed = (MightAndMagic7Session)MightAndMagic7Ruleset.Instance.ResumeSession(RulesetTestContext.RulesetContext(resumeContext, resumeUi));
        resumed.Start();
        Assert.Equal(new PlaceId("2"), resumed.Owners.World!.Place);
        Assert.Equal(bottle.Definition, resumed.Party!.FindItem(bottle.Id)!.Definition);
        Assert.True(resumed.Owners.World.Travel(resumed.Owners.World.Graph.Transitions.Single(link => link.Source == "back"), TransitionKind.Entrance).Arrived);
        Assert.Equal(new PlaceId("1"), resumed.Owners.World.Place);
    }

    [Fact]
    public void Wetsuits_use_real_equipment_and_a_missing_or_broken_suit_refuses_without_charging()
    {
        var (context, ui) = RulesetTestContext.Create(Content());
        using var session = (MightAndMagic7Session)MightAndMagic7Ruleset.Instance.CreateSession(RulesetTestContext.RulesetContext(context, ui));
        session.Start();
        var party = session.Party!;
        var world = session.Owners.World!;
        var road = world.Graph.Transitions.Single(link => link.Source == "road");
        var time = session.Owners.Clock!.Now;
        Assert.Equal("travel-equipment-required", world.Travel(road, TransitionKind.Walking).Refusal!.Code);
        Assert.Equal(time, session.Owners.Clock.Now);
        Assert.Equal(new PlaceId("1"), world.Place);
        var catalog = ContentCatalogLoader.Load(RulesetTestContext.Content(context), ContentLayout.Under(RulesetTestContext.ContentDirectory));
        var equipment = MightAndMagic7EquipmentUse.Read(catalog, null)!;
        foreach (var member in party.Members)
        {
            var suit = party.CreateItem(new("604"));
            Assert.True(party.AcquireItem(suit).Admitted);
            Assert.Null(equipment.Judge(member, MightAndMagic7Figure.Armour, suit));
            Assert.True(party.Equip(member.Id, MightAndMagic7Figure.Armour, suit.Id).Admitted);
            Assert.True(MightAndMagic7Movement.WearsDivingSuit(member));
        }
        var damaged = party.Members[0].Equipment.ItemIn(MightAndMagic7Figure.Armour)!;
        damaged.TakeDamage(1);
        Assert.False(world.Travel(road, TransitionKind.Walking).Arrived);
        Assert.Equal(time, session.Owners.Clock.Now);
        Assert.False(MightAndMagic7Movement.WearsDivingSuit(party.Members[0]));
        damaged.Repair(1);
        int food = party.Food.Portions;
        var arrival = world.Travel(road, TransitionKind.Walking);
        Assert.True(arrival.Arrived);
        Assert.Equal(time.Day + 1, session.Owners.Clock.Now.Day);
        Assert.Equal(MightAndMagic7TravelCostRule.Crossing, arrival.ChargedCost);
        Assert.Equal(Math.Max(0, food - 1), party.Food.Portions);
    }

    [Fact]
    public void A_counter_can_sell_a_destination_without_a_counter_at_that_destination()
    {
        var (context, _) = RulesetTestContext.Create(Content());
        var catalog = ContentCatalogLoader.Load(RulesetTestContext.Content(context), ContentLayout.Under(RulesetTestContext.ContentDirectory)).RequireValid();
        var network = MightAndMagic7FareNetwork.Read(catalog);
        var offer = Assert.Single(network.SoldBy("54"));
        Assert.Equal(new PlaceId("2"), offer.Place);
        var graph = MightAndMagic7World.Graph(catalog);
        var fare = Assert.Single(graph.Transitions, link => link.IsFare);
        Assert.Equal(offer.Route, fare.FareRoute);
        Assert.Equal(new PlaceId("1"), fare.From);
        Assert.Equal(new PlacePose(5, 6, 7, 0, 0), graph.ResolveArrival(fare));
        Assert.Empty(network.SoldBy("nonexistent"));
    }

    internal static (string Path, string Text)[] Content()
    {
        var docs = new (string Name, string Kind, string[] Entries)[]
        {
            ("links", "travel-link", [
                """{"id":"bottle","toPlace":"2","entryPoint":"Party Start"}""",
                """{"id":"road","fromPlace":"1","toPlace":"2","entryPoint":"Party Start","requiresWornItem":"604","requiresSlot":"armour"}""",
                """{"id":"back","fromPlace":"2","toPlace":"1","entryPoint":"Party Start"}"""]),
            ("item-travel", "item-travel", ["""{"id":"bottle","item":"650","link":"bottle"}"""]),
            ("services", "service", ["""{"id":"54","name":"Coach","kind":"Stables"}"""]),
            ("destinations", "service-destination", ["""{"id":"special","service":"54","toPlace":"2"}"""]),
        };
        var files = EquipmentPolicyTests.Content().Select(file =>
        {
            var json = JsonNode.Parse(file.Text)!;
            if (file.Path.EndsWith("/pack.json", StringComparison.Ordinal))
                foreach (var doc in docs) json["documents"]!.AsArray().Add(new JsonObject { ["path"] = doc.Name + ".json", ["documentId"] = doc.Name, ["definitionKind"] = doc.Kind });
            if (file.Path.EndsWith("/places.json", StringComparison.Ordinal))
            {
                json["entries"]![0]!["placements"] = JsonNode.Parse("""[{"id":"coach","kind":"service","houseId":"54","x":0,"y":0,"z":0}]""");
                json["entries"]!.AsArray().Add(JsonNode.Parse("""{"id":"2","name":"Destination","kind":"interior","entryPoints":[{"id":"Party Start","x":5,"y":6,"z":7}]}"""));
            }
            if (file.Path.EndsWith("/items.json", StringComparison.Ordinal))
                foreach (string entry in new[] { """{"id":"604","name":"Wetsuit","type":"misc","skill":"misc"}""", """{"id":"650","name":"Temple in a Bottle","type":"misc","skill":"misc"}""" })
                    json["entries"]!.AsArray().Add(JsonNode.Parse(entry));
            return (file.Path, json.ToJsonString());
        }).ToList();
        foreach (var doc in docs) files.Add(($"{RulesetTestContext.ContentDirectory}/content-packs/world/{doc.Name}.json", TestPacks.Document(doc.Name, doc.Kind, doc.Entries)));
        return files.ToArray();
    }
}
