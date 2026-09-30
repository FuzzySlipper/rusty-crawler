namespace PartyRpg.Testing;

/// <summary>
/// Content packs a suite writes in memory: a definitions manifest and the documents it lists, in the shape the
/// kit's loader reads.
/// </summary>
public static class TestPacks
{
    /// <summary>The places document a world pack lists.</summary>
    public static readonly (string DocumentId, string DefinitionKind) Places = ("places", "place");

    /// <summary>The travel links document a world pack lists.</summary>
    public static readonly (string DocumentId, string DefinitionKind) Links = ("links", "travel-link");

    /// <summary>A world pack of places and the links between them.</summary>
    public static string World { get; } = Manifest("world", Places, Links);

    /// <summary>A world pack of places alone.</summary>
    public static string PlacesOnly { get; } = Manifest("world", Places);

    /// <summary>A definitions pack manifest listing documents, each stored as <c>{documentId}.json</c>.</summary>
    public static string Manifest(string packId, params (string DocumentId, string DefinitionKind)[] documents) =>
        $$"""
        {
          "schemaVersion": 1,
          "packId": "{{packId}}",
          "kind": "definitions",
          "provenance": { "description": "authored for a test" },
          "documents": [
            {{string.Join(",\n    ", documents.Select(document =>
                $$"""{ "path": "{{document.DocumentId}}.json", "documentId": "{{document.DocumentId}}", "definitionKind": "{{document.DefinitionKind}}" }"""))}}
          ]
        }
        """;

    /// <summary>One definitions document of the given entries, each an object written by the suite.</summary>
    public static string Document(string documentId, string definitionKind, params string[] entries) =>
        $$"""
        { "documentId": "{{documentId}}", "definitionKind": "{{definitionKind}}", "entries": [ {{string.Join(",", entries)}} ] }
        """;
}
