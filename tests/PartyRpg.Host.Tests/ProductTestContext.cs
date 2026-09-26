using System.Text;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Rulesets.MightAndMagic7;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Host.Tests;

/// <summary>Builds the engine values a product is created with, from content the test declares.</summary>
internal static class ProductTestContext
{
    internal const string ContentDirectory = "partyrpg";

    /// <summary>A product context whose staged content holds exactly the given files.</summary>
    internal static (ProductCreateContext Context, RecordingUiService Ui) Create(params (string Path, string Text)[] files) =>
        Create(persistence: null, files);

    /// <summary>
    /// A product context whose engine supplies persistence, which is what a session's save store is
    /// composed over.
    /// </summary>
    internal static (ProductCreateContext Context, RecordingUiService Ui) Create(
        IPersistenceService? persistence,
        params (string Path, string Text)[] files)
    {
        RecordingUiService ui = new();
        ProductContentFile[] staged =
        [
            .. files.Select(file => new ProductContentFile(
                Encoding.UTF8.GetBytes(file.Path),
                Encoding.UTF8.GetBytes(file.Text))),
        ];
        ProductContent content = new(staged);
        ProductInputConfiguration input = new(
            new InputBinding(1, 1, 1),
            new InputContext(ReadOnlyMemory<byte>.Empty),
            ReadOnlyMemory<ProductInputDescriptor>.Empty,
            ReadOnlyMemory<ProductInputMapping>.Empty,
            InputCursorMode.PointerLock);
        ProductCreateContext context = new(new FakeEngineContext(ui, persistence), content, input, new Rusty.Engine.Debugging.DebugExecutionContext());
        return (context, ui);
    }

    /// <summary>A staged bundle that selects the packs the test declares.</summary>
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

    /// <summary>A staged pack of one document with one entry.</summary>
    internal static (string Path, string Text)[] Pack(string packId, string entryId) =>
    [
        ($"{ContentDirectory}/content-packs/{packId}/pack.json",
            $$"""
            {
              "schemaVersion": 1,
              "packId": "{{packId}}",
              "kind": "definitions",
              "provenance": { "description": "authored for a test" },
              "documents": [ { "path": "entries.json", "documentId": "{{packId}}-entries", "definitionKind": "thing" } ]
            }
            """),
        ($"{ContentDirectory}/content-packs/{packId}/entries.json",
            $$"""{ "documentId": "{{packId}}-entries", "definitionKind": "thing", "entries": [ { "id": "{{entryId}}" } ] }"""),
    ];

    /// <summary>An update admitting the given number of fixed steps.</summary>
    internal static ProductUpdate Update(ulong simulationStep, uint admittedSteps, double stepSeconds = 1.0 / 60.0) =>
        Update(simulationStep, admittedSteps, stepSeconds, ReadOnlySpan<ProductInputEvent>.Empty);

    /// <summary>An update admitting the given number of fixed steps, carrying the given admitted input.</summary>
    internal static ProductUpdate Update(
        ulong simulationStep,
        uint admittedSteps,
        params ProductInputEvent[] input) => Update(simulationStep, admittedSteps, 1.0 / 60.0, input);

    /// <summary>An update admitting the given number of fixed steps, carrying the given admitted input.</summary>
    internal static ProductUpdate Update(
        ulong simulationStep,
        uint admittedSteps,
        double stepSeconds,
        ReadOnlySpan<ProductInputEvent> input)
    {
        ProductUpdateFacts facts = new(
            ProductUpdateMode.Realtime,
            ProductLifecycleState.Running,
            Generation: 1,
            ControlRevision: 1,
            ObservedHostTimeNanoseconds: 0,
            SimulationStep: simulationStep,
            FixedStepHz: 60,
            AdmittedStepCount: admittedSteps,
            DroppedStepCount: 0,
            FixedDeltaSeconds: stepSeconds);
        return new ProductUpdate(facts, input);
    }

