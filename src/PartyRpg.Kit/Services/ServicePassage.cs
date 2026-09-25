using PartyRpg.Kit.Party;
using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Services;

/// <summary>Permission the party bought to travel to one place, with how long the journey takes.</summary>
/// <remarks>
/// <para>
/// <b>A passage is party-carried state, not a counter's record.</b> The party hands its coin over at the
/// counter and the ticket is then the party's, so it travels as a party-wide effect: the same state a guild
/// membership is, written by the purchase, read by the travel policy, and already recorded by a save. The
/// effect's identity names the place the passage reaches and its magnitude states how many game days the
/// journey takes, so one piece of state carries both what the ticket is for and what boarding it costs in
/// time.
/// </para>
/// <para>
/// <b>The kit names the state; the game names the place.</b> Nothing here knows what a place is beyond its
/// kit identity or what a journey should cost: the ruleset decides which counters sell passages, what they
/// charge, and whether a transition may be taken on the strength of one. What the kit supplies is the one
/// way a passage is written down and read back, so the counter that sells it and the road that honours it
/// cannot disagree about what the party holds.
/// </para>
/// </remarks>
/// <param name="Destination">The place the passage reaches.</param>
/// <param name="Days">How many game days the journey takes, which is at least one.</param>
/// <exception cref="ArgumentOutOfRangeException">The journey takes no time at all, which is not a journey.</exception>
public readonly record struct ServicePassage(PlaceId Destination, int Days)
{
    /// <summary>How many game days a passage to a place takes, or zero when the party holds none.</summary>
    /// <param name="party">The party whose carried state is read.</param>
    /// <param name="destination">The place the passage would reach.</param>
    /// <returns>The journey's days, or zero when no passage to that place is held.</returns>
    /// <exception cref="ArgumentNullException">The party is null.</exception>
    public static int DaysTo(PartyEntity party, PlaceId destination)
    {
        ArgumentNullException.ThrowIfNull(party);
        return party.Effects.Has(EffectFor(destination)) ? party.Effects.MagnitudeOf(EffectFor(destination)) : 0;
    }

    /// <summary>Records a passage on the party, replacing any passage to the same place.</summary>
    /// <param name="party">The party that bought it.</param>
    /// <param name="destination">The place the passage reaches.</param>
    /// <param name="days">How many game days the journey takes, which must be at least one.</param>
    /// <exception cref="ArgumentNullException">The party is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The journey takes no time at all.</exception>
    public static void Grant(PartyEntity party, PlaceId destination, int days)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentOutOfRangeException.ThrowIfLessThan(days, 1);
        party.Effects.Apply(new PartyEffect(EffectFor(destination), days));
    }

    /// <summary>
    /// Spends the passage to a place, which is what tearing the ticket on boarding is.
    /// </summary>
    /// <param name="party">The party that holds it.</param>
    /// <param name="destination">The place the passage reaches.</param>
    /// <returns>Whether the party held one.</returns>
    /// <exception cref="ArgumentNullException">The party is null.</exception>
    public static bool Spend(PartyEntity party, PlaceId destination)
    {
        ArgumentNullException.ThrowIfNull(party);
        return party.Effects.Remove(EffectFor(destination));
    }

    /// <summary>The effect identity a passage to a place is carried under.</summary>
    /// <remarks>
    /// The identity is derived from the place rather than authored, so the counter that grants a passage and
    /// the road that honours it compute the same one from the same place and no content has to carry a name
    /// for it. The prefix keeps a passage from colliding with a game's own effects, which are content's
    /// names.
    /// </remarks>
    /// <param name="destination">The place the passage reaches.</param>
    public static EffectId EffectFor(PlaceId destination) =>
        new(string.Concat("passage:", destination.Value));
}
