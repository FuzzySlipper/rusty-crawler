namespace PartyRpg.Kit.World;

/// <summary>A place the party can be: an outdoor region or an interior.</summary>
/// <param name="Value">The place's durable identity, which is content's id for it.</param>
public readonly record struct PlaceId(string Value)
{
    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>What kind of place this is. The distinction decides which map data a place carries, not how the party behaves in it.</summary>
public enum PlaceKind
{
    /// <summary>An outdoor region, whose places connect to each other along their edges.</summary>
    Region,

    /// <summary>An interior, reached through an entrance and left through an exit.</summary>
    Interior,
}
