using PartyRpg.Kit.Content;

namespace PartyRpg.Kit.Tests;

/// <summary>An in-memory content root, so the loader is exercised without touching the file system.</summary>
internal sealed class InMemoryContentSource : IContentSource
{
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);

    internal InMemoryContentSource Add(string path, string text)
    {
        _files[path] = text;
        return this;
    }

    public IReadOnlyList<string> ListDirectories(string relativePath)
    {
        string prefix = Normalize(relativePath);
        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (string path in _files.Keys)
        {
            if (!path.StartsWith(prefix, StringComparison.Ordinal)) continue;
            string remainder = path[prefix.Length..];
            int separator = remainder.IndexOf('/', StringComparison.Ordinal);
            if (separator > 0) names.Add(remainder[..separator]);
        }

        return [.. names.Order(StringComparer.Ordinal)];
    }

    public IReadOnlyList<string> ListFiles(string relativePath)
    {
        string prefix = Normalize(relativePath);
        return [.. _files.Keys
            .Where(path => path.StartsWith(prefix, StringComparison.Ordinal))
            .Select(path => path[prefix.Length..])
            .Where(remainder => remainder.Length > 0 && !remainder.Contains('/', StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)];
    }

    public bool FileExists(string relativePath) => _files.ContainsKey(relativePath);

    public string ReadText(string relativePath) =>
        _files.TryGetValue(relativePath, out string? text) ? text : throw new FileNotFoundException(relativePath);

    private static string Normalize(string relativePath)
    {
        string trimmed = relativePath.Trim('/');
        return trimmed.Length == 0 ? string.Empty : trimmed + "/";
    }
}
