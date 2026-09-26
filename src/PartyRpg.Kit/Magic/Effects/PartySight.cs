namespace PartyRpg.Kit.Magic;

/// <summary>What a party can see by, as the clock's own daylight and a spell's own light decide it.</summary>
/// <remarks>
/// <para>
/// This is the mechanism's whole vocabulary for brightness. Whether a place is dark, what a light spell is
/// called, and how long one lasts are a game's answers; what the kit states is that the party either stands
/// in the clock's daylight, or carries a light of its own, or has neither — which is the fact every reader of
/// "can the party see" needs, and the one a panel shows.
/// </para>
/// <para>
/// <b>Unstated is not dark.</b> A session whose ruleset or clock says nothing about light publishes that it
/// cannot say, rather than reporting darkness: a panel that showed night where the game had not answered
/// would be inventing a fact about the world.
/// </para>
/// </remarks>
public enum PartySight
{
    /// <summary>Nothing in this session says what the party sees by.</summary>
    Unstated,

    /// <summary>The clock's own daylight window covers the moment.</summary>
    Daylight,

    /// <summary>The party carries a light of its own, and the daylight window does not cover the moment.</summary>
    Light,

    /// <summary>Neither daylight nor a carried light: the party is in the dark.</summary>
    Dark,
}

/// <summary>The one vocabulary sight is spelled with, on the wire and in a panel.</summary>
public static class PartySights
{
    /// <summary>The wire name for one reading.</summary>
    /// <param name="sight">The reading to spell.</param>
    /// <returns>The word the wire spells it as, empty for a session that stated none.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The reading has no wire name.</exception>
    public static string WireName(PartySight sight) => sight switch
    {
        PartySight.Unstated => string.Empty,
        PartySight.Daylight => "daylight",
        PartySight.Light => "light",
        PartySight.Dark => "dark",
        _ => throw new ArgumentOutOfRangeException(nameof(sight), sight, "Unknown party sight."),
    };
}

/// <summary>What the party can see by, as the game that owns light reads its own effects.</summary>
/// <remarks>
/// The reading is the effect path's because it is the only owner that knows which effect a light spell left,
/// and the clock's daylight window is what its answer is measured against. A game that states no light
/// answers <see cref="PartySight.Unstated"/>, and a panel then shows nothing rather than darkness.
/// </remarks>
public interface IPartySightRule
{
    /// <summary>What the party sees by now.</summary>
    PartySight Sight { get; }
}
