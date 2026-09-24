namespace PartyRpg.Kit.Content;

/// <summary>
/// The four kinds of content, kept apart because they change at different rates and have different
/// authors: catalogs of things that exist, adjustable values, scenario state, and imported world data.
/// </summary>
public enum ContentPackKind
{
    /// <summary>Catalogs with meaning: classes, skills, spells, monsters, items, services, quests, places.</summary>
    Definitions,

    /// <summary>One discoverable profile tree of adjustable values.</summary>
    Tuning,

    /// <summary>The starting state: party defaults, placements, spawns, scenario flags.</summary>
    Scenario,

    /// <summary>World data produced by the importer: geometry, spatial data, normalized tables, media references.</summary>
    World,
}
