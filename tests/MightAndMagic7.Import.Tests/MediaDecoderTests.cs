using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using MightAndMagic7.Import.Lod;
using MightAndMagic7.Import.Media;
using Xunit;

namespace MightAndMagic7.Import.Tests;

/// <summary>
/// Exercises the media decoder and the extractor over synthesized entries.
/// </summary>
/// <remarks>
/// Every byte these tests read is built here, so the suite runs without the operator's installation.
/// The real installation is evidence for the importer's report, never a test dependency.
/// </remarks>
public sealed class MediaDecoderTests
{
    [Fact]
    public void Image_decodes_geometry_palette_and_the_transparency_flag()
    {
        byte[] palette = MediaFixture.Palette(seed: 7);
        byte[] pixels = MediaFixture.Pixels(4, 4, seed: 3);
        pixels[5] = 0;
        byte[] entry = MediaFixture.ImageWrapper(pixels, 4, 4, palette, paletteId: 132, flags: MediaDecoder.ZeroIsTransparentFlag, mipLevels: 1);

        DecodedImage image = MediaDecoder.DecodeImage(entry);

        Assert.Equal(MediaKind.Bitmap, image.Kind);
        Assert.Equal(4, image.Width);
        Assert.Equal(4, image.Height);
        Assert.Equal(pixels, image.Indices.ToArray());
        Assert.Single(image.Levels);
        Assert.Equal(MediaPaletteSource.Embedded, image.PaletteSource);
        Assert.True(image.ZeroIsTransparent);
        Assert.Equal(MediaDecoder.ZeroIsTransparentFlag, image.Flags);
        Assert.Equal(132, image.PaletteId);

        // Index 0 is transparent only because the flag says so, and the colours still come from the
        // palette rather than from the index.
        byte[] rgba = image.ToRgba();
        Assert.Equal(0, rgba[(5 * 4) + 3]);
        Assert.Equal(255, rgba[(6 * 4) + 3]);
        Assert.Equal(palette[pixels[6] * 3], rgba[6 * 4]);
        Assert.Equal(palette[(pixels[6] * 3) + 1], rgba[(6 * 4) + 1]);
        Assert.Equal(palette[(pixels[6] * 3) + 2], rgba[(6 * 4) + 2]);
    }

    [Fact]
    public void Image_without_the_transparency_flag_keeps_index_zero_opaque()
    {
        byte[] pixels = MediaFixture.Pixels(4, 4, seed: 5);
        pixels[0] = 0;
        byte[] entry = MediaFixture.ImageWrapper(pixels, 4, 4, MediaFixture.Palette(seed: 1), paletteId: 1, flags: 0, mipLevels: 1);

        DecodedImage image = MediaDecoder.DecodeImage(entry);

        Assert.False(image.ZeroIsTransparent);
        byte[] rgba = image.ToRgba();
        for (int pixel = 0; pixel < 16; pixel++) Assert.Equal(255, rgba[(pixel * 4) + 3]);
    }

    [Fact]
    public void Image_mip_chain_is_measured_from_the_pixel_block()
    {
        // The shipped mipmapped images carry four levels: the base, then three halvings. The same rule
        // has to hold for a block that stops after two, which is why the count is measured.
        byte[] four = MediaFixture.ImageWrapper(MediaFixture.Pixels(32, 32, seed: 9), 32, 32, MediaFixture.Palette(seed: 2), 4, 0, mipLevels: 4);
        byte[] two = MediaFixture.ImageWrapper(MediaFixture.Pixels(32, 32, seed: 9), 32, 32, MediaFixture.Palette(seed: 2), 4, 0, mipLevels: 2);

        DecodedImage full = MediaDecoder.DecodeImage(four);
        DecodedImage partial = MediaDecoder.DecodeImage(two);

        Assert.Equal(4, full.Levels.Count);
        Assert.Equal(new[] { 32, 16, 8, 4 }, full.Levels.Select(level => level.Width));
        Assert.Equal(new[] { 0, 1024, 1280, 1344 }, full.Levels.Select(level => level.Offset));
        Assert.Equal(2, partial.Levels.Count);
        Assert.Equal(32 * 32, partial.Levels[0].Width * partial.Levels[0].Height);
        Assert.Equal(1280, partial.Pixels.Length);
    }

    [Fact]
    public void Palette_entry_decodes_to_a_256_by_1_strip()
    {
        byte[] palette = MediaFixture.Palette(seed: 11);
        DecodedImage image = MediaDecoder.DecodeImage(MediaFixture.PaletteEntry(palette));

        Assert.Equal(MediaKind.Palette, image.Kind);
        Assert.Equal(256, image.Width);
        Assert.Equal(1, image.Height);
        Assert.Equal(MediaPaletteSource.Embedded, image.PaletteSource);
        Assert.Equal(0, image.Indices[0]);
        Assert.Equal(255, image.Indices[255]);
        Assert.Equal(new Rgb24(palette[30], palette[31], palette[32]), image.Palette[10]);
    }

