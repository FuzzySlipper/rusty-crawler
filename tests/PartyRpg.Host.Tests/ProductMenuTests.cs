using System.Text;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Sessions;
using PartyRpg.Rulesets.MightAndMagic7;
using Rusty.Engine;
using Rusty.Engine.Persistence;
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
    public void A_visible_menu_owns_creation_input_through_the_update_that_opens_or_closes_it()
    {
        InMemoryPersistenceService persistence = new();
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(persistence, ProductTestContext.DoorWorld());
        using CrawlerProduct product = new(context, ProductTestContext.NoVariables, showLaunchTitle: true);
        product.Start();

        ProjectedNode before = ProjectedNode.Of(ui.Latest().Value).Field("creation");
        string beforeStep = before.Field("step").AsString();
        string beforeRefusal = before.Field("refusalCode").AsString();
        string beforeMemberStep = before.Field("roster").Item(0).Field("step").AsString();

        // Enter and the legacy accept key arrive while the title is visible. Neither may mutate the creation
        // flow or implicitly dismiss the title; New Game remains the visible entry control.
        product.Update(ProductTestContext.Update(
            1,
            1,
            ProductTestContext.Digital(ProductIdentity.CreationAdvanceIntent),
            ProductTestContext.Digital(ProductIdentity.CreationAcceptIntent),
            ProductTestContext.Digital(ProductIdentity.SaveIntent)));

        ProjectedNode title = ProjectedNode.Of(ui.Latest().Value);
        Assert.Equal("title", title.Field("menu").Field("screen").AsString());
        Assert.Equal(SessionMode.Creating, product.Mode);
        AssertCreationUnchanged(title, beforeStep, beforeRefusal, beforeMemberStep);
        Assert.Null(persistence.Payload(MightAndMagic7Persistence.StoreScope, MightAndMagic7Persistence.SaveSlot));

        // The visible New Game action reveals that same creation owner, but its activating update still belongs
        // to the menu and cannot also advance the newly revealed flow.
        product.Update(ProductTestContext.Update(
            2,
            0,
            ProductTestContext.Payload("{\"action\":\"session.new-game\"}"),
            ProductTestContext.Digital(ProductIdentity.CreationAdvanceIntent),
            ProductTestContext.Digital(ProductIdentity.CreationAcceptIntent)));
        ProjectedNode adventure = ProjectedNode.Of(ui.Latest().Value);
        Assert.Equal("adventure", adventure.Field("menu").Field("screen").AsString());
        AssertCreationUnchanged(adventure, beforeStep, beforeRefusal, beforeMemberStep);

        // Opening and cancelling the return confirmation while creation is still underneath it both own their
        // whole admitted updates, including the Enter shortcut carried alongside the menu action.
        product.Update(ProductTestContext.Update(
            3,
            1,
            ProductTestContext.Digital(ProductIdentity.CreationAdvanceIntent),
            ProductTestContext.Payload("{\"action\":\"session.return-title\"}")));
        ProjectedNode confirmation = ProjectedNode.Of(ui.Latest().Value);
        Assert.Equal("confirm-return", confirmation.Field("menu").Field("screen").AsString());
        Assert.Equal(SessionMode.Creating, product.Mode);
        AssertCreationUnchanged(confirmation, beforeStep, beforeRefusal, beforeMemberStep);

        product.Update(ProductTestContext.Update(
            4,
            1,
            ProductTestContext.Digital(ProductIdentity.CreationAdvanceIntent),
            ProductTestContext.Digital(ProductIdentity.CreationAcceptIntent)));
        confirmation = ProjectedNode.Of(ui.Latest().Value);
        Assert.Equal("confirm-return", confirmation.Field("menu").Field("screen").AsString());
        Assert.Equal(SessionMode.Creating, product.Mode);
        AssertCreationUnchanged(confirmation, beforeStep, beforeRefusal, beforeMemberStep);

        product.Update(ProductTestContext.Update(
            5,
            1,
            ProductTestContext.Digital(ProductIdentity.CreationAdvanceIntent),
            ProductTestContext.Payload("{\"action\":\"session.cancel-return-title\"}")));
        adventure = ProjectedNode.Of(ui.Latest().Value);
        Assert.Equal("adventure", adventure.Field("menu").Field("screen").AsString());
        Assert.Equal(SessionMode.Creating, product.Mode);
        AssertCreationUnchanged(adventure, beforeStep, beforeRefusal, beforeMemberStep);

        // The temporary gate is cleared after the admitted update: once the menu is gone, the same creation
        // owner can receive its ordinary visible creation action.
        product.Update(ProductTestContext.Update(6, 1, ProductTestContext.Payload("{\"action\":\"creation.select-member\",\"member\":0}")));
        Assert.Equal("portrait", ProjectedNode.Of(ui.Latest().Value).Field("creation").Field("roster").Item(0).Field("step").AsString());

        static void AssertCreationUnchanged(ProjectedNode value, string step, string refusal, string memberStep)
        {
            ProjectedNode creation = value.Field("creation");
            Assert.Equal(step, creation.Field("step").AsString());
            Assert.Equal(refusal, creation.Field("refusalCode").AsString());
            Assert.Equal(memberStep, creation.Field("roster").Item(0).Field("step").AsString());
        }
    }

    [Fact]
    public void Return_to_title_pauses_for_confirmation_then_rebuilds_a_fresh_session()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(ProductTestContext.DoorWorld());
        using CrawlerProduct product = new(context, ProductTestContext.NoVariables, showLaunchTitle: true);
        product.Start();

        // The visible title control reveals the same creation flow, then the creation key accepts the default party.
        StartNewGame(product);
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
    public void Continue_from_a_no_content_title_keeps_setup_usable_instead_of_stopping_the_session()
    {
        // This is the shipped checkout before an operator has generated packs: the title can still explain setup
        // and expose Continue, but a refused resume must not dispose the only session behind that card.
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create();
        using CrawlerProduct product = new(context, ProductTestContext.NoVariables, showLaunchTitle: true);
        product.Start();

        product.Update(ProductTestContext.Update(1, 1, ProductTestContext.Payload("{\"action\":\"session.continue\"}")));

        ProjectedNode menu = ProjectedNode.Of(ui.Latest().Value).Field("menu");
        Assert.True(menu.Field("visible").AsBoolean());
        Assert.Equal("title", menu.Field("screen").AsString());
        Assert.Equal("failed", menu.Field("state").AsString());
        Assert.NotEqual(SessionMode.Stopped, product.Mode);

        // The same no-content title can still take its ordinary New Game path after the refusal.
        product.Update(ProductTestContext.Update(2, 1, ProductTestContext.Payload("{\"action\":\"session.new-game\"}")));
        Assert.NotEqual(SessionMode.Stopped, product.Mode);
        Assert.False(ProjectedNode.Of(ui.Latest().Value).Field("menu").Field("visible").AsBoolean());
    }

    [Fact]
    public void Save_load_without_a_persistence_root_advertises_an_unavailable_empty_slot()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(
            new NoPersistenceRoot(),
            ProductTestContext.DoorWorld());
        using CrawlerProduct product = new(context, ProductTestContext.NoVariables, showLaunchTitle: true);
        product.Start();
        StartNewGame(product);
        product.Update(ProductTestContext.Update(1, 1, ProductTestContext.Digital(ProductIdentity.CreationAcceptIntent)));
        product.Update(ProductTestContext.Update(2, 1, ProductTestContext.Payload("{\"action\":\"session.open-save-load\"}")));

        ProjectedNode save = ProjectedNode.Of(ui.Latest().Value).Field("menu").Field("save");
        Assert.False(save.Field("available").AsBoolean());
        Assert.False(save.Field("present").AsBoolean());
        Assert.Equal(SaveCodes.SaveStoreUnopened, save.Field("code").AsString());
    }

    [Fact]
    public void Save_load_with_corrupt_existing_bytes_keeps_the_slot_present_and_names_the_read_failure()
    {
        InMemoryPersistenceService persistence = new();
        persistence.Seed(
            MightAndMagic7Persistence.StoreScope,
            MightAndMagic7Persistence.SaveSlot,
            Encoding.UTF8.GetBytes("{\"party\": {\"nextMemberValue\": 1"));
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(
            persistence,
            ProductTestContext.DoorWorld());
        using CrawlerProduct product = new(context, ProductTestContext.NoVariables, showLaunchTitle: true);
        product.Start();
        StartNewGame(product);
        product.Update(ProductTestContext.Update(1, 1, ProductTestContext.Digital(ProductIdentity.CreationAcceptIntent)));
        product.Update(ProductTestContext.Update(2, 1, ProductTestContext.Payload("{\"action\":\"session.open-save-load\"}")));

        ProjectedNode save = ProjectedNode.Of(ui.Latest().Value).Field("menu").Field("save");
        Assert.True(save.Field("available").AsBoolean());
        Assert.True(save.Field("present").AsBoolean());
        Assert.Equal(SaveCodes.SaveUnreadable, save.Field("code").AsString());
    }

    [Fact]
    public void Save_load_screen_shows_the_saved_expedition_and_requires_overwrite_confirmation()
    {
        InMemoryPersistenceService persistence = new();
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(persistence, ProductTestContext.DoorWorld());
        using CrawlerProduct product = new(context, ProductTestContext.NoVariables, showLaunchTitle: true);
        product.Start();
        StartNewGame(product);
        product.Update(ProductTestContext.Update(1, 1, ProductTestContext.Digital(ProductIdentity.CreationAcceptIntent)));

        product.Update(ProductTestContext.Update(2, 1, ProductTestContext.Payload("{\"action\":\"session.open-save-load\"}")));
        ProjectedNode empty = ProjectedNode.Of(ui.Latest().Value).Field("menu");
        Assert.Equal("save-load", empty.Field("screen").AsString());
        Assert.True(empty.Field("save").Field("available").AsBoolean());
        Assert.False(empty.Field("save").Field("present").AsBoolean());
        Assert.Equal(SessionMode.Paused, product.Mode);

        product.Update(ProductTestContext.Update(3, 1, ProductTestContext.Payload("{\"action\":\"session.menu-save\"}")));
        ProjectedNode saved = ProjectedNode.Of(ui.Latest().Value).Field("menu");
        Assert.Equal("save-load", saved.Field("screen").AsString());
        Assert.True(saved.Field("save").Field("present").AsBoolean());
        Assert.Equal("saved", saved.Field("save").Field("state").AsString());
        Assert.False(saved.Field("hasUnsaved").AsBoolean());
        Assert.Equal("The Dragon's Lair", saved.Field("save").Field("place").AsString());
        Assert.NotEqual(string.Empty, saved.Field("save").Field("calendar").AsString());
        Assert.Contains("Saved the expedition", saved.Field("save").Field("message").AsString(), StringComparison.Ordinal);

        // Ordinary semantic and F requests arriving together still produce one deliberate overwrite decision.
        product.Update(ProductTestContext.Update(
            4,
            1,
            ProductTestContext.Digital(ProductIdentity.SaveIntent),
            ProductTestContext.Payload("{\"action\":\"session.save\"}")));
        Assert.Equal("confirm-overwrite", ProjectedNode.Of(ui.Latest().Value).Field("menu").Field("screen").AsString());
        product.Update(ProductTestContext.Update(5, 1, ProductTestContext.Payload("{\"action\":\"session.cancel-overwrite\"}")));
        Assert.Equal("save-load", ProjectedNode.Of(ui.Latest().Value).Field("menu").Field("screen").AsString());

        // The declared F intent is subject to the same decision while the explicit screen is open.
        product.Update(ProductTestContext.Update(6, 1, ProductTestContext.Digital(ProductIdentity.SaveIntent)));
        Assert.Equal("confirm-overwrite", ProjectedNode.Of(ui.Latest().Value).Field("menu").Field("screen").AsString());
        product.Update(ProductTestContext.Update(7, 1, ProductTestContext.Payload("{\"action\":\"session.cancel-overwrite\"}")));
        Assert.Equal("save-load", ProjectedNode.Of(ui.Latest().Value).Field("menu").Field("screen").AsString());

        // A second menu save reaches the same canonical boundary only after the player answers the overwrite prompt.
        product.Update(ProductTestContext.Update(8, 1, ProductTestContext.Payload("{\"action\":\"session.menu-save\"}")));
        Assert.Equal("confirm-overwrite", ProjectedNode.Of(ui.Latest().Value).Field("menu").Field("screen").AsString());
        product.Update(ProductTestContext.Update(9, 1, ProductTestContext.Payload("{\"action\":\"session.cancel-overwrite\"}")));
        Assert.Equal("save-load", ProjectedNode.Of(ui.Latest().Value).Field("menu").Field("screen").AsString());

        product.Update(ProductTestContext.Update(10, 1, ProductTestContext.Payload("{\"action\":\"session.menu-save\"}")));
        Assert.Equal("confirm-overwrite", ProjectedNode.Of(ui.Latest().Value).Field("menu").Field("screen").AsString());
        product.Update(ProductTestContext.Update(11, 1, ProductTestContext.Payload("{\"action\":\"session.menu-save\"}")));
        ProjectedNode overwritten = ProjectedNode.Of(ui.Latest().Value).Field("menu");
        Assert.Equal("save-load", overwritten.Field("screen").AsString());
        Assert.Equal("saved", overwritten.Field("save").Field("state").AsString());

        product.Update(ProductTestContext.Update(12, 1, ProductTestContext.Payload("{\"action\":\"session.close-save-load\"}")));
        Assert.Equal(SessionMode.Running, product.Mode);
        Assert.False(ProjectedNode.Of(ui.Latest().Value).Field("menu").Field("visible").AsBoolean());
    }

    [Fact]
    public void Leaving_the_save_load_screen_takes_precedence_over_a_same_update_save_request()
    {
        InMemoryPersistenceService persistence = new();
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(persistence, ProductTestContext.DoorWorld());
        using CrawlerProduct product = new(context, ProductTestContext.NoVariables, showLaunchTitle: true);
        product.Start();
        StartNewGame(product);
        product.Update(ProductTestContext.Update(1, 1, ProductTestContext.Digital(ProductIdentity.CreationAcceptIntent)));
        product.Update(ProductTestContext.Update(2, 1, ProductTestContext.Payload("{\"action\":\"session.open-save-load\"}")));
        product.Update(ProductTestContext.Update(3, 1, ProductTestContext.Payload("{\"action\":\"session.menu-save\"}")));

        byte[] before = persistence.Payload(MightAndMagic7Persistence.StoreScope, MightAndMagic7Persistence.SaveSlot)!;
        product.Update(ProductTestContext.Update(
            4,
            1,
            ProductTestContext.Digital(ProductIdentity.SaveIntent),
            ProductTestContext.Payload("{\"action\":\"session.return-title\"}")));

        ProjectedNode menu = ProjectedNode.Of(ui.Latest().Value).Field("menu");
        Assert.Equal("confirm-return", menu.Field("screen").AsString());
        Assert.Equal(before, persistence.Payload(MightAndMagic7Persistence.StoreScope, MightAndMagic7Persistence.SaveSlot));
    }

    [Fact]
    public void Closing_the_save_load_screen_cannot_save_from_the_same_update()
    {
        InMemoryPersistenceService persistence = new();
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(persistence, ProductTestContext.DoorWorld());
        using CrawlerProduct product = new(context, ProductTestContext.NoVariables, showLaunchTitle: true);
        product.Start();
        StartNewGame(product);
        product.Update(ProductTestContext.Update(1, 1, ProductTestContext.Digital(ProductIdentity.CreationAcceptIntent)));
        product.Update(ProductTestContext.Update(2, 1, ProductTestContext.Payload("{\"action\":\"session.open-save-load\"}")));
        product.Update(ProductTestContext.Update(3, 1, ProductTestContext.Payload("{\"action\":\"session.menu-save\"}")));

        byte[] before = persistence.Payload(MightAndMagic7Persistence.StoreScope, MightAndMagic7Persistence.SaveSlot)!;
        product.Update(ProductTestContext.Update(
            4,
            1,
            ProductTestContext.Digital(ProductIdentity.SaveIntent),
            ProductTestContext.Payload("{\"action\":\"session.close-save-load\"}")));

        Assert.Equal(before, persistence.Payload(MightAndMagic7Persistence.StoreScope, MightAndMagic7Persistence.SaveSlot));
        Assert.Equal("adventure", ProjectedNode.Of(ui.Latest().Value).Field("menu").Field("screen").AsString());
    }

    [Fact]
    public void Continue_restores_the_existing_save_without_restarting_the_product()
    {
        InMemoryPersistenceService persistence = new();
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(persistence, ProductTestContext.DoorWorld());
        using CrawlerProduct product = new(context, ProductTestContext.NoVariables, showLaunchTitle: true);
        product.Start();
        StartNewGame(product);
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
        StartNewGame(product);
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

    /// <summary>Activates the visible title control without admitting a creation step in the same update.</summary>
    private static void StartNewGame(CrawlerProduct product) =>
        product.Update(ProductTestContext.Update(1, 0, ProductTestContext.Payload("{\"action\":\"session.new-game\"}")));

    /// <summary>An engine persistence service whose host selected no persistence root.</summary>
    private sealed class NoPersistenceRoot : IPersistenceService
    {
        public PersistenceStore OpenStore(PersistenceOpenRequest request) =>
            throw new EngineCallException("Persistence", "OpenStore", 0);

        public PersistenceSaveReceipt Save(PersistenceSaveRequest request) => throw new NotSupportedException();

        public PersistenceDeleteReceipt Delete(PersistenceDeleteRequest request) => throw new NotSupportedException();

        public PersistenceBlob Load(PersistenceLoadRequest request) => throw new NotSupportedException();

        public PersistenceBlobInfo DescribeBlob(PersistenceBlob blob) => throw new NotSupportedException();

        public void CopyBlob(PersistenceCopyBlobRequest request) => throw new NotSupportedException();

        public ReadOnlyMemory<byte> ReadBlobBytes(PersistenceBlob blob) => throw new NotSupportedException();
    }
}
