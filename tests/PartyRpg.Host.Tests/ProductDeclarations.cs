using System.Reflection;
using System.Xml.Linq;

namespace PartyRpg.Host.Tests;

/// <summary>
/// What the host declares, read from the two places it declares it: <see cref="ProductIdentity"/> in code, and the
/// host's project file, which is what the engine reads.
/// </summary>
/// <remarks>
/// The code half is read by referencing the constants — the suite is the host's friend — so a renamed constant is a
/// compile error here rather than a regular expression that stops matching. The project half is read as the XML the
/// engine reads, by element and attribute, so a mapping is tied to the intent it names rather than found anywhere
/// in the file.
/// </remarks>
internal static class ProductDeclarations
{
    private static readonly Lazy<XDocument> ProjectDocument = new(() => XDocument.Load(ProjectFile));

    /// <summary>The host's project file.</summary>
    internal static string ProjectFile { get; } = Repository.PathOf("src", "PartyRpg.Host", "PartyRpg.Host.csproj");

    /// <summary>The host's project file, as the engine reads it.</summary>
    internal static XDocument Project => ProjectDocument.Value;

    /// <summary>Every intent the code names: each <c>*Intent</c> constant of <see cref="ProductIdentity"/>, by its value.</summary>
    internal static IReadOnlyList<string> IntentsInCode { get; } =
    [
        .. typeof(ProductIdentity)
            .GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(field => field.IsLiteral && field.Name.EndsWith("Intent", StringComparison.Ordinal))
            .Select(field => (string)field.GetRawConstantValue()!)
            .Order(StringComparer.Ordinal),
    ];

    /// <summary>Every intent the project declares, with the kind of value it carries.</summary>
    internal static IReadOnlyList<(string Intent, string Value)> DeclaredIntents =>
    [
        .. Project.Descendants("RustyEngineProductInputIntent")
            .Select(element => ((string?)element.Attribute("Include") ?? string.Empty, (string?)element.Attribute("Value") ?? string.Empty)),
    ];

    /// <summary>The value one declared intent carries, which must be declared exactly once.</summary>
    internal static string ValueOf(string intent)
    {
        (string Intent, string Value)[] declared = [.. DeclaredIntents.Where(entry => entry.Intent == intent)];
        return declared.Length == 1
            ? declared[0].Value
            : throw new Xunit.Sdk.XunitException($"The project declares '{intent}' {declared.Length} times; the engine admits an intent it was told about once.");
    }

    /// <summary>The trigger of the one key mapping bound to an intent.</summary>
    internal static string TriggerOf(string intent)
    {
        string[] triggers =
        [
            .. Project.Descendants("RustyEngineProductInputMapping")
                .Where(mapping => (string?)mapping.Attribute("Intent") == intent)
                .Select(mapping => (string?)mapping.Attribute("Trigger") ?? string.Empty),
        ];
        return triggers.Length == 1
            ? triggers[0]
            : throw new Xunit.Sdk.XunitException($"The project maps {triggers.Length} keys to '{intent}'; each control has one.");
    }

    /// <summary>The value of one MSBuild property the project sets.</summary>
    internal static string Property(string name) =>
        Project.Descendants(name).FirstOrDefault()?.Value.Trim() ?? string.Empty;
}