    [Fact]
    public void Unwrapped_image_payload_is_refused_because_its_geometry_is_gone()
    {
        byte[] pixels = MediaFixture.Pixels(4, 4, seed: 1);
        LodPayload payload = new(
            new LodEntry("A1b", 0, pixels.Length),
            pixels,
            LodPayloadKind.Image,
            MediaFixture.Palette(seed: 3));

        MediaFormatException error = Assert.Throws<MediaFormatException>(() => MediaDecoder.DecodeImage(payload));

        Assert.Contains("geometry", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Sprite_draws_its_line_table_onto_a_transparent_canvas()
    {
        byte[] canvas = new byte[6 * 4];
        for (int x = 1; x < 5; x++)
        {
            canvas[(2 * 6) + x] = (byte)(10 + x);
            canvas[(3 * 6) + x] = (byte)(20 + x);
        }

        byte[] entry = MediaFixture.Sprite(canvas, 6, 4, paletteId: 2, emptyBottomLines: 1, flags: 0x20, deflated: true);
        DecodedImage image = MediaDecoder.DecodeImage(entry, paletteId => paletteId == 2 ? MediaFixture.PaletteAsObject(seed: 4) : null);

        Assert.Equal(MediaKind.Sprite, image.Kind);
        Assert.Equal(6, image.Width);
        Assert.Equal(4, image.Height);
        Assert.Equal(canvas, image.Indices.ToArray());
        Assert.Equal(2, image.PaletteId);
        Assert.Equal(1, image.EmptyBottomLines);
        Assert.Equal(0x20u, image.Flags);
        Assert.True(image.ZeroIsTransparent);
        Assert.Equal(MediaPaletteSource.Named, image.PaletteSource);

        // The columns outside the drawn span stay index 0, which is the transparent colour.
        byte[] rgba = image.ToRgba();
        Assert.Equal(0, rgba[(2 * 6) * 4 + 3]);
        Assert.Equal(255, rgba[((2 * 6) + 2) * 4 + 3]);
    }

    [Fact]
    public void Sprite_with_an_unknown_palette_id_still_decodes_its_shape()
    {
        byte[] canvas = new byte[4 * 4];
        canvas[5] = 200;
        byte[] entry = MediaFixture.Sprite(canvas, 4, 4, paletteId: 940, emptyBottomLines: 0, flags: 0, deflated: false);

        DecodedImage image = MediaDecoder.DecodeImage(entry, _ => null);

        Assert.Equal(MediaPaletteSource.Unresolved, image.PaletteSource);
        Assert.Equal(940, image.PaletteId);
        byte[] rgba = image.ToRgba();
        Assert.Equal(200, rgba[(5 * 4) + 0]);
        Assert.Equal(200, rgba[(5 * 4) + 2]);
    }

    [Fact]
    public void Sprite_line_table_that_runs_past_its_pixel_block_is_refused()
    {
        byte[] canvas = new byte[4 * 4];
        canvas[0] = 7;
        byte[] entry = MediaFixture.Sprite(canvas, 4, 4, paletteId: 1, emptyBottomLines: 0, flags: 0, deflated: false);

        // Row 0 has a span, and its offset field is the fourth word of its eight-byte record.
        BinaryPrimitives.WriteUInt32LittleEndian(entry.AsSpan(32 + 4), 4096);

        Assert.False(MediaDecoder.TryDecode(entry, null, out DecodedImage? image, out string? reason));
        Assert.Null(image);
        Assert.Contains("line table is invalid", reason!, StringComparison.Ordinal);
    }

    [Fact]
    public void Nested_pcx_three_plane_file_decodes_to_direct_colour()
    {
        byte[] rgb = new byte[3 * 2 * 3];
        for (int pixel = 0; pixel < 6; pixel++)
        {
            rgb[(pixel * 3) + 0] = (byte)(pixel * 10);
            rgb[(pixel * 3) + 1] = (byte)(100 + pixel);
            rgb[(pixel * 3) + 2] = (byte)(200 - pixel);
        }

        byte[] entry = MediaFixture.NonImageWrapper(MediaFixture.Pcx24(3, 2, rgb), deflated: true);
        DecodedImage image = MediaDecoder.DecodeImage(entry);

        Assert.Equal(MediaKind.Pcx, image.Kind);
        Assert.Equal(3, image.Width);
        Assert.Equal(2, image.Height);
        Assert.Equal(MediaPaletteSource.None, image.PaletteSource);

        // A three-plane PCX stores colour rather than indices, so the unit is direct RGBA and every
        // pixel is opaque.
        byte[] expected = new byte[6 * 4];
        for (int pixel = 0; pixel < 6; pixel++)
        {
            expected[(pixel * 4) + 0] = rgb[(pixel * 3) + 0];
            expected[(pixel * 4) + 1] = rgb[(pixel * 3) + 1];
            expected[(pixel * 4) + 2] = rgb[(pixel * 3) + 2];
            expected[(pixel * 4) + 3] = 255;
        }

        Assert.Equal(expected, image.ToRgba());
    }

    [Fact]
    public void Nested_pcx_single_plane_file_uses_its_tail_palette()
    {
        byte[] palette = MediaFixture.Palette(seed: 13);
        byte[] indices = [1, 2, 3, 4, 5, 6];
        byte[] entry = MediaFixture.NonImageWrapper(MediaFixture.Pcx8(3, 2, indices, palette), deflated: false);

        DecodedImage image = MediaDecoder.DecodeImage(entry);

        Assert.Equal(MediaKind.Pcx, image.Kind);
        Assert.Equal(indices, image.Indices.ToArray());
        Assert.Equal(MediaPaletteSource.PcxTail, image.PaletteSource);
        Assert.Equal(new Rgb24(palette[9], palette[10], palette[11]), image.Palette[3]);
    }

    [Fact]
    public void Nested_pcx_run_length_data_that_ends_early_is_refused()
    {
        // Truncating after the 128-byte header leaves a file whose header promises more scanlines than
        // the run-length data can produce.
        byte[] pcx = MediaFixture.Pcx8(16, 16, MediaFixture.Pixels(16, 16, seed: 4), tailPalette: null);
        byte[] entry = MediaFixture.NonImageWrapper(pcx[..140], deflated: false);

        Assert.False(MediaDecoder.TryDecode(entry, null, out DecodedImage? image, out string? reason));
        Assert.Null(image);
        Assert.Contains("run-length data ends", reason!, StringComparison.Ordinal);
    }

    [Fact]
    public void Font_is_measured_and_its_glyph_bytes_account_for_the_payload()
    {
        byte[] font = MediaFixture.Font(firstChar: 31, lastChar: 255, height: 4, glyphWidth: 1);
        byte[] entry = MediaFixture.NonImageWrapper(font, deflated: true);

        Assert.True(MediaDecoder.TryReadFont(entry, out FontFacts facts, out string? reason), reason);
        Assert.Equal(31, facts.FirstChar);
        Assert.Equal(255, facts.LastChar);
        Assert.Equal(4, facts.Height);
        Assert.Equal(4096, facts.AtlasSize);
        Assert.Equal(225 * 4, facts.GlyphBytes);
        Assert.True(facts.GlyphBytesMatchPayload);

        // A font is not a decodable image, so the extractor records it rather than writing a PNG.
        Assert.False(MediaDecoder.TryDecode(entry, null, out DecodedImage? image, out _));
        Assert.Null(image);
    }

    [Fact]
    public void Font_in_the_narrow_atlas_layout_is_recognised_by_its_glyph_bytes()
    {
        // The shipped archive holds one font whose atlas carries only a u8 width per character; which
        // layout a file uses is decided by which one accounts for the payload, not by its name.
        byte[] entry = MediaFixture.NonImageWrapper(MediaFixture.NarrowFont(firstChar: 31, lastChar: 255, height: 27), deflated: true);

        Assert.True(MediaDecoder.TryReadFont(entry, out FontFacts facts, out string? reason), reason);
        Assert.Equal(1280, facts.AtlasSize);
        Assert.Equal(225 * 27, facts.GlyphBytes);
        Assert.True(facts.GlyphBytesMatchPayload);
    }

    [Fact]
    public void Wrapper_classification_follows_the_container_reader()
    {
        byte[] image = MediaFixture.ImageWrapper(MediaFixture.Pixels(2, 2, 1), 2, 2, MediaFixture.Palette(1), 1, 0, mipLevels: 1);
        byte[] sprite = MediaFixture.Sprite(MediaFixture.Pixels(2, 2, 1), 2, 2, 1, 0, 0, deflated: false);
        byte[] pcx = MediaFixture.NonImageWrapper(MediaFixture.Pcx8(2, 2, new byte[4], null), deflated: false);

        Assert.Equal(MediaWrapper.Image, MediaDecoder.WrapperOf(Payload("A1b", image)));
        Assert.Equal(MediaWrapper.Palette, MediaDecoder.WrapperOf(Payload("pal002", MediaFixture.PaletteEntry(MediaFixture.Palette(1)))));
        Assert.Equal(MediaWrapper.Sprite, MediaDecoder.WrapperOf(Payload("ArrowA0", sprite)));
        Assert.Equal(MediaWrapper.NonImage, MediaDecoder.WrapperOf(Payload("Border2.pcx", pcx)));
        Assert.Equal(MediaWrapper.Compressed, MediaDecoder.WrapperOf(Payload("7out13.odm", LodFixture.Compressed("map data"u8.ToArray()))));
    }

    [Fact]
    public void Pixel_deflate_fact_is_reported_separately_from_the_container_wrapper()
    {
        byte[] deflated = MediaFixture.ImageWrapper(MediaFixture.Pixels(4, 4, 2), 4, 4, MediaFixture.Palette(1), 1, 0, mipLevels: 1, deflatePixels: true);
        byte[] stored = MediaFixture.ImageWrapper(MediaFixture.Pixels(4, 4, 2), 4, 4, MediaFixture.Palette(1), 1, 0, mipLevels: 1);
        byte[] sprite = MediaFixture.Sprite(MediaFixture.Pixels(2, 2, 1), 2, 2, 1, 0, 0, deflated: true);

        Assert.True(MediaDecoder.IsPixelBlockDeflated(deflated));
        Assert.False(MediaDecoder.IsPixelBlockDeflated(stored));
        Assert.True(MediaDecoder.IsPixelBlockDeflated(sprite));
    }

    [Fact]
    public void Png_writer_emits_a_readable_file_with_matching_chunk_checksums()
    {
        byte[] palette = MediaFixture.Palette(seed: 21);
        byte[] pixels = MediaFixture.Pixels(5, 3, seed: 17);
        DecodedImage image = MediaDecoder.DecodeImage(MediaFixture.ImageWrapper(pixels, 5, 3, palette, 1, MediaDecoder.ZeroIsTransparentFlag, mipLevels: 1));

        using MemoryStream stream = new();
        ImageWriter.WritePng(image, stream);
        byte[] png = stream.ToArray();

        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, png[..8]);
        Png pngFile = Png.Parse(png);
        Assert.Equal(5, pngFile.Width);
        Assert.Equal(3, pngFile.Height);
        Assert.Equal(8, pngFile.BitDepth);
        Assert.Equal(6, pngFile.ColorType);
        Assert.Equal("IEND", pngFile.LastChunkType);
        Assert.Equal(image.ToRgba(), pngFile.DecodeRgba());
    }

    [Fact]
    public void Sound_bank_decodes_stored_and_deflated_samples_and_records_a_corrupt_one()
    {
        byte[] first = MediaFixture.Riff(1, 22050, 512, samples: 1017, payloadBytes: 40);
        byte[] second = MediaFixture.Riff(2, 22050, 1024, samples: 2041, payloadBytes: 60);
        byte[] bank = MediaFixture.SoundBank(
            ("one", first, Deflated: true),
            ("two", second, Deflated: false),
            ("three", [1, 2, 3, 4], Deflated: false));

        // Claim the third sample was deflated although its bytes are not, which is how a corrupt sample
        // presents itself in the shipped bank.
        BinaryPrimitives.WriteUInt32LittleEndian(bank.AsSpan(4 + (2 * 52) + 0x30), 999);
        SndArchive archive = SndArchive.FromBytes("Audio.snd", bank);

        Assert.Equal(3, archive.Entries.Count);
        Assert.True(archive.TryRead(archive.Entries[0], out byte[] one, out bool fallback, out _));
        Assert.False(fallback);
        Assert.Equal(first, one);
        Assert.True(archive.TryRead(archive.Entries[1], out byte[] two, out _, out _));
        Assert.Equal(second, two);
        Assert.Equal(archive.Entries[1].StoredSize, archive.Entries[1].DecompressedSize);
        Assert.False(archive.TryRead(archive.Entries[2], out _, out _, out string? reason));
        Assert.Contains("not readable deflate", reason!, StringComparison.Ordinal);

        Assert.True(WaveFacts.TryRead(one, out WaveFacts facts, out _));
        Assert.Equal(17, facts.FormatTag);
        Assert.Equal("adpcm_ima_wav", facts.Codec);
        Assert.Equal(1, facts.Channels);
        Assert.Equal(22050, facts.SampleRate);
        Assert.Equal(512, facts.BlockAlign);
        Assert.Equal(1017, facts.SamplesPerBlock);
        Assert.True(facts.IsCompressed);
    }

    [Fact]
    public void Video_container_lists_entries_with_the_size_the_next_offset_gives()
    {
        byte[] container = MediaFixture.VideoContainer(("first", new byte[16]), ("second", new byte[24]));
        VidArchive video = VidArchive.FromBytes("Might7.vid", container);

        Assert.Equal(2, video.Entries.Count);
        Assert.Equal(16, video.Entries[0].Size);
        Assert.Equal(24, video.Entries[1].Size);
        Assert.Equal(4 + (2 * 44), video.Entries[0].Offset);
        Assert.Equal(4 + (2 * 44) + 16, video.Entries[1].Offset);
    }

    [Fact]
    public void Extraction_emits_one_file_per_decoded_unit_and_records_the_boundaries()
    {
        using MediaFixture.Installation install = new();
        string output = install.NewOutput();

        MediaManifest manifest = MediaExtractor.Extract(install.Open(), output);

        Assert.Equal(MediaExtractor.DecoderVersion, manifest.DecoderVersion);
        Assert.Equal("GOG 1207658916 \"Might and Magic 7: For Blood and Honor\" version 1 (English)", manifest.Source.Release);
        Assert.StartsWith("MM7.exe sha256:", manifest.Source.Build, StringComparison.Ordinal);

        Dictionary<string, MediaArtifact> byId = manifest.Artifacts.ToDictionary(artifact => artifact.Id, StringComparer.Ordinal);
        Assert.Equal(2, manifest.Artifacts.Count(artifact => artifact.Kind == MediaKind.Bitmap));
        Assert.Equal(1, manifest.Artifacts.Count(artifact => artifact.Kind == MediaKind.Palette));
        Assert.Equal(2, manifest.Artifacts.Count(artifact => artifact.Kind == MediaKind.Sprite));
        Assert.Equal(2, manifest.Artifacts.Count(artifact => artifact.Kind == MediaKind.Pcx));
        Assert.Equal(1, manifest.Artifacts.Count(artifact => artifact.Kind == MediaKind.Font));
        Assert.Equal(2, manifest.Artifacts.Count(artifact => artifact.Kind == MediaKind.Sound));
        Assert.Equal(1, manifest.Artifacts.Count(artifact => artifact.Kind == MediaKind.Video));
        // NOISE.BIN is not media, CORRUPT.DAT does not inflate, and GAMES.LOD holds map data.
        Assert.Equal(3, manifest.Artifacts.Count(artifact => artifact.Kind == MediaKind.Other));

        MediaArtifact sprite = byId["mm7:SPRITES.LOD/ArrowA0"];
        Assert.Equal(MediaKind.Sprite, sprite.Kind);
        Assert.Equal(6, sprite.Width);
        Assert.Equal(4, sprite.Height);
        Assert.Equal(1, sprite.MipLevels);
        Assert.Equal(2, sprite.PaletteId);
        Assert.Equal(MediaPaletteSource.Named, sprite.PaletteSource);
        Assert.True(sprite.ZeroIsTransparent);
        Assert.Equal(1, sprite.EmptyBottomLines);
        Assert.Equal(0x20, sprite.Flags);
        Assert.NotNull(sprite.OutputPath);
        Assert.NotNull(sprite.EntrySha256);

        MediaArtifact orphan = byId["mm7:SPRITES.LOD/OrphanFrame"];
        Assert.Equal(MediaPaletteSource.Unresolved, orphan.PaletteSource);
        Assert.Contains("grey ramp", orphan.Notes!, StringComparison.Ordinal);

        MediaArtifact font = byId["mm7:ICONS.LOD/TINY.FNT"];
        Assert.Equal(MediaKind.Font, font.Kind);
        Assert.Null(font.OutputPath);
        Assert.Contains("glyph bytes", font.Notes!, StringComparison.Ordinal);
        Assert.Contains("no glyph atlas", font.ExcludedReason!, StringComparison.Ordinal);

        MediaArtifact payload = byId["mm7:ICONS.LOD/NOISE.BIN"];
        Assert.Equal(MediaKind.Other, payload.Kind);
        Assert.Null(payload.OutputPath);
        Assert.Contains("too few for any wrapper", payload.ExcludedReason!, StringComparison.Ordinal);

        MediaArtifact unreadable = byId["mm7:ICONS.LOD/CORRUPT.DAT"];
        Assert.Null(unreadable.OutputPath);
        Assert.Contains("could not decode", unreadable.ExcludedReason!, StringComparison.Ordinal);

        MediaArtifact movie = byId["mm7:Might7.vid/Intro"];
        Assert.Equal(MediaKind.Video, movie.Kind);
        Assert.Null(movie.OutputPath);
        Assert.Equal(40, movie.EntrySize);
        Assert.Contains("SMK2", movie.Notes!, StringComparison.Ordinal);

        MediaArtifact sound = byId["mm7:Audio.snd/02Flame01"];
        Assert.Equal(MediaKind.Sound, sound.Kind);
        Assert.NotNull(sound.Audio);
        Assert.Equal("adpcm_ima_wav", sound.Audio!.Value.Codec);
        Assert.Equal(22050, sound.Audio.Value.SampleRate);

        // Every emitted path is relative, forward-slashed, inside the output root, and hashes as the
        // manifest says.
        foreach (MediaArtifact artifact in manifest.Artifacts.Where(artifact => artifact.OutputPath is not null))
        {
            Assert.DoesNotContain('\\', artifact.OutputPath!);
            string full = Path.Combine(output, artifact.OutputPath!.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(full), artifact.OutputPath);
            byte[] bytes = File.ReadAllBytes(full);
            Assert.Equal(bytes.LongLength, artifact.OutputBytes);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), artifact.OutputSha256);
        }

