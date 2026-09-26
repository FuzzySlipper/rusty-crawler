using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Services;

/// <summary>One thing content puts on a service's shelves: a kind of item, how many, and what it is worth.</summary>
/// <remarks>
/// <para>
/// This is the line a shop's stock is declared as. The value is the base the price policy works from —
/// content's number, which a shop's multiplier and the party's own standing then adjust — so the same kind
/// of item can be dearer in one town than another without the mechanism knowing anything about either.
/// </para>
/// <para>
/// A line names a definition rather than an instance because the instances do not exist until the party
/// buys them: the party mints the durable identity, exactly as it does for anything else it takes. What a
/// shelf holds after a sale is a different thing, and a shelf lot carries it.
/// </para>
/// </remarks>
public sealed record ServiceStockLine
{
    /// <summary>Creates a stock line.</summary>
    /// <param name="definition">The item definition the line offers.</param>
    /// <param name="count">How many the shelves hold when they are full, which must be at least one.</param>
    /// <param name="value">What one of them is worth as a base price, which cannot be negative.</param>
    /// <param name="name">What a person reads for it, or empty to read the definition's own id.</param>
    /// <exception cref="ArgumentOutOfRangeException">The count is below one, or the value is negative.</exception>
    public ServiceStockLine(ItemDefinitionId definition, int count, int value, string name = "")
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        Definition = definition;
        Count = count;
        Value = value;
        Name = name;
    }

    /// <summary>The item definition the line offers.</summary>
    public ItemDefinitionId Definition { get; init; }

    /// <summary>How many the shelves hold when they are full.</summary>
    public int Count { get; init; }

    /// <summary>What one of them is worth as a base price.</summary>
    public int Value { get; init; }

    /// <summary>What a person reads for it, or empty to read the definition's own id.</summary>
    public string Name { get; init; }

    /// <summary>What the line reads as, which is content's own word for it when it states one.</summary>
    public string Label => Name.Length > 0 ? Name : Definition.Value;
}

/// <summary>What a lesson grants: a member's skill, a member's spell, or a party-wide effect such as a guild membership.</summary>
/// <remarks>
/// <para>
/// A lesson is one of the three things a service can teach, because those are the three the party's state
/// has: a skill on a member, a spell in a member's spellbook, and an effect acting on the whole band. A
/// guild's membership is the third kind — the ruleset names the effect, content states which effect a
/// membership is, and the service's access requirement reads the same effect back — so buying a membership
/// and checking one are one piece of party state rather than two that could disagree.
/// </para>
/// <para>
/// <b>A spell lesson is a book bought at a counter.</b> This game sells its spells as books, and the book is
/// consumed by the learning rather than carried: the counter's lesson names the spell, the price is the
/// book's own price, and what the party takes away is what the character learned. That is why a spell is a
/// lesson rather than a line of stock — a purchased book that landed in the pack would be a second thing to
/// carry and a second way to learn, and the shipped books have no use other than the one they teach.
/// </para>
/// <para>
/// Which of the three a lesson is is content's answer about that lesson, not a kind of service: the same
/// guild counter can teach a skill, sell its membership, and sell its school's spell books, and the
/// mechanism applies whichever the lesson says.
/// </para>
/// </remarks>
public enum ServiceLessonKind
{
    /// <summary>A member learns or improves a skill.</summary>
    Skill,

    /// <summary>A member learns a spell, which is what a spell book bought at a counter is.</summary>
    Spell,

    /// <summary>The whole party gains an effect, which is what a membership is.</summary>
    Effect,
}

