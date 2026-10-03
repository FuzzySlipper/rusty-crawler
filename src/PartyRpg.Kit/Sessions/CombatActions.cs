using System.Text;
using PartyRpg.Kit.Input;
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
    /// <param name="actionContract">The payload contract the same attack arrives on, as <see cref="CombatActions.Attack"/>.</param>
    /// <param name="turn">
    /// The pace controls declared beside the act control, when the product declares any: the toggle and the
    /// two turn actions a paced fight has beyond attacking. A product that declares none still fights — its
    /// fight is simply played in real time, because nothing can switch the pacing or pass a turn.
    /// </param>
    /// <exception cref="ArgumentException">A name is missing, so no event could ever be claimed for it.</exception>
    public CombatIntentNames(string attack, string actionContract, TurnIntentNames? turn = null, string? nextMember = null)
    {
        Attack = Require(attack, nameof(attack));
        ActionContract = Require(actionContract, nameof(actionContract));
        Turn = turn;
        NextMember = nextMember is null ? null : Require(nextMember, nameof(nextMember));
    }

    /// <summary>The intent that orders the party to attack.</summary>
    public string Attack { get; }

    /// <summary>The payload contract that action arrives on.</summary>
    public string ActionContract { get; }

    /// <summary>The pace controls declared beside the act control, or null when the product declared none.</summary>
    public TurnIntentNames? Turn { get; }

    /// <summary>The declared digital control that cycles the acting member, or none.</summary>
    public string? NextMember { get; }

    private static string Require(string name, string parameterName) =>
        !string.IsNullOrWhiteSpace(name)
            ? name
            : throw new ArgumentException(
                $"The combat control '{parameterName}' declares no name, so no event could ever be claimed for it.",
                parameterName);
}

/// <summary>The payload action a screen sends to order the party to attack.</summary>
public static class CombatActions
{
    /// <summary>Orders the party to attack what it faces, exactly as the act key does.</summary>
    public const string Attack = "party.attack";

    /// <summary>Chooses the durable party member named in the payload.</summary>
    public const string SelectMember = "party.select-member";

    /// <summary>Chooses the next capable member in roster order.</summary>
    public const string NextMember = "party.next-member";
}

/// <summary>
/// Whether the player is asking the party to attack, read from the admitted input of each update.
/// </summary>
/// <remarks>
/// <para>
/// The act control is held rather than pressed, because the original's own control repeats while it is down: a
/// party that holds it keeps attacking as its members recover, and each attack is still gated by that member's
/// own recovery rather than by the key repeating. So a hold is remembered between updates exactly as a held
/// movement key is.
/// </para>
/// <para>
/// A control a product maps as <c>held</c> arrives as a state rather than as a transition: the engine reports it
/// for every update it stays down and nothing once it comes up, so this update's held events replace the
/// previous update's rather than accumulating. A direct claim and a payload action have no release behind them,
/// so each asks for exactly the update it arrives in.
/// </para>
/// </remarks>
public sealed class CombatInput
{
    private readonly byte[] _attack;
    private readonly string _actionContract;
    private bool _held;
    private bool _stateDriven;

    /// <summary>Creates the reader for the declared act control.</summary>
    /// <param name="names">The act control the host declared.</param>
    public CombatInput(CombatIntentNames names)
    {
        ArgumentNullException.ThrowIfNull(names);
        _attack = Encoding.UTF8.GetBytes(names.Attack);
        _actionContract = names.ActionContract;
    }

    /// <summary>Whether this update's input orders the party to attack.</summary>
    /// <param name="inbox">The update's input.</param>
    public bool Read(ActionInbox inbox)
    {
        ArgumentNullException.ThrowIfNull(inbox);
        bool state = false;
        bool claimed = false;
        bool pressed = false;
        foreach (ProductInputEvent inputEvent in inbox.Digital)
        {
            if (!inputEvent.Intent.Span.SequenceEqual(_attack)) continue;
            if (inputEvent.Edge == InputEdge.Held) state = true;
            else if (inputEvent.Edge == InputEdge.Pressed) _held = pressed = true;
            else if (inputEvent.Edge == InputEdge.Released) _held = false;
            else if (inputEvent.Phase == InputPhase.DirectUi || inputEvent.Provenance == InputProvenance.DirectUi) claimed = true;
        }

        if (inbox.Take(_actionContract, CombatActions.Attack).Count > 0) claimed = true;

        // A control that arrives as state is whatever this update said and nothing else, so a key the player let
        // go of stops ordering attacks.
        _stateDriven = state;
        Struck = claimed || pressed;
        return _held || _stateDriven || claimed;
    }

    /// <summary>
    /// Whether the last update carried a press of its own — a pressed edge, or a panel's control, which is always one
    /// press — rather than only a control still held from an earlier update.
    /// </summary>
    public bool Struck { get; private set; }

    /// <summary>Drops the hold, which a change of pacing asks for.</summary>
    public void Release()
    {
        _held = false;
        _stateDriven = false;
    }
}
