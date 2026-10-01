using PartyRpg.Kit.Movement;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Sessions;

namespace PartyRpg.Kit.Presentation;

/// <summary>One effect that spares the party some ground's harm, as the panel shows it.</summary>
/// <param name="Effect">The effect's identity.</param>
/// <param name="Name">What the game calls it.</param>
/// <param name="Member">The carrier's member identity.</param>
/// <param name="MemberName">The carrier's name.</param>
/// <param name="Everybody">Whether it spares the whole party; otherwise its carrier alone.</param>
public sealed record FootingShelterSnapshot(string Effect, string Name, string Member, string MemberName, bool Everybody)
{
    /// <summary>Writes one shelter.</summary>
    internal uint Write(UiValueBuilder builder) =>
        builder.Object(
            ("effect", builder.String(Effect)),
            ("name", builder.String(Name)),
            ("member", builder.String(Member)),
            ("memberName", builder.String(MemberName)),
            ("everybody", builder.Boolean(Everybody)));
}

/// <summary>
/// What the ground under the party does to it now, as the panel needs it: what it stands on, whether that harms it
/// and how often, how long before the next harm lands, and what spares whom.
/// </summary>
/// <remarks>
/// These are the world's own reading (<see cref="SessionWorld.Ground"/>) copied into one presentation value: the
/// footing is the mover's, the interval and who is spared are the game's hazard rule's, and the time before the next
/// harm is the calendar boundary the world counts harm at. The panel prints them and works nothing out. It is
/// written inside the movement block, which is read on every update, so it is never kept between readings.
/// </remarks>
/// <param name="Ground">The ground the party stands on, as content names it, or empty when it stands on nothing.</param>
/// <param name="Harmful">Whether standing there harms the party now.</param>
/// <param name="Every">How often it does, in game seconds; zero when it does not.</param>
/// <param name="NextHarmIn">How many game seconds lie before the next harm lands; zero when none will.</param>
/// <param name="Shelters">What spares the party, or some of it, the game's ground now.</param>
public sealed record FootingSnapshot(
    string Ground,
    bool Harmful,
    double Every,
    double NextHarmIn,
    IReadOnlyList<FootingShelterSnapshot> Shelters)
{
    /// <summary>Nothing underfoot, nothing harming, nobody spared.</summary>
    public static FootingSnapshot None { get; } = new(string.Empty, false, 0, 0, []);

    /// <summary>Reads the world's reading of the ground under the party.</summary>
    /// <param name="ground">The world's reading, or null for a session with no world.</param>
    /// <param name="party">The party whose members carry the shelters, which names them.</param>
    public static FootingSnapshot From(GroundReading? ground, PartyEntity? party)
    {
        if (ground is null) return None;
        List<FootingShelterSnapshot> shelters = [];
        foreach (GroundShelter shelter in ground.Shelters)
        {
            string name = party is not null && party.TryMember(shelter.Carrier, out PartyMember? carrier) && carrier is not null
                ? carrier.Profile.Name
                : shelter.Carrier.ToString();
            shelters.Add(new FootingShelterSnapshot(shelter.Effect.Value, shelter.Name, shelter.Carrier.ToString(), name, shelter.Everybody));
        }

        return new FootingSnapshot(
            ground.Footing?.Id ?? string.Empty,
            ground.Interval is not null,
            ground.Interval?.TotalSeconds ?? 0,
            ground.UntilNextHarm.TotalSeconds,
            shelters);
    }

    /// <summary>Writes the footing object.</summary>
    internal uint Write(UiValueBuilder builder) =>
        builder.Object(
            ("ground", builder.String(Ground)),
            ("harmful", builder.Boolean(Harmful)),
            ("every", builder.Number(Every)),
            ("nextHarmIn", builder.Number(NextHarmIn)),
            ("shelters", builder.Array([.. Shelters.Select(shelter => shelter.Write(builder))])));
}
