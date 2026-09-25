namespace PartyRpg.Kit.Party;

/// <summary>One member of the default party a ruleset offers as a starting point.</summary>
/// <remarks>
/// The default is a set of answers, not a finished character: it names the same choices a player would make
/// — portrait, class, name, attribute values and chosen skills — and creation applies them through the same
/// steps, so a default that breaks a rule is refused exactly where a player's own choice would be.
/// </remarks>
/// <param name="Portrait">The portrait the default member is created with.</param>
/// <param name="Class">The class the default member belongs to.</param>
/// <param name="Name">The name the default member is given.</param>
/// <param name="Attributes">The attribute scores the default aims for, in the race's own order.</param>
/// <param name="ChosenSkills">The skills the default picks, beyond the class's fixed ones.</param>
public sealed record CreationMemberDefaults(
    PortraitId Portrait,
    ClassId Class,
    string Name,
    IReadOnlyList<AttributeScore> Attributes,
    IReadOnlyList<SkillId> ChosenSkills);

/// <summary>The whole default party a ruleset offers when a player starts a new game.</summary>
/// <remarks>
/// Offered, never imposed: a player may accept it or change any character, and the flow treats it as one more
/// set of choices to validate. The values themselves are the ruleset's — races, classes, skills and starting
/// numbers are its vocabulary — so the kit only carries them from the ruleset to the flow.
/// </remarks>
public sealed record PartyCreationDefaults
{
    /// <summary>States a default party.</summary>
    /// <param name="members">The default members, in the order the party stands in; at least one.</param>
    /// <exception cref="ArgumentException">The default party has no members.</exception>
    public PartyCreationDefaults(IReadOnlyList<CreationMemberDefaults> members)
    {
        ArgumentNullException.ThrowIfNull(members);
        if (members.Count == 0)
        {
            throw new ArgumentException(
                "A default party has at least one member; an empty one is not a party a player could accept.",
                nameof(members));
        }

        Members = members;
    }

    /// <summary>The default members, in the order the party stands in.</summary>
    public IReadOnlyList<CreationMemberDefaults> Members { get; }
}
