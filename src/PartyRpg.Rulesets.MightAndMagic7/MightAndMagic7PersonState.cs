using System.Globalization;
using PartyRpg.Kit.Party;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// What the game's own events have changed about a person — the house they live in, the greeting row they greet the
/// party with, and the items they were given to carry — as records the party carries: the one writer's names for them
/// and the one reading of each.
/// </summary>
/// <remarks>
/// <para>
/// <b>The donor keeps both on the person.</b> A <c>move-npc</c> step writes the house on the person's own record
/// (OpenEnroth <c>src/Engine/Evt/EvtInterpreter.cpp:470-471</c>), and a house lists the people whose record names it
/// (<c>src/GUI/UI/UIHouses.cpp:401</c>); a <c>set-npc-greeting</c> step writes the greeting row and
/// forgets that the person greeted the party already (<c>EvtInterpreter.cpp:541-545</c>), so the row's first line is
/// said next. The people here are content, the same for every party, so the change is the party's — as a changed topic
/// slot is (<see cref="MightAndMagic7TopicSlots"/>) — saved with the party's other records and judged on load. The
/// donor saves its people table per game, so the reading is the same for a one-party game.
/// </para>
/// <para>
/// <b>What a person carries.</b> A <c>npc-set-item</c> step gives an item to every creature standing for a person, or
/// takes it from them (OpenEnroth <c>src/Engine/Evt/EvtInterpreter.cpp:538-539</c>, <c>src/Engine/Objects/Actor.cpp:139-165</c>),
/// and what a person carries is what a thief lifts first (<c>src/Engine/Objects/Character.cpp:1254-1260</c>) and what
/// their body gives up (<c>src/Engine/Objects/Actor.cpp:3519-3529</c>): <see cref="MightAndMagic7Theft"/> and
/// <see cref="MightAndMagic7Corpses"/> read it here. <b>Ours</b>: the donor's person holds three such items at most and
/// starts with the one their map record names; the packs carry no record's item, so a person carries only what an event
/// gave them, a take of anything else changes nothing, and the three are not counted.
/// </para>
/// <para>
/// <b>Of a house and a greeting, only the latest change stands.</b> The writer takes every earlier change of the same kind for the person off the
/// record before it marks the new one, so a person always lives in one house and greets with one row. House zero is the
/// donor's "in no house"; a house no placement of the world holds is a person nobody can find, as the donor's is.
/// </para>
/// </remarks>
internal static class MightAndMagic7PersonState
{
    /// <summary>The prefix the record of a person's house carries.</summary>
    internal const string HousePrefix = "person-house:";

    /// <summary>The prefix the record of a person's greeting row carries.</summary>
    internal const string GreetingPrefix = "person-greeting:";

    /// <summary>The prefix the record of an item an event gave a person carries.</summary>
    internal const string ItemPrefix = "person-item:";

    /// <summary>The name of the record that says a person lives in a house.</summary>
    /// <param name="person">The person's identity in content.</param>
    /// <param name="house">The house, zero for none.</param>
    internal static string HouseRecord(string person, int house) =>
        string.Create(CultureInfo.InvariantCulture, $"{HousePrefix}{person}:{house}");

    /// <summary>The name of the record that says a person greets the party with a row of the greeting table.</summary>
    /// <param name="person">The person's identity in content.</param>
    /// <param name="row">The greeting table row, zero for none.</param>
    internal static string GreetingRecord(string person, int row) =>
        string.Create(CultureInfo.InvariantCulture, $"{GreetingPrefix}{person}:{row}");

    /// <summary>The house an event moved a person to, or null when nothing moved them.</summary>
    /// <param name="records">The party's records.</param>
    /// <param name="person">The person's identity in content.</param>
    internal static int? House(PartyRecords records, string person) => Read(records, HousePrefix, person);

    /// <summary>The greeting row an event gave a person, or null when nothing changed it.</summary>
    /// <param name="records">The party's records.</param>
    /// <param name="person">The person's identity in content.</param>
    internal static int? Greeting(PartyRecords records, string person) => Read(records, GreetingPrefix, person);

    /// <summary>Every person an event moved, with the house they live in now.</summary>
    /// <param name="records">The party's records.</param>
    internal static IEnumerable<(string Person, int House)> Moved(PartyRecords records)
    {
        ArgumentNullException.ThrowIfNull(records);
        foreach (PartyRecord record in records.All)
        {
            if (Parse(record.Name, HousePrefix) is { } moved) yield return moved;
        }
    }

    /// <summary>The items events gave a person to carry and nothing has taken since, in the order they were given.</summary>
    /// <param name="records">The party's records.</param>
    /// <param name="person">The person's identity in content.</param>
    internal static IReadOnlyList<int> Carried(PartyRecords records, string person)
    {
        ArgumentNullException.ThrowIfNull(records);
        List<int> items = [];
        foreach (PartyRecord record in records.All)
        {
            if (Parse(record.Name, ItemPrefix) is { } held && string.Equals(held.Person, person, StringComparison.Ordinal)) items.Add(held.Number);
        }

        return items;
    }

