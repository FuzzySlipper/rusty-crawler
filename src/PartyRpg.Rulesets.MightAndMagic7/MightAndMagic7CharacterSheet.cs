using System.Globalization;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Time;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// This game's character page: the seven scores as they actually read, the vitals, and the six resistances, each
/// read through the same rule the fight prices a character by.
/// </summary>
/// <remarks>
/// <para>
/// The original's stats page (OpenEnroth <c>src/GUI/UI/UICharacter.cpp</c>, <c>CharacterUI_StatsTab_Draw</c>) lists
/// skill points, the seven scores as current over base, hit and spell points, armour class, condition, quick spell,
/// age, level, experience, attack and damage, shoot and damage, and Fire, Air, Water, Earth, Mind and Body. This page
/// states the scores, vitals, armour class and resistances from the fight's own sums
/// (<see cref="MightAndMagic7Combat.ActualAttribute"/>, <see cref="MightAndMagic7Combat.CharacterArmorClass"/>,
/// <see cref="MightAndMagic7Combat.CharacterResistance"/>), so the page cannot disagree with a blow.
/// </para>
/// <para>
/// A Luck or resistance row a joined companion's profession changes names that companion beside it.
/// </para>
/// <para>
/// Approximate. Attack and shoot bonuses and their damage lines, and the quick spell, are not on this page yet:
/// they are priced inside a blow's resolution rather than read as standing values.
/// </para>
/// </remarks>
internal sealed class MightAndMagic7CharacterSheet : ICharacterSheetRule
{
    private static readonly (string Label, Kit.Combat.DamageKindId Kind)[] Resistances =
    [
        ("Fire", MightAndMagic7Damage.Fire),
        ("Air", MightAndMagic7Damage.Air),
        ("Water", MightAndMagic7Damage.Water),
        ("Earth", MightAndMagic7Damage.Earth),
        ("Mind", MightAndMagic7Damage.Mind),
        ("Body", MightAndMagic7Damage.Body),
    ];

    private readonly Func<MightAndMagic7Combat?> _combat;
    private readonly GameClock? _clock;
    private readonly Func<MightAndMagic7Followers?> _followers;

    /// <summary>Creates the sheet over the fight whose sums it reads.</summary>
    /// <param name="combat">The session's fight, which is composed after the sheet is.</param>
    /// <param name="clock">The clock a member's age is read on.</param>
    /// <param name="followers">The companions whose professions the fight's sums include, named beside a changed row.</param>
    internal MightAndMagic7CharacterSheet(Func<MightAndMagic7Combat?> combat, GameClock? clock, Func<MightAndMagic7Followers?>? followers = null)
    {
        _combat = combat ?? throw new ArgumentNullException(nameof(combat));
        _clock = clock;
        _followers = followers ?? (() => null);
    }

    /// <inheritdoc />
    public IReadOnlyList<CharacterSheetSection> Read(PartyMember member)
    {
        ArgumentNullException.ThrowIfNull(member);
        MightAndMagic7Combat? combat = _combat();
        List<CharacterSheetRow> scores = [];
        foreach (AttributeScore carried in member.Attributes.Scores)
        {
            int actual = combat?.ActualAttribute(member, carried.Attribute) ?? carried.Value;
            string companions = carried.Attribute == MightAndMagic7Combat.LuckAttribute && combat is not null
                ? _followers()?.Luck.Sources ?? string.Empty
                : string.Empty;
            scores.Add(new CharacterSheetRow(
                carried.Attribute.Value,
                Number(actual),
                actual == carried.Value && companions.Length == 0
                    ? string.Empty
                    : Joined($"carried {Number(carried.Value)}", Companions(companions))));
        }

        CharacterResources pools = member.Resources;
        CharacterProgression growth = member.Progression;
        List<CharacterSheetRow> vitals =
        [
            new("Hit points", $"{Number(pools.HitPoints.Current)} / {Number(pools.HitPoints.Maximum)}"),
            new("Spell points", $"{Number(pools.SpellPoints.Current)} / {Number(pools.SpellPoints.Maximum)}"),
            new("Condition", member.Conditions.Count == 0
                ? "Good"
                : string.Join(", ", member.Conditions.Active.Select(condition =>
                    condition.Severity > 0 ? $"{condition.Condition} ({Number(condition.Severity)})" : condition.Condition.ToString()))),
            new("Age", Number(MightAndMagic7Ageing.AgeOf(member, _clock))),
            new("Level", Number(growth.Level)),
            new("Experience", growth.Experience.ToString("N0", CultureInfo.InvariantCulture)),
            new("Skill points", Number(growth.SkillPoints)),
        ];

        // The fight prices armour from the member's speed, so a member who carries no speed score has no armour class
        // this game can state; the row is left off rather than made up.
        if (combat is not null && member.Attributes.TryGet(MightAndMagic7Combat.SpeedAttribute, out _))
            vitals.Insert(2, new CharacterSheetRow("Armour class", Number(combat.CharacterArmorClass(member))));

        List<CharacterSheetRow> resistances = [];
        foreach ((string label, Kit.Combat.DamageKindId kind) in Resistances)
        {
            Kit.Combat.Resistance resistance = combat?.CharacterResistance(member, kind) ?? Kit.Combat.Resistance.Of(0);
            string companions = combat is null ? string.Empty : _followers()?.Resistance(kind).Sources ?? string.Empty;
            resistances.Add(new CharacterSheetRow(label, resistance.IsImmune ? "Immune" : Number(resistance.Points), Companions(companions)));
        }

        return
        [
            new CharacterSheetSection("Scores", scores),
            new CharacterSheetSection("Vitals", vitals),
            new CharacterSheetSection("Resistances", resistances),
        ];
    }

    // Who among the joined companions adds to a row, so the page says where a changed reading came from.
    private static string Companions(string sources) => sources.Length == 0 ? string.Empty : $"companions: {sources}";

    private static string Joined(string first, string second) => second.Length == 0 ? first : $"{first}; {second}";

    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
}
