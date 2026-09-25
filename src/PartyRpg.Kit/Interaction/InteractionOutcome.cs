using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Interaction;

/// <summary>One kind of item a use gives the party, and how many of it.</summary>
/// <remarks>
/// A yield names a definition rather than an instance: the instance does not exist until the party mints its
/// durable identity, and the party's own acquisition path is what mints it. What a search finds is therefore
/// content's answer about kinds and counts, and the kit's own bookkeeping about instances.
/// </remarks>
/// <param name="Definition">The item definition the use gives.</param>
/// <param name="Count">How many of it, which must be at least one.</param>
public readonly record struct InteractionItemYield
{
    /// <summary>Creates a yield.</summary>
    /// <param name="definition">The item definition.</param>
    /// <param name="count">How many, which must be at least one.</param>
    /// <exception cref="ArgumentOutOfRangeException">The count is below one, which gives nothing.</exception>
    public InteractionItemYield(ItemDefinitionId definition, int count = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        Definition = definition;
        Count = count;
    }

    /// <summary>The item definition the use gives.</summary>
    public ItemDefinitionId Definition { get; }

    /// <summary>How many of it.</summary>
    public int Count { get; }
}

/// <summary>What a use the party is allowed to make produces: either what happened, or why it did not.</summary>
/// <remarks>
/// <para>
/// The ruleset answers with this and the kit applies it, which is the split the whole mechanism rests on: a
/// rule says what a door becomes, what a search finds, what a sign says, or that the fixture raises an event
/// nothing executes; the kit moves the party's own accounts, records the target's state, and reports it.
/// </para>
/// <para>
/// <b>A refusal is an outcome too.</b> A door that already stands open, a fixture whose event nothing can
/// run, and a sign nobody can read are all answers a player must see, so they are stated here with a code
/// and a sentence rather than being expressed by applying nothing.
/// </para>
/// <para>
/// <b>The residue is what a use could not deliver.</b> Where a mechanism behind a use is not built yet, the
/// success states the part it did not carry out beside the part it did — a door that opens in state while
/// its collision still stands is a fact about this build, and a report that hid it would be claiming a
/// passage the party cannot walk.
/// </para>
/// </remarks>
public sealed record InteractionOutcome
{
    private InteractionOutcome(
        bool isApplied,
        string state,
        string message,
        string residue,
        IReadOnlyList<InteractionItemYield> items,
        PartyCost gain,
        PartyRefusal? refusal)
    {
        IsApplied = isApplied;
        State = state;
        Message = message;
        Residue = residue;
        Items = items;
        Gain = gain;
        Refusal = refusal;
    }

    /// <summary>The use happened: this is what it made of the target.</summary>
    /// <param name="state">The word the target's state becomes, which must not be blank.</param>
    /// <param name="message">What the use did, in the words a person reads.</param>
    /// <param name="residue">What the use could not deliver, or empty when it delivered all of it.</param>
    /// <param name="items">The items the use gives the party, or empty when it gives none.</param>
    /// <param name="gain">What the use puts into the party's accounts, or nothing when it puts nothing there.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="ArgumentException">The state or the message is blank.</exception>
    public static InteractionOutcome Applied(
        string state,
        string message,
        string residue = "",
        IReadOnlyList<InteractionItemYield>? items = null,
        PartyCost? gain = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        return new InteractionOutcome(true, state, message, residue, items ?? [], gain ?? PartyCost.Free, null);
    }

    /// <summary>The use happened and changed nothing, and this is why — a refusal with a stated consequence.</summary>
    /// <param name="code">A short stable code naming the kind of refusal.</param>
    /// <param name="message">Why nothing happened, in terms a person can act on.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="ArgumentException">The code or the message is blank.</exception>
    public static InteractionOutcome Refused(string code, string message) =>
        new(false, string.Empty, message, string.Empty, [], PartyCost.Free, new PartyRefusal(code, message));

    /// <summary>Whether the use happened. A refused outcome changed nothing at all.</summary>
    public bool IsApplied { get; }

    /// <summary>The word the target's state becomes, or empty on a refusal.</summary>
    public string State { get; }

    /// <summary>What the use did, in the words a person reads.</summary>
    public string Message { get; }

    /// <summary>What the use could not deliver, or empty when it delivered all of it.</summary>
    public string Residue { get; }

    /// <summary>The items the use gives the party.</summary>
    public IReadOnlyList<InteractionItemYield> Items { get; }

    /// <summary>What the use puts into the party's accounts.</summary>
    public PartyCost Gain { get; }

    /// <summary>The refusal, or null when the use happened.</summary>
    public PartyRefusal? Refusal { get; }
}
