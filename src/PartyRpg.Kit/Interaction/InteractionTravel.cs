using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Interaction;

/// <summary>A transition a use takes the party through: the way it leads, and what kind of travel that is.</summary>
/// <remarks>
/// <para>
/// A face a party clicks to enter a cave, a shrine that carries it to another region, and a plate in the floor
/// that drops it to the ground below are all uses whose outcome is a journey. The rule that judged the use names
/// the transition; the world takes it through its one transition path (<c>SessionWorld.Travel</c>), so the
/// journey is priced, charged and arrived at exactly as a walk-in or a boarded fare is, and there is no second way
/// between places.
/// </para>
/// <para>
/// The kind is the use's statement of what the party did, as it is for every other caller of the transition path.
/// A fare is a counter's and never a use's, and the world's own scripted moves have no place to be used from, so
/// neither is a kind a use can take.
/// </para>
/// </remarks>
public sealed record InteractionTravel
{
    /// <summary>Creates the journey a use takes.</summary>
    /// <param name="transition">The transition, which the place the use was made in must issue.</param>
    /// <param name="kind">What kind of travel the journey is.</param>
    /// <exception cref="ArgumentNullException">The transition is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The kind is a fare or the world's own scripted move.</exception>
    public InteractionTravel(PlaceTransition transition, TransitionKind kind)
    {
        ArgumentNullException.ThrowIfNull(transition);
        if (kind is TransitionKind.PaidService or TransitionKind.Scripted)
        {
            throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "A use leads the party from where it stands: a fare is boarded at the counter that sells it and a scripted move is the world's own, so neither is a journey a use can take.");
        }

        Transition = transition;
        Kind = kind;
    }

    /// <summary>The transition the use takes.</summary>
    public PlaceTransition Transition { get; }

    /// <summary>What kind of travel the journey is, which the transition path prices it as.</summary>
    public TransitionKind Kind { get; }
}

/// <summary>Where a use sets the party down in the place it already stands in.</summary>
/// <remarks>
/// A teleport pad in a dungeon, or a plate that drops the party to another spot of the same region, moves the party
/// without leaving the place: no transition is crossed, nothing is charged, and the party's pose is asserted as a
/// script asserts it (<see cref="Party.PartyPoseOwner.Enter"/>). The facing is kept when the use states none.
/// </remarks>
/// <param name="X">Where along the place's first axis.</param>
/// <param name="Y">Where along the place's second axis.</param>
/// <param name="Z">Where in height.</param>
/// <param name="Yaw">The facing the party is set down with, or null to keep its own.</param>
public sealed record InteractionRelocation(double X, double Y, double Z, double? Yaw);
