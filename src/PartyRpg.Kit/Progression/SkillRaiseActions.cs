using PartyRpg.Kit.Input;
using PartyRpg.Kit.Party;
using Rusty.Engine;

namespace PartyRpg.Kit.Progression;

/// <summary>The names a product declares for spending skill points, as its screen asks for a raise.</summary>
/// <remarks>
/// Names are data rather than vocabulary, exactly as the movement, creation, save, use, service, and rest
/// controls are: the kit claims what a product declares and invents no control of its own. A raise is the
/// one panel action the progression owner reads, because a raise is the one thing the owner does that a
/// player asks for directly — experience is awarded by the world and a level is bought at a counter.
/// </remarks>
/// <param name="action">The payload action a screen's raise control sends.</param>
/// <param name="actionContract">The payload contract the screen's actions arrive on.</param>
/// <exception cref="ArgumentException">A name is missing, so no event could ever be claimed for it.</exception>
public sealed record SkillRaiseIntentNames
{
    /// <summary>Creates the declared skill-spend control.</summary>
    /// <param name="actionContract">The payload contract a screen's skill-spend commands arrive on.</param>
    /// <exception cref="ArgumentException">The contract is missing, so no event could ever be claimed on it.</exception>
    public SkillRaiseIntentNames(string actionContract)
    {
        ActionContract = !string.IsNullOrWhiteSpace(actionContract)
            ? actionContract
            : throw new ArgumentException(
                "The skill control declares no contract, so no event could ever be claimed on it.",
                nameof(actionContract));
    }

    /// <summary>The payload contract a screen's skill-spend commands arrive on.</summary>
    public string ActionContract { get; }
}

/// <summary>The actions a skills screen sends, on the payload contract the product declares.</summary>
/// <remarks>
/// A wire name, not a rule: the screen reports which member's skill the player asked to raise, and the
/// owner judges the ceiling, the price, and the pool before anything moves.
/// </remarks>
public static class SkillRaiseActions
{
    /// <summary>Raises one member's skill by the levels the action states.</summary>
    /// <remarks>
    /// The action carries the member, the skill, and how many levels, because a raise is about one
    /// character's one skill: a bare "raise something" would leave the owner guessing which of a party's
    /// entries the player meant.
    /// </remarks>
    public const string Raise = "party.raise-skill";
}

/// <summary>One raise a screen asked for: which member, which skill, and how many levels.</summary>
/// <remarks>
/// The member is the party's own index rather than a durable identity, exactly as a service lesson's member
/// is: the screen was shown the party in its order and names the row it drew, and the session resolves that
/// row against the party it holds inside the same update. A row the party no longer has is refused by name
/// rather than applied to whoever now stands there.
/// </remarks>
/// <param name="Member">The member's place in the party, counted from zero.</param>
/// <param name="Skill">The skill the screen named.</param>
/// <param name="Levels">How many levels to add, at least one.</param>
public readonly record struct SkillRaiseRequest(int Member, SkillId Skill, int Levels);

/// <summary>The skill raises a player asked for, read from the admitted input of each update, in order.</summary>
public sealed class SkillRaiseInput
{
    private readonly string _actionContract;

    /// <summary>Creates the reader for the declared skill-spend control.</summary>
    /// <param name="names">The skill-spend control the host declared.</param>
    public SkillRaiseInput(SkillRaiseIntentNames names)
    {
        ArgumentNullException.ThrowIfNull(names);
        _actionContract = names.ActionContract;
    }

    /// <summary>The raises this update carried; one that names no skill is dropped.</summary>
    /// <param name="inbox">The update's input.</param>
    public IReadOnlyList<SkillRaiseRequest> Read(ActionInbox inbox)
    {
        ArgumentNullException.ThrowIfNull(inbox);
        List<SkillRaiseRequest> raises = [];
        foreach (UiAction action in inbox.Take(_actionContract, SkillRaiseActions.Raise))
        {
            string skill = action.Text("skill");
            if (skill.Length == 0) continue;

            // A raise of no levels is read as one level: the owner refuses what it will not do, rather than the
            // reader silently turning a request into nothing.
            raises.Add(new SkillRaiseRequest(
                action.Int("member") ?? 0,
                new SkillId(skill),
                action.Int("levels") is { } levels && levels > 0 ? levels : 1));
        }

        return raises;
    }
}
