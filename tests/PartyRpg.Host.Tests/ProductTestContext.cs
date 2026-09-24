using System.Text;
using Rusty.Engine;

namespace PartyRpg.Host.Tests;

/// <summary>Builds the engine values a product is created with, from content the test declares.</summary>
internal static class ProductTestContext
{
    internal const string ContentDirectory = "partyrpg";

    /// <summary>A product context whose staged content holds exactly the given files.</summary>
    internal static (ProductCreateContext Context, RecordingUiService Ui) Create(params (string Path, string Text)[] files)
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
        ProductCreateContext context = new(new FakeEngineContext(ui), content, input, new Rusty.Engine.Debugging.DebugExecutionContext());
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
    internal static ProductUpdate Update(ulong simulationStep, uint admittedSteps, double stepSeconds = 1.0 / 60.0)
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
        return new ProductUpdate(facts, ReadOnlySpan<ProductInputEvent>.Empty);
    }
}
