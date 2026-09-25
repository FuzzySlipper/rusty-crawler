using System.Text;
using PartyRpg.Kit.Input;
using Rusty.Engine;

namespace PartyRpg.Kit.Interaction;

/// <summary>The use controls a product declares, by the names a player's request arrives on.</summary>
/// <remarks>
/// The names are data rather than vocabulary, exactly as the movement, creation, and save controls are: the
/// kit claims what a product declares and invents no key of its own. Two names are needed because a product
/// offers two ways to ask — a digital intent for a key, and an action name on the payload contract the
/// interface claims its semantic actions on — and both are read inside the one admitted update, so a key and
/// a button ask for exactly the same use.
/// </remarks>
public sealed record UseIntentNames
{
    /// <summary>Creates the declared control names.</summary>
    /// <param name="intent">The digital intent a use arrives on.</param>
    /// <param name="action">The payload action name a use arrives on.</param>
    /// <param name="actionContract">The payload contract that action arrives on.</param>
    /// <exception cref="ArgumentException">A name is missing, so no event could ever be claimed for it.</exception>
    public UseIntentNames(string intent, string action, string actionContract)
    {
        Intent = Require(intent, nameof(intent));
        Action = Require(action, nameof(action));
        ActionContract = Require(actionContract, nameof(actionContract));
    }

    /// <summary>The digital intent a use arrives on.</summary>
    public string Intent { get; }

    /// <summary>The payload action name a use arrives on.</summary>
    public string Action { get; }

    /// <summary>The payload contract that action arrives on.</summary>
    public string ActionContract { get; }

    private static string Require(string name, string parameterName) =>
        !string.IsNullOrWhiteSpace(name)
            ? name
            : throw new ArgumentException(
                $"The use control '{parameterName}' declares no name, so no event could ever be claimed for it.",
                parameterName);
}

/// <summary>
/// Reads a player's request to use what the party faces out of one admitted update's input.
/// </summary>
/// <remarks>
/// <para>
/// A use is an instant rather than a held control: the player asks for exactly one use per activation, so
/// nothing is remembered between updates and a held key does not keep using what the party faces. That is
/// the opposite of the movement controls — where a held key is a state — and it is why this reader has no
/// memory and cannot leak a press into a later update.
/// </para>
/// <para>
/// A digital event on the declared intent asks, whether it arrived as a physical press or as a direct
/// interface claim, which carries no edge; a payload on the declared contract asks when it names the
/// declared action. Anything else, including a malformed payload, carries no request: an input channel must
/// not throw on hostile bytes, and a caller that receives nothing simply has nothing to apply.
/// </para>
/// </remarks>
public sealed class InteractionUseInput
{
    private readonly byte[] _intent;
    private readonly string _action;
    private readonly byte[] _contract;

    /// <summary>Creates the reader for one product's declared use controls.</summary>
    /// <param name="names">The intent and payload action a use arrives on.</param>
    /// <exception cref="ArgumentNullException">No control names were declared.</exception>
    public InteractionUseInput(UseIntentNames names)
    {
        ArgumentNullException.ThrowIfNull(names);
        _intent = Encoding.UTF8.GetBytes(names.Intent);
        _action = names.Action;
        _contract = Encoding.UTF8.GetBytes(names.ActionContract);
    }

    /// <summary>Whether this update's admitted input carries a request to use what the party faces.</summary>
    /// <param name="input">The admitted input slice of one engine update.</param>
    /// <returns>Whether the player asked for a use.</returns>
    public bool Read(ReadOnlySpan<ProductInputEvent> input)
    {
        foreach (ProductInputEvent inputEvent in input)
        {
            if (inputEvent.ValueKind == InputValueKind.Digital)
            {
                if (inputEvent.Intent.Span.SequenceEqual(_intent) && IsActivation(inputEvent)) return true;
                continue;
            }

            if (inputEvent.ValueKind != InputValueKind.ProductPayload) continue;
            if (!inputEvent.PayloadContract.Span.SequenceEqual(_contract)) continue;
            if (string.Equals(UiActionPayload.Parse(inputEvent.PayloadData.Span)?.Name, _action, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether a digital event is an activation. A physical press carries an edge; a direct interface claim
    /// is admitted with no edge at all, so its own phase and provenance are what identify it.
    /// </summary>
    private static bool IsActivation(in ProductInputEvent inputEvent) =>
        inputEvent.Edge == InputEdge.Pressed
        || inputEvent.Phase == InputPhase.DirectUi
        || inputEvent.Provenance == InputProvenance.DirectUi;
}
