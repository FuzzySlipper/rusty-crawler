using System.Globalization;
using PartyRpg.Kit.Combat;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// This game's kinds of harm and what the shipped bestiary resists of each.
/// </summary>
/// <remarks>
/// <para>
/// <b>The kinds are the donor's own damage types.</b> OpenEnroth <c>src/Engine/Objects/ItemEnums.h:10-23</c>,
/// <c>enum class DamageType</c>: fire, air, water, earth, physical, magic, spirit, mind, body, light, dark.
/// The names used here are the monster table's own column names for the ten it carries resistances for
/// (<c>monsters.txt</c> header row 2, columns 28-37: <c>Fire Air Water Earth Mind Spirit Body Light Dark
/// Phys</c>), so a reading and a rule use one spelling.
/// </para>
/// <para>
/// <b>The magic type is the donor's odd one out.</b> <c>DAMAGE_MAGIC</c> is annotated "Only used for
/// Armageddon in MM7, and cannot be resisted" and the table has no column for it, so it is stated here and
/// everything resists it with nothing. A spell attack resolves as this kind until the schools arrive and a
/// spell states its own.
/// </para>
/// <para>
/// <b>How the table states immunity is read literally.</b> A resistance cell is a number, or the text
/// <c>Imm</c>, which the donor reads as full immunity encoded as 200
/// (OpenEnroth <c>src/Engine/Objects/Monsters.cpp:327-330</c>, <c>parseResistance</c>), and its damage
/// arithmetic then answers no harm at all for a resistance at or above 200
/// (<c>src/Engine/Objects/Actor.cpp:3754-3760</c>). This reads <c>Imm</c> as <see cref="Resistance.Immune"/>
/// directly rather than as a weight of 200, which is the same outcome and does not depend on a threshold
/// nothing else in this game states.
/// </para>
/// </remarks>
internal static class MightAndMagic7Damage
{
    /// <summary>Physical harm: a fist, a blade, an arrow, a monster's claws.</summary>
    internal static readonly DamageKindId Physical = new("Phys");

    /// <summary>Fire.</summary>
    internal static readonly DamageKindId Fire = new("Fire");

    /// <summary>Air.</summary>
    internal static readonly DamageKindId Air = new("Air");

    /// <summary>Water.</summary>
    internal static readonly DamageKindId Water = new("Water");

    /// <summary>Earth.</summary>
    internal static readonly DamageKindId Earth = new("Earth");

    /// <summary>Mind.</summary>
    internal static readonly DamageKindId Mind = new("Mind");

    /// <summary>Spirit.</summary>
    internal static readonly DamageKindId Spirit = new("Spirit");

    /// <summary>Body.</summary>
    internal static readonly DamageKindId Body = new("Body");

    /// <summary>Light.</summary>
    internal static readonly DamageKindId Light = new("Light");

    /// <summary>Dark.</summary>
    internal static readonly DamageKindId Dark = new("Dark");

    /// <summary>Magic that nothing resists, which is the donor's own reading of its magic damage type.</summary>
    internal static readonly DamageKindId Magic = new("Magic");

    /// <summary>Energy, which the monster table's attack columns carry and no resistance column covers.</summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/Objects/ItemEnums.h:22</c> — <c>DAMAGE_ENERGY = 12</c>. The operator's own
    /// monster table names it <c>Ener</c> in fifteen attack columns, so a creature's blow can be of this kind
    /// and no target resists it, which is what an absent resistance column states.
    /// </remarks>
    internal static readonly DamageKindId Energy = new("Ener");

    /// <summary>The ten kinds of harm a monster row states a resistance to.</summary>
    private static readonly DamageKindId[] Resisted =
        [Fire, Air, Water, Earth, Mind, Spirit, Body, Light, Dark, Physical];

    /// <summary>The cell text the shipped table writes for full immunity.</summary>
    internal const string ImmunityText = "Imm";

    /// <summary>The value the donor encodes that text as, kept for the citation and for a pack that states it.</summary>
    /// <remarks>OpenEnroth <c>src/Engine/Objects/Monsters.cpp:328-330</c> and <c>Actor.cpp:3754</c>.</remarks>
    internal const int ImmunityValue = 200;

    /// <summary>The kind a kind id names, or null when this game has no such kind.</summary>
    /// <param name="kind">The kind to look for.</param>
    internal static DamageKindId? Known(string kind)
    {
        foreach (DamageKindId known in Resisted)
        {
            if (string.Equals(known.Value, kind, StringComparison.Ordinal)) return known;
        }

        if (string.Equals(Magic.Value, kind, StringComparison.Ordinal)) return Magic;
        return string.Equals(Energy.Value, kind, StringComparison.Ordinal) ? Energy : null;
    }
}
