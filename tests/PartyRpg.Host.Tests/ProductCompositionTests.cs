using PartyRpg.Kit.Content;
using PartyRpg.Kit.Sessions;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Host.Tests;

/// <summary>
/// The product's composition seam: which bundle it starts from, what it reports, and what stops it.
/// </summary>
public sealed class ProductCompositionTests
{
    [Fact]
    public void The_product_starts_from_the_bundle_it_ships_and_reports_it()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(
        [
            ProductTestContext.Bundle("partyrpg-default", "places", "creation-tables"),
            .. ProductTestContext.Pack("places", "emerald"),
            .. ProductTestContext.CreationTables(),
        ]);

        using CrawlerProduct product = new(context);

        Assert.Equal(BuiltInBundles.Default, product.Selection.BundleId);
        Assert.Equal(2, product.Selection.PackCount);

        product.Start();
        product.Attach();

        UiProjection projection = ui.Latest();
        Assert.Equal("crawler.hud", ui.LastRequest?.Stream);
        Assert.Equal("crawler.ui.snapshot.v1", ui.LastRequest?.Contract);
        // A new game is created: the product's session starts in creation, and its party does not exist
        // until the player accepts one.
        Assert.Equal(SessionMode.Creating, product.Mode);
        ProjectedNode creation = ProjectedNode.Of(projection.Value).Field("creation");
        Assert.True(creation.Field("active").AsBoolean());
        Assert.True(creation.Field("hasDefault").AsBoolean());
        Assert.Equal(4d, creation.Field("members").AsNumber());
        Assert.Equal(0d, creation.Field("member").AsNumber());
        // The ruleset's default party is applied, so the flow opens finished and ready to accept: choosing a
        // member to change is what reopens it a step at a time.
        Assert.Equal("complete", creation.Field("step").AsString());
        Assert.Equal(4, creation.Field("roster").Count());
    }

    [Fact]
    public void The_projection_carries_the_bundle_and_the_content_it_resolved_to()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(
        [
            ProductTestContext.Bundle("partyrpg-default", "places", "creation-tables"),
            .. ProductTestContext.Pack("places", "emerald"),
            .. ProductTestContext.CreationTables(),
        ]);

        using CrawlerProduct product = new(context);
        product.Start();

        ProjectedNode value = ProjectedNode.Of(ui.Latest().Value);
        ProjectedNode composition = value.Field("composition");
        Assert.Equal("partyrpg-default", composition.Field("bundle").AsString());
        Assert.Equal(2.0, composition.Field("contentPacks").AsNumber());
        Assert.Equal("mightandmagic7", composition.Field("ruleset").AsString());
    }

    [Fact]
    public void Content_that_is_present_and_wrong_stops_the_product_with_every_problem_named()
    {
        (ProductCreateContext context, _) = ProductTestContext.Create(
            ProductTestContext.Bundle("partyrpg-default", "absent-pack"));

        ContentValidationException error = Assert.Throws<ContentValidationException>(() => new CrawlerProduct(context));

        Assert.Contains("absent-pack", error.Message);
        Assert.Contains(error.Issues, issue => issue.Code == "bundle-pack-missing");
    }

    [Fact]
    public void A_product_whose_content_has_not_been_generated_yet_still_starts()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create();

        using CrawlerProduct product = new(context);

        Assert.Null(product.Selection.BundleId);
        Assert.Equal(0, product.Selection.PackCount);

        product.Start();
        product.Attach();

        // Content that has not been generated yet leaves the product with no world and no scenario party,
        // and creation is what still works: this game's choices and default party are compiled.
        Assert.Equal(SessionMode.Creating, product.Mode);
        Assert.Equal(string.Empty, ProjectedNode.Of(ui.Latest().Value).Field("composition").Field("bundle").AsString());
    }

    [Fact]
    public void While_creating_an_update_measures_nothing_and_accepting_the_party_starts_the_clock()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(
            [ProductTestContext.Bundle("partyrpg-default")]);

        using CrawlerProduct product = new(context);
        product.Start();

        // The admitted update drives the flow and nothing else: it is counted, and no interval is measured
        // for a world that is not being stepped.
        Assert.Equal(ProductUpdateResult.None, product.Update(ProductTestContext.Update(simulationStep: 100, admittedSteps: 4)));
        ProjectedNode session = ProjectedNode.Of(ui.Latest().Value).Field("session");
        Assert.Equal("creating", session.Field("mode").AsString());
        Assert.Equal(0d, session.Field("admittedSteps").AsNumber());
        Assert.Equal(0d, session.Field("simulationSeconds").AsNumber());
        Assert.Equal(1d, session.Field("updates").AsNumber());

        // Accepting the default party leaves creation for the world, and the next update is the one the
        // world's interval belongs to.
        product.Update(ProductTestContext.Update(
            simulationStep: 101,
            admittedSteps: 1,
            ProductTestContext.Digital(ProductIdentity.CreationAcceptIntent)));
        product.Update(ProductTestContext.Update(simulationStep: 102, admittedSteps: 4));

        session = ProjectedNode.Of(ui.Latest().Value).Field("session");
        Assert.Equal("running", session.Field("mode").AsString());
        Assert.Equal(4d, session.Field("admittedSteps").AsNumber());
        Assert.Equal(4.0 / 60.0, session.Field("simulationSeconds").AsNumber(), precision: 9);
        Assert.True(ProjectedNode.Of(ui.Latest().Value).Field("party").Field("present").AsBoolean());
    }

    [Fact]
    public void A_bundle_with_a_world_places_the_created_party_where_its_scenario_names()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(
        [
            ProductTestContext.Bundle("partyrpg-default", "world", "creation-tables"),
            .. ProductTestContext.CreationTables(),
            ($"{ProductTestContext.ContentDirectory}/content-packs/world/pack.json",
                """
                {
                  "schemaVersion": 1,
                  "packId": "world",
                  "kind": "definitions",
                  "provenance": { "description": "authored for a test" },
                  "documents": [
                    { "path": "places.json", "documentId": "places", "definitionKind": "place" },
                    { "path": "links.json", "documentId": "links", "definitionKind": "travel-link" },
                    { "path": "start.json", "documentId": "start", "definitionKind": "scenario-start" }
                  ]
                }
                """),
            ($"{ProductTestContext.ContentDirectory}/content-packs/world/places.json",
                """
                {
                  "documentId": "places",
                  "definitionKind": "place",
                  "entries": [
                    { "id": "1", "kind": "region", "name": "Emerald Island", "respawnDays": 7,
                      "entryPoints": [ { "id": "Party Start", "x": 12552, "y": 800, "z": 160, "yaw": 512 } ] },
                    { "id": "2", "kind": "interior", "name": "Cave", "respawnDays": 7,
                      "entryPoints": [ { "id": "Party Start", "x": 1, "y": 2, "z": 3, "yaw": 0 } ] }
                  ]
                }
                """),
            ($"{ProductTestContext.ContentDirectory}/content-packs/world/links.json",
                """
                {
                  "documentId": "links",
                  "definitionKind": "travel-link",
                  "entries": [ { "id": "0", "fromPlace": "1", "toPlace": "2", "entryPoint": "Party Start" } ]
                }
                """),
            ($"{ProductTestContext.ContentDirectory}/content-packs/world/start.json",
                """
                { "documentId": "start", "definitionKind": "scenario-start", "entries": [ { "id": "start", "place": "1", "entryPoint": "Party Start" } ] }
                """),
        ]);

        using CrawlerProduct product = new(context);
        product.Start();

        // Nothing is placed until a party exists: the world is composed with the party that walks in it.
        ProjectedNode before = ProjectedNode.Of(ui.Latest().Value).Field("world");
        Assert.Equal(string.Empty, before.Field("place").AsString());
        Assert.Equal(0d, before.Field("places").AsNumber());

        product.Update(ProductTestContext.Update(
            simulationStep: 1,
            admittedSteps: 1,
            ProductTestContext.Digital(ProductIdentity.CreationAcceptIntent)));
        product.Attach();

        ProjectedNode world = ProjectedNode.Of(ui.Latest().Value).Field("world");
        Assert.Equal("1", world.Field("place").AsString());
        Assert.Equal("Emerald Island", world.Field("name").AsString());
        Assert.Equal("region", world.Field("kind").AsString());
        Assert.Equal(1.0, world.Field("visited").AsNumber());
        Assert.Equal(2.0, world.Field("places").AsNumber());
    }

    [Fact]
    public void A_restart_reuses_the_bundle_it_started_with()
    {
        (ProductCreateContext context, _) = ProductTestContext.Create(
        [
            ProductTestContext.Bundle("partyrpg-default", "places", "creation-tables"),
            .. ProductTestContext.Pack("places", "emerald"),
            .. ProductTestContext.CreationTables(),
        ]);

        using CrawlerProduct product = new(context);
        product.Start();
        product.Restart();

        Assert.Equal("partyrpg-default", product.Selection.BundleId);
        // A restart is a new game, and a new game is created.
        Assert.Equal(SessionMode.Creating, product.Mode);
    }
}
