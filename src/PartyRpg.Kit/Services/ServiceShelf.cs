using PartyRpg.Kit.Party;
using PartyRpg.Kit.Time;

namespace PartyRpg.Kit.Services;

/// <summary>One service's shelves: what it holds right now, and the deadline that fills them again.</summary>
/// <remarks>
/// <para>
/// The shelves are the service's own state and not a visit's: a party that buys out a shop and walks out
/// finds the same empty shelf when it comes back, and a party that never visits is not restocked at all
/// because nothing was ever laid out. The refresh is a repeating deadline on the session's one clock —
/// game time, never a frame count — and the interval is content's, from the schedule the building table
/// carries.
/// </para>
/// <para>
/// <b>What the party sold stays sold.</b> A refresh restores content's own lines to what they hold when
/// full; the lots the shop bought from the party are not content's and are left alone, because a shop that
/// forgot what it bought would sell back an item that no longer exists.
/// </para>
/// </remarks>
internal sealed class ServiceShelf
{
    private readonly List<ServiceStockLot> _lots;

    private ServiceShelf(ServiceDefinition service, List<ServiceStockLot> lots)
    {
        Service = service;
        _lots = lots;
    }

    /// <summary>The service whose shelves these are.</summary>
    internal ServiceDefinition Service { get; }

    /// <summary>The deadline the shelves refresh at, or null when content states no schedule or there is no clock.</summary>
    internal DeadlineId? Refresh { get; set; }

    /// <summary>The lots the shelves hold, content's own first and what the shop bought after them.</summary>
    internal IReadOnlyList<ServiceStockLot> Lots => _lots;

    /// <summary>Lays out a shelf from the lines content declares and the rule answered with.</summary>
    /// <param name="service">The service whose shelves these are.</param>
    /// <param name="lines">The lines the shelves hold when full.</param>
    internal static ServiceShelf From(ServiceDefinition service, IReadOnlyList<ServiceStockLine> lines) =>
        new(service, [.. lines.Select(ServiceStockLot.OfStock)]);

    /// <summary>Finds one lot by its identity, or null when the shelves hold none with it.</summary>
    /// <param name="id">The lot's identity.</param>
    internal ServiceStockLot? Lot(ServiceLotId id)
    {
        foreach (ServiceStockLot lot in _lots)
        {
            if (lot.Id == id) return lot;
        }

        return null;
    }

    /// <summary>Takes goods off a lot, and drops the lot when there is nothing left of it to keep.</summary>
    /// <remarks>
    /// Content's own line stays on the shelves at zero until the schedule fills it again: a shop that has
    /// sold out of something is a fact a player can read, and the line is what says so. A line the shop
    /// bought from the party is gone the moment it is bought back, because there was one of it and the
    /// party has it now.
    /// </remarks>
    /// <param name="lot">The lot being bought from.</param>
    /// <param name="count">How many are taken.</param>
    internal void Take(ServiceStockLot lot, int count)
    {
        lot.Take(count);
        if (lot.IsEmpty && lot.IsSale) _lots.Remove(lot);
    }

    /// <summary>Puts an item the party sold onto the shelves, held as the very instance it was.</summary>
    /// <param name="instance">The instance the party released.</param>
    /// <param name="value">What the shop paid for it, which is the base it prices the item back from.</param>
    internal void Accept(ItemInstance instance, int value) => _lots.Add(ServiceStockLot.OfSale(instance, value));

    /// <summary>Fills content's own lines again, leaving what the shop bought from the party where it lies.</summary>
    /// <param name="lines">The lines the shelves hold when full, as the rule answers them now.</param>
    internal void Restock(IReadOnlyList<ServiceStockLine> lines)
    {
        _lots.RemoveAll(lot => !lot.IsSale);
        _lots.InsertRange(0, lines.Select(ServiceStockLot.OfStock));
    }

    /// <summary>Whether the shelves hold anything at all, which is what a browse shows.</summary>
    internal bool IsEmpty => _lots.Count == 0;
}
