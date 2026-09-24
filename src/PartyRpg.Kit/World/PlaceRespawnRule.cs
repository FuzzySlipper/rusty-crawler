using PartyRpg.Kit.Content;

namespace PartyRpg.Kit.World;

/// <summary>
/// How long a place's population lasts before the world restores it: a whole number of game days taken
/// from the place's own content entry, or a value this rule carries for places that declare none.
/// </summary>
/// <remarks>
/// The interval is deliberately not a constant here. A per-place interval is authored data — one place
/// is a quiet road, another is a nest that fills again quickly — so it comes from the place's content
/// entry, and a ruleset that wants a policy of its own supplies an explicit value instead. Two of the
/// three modes exist so neither story has to be faked: <see cref="FromContent"/> is content's answer for
/// every place, <see cref="FromContentOr"/> adds a policy default for entries that stay silent, and
/// <see cref="Every"/> ignores content entirely.
/// </remarks>
public sealed class PlaceRespawnRule
{
    /// <summary>The place-content field that carries a place's own reset interval, in game days.</summary>
    public const string RespawnDaysField = "respawnDays";

    private readonly bool _readsContent;
    private readonly int? _everyDays;
    private readonly int? _fallbackDays;

    private PlaceRespawnRule(bool readsContent, int? everyDays, int? fallbackDays)
    {
        _readsContent = readsContent;
        _everyDays = everyDays;
        _fallbackDays = fallbackDays;
    }

    /// <summary>Reads every place's interval from its own content entry.</summary>
    /// <remarks>
    /// An entry that declares no interval puts that place on no schedule: the ledger never restores its
    /// population on its own. That is the honest reading of silence, and it is what an interior whose
    /// occupants are defeated once and stay defeated looks like in content.
    /// </remarks>
    public static PlaceRespawnRule FromContent() => new(true, null, null);

    /// <summary>Reads each place's interval from content, falling back to a policy value when it declares none.</summary>
    /// <param name="fallbackDays">The interval to use for a place whose content entry declares none, in game days.</param>
    public static PlaceRespawnRule FromContentOr(int fallbackDays)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(fallbackDays);
        return new PlaceRespawnRule(true, null, fallbackDays);
    }

    /// <summary>Puts every place on one interval, whatever its content entry says.</summary>
    /// <param name="days">The interval every place resets on, in game days.</param>
    public static PlaceRespawnRule Every(int days)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(days);
        return new PlaceRespawnRule(false, days, null);
    }

    /// <summary>
    /// The whole game days a place's population lasts before it is restored, or null when the place is
    /// on no reset schedule.
    /// </summary>
    /// <remarks>
    /// A place that declares an interval the world cannot use — a negative count, or a fractional one —
    /// fails here by name instead of being rounded to something plausible: the world would otherwise
    /// silently reset a place at the wrong pace, which is far harder to notice than a refusal.
    /// </remarks>
    /// <param name="place">The place whose interval to resolve.</param>
    public int? DaysFor(PlaceDefinition place)
    {
        ArgumentNullException.ThrowIfNull(place);
        if (!_readsContent) return _everyDays;
        if (!place.Source.Has(RespawnDaysField)) return _fallbackDays;

        int? days = place.Source.GetInt32(RespawnDaysField);
        if (days is not null && days >= 0) return days;

        string message = days is null
            ? $"Place '{place.Id}' declares '{RespawnDaysField}' as something other than a whole number of game days, so it states no usable reset interval."
            : $"Place '{place.Id}' declares a reset interval of {days} game days; a reset interval counts forward in whole days or is left out entirely.";
        throw new ContentValidationException(
            message,
            [new ContentValidationIssue("place-respawn-interval-invalid", message, place.Id.Value)]);
    }
}
