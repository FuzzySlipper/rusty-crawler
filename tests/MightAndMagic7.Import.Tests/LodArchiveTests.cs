using System.Text;
using MightAndMagic7.Import.Lod;
using Xunit;

namespace MightAndMagic7.Import.Tests;

/// <summary>The container reader, over constructed archives.</summary>
public sealed class LodArchiveTests
{
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
        Assert.Contains("too small", Assert.Throws<LodFormatException>(() => LodArchive.FromBytes("x.lod", tooSmall)).Message);

        byte[] wrongSignature = LodFixture.Archive("MMVI", ("a.txt", LodFixture.Verbatim("a"u8.ToArray())));
        "BAD\0"u8.CopyTo(wrongSignature);
        Assert.Contains("signature", Assert.Throws<LodFormatException>(() => LodArchive.FromBytes("x.lod", wrongSignature)).Message);

        LodArchive archive = LodArchive.FromBytes("x.lod", LodFixture.Archive("MMVI"));
        Assert.Contains("no entry named", Assert.Throws<LodFormatException>(() => archive.Read("missing.txt")).Message);
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
            Assert.Contains(
                "no entry named",
                Assert.Throws<LodFormatException>(() => install.Read(new LodSource("missing", "Icons.LOD", "Absent.txt"))).Message);
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
            Assert.Contains("DATA directory", Assert.Throws<LodFormatException>(() => LodInstall.Open(root)).Message);
            Assert.Contains("not a directory", Assert.Throws<LodFormatException>(() => LodInstall.Open(Path.Combine(root, "absent"))).Message);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
