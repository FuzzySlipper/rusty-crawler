using PartyRpg.Kit.Content;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Rulesets.MightAndMagic7;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Host.Tests;

/// <summary>This game's session composes over the packs written from the operator's own installation.</summary>
/// <remarks>
/// The importer's own suite proves the same over its synthetic installation; this proves it over every row the
/// release actually ships, which is where a table's shape the synthetic data does not carry would surface as a
/// content defect.
/// </remarks>
public sealed class ImportedSessionTests
{
    [ImportedFact("monsters.json")]
    public void The_operators_imported_packs_compose_this_games_session_with_no_content_defect()
    {
        ContentCatalog catalog = ImportedContent.Load();
        Assert.True(catalog.IsValid, string.Join("; ", catalog.Issues.Select(issue => issue.ToString())));

        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create();
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(new RulesetSessionContext(
            new EngineUiProjectionChannel(ui, new UiStreamRequest(ProductIdentity.UiStream, ProductIdentity.UiContract)),
            new BundleSelection("imported", catalog.Packs.Count),
            catalog.Selected(catalog.Packs),
            Engine: context.Engine));
        session.Start();
        session.Update(ProductTestContext.Update(1, 1));
        Assert.NotEmpty(ui.Projections);
    }
}
