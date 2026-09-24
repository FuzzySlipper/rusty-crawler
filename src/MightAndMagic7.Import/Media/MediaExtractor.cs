using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MightAndMagic7.Import.Lod;

namespace MightAndMagic7.Import.Media;

/// <summary>
/// Walks an installation's media containers and writes one file per decoded unit plus one manifest.
/// </summary>
/// <remarks>
/// <para>
/// What is emitted: one PNG per flat image, palette, nested PCX image, and sprite frame, and one RIFF
/// file per sound sample. What is deliberately not emitted, with the reason recorded in the manifest's
/// <c>boundaries</c> and against each artifact: font glyph atlases, movies, and the already-plain music
/// files. Sprite animation structure does not exist in these files at all — each sprite entry is one
/// frame image, and the grouping into animations lives in the game's own frame table — so nothing here
/// invents it.
/// </para>
/// <para>
/// The run is deterministic: containers are walked in name order, entries in the order the archive
/// stores them (already name-sorted), artifacts are sorted by identity, file names are derived only from
/// entry names, and the manifest carries no timestamp. Two runs over the same installation produce
/// byte-identical trees; the only machine-specific string in the manifest is the installation root.
/// </para>
/// </remarks>
public static class MediaExtractor
{
    /// <summary>The decoder generation recorded in the manifest.</summary>
    public const string DecoderVersion = "mm7-media-1";

    /// <summary>The manifest's file name inside the output root.</summary>
    public const string ManifestFileName = "media-manifest.json";

    private static readonly MediaBoundary[] DeclaredBoundaries =
    [
        new(
            "font glyph atlases",
            "Fonts are unwrapped and measured so the manifest proves the entry decoded, but no glyph atlas is emitted: the product renders its own interface text, so a font PNG has no consumer. Closing this needs the text renderer to consume font metrics first."),
        new(
            "video",
            "Payloads are Smacker and Bink movies. Neither codec has a decoder in this repository, the donor also defers to libav, and the product does not reproduce the original's movies, so the container is listed and its entries hashed but no slice is written."),
        new(
            "music",
            "Music/*.mp3 is already plain audio with no wrapper to undo, so there is nothing to extract; the extractor does not copy it."),
        new(
            "sprite animation semantics",
            "A sprite entry is one frame image. Animation groups, frame timing, octant mirroring, and the pivot rule live in the game's frame table rather than in the media, so no grouping is invented: every artifact carries its entry name, palette id, and empty bottom lines instead."),
        new(
            "map and game data archives",
            "GAMES.LOD, Events.lod, and the non-image payloads of the icon archive carry maps, event programs, and text rather than media. They are recorded as artifacts with their wrapper and hash so the census is complete, and no bytes are written."),
    ];

    /// <summary>Extracts every media unit an installation holds and writes the manifest.</summary>
    /// <param name="install">The installation to read.</param>
    /// <param name="outputRoot">The directory to write into; it is created when absent.</param>
    /// <returns>The manifest describing every artifact, emitted or not.</returns>
    public static MediaManifest Extract(LodInstall install, string outputRoot)
    {
        ArgumentNullException.ThrowIfNull(install);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputRoot);
        Directory.CreateDirectory(outputRoot);

        InstallIdentity identity = InstallIdentity.Read(install.Root);
        Func<ushort, IndexedPalette?> namedPalettes = BuildNamedPaletteLookup(install);
        List<MediaArtifact> artifacts = [];
        List<MediaArchiveSummary> archives = [];

        foreach (string archiveName in install.ArchiveNames())
        {
            LodArchive archive = install.Archive(archiveName);
            string directory = Path.Combine(outputRoot, SafeName(archiveName));
            Directory.CreateDirectory(directory);
            HashSet<string> taken = new(StringComparer.OrdinalIgnoreCase);
            int emitted = 0;
            int first = artifacts.Count;
            foreach (LodEntry entry in archive.Entries)
            {
                artifacts.Add(ExtractLodEntry(archive, archiveName, entry, directory, identity, namedPalettes, taken, ref emitted));
            }

            archives.Add(Summarize(archiveName, "lod", Path.Combine(install.DataDirectory, archiveName), archive.Entries.Count, artifacts, first, emitted));
        }

        foreach (string soundPath in MediaFiles(Path.Combine(install.Root, "SOUNDS"), ".snd"))
        {
            SndArchive bank = SndArchive.Open(soundPath);
            string directory = Path.Combine(outputRoot, SafeName(bank.Name));
            Directory.CreateDirectory(directory);
            HashSet<string> taken = new(StringComparer.OrdinalIgnoreCase);
            int emitted = 0;
            int first = artifacts.Count;
            foreach (SndEntry entry in bank.Entries)
            {
                artifacts.Add(ExtractSound(bank, entry, directory, identity, taken, ref emitted));
            }

            archives.Add(Summarize(bank.Name, "snd", soundPath, bank.Entries.Count, artifacts, first, emitted));
        }

