namespace MightAndMagic7.Import.Maps;

/// <summary>A place where the level spawns monsters or treasure.</summary>
/// <remarks>
/// Spawn points are not party arrival points; a level spawns from them on first visit. <see cref="Type"/>
/// is an object reference whose kind decides whether the spawn is a treasure or an actor, so
/// <see cref="TreasureLevelOrMonsterIndex"/> means a treasure level or a monster index depending on it.
/// Both families store the same 24-byte record, with an absolute position.
/// </remarks>
/// <param name="Index">The spawn's index in the level's spawn array.</param>
/// <param name="Position">The spawn's absolute position.</param>
/// <param name="Radius">The radius within which the spawn is placed.</param>
/// <param name="Type">The spawn's object reference, whose kind selects treasure or actor.</param>
/// <param name="TreasureLevelOrMonsterIndex">The treasure level or monster index to spawn.</param>
/// <param name="Attributes">The spawn's attribute bitfield, kept raw.</param>
/// <param name="Group">The group the spawn belongs to.</param>
public sealed record MapSpawnPoint(
    int Index,
    MapPoint Position,
    int Radius,
    int Type,
    int TreasureLevelOrMonsterIndex,
    int Attributes,
    uint Group);
