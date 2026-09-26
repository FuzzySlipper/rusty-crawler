using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Rusty.Engine;

namespace PartyRpg.Kit.Sessions;

/// <summary>The controls a product declares for fighting: the one act control.</summary>
/// <remarks>
/// <para>
/// Names are data rather than vocabulary, exactly as the movement, use, service, conversation, and stop
/// controls are: the kit claims what a product declares and invents no key of its own. One control rather
/// than one per kind of attack, because the design's act key is one key: what a member does with it — a
/// quick spell, a bow, or hand-to-hand — is a ruleset answer about that member, not something a player
/// chooses per press.
/// </para>
/// <para>
/// Two names are needed because a product offers two ways to ask: a digital intent for a key, which a
/// product may declare as a held control so that holding it keeps attacking, and one action name on the
/// payload contract the interface already claims its semantic actions on, so a key and a panel button ask
/// for exactly the same attack.
/// </para>
/// </remarks>
public sealed record CombatIntentNames
{
    /// <summary>Creates the declared combat control names.</summary>
    /// <param name="attack">The intent that orders the party to attack what it faces.</param>
    /// <param name="action">The payload action name that orders the same attack.</param>
    /// <param name="actionContract">The payload contract that action arrives on.</param>
    /// <param name="turn">
    /// The pace controls declared beside the act control, when the product declares any: the toggle and the
    /// two turn actions a paced fight has beyond attacking. A product that declares none still fights — its
    /// fight is simply played in real time, because nothing can switch the pacing or pass a turn.
    /// </param>
    /// <exception cref="ArgumentException">A name is missing, so no event could ever be claimed for it.</exception>
    public CombatIntentNames(string attack, string action, string actionContract, TurnIntentNames? turn = null)
    {
        Attack = Require(attack, nameof(attack));
        Action = Require(action, nameof(action));
        ActionContract = Require(actionContract, nameof(actionContract));
        Turn = turn;
    }

    /// <summary>The intent that orders the party to attack.</summary>
    public string Attack { get; }

    /// <summary>The payload action name that orders the same attack.</summary>
    public string Action { get; }

    /// <summary>The payload contract that action arrives on.</summary>
    public string ActionContract { get; }

    /// <summary>The pace controls declared beside the act control, or null when the product declared none.</summary>
    public TurnIntentNames? Turn { get; }

    private static string Require(string name, string parameterName) =>
        !string.IsNullOrWhiteSpace(name)
            ? name
            : throw new ArgumentException(
                $"The combat control '{parameterName}' declares no name, so no event could ever be claimed for it.",
                parameterName);
}

/// <summary>
/// Whether the player is asking the party to attack, read from the admitted input of each update.
/// </summary>
/// <remarks>
/// <para>
/// The act control is held rather than pressed, because the donor's own control repeats while it is down:
/// a party that holds it keeps attacking as its members recover, and each attack is still gated by that
/// member's own recovery rather than by the key repeating. So a hold is remembered between updates exactly
/// as a held movement key is — the press is one event and the release is a later one, and reading one update
/// in isolation cannot tell a key still down from a key never pressed.
/// </para>
/// <para>
/// A control a product maps as <c>held</c> arrives as a state rather than as a transition: the engine reports
/// the control for every update it stays down and reports nothing at all once it comes up, so this update's
/// held events replace the previous update's rather than accumulating — the same discipline the movement
/// reader keeps, because a key that is no longer held simply stops.
/// </para>
/// <para>
/// A direct interface claim carries neither an edge nor a release, so a panel button asks for its attack for
/// the one update it arrives in: a claim has nobody to send the release, and a button that stayed held would
/// keep the party attacking after the player stopped pressing it.
/// </para>
/// <para>
/// An event on an intent this reader does not claim does nothing at all: the controls are exactly the names
/// a product declared, and a foreign intent is somebody else's event. A malformed payload carries no attack,
/// because an input channel must not throw on hostile bytes.
/// </para>
/// </remarks>
public sealed class CombatInput
{
    private readonly byte[] _attack;
    private readonly byte[] _actionContract;
    private readonly string _action;
    private bool _held;
    private bool _stateDriven;