        foreach (string videoPath in MediaFiles(Path.Combine(install.Root, "Anims"), ".vid"))
        {
            VidArchive video = VidArchive.Open(videoPath);
            int first = artifacts.Count;
            foreach (VidEntry entry in video.Entries)
            {
                artifacts.Add(new MediaArtifact
                {
                    Id = $"mm7:{video.Name}/{entry.Name}",
                    SourceRelease = identity.Release,
                    SourceBuild = identity.Build,
                    Archive = video.Name,
                    EntryName = entry.Name,
                    EntryOffset = entry.Offset,
                    EntrySize = checked((int)entry.Size),
                    EntrySha256 = Sha256OfSlice(videoPath, entry.Offset, entry.Size),
                    Wrapper = MediaWrapper.Raw,
                    PayloadDeflated = false,
                    PixelDeflated = false,
                    Kind = MediaKind.Video,
                    ExcludedReason = "movies are neither decoded nor sliced: no codec implementation exists here, and the product does not reproduce them",
                    Notes = $"{entry.Size} bytes beginning {MovieMagic(videoPath, entry)}",
                });
            }

            archives.Add(Summarize(video.Name, "vid", videoPath, video.Entries.Count, artifacts, first, 0));
        }

        // Sorting by identity rather than by walk order makes the manifest independent of the order the
        // file system hands names back.
        artifacts.Sort(static (left, right) => string.CompareOrdinal(left.Id, right.Id));
        MediaManifest manifest = new()
        {
            DecoderVersion = DecoderVersion,
            Source = identity,
            Boundaries = DeclaredBoundaries,
            Archives = archives,
            Artifacts = artifacts,
        };

