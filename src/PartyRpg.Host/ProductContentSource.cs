using PartyRpg.Kit.Content;
using Rusty.Engine;

namespace PartyRpg.Host;

/// <summary>
/// Reads the product's content root through the engine's staged content, so the packs a running
/// product sees are the packs the engine staged for it — not whatever happens to be on disk beside the
/// binary.
/// </summary>
/// <remarks>
/// Directories are derived from the staged file paths rather than asked of the engine, because the
/// staged content is a flat list of files: a directory exists exactly when some file lives under it.
/// </remarks>
internal sealed class ProductContentSource : IContentSource
{
    private readonly ProductContent _content;

    internal ProductContentSource(ProductContent content)
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ListDirectories(string relativePath)
    {
        string prefix = Normalize(relativePath);
        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (string path in Paths())
        {
            if (!path.StartsWith(prefix, StringComparison.Ordinal)) continue;
            string remainder = path[prefix.Length..];
            int separator = remainder.IndexOf('/', StringComparison.Ordinal);
            if (separator > 0) names.Add(remainder[..separator]);
        }

        return [.. names.Order(StringComparer.Ordinal)];
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ListFiles(string relativePath)
    {
        string prefix = Normalize(relativePath);
        return [.. Paths()
            .Where(path => path.StartsWith(prefix, StringComparison.Ordinal))
            .Select(path => path[prefix.Length..])
            .Where(remainder => remainder.Length > 0 && !remainder.Contains('/', StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)];
    }

    /// <inheritdoc />
    public bool FileExists(string relativePath) => _content.TryReadFile(relativePath, out _);

    /// <inheritdoc />
    public string ReadText(string relativePath) => _content.ReadText(relativePath);

    private IEnumerable<string> Paths() =>
        _content.Files.ToArray().Select(file => file.RelativePath.Replace('\\', '/'));

    private static string Normalize(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        string trimmed = relativePath.Replace('\\', '/').Trim('/');
        return trimmed.Length == 0 ? string.Empty : trimmed + "/";
    }
}
