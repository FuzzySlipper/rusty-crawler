using System.Globalization;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Party;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>Content assignments read through the party's one saved record owner.</summary>
/// <remarks>Unnamed actors read their group's news; named people keep their greetings
/// (OpenEnroth src/Engine/Graphics/Viewport.cpp:186-200).</remarks>
internal sealed class MightAndMagic7GroupNews
{
    private const string Prefix = "person-group-news:";
    private readonly Dictionary<uint, uint> _groups = [];
    private readonly Dictionary<uint, string> _texts = [];

    internal static MightAndMagic7GroupNews Read(ContentCatalog? catalog)
    {
        MightAndMagic7GroupNews result = new();
        if (catalog is null) return result;
        foreach (var (_, _, entry) in catalog.Entries("person-news"))
            result._texts.Add(uint.Parse(entry.Id, CultureInfo.InvariantCulture), entry.GetString("text"));
        foreach (var (_, _, entry) in catalog.Entries("person-group"))
        {
            uint group = uint.Parse(entry.Id, CultureInfo.InvariantCulture);
            uint news = checked((uint)(entry.GetInt32("news") ?? -1));
            if (news != 0 && !result._texts.ContainsKey(news))
                throw new InvalidDataException($"Actor group {group} names missing news {news}; its conversation would be silently wrong.");
            result._groups.Add(group, news);
        }
        return result;
    }

    internal bool Contains(uint group, uint news) => _groups.ContainsKey(group) && (news == 0 || _texts.ContainsKey(news));

    internal string Text(PartyRecords? records, uint group)
    {
        if (!_groups.TryGetValue(group, out uint news)) return string.Empty;
        if (records is not null)
            foreach (var record in records.All)
                if (Parse(record.Name) is { } changed && changed.Group == group) news = changed.News;
        return news == 0 ? string.Empty : _texts.GetValueOrDefault(news, string.Empty);
    }

    internal static void Change(PartyRecords records, uint group, uint news)
    {
        foreach (var record in records.All)
            if (Parse(record.Name) is { } changed && changed.Group == group) records.Remove(record.Name);
        records.Mark(string.Create(CultureInfo.InvariantCulture, $"{Prefix}{group}:{news}"));
    }

    internal string? JudgeRecord(string name, int count)
    {
        if (!name.StartsWith(Prefix, StringComparison.Ordinal)) return null;
        return Parse(name) is { } changed && Contains(changed.Group, changed.News) && count == 1
            ? null : "group news must name an existing actor group and news row, held once";
    }

    private static (uint Group, uint News)? Parse(string name)
    {
        if (!name.StartsWith(Prefix, StringComparison.Ordinal)) return null;
        string[] fields = name[Prefix.Length..].Split(':');
        return fields.Length == 2 && uint.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out uint group)
            && uint.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out uint news) ? (group, news) : null;
    }
}