    /// <summary>Creates the reader for one product's declared combat controls.</summary>
    /// <param name="names">The intent and contract names the order arrives on.</param>
    /// <exception cref="ArgumentNullException">No control names were declared.</exception>
    public CombatInput(CombatIntentNames names)
    {
        ArgumentNullException.ThrowIfNull(names);
        _attack = Encoding.UTF8.GetBytes(names.Attack);
        _action = names.Action;
        _actionContract = Encoding.UTF8.GetBytes(names.ActionContract);
    }

    /// <summary>Reads one update's admitted input into whether the party is being ordered to attack.</summary>
    /// <param name="input">The admitted input slice of one engine update.</param>
    /// <returns>Whether the act control is down.</returns>
    public bool Read(ReadOnlySpan<ProductInputEvent> input)
    {
        bool state = false;
        bool claimed = false;
        foreach (ProductInputEvent inputEvent in input)
        {
            if (inputEvent.ValueKind == InputValueKind.Digital)
            {
                if (!inputEvent.Intent.Span.SequenceEqual(_attack)) continue;
                if (inputEvent.Edge == InputEdge.Held)
                {
                    state = true;
                }
                else if (inputEvent.Edge == InputEdge.Pressed)
                {
                    _held = true;
                }
                else if (inputEvent.Edge == InputEdge.Released)
                {
                    _held = false;
                }
                else if (inputEvent.Phase == InputPhase.DirectUi || inputEvent.Provenance == InputProvenance.DirectUi)
                {
                    // A direct claim has no release behind it, so it asks for exactly the update it arrives in.
                    claimed = true;
                }

                continue;
            }

            if (inputEvent.ValueKind != InputValueKind.ProductPayload) continue;
            if (!inputEvent.PayloadContract.Span.SequenceEqual(_actionContract)) continue;
            if (string.Equals(Parse(inputEvent.PayloadData.Span)?.Action, _action, StringComparison.Ordinal))
            {
                // A payload action is one press and no more: the contract carries no hold.
                claimed = true;
            }
        }

        // A control that arrives as state is whatever this update said and nothing else: the bit a previous
        // update set is dropped first, so a key the player let go of stops ordering attacks.
        _stateDriven = state;
        return _held || _stateDriven || claimed;
    }

    /// <summary>
    /// Drops what the control was holding, which is what a change of pacing does to it.
    /// </summary>
    /// <remarks>
    /// A key held across a mode change belonged to the pacing the player was in: in real time it means "keep
    /// attacking as each member recovers", and in a paced fight it would mean "keep committing turns", which
    /// is not the same decision. Releasing it here makes the switch an edge rather than a state — the player
    /// presses again for the mode they are in now — and a key the engine is still reporting as down is
    /// reported again by the next update, so nothing is lost that the player is still holding.
    /// </remarks>
    public void Release()
    {
        _held = false;
        _stateDriven = false;
    }

    /// <summary>
    /// Reads an action from admitted payload bytes. Malformed or empty payloads return null: an input channel
    /// must not throw on hostile bytes, and a caller that receives null simply has no action.
    /// </summary>
    private static CombatActionDto? Parse(ReadOnlySpan<byte> utf8)
    {
        if (utf8.IsEmpty) return null;
        try
        {
            return JsonSerializer.Deserialize(utf8, CombatActionJsonContext.Default.CombatActionDto);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>The combat action payload's wire record: an action name, and nothing else.</summary>
internal sealed record CombatActionDto(string? Action);

/// <summary>Source-generated JSON for the combat action payload, so reading it stays AOT-safe.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(CombatActionDto))]
internal sealed partial class CombatActionJsonContext : JsonSerializerContext;
