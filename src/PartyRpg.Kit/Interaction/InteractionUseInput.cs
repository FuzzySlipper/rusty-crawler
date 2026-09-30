using System.Text;
using PartyRpg.Kit.Input;
using Rusty.Engine;

namespace PartyRpg.Kit.Interaction;

/// <summary>The use controls a product declares, by the names a player's request arrives on.</summary>
/// <remarks>
/// The names are data rather than vocabulary, exactly as the movement, creation, and save controls are: the
/// kit claims what a product declares and invents no key of its own. A product offers two ways to ask — a
/// digital intent for a key, and the kit's own action (<see cref="UseActions.Use"/>) on the payload contract
/// the interface claims its semantic actions on — and both are read inside the one admitted update, so a key
/// and a button ask for exactly the same use.
/// </remarks>
public sealed record UseIntentNames
{
    /// <summary>Creates the declared control names.</summary>
    /// <param name="intent">The digital intent a use arrives on.</param>
    /// <param name="actionContract">The payload contract the use action arrives on.</param>
    /// <exception cref="ArgumentException">A name is missing, so no event could ever be claimed for it.</exception>
    public UseIntentNames(string intent, string actionContract)
    {
        Intent = Require(intent, nameof(intent));
        ActionContract = Require(actionContract, nameof(actionContract));
    }

    /// <summary>The digital intent a use arrives on.</summary>
    public string Intent { get; }

    /// <summary>The payload contract that action arrives on.</summary>
    public string ActionContract { get; }

    private static string Require(string name, string parameterName) =>
        !string.IsNullOrWhiteSpace(name)
            ? name
            : throw new ArgumentException(
                $"The use control '{parameterName}' declares no name, so no event could ever be claimed for it.",
                parameterName);
}

/// <summary>The payload action a screen sends to use what the party faces.</summary>
public static class UseActions
{
    /// <summary>Uses what the party faces, exactly as the use key does.</summary>
    public const string Use = "party.use";
}

/// <summary>Whether the player asked to use what the party faces, read from the admitted input of each update.</summary>
public sealed class InteractionUseInput
{
    private readonly byte[] _intent;
    private readonly string _contract;

    /// <summary>Creates the reader for the declared use controls.</summary>
    /// <param name="names">The use controls the host declared.</param>
    public InteractionUseInput(UseIntentNames names)
    {
        ArgumentNullException.ThrowIfNull(names);
        _intent = Encoding.UTF8.GetBytes(names.Intent);
        _contract = names.ActionContract;
    }

    /// <summary>Whether this update asked for a use; several requests in one update are one use.</summary>
    /// <param name="inbox">The update's input.</param>
    public bool Read(ActionInbox inbox)
    {
        ArgumentNullException.ThrowIfNull(inbox);
        bool asked = inbox.Activated(_intent);
        return inbox.Take(_contract, UseActions.Use).Count > 0 || asked;
    }
}