        Assert.Equal(manifest.EmittedCount, manifest.Artifacts.Count(artifact => artifact.OutputPath is not null));
        Assert.Equal(manifest.EmittedBytes, manifest.Artifacts.Sum(artifact => artifact.OutputBytes ?? 0));
        Assert.Equal(5, manifest.Boundaries.Count);
        Assert.Equal(manifest.Artifacts.Count, manifest.Archives.Sum(archive => archive.Artifacts));
        Assert.Equal(manifest.EmittedCount, manifest.Archives.Sum(archive => archive.Emitted));
    }

    [Fact]
    public void Two_extraction_runs_over_one_installation_are_byte_identical()
    {
        using MediaFixture.Installation install = new();
        LodInstall opened = install.Open();
        string first = install.NewOutput();
        string second = install.NewOutput();

        MediaManifest firstManifest = MediaExtractor.Extract(opened, first);
        MediaManifest secondManifest = MediaExtractor.Extract(opened, second);

        Assert.Equal(
            File.ReadAllBytes(Path.Combine(first, MediaExtractor.ManifestFileName)),
            File.ReadAllBytes(Path.Combine(second, MediaExtractor.ManifestFileName)));
        Assert.Equal(firstManifest.Artifacts.Count, secondManifest.Artifacts.Count);

        string[] firstFiles = [.. Directory.EnumerateFiles(first, "*", SearchOption.AllDirectories).Select(path => Path.GetRelativePath(first, path)).Order(StringComparer.Ordinal)];
        string[] secondFiles = [.. Directory.EnumerateFiles(second, "*", SearchOption.AllDirectories).Select(path => Path.GetRelativePath(second, path)).Order(StringComparer.Ordinal)];
        Assert.Equal(firstFiles, secondFiles);
        Assert.NotEmpty(firstFiles);
        foreach (string relative in firstFiles)
        {
            Assert.Equal(File.ReadAllBytes(Path.Combine(first, relative)), File.ReadAllBytes(Path.Combine(second, relative)));
        }
    }

    [Fact]
    public void Entry_names_cannot_write_outside_the_output_root()
    {
        using MediaFixture.Installation install = new(hostileEntryNames: true);
        string output = install.NewOutput();

        MediaManifest manifest = MediaExtractor.Extract(install.Open(), output);

        MediaArtifact hostile = manifest.Artifacts.Single(artifact => artifact.EntryName == "../../escape");
        Assert.NotNull(hostile.OutputPath);
        Assert.DoesNotContain("..", hostile.OutputPath!, StringComparison.Ordinal);

        string root = Path.GetFullPath(output) + Path.DirectorySeparatorChar;
        foreach (MediaArtifact artifact in manifest.Artifacts.Where(artifact => artifact.OutputPath is not null))
        {
            string full = Path.GetFullPath(Path.Combine(output, artifact.OutputPath!.Replace('/', Path.DirectorySeparatorChar)));
            Assert.StartsWith(root, full, StringComparison.Ordinal);
        }

        Assert.False(File.Exists(Path.Combine(install.Root, "escape")));
    }

    [Fact]
    public void Colliding_output_names_are_disambiguated_deterministically()
    {
        using MediaFixture.Installation install = new();
        string output = install.NewOutput();

        MediaManifest manifest = MediaExtractor.Extract(install.Open(), output);

        // Border2 is an image and Border2.pcx is a nested PCX; both decode to Border2.png, which is a
        // collision the shipped icon archive really has.
        MediaArtifact first = manifest.Artifacts.Single(artifact => artifact.EntryName == "Border2");
        MediaArtifact second = manifest.Artifacts.Single(artifact => artifact.EntryName == "Border2.pcx");
        Assert.Equal("ICONS.LOD/Border2.png", first.OutputPath);
        Assert.NotNull(second.OutputPath);
        Assert.Contains("~", second.OutputPath!, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(output, second.OutputPath!.Replace('/', Path.DirectorySeparatorChar))));
    }

    private static LodPayload Payload(string name, byte[] entry)
    {
        LodArchive archive = LodArchive.FromBytes("synthetic.lod", LodFixture.Archive("MMVII", (name, entry)));
        return archive.Read(name);
    }
}

