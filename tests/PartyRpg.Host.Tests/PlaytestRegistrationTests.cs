using System.Text.Json;
using Rusty.Engine;
using Rusty.Engine.Debugging;
using Rusty.Engine.NativeProduct;
using Xunit;

namespace PartyRpg.Host.Tests;

/// <summary>
/// The product's registrations in the Engine's generated debug catalog: the playtest commands read the live
/// session, describe each declared control with its key, and turn the party through its own facing rule, and
/// the interaction inspection reads the focus the party actually holds.
/// </summary>
/// <remarks>
/// Every case drives the catalog the Engine's generator builds for this product — the same object the runtime
/// creates once for the product's life — rather than the modules alone, so a registration that went missing, or a
/// module type the generated catalog has no commands for, fails here rather than in a live check.
/// </remarks>
public sealed class PlaytestRegistrationTests
{
    [Fact]
    public void The_catalog_holds_the_playtest_and_interaction_commands_with_live_modules()
    {
        (ProductCreateContext context, RecordingUiService _) = ProductTestContext.Create(ProductTestContext.DoorWorld());
        using CrawlerProduct product = new(context, ProductTestContext.NoVariables);
        IDebugCommandCatalog catalog = GeneratedDebugCommandCatalogFactory.Create(product);

        string[] names = [.. catalog.Commands.Select(command => command.Name)];
        foreach (string command in new[]
                 {
                     "playtest.help", "playtest.observe", "playtest.action", "playtest.look",
                     "interaction.help", "interaction.inspect", "interaction.use",
                 })
        {
            Assert.Contains(command, names);
            Assert.NotEqual(DebugCommandStatus.ModuleUnavailable, catalog.Execute(command).Status);
        }

        // Every declared keyboard control is an action the help lists by its intent.
        using JsonDocument help = Json(catalog.Execute("playtest.help"));
        string[] actions = [.. help.RootElement.GetProperty("actions").EnumerateArray().Select(action => action.GetString()!)];
        Assert.Contains(ProductIdentity.MoveForwardIntent, actions);
        Assert.Contains(ProductIdentity.UseIntent, actions);
        Assert.Contains(ProductIdentity.SaveIntent, actions);
        Assert.Contains(ProductIdentity.AscendIntent, actions);
        Assert.Contains(ProductIdentity.DescendIntent, actions);
    }

