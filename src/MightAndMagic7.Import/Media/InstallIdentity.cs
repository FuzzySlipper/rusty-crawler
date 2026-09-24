using System.Security.Cryptography;
using System.Text.Json;

namespace MightAndMagic7.Import.Media;

/// <summary>
/// Which release and build an installation is, recorded on every artifact so imported media cannot
/// silently mix editions.
/// </summary>
/// <remarks>
/// Two facts are taken from the installation itself: the release from the store's own descriptor file,
/// and the build from the SHA-256 of the game executable. Nothing here is a timestamp or a machine
/// name, because the manifest has to be byte-identical between runs.
/// </remarks>
/// <param name="Root">The installation root the media was read from.</param>
/// <param name="Release">Store, product id, name, language, and version, or <c>unknown</c>.</param>
/// <param name="Build">The hashed executable's name and its SHA-256, or <c>unknown</c>.</param>
public sealed record InstallIdentity(string Root, string Release, string Build)
{
    private const string Unknown = "unknown";

    /// <summary>Reads the identity of an installation.</summary>
    public static InstallIdentity Read(string installRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installRoot);
        return new InstallIdentity(installRoot, ReadRelease(installRoot), ReadBuild(installRoot));
    }

    private static string ReadRelease(string installRoot)
    {
        string? descriptor = Directory
            .EnumerateFiles(installRoot, "goggame-*.info", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.Ordinal)
            .FirstOrDefault();
        if (descriptor is null) return Unknown;

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(descriptor));
            JsonElement root = document.RootElement;
            string gameId = Text(root, "gameId");
            string name = Text(root, "name");
            string language = Text(root, "language");
            string version = root.TryGetProperty("version", out JsonElement versionElement) && versionElement.TryGetInt32(out int number)
                ? number.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : Unknown;

            return $"GOG {gameId} \"{name}\" version {version} ({language})";
        }
        catch (JsonException)
        {
            return Unknown;
        }
        catch (IOException)
        {
            return Unknown;
        }
    }

    private static string ReadBuild(string installRoot)
    {
        // The store's descriptor names mm7.exe as the game task, and that is the file whose hash pins
        // the build; the re-release's other executable is only a fallback for installations that ship
        // it under a different name.
        foreach (string candidate in new[] { "MM7.exe", "MM7-Rel.exe" })
        {
            string path = Path.Combine(installRoot, candidate);
            if (!File.Exists(path)) continue;
            using FileStream stream = File.OpenRead(path);
            return $"{candidate} sha256:{Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant()}";
        }

        return Unknown;
    }

    private static string Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? Unknown
            : Unknown;
}
