using System.Text.RegularExpressions;
using System.Xml.Linq;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Host.Tests;

/// <summary>
/// How a use reaches the product: the declaration the engine checks in both directions, the companion's own
/// copy of the action name, and the whole trip from a pressed key to what the panel reports.
/// </summary>
/// <remarks>
/// The engine learns the admitted intent names from the project file and rejects a mapping whose intent the
/// product never declared, so a name that exists in code alone is a control that silently does nothing. This
/// case checks the use controls the way the save and creation controls are checked, and then presses the key
/// at a door in a staged world so the declaration is proved by a use rather than by a string.
/// </remarks>
public sealed class UseIntentTests
{
    [Fact]
    public void The_use_key_opens_a_door_in_a_staged_world_and_the_panel_reports_it()
    {
        // A world with one interior whose door stands in front of the scenario's starting point, and the
        // creation tables the product needs to make its party before the world is composed over it.
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(ProductTestContext.DoorWorld());

        using CrawlerProduct product = new(context, ProductTestContext.NoVariables);
        product.Start();

        // The party is created first: the world this session plays is composed over the party when creation
        // is accepted, which is also when the interaction mechanism exists.
        product.Update(ProductTestContext.Update(1, 1, ProductTestContext.Digital(ProductIdentity.CreationAcceptIntent)));

        ProjectedNode world = ProjectedNode.Of(ui.Latest().Value).Field("world");
        Assert.Equal("52", world.Field("place").AsString());
        ProjectedNode before = ProjectedNode.Of(ui.Latest().Value).Field("interaction");
        Assert.True(before.Field("available").AsBoolean());
        Assert.Equal("none", before.Field("outcome").AsString());

        // The world's own admitted update faces the door the party stands in front of and reports the door's
        // own state, and the press in the next update is what uses it.
        product.Update(ProductTestContext.Update(2, 1));
        Assert.Equal("A door", ProjectedNode.Of(ui.Latest().Value).Field("interaction").Field("label").AsString());

        product.Update(ProductTestContext.Update(3, 1, ProductTestContext.Digital(ProductIdentity.UseIntent)));
        ProjectedNode applied = ProjectedNode.Of(ui.Latest().Value).Field("interaction");
        Assert.Equal("applied", applied.Field("outcome").AsString());
        Assert.Equal("open", applied.Field("state").AsString());
        Assert.Contains("swings open", applied.Field("message").AsString(), StringComparison.Ordinal);
        // The passage this build cannot deliver is stated beside the success rather than left implied.
        Assert.Contains("cannot be walked through yet", applied.Field("residue").AsString(), StringComparison.Ordinal);

        // The same door used again is refused with its own reason rather than doing nothing quietly, and the
        // refusal is part of the projection the press arrived in.
        product.Update(ProductTestContext.Update(4, 1, ProductTestContext.Digital(ProductIdentity.UseIntent)));
        ProjectedNode refused = ProjectedNode.Of(ui.Latest().Value).Field("interaction");
        Assert.Equal("refused", refused.Field("outcome").AsString());
        Assert.Equal("door-already-open", refused.Field("code").AsString());
    }

}
