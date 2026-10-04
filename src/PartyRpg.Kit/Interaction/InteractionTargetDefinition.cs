using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Interaction;

/// <summary>What one piece of content offers the party, as the ruleset answers it.</summary>
/// <remarks>
/// <para>
/// This is the ruleset's whole answer about a placement: what kind of thing it is, what a person calls it,
/// which use applies, how close the party must stand, what it currently reads as, what that use requires,
/// and what it costs. When the target is a person, the ruleset may also state its disposition so the
/// adventure frame can distinguish a peaceful speaker from an object before an intentional attack. The kit
/// discovers placements from the place's content and asks for this; a placement
/// the ruleset answers nothing about is not usable and is not a silent failure, because it was never a
/// target. What guards a target — a trap — is a second answer asked for when it is used, because what the
/// party brings to it is read from the party.
/// </para>
/// <para>
/// The state word is part of the answer rather than a second fact beside it because only the ruleset knows
/// how content's own state and what the party has done to it become one word: a door's stored position, a
/// container's emptiness, and a lever's throw are this game's readings of content, and the kit carries
/// whatever word comes back without knowing what it means. What a trap makes of that word travels with the
/// trap, for the same reason.
/// </para>
/// <para>
/// A definition is a value about content, never about one visit: two parties that walk into the same place
/// read the same definition, and what has already happened to the target travels beside it as state. Its
/// members are settable through a <c>with</c> so a rule can answer about one placement by adjusting what it
/// answers about its kind — a door with a lock, a container that has been emptied — without a type per case.
/// </para>
/// </remarks>
public sealed record InteractionTargetDefinition
{
    /// <summary>Creates a target definition.</summary>
    /// <param name="kind">What kind of thing the target is.</param>
    /// <param name="name">What a person calls it, which must not be blank.</param>
    /// <param name="verb">Which use applies to it.</param>
    /// <param name="reach">
    /// How far from the target the party may stand and still use it, in the place's own units. It is the
    /// ruleset's policy because it is a distance a game tunes; content that states its own range passes it
    /// through here.
    /// </param>
    /// <param name="state">
    /// The word the target currently reads as — what the party has already done to it, or what content says
    /// it is when nothing has. Empty is a target with no state to report, such as one that is the same every
    /// time it is used.
    /// </param>
    /// <param name="requires">What the use requires, in the order the checks happen; empty when it requires nothing.</param>
    /// <param name="disposition">
    /// The ruleset's reading of the target's disposition, such as <c>peaceful</c> for a person the party may
    /// talk to; empty when the target has no disposition to show, such as an entrance or container.
    /// </param>
    /// <exception cref="ArgumentException">The name is blank, which names nothing a person could be shown.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The reach is not a finite, positive distance.</exception>
    public InteractionTargetDefinition(
        InteractionTargetKind kind,
        string name,
        InteractionVerb verb,
        double reach,
        string state = "",
        IReadOnlyList<InteractionRequirement>? requires = null,
        string disposition = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!double.IsFinite(reach) || reach <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(reach),
                reach,
                "A target's reach must be a finite, positive distance; a target nobody can be close enough to is not usable.");
        }

        Kind = kind;
        Name = name;
        Verb = verb;
        Reach = reach;
        State = state;
        Requires = requires ?? [];
        Disposition = disposition;
    }

    /// <summary>What kind of thing the target is.</summary>
    public InteractionTargetKind Kind { get; init; }

    /// <summary>What a person calls the target.</summary>
    public string Name { get; init; }

    /// <summary>Which use applies to it.</summary>
    public InteractionVerb Verb { get; init; }

    /// <summary>How far from the target the party may stand and still use it.</summary>
    public double Reach { get; init; }

    /// <summary>The word the target currently reads as, or empty when it has no state to report.</summary>
    public string State { get; init; }

    /// <summary>What the use requires, in the order the checks happen.</summary>
    public IReadOnlyList<InteractionRequirement> Requires { get; init; }

    /// <summary>
    /// The ruleset's word for the target's disposition, or empty when the target is not a being whose disposition
    /// is meaningful. This is presentation context, not a combat permission: an intentional attack still goes
    /// through the combat owner's existing provocation path.
    /// </summary>
    public string Disposition { get; init; }

    /// <summary>
    /// How high above its placement's feet the reticle aims at the target and judges it in sight and in reach, in the
    /// place's own units, in place of the height the place centres a body at. Zero keeps that body-centre height; a thing
    /// standing on its point, such as an object on a shelf, is aimed at its own middle.
    /// </summary>
    public double AimHeight { get; init; }

    /// <summary>
    /// How far the target's body reaches out from its point, in the place's own units: collision within that distance of
    /// the point does not hide it (the Engine's endpoint tolerance), so a thing set into a niche or against a wall is seen
    /// by its front rather than hidden by the stone its point stands in. Zero judges sight to the point itself.
    /// </summary>
    public double Radius { get; init; }
}
