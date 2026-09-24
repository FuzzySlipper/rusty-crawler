using System.Text.Json;

namespace MightAndMagic7.Import.Media;

/// <summary>
/// Writes the extraction manifest as JSON, with the field order and formatting fixed in code.
/// </summary>
/// <remarks>
/// The manifest is written by hand rather than through a serializer because it is compared byte for
/// byte between runs: property order, indentation, line ending, and number formatting all have to be
/// decided here rather than left to reflection order or the machine's culture. There is no timestamp in
/// the document for the same reason — the run is identified by the container hashes instead.
/// </remarks>
public static class MediaManifestWriter
{
    private static readonly JsonWriterOptions Options = new()
    {
        Indented = true,

        // Pinned so a run on another platform produces the same bytes as a run here.
        NewLine = "\n",
    };

    /// <summary>Writes a manifest to disk, replacing any file already there.</summary>
    public static void Write(MediaManifest manifest, string path)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        File.WriteAllBytes(path, ToUtf8(manifest));
    }

    /// <summary>Renders a manifest as the bytes that would be written to disk.</summary>
    public static byte[] ToUtf8(MediaManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, Options))
        {
            WriteManifest(writer, manifest);
        }

        // A trailing newline keeps the file well formed as text and is part of the bytes compared
        // between runs.
        stream.WriteByte((byte)'\n');
        return stream.ToArray();
    }

    private static void WriteManifest(Utf8JsonWriter writer, MediaManifest manifest)
    {
        writer.WriteStartObject();
        writer.WriteNumber("manifestVersion", 1);
        writer.WriteString("decoderVersion", manifest.DecoderVersion);

        writer.WriteStartObject("sourceInstall");
        writer.WriteString("root", manifest.Source.Root);
        writer.WriteString("release", manifest.Source.Release);
        writer.WriteString("build", manifest.Source.Build);
        writer.WriteEndObject();

        writer.WriteStartObject("counts");
        writer.WriteNumber("artifacts", manifest.Artifacts.Count);
        writer.WriteNumber("emitted", manifest.EmittedCount);
        writer.WriteNumber("emittedBytes", manifest.EmittedBytes);
        writer.WriteStartObject("byKind");

        // Enum declaration order, so the counts do not depend on hashing or on the data.
        foreach (MediaKind kind in Enum.GetValues<MediaKind>())
        {
            writer.WriteNumber(JsonName(kind), manifest.Artifacts.Count(artifact => artifact.Kind == kind));
        }

        writer.WriteEndObject();
        writer.WriteEndObject();

        writer.WriteStartArray("boundaries");
        foreach (MediaBoundary boundary in manifest.Boundaries)
        {
            writer.WriteStartObject();
            writer.WriteString("family", boundary.Family);
            writer.WriteString("reason", boundary.Reason);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        writer.WriteStartArray("archives");
        foreach (MediaArchiveSummary archive in manifest.Archives)
        {
            writer.WriteStartObject();
            writer.WriteString("name", archive.Name);
            writer.WriteString("kind", archive.Kind);
            writer.WriteString("sha256", archive.Sha256);
            writer.WriteNumber("bytes", archive.Bytes);
            writer.WriteNumber("entries", archive.Entries);
            writer.WriteNumber("artifacts", archive.Artifacts);
            writer.WriteNumber("emitted", archive.Emitted);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();

        writer.WriteStartArray("artifacts");
        foreach (MediaArtifact artifact in manifest.Artifacts) WriteArtifact(writer, artifact);
        writer.WriteEndArray();

        writer.WriteEndObject();
    }

    private static void WriteArtifact(Utf8JsonWriter writer, MediaArtifact artifact)
    {
        writer.WriteStartObject();
        writer.WriteString("id", artifact.Id);
        writer.WriteString("sourceRelease", artifact.SourceRelease);
        writer.WriteString("sourceBuild", artifact.SourceBuild);
        writer.WriteString("archive", artifact.Archive);
        writer.WriteString("entryName", artifact.EntryName);
        writer.WriteNumber("entryOffset", artifact.EntryOffset);
        writer.WriteNumber("entrySize", artifact.EntrySize);
        writer.WriteString("entrySha256", artifact.EntrySha256);
        writer.WriteString("wrapper", JsonName(artifact.Wrapper));
        writer.WriteBoolean("payloadDeflated", artifact.PayloadDeflated);
        writer.WriteBoolean("pixelDeflated", artifact.PixelDeflated);
        writer.WriteString("kind", JsonName(artifact.Kind));
        WriteNullableNumber(writer, "width", artifact.Width);
        WriteNullableNumber(writer, "height", artifact.Height);
        WriteNullableNumber(writer, "mipLevels", artifact.MipLevels);
        WriteNullableNumber(writer, "paletteId", artifact.PaletteId);
        if (artifact.PaletteSource is MediaPaletteSource source) writer.WriteString("paletteSource", JsonName(source));
        else writer.WriteNull("paletteSource");

        WriteNullableBoolean(writer, "zeroIsTransparent", artifact.ZeroIsTransparent);
        WriteNullableNumber(writer, "emptyBottomLines", artifact.EmptyBottomLines);
        WriteNullableNumber(writer, "flags", artifact.Flags);

        if (artifact.Audio is WaveFacts audio)
        {
            writer.WriteStartObject("audio");
            writer.WriteString("codec", audio.Codec);
            writer.WriteNumber("formatTag", audio.FormatTag);
            writer.WriteNumber("sampleRate", audio.SampleRate);
            writer.WriteNumber("channels", audio.Channels);
            writer.WriteNumber("bitsPerSample", audio.BitsPerSample);
            writer.WriteNumber("blockAlign", audio.BlockAlign);
            writer.WriteNumber("samplesPerBlock", audio.SamplesPerBlock);
            writer.WriteNumber("samples", audio.Samples);
            writer.WriteEndObject();
        }
        else
        {
            writer.WriteNull("audio");
        }

        if (artifact.OutputPath is string path) writer.WriteString("outputPath", path);
        else writer.WriteNull("outputPath");
        if (artifact.OutputSha256 is string sha) writer.WriteString("outputSha256", sha);
        else writer.WriteNull("outputSha256");
        WriteNullableNumber(writer, "outputBytes", artifact.OutputBytes);
        if (artifact.ExcludedReason is string excluded) writer.WriteString("excludedReason", excluded);
        else writer.WriteNull("excludedReason");
        if (artifact.Notes is string notes) writer.WriteString("notes", notes);
        else writer.WriteNull("notes");
        writer.WriteEndObject();
    }

    private static void WriteNullableNumber(Utf8JsonWriter writer, string name, int? value)
    {
        if (value is int number) writer.WriteNumber(name, number);
        else writer.WriteNull(name);
    }

    private static void WriteNullableNumber(Utf8JsonWriter writer, string name, long? value)
    {
        if (value is long number) writer.WriteNumber(name, number);
        else writer.WriteNull(name);
    }

    private static void WriteNullableBoolean(Utf8JsonWriter writer, string name, bool? value)
    {
        if (value is bool flag) writer.WriteBoolean(name, flag);
        else writer.WriteNull(name);
    }

    /// <summary>The lower-case name a value carries in the manifest.</summary>
    private static string JsonName(MediaKind kind) => kind switch
    {
        MediaKind.Bitmap => "bitmap",
        MediaKind.Palette => "palette",
        MediaKind.Sprite => "sprite",
        MediaKind.Pcx => "pcx",
        MediaKind.Font => "font",
        MediaKind.Sound => "sound",
        MediaKind.Video => "video",
        _ => "other",
    };

    private static string JsonName(MediaWrapper wrapper) => wrapper switch
    {
        MediaWrapper.Compressed => "compressed",
        MediaWrapper.Image => "image",
        MediaWrapper.Palette => "palette",
        MediaWrapper.NonImage => "nonImage",
        MediaWrapper.Sprite => "sprite",
        _ => "raw",
    };

    private static string JsonName(MediaPaletteSource source) => source switch
    {
        MediaPaletteSource.None => "none",
        MediaPaletteSource.Embedded => "embedded",
        MediaPaletteSource.Named => "palNamed",
        MediaPaletteSource.PcxTail => "pcxTail",
        _ => "unresolved",
    };
}