/// <summary>One thing a service teaches: what it grants, how much of it, and what it charges.</summary>
/// <remarks>
/// A lesson names its subject by content identity — a skill, or the effect a membership is — because that
/// is what the party's own state is keyed by. Whether a member may learn it, how far the skill may be
/// taken, and whether the party already has the membership are the eligibility policy's answers, which is
/// why the lesson states what is taught and not who may be taught it.
/// </remarks>
public sealed record ServiceLesson
{
    /// <summary>Creates a lesson.</summary>
    /// <param name="kind">Whether the lesson grants a skill or a party-wide effect.</param>
    /// <param name="subject">The skill's id, or the effect's id, as content names it.</param>
    /// <param name="amount">The skill level the lesson reaches, or the effect's magnitude; at least one.</param>
    /// <param name="value">What the lesson is worth as a base fee, which cannot be negative.</param>
    /// <param name="name">What a person reads for it, or empty to read the subject's own id.</param>
    /// <param name="tier">
    /// The rung of a skill's ladder the lesson leaves a member at, where one is the first rung; a lesson
    /// never lowers a member who already stands higher. It is content's number because how many rungs a
    /// ladder has and what each unlocks are the ruleset's policy over its own definitions.
    /// </param>
    /// <exception cref="ArgumentException">The subject is blank, which teaches nothing.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The amount is below one, the value is negative, or the tier is below one.</exception>
    public ServiceLesson(ServiceLessonKind kind, string subject, int amount, int value, string name = "", int tier = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentOutOfRangeException.ThrowIfLessThan(amount, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(value);
        ArgumentOutOfRangeException.ThrowIfLessThan(tier, 1);
        Kind = kind;
        Subject = subject;
        Amount = amount;
        Value = value;
        Name = name;
        Tier = tier;
    }

    /// <summary>Whether the lesson grants a skill or a party-wide effect.</summary>
    public ServiceLessonKind Kind { get; init; }

    /// <summary>The skill's id, or the effect's id, as content names it.</summary>
    public string Subject { get; init; }

    /// <summary>The skill level the lesson reaches, or the effect's magnitude.</summary>
    public int Amount { get; init; }

    /// <summary>What the lesson is worth as a base fee.</summary>
    public int Value { get; init; }

    /// <summary>What a person reads for it, or empty to read the subject's own id.</summary>
    public string Name { get; init; }

    /// <summary>The rung of a skill's ladder the lesson leaves a member at; unused by an effect lesson.</summary>
    public int Tier { get; init; }

    /// <summary>What the lesson reads as, which is content's own word for it when it states one.</summary>
    public string Label => Name.Length > 0 ? Name : Subject;

    /// <inheritdoc />
    public override string ToString() => $"{Kind}:{Subject} {Amount}";
}

/// <summary>Which line of a service's shelves a command names.</summary>
/// <remarks>
/// <para>
/// A shelf can hold two lines of one definition — the shop's own stock, and the very item the party sold
/// it — and they are not interchangeable: one mints a new instance, the other returns the instance the
/// party brought in, with its damage and its enchantments intact. The lot identity is what keeps the two
/// apart, and it is what a browse publishes and a buy command echoes back.
/// </para>
/// <para>
/// A lot's identity is derived from what the lot holds (a definition, or an instance's durable identity),
/// so two projections of one shelf name the same lot the same way and a client that echoes an id from the
/// last projection is understood.
/// </para>
/// </remarks>
public readonly record struct ServiceLotId
{
    /// <summary>Creates a lot identity.</summary>
    /// <param name="value">The lot's identity, which must not be blank.</param>
    /// <exception cref="ArgumentException">The identity is blank, which names no lot.</exception>
    public ServiceLotId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>The lot's identity.</summary>
    public string Value { get; }

    /// <summary>The identity of a lot holding a definition's worth of goods, which is a shop's own stock.</summary>
    /// <param name="definition">The definition the line offers.</param>
    public static ServiceLotId OfStock(ItemDefinitionId definition) => new($"stock:{definition.Value}");

    /// <summary>The identity of a lot holding one instance the party sold, which is that very item.</summary>
    /// <param name="item">The instance's durable identity.</param>
    public static ServiceLotId OfSale(ItemInstanceId item) =>
        new(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"sold:{item.Value}"));

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>One line of a service's shelves as it stands now: what it holds and how much is left.</summary>
/// <remarks>
/// <para>
/// A lot is either content's own line — a definition, from which buying mints new instances — or an
/// instance the party sold, which the shop holds and can sell back as the very same item. The distinction
/// matters because a sold artifact's damage and enchantments are part of it: a shelf that minted a fresh
/// copy would be selling something the party never brought in.
/// </para>
/// <para>
/// A lot's remaining count is the shelf's own state, and it is what depletes when the party buys. What
/// fills it again is the service's refresh schedule, which is a deadline on the session's one clock.
/// </para>
/// </remarks>
public sealed class ServiceStockLot
{
    private readonly ItemInstance? _instance;

    private ServiceStockLot(ServiceLotId id, ItemDefinitionId definition, int count, int value, string label, ItemInstance? instance)
    {
        Id = id;
        Definition = definition;
        Count = count;
        Value = value;
        Label = label;
        _instance = instance;
    }

    /// <summary>The lot's identity, which is what a buy command names.</summary>
    public ServiceLotId Id { get; }

    /// <summary>The definition the lot's goods are copies of.</summary>
    public ItemDefinitionId Definition { get; }

    /// <summary>How many are left on the shelf.</summary>
    public int Count { get; private set; }

    /// <summary>What one of them is worth as a base price.</summary>
    public int Value { get; }

    /// <summary>What a person reads for the lot.</summary>
    public string Label { get; }

    /// <summary>Whether the lot holds one specific instance rather than a definition to mint from.</summary>
    public bool IsSale => _instance is not null;

    /// <summary>The instance the lot holds, or null when the lot is content's own stock.</summary>
    public ItemInstance? Instance => _instance;

    /// <summary>Whether the shelf still holds any of the lot.</summary>
    public bool IsEmpty => Count <= 0;

    /// <summary>States a lot from content's own stock.</summary>
    /// <param name="line">The line content declared.</param>
    internal static ServiceStockLot OfStock(ServiceStockLine line) =>
        new(ServiceLotId.OfStock(line.Definition), line.Definition, line.Count, line.Value, line.Label, null);

    /// <summary>States a lot holding one instance the party sold, which the shop can sell back.</summary>
    /// <param name="instance">The instance the party released.</param>
    /// <param name="value">What the shop paid for it, which is the base its own price is worked from.</param>
    internal static ServiceStockLot OfSale(ItemInstance instance, int value) =>
        new(ServiceLotId.OfSale(instance.Id), instance.Definition, instance.StackCount, value, instance.Definition.Value, instance);

    /// <summary>Takes goods off the shelf.</summary>
    /// <param name="count">How many to take, which must not exceed what the shelf holds.</param>
    /// <exception cref="ArgumentOutOfRangeException">The count is below one or above what the lot holds.</exception>
    internal void Take(int count)
    {
        if (count < 1 || count > Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(count),
                count,
                "A shelf gives what it holds and no more; a caller takes only after the count was judged against what is left.");
        }

        Count -= count;
    }

    /// <inheritdoc />
    public override string ToString() => $"{Label} x{Count} ({Id})";
}
