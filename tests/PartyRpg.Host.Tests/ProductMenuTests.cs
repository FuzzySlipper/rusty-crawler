using PartyRpg.Kit.Sessions;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Host.Tests;

/// <summary>The ordinary title and leave-expedition controls stay on the Host's one session lifecycle.</summary>
public sealed class ProductMenuTests
{
    [Fact]
    public void Fresh_entry_publishes_title_and_new_game_reveals_the_existing_creation_flow()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(ProductTestContext.DoorWorld());
        using CrawlerProduct product = new(context, ProductTestContext.NoVariables, showLaunchTitle: true);

        product.Start();
        ProjectedNode title = ProjectedNode.Of(ui.Latest().Value).Field("menu");
        Assert.True(title.Field("visible").AsBoolean());
        Assert.Equal("title", title.Field("screen").AsString());
        Assert.True(title.Field("canNewGame").AsBoolean());
        Assert.True(title.Field("canContinue").AsBoolean());

        product.Update(ProductTestContext.Update(1, 1, ProductTestContext.Payload("{\"action\":\"session.new-game\"}")));

        ProjectedNode adventure = ProjectedNode.Of(ui.Latest().Value);
        Assert.False(adventure.Field("menu").Field("visible").AsBoolean());
        Assert.Equal("adventure", adventure.Field("menu").Field("screen").AsString());
        Assert.Equal(SessionMode.Creating, product.Mode);
    }

    [Fact]
    public void Return_to_title_pauses_for_confirmation_then_rebuilds_a_fresh_session()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(ProductTestContext.DoorWorld());
        using CrawlerProduct product = new(context, ProductTestContext.NoVariables, showLaunchTitle: true);
        product.Start();

        // The keyboard shortcut is still an ordinary creation control, and selecting it from the title starts the
        // same creation flow the visible New Game action exposes.
        product.Update(ProductTestContext.Update(1, 1, ProductTestContext.Digital(ProductIdentity.CreationAcceptIntent)));
        Assert.Equal(SessionMode.Running, product.Mode);

        product.Update(ProductTestContext.Update(2, 1, ProductTestContext.Payload("{\"action\":\"session.return-title\"}")));
        ProjectedNode confirmation = ProjectedNode.Of(ui.Latest().Value).Field("menu");
        Assert.True(confirmation.Field("visible").AsBoolean());
        Assert.Equal("confirm-return", confirmation.Field("screen").AsString());
        Assert.True(confirmation.Field("hasUnsaved").AsBoolean());
        Assert.Equal(SessionMode.Paused, product.Mode);

        product.Update(ProductTestContext.Update(3, 1, ProductTestContext.Payload("{\"action\":\"session.confirm-return-title\"}")));
        ProjectedNode title = ProjectedNode.Of(ui.Latest().Value).Field("menu");
        Assert.True(title.Field("visible").AsBoolean());
        Assert.Equal("title", title.Field("screen").AsString());
        Assert.Equal(SessionMode.Starting, product.Mode);

        product.Update(ProductTestContext.Update(4, 1, ProductTestContext.Payload("{\"action\":\"session.new-game\"}")));
        Assert.Equal(SessionMode.Creating, product.Mode);
        Assert.False(ProjectedNode.Of(ui.Latest().Value).Field("menu").Field("visible").AsBoolean());
    }

    [Fact]
    public void Continue_failure_keeps_a_usable_title_and_explains_the_missing_save()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(ProductTestContext.DoorWorld());
        using CrawlerProduct product = new(context, ProductTestContext.NoVariables, showLaunchTitle: true);
        product.Start();

        product.Update(ProductTestContext.Update(1, 1, ProductTestContext.Payload("{\"action\":\"session.continue\"}")));

        ProjectedNode menu = ProjectedNode.Of(ui.Latest().Value).Field("menu");
        Assert.True(menu.Field("visible").AsBoolean());
        Assert.Equal("title", menu.Field("screen").AsString());
        Assert.Equal("failed", menu.Field("state").AsString());
        Assert.NotEqual(string.Empty, menu.Field("message").AsString());
        Assert.Equal(SessionMode.Starting, product.Mode);

        // A failed load did not poison the Host: the same title can still start the existing creation path.
        product.Update(ProductTestContext.Update(2, 1, ProductTestContext.Payload("{\"action\":\"session.new-game\"}")));
        Assert.Equal(SessionMode.Creating, product.Mode);
        Assert.False(ProjectedNode.Of(ui.Latest().Value).Field("menu").Field("visible").AsBoolean());
    }

    [Fact]
    public void Continue_restores_the_existing_save_without_restarting_the_product()
    {
        InMemoryPersistenceService persistence = new();
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(persistence, ProductTestContext.DoorWorld());
        using CrawlerProduct product = new(context, ProductTestContext.NoVariables, showLaunchTitle: true);
        product.Start();
        product.Update(ProductTestContext.Update(1, 1, ProductTestContext.Digital(ProductIdentity.CreationAcceptIntent)));
        product.Update(ProductTestContext.Update(2, 1, ProductTestContext.Digital(ProductIdentity.SaveIntent)));
        Assert.Equal("saved", ProjectedNode.Of(ui.Latest().Value).Field("save").Field("state").AsString());

        product.Update(ProductTestContext.Update(3, 1, ProductTestContext.Payload("{\"action\":\"session.return-title\"}")));
        product.Update(ProductTestContext.Update(4, 1, ProductTestContext.Payload("{\"action\":\"session.confirm-return-title\"}")));
        Assert.Equal(SessionMode.Starting, product.Mode);

        product.Update(ProductTestContext.Update(5, 1, ProductTestContext.Payload("{\"action\":\"session.continue\"}")));

        ProjectedNode restored = ProjectedNode.Of(ui.Latest().Value);
        Assert.Equal(SessionMode.Running, product.Mode);
        Assert.False(restored.Field("menu").Field("visible").AsBoolean());
        Assert.True(restored.Field("save").Field("resumed").AsBoolean());
    }

    [Fact]
    public void Cancelling_return_does_not_release_a_hold_that_preceded_the_confirmation()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(ProductTestContext.DoorWorld());
        using CrawlerProduct product = new(context, ProductTestContext.NoVariables, showLaunchTitle: true);
        product.Start();
        product.Update(ProductTestContext.Update(1, 1, ProductTestContext.Digital(ProductIdentity.CreationAcceptIntent)));
        product.Update(ProductTestContext.Update(2, 1, ProductTestContext.Digital(ProductIdentity.PauseToggleIntent)));
        Assert.Equal(SessionMode.Paused, product.Mode);

        product.Update(ProductTestContext.Update(3, 1, ProductTestContext.Payload("{\"action\":\"session.return-title\"}")));
        product.Update(ProductTestContext.Update(4, 1, ProductTestContext.Payload("{\"action\":\"session.cancel-return-title\"}")));

        Assert.Equal(SessionMode.Paused, product.Mode);
        ProjectedNode menu = ProjectedNode.Of(ui.Latest().Value).Field("menu");
        Assert.False(menu.Field("visible").AsBoolean());
        Assert.Equal("adventure", menu.Field("screen").AsString());
    }
}
