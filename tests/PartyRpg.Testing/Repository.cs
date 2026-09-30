namespace PartyRpg.Testing;

/// <summary>
/// The checkout a suite runs from, and the files in it that belong to the repository.
/// </summary>
/// <remarks>
/// <para>
/// The root is found by two markers the repository tracks — <c>Directory.Build.props</c> and
/// <c>scripts/verify.sh</c> — rather than by <c>AGENTS.md</c>, which <c>.gitignore</c> matches and a checkout may
/// carry a local variant of.
/// </para>
/// <para>
/// Every scan goes through <see cref="Files"/>, which never descends into what is not the repository's own: build
/// output, installed packages, the operator's local evidence, generated UI, and the agent worktrees an agent
/// session keeps under <c>.claude/</c>. A law that read those would fail because a second checkout exists, which
/// is not a boundary.
/// </para>
/// </remarks>
public static class Repository
{
    /// <summary>Directory names a repository scan never descends into, wherever they stand.</summary>
    public static IReadOnlySet<string> IgnoredDirectories { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        ".claude", ".git", ".runtime", ".letta", "bin", "obj", "node_modules", "generated", "GeneratedInputs",
    };

    /// <summary>Paths below the root a repository scan never reads, because the operator owns them.</summary>
    private static readonly string[] IgnoredRoots = ["local"];

    private static readonly Lazy<string> RootDirectory = new(Find);

    /// <summary>The repository root.</summary>
    public static string Root => RootDirectory.Value;

    /// <summary>A path below the root.</summary>
    public static string PathOf(params string[] parts) => Path.Combine([Root, .. parts]);

    /// <summary>A path relative to the root, with forward slashes.</summary>
    public static string Relative(string path) =>
        Path.GetRelativePath(Root, path).Replace(Path.DirectorySeparatorChar, '/');

    /// <summary>The text of a file below the root.</summary>
    public static string Read(params string[] parts) => File.ReadAllText(PathOf(parts));

    /// <summary>
    /// Every file below a directory of the repository that matches a pattern, skipping everything the repository
    /// does not own.
    /// </summary>
    /// <param name="relativeDirectory">The directory, relative to the root; empty for the whole repository.</param>
    /// <param name="pattern">The file pattern, such as <c>*.cs</c>.</param>
    public static IEnumerable<string> Files(string relativeDirectory, string pattern)
    {
        string start = relativeDirectory.Length == 0 ? Root : PathOf(relativeDirectory.Split('/'));
        if (!Directory.Exists(start)) throw new DirectoryNotFoundException($"{start} is not a directory of the repository.");
        Stack<string> pending = new([start]);
        List<string> found = [];
        while (pending.Count > 0)
        {
            string directory = pending.Pop();
            found.AddRange(Directory.EnumerateFiles(directory, pattern));
            foreach (string child in Directory.EnumerateDirectories(directory))
            {
                if (IgnoredDirectories.Contains(Path.GetFileName(child))) continue;
                if (IgnoredRoots.Contains(Relative(child), StringComparer.Ordinal)) continue;
                pending.Push(child);
            }
        }

        return found.Order(StringComparer.Ordinal);
    }

    private static string Find()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Directory.Build.props")) &&
                File.Exists(Path.Combine(directory.FullName, "scripts", "verify.sh")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "The repository root (the directory holding Directory.Build.props and scripts/verify.sh) is not above the test output.");
    }
}