/// <summary>Builds the synthetic containers the media tests read.</summary>
internal static class MediaFixture
{
    private const int ImageHeaderSize = 48;
    private const int SpriteHeaderSize = 32;
    private const int PaletteSize = 0x300;

    /// <summary>Sequential 8-bit indices, so every pixel in a fixture is distinguishable.</summary>
    internal static byte[] Pixels(int width, int height, int seed)
    {
        byte[] pixels = new byte[width * height];
        for (int index = 0; index < pixels.Length; index++) pixels[index] = (byte)((index * 7) + seed);
        return pixels;
    }

    /// <summary>A palette whose bytes are a function of their position, so a mix-up is visible.</summary>
    internal static byte[] Palette(int seed)
    {
        byte[] palette = new byte[PaletteSize];
        for (int index = 0; index < palette.Length; index++) palette[index] = (byte)((index * 3) + seed);
        return palette;
    }

    internal static IndexedPalette PaletteAsObject(int seed) => IndexedPalette.FromBytes(Palette(seed));

    /// <summary>An image wrapper: geometry, an optional mip chain, and the 768-byte palette.</summary>
    internal static byte[] ImageWrapper(
        byte[] pixels,
        int width,
        int height,
        byte[] palette,
        int paletteId,
        uint flags,
        int mipLevels,
        bool deflatePixels = false)
    {
        List<byte> block = [.. pixels];
        int levelWidth = width / 2;
        int levelHeight = height / 2;
        for (int level = 1; level < mipLevels && levelWidth > 0 && levelHeight > 0; level++)
        {
            for (int index = 0; index < levelWidth * levelHeight; index++) block.Add((byte)(index + level));
            levelWidth /= 2;
            levelHeight /= 2;
        }

        byte[] payload = deflatePixels ? Deflate([.. block]) : [.. block];
        byte[] result = new byte[ImageHeaderSize + payload.Length + PaletteSize];
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0x10), (uint)(width * height));
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0x14), (uint)payload.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(0x18), (ushort)width);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(0x1A), (ushort)height);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(0x24), (ushort)paletteId);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0x28), deflatePixels ? (uint)block.Count : 0);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0x2C), flags);
        payload.CopyTo(result, ImageHeaderSize);
        palette.CopyTo(result, ImageHeaderSize + payload.Length);
        return result;
    }

    /// <summary>A palette-only entry: a zeroed header and 768 bytes.</summary>
    internal static byte[] PaletteEntry(byte[] palette)
    {
        byte[] result = new byte[ImageHeaderSize + PaletteSize];
        palette.CopyTo(result, ImageHeaderSize);
        return result;
    }

    /// <summary>
    /// A sprite wrapper. The line table is derived from the canvas the way the game stores one: each row
    /// carries the span from its first to its last non-zero pixel, and a row without one is written as
    /// -1/-1.
    /// </summary>
    internal static byte[] Sprite(byte[] canvas, int width, int height, ushort paletteId, ushort emptyBottomLines, ushort flags, bool deflated)
    {
        List<byte> pixelBlock = [];
        byte[] lines = new byte[height * 8];
        for (int y = 0; y < height; y++)
        {
            int begin = -1;
            int end = -1;
            for (int x = 0; x < width; x++)
            {
                if (canvas[(y * width) + x] == 0) continue;
                if (begin < 0) begin = x;
                end = x + 1;
            }

            int record = y * 8;
            if (begin < 0)
            {
                BinaryPrimitives.WriteInt16LittleEndian(lines.AsSpan(record), -1);
                BinaryPrimitives.WriteInt16LittleEndian(lines.AsSpan(record + 2), -1);
                continue;
            }

            BinaryPrimitives.WriteInt16LittleEndian(lines.AsSpan(record), (short)begin);
            BinaryPrimitives.WriteInt16LittleEndian(lines.AsSpan(record + 2), (short)end);
            BinaryPrimitives.WriteUInt32LittleEndian(lines.AsSpan(record + 4), (uint)pixelBlock.Count);
            for (int x = begin; x < end; x++) pixelBlock.Add(canvas[(y * width) + x]);
        }

        byte[] stored = deflated ? Deflate([.. pixelBlock]) : [.. pixelBlock];
        byte[] result = new byte[SpriteHeaderSize + lines.Length + stored.Length];
        Encoding.ASCII.GetBytes("SPRITE").CopyTo(result, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0x0C), (uint)stored.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(0x10), (ushort)width);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(0x12), (ushort)height);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(0x14), paletteId);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(0x18), emptyBottomLines);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(0x1A), flags);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0x1C), deflated ? (uint)pixelBlock.Count : 0);
        lines.CopyTo(result, SpriteHeaderSize);
        stored.CopyTo(result, SpriteHeaderSize + lines.Length);
        return result;
    }

    /// <summary>A three-plane 8-bit PCX file: 24-bit colour, one run-length encoded plane per channel.</summary>
    internal static byte[] Pcx24(int width, int height, byte[] rgb)
    {
        int bytesPerLine = width + (width & 1);
        List<byte> body = [];
        for (int y = 0; y < height; y++)
        {
            for (int plane = 0; plane < 3; plane++)
            {
                byte[] line = new byte[bytesPerLine];
                for (int x = 0; x < width; x++) line[x] = rgb[(((y * width) + x) * 3) + plane];
                body.AddRange(RunLengthEncode(line));
            }
        }

        return PcxHeader(width, height, planes: 3, bytesPerLine, [.. body], tailPalette: null);
    }

    /// <summary>A single-plane 8-bit PCX file, with or without the 256-colour tail palette.</summary>
    internal static byte[] Pcx8(int width, int height, byte[] indices, byte[]? tailPalette)
    {
        int bytesPerLine = width + (width & 1);
        List<byte> body = [];
        for (int y = 0; y < height; y++)
        {
            byte[] line = new byte[bytesPerLine];
            for (int x = 0; x < width; x++) line[x] = indices[(y * width) + x];
            body.AddRange(RunLengthEncode(line));
        }

        return PcxHeader(width, height, planes: 1, bytesPerLine, [.. body], tailPalette);
    }

    /// <summary>A non-image wrapper: the header with the text flag and the payload, and no palette.</summary>
    internal static byte[] NonImageWrapper(byte[] payload, bool deflated)
    {
        byte[] stored = deflated ? Deflate(payload) : payload;
        byte[] result = new byte[ImageHeaderSize + stored.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0x14), (uint)stored.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0x28), deflated ? (uint)payload.Length : 0);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0x2C), MediaDecoder.NotAnImageFlag);
        stored.CopyTo(result, ImageHeaderSize);
        return result;
    }

    /// <summary>A font: a 32-byte header, the 4096-byte MM7 atlas, then the packed glyphs.</summary>
    internal static byte[] Font(int firstChar, int lastChar, int height, int glyphWidth)
    {
        int glyphs = (lastChar - firstChar + 1) * height * glyphWidth;
        byte[] font = new byte[32 + 4096 + glyphs];
        font[0x00] = (byte)firstChar;
        font[0x01] = (byte)lastChar;
        font[0x02] = 8;
        font[0x05] = (byte)height;
        for (int character = firstChar; character <= lastChar; character++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(font.AsSpan(0x20 + (character * 12)), 0);
            BinaryPrimitives.WriteInt32LittleEndian(font.AsSpan(0x20 + (character * 12) + 4), glyphWidth);
            BinaryPrimitives.WriteInt32LittleEndian(font.AsSpan(0x20 + (character * 12) + 8), 0);
            BinaryPrimitives.WriteUInt32LittleEndian(font.AsSpan(0x20 + 3072 + (character * 4)), (uint)((character - firstChar) * height * glyphWidth));
        }

        return font;
    }

    /// <summary>A font in the narrow atlas layout: a u8 width per character and 256 glyph offsets.</summary>
    internal static byte[] NarrowFont(int firstChar, int lastChar, int height)
    {
        int glyphs = (lastChar - firstChar + 1) * height;
        byte[] font = new byte[32 + 1280 + glyphs];
        font[0x00] = (byte)firstChar;
        font[0x01] = (byte)lastChar;
        font[0x02] = 8;
        font[0x05] = (byte)height;
        for (int character = firstChar; character <= lastChar; character++)
        {
            font[0x20 + character] = 1;
            BinaryPrimitives.WriteUInt32LittleEndian(font.AsSpan(0x20 + 256 + (character * 4)), (uint)((character - firstChar) * height));
        }

        return font;
    }

    /// <summary>A RIFF/WAVE payload with an IMA ADPCM format chunk and a fact chunk.</summary>
    internal static byte[] Riff(int channels, int sampleRate, int blockAlign, int samples, int payloadBytes)
    {
        List<byte> file = [];
        AppendAscii(file, "RIFF");
        AppendUInt32(file, 0);
        AppendAscii(file, "WAVE");
        AppendAscii(file, "fmt ");
        AppendUInt32(file, 20);
        AppendUInt16(file, 17);
        AppendUInt16(file, (ushort)channels);
        AppendUInt32(file, (uint)sampleRate);
        AppendUInt32(file, (uint)(sampleRate * blockAlign / 512));
        AppendUInt16(file, (ushort)blockAlign);
        AppendUInt16(file, 4);
        AppendUInt16(file, 2);
        AppendUInt16(file, (ushort)samples);
        AppendAscii(file, "fact");
        AppendUInt32(file, 4);
        AppendUInt32(file, (uint)samples);
        AppendAscii(file, "data");
        AppendUInt32(file, (uint)payloadBytes);
        for (int index = 0; index < payloadBytes; index++) file.Add((byte)index);
        byte[] result = [.. file];
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(4), (uint)(result.Length - 8));
        return result;
    }

    /// <summary>A sound bank: a count, 52-byte entry records, then each payload behind its size word.</summary>
    internal static byte[] SoundBank(params (string Name, byte[] Payload, bool Deflated)[] samples)
    {
        int headerSize = 4 + (samples.Length * 52);
        byte[] entries = new byte[samples.Length * 52];
        List<byte> body = [];
        for (int index = 0; index < samples.Length; index++)
        {
            (string name, byte[] payload, bool deflated) = samples[index];
            byte[] stored = deflated ? Deflate(payload) : payload;
            int record = index * 52;
            Encoding.ASCII.GetBytes(name).CopyTo(entries, record);
            BinaryPrimitives.WriteUInt32LittleEndian(entries.AsSpan(record + 0x28), (uint)(headerSize + body.Count + 4));
            BinaryPrimitives.WriteUInt32LittleEndian(entries.AsSpan(record + 0x2C), (uint)stored.Length);
            // A sample stored as-is carries its stored size in the inflated-size field; that is how the
            // shipped bank marks the entry it did not deflate.
            BinaryPrimitives.WriteUInt32LittleEndian(entries.AsSpan(record + 0x30), deflated ? (uint)payload.Length : (uint)stored.Length);
            AppendUInt32(body, (uint)stored.Length);
            body.AddRange(stored);
        }

        byte[] result = new byte[headerSize + body.Count];
        BinaryPrimitives.WriteUInt32LittleEndian(result, (uint)samples.Length);
        entries.CopyTo(result, 4);
        byte[] payloads = [.. body];
        payloads.CopyTo(result, headerSize);
        return result;
    }

    /// <summary>A video container: a count, 44-byte records, then the movies back to back.</summary>
    internal static byte[] VideoContainer(params (string Name, byte[] Payload)[] movies)
    {
        int headerSize = 4 + (movies.Length * 44);
        byte[] entries = new byte[movies.Length * 44];
        List<byte> body = [];
        for (int index = 0; index < movies.Length; index++)
        {
            (string name, byte[] payload) = movies[index];
            int record = index * 44;
            Encoding.ASCII.GetBytes(name).CopyTo(entries, record);
            BinaryPrimitives.WriteUInt32LittleEndian(entries.AsSpan(record + 0x28), (uint)(headerSize + body.Count));
            body.AddRange(payload);
        }

        byte[] result = new byte[headerSize + body.Count];
        BinaryPrimitives.WriteUInt32LittleEndian(result, (uint)movies.Length);
        entries.CopyTo(result, 4);
        byte[] slices = [.. body];
        slices.CopyTo(result, headerSize);
        return result;
    }

    /// <summary>A whole installation, in a temporary directory that is removed with the fixture.</summary>
    internal sealed class Installation : IDisposable
    {
        private readonly List<string> _outputs = [];

        internal Installation(bool hostileEntryNames = false)
        {
            Root = Path.Combine(Path.GetTempPath(), $"mm7-media-fixture-{Guid.NewGuid():N}");
            string data = Path.Combine(Root, "DATA");
            Directory.CreateDirectory(data);

            byte[] palette = Palette(seed: 4);
            File.WriteAllBytes(Path.Combine(data, "BITMAPS.LOD"), LodFixture.Archive(
                "MMVI",
                ("A1b", ImageWrapper(Pixels(128, 16, seed: 2), 128, 16, palette, paletteId: 2, flags: 0, mipLevels: 4)),
                ("pal002", PaletteEntry(palette))));

            byte[] spriteCanvas = new byte[6 * 4];
            for (int x = 1; x < 5; x++) spriteCanvas[(2 * 6) + x] = (byte)(10 + x);
            File.WriteAllBytes(Path.Combine(data, "SPRITES.LOD"), LodFixture.Archive(
                "MMVI",
                ("ArrowA0", Sprite(spriteCanvas, 6, 4, paletteId: 2, emptyBottomLines: 1, flags: 0x20, deflated: true)),
                ("OrphanFrame", Sprite(spriteCanvas, 6, 4, paletteId: 940, emptyBottomLines: 0, flags: 0, deflated: false))));

            byte[] rgb = new byte[3 * 2 * 3];
            for (int index = 0; index < rgb.Length; index++) rgb[index] = (byte)(index * 5);
            List<(string Name, byte[] Payload)> icons =
            [
                ("Border2.pcx", NonImageWrapper(Pcx24(3, 2, rgb), deflated: true)),
                ("GryLite2.pcx", NonImageWrapper(Pcx8(4, 4, Pixels(4, 4, seed: 8), Palette(seed: 12)), deflated: true)),
                ("TINY.FNT", NonImageWrapper(Font(31, 255, height: 4, glyphWidth: 1), deflated: true)),
                ("NOISE.BIN", NonImageWrapper([9, 9, 9, 9, 9, 9, 9, 9], deflated: false)),
                ("CORRUPT.DAT", CorruptEntry()),
                ("Border2", ImageWrapper(Pixels(2, 2, seed: 3), 2, 2, palette, paletteId: 2, flags: 0, mipLevels: 1)),
            ];

            if (hostileEntryNames)
            {
                icons.Add(("../../escape", ImageWrapper(Pixels(2, 2, seed: 3), 2, 2, palette, paletteId: 2, flags: 0, mipLevels: 1)));
            }

            File.WriteAllBytes(Path.Combine(data, "ICONS.LOD"), LodFixture.Archive("MMVII", [.. icons]));
            File.WriteAllBytes(Path.Combine(data, "GAMES.LOD"), LodFixture.Archive("MMVII", ("7out13.odm", LodFixture.Compressed("map data"u8.ToArray()))));

            Directory.CreateDirectory(Path.Combine(Root, "SOUNDS"));
            File.WriteAllBytes(
                Path.Combine(Root, "SOUNDS", "Audio.snd"),
                SoundBank(
                    ("02Flame01", Riff(1, 22050, 512, 1017, 40), Deflated: true),
                    ("StartMain", Riff(1, 22050, 512, 1017, 32), Deflated: false)));

            Directory.CreateDirectory(Path.Combine(Root, "Anims"));
            File.WriteAllBytes(Path.Combine(Root, "Anims", "Might7.vid"), VideoContainer(("Intro", [.. "SMK2"u8.ToArray(), .. new byte[36]])));

            File.WriteAllBytes(Path.Combine(Root, "MM7.exe"), "not really the game"u8.ToArray());
            File.WriteAllText(
                Path.Combine(Root, "goggame-1207658916.info"),
                """{"gameId":"1207658916","name":"Might and Magic 7: For Blood and Honor","language":"English","version":1}""");
        }

        internal string Root { get; }

        internal LodInstall Open() => LodInstall.Open(Root);

        internal string NewOutput()
        {
            string output = Path.Combine(Path.GetTempPath(), $"mm7-media-out-{Guid.NewGuid():N}");
            _outputs.Add(output);
            return output;
        }

        public void Dispose()
        {
            foreach (string output in _outputs) Delete(output);
            Delete(Root);
        }

        /// <summary>A text entry whose deflate stream is broken, which the container reader refuses.</summary>
        private static byte[] CorruptEntry()
        {
            byte[] entry = LodFixture.DeflatedText("this will not inflate"u8.ToArray());
            entry[^1] ^= 0xFF;
            entry[^2] ^= 0xFF;
            entry[^3] ^= 0xFF;
            return entry;
        }

        private static void Delete(string directory)
        {
            try
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
                // A fixture directory the file system will not release is not a test failure.
            }
        }
    }

    private static byte[] PcxHeader(int width, int height, int planes, int bytesPerLine, byte[] body, byte[]? tailPalette)
    {
        int tail = tailPalette is null ? 0 : 1 + PaletteSize;
        byte[] result = new byte[128 + body.Length + tail];
        result[0] = 0x0A;
        result[1] = 5;
        result[2] = 1;
        result[3] = 8;
        BinaryPrimitives.WriteInt16LittleEndian(result.AsSpan(4), 0);
        BinaryPrimitives.WriteInt16LittleEndian(result.AsSpan(6), 0);
        BinaryPrimitives.WriteInt16LittleEndian(result.AsSpan(8), (short)(width - 1));
        BinaryPrimitives.WriteInt16LittleEndian(result.AsSpan(10), (short)(height - 1));
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(12), 72);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(14), 72);
        result[65] = (byte)planes;
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(66), (ushort)bytesPerLine);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(68), 1);
        body.CopyTo(result, 128);
        if (tailPalette is not null)
        {
            result[128 + body.Length] = 0x0C;
            tailPalette.CopyTo(result, 129 + body.Length);
        }

        return result;
    }

    private static byte[] RunLengthEncode(byte[] line)
    {
        List<byte> encoded = [];
        int at = 0;
        while (at < line.Length)
        {
            byte value = line[at];
            int run = 1;
            while (at + run < line.Length && line[at + run] == value && run < 63) run++;
            if (run > 1 || value >= 0xC0)
            {
                encoded.Add((byte)(0xC0 | run));
                encoded.Add(value);
            }
            else
            {
                encoded.Add(value);
            }

            at += run;
        }

        return [.. encoded];
    }

    private static byte[] Deflate(byte[] bytes)
    {
        using MemoryStream output = new();
        using (ZLibStream stream = new(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            stream.Write(bytes);
        }

        return output.ToArray();
    }

    private static void AppendAscii(List<byte> target, string text) => target.AddRange(Encoding.ASCII.GetBytes(text));

    private static void AppendUInt16(List<byte> target, ushort value)
    {
        target.Add((byte)(value & 0xFF));
        target.Add((byte)(value >> 8));
    }

    private static void AppendUInt32(List<byte> target, uint value)
    {
        target.Add((byte)(value & 0xFF));
        target.Add((byte)((value >> 8) & 0xFF));
        target.Add((byte)((value >> 16) & 0xFF));
        target.Add((byte)((value >> 24) & 0xFF));
    }
}

