using System.Text.RegularExpressions;
using System.Xml.Linq;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Rulesets.MightAndMagic7;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Host.Tests;

/// <summary>
/// How a save reaches the product, and how a run is told to resume one: the two declarations the engine
/// checks, the companion's own copy of the action name, and the start switch that decides a new game from a
/// continued one.
/// </summary>
/// <remarks>
/// The engine learns the admitted intent names from the project file and rejects a mapping whose intent the
/// product never declared, so a name that exists in code alone is a control that silently does nothing.
/// This case checks the save controls in both directions, for the same reason the creation controls are
/// checked: they are the ones a player presses and the newest.
/// </remarks>
public sealed class SaveIntentTests
{
    [Fact]
    public void A_run_told_to_resume_with_nothing_saved_fails_by_name()
    {
        InMemoryPersistenceService persistence = new();
        (ProductCreateContext context, RecordingUiService _) = ProductTestContext.Create(
            persistence,
            ProductTestContext.CreationTables());

        // The switch is the operator's, so a slot that holds nothing stops the run with the loss named
        // rather than starting a new expedition in place of the one that was asked for. The switch arrives the
        // way the product's own entry reads it, through the declared variable.
        SessionSaveException refused = Assert.Throws<SessionSaveException>(() =>
        {
            using CrawlerProduct product = new(context, name => name == ProductIdentity.StartVariable ? "resume" : null);
        });

        SaveProblem empty = Assert.Single(refused.Problems);
        Assert.Equal(SaveCodes.SaveSlotEmpty, empty.Code);
        Assert.Equal(MightAndMagic7Persistence.SaveSlot, empty.Subject);
        Assert.Null(persistence.Payload(MightAndMagic7Persistence.StoreScope, MightAndMagic7Persistence.SaveSlot));
    }

    [Fact]
    public void A_start_switch_that_names_neither_start_is_refused_with_the_value_it_read()
    {
        // The product's two words mean what they say, and an empty or absent switch is a new game.
        Assert.Equal(SessionStart.Fresh, ProductStart.Parse(null));
        Assert.Equal(SessionStart.Fresh, ProductStart.Parse(""));
        Assert.Equal(SessionStart.Fresh, ProductStart.Parse("fresh"));
        Assert.Equal(SessionStart.Resume, ProductStart.Parse("resume"));
        Assert.Equal(SessionStart.Resume, ProductStart.Parse("  Resume  "));

        // Anything else stops the run with the value it read, rather than being rounded to one of the two:
        // an operator who typed a switch wrong must not be handed a new game when they asked to continue.
        InvalidOperationException refused = Assert.Throws<InvalidOperationException>(() => ProductStart.Parse("continue"));
        Assert.Contains("'continue'", refused.Message, StringComparison.Ordinal);
        Assert.Contains(ProductIdentity.StartVariable, refused.Message, StringComparison.Ordinal);

        // And the switch the product reads is the declared variable rather than an implicit one: it asks for
        // that one name and nothing else, and an unset switch is a new game.
        List<string> asked = [];
        Assert.Equal(SessionStart.Resume, ProductStart.From(name => { asked.Add(name); return "resume"; }));
        Assert.Equal([ProductIdentity.StartVariable], asked);
        Assert.Equal(SessionStart.Fresh, ProductStart.From(ProductTestContext.NoVariables));
    }
}
