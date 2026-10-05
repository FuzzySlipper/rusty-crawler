using PartyRpg.Kit.Content;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Sessions;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>Content-defined endings reached by the imported throne-room completion instructions.</summary>
/// <remarks>OpenEnroth src/GUI/UI/UIHouses.cpp:297-300 treats houses 600/601 as endings, not buildings.</remarks>
internal sealed class MightAndMagic7Endgame : ICompletionRule
{
    internal sealed record Ending(int House, string Record, string QuestRecord, IReadOnlyList<string> Requires, CompletionSnapshot Reading);
    private readonly IReadOnlyList<Ending> _endings;
    private MightAndMagic7Endgame(IReadOnlyList<Ending> endings) => _endings = endings;

    internal static MightAndMagic7Endgame Read(ContentCatalog? content)
    {
        List<Ending> endings = [];
        foreach (var (pack, document, entry) in content?.Entries("ending") ?? [])
        {
            int house = entry.GetInt32("house") ?? 0;
            string title = entry.GetString("title"), text = entry.GetString("text"), quest = entry.GetString("questRecord");
            string[] requires = entry.GetArray("requires").Select(value => value.ValueKind == System.Text.Json.JsonValueKind.String ? value.GetString() ?? "" : "").ToArray();
            if (house < 1 || title.Length == 0 || text.Length == 0 || quest.Length == 0 || requires.Length == 0 ||
                requires.Any(string.IsNullOrWhiteSpace) || endings.Any(e => e.House == house))
                throw new ContentValidationException("Ending content cannot be read.",
                    [new("ending-content-invalid", $"Ending '{entry.Id}' must identify its unique trigger, requirements and words; otherwise the session could announce the wrong ending.", pack.PackId, document.DocumentId)]);
            endings.Add(new(house, $"ending:{entry.Id}", quest, requires, new(entry.Id, title, text, true)));
        }
        return new(endings);
    }

    internal Ending? AtHouse(int house) => _endings.FirstOrDefault(ending => ending.House == house);
    public CompletionSnapshot Read(PartyEntity? party) =>
        _endings.FirstOrDefault(ending => party?.Records.Has(ending.Record) == true)?.Reading ?? CompletionSnapshot.None;

    internal string? JudgeRecord(string name, int count) => !name.StartsWith("ending:", StringComparison.Ordinal) ||
        (count == 1 && _endings.Any(ending => ending.Record == name)) ? null : "The saved ending is not one this bundle can report.";
}
