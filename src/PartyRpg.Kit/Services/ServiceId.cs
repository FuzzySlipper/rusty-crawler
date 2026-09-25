namespace PartyRpg.Kit.Services;

/// <summary>Which service something is: the identity content gives one counter, shop, guild, or hall.</summary>
/// <remarks>
/// <para>
/// This is content identity, and it is what ties a place's placement to the definition that says what the
/// service sells, what it charges, when it opens, and what it teaches. A placement names a service by this
/// id and the service mechanism resolves it through the ruleset, so two counters of one kind — two weapon
/// shops in two towns — are two services with their own shelves and their own hours rather than two
/// instances of a class.
/// </para>
/// <para>
/// The id is deliberately not a kind. A kind is content's word for what sort of building this is (a weapon
/// shop, a temple, a guild), shared by every service of that sort; the id picks out this one, so that a
/// shelf one town has sold out of is not a shelf the next town has sold out of.
/// </para>
/// </remarks>
public readonly record struct ServiceId
{
    /// <summary>Creates a service identity.</summary>
    /// <param name="value">The service's id in content, which must not be blank.</param>
    /// <exception cref="ArgumentException">The id is blank, which names no service.</exception>
    public ServiceId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>The service's id in content.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}
