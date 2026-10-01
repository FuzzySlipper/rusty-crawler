using System.Globalization;
using PartyRpg.Kit.Party;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// Which topic one of a person's slots raises after a map event changed it: the record the party carries for
/// the change, the one writer's name for it and the conversation's one reading of it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The donor keeps it on the person.</b> A person's six dialogue slots each raise one row of the topic table,
/// and a map event's <c>set-npc-topic</c> step replaces the row one slot raises, on the person's own record,
/// for the rest of the game (OpenEnroth <c>src/Engine/Evt/EvtInterpreter.cpp:449-468</c>; the slot's label is
/// that row's, <c>src/GUI/UI/NPCTopics.cpp:546-559</c>). The people here are content, the same for every party,
/// so the change is the party's: a record named by the person, the slot and the row it now raises, which is saved
/// with the party's other records and judged on load like them (<see cref="Judge"/>). The donor's people table is
/// saved per game as well, so the reading is the same.
/// </para>
/// <para>
/// <b>Only the latest change stands.</b> The writer takes every earlier change of the same slot off the record
/// before it marks the new one, so a slot always reads as one row; a change to row zero is a slot that raises
/// nothing, which the donor's own empty slot is.
/// </para>
/// </remarks>
internal static class MightAndMagic7TopicSlots
{
    /// <summary>The prefix every such record is named under.</summary>
    internal const string Prefix = "topic-slot:";

    /// <summary>How many dialogue slots a person has (OpenEnroth <c>src/Engine/Evt/EvtInterpreter.cpp:452-457</c>).</summary>
    internal const int Slots = 6;

    /// <summary>The name of the record that says a person's slot raises a row.</summary>
    /// <param name="person">The person's identity in content.</param>
    /// <param name="slot">The slot, from zero.</param>
    /// <param name="raises">The topic table row it raises, zero for none.</param>
    internal static string Record(string person, int slot, int raises) =>
        string.Create(CultureInfo.InvariantCulture, $"{SlotPrefix(person, slot)}{raises}");

    /// <summary>The part of the name every record of one slot shares.</summary>
    private static string SlotPrefix(string person, int slot) =>
        string.Create(CultureInfo.InvariantCulture, $"{Prefix}{person}.{slot}:");

    /// <summary>The row a person's slot was changed to raise, or null when nothing changed it.</summary>
    /// <param name="records">The party's records.</param>
    /// <param name="person">The person's identity in content.</param>
    /// <param name="slot">The slot, from zero.</param>
    internal static int? Raised(PartyRecords records, string person, int slot)
    {
        ArgumentNullException.ThrowIfNull(records);
        string prefix = SlotPrefix(person, slot);
        foreach (PartyRecord record in records.All)
        {
            if (!record.Name.StartsWith(prefix, StringComparison.Ordinal)) continue;
            if (int.TryParse(record.Name.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int raises)) return raises;
        }

        return null;
    }

    /// <summary>Changes the row a person's slot raises, taking every earlier change of that slot off the record.</summary>
    /// <param name="records">The party's records.</param>
    /// <param name="person">The person's identity in content.</param>
    /// <param name="slot">The slot, from zero.</param>
    /// <param name="raises">The row it raises from now on, zero for none.</param>
    internal static void Change(PartyRecords records, string person, int slot, int raises)
    {
        ArgumentNullException.ThrowIfNull(records);
        string prefix = SlotPrefix(person, slot);
        foreach (PartyRecord earlier in records.All)
        {
            if (earlier.Name.StartsWith(prefix, StringComparison.Ordinal)) records.Remove(earlier.Name);
        }

        records.Mark(Record(person, slot, raises));
    }

    /// <summary>
    /// Why a record a save carries under this prefix is not one the writer could have left, or null when it is or
    /// when it is not one of these records.
    /// </summary>
    /// <param name="name">The record's name.</param>
    /// <param name="count">How many times it is on record.</param>
    /// <param name="person">Whether the content carries a person of an identity.</param>
    internal static string? Judge(string name, int count, Func<string, bool> person)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(person);
        if (!name.StartsWith(Prefix, StringComparison.Ordinal)) return null;
        string rest = name[Prefix.Length..];
        int colon = rest.LastIndexOf(':');
        int dot = colon < 0 ? -1 : rest.LastIndexOf('.', colon);
        if (dot <= 0 ||
            !int.TryParse(rest.AsSpan(dot + 1, colon - dot - 1), NumberStyles.None, CultureInfo.InvariantCulture, out int slot) ||
            !int.TryParse(rest.AsSpan(colon + 1), NumberStyles.None, CultureInfo.InvariantCulture, out _))
        {
            return $"a changed topic slot is named '{Prefix}<person>.<slot>:<row>'";
        }

        if (slot >= Slots) return string.Create(CultureInfo.InvariantCulture, $"a person has topic slots 0 to {Slots - 1} only");
        if (!person(rest[..dot])) return $"the content carries no person '{rest[..dot]}'";
        return count == 1 ? null : "a slot is changed once, so the record is held once";
    }
}
