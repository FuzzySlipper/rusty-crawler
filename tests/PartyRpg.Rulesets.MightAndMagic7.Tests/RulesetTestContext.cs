using System.Text;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

/// <summary>
/// Stages the content a case declares, and composes this game's session over it the way a host would, with the
/// names this suite declares.
/// </summary>
/// <remarks>
/// A case reaches the ruleset through its one public entry, <c>MightAndMagic7Ruleset.Instance.CreateSession</c>, and
/// its policies directly through the ruleset's friend declaration. What a host adds — its product identity, the keys
/// it binds, the bundle it ships — is the host suite's to prove, so nothing here names the host.
/// </remarks>
internal static class RulesetTestContext
{
    /// <summary>The directory a case stages its content under.</summary>
    internal const string ContentDirectory = "partyrpg";

    /// <summary>The bundle every staged world is selected by.</summary>
    internal const string BundleId = "partyrpg-default";

    /// <summary>A product context whose staged content holds exactly the given files.</summary>
    internal static (ProductCreateContext Context, RecordingUiService Ui) Create(params (string Path, string Text)[] files) =>
        Create(persistence: null, files);

    /// <summary>A product context whose engine supplies persistence, which a session's save store is composed over.</summary>
    internal static (ProductCreateContext Context, RecordingUiService Ui) Create(
        IPersistenceService? persistence,
        params (string Path, string Text)[] files) =>
        Create(persistence, spatial: null, contentService: null, files);

    /// <summary>A product context whose engine supplies the given services, which a session's movers are composed over.</summary>
    internal static (ProductCreateContext Context, RecordingUiService Ui) Create(
        IPersistenceService? persistence,
        ISpatialService? spatial,
        IContentService? contentService,
        params (string Path, string Text)[] files)
    {
        RecordingUiService ui = new();
        ProductContent content = new(
            files.Select(file => new ProductContentFile(Encoding.UTF8.GetBytes(file.Path), Encoding.UTF8.GetBytes(file.Text))).ToArray());
        ProductInputConfiguration input = new(
            new InputBinding(1, 1, 1),
            new InputContext(ReadOnlyMemory<byte>.Empty),
            ReadOnlyMemory<ProductInputDescriptor>.Empty,
            ReadOnlyMemory<ProductInputMapping>.Empty,
            InputCursorMode.PointerLock);
        ProductCreateContext context = new(
            new FakeEngineContext(ui, persistence, spatial, contentService),
            content,
            input,
            new Rusty.Engine.Debugging.DebugExecutionContext());
        return (context, ui);
    }

    /// <summary>The staged content of a context, as a content root the kit's loader reads.</summary>
    internal static IContentSource Content(ProductCreateContext context) => InMemoryContentSource.Of(context.Content);

    /// <summary>A staged bundle that selects the packs the case declares.</summary>
    internal static (string Path, string Text) Bundle(string bundleId, params string[] packIds) =>
        ($"{ContentDirectory}/bundles/{bundleId}/bundle.json",
            $$"""
            {
              "schemaVersion": 1,
              "bundleId": "{{bundleId}}",
              "ruleset": "mightandmagic7",
              "contentPacks": [{{string.Join(", ", packIds.Select(id => $"\"{id}\""))}}],
              "description": "test bundle"
            }
            """);

    /// <summary>An update admitting the given number of fixed steps, carrying the given admitted input.</summary>
    internal static ProductUpdate Update(ulong simulationStep, uint admittedSteps, params ProductInputEvent[] input) =>
        Admitted.Update(simulationStep, admittedSteps, input);

    /// <summary>An update admitting the given number of fixed steps of a stated length.</summary>
    internal static ProductUpdate Update(ulong simulationStep, uint admittedSteps, double stepSeconds, params ProductInputEvent[] input) =>
        Admitted.Update(simulationStep, admittedSteps, stepSeconds, input);

    /// <summary>One digital event on an intent, in the shape the engine admits it.</summary>
    internal static ProductInputEvent Digital(string intent, InputEdge edge = InputEdge.Pressed) => Admitted.Digital(intent, edge);

