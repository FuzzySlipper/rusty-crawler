using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Services;

/// <summary>One thing a service operation acts on: a line of stock, an item the party holds, or a lesson.</summary>
/// <remarks>
/// <para>
/// The mechanism resolves what a command names into one of these before it asks policy anything, because
/// eligibility, price, and the change itself are all answers about a particular thing: this line of the
/// shelves, this instance the party carries, this lesson the counter teaches. Resolving first is also what
/// lets "there is no such item" and "the shelves are out of it" be different refusals rather than one.
/// </para>
/// <para>
/// The value is the base a price is worked from where the subject itself states one — a shelf line's worth,
/// a lesson's fee — and zero where only the ruleset can know it, which is an item the party is selling or
/// having repaired: its worth is the item table's, which the kit does not read.
/// </para>
/// </remarks>
public sealed record ServiceSubject
{
    private ServiceSubject(ServiceStockLot? lot, ItemInstance? item, ServiceLesson? lesson, int count, int value)
    {
        Lot = lot;
        Item = item;
        Lesson = lesson;
        Count = count;
        Value = value;
    }

    /// <summary>A line of the shelves, and how many of it the party is taking.</summary>
    /// <param name="lot">The lot the shelves hold.</param>
    /// <param name="count">How many to take, which must be at least one.</param>
    /// <exception cref="ArgumentNullException">The lot is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The count is below one, which buys nothing.</exception>
    public static ServiceSubject OfLot(ServiceStockLot lot, int count = 1)
    {
        ArgumentNullException.ThrowIfNull(lot);
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        return new ServiceSubject(lot, null, null, count, lot.Value);
    }

    /// <summary>An instance the party holds, which is what it sells, identifies, or repairs.</summary>
    /// <param name="item">The instance, which the party must hold.</param>
    /// <exception cref="ArgumentNullException">The instance is null.</exception>
    public static ServiceSubject OfItem(ItemInstance item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new ServiceSubject(null, item, null, item.StackCount, 0);
    }

    /// <summary>A lesson the counter teaches.</summary>
    /// <param name="lesson">The lesson.</param>
    /// <exception cref="ArgumentNullException">The lesson is null.</exception>
    public static ServiceSubject OfLesson(ServiceLesson lesson)
    {
        ArgumentNullException.ThrowIfNull(lesson);
        return new ServiceSubject(null, null, lesson, 1, lesson.Value);
    }

    /// <summary>The shelf lot the subject is, or null when it is not a purchase.</summary>
    public ServiceStockLot? Lot { get; }

    /// <summary>The instance the subject is, or null when it is not an item the party holds.</summary>
    public ItemInstance? Item { get; }

    /// <summary>The lesson the subject is, or null when it is not a lesson.</summary>
    public ServiceLesson? Lesson { get; }

    /// <summary>How many of the subject the operation acts on.</summary>
    public int Count { get; }

    /// <summary>The base value the price policy works from, or zero when only the ruleset can know it.</summary>
    public int Value { get; }

    /// <summary>
    /// What the subject's whole quantity is worth at that base value: the unit value times how many the
    /// operation acts on. A price rule that charges for goods rather than for one item prices from here, so
    /// the multiplication is stated once instead of being re-derived by every rule.
    /// </summary>
    public int TotalValue => Value * Count;

    /// <summary>The definition the subject is a copy of, or null when the subject is a lesson.</summary>
    public ItemDefinitionId? Definition => Lot?.Definition ?? Item?.Definition;

    /// <summary>What a person reads for the subject.</summary>
    public string Label =>
        Lot?.Label ?? Lesson?.Label ?? Item?.Definition.Value ?? string.Empty;

    /// <inheritdoc />
    public override string ToString() =>
        Lot is { } lot ? $"lot {lot.Id} x{Count}"
        : Lesson is { } lesson ? $"lesson {lesson}"
        : $"item {Item?.Id}";
}
