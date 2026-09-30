using PartyRpg.Kit.Input;
using PartyRpg.Kit.Party;
using Rusty.Engine;

namespace PartyRpg.Kit.Sessions;

/// <summary>The control a product declares for mixing: the contract its payload action arrives on.</summary>
/// <remarks>
/// <para>
/// The action's name is the kit's (<see cref="AlchemyActions.Mix"/>), as every semantic action's is; a product
/// declares the contract it arrives on.
/// </para>
/// <para>
/// <b>Mixing has no key of its own, and that is deliberate.</b> A mixture names two things out of the party's
/// own pack, and no single press can say which two of the things it carries the player meant — mixing is
/// two clicks on two items rather than a key. It is a payload action for the same reason casting is:
/// the screen's own rows name what was chosen.
/// </para>
/// </remarks>
public sealed record MixIntentNames
{
    /// <summary>Creates the declared mixing control.</summary>
    /// <param name="actionContract">The payload contract a screen's mixing commands arrive on.</param>
    /// <exception cref="ArgumentException">The contract is missing, so no event could ever be claimed on it.</exception>
    public MixIntentNames(string actionContract)
    {
        ActionContract = !string.IsNullOrWhiteSpace(actionContract)
            ? actionContract
            : throw new ArgumentException(
                "The mixing control declares no contract, so no event could ever be claimed on it.",
                nameof(actionContract));
    }

    /// <summary>The payload contract a screen's mixing commands arrive on.</summary>
    public string ActionContract { get; }
}

/// <summary>The payload action a pack screen sends when a player mixes two things.</summary>
public static class AlchemyActions
{
    /// <summary>Mixes the two items the screen drew, through the session's one mixing workflow.</summary>
    public const string Mix = "party.mix";
}

/// <summary>One mixture a screen asked for: which member mixes, and which two things they mix.</summary>
/// <remarks>
/// The member is the party's own index rather than a durable identity, exactly as a casting's member is: the
/// screen was shown the party in its order and names the row it drew. The two items are named by the identity
/// the projection published for the pack's own rows, because two potions of one kind are two things at two
/// strengths and the workflow refuses an identity the pack does not hold.
/// </remarks>
/// <param name="Member">The mixing character's place in the party, counted from zero.</param>
/// <param name="First">One ingredient, as the pack holds it.</param>
/// <param name="Second">The other ingredient, as the pack holds it.</param>
public readonly record struct MixRequest(int Member, ItemInstanceId First, ItemInstanceId Second);

/// <summary>The mixtures a player asked for, read from the admitted input of each update, in the order they arrived.</summary>
public sealed class MixInput
{
    private readonly string _actionContract;

    /// <summary>Creates the reader for the declared mixing control.</summary>
    /// <param name="names">The mixing control the host declared.</param>
    public MixInput(MixIntentNames names)
    {
        ArgumentNullException.ThrowIfNull(names);
        _actionContract = names.ActionContract;
    }

    /// <summary>The mixtures this update carried; one that does not name two items is dropped.</summary>
    /// <param name="inbox">The update's input.</param>
    public IReadOnlyList<MixRequest> Read(ActionInbox inbox)
    {
        ArgumentNullException.ThrowIfNull(inbox);
        List<MixRequest> mixes = [];
        foreach (UiAction action in inbox.Take(_actionContract, AlchemyActions.Mix))
        {
            if (action.Identity("first") is not { } first || action.Identity("second") is not { } second) continue;
            mixes.Add(new MixRequest(action.Int("member") ?? 0, new ItemInstanceId(first), new ItemInstanceId(second)));
        }

        return mixes;
    }
}
