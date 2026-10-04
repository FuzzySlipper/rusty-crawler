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
/// and a button ask for exactly the same use. The same owner may also declare one next-target intent and
/// action; it changes only the Engine's existing focus before a use is resolved.
/// </remarks>
public sealed record UseIntentNames
{
    /// <summary>Creates the declared control names.</summary>
    /// <param name="intent">The digital intent a use arrives on.</param>
    /// <param name="actionContract">The payload contract the use action arrives on.</param>
    /// <param name="nextTarget">The optional digital intent that cycles the interaction focus.</param>
    /// <exception cref="ArgumentException">A name is missing, so no event could ever be claimed for it.</exception>
    public UseIntentNames(string intent, string actionContract, string? nextTarget = null)
    {
        Intent = Require(intent, nameof(intent));
        ActionContract = Require(actionContract, nameof(actionContract));
        NextTarget = nextTarget is null ? null : Require(nextTarget, nameof(nextTarget));
    }

    /// <summary>The digital intent a use arrives on.</summary>
    public string Intent { get; }

    /// <summary>The payload contract that action arrives on.</summary>
    public string ActionContract { get; }

    /// <summary>The optional digital intent that cycles the Engine's existing interaction focus.</summary>
    public string? NextTarget { get; }

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

    /// <summary>Cycles to the next eligible target in the Engine's existing interaction focus.</summary>
    public const string NextTarget = "party.next-target";
}

/// <summary>Whether the player asked to use what the party faces, read from the admitted input of each update.</summary>
public sealed class InteractionUseInput
{
    private readonly byte[] _intent;
    private readonly byte[]? _nextTarget;
    private readonly string _contract;

    /// <summary>Creates the reader for the declared use controls.</summary>
    /// <param name="names">The use controls the host declared.</param>
    public InteractionUseInput(UseIntentNames names)
    {
        ArgumentNullException.ThrowIfNull(names);
        _intent = Encoding.UTF8.GetBytes(names.Intent);
        _nextTarget = names.NextTarget is { } nextTarget ? Encoding.UTF8.GetBytes(nextTarget) : null;
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

    /// <summary>
    /// Whether this update asks the interaction owner to advance the Engine's current target focus. A key and
    /// the companion's semantic action are one request, and several requests in one update are one cycle.
    /// </summary>
    /// <param name="inbox">The update's input.</param>
    /// <returns><c>+1</c> for a next-target request, otherwise <c>0</c>.</returns>
    public int ReadCycle(ActionInbox inbox)
    {
        ArgumentNullException.ThrowIfNull(inbox);
        bool asked = _nextTarget is { } nextTarget && inbox.Activated(nextTarget);
        asked |= inbox.Take(_contract, UseActions.NextTarget).Count > 0;
        return asked ? 1 : 0;
    }
}
