namespace PartyRpg.Kit.Party;

/// <summary>One portrait creation may choose, and the race it is drawn as.</summary>
/// <remarks>
/// In this game family the face is the race: a player picks a portrait and the character's race follows from
/// it, which is why a portrait states the race it belongs to rather than leaving creation to guess. A
/// portrait whose race creation does not offer is refused when the choices are assembled, not when a player
/// reaches for it.
/// </remarks>
public sealed record CreationPortrait
{
    /// <summary>States one portrait choice.</summary>
    /// <param name="id">The portrait's id, which is what a save carries.</param>
    /// <param name="race">The race the portrait is drawn as.</param>
    /// <param name="name">What the portrait is called, for the messages a refused choice carries.</param>
    /// <exception cref="ArgumentException">The name is blank, so a refusal could not say what it is about.</exception>
    public CreationPortrait(PortraitId id, RaceId race, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Id = id;
        Race = race;
        Name = name;
    }

    /// <summary>The portrait's id, which is what a save carries.</summary>
    public PortraitId Id { get; }

    /// <summary>The race the portrait is drawn as.</summary>
    public RaceId Race { get; }

    /// <summary>What the portrait is called.</summary>
    public string Name { get; }
}
