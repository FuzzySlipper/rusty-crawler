using System.Text.RegularExpressions;
using System.Xml.Linq;
using PartyRpg.Kit.Sessions;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Host.Tests;

/// <summary>
/// How creation reaches the product: the intents the host declares in code and in the project file, and a
/// creation choice travelling from admitted input through the product into this game's flow.
/// </summary>
/// <remarks>
/// The engine learns the admitted intent names from the project file and rejects a mapping whose intent the
/// product never declared, so a name that exists in code alone is a control that silently does nothing.
/// The architecture suite checks the product identity it knows about; this case checks the creation controls
/// in both directions, because they are the ones a screen depends on and the newest.
/// </remarks>
public sealed class CreationIntentTests
{
    [Fact]
    public void A_creation_choice_travels_from_admitted_input_into_the_flow_and_a_refusal_comes_back()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create();

        using CrawlerProduct product = new(context, ProductTestContext.NoVariables);
        product.Start();
        Assert.Equal(SessionMode.Creating, product.Mode);

        // The default party creation opens on is finished, so changing one member begins by reopening it:
        // the screen's own member button, on the product's payload contract.
        product.Update(ProductTestContext.Update(1, 1, ProductTestContext.Payload("""{"action":"creation.select-member","member":0}""")));
        Assert.Equal("portrait", ProjectedNode.Of(ui.Latest().Value).Field("creation").Field("roster").Item(0).Field("step").AsString());

        // A choice a player clicked: the portrait, which is what decides the character's race. The flow
        // draws its race, and the screen is told what the flow decided.
        product.Update(ProductTestContext.Update(2, 1, ProductTestContext.Payload("""{"action":"creation.select-portrait","portrait":"elf-woman"}""")));

        ProjectedNode creation = ProjectedNode.Of(ui.Latest().Value).Field("creation");
        Assert.Equal("Elf", creation.Field("roster").Item(0).Field("race").AsString());
        Assert.Equal("elf-woman", creation.Field("roster").Item(0).Field("portrait").AsString());
        Assert.True(creation.Field("portraits").Item(2).Field("selected").AsBoolean());
        Assert.Equal(string.Empty, creation.Field("refusalCode").AsString());

        // An illegal choice is refused by name and the session stays in creation: the rule the flow broke is
        // what the product publishes, not an exception inside an admitted update.
        product.Update(ProductTestContext.Update(3, 1, ProductTestContext.Payload("""{"action":"creation.select-portrait","portrait":"nobody"}""")));

        creation = ProjectedNode.Of(ui.Latest().Value).Field("creation");
        Assert.Equal("portrait-unknown", creation.Field("refusalCode").AsString());
        Assert.Contains("'nobody'", creation.Field("refusalMessage").AsString(), StringComparison.Ordinal);
        Assert.Equal(SessionMode.Creating, product.Mode);

        // Accepting a party whose member was reopened and left unfinished is refused with the member and the
        // step that are unfinished, so the screen can show what is left to do.
        product.Update(ProductTestContext.Update(4, 1, ProductTestContext.Digital(ProductIdentity.CreationAcceptIntent)));

        creation = ProjectedNode.Of(ui.Latest().Value).Field("creation");
        Assert.Equal("creation-incomplete", creation.Field("refusalCode").AsString());
        Assert.Contains("member 1", creation.Field("refusalMessage").AsString(), StringComparison.Ordinal);
        Assert.Equal("creating", ProjectedNode.Of(ui.Latest().Value).Field("session").Field("mode").AsString());
    }

    [Fact]
    public void Accepting_the_finished_party_leaves_creation_for_the_world_through_the_product()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create();

        using CrawlerProduct product = new(context, ProductTestContext.NoVariables);
        product.Start();

        // The declared accept control finishes a new game: the running product is playing the party it
        // created, and the panel's accepted list is that party's own members.
        product.Update(ProductTestContext.Update(1, 1, ProductTestContext.Digital(ProductIdentity.CreationAcceptIntent)));

        ProjectedNode value = ProjectedNode.Of(ui.Latest().Value);
        Assert.Equal(SessionMode.Running, product.Mode);
        Assert.False(value.Field("creation").Field("active").AsBoolean());
        Assert.True(value.Field("creation").Field("accepted").AsBoolean());
        Assert.Equal(4d, value.Field("creation").Field("party").Count());
        Assert.Equal("Roderick", value.Field("creation").Field("party").Item(0).Field("name").AsString());
        Assert.Equal("human-man", value.Field("creation").Field("party").Item(0).Field("portrait").AsString());
        Assert.True(value.Field("party").Field("present").AsBoolean());
        Assert.Equal(4d, value.Field("party").Field("members").AsNumber());

        // And the world steps once it is playing: the update after the accept is the first one measured.
        product.Update(ProductTestContext.Update(2, 60));
        Assert.Equal(60d, ProjectedNode.Of(ui.Latest().Value).Field("session").Field("admittedSteps").AsNumber());
    }
}
