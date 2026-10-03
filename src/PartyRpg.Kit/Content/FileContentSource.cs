using System.Text;

namespace PartyRpg.Kit.Content;

/// <summary>
/// A content root on disk: the layout a checkout has, and the layout a developer edits while the
/// product is running. Relative paths are content-root-relative and are checked, so a pack manifest
/// cannot reach outside the root it was found in.
/// </summary>
public sealed class FileContentSource : IContentSource
{
    private readonly string _root;

    /// <summary>Opens a directory as a content root.</summary>
    public FileContentSource(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        _root = Path.GetFullPath(root);
    }

    /// <summary>The directory this source reads from.</summary>
    public string Root => _root;

    /// <inheritdoc />
    public IReadOnlyList<string> ListDirectories(string relativePath)
    {
        string path = Resolve(relativePath);
        if (!Directory.Exists(path)) return [];
        return [.. Directory.EnumerateDirectories(path)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!)
            .Order(StringComparer.Ordinal)];
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ListFiles(string relativePath)
    {
        string path = Resolve(relativePath);
        if (!Directory.Exists(path)) return [];
        return [.. Directory.EnumerateFiles(path)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!)
            .Order(StringComparer.Ordinal)];
    }

    /// <inheritdoc />
    public bool FileExists(string relativePath) => File.Exists(Resolve(relativePath));

    /// <inheritdoc />
    public string ReadText(string relativePath) => File.ReadAllText(Resolve(relativePath), Encoding.UTF8);

    private string Resolve(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        string combined = Path.GetFullPath(Path.Combine(_root, relativePath));
        // A separator is required after the root, or a sibling directory whose name merely starts with
        // the root's name would pass the prefix test.
        if (!string.Equals(combined, _root, StringComparison.Ordinal) &&
            !combined.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ContentValidationException(
                $"'{relativePath}' points outside the content root.",
                [new ContentValidationIssue("path-escapes-root", $"'{relativePath}' points outside the content root.", relativePath)]);
        }

        return combined;
    }
}