    /// <summary>Gives a person an item to carry, once.</summary>
    /// <param name="records">The party's records.</param>
    /// <param name="person">The person's identity in content.</param>
    /// <param name="item">The item's row.</param>
    internal static void Give(PartyRecords records, string person, int item)
    {
        ArgumentNullException.ThrowIfNull(records);
        records.Mark(string.Create(CultureInfo.InvariantCulture, $"{ItemPrefix}{person}:{item}"));
    }

    /// <summary>Takes an item from a person, when an event gave it to them; answers whether they carried it.</summary>
    /// <param name="records">The party's records.</param>
    /// <param name="person">The person's identity in content.</param>
    /// <param name="item">The item's row.</param>
    internal static bool Take(PartyRecords records, string person, int item)
    {
        ArgumentNullException.ThrowIfNull(records);
        return records.Remove(string.Create(CultureInfo.InvariantCulture, $"{ItemPrefix}{person}:{item}"));
    }

    /// <summary>Moves a person to a house, taking every earlier move of theirs off the record.</summary>
    /// <param name="records">The party's records.</param>
    /// <param name="person">The person's identity in content.</param>
    /// <param name="house">The house, zero for none.</param>
    internal static void Move(PartyRecords records, string person, int house) => Change(records, HousePrefix, person, house);

    /// <summary>
    /// Gives a person a greeting row, taking every earlier one off the record, and forgets that they have met the party,
    /// so the row's first line is what they say next.
    /// </summary>
    /// <param name="records">The party's records.</param>
    /// <param name="person">The person's identity in content.</param>
    /// <param name="row">The greeting table row, zero for none.</param>
    internal static void Greet(PartyRecords records, string person, int row)
    {
        Change(records, GreetingPrefix, person, row);
        records.Remove($"{MightAndMagic7Identities.MetFlagPrefix}{person}");
    }

    /// <summary>
    /// Why a record a save carries under one of these prefixes is not one the writer could have left, or null when it is
    /// or when it is not one of these records.
    /// </summary>
    /// <param name="name">The record's name.</param>
    /// <param name="count">How many times it is on record.</param>
    /// <param name="person">Whether the content carries a person of an identity.</param>
    /// <param name="greeting">Whether the content's greeting table carries a row.</param>
    internal static string? Judge(string name, int count, Func<string, bool> person, Func<int, bool> greeting)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(person);
        ArgumentNullException.ThrowIfNull(greeting);
        string? prefix = name.StartsWith(HousePrefix, StringComparison.Ordinal) ? HousePrefix
            : name.StartsWith(GreetingPrefix, StringComparison.Ordinal) ? GreetingPrefix
            : name.StartsWith(ItemPrefix, StringComparison.Ordinal) ? ItemPrefix
            : null;
        if (prefix is null) return null;
        if (Parse(name, prefix) is not { } changed) return $"a changed person is named '{prefix}<person>:<number>'";
        if (!person(changed.Person)) return $"the content carries no person '{changed.Person}'";
        if (prefix == ItemPrefix && changed.Number == 0) return "an item a person carries is a row of the item table, which zero is not";
        if (prefix == GreetingPrefix && changed.Number != 0 && !greeting(changed.Number))
        {
            return string.Create(CultureInfo.InvariantCulture, $"the content's greeting table has no row {changed.Number}");
        }

        return count == 1 ? null : "a person's change is held once, so the record is held once";
    }

    private static int? Read(PartyRecords records, string prefix, string person)
    {
        ArgumentNullException.ThrowIfNull(records);
        foreach (PartyRecord record in records.All)
        {
            if (Parse(record.Name, prefix) is { } changed && string.Equals(changed.Person, person, StringComparison.Ordinal)) return changed.Number;
        }

        return null;
    }

    private static void Change(PartyRecords records, string prefix, string person, int number)
    {
        ArgumentNullException.ThrowIfNull(records);
        foreach (PartyRecord earlier in records.All)
        {
            if (Parse(earlier.Name, prefix) is { } changed && string.Equals(changed.Person, person, StringComparison.Ordinal)) records.Remove(earlier.Name);
        }

        records.Mark(string.Create(CultureInfo.InvariantCulture, $"{prefix}{person}:{number}"));
    }

    private static (string Person, int Number)? Parse(string name, string prefix)
    {
        if (!name.StartsWith(prefix, StringComparison.Ordinal)) return null;
        string rest = name[prefix.Length..];
        int colon = rest.LastIndexOf(':');
        if (colon <= 0 || !int.TryParse(rest.AsSpan(colon + 1), NumberStyles.None, CultureInfo.InvariantCulture, out int number)) return null;
        return (rest[..colon], number);
    }
}
