using PartyRpg.Kit.Conversation;
using PartyRpg.Kit.Knowledge;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.World;

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

/// <summary>What one use made of another target of the same place.</summary>
/// <remarks>
/// A lever that opens a door is a use of the lever whose outcome changes the door: the door's state is the
/// ruleset's word, recorded by the mechanism under the door's own identity exactly as a use of the door would
/// record it, so the door afterwards reads as what the lever left it.
/// </remarks>
/// <param name="Target">The other target's placement in the same place.</param>
/// <param name="State">The word its state becomes, which must not be blank.</param>
public readonly record struct InteractionTargetChange(PlacementContentId Target, string State);

/// <summary>What a use the party is allowed to make produces: either what happened, or why it did not.</summary>
/// <remarks>
/// <para>
/// The ruleset answers with this and the kit applies it, which is the split the whole mechanism rests on: a
/// rule says what a door becomes, what a search finds, what a sign says, or that the fixture raises an event
/// it will not run; the kit moves the party's own accounts, records the target's state, and reports it.
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
/// <para>
/// <b>What a use taught travels with it.</b> The rule that carried the use out is the owner of the moment
/// the party learned something — a search that yielded a thing worth knowing, an inscription it read, a
/// landmark whose effect it felt — so the discoveries are stated here and the mechanism hands them on. The
/// knowledge owner is what decides whether each is news, which is why an outcome reports every discovery
/// it made rather than filtering them itself.
/// </para>
/// </remarks>
public sealed record InteractionOutcome
{
    private InteractionOutcome(
        string state,
        string message,
        string residue,
        IReadOnlyList<InteractionItemYield> items,
        PartyCost gain,
        IReadOnlyList<KnowledgeReport> learned,
        IReadOnlyDictionary<string, long> kept,
        IReadOnlyList<InteractionTargetChange> changes,
        ConversationSubject? speaks,
        InteractionTravel? travels,
        InteractionRelocation? relocates,
        Refusal? refusal)
    {
        Travels = travels;
        Relocates = relocates;
        Kept = kept;
        Changes = changes;
        Speaks = speaks;
        State = state;
        Message = message;
        Residue = residue;
        Items = items;
        Gain = gain;
        Learned = learned;
        Refusal = refusal;
    }

    /// <summary>The use happened: this is what it made of the target.</summary>
    /// <param name="state">The word the target's state becomes, which must not be blank.</param>
    /// <param name="message">What the use did, in the words a person reads.</param>
    /// <param name="residue">What the use could not deliver, or empty when it delivered all of it.</param>
    /// <param name="items">The items the use gives the party, or empty when it gives none.</param>
    /// <param name="gain">What the use puts into the party's accounts, or nothing when it puts nothing there.</param>
    /// <param name="learned">What the use taught the party, or empty when it taught nothing.</param>
    /// <param name="kept">The values of the target's place the use changed, by name, or empty when it changed none.</param>
    /// <param name="changes">What the use made of other targets of the same place, or empty when it touched none.</param>
    /// <param name="speaks">Whom the use hands the party to speak with, or null when it hands it to nobody.</param>
    /// <param name="travels">The journey the use takes the party on, or null when it takes none.</param>
    /// <param name="relocates">Where in its own place the use sets the party down, or null when it moves it nowhere.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="ArgumentException">The state or the message is blank, or a change states no word.</exception>
    public static InteractionOutcome Applied(
        string state,
        string message,
        string residue = "",
        IReadOnlyList<InteractionItemYield>? items = null,
        PartyCost? gain = null,
        IReadOnlyList<KnowledgeReport>? learned = null,
        IReadOnlyDictionary<string, long>? kept = null,
        IReadOnlyList<InteractionTargetChange>? changes = null,
        ConversationSubject? speaks = null,
        InteractionTravel? travels = null,
        InteractionRelocation? relocates = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        foreach (InteractionTargetChange change in changes ?? []) ArgumentException.ThrowIfNullOrWhiteSpace(change.State, nameof(changes));
        return new InteractionOutcome(state, message, residue, items ?? [], gain ?? PartyCost.Free, learned ?? [], kept ?? NothingKept, changes ?? [], speaks, travels, relocates, null);
    }

    /// <summary>The use happened and changed nothing, and this is why — a refusal with a stated consequence.</summary>
    /// <param name="refusal">Why nothing happened.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="ArgumentNullException">No refusal was given.</exception>
    public static InteractionOutcome Refused(Refusal refusal) =>
        new(string.Empty, (refusal ?? throw new ArgumentNullException(nameof(refusal))).Message, string.Empty, [], PartyCost.Free, [], NothingKept, [], null, null, null, refusal);

    /// <summary>Whether the use happened. A refused outcome changed nothing at all.</summary>
    public bool IsApplied => Refusal is null;

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

    /// <summary>What the use taught the party, in the reporting owner's own words for each fact.</summary>
    public IReadOnlyList<KnowledgeReport> Learned { get; }

    /// <summary>
    /// The values of the target's place the use changed, by the ruleset's own names; the mechanism writes them
    /// into the place's ledger beside the target's state, and a refused use changes none.
    /// </summary>
    public IReadOnlyDictionary<string, long> Kept { get; }

    /// <summary>
    /// What the use made of other targets of the same place, which the mechanism records under each target's
    /// own identity when the use is applied; a refused use changes none.
    /// </summary>
    public IReadOnlyList<InteractionTargetChange> Changes { get; }

    /// <summary>
    /// Whom the use hands the party to speak with, or null: a fixture whose event calls somebody over opens the
    /// conversation with them the way using a person would.
    /// </summary>
    public ConversationSubject? Speaks { get; }

    /// <summary>
    /// The journey the use takes the party on, or null: a face that leads into a cave, a shrine that carries the
    /// party to another region. The mechanism records the use in the place it was made in first, and the world
    /// then takes the journey through its one transition path.
    /// </summary>
    public InteractionTravel? Travels { get; }

    /// <summary>
    /// Where in the place it stands in the use sets the party down, or null: a teleport pad. A use that also takes a
    /// journey leaves the place, and the journey is what is taken.
    /// </summary>
    public InteractionRelocation? Relocates { get; }

    /// <summary>The refusal, or null when the use happened.</summary>
    /// <summary>
    /// Whether the use reached somebody's door and the party was kept outside: whoever stands there is not spoken with,
    /// and the use's message says why — a house whose own event shut it, a door that led elsewhere instead.
    /// </summary>
    /// <remarks>
    /// A use that reaches somebody opens the conversation with them (<c>SessionActs</c>); a ruleset whose answer to a
    /// door is that nobody answers it states that here rather than leaving the session to open a conversation the
    /// door never let the party into.
    /// </remarks>
    public bool KeptOut { get; init; }

    public Refusal? Refusal { get; }

    private static readonly IReadOnlyDictionary<string, long> NothingKept = new Dictionary<string, long>();
}
