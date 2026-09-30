using System.Buffers.Binary;
using System.Text;
using MightAndMagic7.Import.Lod;
using MightAndMagic7.Import.Maps;
using Xunit;

namespace MightAndMagic7.Import.Tests;

/// <summary>The container reader, over constructed archives.</summary>
public sealed class LodArchiveTests
{
    [Fact]
    public void A_text_table_is_read_as_Western_text_and_an_undefined_byte_is_named()
    {
        // A curly apostrophe and an accented letter as the English release writes them, one byte each.
        LodPayload table = new(new LodEntry("npcnews.txt", 0, 6), [0x4F, 0x92, 0x43, 0x6C, 0xE9, 0x72], LodPayloadKind.Verbatim);
        Assert.Equal("O\u2019Cl\u00e9r", table.AsText());

        // A table that says it is UTF-8 is read as UTF-8.
        LodPayload marked = new(new LodEntry("marked.txt", 0, 5), [0xEF, 0xBB, 0xBF, 0xC3, 0xA9], LodPayloadKind.Verbatim);
        Assert.Equal("\u00e9", marked.AsText());

        // A byte the code page does not define is refused with the entry and where it stands.
        LodPayload broken = new(new LodEntry("broken.txt", 0, 3), [0x41, 0x81, 0x42], LodPayloadKind.Verbatim);
        LodFormatException refused = Assert.Throws<LodFormatException>(() => broken.AsText());
        Assert.Equal(LodFault.Value, refused.Fault);
        Assert.Contains("broken.txt", refused.Message, StringComparison.Ordinal);
        Assert.Contains("0x81", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Reads_the_header_and_lists_entries_by_name()
    {
        LodArchive archive = LodArchive.FromBytes(
            "fixture.lod",
            LodFixture.Archive("MMVI", ("beta.txt", LodFixture.Verbatim("b"u8.ToArray())), ("alpha.txt", LodFixture.Verbatim("a"u8.ToArray()))));

        Assert.Equal(LodVersion.Mm6, archive.Version);
        Assert.Equal("MMVI", archive.VersionString);
        Assert.Equal(32, archive.FileEntrySize);
        Assert.Equal(["alpha.txt", "beta.txt"], archive.Entries.Select(entry => entry.Name));
        Assert.Equal("a"u8.ToArray(), archive.Read("ALPHA.TXT").Bytes);
    }

    [Fact]
    public void The_version_field_is_not_a_game_identity()
    {
        // Every archive in the seventh game's installation spells its version field MMVI; nothing may
        // pick a rule table by it.
        LodArchive archive = LodArchive.FromBytes("Events.lod", LodFixture.Archive("MMVI", ("A.txt", LodFixture.Verbatim("a"u8.ToArray()))));
        Assert.Equal(LodVersion.Mm6, archive.Version);
        Assert.Equal(LodVersion.Unknown, LodArchive.FromBytes("x.lod", LodFixture.Archive("MMIX")).Version);
    }

    [Fact]
    public void Decodes_both_payload_wrappers_and_verbatim_storage()
    {
        byte[] text = Encoding.Latin1.GetBytes("row\tvalue\n1\t2\n");
        LodArchive archive = LodArchive.FromBytes(
            "fixture.lod",
            LodFixture.Archive(
                "MMVI",
                ("plain.txt", LodFixture.Verbatim("plain"u8.ToArray())),
                ("deflated.txt", LodFixture.Compressed(text)),
                ("stored.txt", LodFixture.CompressedStored(text)),
                ("table.txt", LodFixture.DeflatedText(text)),
                ("image.bmp", LodFixture.Image(new byte[16])),
                ("palette.pal", LodFixture.Palette())));

        Assert.Equal(LodPayloadKind.Verbatim, archive.Read("plain.txt").Kind);
        Assert.Equal(LodPayloadKind.Compressed, archive.Read("deflated.txt").Kind);
        Assert.Equal("plain", Encoding.Latin1.GetString(archive.Read("plain.txt").Bytes));
        Assert.Equal(text, archive.Read("deflated.txt").Bytes);
        Assert.Equal(LodPayloadKind.CompressedStored, archive.Read("stored.txt").Kind);
        Assert.Equal(text, archive.Read("stored.txt").Bytes);
        Assert.Equal(LodPayloadKind.DeflatedText, archive.Read("table.txt").Kind);
        Assert.Equal(text, archive.Read("table.txt").Bytes);
        LodPayload image = archive.Read("image.bmp");
        Assert.Equal(LodPayloadKind.Image, image.Kind);
        Assert.Equal(16, image.Bytes.Length);
        Assert.Equal(0x300, image.Palette!.Length);
        Assert.Equal(LodPayloadKind.Palette, archive.Read("palette.pal").Kind);
        Assert.Equal(0x300, archive.Read("palette.pal").Palette!.Length);
        Assert.True(archive.IsCompressed(archive.Require("deflated.txt")));
        Assert.False(archive.IsCompressed(archive.Require("plain.txt")));
        Assert.True(archive.IsCompressed(archive.Require("image.bmp")) is false);
    }

    [Fact]
    public void A_deflated_map_and_its_delta_are_inflated_and_decode_as_the_stored_pair_does()
    {
        // A map is the largest thing the compressed wrapper carries and the one a decoder walks field by
        // field, so the inflated bytes have to be the payload exactly: one byte short or long fails the walk.
        byte[] level = MapDecoderTests.IndoorPayload(faceCorners: 6, lightCount: 3, doorSlots: 3);
        byte[] delta = MapDecoderTests.IndoorDeltaPayload(doorSlots: 3);
        LodArchive archive = LodArchive.FromBytes(
            "Games.lod",
            LodFixture.Archive(
                "GameMMVI",
                ("d07.blv", LodFixture.Compressed(level)),
                ("d07.dlv", LodFixture.Compressed(delta)),
                ("d08.blv", LodFixture.CompressedStored(level)),
                ("d08.dlv", LodFixture.CompressedStored(delta))));

        LodPayload deflatedLevel = archive.Read("d07.blv");
        LodPayload deflatedDelta = archive.Read("d07.dlv");
        Assert.Equal(LodPayloadKind.Compressed, deflatedLevel.Kind);
        Assert.Equal(LodPayloadKind.Compressed, deflatedDelta.Kind);
        Assert.True(archive.IsCompressed(archive.Require("d07.blv")));
        Assert.True(archive.Require("d07.blv").Size < LodFixture.CompressionHeaderSize + level.Length);
        Assert.Equal(level, deflatedLevel.Bytes);
        Assert.Equal(delta, deflatedDelta.Bytes);
        Assert.Equal(LodPayloadKind.CompressedStored, archive.Read("d08.blv").Kind);

        IndoorMap inflated = MapDecoder.DecodeIndoor(deflatedLevel, deflatedDelta);
        IndoorMap stored = MapDecoder.DecodeIndoor(archive.Read("d08.blv"), archive.Read("d08.dlv"));
        Assert.Equal(6, Assert.Single(inflated.Faces).VertexIds.Count);
        Assert.Equal(3, inflated.Lights.Count);
        Assert.Equal(3, inflated.Doors.Count);
        Assert.Equal(stored.Counts, inflated.Counts);
        Assert.Equal(stored.Vertices, inflated.Vertices);
        Assert.Equal(stored.Faces[0].Vertices, inflated.Faces[0].Vertices);
    }

    [Fact]
    public void A_deflated_payload_that_does_not_inflate_to_its_declared_size_is_a_compression_failure()
    {
        byte[] level = MapDecoderTests.IndoorPayload();
        byte[] wrongSize = LodFixture.Compressed(level);
        BinaryPrimitives.WriteUInt32LittleEndian(wrongSize.AsSpan(12), (uint)(level.Length + 1));
        byte[] corrupt = LodFixture.Compressed(level);
        corrupt.AsSpan(LodFixture.CompressionHeaderSize, 8).Fill(0xFF);
        LodArchive archive = LodArchive.FromBytes(
            "Games.lod",
            LodFixture.Archive("GameMMVI", ("short.blv", wrongSize), ("corrupt.blv", corrupt)));

        LodFormatException mismatched = Assert.Throws<LodFormatException>(() => archive.Read("short.blv"));
        Assert.Equal(LodFault.Compression, mismatched.Fault);
        Assert.Contains("short.blv", mismatched.Message, StringComparison.Ordinal);
        Assert.Equal(LodFault.Compression, Assert.Throws<LodFormatException>(() => archive.Read("corrupt.blv")).Fault);
    }

    [Fact]
    public void An_archive_declaring_the_eighth_games_version_reads_its_76_byte_directory_records()
    {
        // Three entries, so a reader striding the directory at the narrower record width would read the
        // second and third names out of the first record's unused words; the unused words themselves are
        // filled, so an offset or a size read from the narrower record's positions would be garbage.
        byte[] text = Encoding.Latin1.GetBytes("row\tvalue\n1\t2\n");
        LodArchive archive = LodArchive.FromBytes(
            "wide.lod",
            LodFixture.Mm8Archive(
            [
                ("gamma.txt", LodFixture.Verbatim("third"u8.ToArray())),
                ("alpha.txt", LodFixture.Compressed(text)),
                ("beta.txt", LodFixture.Verbatim("second payload"u8.ToArray())),
            ]));

        Assert.Equal(LodVersion.Mm8, archive.Version);
        Assert.Equal("MMVIII", archive.VersionString);
        Assert.Equal(LodArchive.Mm8FileEntrySize, archive.FileEntrySize);
        Assert.Empty(archive.DuplicateEntryNames);
        Assert.Equal(["alpha.txt", "beta.txt", "gamma.txt"], archive.Entries.Select(entry => entry.Name));
        Assert.Equal(LodFixture.Compressed(text).Length, archive.Require("alpha.txt").Size);
        Assert.Equal(14, archive.Require("beta.txt").Size);
        Assert.Equal(text, archive.Read("alpha.txt").Bytes);
        Assert.Equal(LodPayloadKind.Compressed, archive.Read("alpha.txt").Kind);
        Assert.Equal("second payload", Encoding.Latin1.GetString(archive.Read("beta.txt").Bytes));
        Assert.Equal("third", Encoding.Latin1.GetString(archive.Read("gamma.txt").Bytes));
    }

    [Fact]
    public void A_wide_directory_too_short_for_its_declared_records_is_refused_by_the_width_it_needs()
    {
        // The root entry declares room for three records of the narrower width only: enough for a reader
        // that assumed 32 bytes, too little for the 76 this version's records take.
        byte[] archive = LodFixture.Mm8Archive(
            [
                ("a.txt", LodFixture.Verbatim("a"u8.ToArray())),
                ("b.txt", LodFixture.Verbatim("b"u8.ToArray())),
                ("c.txt", LodFixture.Verbatim("c"u8.ToArray())),
            ],
            declaredDirectorySize: 3 * LodFixture.EntrySize);

        LodFormatException refused = Assert.Throws<LodFormatException>(() => LodArchive.FromBytes("wide.lod", archive));
        Assert.Equal(LodFault.Truncated, refused.Fault);
        Assert.Contains("wide.lod", refused.Message, StringComparison.Ordinal);
        Assert.Contains("3 entries of 76 bytes", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_wide_record_whose_size_runs_past_the_file_is_refused_when_it_is_read()
    {
        byte[] bytes = LodFixture.Mm8Archive([("a.txt", LodFixture.Verbatim("abc"u8.ToArray()))]);
        int record = LodFixture.HeaderSize + LodFixture.RootEntrySize;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(record + 68), 4096);
        LodArchive archive = LodArchive.FromBytes("wide.lod", bytes);

        Assert.Equal(4096, archive.Require("a.txt").Size);
        LodFormatException refused = Assert.Throws<LodFormatException>(() => archive.Read("a.txt"));
        Assert.Equal(LodFault.Truncated, refused.Fault);
        Assert.Contains("'a.txt'", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Tolerates_the_original_writer_size_bug()
    {
        byte[] text = Encoding.Latin1.GetBytes("payload");
        LodArchive archive = LodArchive.FromBytes("fixture.lod", LodFixture.Archive("MMVI", ("bug.txt", LodFixture.Compressed(text, setWriterBug: true))));

        Assert.Equal(text, archive.Read("bug.txt").Bytes);
    }

    [Fact]
    public void Keeps_the_first_of_duplicate_entry_names_and_records_the_rest()
    {
        LodArchive archive = LodArchive.FromBytes(
            "fixture.lod",
            LodFixture.Archive(
                "MMVI",
                ("dup.txt", LodFixture.Verbatim("first"u8.ToArray())),
                ("DUP.txt", LodFixture.Verbatim("second"u8.ToArray()))));

        Assert.Single(archive.Entries);
        Assert.Equal(["DUP.txt"], archive.DuplicateEntryNames);
        Assert.Equal("first", Encoding.Latin1.GetString(archive.Read("dup.txt").Bytes));
    }

    [Fact]
    public void Rejects_files_that_are_not_containers()
    {
        byte[] tooSmall = new byte[16];
        Assert.Equal(LodFault.Truncated, Assert.Throws<LodFormatException>(() => LodArchive.FromBytes("x.lod", tooSmall)).Fault);

        byte[] wrongSignature = LodFixture.Archive("MMVI", ("a.txt", LodFixture.Verbatim("a"u8.ToArray())));
        "BAD\0"u8.CopyTo(wrongSignature);
        Assert.Equal(LodFault.Signature, Assert.Throws<LodFormatException>(() => LodArchive.FromBytes("x.lod", wrongSignature)).Fault);

        LodArchive archive = LodArchive.FromBytes("x.lod", LodFixture.Archive("MMVI"));
        Assert.Equal(LodFault.Missing, Assert.Throws<LodFormatException>(() => archive.Read("missing.txt")).Fault);
    }

    [Fact]
    public void An_installation_finds_its_archives_and_reports_a_table_that_lives_in_two_of_them()
    {
        string root = Path.Combine(Path.GetTempPath(), $"mm7-install-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "DATA"));
        try
        {
            File.WriteAllBytes(
                Path.Combine(root, "DATA", "Events.lod"),
                LodFixture.Archive("MMVI", LodFixture.TextTable("MAPSTATS.TXT", "new\n")));
            File.WriteAllBytes(
                Path.Combine(root, "DATA", "Icons.LOD"),
                LodFixture.Archive("MMVI", LodFixture.TextTable("MAPSTATS.TXT", "old\n")));

            LodInstall install = LodInstall.Open(root);
            Assert.Equal(["Events.lod", "Icons.LOD"], install.ArchiveNames());
            Assert.Equal(["Events.lod", "Icons.LOD"], install.ArchivesContaining("mapstats.txt"));

            // A declared source reads its own archive and never falls back to the other one.
            LodPayload payload = install.Read(new LodSource("map-stats", "Events.lod", "MapStats.txt"));
            Assert.Equal("new\n", payload.AsText());
            Assert.Equal(
                LodFault.Missing,
                Assert.Throws<LodFormatException>(() => install.Read(new LodSource("missing", "Icons.LOD", "Absent.txt"))).Fault);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void An_installation_that_is_not_one_is_refused_by_name()
    {
        string root = Path.Combine(Path.GetTempPath(), $"mm7-not-install-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            Assert.Equal(LodFault.Missing, Assert.Throws<LodFormatException>(() => LodInstall.Open(root)).Fault);
            Assert.Equal(LodFault.Missing, Assert.Throws<LodFormatException>(() => LodInstall.Open(Path.Combine(root, "absent"))).Fault);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
