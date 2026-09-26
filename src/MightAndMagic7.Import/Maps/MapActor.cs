namespace MightAndMagic7.Import.Maps;

/// <summary>One actor a map's delta carries: a creature, or somebody standing in the world.</summary>
/// <remarks>
/// <para>
/// An actor record is the runtime object the original saves with a map, and the donor's own snapshot
/// structure is what fixes its width and field offsets (<c>Actor_MM7</c>, 0x344 bytes,
/// OpenEnroth <c>src/Engine/Snapshots/EntitySnapshots.h:764-809</c>). Most of the record is combat state
/// this importer has no use for — recovery, buffs, carried items, scheduled jobs — and the walk consumes
/// every one of those bytes without keeping them, because the record's width is what keeps the array
/// aligned.
/// </para>
/// <para>
/// <b>What is kept is what says who is here.</b> An actor whose <see cref="NpcId"/> is non-zero is one of
/// the game's people rather than a monster: the donor names such an actor through the NPC table rather than
/// the monster table (OpenEnroth <c>src/Engine/Objects/Actor.cpp:2354-2358</c>, <c>GetDisplayName</c>),
/// which is what makes a delta actor a person this product can place. The record's own name field is kept
/// beside the identity because the shipped data spells it differently from the table — a peasant actor is
/// named by its monster type there — so a report can show both.
/// </para>
/// <para>
/// The identity is the NPC table's own row number, kept exactly as the record states it. Whether that row
/// exists is not this reader's question: a record naming a row the table does not carry is reported by the
/// emitter that tried to resolve it, which is where the absence has a consequence.
/// </para>
/// </remarks>
/// <param name="Index">The actor's position in the delta's own array.</param>
/// <param name="Name">The name the record carries, empty on the shipped records that state none.</param>
/// <param name="NpcId">The NPC table row the actor is, or zero when the actor is a monster.</param>
/// <param name="MonsterId">
/// The monster row the actor is, read from the record's own embedded monster info, which is what a person
/// in a level is as far as a fight is concerned too; zero when the record states none.
/// </param>
/// <param name="HitPoints">The actor's current hit points, as the record stores them.</param>
/// <param name="Attributes">The actor's attribute bits, as the record stores them.</param>
/// <param name="Position">Where the actor stands, in the level's own coordinates.</param>
/// <param name="YawAngle">Which way the actor faces, in the game's own angle units.</param>
/// <param name="SectorId">The sector the record names, which is meaningful indoors and zero outdoors.</param>
/// <param name="Group">The actor's group, as the record stores it.</param>
/// <param name="UniqueNameIndex">
/// The unique-name index the record carries, which the donor reads from its placed-monster names before it
/// reads the NPC table; non-zero means the actor has a name of its own.
/// </param>
public sealed record MapActor(
    int Index,
    string Name,
    short NpcId,
    short MonsterId,
    int HitPoints,
    int Attributes,
    MapPoint Position,
    int YawAngle,
    int SectorId,
    int Group,
    int UniqueNameIndex)
{
    /// <summary>Whether the actor is somebody in the game's NPC table rather than a monster.</summary>
    public bool IsPerson => NpcId != 0;

    /// <summary>How the actor reads in a report: the person's identity, or the monster's.</summary>
    public string Describe() =>
        IsPerson
            ? $"person npc {NpcId}{(Name.Length > 0 ? $" ('{Name}')" : string.Empty)}"
            : $"monster {MonsterId}{(Name.Length > 0 ? $" ('{Name}')" : string.Empty)}";
}
