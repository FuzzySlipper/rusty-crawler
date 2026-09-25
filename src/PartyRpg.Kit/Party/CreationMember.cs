namespace PartyRpg.Kit.Party;

/// <summary>One member of the party being created, as it stands right now.</summary>
/// <remarks>
/// A value, not a handle: a screen reads it to draw the character and lets go, and nothing outside the flow
/// can change a character's answers behind the validation that admitted them. Everything that is still open
/// reads as absent — no portrait, no race, no class — so a caller cannot mistake an unanswered step for an
/// answered one.
/// </remarks>
/// <param name="Index">Which member this is, counted from zero in the order the party stands in.</param>
/// <param name="Step">Where this member stands in creation's step sequence.</param>
/// <param name="Portrait">The portrait chosen, or null while none has been.</param>
/// <param name="Race">The race the portrait decided, or null while no portrait has been chosen.</param>
/// <param name="Class">The class chosen, or null while none has been.</param>
/// <param name="Name">The name given, or blank while none has been.</param>
/// <param name="Attributes">The attribute scores, in the race's own order; empty while no race is chosen.</param>
/// <param name="FixedSkills">The skills the class grants, empty while no class is chosen.</param>
/// <param name="ChosenSkills">The skills the player chose, in the order they were chosen.</param>
/// <param name="PoolRemaining">How many attribute points are still unspent; zero once the pool is spent exactly.</param>
public sealed record CreationMember(
    int Index,
    CreationStep Step,
    PortraitId? Portrait,
    RaceId? Race,
    ClassId? Class,
    string Name,
    IReadOnlyList<AttributeScore> Attributes,
    IReadOnlyList<SkillId> FixedSkills,
    IReadOnlyList<SkillId> ChosenSkills,
    int PoolRemaining)
{
    /// <summary>Whether this member has been confirmed and creation may leave it.</summary>
    public bool IsComplete => Step == CreationStep.Complete;

    /// <summary>The skills the character will start with: the class's fixed ones, then the chosen ones.</summary>
    public IReadOnlyList<SkillId> StartingSkills => [.. FixedSkills, .. ChosenSkills];
}
