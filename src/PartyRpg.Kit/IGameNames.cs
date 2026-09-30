using System.Globalization;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Skills;

namespace PartyRpg.Kit;

/// <summary>What a game calls the things the kit only counts: a rung of a skill's ladder, and an item.</summary>
/// <remarks>
/// <para>
/// The kit's tier is a rung number and its item definition is a row identity, and neither carries a word,
/// because a ladder's names and an item's name belong to the game that has them. Every mechanism and panel
/// that has to say one asks this one seam — a casting refused for its mastery, a mixture refused for its
/// rung, a panel's list of what a character knows, the scroll a spell was read from — so a game states its
/// words once rather than once per mechanism.
/// </para>
/// <para>
/// <b>A session with no names still reads.</b> <see cref="GameNames"/> answers with the rung's number and
/// the item's own identity when a game states nothing, which is at least a thing a person can match against
/// the pack they are looking at.
/// </para>
/// </remarks>
public interface IGameNames
{
    /// <summary>What one rung of a skill's ladder is called, untrained included.</summary>
    /// <param name="tier">The rung to name.</param>
    /// <returns>The word a person reads for that rung.</returns>
    string TierName(SkillTier tier);

    /// <summary>What a person reads for an item definition.</summary>
    /// <param name="definition">The item definition to name.</param>
    /// <returns>The name, or empty when the game states none.</returns>
    string ItemName(ItemDefinitionId definition);
}

/// <summary>Reads a game's names, falling back to the kit's own numbers when a game states none.</summary>
public static class GameNames
{
    /// <summary>What a rung is called, or its number when nothing names it.</summary>
    /// <param name="names">The game's names, or null when the session composes none.</param>
    /// <param name="tier">The rung to name.</param>
    /// <returns>The word or the number a person reads.</returns>
    public static string Tier(IGameNames? names, SkillTier tier) =>
        names?.TierName(tier) ?? tier.Value.ToString(CultureInfo.InvariantCulture);

    /// <summary>What an item is called, or its identity when nothing names it.</summary>
    /// <param name="names">The game's names, or null when the session composes none.</param>
    /// <param name="definition">The item definition to name.</param>
    /// <returns>The name or the identity a person reads.</returns>
    public static string Item(IGameNames? names, ItemDefinitionId definition) =>
        names?.ItemName(definition) is { Length: > 0 } called ? called : definition.Value;
}
