using System.Security.Cryptography;
using System.Text;
using MightAndMagic7.Import.Lod;

namespace MightAndMagic7.Import.Tool;

/// <summary>
/// Where an imported pack came from, read from the installation itself rather than assumed.
/// </summary>
/// <remarks>
/// The installation names its own release in its readme and its digital-distribution metadata names
/// the store build, so the provenance records both plus a digest over the containers. A pack therefore
/// cannot silently mix editions: an import from another copy of the game produces a different build
/// string, and the difference is visible in the pack rather than in someone's memory.
/// </remarks>
/// <param name="Game">The ruleset identity the content belongs to.</param>
/// <param name="Build">The release and store build the content was read from.</param>
/// <param name="Description">A statement of what was read and from where.</param>
/// <param name="ContainerDigest">A digest over the container names, sizes, and entry digests.</param>
internal sealed record InstallProvenance(string Game, string Build, string Description, string ContainerDigest)
{
    /// <summary>Reads the provenance of an installation.</summary>
    internal static InstallProvenance Read(LodInstall install)
    {
        ArgumentNullException.ThrowIfNull(install);
        string release = ReadRelease(install.Root);
        string store = ReadStoreBuild(install.Root);
        List<string> digests = [];
        foreach (string name in install.ArchiveNames())
        {
            string path = Path.Combine(install.DataDirectory, name);
            byte[] bytes = File.ReadAllBytes(path);
            digests.Add($"{name}\t{bytes.Length}\t{Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()}");
        }

        string containerDigest = Digest(Encoding.UTF8.GetBytes(string.Join('\n', digests)));
        string build = store.Length == 0 ? release : $"{release} ({store})";
        string description =
            $"Imported from the operator's own installation at {install.Root}: {install.ArchiveNames().Count} containers, " +
            $"{digests.Count} digests, release {release}{(store.Length == 0 ? string.Empty : $", {store}")}.";
        return new InstallProvenance("mightandmagic7", build, description, containerDigest);
    }

    /// <summary>The build string a pack records.</summary>
    internal string BuildString => $"{Build} [containers {ContainerDigest[..16]}]";

    /// <summary>Reads the game's own release line from its readme, when it is there.</summary>
    private static string ReadRelease(string root)
    {
        string? readme = Directory.EnumerateFiles(root, "*.txt", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .FirstOrDefault(name => string.Equals(name, "Readme.txt", StringComparison.OrdinalIgnoreCase));
        if (readme is null) return "unknown release";

        foreach (string line in File.ReadLines(Path.Combine(root, readme!)).Take(40))
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("Update v.", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed.Replace("ReadMe", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
            }
        }

        return "unknown release";
    }

    /// <summary>Reads the store build from the distribution metadata, when the installation has it.</summary>
    private static string ReadStoreBuild(string root)
    {
        string? info = Directory.EnumerateFiles(root, "*.info", SearchOption.TopDirectoryOnly)
            .FirstOrDefault(path => Path.GetFileName(path).StartsWith("goggame-", StringComparison.OrdinalIgnoreCase));
        if (info is null) return string.Empty;

        try
        {
            using System.Text.Json.JsonDocument parsed = System.Text.Json.JsonDocument.Parse(File.ReadAllText(info));
            System.Text.Json.JsonElement rootElement = parsed.RootElement;
            string gameId = rootElement.TryGetProperty("gameId", out System.Text.Json.JsonElement id) ? id.ToString() : string.Empty;
            string name = rootElement.TryGetProperty("name", out System.Text.Json.JsonElement title) ? title.GetString() ?? string.Empty : string.Empty;
            string language = rootElement.TryGetProperty("language", out System.Text.Json.JsonElement culture) ? culture.GetString() ?? string.Empty : string.Empty;
            return string.Join(' ', new[] { name, gameId.Length == 0 ? string.Empty : $"build {gameId}", language }.Where(part => part.Length > 0));
        }
        catch (System.Text.Json.JsonException)
        {
            return string.Empty;
        }
    }

    private static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
