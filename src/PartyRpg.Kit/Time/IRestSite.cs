using PartyRpg.Kit.Party;
using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Time;

/// <summary>
/// Where the party stands when it stops: the place, where in it the party is, and what lives there.
/// </summary>
/// <remarks>
/// <para>
/// This is the seam the live world answers, so a rest is judged against the world as it stands at the moment
/// of the command rather than against a list somebody wrote down. The party's own pose travels here because
/// whether a creature is near is a distance, and the place travels whole — with the content entry it was read
/// from — because whether a party may lie down in the open, what the ground costs, and how dangerous the
/// night is are this game's readings of its own content, which this layer has no opinion about.
/// </para>
/// <para>
/// A session with no world has no site, and a rest is then refused by name rather than judged against an
/// invented place: the same honesty with which an interaction that needs a party is unmet when the session
/// holds none.
/// </para>
/// </remarks>
public interface IRestSite
{
    /// <summary>The place the party is in, which carries its kind and the content entry behind it.</summary>
    PlaceDefinition Place { get; }

    /// <summary>Where in that place the party stands, in the place's own coordinates.</summary>
    PlacePose Pose { get; }

    /// <summary>
    /// What lives in the place right now, in content order: the entities the party's own visit populated.
    /// </summary>
    /// <remarks>
    /// This is what a ruleset reads to decide whether anything hostile is near enough to keep the party from
    /// lying down. The kit does not know what a hostile is — a spawn point, a wandering monster, and a
    /// townsfolk are content's words — so it hands the entities over and the policy answers.
    /// </remarks>
    IReadOnlyList<PlacePopulationEntity> Population { get; }
}

/// <summary>What one stop is judged against: the kind asked for, where the party stands, and the one clock.</summary>
/// <remarks>
/// The party and the clock travel whole rather than as answers the kit thought a rule would want, because
/// what a night costs, whether it may happen here, and what breaks it are the ruleset's decisions over its
/// own content and the party's own state.
/// </remarks>
/// <param name="Kind">What the party asked for.</param>
/// <param name="Site">Where the party stands.</param>
/// <param name="Party">The party that would sleep or wait.</param>
/// <param name="Clock">The session's one clock, which is where the moment being judged is read from.</param>
public sealed record RestRequest(RestKind Kind, IRestSite Site, PartyEntity Party, GameClock Clock);
