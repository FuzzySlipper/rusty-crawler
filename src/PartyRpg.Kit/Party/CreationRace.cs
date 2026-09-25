namespace PartyRpg.Kit.Party;

/// <summary>One race creation may choose, with the attribute table it brings.</summary>
/// <remarks>
/// A race is a name and a set of starting values and ranges: choosing one re-derives every attribute score,
/// so the table is what a portrait choice actually changes about a character. Which attributes exist, what
/// they are called, and how far each may move are all stated here by whoever supplies creation's choices, so
/// the kit reads a table and holds no attribute of its own.
/// </remarks>
public sealed record CreationRace
{
    /// <summary>States one race's creation table.</summary>
    /// <param name="id">The race definition's id.</param>
    /// <param name="name">What the race is called, for the messages a refused choice carries.</param>
    /// <param name="attributes">The race's attributes and their ranges, in the order they are shown.</param>
    /// <exception cref="ArgumentException">The name is blank, the table is empty, or an attribute appears twice.</exception>
    public CreationRace(RaceId id, string name, IReadOnlyList<AttributeCreationRange> attributes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(attributes);
        if (attributes.Count == 0)
        {
            throw new ArgumentException(
                $"Race '{name}' states no attributes, so creation would have nothing to spend its points on.",
                nameof(attributes));
        }

        HashSet<AttributeId> seen = [];
        foreach (AttributeCreationRange range in attributes)
        {
            if (!seen.Add(range.Attribute))
            {
                throw new ArgumentException(
                    $"Race '{name}' states '{range.Name}' twice, so which range applies to it would be ambiguous.",
                    nameof(attributes));
            }
        }

        Id = id;
        Name = name;
        Attributes = attributes;
    }

    /// <summary>The race definition's id.</summary>
    public RaceId Id { get; }

    /// <summary>What the race is called.</summary>
    public string Name { get; }

    /// <summary>The race's attributes and their ranges, in the order they are shown.</summary>
    public IReadOnlyList<AttributeCreationRange> Attributes { get; }

    /// <summary>Reads one attribute's range.</summary>
    /// <param name="attribute">The attribute to look for.</param>
    /// <param name="range">The attribute's range, when this race states one.</param>
    /// <returns>Whether the race has that attribute.</returns>
    public bool TryAttribute(AttributeId attribute, out AttributeCreationRange? range)
    {
        foreach (AttributeCreationRange candidate in Attributes)
        {
            if (candidate.Attribute != attribute) continue;
            range = candidate;
            return true;
        }

        range = null;
        return false;
    }
}
