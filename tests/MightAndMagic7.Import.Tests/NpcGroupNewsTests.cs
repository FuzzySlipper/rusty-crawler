using System.Text.Json;
using MightAndMagic7.Import.Events;
using MightAndMagic7.Import.Lod;
using MightAndMagic7.Import.Tables;
using MightAndMagic7.Import.Tool;
using Xunit;

namespace MightAndMagic7.Import.Tests;

public sealed class NpcGroupNewsTests
{
    private sealed class OperatorFactAttribute : FactAttribute
    {
        public OperatorFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CRAWLER_OPERATOR_INSTALL")))
                Skip = "CRAWLER_OPERATOR_INSTALL is unset; the operator source is not committed.";
        }
    }

    [OperatorFact]
    public void Operator_event_arguments_name_actual_groups_and_news_rows()
    {
        var install = LodInstall.Open(Environment.GetEnvironmentVariable("CRAWLER_OPERATOR_INSTALL")!);
        var tables = NpcNewsTable.Read(install);
        Assert.Equal(51, tables.Groups.Count);
        Assert.Equal(51, tables.Texts.Count);
        var instructions = EvtProgram.ReadAll(install).SelectMany(program => program.Instructions
            .Where(step => step.Opcode == EvtOpcodes.SetNpcGroupNews).Select(step => (program.Name, Step: step))).ToArray();
        Assert.NotEmpty(instructions);
        foreach (var (_, step) in instructions)
        {
            Assert.True(step.TryReadNpcGroupNews(out uint group, out uint news));
            Assert.Contains((int)group, tables.Groups.Keys);
            Assert.Contains((int)news, tables.Texts.Keys);
        }
        var starting = Assert.Single(instructions, row => string.Equals(row.Name, "global.evt", StringComparison.OrdinalIgnoreCase) && row.Step.EventId == 3 && row.Step.Step == 12).Step;
        Assert.True(starting.TryReadNpcGroupNews(out uint firstGroup, out uint firstNews));
        Assert.Equal((1u, 5u), (firstGroup, firstNews));
    }

    [Fact]
    public void Both_unsigned_operands_are_preserved_and_short_operands_are_not_invented()
    {
        byte[] operands = [.. BitConverter.GetBytes(uint.MaxValue), .. BitConverter.GetBytes(0x80000000u)];
        var instruction = Assert.Single(EvtProgram.Read("news.evt", [12, 1, 0, 0, 47, .. operands]).Instructions);
        Assert.True(instruction.TryReadNpcGroupNews(out uint group, out uint news));
        Assert.Equal(uint.MaxValue, group);
        Assert.Equal(0x80000000u, news);
        Assert.False(new EvtInstruction(1, 0, 47, operands.AsMemory(0, 7)).TryReadNpcGroupNews(out _, out _));
    }

    [Fact]
    public void Pack_emits_real_group_assignments_and_news_text_as_separate_definitions()
    {
        string install = SyntheticInstallation.Create(globalProgram: [12, 1, 0, 0, 47, 1, 0, 0, 0, 2, 0, 0, 0]);
        string root = Path.Combine(Path.GetTempPath(), $"mm7-news-{Guid.NewGuid():N}");
        try
        {
            var tables = NpcNewsTable.Read(LodInstall.Open(install));
            Assert.Equal(1, tables.Groups[1]);
            Assert.Equal("New news", tables.Texts[2]);
            PackWriter.Write(LodInstall.Open(install), root, PackWriter.MapDetail.None);
            using var groups = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "mm7-tables", "npc-groups.json")));
            using var news = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "mm7-tables", "npc-news.json")));
            using var events = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "mm7-tables", "global-events.json")));
            var step = events.RootElement.GetProperty("entries")[0].GetProperty("steps")[0];
            Assert.Equal(1u, step.GetProperty("newsGroup").GetUInt32());
            Assert.Equal(2u, step.GetProperty("news").GetUInt32());
            Assert.Equal(1, groups.RootElement.GetProperty("entries")[1].GetProperty("news").GetInt32());
            Assert.Equal("New news", news.RootElement.GetProperty("entries")[2].GetProperty("text").GetString());
        }
        finally
        {
            Directory.Delete(install, true);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
