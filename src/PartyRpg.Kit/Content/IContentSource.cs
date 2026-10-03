namespace PartyRpg.Kit.Content;

/// <summary>
/// Somewhere packs can be read from: a directory of a content root, the engine's staged content, or a
/// test's in-memory tree. The loader reads packs through this rather than through the file system, so
/// the same code validates authored content, imported content, and fixtures.
/// </summary>
public interface IContentSource
{
    /// <summary>The subdirectory names directly under a relative directory, ordered by name.</summary>
    IReadOnlyList<string> ListDirectories(string relativePath);

    /// <summary>The file names directly under a relative directory, ordered by name.</summary>
    IReadOnlyList<string> ListFiles(string relativePath);

    /// <summary>Whether a relative path exists as a file.</summary>
    bool FileExists(string relativePath);

    /// <summary>Reads a relative path as text.</summary>
    string ReadText(string relativePath);
}