    [Fact]
    public void Observe_reports_the_place_and_pose_and_a_movement_action_is_described_with_its_key()
    {
        (ProductCreateContext context, RecordingUiService _) = ProductTestContext.Create(ProductTestContext.DoorWorld());
        using CrawlerProduct product = new(context, ProductTestContext.NoVariables);
        IDebugCommandCatalog catalog = GeneratedDebugCommandCatalogFactory.Create(product);
        product.Start();

        // While the party is being made there is no place, and the movement keys would step nothing.
        using (JsonDocument creating = Json(catalog.Execute("playtest.observe")))
        {
            Assert.Equal("creating", creating.RootElement.GetProperty("mode").GetString());
            Assert.Equal(JsonValueKind.Null, creating.RootElement.GetProperty("place").ValueKind);
            // Which game this is: the bundle, what it selected, and the start the party is taking.
            JsonElement composition = creating.RootElement.GetProperty("composition");
            Assert.Equal(BuiltInBundles.Default, composition.GetProperty("bundle").GetString());
            Assert.Equal("creation", composition.GetProperty("partyStart").GetString());
        }

        using (JsonDocument forward = Json(catalog.Execute($"playtest.action {ProductIdentity.MoveForwardIntent}")))
        {
            Assert.Equal("KeyW", forward.RootElement.GetProperty("key").GetString());
            Assert.True(forward.RootElement.GetProperty("hold").GetBoolean());
            Assert.False(forward.RootElement.GetProperty("available").GetBoolean());
            Assert.Contains("being made", forward.RootElement.GetProperty("reason").GetString(), StringComparison.Ordinal);
        }

        // Creation is accepted with its own key, which the action describes as a press of the space bar.
        using (JsonDocument accept = Json(catalog.Execute($"playtest.action {ProductIdentity.CreationAcceptIntent}")))
        {
            Assert.Equal("Space", accept.RootElement.GetProperty("key").GetString());
            Assert.False(accept.RootElement.GetProperty("hold").GetBoolean());
            Assert.True(accept.RootElement.GetProperty("available").GetBoolean());
        }

        product.Update(ProductTestContext.Update(1, 1, ProductTestContext.Digital(ProductIdentity.CreationAcceptIntent)));
        product.Update(ProductTestContext.Update(2, 1));

        using (JsonDocument playing = Json(catalog.Execute("playtest.observe")))
        {
            JsonElement root = playing.RootElement;
            Assert.Equal("running", root.GetProperty("mode").GetString());
            Assert.Equal("52", root.GetProperty("place").GetProperty("id").GetString());
            Assert.Equal("The Dragon's Lair", root.GetProperty("place").GetProperty("name").GetString());
            Assert.Equal(0, root.GetProperty("pose").GetProperty("x").GetDouble());
            Assert.Equal(0, root.GetProperty("pose").GetProperty("yaw").GetDouble());
            Assert.True(root.GetProperty("steering").GetProperty("available").GetBoolean());
            Assert.Equal("A door", root.GetProperty("facing").GetProperty("label").GetString());
            Assert.Equal(0, root.GetProperty("combat").GetProperty("hostile").GetArrayLength());
            foreach (JsonElement member in root.GetProperty("combat").GetProperty("members").EnumerateArray())
            {
                Assert.Equal(root.GetProperty("pose").GetProperty("x").GetDouble(), member.GetProperty("pose").GetProperty("x").GetDouble());
                Assert.Equal(root.GetProperty("pose").GetProperty("y").GetDouble(), member.GetProperty("pose").GetProperty("y").GetDouble());
                Assert.Equal(root.GetProperty("pose").GetProperty("z").GetDouble(), member.GetProperty("pose").GetProperty("z").GetDouble());
            }
        }

        using (JsonDocument forward = Json(catalog.Execute($"playtest.action {ProductIdentity.MoveForwardIntent}")))
        {
            Assert.Equal("KeyW", forward.RootElement.GetProperty("key").GetString());
            Assert.True(forward.RootElement.GetProperty("available").GetBoolean());
            Assert.True(forward.RootElement.GetProperty("durationMs").GetDouble() > 0);
        }

        // Rising and sinking are movement controls the party's own flight decides: in a lair with no flight running
        // they are described with their keys and refused with the reason, while a walk is offered.
        foreach ((string intent, string key) in new[] { (ProductIdentity.AscendIntent, "ArrowUp"), (ProductIdentity.DescendIntent, "ArrowDown") })
        {
            using JsonDocument rise = Json(catalog.Execute($"playtest.action {intent}"));
            Assert.Equal(key, rise.RootElement.GetProperty("key").GetString());
            Assert.True(rise.RootElement.GetProperty("hold").GetBoolean());
            Assert.False(rise.RootElement.GetProperty("available").GetBoolean());
            Assert.Contains("may not fly", rise.RootElement.GetProperty("reason").GetString(), StringComparison.Ordinal);
        }

        // The use control is offered because the party faces the door, as the panel's use button is.
        using (JsonDocument use = Json(catalog.Execute($"playtest.action {ProductIdentity.UseIntent}")))
        {
            Assert.Equal("KeyG", use.RootElement.GetProperty("key").GetString());
            Assert.True(use.RootElement.GetProperty("available").GetBoolean());
        }

        using (JsonDocument unknown = Json(catalog.Execute("playtest.action party.fly")))
        {
            Assert.False(unknown.RootElement.GetProperty("available").GetBoolean());
        }
    }