    /// <summary>One semantic action on this suite's payload contract, as the DOM companion sends it.</summary>
    internal static ProductInputEvent Payload(string json) => Admitted.Payload(Declared.UiActionContract, json);

    /// <summary>
    /// One semantic action choosing a topic in a conversation, as the companion's own button sends it.
    /// </summary>
    /// <remarks>
    /// A party reaches a counter through the conversation rather than through a second way in: the use opens
    /// the conversation with whoever keeps it, and this is the choice that hands the party over to the
    /// counter's own mechanism. That is why every walk-in a suite performs is two updates rather than one.
    /// </remarks>
    /// <param name="topic">The topic's identity.</param>
    internal static ProductInputEvent ChooseTopic(string topic) =>
        Payload($$"""{ "action": "conversation.topic", "target": "{{topic}}" }""");

    /// <summary>
    /// A pack declaring the class and skill definitions this game's creation is checked against, which any
    /// bundle a player creates a party in must carry.
    /// </summary>
    /// <remarks>
    /// The classes and their skills come from the ruleset's own creation tables, so this fixture cannot drift
    /// from what creation offers.
    /// </remarks>
    internal static (string Path, string Text)[] CreationTables()
    {
        List<string> classes = [];
        SortedSet<string> skills = new(StringComparer.Ordinal);
        foreach (CreationClass characterClass in MightAndMagic7CreationTables.Classes)
        {
            classes.Add($$"""{ "id": "{{characterClass.Id.Value}}", "baseClass": "{{characterClass.Id.Value}}" }""");
            foreach (SkillId skill in characterClass.FixedSkills.Concat(characterClass.ChoosableSkills)) skills.Add(skill.Value);
        }

        return
        [
            ($"{ContentDirectory}/content-packs/creation-tables/pack.json",
                TestPacks.Manifest("creation-tables", ("classes", "class"), ("skills", "skill"))),
            ($"{ContentDirectory}/content-packs/creation-tables/classes.json",
                TestPacks.Document("classes", "class", [.. classes])),
            ($"{ContentDirectory}/content-packs/creation-tables/skills.json",
                TestPacks.Document("skills", "skill", [.. skills.Select(skill => $$"""{ "id": "{{skill}}" }""")])),
        ];
    }

    /// <summary>The context this game composes a session from, over the content a case staged.</summary>
    /// <param name="context">The product context the content was staged in.</param>
    /// <param name="ui">The UI service the session publishes to.</param>
    /// <param name="creation">Whether the session creates its party rather than playing the one its scenario fixes.</param>
    /// <param name="engine">Whether the session runs inside an engine.</param>
    /// <param name="combat">Whether the session reads an order to attack; without it the fight still reads the world.</param>
    internal static RulesetSessionContext RulesetContext(
        ProductCreateContext context,
        RecordingUiService ui,
        bool creation = false,
        bool engine = true,
        bool combat = false)
    {
        ContentBootstrapResult bootstrap = ContentBootstrap.Load(Content(context), ContentLayout.Under(ContentDirectory), BundleId);
        Assert.True(bootstrap.IsValid, string.Join("; ", bootstrap.Issues.Select(issue => issue.ToString())));
        Assert.NotNull(bootstrap.Selection);

        EngineUiProjectionChannel channel = new(ui, new UiStreamRequest(Declared.UiStream, Declared.UiContract));
        return new RulesetSessionContext(
            channel,
            new BundleSelection(bootstrap.Selection.Bundle.BundleId, bootstrap.Selection.Packs.Count),
            PromotionTestContent.With(bootstrap.Catalog),
            Engine: engine ? context.Engine : null,
            Creation: creation
                ? new CreationIntentNames(Declared.CreationAdvanceIntent, Declared.CreationAcceptIntent, Declared.UiActionContract)
                : null,
            Combat: combat
                ? new CombatIntentNames(
                    Declared.AttackIntent,
                    Declared.UiActionContract,
                    new TurnIntentNames(
                        Declared.TurnBasedToggleIntent,
                        Declared.TurnSkipIntent,
                        Declared.TurnWaitIntent,
                        Declared.UiActionContract))
                : null);
    }
}
