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
            [ProductTestContext.Bundle("partyrpg-default", "places"), .. ProductTestContext.Pack("places", "emerald")]);

        using CrawlerProduct product = new(context);

        Assert.Equal(BuiltInBundles.Default, product.Selection.BundleId);
        Assert.Equal(1, product.Selection.PackCount);

        product.Start();
        product.Attach();

        UiProjection projection = ui.Latest();
        Assert.Equal("crawler.hud", ui.LastRequest?.Stream);
        Assert.Equal("crawler.ui.snapshot.v1", ui.LastRequest?.Contract);
        Assert.Equal(SessionMode.Running, product.Mode);
    }

    [Fact]
    public void The_projection_carries_the_bundle_and_the_content_it_resolved_to()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(
            [ProductTestContext.Bundle("partyrpg-default", "places"), .. ProductTestContext.Pack("places", "emerald")]);

        using CrawlerProduct product = new(context);
        product.Start();

        ProjectedNode value = ProjectedNode.Of(ui.Latest().Value);
        ProjectedNode composition = value.Field("composition");
        Assert.Equal("partyrpg-default", composition.Field("bundle").AsString());
        Assert.Equal(1.0, composition.Field("contentPacks").AsNumber());
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

        Assert.Equal(SessionMode.Running, product.Mode);
        Assert.Equal(string.Empty, ProjectedNode.Of(ui.Latest().Value).Field("composition").Field("bundle").AsString());
    }

    [Fact]
    public void An_update_advances_the_admitted_simulation_the_projection_reports()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(
            [ProductTestContext.Bundle("partyrpg-default"), .. ProductTestContext.Pack("places", "emerald")]);

        using CrawlerProduct product = new(context);
        product.Start();

        Assert.Equal(ProductUpdateResult.None, product.Update(ProductTestContext.Update(simulationStep: 100, admittedSteps: 4)));

        ProjectedNode session = ProjectedNode.Of(ui.Latest().Value).Field("session");
        Assert.Equal("running", session.Field("mode").AsString());
        Assert.Equal(4.0, session.Field("admittedSteps").AsNumber());
        Assert.Equal(4.0 / 60.0, session.Field("simulationSeconds").AsNumber(), precision: 9);
    }

    [Fact]
    public void A_restart_reuses_the_bundle_it_started_with()
    {
        (ProductCreateContext context, _) = ProductTestContext.Create(
            [ProductTestContext.Bundle("partyrpg-default", "places"), .. ProductTestContext.Pack("places", "emerald")]);

        using CrawlerProduct product = new(context);
        product.Start();
        product.Restart();

        Assert.Equal("partyrpg-default", product.Selection.BundleId);
        Assert.Equal(SessionMode.Running, product.Mode);
    }
}