    [Fact]
    public void A_look_turns_the_party_by_its_facing_rule_and_refuses_a_pitch()
    {
        (ProductCreateContext context, RecordingUiService _) = ProductTestContext.Create(ProductTestContext.DoorWorld());
        using CrawlerProduct product = new(context, ProductTestContext.NoVariables);
        IDebugCommandCatalog catalog = GeneratedDebugCommandCatalogFactory.Create(product);
        product.Start();

        // Nothing turns while the party is still being made.
        Assert.Equal(DebugCommandStatus.Failed, catalog.Execute("playtest.look 90 0").Status);

        product.Update(ProductTestContext.Update(1, 1, ProductTestContext.Digital(ProductIdentity.CreationAcceptIntent)));

        // A quarter turn to the right is a quarter of this world's turn, taken the way its turn-right control
        // turns: the facing grows to the left, so a right turn wraps below zero.
        Assert.True(catalog.Execute("playtest.look 90 0").Succeeded);
        using (JsonDocument turned = Json(catalog.Execute("playtest.observe")))
        {
            Assert.Equal(1536, turned.RootElement.GetProperty("pose").GetProperty("yaw").GetDouble(), 6);
        }

        DebugCommandResult pitched = catalog.Execute("playtest.look 0 10");
        Assert.Equal(DebugCommandStatus.Failed, pitched.Status);
        Assert.Contains("look-no-pitch", pitched.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Interaction_inspection_reads_the_focus_the_party_holds_across_a_restart_and_refuses_targeted_use()
    {
        (ProductCreateContext context, RecordingUiService _) = ProductTestContext.Create(ProductTestContext.DoorWorld());
        using CrawlerProduct product = new(context, ProductTestContext.NoVariables);
        IDebugCommandCatalog catalog = GeneratedDebugCommandCatalogFactory.Create(product);
        product.Start();

        // Before the party exists no world holds the selection, and the inspection says so.
        using (JsonDocument empty = Json(catalog.Execute("interaction.inspect")))
        {
            Assert.Equal("no-world", empty.RootElement.GetProperty("stamp").GetString());
            Assert.Equal(0, empty.RootElement.GetProperty("totalCandidates").GetInt32());
        }

        product.Update(ProductTestContext.Update(1, 1, ProductTestContext.Digital(ProductIdentity.CreationAcceptIntent)));
        product.Update(ProductTestContext.Update(2, 1));
        AssertFacesTheDoor(catalog);

        // Targeted use is off: a use reaches the world through the party's own use control alone.
        using (JsonDocument candidates = Json(catalog.Execute("interaction.inspect")))
        {
            JsonElement door = candidates.RootElement.GetProperty("candidates")[0];
            using JsonDocument used = Json(catalog.Execute(
                $"interaction.use {door.GetProperty("id").GetUInt64()} {door.GetProperty("revision").GetUInt64()}"));
            Assert.False(used.RootElement.GetProperty("performed").GetBoolean());
        }

        // A restart replaces the session and its world; the catalog the engine created once still reads the
        // live one, because every query resolves the session held when it is asked.
        product.Restart();
        product.Update(ProductTestContext.Update(3, 1, ProductTestContext.Digital(ProductIdentity.CreationAcceptIntent)));
        product.Update(ProductTestContext.Update(4, 1));
        AssertFacesTheDoor(catalog);
        using JsonDocument observed = Json(catalog.Execute("playtest.observe"));
        Assert.Equal("52", observed.RootElement.GetProperty("place").GetProperty("id").GetString());
    }

    private static void AssertFacesTheDoor(IDebugCommandCatalog catalog)
    {
        using JsonDocument inspected = Json(catalog.Execute("interaction.inspect"));
        JsonElement root = inspected.RootElement;
        Assert.False(root.GetProperty("targetedUseEnabled").GetBoolean());
        Assert.Equal(JsonValueKind.Object, root.GetProperty("selected").ValueKind);
        Assert.Equal("A door", root.GetProperty("candidates")[0].GetProperty("label").GetString());
        Assert.True(root.GetProperty("candidates")[0].GetProperty("selected").GetBoolean());
    }

    private static JsonDocument Json(DebugCommandResult result)
    {
        Assert.True(result.Succeeded, result.Message);
        return JsonDocument.Parse(result.Message);
    }
}
