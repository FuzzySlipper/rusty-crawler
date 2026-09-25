using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Sessions;

/// <summary>
/// The creation a session holds while a party is being made: the flow that owns the choices, the factory
/// that builds the accepted party, and the world that party walks into.
/// </summary>
/// <remarks>
/// <para>
/// Both factories are supplied by whoever owns the rules the party obeys, because both are policy: a party
/// is built through the product's one party factory, so the created party and the restored party are the
/// same durable shape, and the world is composed once there is a party whose accounts it charges. That is
/// why a session which creates composes neither until the party is accepted: a world built before its party
/// would have no larder to take a road's provisions from, and a party built twice would be two bands for
/// one expedition.
/// </para>
/// <para>
/// Nothing here mints an identity, advances a clock, or steps anything. The flow refuses what is illegal,
/// the factory builds what the flow finished, and the world is handed the party that was just created.
/// </para>
/// </remarks>
public sealed class SessionCreation
{
    /// <summary>States the creation a session holds.</summary>
    /// <param name="flow">The flow that owns the party being assembled and refuses every illegal choice.</param>
    /// <param name="buildParty">Builds the party a finished flow describes, through the product's one factory.</param>
    /// <param name="composeWorld">
    /// Composes the world the accepted party walks into. It is asked once, with the created party, so the
    /// accounts a journey charges are the party's own; it may answer null, which is what content that
    /// declares no places gets.
    /// </param>
    /// <exception cref="ArgumentNullException">The flow or one of the factories is missing.</exception>
    public SessionCreation(
        PartyCreationFlow flow,
        Func<PartyCreation, PartyEntity> buildParty,
        Func<PartyEntity, SessionWorld?> composeWorld)
    {
        ArgumentNullException.ThrowIfNull(flow);
        ArgumentNullException.ThrowIfNull(buildParty);
        ArgumentNullException.ThrowIfNull(composeWorld);
        Flow = flow;
        BuildParty = buildParty;
        ComposeWorld = composeWorld;
    }

    /// <summary>The flow that owns the party being assembled.</summary>
    public PartyCreationFlow Flow { get; }

    /// <summary>Builds the party a finished flow describes.</summary>
    public Func<PartyCreation, PartyEntity> BuildParty { get; }

    /// <summary>Composes the world the accepted party walks into.</summary>
    public Func<PartyEntity, SessionWorld?> ComposeWorld { get; }
}
