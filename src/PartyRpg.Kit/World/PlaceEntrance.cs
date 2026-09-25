namespace PartyRpg.Kit.World;

/// <summary>
/// One way out of a place that the party takes by walking into it: the transition it takes, the reach it
/// must come inside, and what kind of travel the walk-in is.
/// </summary>
/// <remarks>
/// <para>
/// A place's exits are not all edges of its geometry. A region has the mouths of the interiors cut into
/// it, an interior has the doorway it was entered through, and both are a transition the place issues
/// together with somewhere in the place the party can walk into — so both are this one value rather than
/// two mechanisms, and there is no second path between places for either of them.
/// </para>
/// <para>
/// The reach is a ball in the place's own coordinates, and it is content's to state: only content knows
/// what its trigger geometry is. The kit never derives a reach from a transition's destination, because a
/// destination says where the party arrives and nothing about where it left from — a party standing at
/// the arrival point of the road back is not standing at the entrance it walks into.
/// </para>
/// <para>
/// Walking in is an <em>entry</em>: the world takes the transition on the step that carries the party
/// from outside the reach to inside it. A party that is already inside — because the reach covers where
/// a transition put it, or because it has not stepped out yet — is not entering, which is what stops a
/// door the party arrives at from throwing it straight back.
/// </para>
/// </remarks>
public sealed record PlaceEntrance
{
    /// <summary>Creates an entrance.</summary>
    /// <param name="transition">The transition walking into this entrance takes, which a place must issue.</param>
    /// <param name="kind">What kind of travel the walk-in is, which the transition path prices it as.</param>
    /// <param name="x">The reach's centre along the place's first axis.</param>
    /// <param name="y">The reach's centre along the place's second axis.</param>
    /// <param name="z">The reach's centre in height.</param>
    /// <param name="radius">How far from the centre the party counts as inside the entrance.</param>
    /// <param name="source">What declared the entrance, so a refusal or a report can name it.</param>
    /// <exception cref="ArgumentNullException">The transition is null.</exception>
    /// <exception cref="ArgumentException">The world issues the transition, or the entrance has no name.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The kind is not a walk-in, a coordinate is not a number, or the reach has no positive radius.
    /// </exception>
    public PlaceEntrance(
        PlaceTransition transition,
        TransitionKind kind,
        double x,
        double y,
        double z,
        double radius,
        string source)
    {
        ArgumentNullException.ThrowIfNull(transition);
        if (transition.From is not { } from)
        {
            throw new ArgumentException(
                $"Transition '{transition.Source}' is issued by the world rather than by a place, so there is no place whose walking party could enter it.",
                nameof(transition));
        }

        if (kind is not (TransitionKind.Walking or TransitionKind.Entrance))
        {
            throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "An entrance is walked into, so the travel it takes is walking across an edge or passing through an entrance; a fare or a portal is not a walk-in and must not become one by being reachable on foot.");
        }

        RequireNumber(x, nameof(x), "The reach's centre must be a number on every axis, or no step could ever be inside it.");
        RequireNumber(y, nameof(y), "The reach's centre must be a number on every axis, or no step could ever be inside it.");
        RequireNumber(z, nameof(z), "The reach's centre must be a number on every axis, or no step could ever be inside it.");
        if (!double.IsFinite(radius) || radius <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(radius),
                radius,
                "An entrance's reach must be a positive, finite distance; an entrance nobody can be inside is not an entrance.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        Transition = transition;
        Kind = kind;
        X = x;
        Y = y;
        Z = z;
        Radius = radius;
        Source = source;
        Place = from;
    }

    /// <summary>The transition walking into this entrance takes.</summary>
    public PlaceTransition Transition { get; }

    /// <summary>What kind of travel the walk-in is, which the transition path asks the cost rule about.</summary>
    public TransitionKind Kind { get; }

    /// <summary>The place the entrance stands in, which is the place the transition leaves.</summary>
    public PlaceId Place { get; }

    /// <summary>The reach's centre along the place's first axis.</summary>
    public double X { get; }

    /// <summary>The reach's centre along the place's second axis.</summary>
    public double Y { get; }

    /// <summary>The reach's centre in height.</summary>
    public double Z { get; }

    /// <summary>How far from the centre the party counts as inside the entrance.</summary>
    public double Radius { get; }

    /// <summary>What declared the entrance, so a refusal or a report can name it.</summary>
    public string Source { get; }

    /// <summary>Whether the party's pose is inside the entrance's reach.</summary>
    /// <param name="pose">The pose to test, in the entrance's own place.</param>
    public bool Contains(PlacePose pose)
    {
        double dx = pose.X - X;
        double dy = pose.Y - Y;
        double dz = pose.Z - Z;
        return (dx * dx) + (dy * dy) + (dz * dz) <= Radius * Radius;
    }

    /// <inheritdoc />
    public override string ToString() => $"{Source} in {Place} -> {Transition.To}";

    /// <summary>Fails when a reach coordinate is not a number, naming what the loss would be.</summary>
    private static void RequireNumber(double value, string name, string message)
    {
        if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(name, value, message);
    }
}