/// <summary>Just enough of a PNG reader to check what the writer produced.</summary>
internal sealed class Png
{
    private readonly byte[] _idat;

    private Png(int width, int height, int bitDepth, int colorType, string lastChunkType, byte[] idat)
    {
        Width = width;
        Height = height;
        BitDepth = bitDepth;
        ColorType = colorType;
        LastChunkType = lastChunkType;
        _idat = idat;
    }

    internal int Width { get; }

    internal int Height { get; }

    internal int BitDepth { get; }

    internal int ColorType { get; }

    internal string LastChunkType { get; }

    /// <summary>Walks the chunk list, checking every chunk's CRC and collecting the image data.</summary>
    internal static Png Parse(byte[] png)
    {
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, png[..8]);
        int at = 8;
        int width = 0;
        int height = 0;
        int bitDepth = 0;
        int colorType = 0;
        string last = string.Empty;
        List<byte> idat = [];
        while (at + 12 <= png.Length)
        {
            int length = (int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(at));
            string type = Encoding.ASCII.GetString(png, at + 4, 4);
            byte[] data = png[(at + 8)..(at + 8 + length)];
            uint stored = BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(at + 8 + length));
            Assert.Equal(Crc32Of(png.AsSpan(at + 4, 4 + length)), stored);

            switch (type)
            {
                case "IHDR":
                    width = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(0));
                    height = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(4));
                    bitDepth = data[8];
                    colorType = data[9];
                    break;
                case "IDAT":
                    idat.AddRange(data);
                    break;
            }

            last = type;
            at += 12 + length;
        }

        Assert.Equal(png.Length, at);
        return new Png(width, height, bitDepth, colorType, last, [.. idat]);
    }

    /// <summary>Inflates the image data and strips the per-scanline filter bytes.</summary>
    internal byte[] DecodeRgba()
    {
        using MemoryStream input = new(_idat);
        using ZLibStream stream = new(input, CompressionMode.Decompress);
        using MemoryStream output = new();
        stream.CopyTo(output);
        byte[] filtered = output.ToArray();

        int stride = Width * 4;
        Assert.Equal((stride + 1) * Height, filtered.Length);
        byte[] rgba = new byte[stride * Height];
        for (int y = 0; y < Height; y++)
        {
            Assert.Equal(0, filtered[y * (stride + 1)]);
            filtered.AsSpan((y * (stride + 1)) + 1, stride).CopyTo(rgba.AsSpan(y * stride));
        }

        return rgba;
    }

    private static uint Crc32Of(ReadOnlySpan<byte> bytes)
    {
        uint crc = 0xFFFFFFFFu;
        foreach (byte value in bytes)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++) crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
        }

        return crc ^ 0xFFFFFFFFu;
    }
}
