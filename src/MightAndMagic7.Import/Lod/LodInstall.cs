namespace MightAndMagic7.Import.Lod;

/// <summary>
/// An operator's game installation, opened for reading. Archives are opened on demand and cached, and
/// a table is read only through its declared <see cref="LodSource"/>.
/// </summary>
public sealed class LodInstall
{
    /// <summary>The directory inside an installation that holds the containers.</summary>
    public const string DataDirectoryName = "DATA";

    private readonly Dictionary<string, LodArchive> _archives = new(StringComparer.OrdinalIgnoreCase);

    private LodInstall(string root, string dataDirectory)
    {
        Root = root;
        DataDirectory = dataDirectory;
    }

    /// <summary>The installation root.</summary>
    public string Root { get; }

    /// <summary>The directory the containers are read from.</summary>
    public string DataDirectory { get; }

    /// <summary>Opens an installation, failing when it does not look like one.</summary>
    public static LodInstall Open(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        if (!Directory.Exists(root))
        {
            throw new LodFormatException($"'{root}' is not a directory.");
        }

        string dataDirectory = Path.Combine(root, DataDirectoryName);
        if (!Directory.Exists(dataDirectory))
        {
            throw new LodFormatException($"'{root}' has no {DataDirectoryName} directory, so it is not a game installation.");
        }

        return new LodInstall(root, dataDirectory);
    }

    /// <summary>
    /// The containers present in the installation's data directory. The search is by extension rather
    /// than by pattern because the shipped names disagree on case: one archive is <c>Events.lod</c>
    /// while the rest are upper case, and a case-sensitive pattern finds only one of them.
    /// </summary>
    public IReadOnlyList<string> ArchiveNames() =>
        [.. Directory.EnumerateFiles(DataDirectory, "*", SearchOption.TopDirectoryOnly)
            .Where(path => string.Equals(Path.GetExtension(path), ".lod", StringComparison.OrdinalIgnoreCase))
            .Select(path => Path.GetFileName(path))
            .Order(StringComparer.OrdinalIgnoreCase)];

    /// <summary>Opens one container by name, ignoring case.</summary>
    public LodArchive Archive(string archiveName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archiveName);
        if (_archives.TryGetValue(archiveName, out LodArchive? cached)) return cached;

        string? path = Directory.EnumerateFiles(DataDirectory, "*", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(candidate =>
                string.Equals(Path.GetExtension(candidate), ".lod", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(Path.GetFileName(candidate), archiveName, StringComparison.OrdinalIgnoreCase));
        if (path is null)
        {
            throw new LodFormatException($"'{Root}' has no container named '{archiveName}'.");
        }

        LodArchive archive = LodArchive.Open(path);
        _archives[archiveName] = archive;
        return archive;
    }

    /// <summary>
    /// Reads a declared source. The declared archive is the only one consulted; a missing entry is an
    /// error naming both the table and the archive rather than a search of the others.
    /// </summary>
    public LodPayload Read(LodSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Archive(source.ArchiveName).Read(source.EntryName);
    }

    /// <summary>
    /// Every container that holds an entry with this name. More than one means a table is ambiguous
    /// by name alone, which is exactly why sources are declared.
    /// </summary>
    public IReadOnlyList<string> ArchivesContaining(string entryName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entryName);
        List<string> matches = [];
        foreach (string archiveName in ArchiveNames())
        {
            if (Archive(archiveName).Find(entryName) is not null) matches.Add(archiveName);
        }

        return matches;
    }
}