    /// <summary>One digital event on a product intent, in the shape the engine admits it.</summary>
    internal static ProductInputEvent Digital(string intent, InputEdge edge = InputEdge.Pressed) => new(
        InputEventKind.MappedDigital, edge, default, default, default, default, default, default, default, default,
        InputValueKind.Digital, InputPhase.Pressed, InputProvenance.Physical, default, default, default, 0f, 0f,
        ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, Encoding.UTF8.GetBytes(intent),
        ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty);

    /// <summary>One semantic action on the product's declared payload contract, as the DOM companion sends it.</summary>
    internal static ProductInputEvent Payload(string json) => new(
        InputEventKind.DirectProductPayload, InputEdge.None, default, default, default, default, default, default, default, default,
        InputValueKind.ProductPayload, InputPhase.DirectUi, InputProvenance.DirectUi, default, default, default, 0f, 0f,
        ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty,
        Encoding.UTF8.GetBytes(ProductIdentity.UiActionContract), Encoding.UTF8.GetBytes(json));

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
    /// The classes and their skills come from the ruleset's own creation tables, so this fixture cannot
    /// drift from what creation offers. Staging it is what lets a product case start a session that creates
    /// a party at all: creation refuses a catalog that does not declare the classes and skills it offers,
    /// rather than offering a player a character the game's own data cannot carry.
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
                """
                {
                  "schemaVersion": 1,
                  "packId": "creation-tables",
                  "kind": "definitions",
                  "provenance": { "description": "authored for a test" },
                  "documents": [
                    { "path": "classes.json", "documentId": "classes", "definitionKind": "class" },
                    { "path": "skills.json", "documentId": "skills", "definitionKind": "skill" }
                  ]
                }
                """),
            ($"{ContentDirectory}/content-packs/creation-tables/classes.json",
                $$"""{ "documentId": "classes", "definitionKind": "class", "entries": [ {{string.Join(", ", classes)}} ] }"""),
            ($"{ContentDirectory}/content-packs/creation-tables/skills.json",
                $$"""{ "documentId": "skills", "definitionKind": "skill", "entries": [ {{string.Join(", ", skills.Select(skill => $$"""{ "id": "{{skill}}" }"""))}} ] }"""),
        ];
    }

    /// <summary>
    /// The context a ruleset composes a session from, over the content a test staged.
    /// </summary>
    /// <param name="context">The product context the content was staged in.</param>
    /// <param name="ui">The UI service the session publishes to.</param>
    /// <param name="creation">
    /// Whether the host declared a creation screen, which is what makes a new session create its party
    /// rather than play the one its scenario fixes.
    /// </param>
    /// <param name="engine">Whether the host is running inside an engine.</param>
    /// <param name="combat">
    /// Whether the host declared its act control, which is what a session reads an order to attack from. A
    /// context that declared none is still composed with the fight — it reads the world and publishes who is
    /// hostile — and no order ever reaches it.
    /// </param>
    internal static RulesetSessionContext RulesetContext(
        ProductCreateContext context,
        RecordingUiService ui,
        bool creation = false,
        bool engine = true,
        bool combat = false)
    {
        ContentBootstrapResult bootstrap = ContentBootstrap.Load(
            new ProductContentSource(context.Content),
            ContentLayout.Under(ContentDirectory),
            BuiltInBundles.Default);
        Assert.True(bootstrap.IsValid, string.Join("; ", bootstrap.Issues.Select(issue => issue.ToString())));
        Assert.NotNull(bootstrap.Selection);

        EngineUiProjectionChannel channel = new(
            ui,
            new UiStreamRequest(ProductIdentity.UiStream, ProductIdentity.UiContract));
        return new RulesetSessionContext(
            channel,
            new BundleSelection(bootstrap.Selection.Bundle.BundleId, bootstrap.Selection.Packs.Count),
            bootstrap.Catalog,
            Engine: engine ? context.Engine : null,
            Creation: creation
                ? new CreationIntentNames(
                    ProductIdentity.CreationAdvanceIntent,
                    ProductIdentity.CreationAcceptIntent,
                    ProductIdentity.UiActionContract)
                : null,
            Combat: combat
                ? new CombatIntentNames(
                    ProductIdentity.AttackIntent,
                    ProductIdentity.AttackAction,
                    ProductIdentity.UiActionContract)
                : null);
    }
}