        MediaManifestWriter.Write(manifest, Path.Combine(outputRoot, ManifestFileName));
        return manifest;
    }

    private static MediaArtifact ExtractLodEntry(
        LodArchive archive,
        string archiveName,
        LodEntry entry,
        string directory,
        InstallIdentity identity,
        Func<ushort, IndexedPalette?> namedPalettes,
        HashSet<string> taken,
        ref int emitted)
    {
        // Entry names are NUL-padded fields that may hold nothing at all; the fallback keeps an identity
        // unique without pretending the name was something it was not.
        string entryName = entry.Name.Length > 0 ? entry.Name : $"unnamed-{entry.Offset:x}";
        byte[] raw = archive.Raw(entry);
        string id = $"mm7:{archiveName}/{entryName}";

        LodPayload payload;
        try
        {
            payload = archive.Read(entry);
        }
        catch (LodFormatException error)
        {
            return new MediaArtifact
            {
                Id = id,
                SourceRelease = identity.Release,
                SourceBuild = identity.Build,
                Archive = archiveName,
                EntryName = entryName,
                EntryOffset = entry.Offset,
                EntrySize = entry.Size,
                EntrySha256 = Sha256(raw),
                Wrapper = MediaWrapper.Raw,
                PayloadDeflated = false,
                PixelDeflated = false,
                Kind = MediaKind.Other,
                ExcludedReason = "the container reader could not decode the entry",
                Notes = error.Message,
            };
        }

        MediaArtifact artifact = new()
        {
            Id = id,
            SourceRelease = identity.Release,
            SourceBuild = identity.Build,
            Archive = archiveName,
            EntryName = entryName,
            EntryOffset = entry.Offset,
            EntrySize = entry.Size,
            EntrySha256 = Sha256(raw),
            Wrapper = MediaDecoder.WrapperOf(payload),
            PayloadDeflated = payload.Kind is LodPayloadKind.Compressed or LodPayloadKind.DeflatedText,
            PixelDeflated = MediaDecoder.IsPixelBlockDeflated(raw),
            Kind = MediaKind.Other,
        };

        if (MediaDecoder.TryDecode(raw, namedPalettes, out DecodedImage? image, out string? reason))
        {
            string outputName = OutputName(entryName, ".png", taken);
            byte[] png = Encode(image!);
            File.WriteAllBytes(Path.Combine(directory, outputName), png);
            emitted++;
            return artifact with
            {
                Kind = image!.Kind,
                Width = image.Width,
                Height = image.Height,
                MipLevels = image.Levels.Count,
                // The image header's palette id is redundant against the embedded palette, so it is kept
                // as the cross-check field the format spec asks for and never used to pick colours.
                PaletteId = image.Kind is MediaKind.Sprite or MediaKind.Bitmap ? image.PaletteId : null,
                PaletteSource = image.Kind == MediaKind.Palette ? MediaPaletteSource.Embedded : image.PaletteSource,
                ZeroIsTransparent = image.Kind is MediaKind.Sprite or MediaKind.Bitmap ? image.ZeroIsTransparent : null,
                EmptyBottomLines = image.Kind == MediaKind.Sprite ? image.EmptyBottomLines : null,
                Flags = image.Kind is MediaKind.Sprite or MediaKind.Bitmap ? image.Flags : null,
                OutputPath = $"{archiveName}/{outputName}",
                OutputSha256 = Sha256(png),
                OutputBytes = png.Length,
                Notes = DecodeNotes(image, artifact.Wrapper),
            };
        }

        if (MediaDecoder.TryReadFont(raw, out FontFacts font, out _))
        {
            return artifact with
            {
                Kind = MediaKind.Font,
                ExcludedReason = "no glyph atlas is emitted: the product renders its own interface text, so a font PNG would have no consumer",
                Notes = string.Create(
                    CultureInfo.InvariantCulture,
                    $"font characters {font.FirstChar}..{font.LastChar}, depth {font.Depth}, height {font.Height}, atlas {font.AtlasSize} bytes, glyph bytes {font.GlyphBytes} of {font.PayloadBytes} unwrapped bytes, atlas accounts for the payload: {font.GlyphBytesMatchPayload}"),
            };
        }

        return artifact with
        {
            ExcludedReason = reason,
            Notes = artifact.Wrapper == MediaWrapper.Compressed
                ? "a compressed-data wrapper whose payload is game data rather than media"
                : null,
        };
    }

    private static MediaArtifact ExtractSound(
        SndArchive bank,
        SndEntry entry,
        string directory,
        InstallIdentity identity,
        HashSet<string> taken,
        ref int emitted)
    {
        MediaArtifact artifact = new()
        {
            Id = $"mm7:{bank.Name}/{entry.Name}",
            SourceRelease = identity.Release,
            SourceBuild = identity.Build,
            Archive = bank.Name,
            EntryName = entry.Name,
            EntryOffset = entry.Offset,
            EntrySize = entry.StoredSize,
            EntrySha256 = Sha256(bank.StoredBytes(entry)),
            Wrapper = MediaWrapper.Raw,
            PayloadDeflated = entry.DecompressedSize != 0 && entry.DecompressedSize != entry.StoredSize,
            PixelDeflated = false,
            Kind = MediaKind.Sound,
        };

        if (!bank.TryRead(entry, out byte[] riff, out bool rawDeflateFallback, out string? reason))
        {
            // One sample in the shipped bank is stored corruptly. Dropping the archive over it would lose
            // two and a half thousand good samples, so it is recorded and the walk continues.
            return artifact with
            {
                ExcludedReason = reason,
                Notes = "the sample is unreadable; the rest of the bank was still extracted",
            };
        }

        string outputName = OutputName(entry.Name, ".wav", taken);
        File.WriteAllBytes(Path.Combine(directory, outputName), riff);
        emitted++;
        bool haveFacts = WaveFacts.TryRead(riff, out WaveFacts facts, out string? waveReason);
        return artifact with
        {
            Audio = haveFacts ? facts : null,
            OutputPath = $"{bank.Name}/{outputName}",
            OutputSha256 = Sha256(riff),
            OutputBytes = riff.Length,
            Notes = haveFacts
                ? $"{(rawDeflateFallback ? "decoded with the raw-deflate fallback; " : string.Empty)}stored as {facts.Codec}, {facts.SampleRate} Hz, {facts.Channels} channel(s), {facts.BitsPerSample} bits"
                : waveReason,
        };
    }

    private static string? DecodeNotes(DecodedImage image, MediaWrapper wrapper)
    {
        List<string> notes = [];
        if (image.Levels.Count > 1)
        {
            notes.Add($"base level plus {image.Levels.Count - 1} mip level(s), largest first; only the base level is written");
        }

        if (image.Kind == MediaKind.Pcx)
        {
            notes.Add("nested PCX decoded to direct RGBA because an 8-bit three-plane file carries colour rather than palette indices");
        }

        if (image.PaletteSource == MediaPaletteSource.Unresolved)
        {
            notes.Add($"palette {image.PaletteId} is not present in any archive, so the PNG uses an identity grey ramp; the indices are unaffected, and sprite colours may be overridden by the game's frame table anyway");
        }

        if (wrapper == MediaWrapper.NonImage)
        {
            notes.Add("carried inside the non-image wrapper and sniffed after unwrapping");
        }

        return notes.Count == 0 ? null : string.Join("; ", notes);
    }

    private static byte[] Encode(DecodedImage image)
    {
        using MemoryStream stream = new();
        ImageWriter.WritePng(image, stream);
        return stream.ToArray();
    }

    /// <summary>
    /// Builds the sprite palette lookup from the palette entries the installation holds. The name is
    /// <c>pal%03d</c>, and the shipped archive mixes <c>PAL001</c> with <c>pal005</c>, so the lookup
    /// ignores case the way the container reader does.
    /// </summary>
    private static Func<ushort, IndexedPalette?> BuildNamedPaletteLookup(LodInstall install)
    {
        Dictionary<string, IndexedPalette> byName = new(StringComparer.OrdinalIgnoreCase);
        foreach (string archiveName in install.ArchiveNames())
        {
            LodArchive archive = install.Archive(archiveName);
            foreach (LodEntry entry in archive.Entries)
            {
                // Filtering on the name before reading keeps this from inflating every image in the
                // installation a second time just to find 376 palettes.
                if (entry.Name.Length != 6 || !entry.Name.StartsWith("pal", StringComparison.OrdinalIgnoreCase)) continue;

                LodPayload payload;
                try
                {
                    payload = archive.Read(entry);
                }
                catch (LodFormatException)
                {
                    continue;
                }

                if (payload.Kind != LodPayloadKind.Palette || payload.Palette is null) continue;
                byName.TryAdd(entry.Name, IndexedPalette.FromBytes(payload.Palette));
            }
        }

        return paletteId => byName.TryGetValue($"pal{paletteId:000}", out IndexedPalette? palette) ? palette : null;
    }

    private static MediaArchiveSummary Summarize(
        string name,
        string kind,
        string path,
        int entries,
        List<MediaArtifact> artifacts,
        int first,
        int emitted)
    {
        using FileStream stream = File.OpenRead(path);
        return new MediaArchiveSummary(
            name,
            kind,
            Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant(),
            stream.Length,
            entries,
            artifacts.Count - first,
            emitted);
    }

    /// <summary>
    /// The container files of one family. The search is by extension rather than by pattern because the
    /// shipped names disagree on case — one archive is <c>Events.lod</c> while the rest are upper case —
    /// and a case-sensitive pattern would find only one of them on a case-sensitive file system.
    /// </summary>
    private static IEnumerable<string> MediaFiles(string directory, string extension) =>
        Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
                .Where(path => string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.Ordinal)
            : [];

    private static string OutputName(string entryName, string extension, HashSet<string> taken)
    {
        string stem = Path.GetFileNameWithoutExtension(entryName);
        if (stem.Length == 0) stem = entryName;
        string candidate = SafeName(stem) + extension;
        if (taken.Add(candidate)) return candidate;

        // Two entries can want the same output name: an image called IB-L-A and a nested PCX called
        // IB-L-A.pcx both decode to IB-L-A.png. The suffix is derived from the entry name, so which one
        // gets it does not depend on walk order. Both cases exist in the shipped icon archive.
        string unique = $"{SafeName(stem)}~{Sha256(Encoding.Latin1.GetBytes(entryName))[..8]}{extension}";
        taken.Add(unique);
        return unique;
    }

    /// <summary>
    /// Makes a container's name safe to use as a path segment. A name read out of a binary file is
    /// attacker-controlled as far as the file system is concerned: it may contain separators, a drive
    /// letter, or <c>..</c>, any of which would write outside the output root. The verbatim name is kept
    /// in the manifest, where it is the identity rather than a path.
    /// </summary>
    private static string SafeName(string name)
    {
        StringBuilder safe = new(name.Length);
        foreach (char character in name)
        {
            bool allowed = character is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '.' or '_' or '-' or '+';
            safe.Append(allowed ? character : '_');
        }

        string result = safe.ToString();
        return result.Length == 0 || result.All(character => character == '.') ? "entry" : result;
    }

    private static string MovieMagic(string path, VidEntry entry)
    {
        using FileStream stream = File.OpenRead(path);
        stream.Seek(entry.Offset, SeekOrigin.Begin);
        byte[] magic = new byte[4];
        int read = stream.Read(magic, 0, magic.Length);
        return read == magic.Length ? Encoding.ASCII.GetString(magic) : "fewer than four bytes";
    }

    private static string Sha256OfSlice(string path, long offset, long size)
    {
        using FileStream stream = File.OpenRead(path);
        stream.Seek(offset, SeekOrigin.Begin);
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = new byte[1 << 20];
        long remaining = size;
        while (remaining > 0)
        {
            int read = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
            if (read <= 0) break;
            hash.AppendData(buffer, 0, read);
            remaining -= read;
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
