using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Magic;

/// <summary>One spell as this game reads it: which school it is in, what it costs, what it is aimed at, and what it does.</summary>
/// <remarks>
/// <para>
/// The identity is content's — a spell is the row the shipped table declares — and everything else here is
/// the ruleset's reading of its own tables, because the shipped spell table carries names, descriptions,
/// and a damage type and nothing about what casting one costs or does. That is the same division the skill
/// catalog keeps: content declares the rows, and policy answers what each row means.
/// </para>
/// <para>
/// <b>The school's skill is part of the definition.</b> Which skill gates a school's spells is a fact about
/// the game's magic, and stating it here is what lets the mechanism compare a spell's tier against the
/// caster's mastery of the right skill without learning a school's name: the kit reads the skill the
/// definition names and the entry the character holds for it.
/// </para>
/// <para>
/// <b>The effect is an identity, not a rule.</b> <see cref="Effect"/> is the ruleset's own word for what
/// the spell does, handed to the effect seam untouched, exactly as an attack order hands an ability name
/// back to the ruleset that named it. The kit never compares one, never keeps a list of them, and never
/// branches on one, which is what keeps a spell's behaviour out of the mechanism.
/// </para>
/// </remarks>
/// <param name="Id">Which spell this is, as content names it.</param>
/// <param name="Name">What a person reads for it.</param>
/// <param name="School">Which school of magic the spell belongs to, as the ruleset reads the row.</param>
/// <param name="SchoolSkill">The skill whose mastery gates the spell, which a caster's own entry answers.</param>
/// <param name="Tier">The rung of the school's ladder a caster must stand at before the spell is theirs.</param>
/// <param name="Cost">What one casting costs in spell points at the caster's own mastery, before any adjustment.</param>
/// <param name="Targeting">What the spell is aimed at.</param>
/// <param name="Effect">The ruleset's own identity for what the spell does, handed to the effect seam unread.</param>
public readonly record struct SpellDefinition(
    SpellId Id,
    string Name,
    string School,
    SkillId SchoolSkill,
    SkillTier Tier,
    int Cost,
    SpellTargeting Targeting,
    string Effect);

/// <summary>The spells one game's content declares, in the order the content states them.</summary>
/// <remarks>
/// <para>
/// This is content's list as this game reads it. Order is content's own, so two readings of one catalog
/// list the same spells the same way and a spellbook shows them in the order the shipped table holds them
/// rather than in an order some screen chose. A spell's place in its school is therefore readable from the
/// catalog itself, which is what a guild's tier gate is stated over.
/// </para>
/// <para>
/// <b>A spell nothing declares is not in the catalog.</b> Reading one answers a definition that declares no
/// school and costs nothing, rather than throwing: a command from a screen, a character's own saved
/// spellbook, and a rule's question can all name a spell this build has never heard of, and the answer that
/// lets a caller refuse by name is worth more than an exception in the middle of a projection. A duplicate
/// identity is a defect rather than a merge, exactly as it is in the skill catalog.
/// </para>
/// </remarks>
public sealed class SpellCatalog
{
    private readonly List<SpellDefinition> _definitions;

    /// <summary>Reads a catalog from the rows a ruleset placed.</summary>
    /// <param name="definitions">The rows, in content's order.</param>
    /// <exception cref="ArgumentNullException">No rows were supplied.</exception>
    /// <exception cref="ArgumentException">A spell is declared twice, so which definition it is would be ambiguous.</exception>
    public SpellCatalog(IEnumerable<SpellDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        _definitions = [];
        HashSet<SpellId> seen = [];
        foreach (SpellDefinition definition in definitions)
        {
            if (!seen.Add(definition.Id))
            {
                throw new ArgumentException(
                    $"Spell '{definition.Id}' is declared twice, so which school and cost it has would be ambiguous.",
                    nameof(definitions));
            }

            _definitions.Add(definition);
        }
    }

    /// <summary>Every spell content declares, in content's order.</summary>
    public IReadOnlyList<SpellDefinition> Definitions => _definitions;

    /// <summary>How many spells the content declares altogether.</summary>
    public int Count => _definitions.Count;

    /// <summary>Whether content declares a spell at all.</summary>
    /// <param name="spell">The spell to look for.</param>
    public bool Declares(SpellId spell) => IndexOf(spell) >= 0;

    /// <summary>How many spells the catalog holds for one school, comparing the school's word exactly.</summary>
    /// <param name="school">The school's name, as the ruleset reads it.</param>
    public int CountOf(string school)
    {
        int count = 0;
        foreach (SpellDefinition definition in _definitions)
        {
            if (string.Equals(definition.School, school, StringComparison.Ordinal)) count++;
        }

        return count;
    }

    /// <summary>The schools the catalog holds, in the order content first states each one.</summary>
    /// <returns>The school names, each once.</returns>
    public IReadOnlyList<string> Schools()
    {
        List<string> schools = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (SpellDefinition definition in _definitions)
        {
            if (definition.School.Length > 0 && seen.Add(definition.School)) schools.Add(definition.School);
        }

        return schools;
    }

    /// <summary>
    /// The spells of one school, in content's order, which is the order a school's ladder runs in.
    /// </summary>
    /// <param name="school">The school's name, as the ruleset reads it.</param>
    /// <returns>The school's spells, in the order content states them.</returns>
    public IReadOnlyList<SpellDefinition> OfSchool(string school)
    {
        List<SpellDefinition> spells = [];
        foreach (SpellDefinition definition in _definitions)
        {
            if (string.Equals(definition.School, school, StringComparison.Ordinal)) spells.Add(definition);
        }

        return spells;
    }

    /// <summary>
    /// Reads one row, or an empty definition when content declares no such spell.
    /// </summary>
    /// <remarks>
    /// A spell the content does not declare reads as an empty row rather than throwing, for the reason the
    /// catalog's own remarks give: the refusal that names the spell is worth more than an exception, and a
    /// caller can tell the two apart with <see cref="Declares"/>.
    /// </remarks>
    /// <param name="spell">The spell to read.</param>
    /// <returns>The row, or an empty row naming the spell when content declares none.</returns>
    public SpellDefinition Read(SpellId spell)
    {
        int index = IndexOf(spell);
        return index >= 0
            ? _definitions[index]
            : new SpellDefinition(spell, spell.Value, string.Empty, default, SkillTier.None, 0, SpellTargeting.None, string.Empty);
    }

    private int IndexOf(SpellId spell)
    {
        for (int index = 0; index < _definitions.Count; index++)
        {
            if (_definitions[index].Id == spell) return index;
        }

        return -1;
    }
}
