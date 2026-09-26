using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Skills;

/// <summary>The skills one game's content declares, in the order the content states them.</summary>
/// <remarks>
/// <para>
/// This is content's list as this game reads it: every row the loaded packs declare, each with the block the
/// ruleset puts it in, including the rows no block carries. Order is content's own, so two readings of one
/// catalog list the same skills the same way, and a panel shows them in the order the shipped table holds
/// them rather than in an order some screen chose.
/// </para>
/// <para>
/// <b>A skill nothing declares is not in the catalog.</b> Adding a row is a content change and nothing else
/// — the ruleset's reading of a row it does not recognise is <see cref="SkillBlock.Unused"/>, never an
/// exception — so a pack that adds a skill, and a ruleset that has no block for it, still loads and says
/// what it could not place. A duplicate identity is a defect rather than a merge: two rows claiming one
/// skill would make the level a character has ambiguous.
/// </para>
/// </remarks>
public sealed class SkillCatalog
{
    private readonly List<SkillDefinition> _definitions;
    private readonly List<SkillDefinition> _used;
    private readonly List<SkillDefinition> _unused;

    /// <summary>Reads a catalog from the rows a ruleset placed.</summary>
    /// <param name="definitions">The rows, in content's order.</param>
    /// <exception cref="ArgumentNullException">No rows were supplied.</exception>
    /// <exception cref="ArgumentException">A skill is declared twice, so which block it is in would be ambiguous.</exception>
    public SkillCatalog(IEnumerable<SkillDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        _definitions = [];
        _used = [];
        _unused = [];
        HashSet<SkillId> seen = [];
        foreach (SkillDefinition definition in definitions)
        {
            if (!seen.Add(definition.Id))
            {
                throw new ArgumentException(
                    $"Skill '{definition.Id}' is declared twice, so which block it belongs to would be ambiguous.",
                    nameof(definitions));
            }

            _definitions.Add(definition);
            (definition.IsUsed ? _used : _unused).Add(definition);
        }
    }

    /// <summary>Every row the content declares, in content's order, the unused ones included.</summary>
    public IReadOnlyList<SkillDefinition> Definitions => _definitions;

    /// <summary>The rows this game's blocks carry, in content's order.</summary>
    public IReadOnlyList<SkillDefinition> Used => _used;

    /// <summary>The rows content carries that this game's blocks do not, in content's order.</summary>
    public IReadOnlyList<SkillDefinition> Unused => _unused;

    /// <summary>How many rows the content declares altogether.</summary>
    public int Count => _definitions.Count;

    /// <summary>Whether content declares a skill at all.</summary>
    /// <param name="skill">The skill to look for.</param>
    public bool Declares(SkillId skill) => IndexOf(skill) >= 0;

    /// <summary>How many rows the catalog holds for one block.</summary>
    /// <param name="block">The block to count.</param>
    public int CountOf(SkillBlock block)
    {
        int count = 0;
        foreach (SkillDefinition definition in _definitions)
        {
            if (definition.Block == block) count++;
        }

        return count;
    }

    /// <summary>
    /// Reads one row, or an unused reading when content declares no such skill.
    /// </summary>
    /// <remarks>
    /// A skill the content does not declare reads as belonging to no block rather than throwing: a
    /// character's entry, a rule's question, and a command from a screen can all name a skill this build has
    /// never heard of, and the answer that lets the caller refuse by name is worth more than an exception in
    /// the middle of a projection.
    /// </remarks>
    /// <param name="skill">The skill to read.</param>
    /// <returns>The row, or an unused row naming the skill when content declares none.</returns>
    public SkillDefinition Read(SkillId skill)
    {
        int index = IndexOf(skill);
        return index >= 0 ? _definitions[index] : new SkillDefinition(skill, SkillBlock.Unused);
    }

    private int IndexOf(SkillId skill)
    {
        for (int index = 0; index < _definitions.Count; index++)
        {
            if (_definitions[index].Id == skill) return index;
        }

        return -1;
    }
}
