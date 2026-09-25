using PartyRpg.Kit.Time;

namespace PartyRpg.Kit.Services;

/// <summary>What content declares about one service: who keeps it, when it serves, what it offers, and its prices.</summary>
/// <remarks>
/// <para>
/// This is one counter, shop, guild, or hall, read from the content a ruleset interpreted. It carries
/// everything the mechanism does not invent: the kind content calls it, what a person reads on the sign,
/// the operations it offers, the hours it keeps, the schedule its shelves refresh on, the access it
/// requires, the multipliers its prices are worked from, and the stock and lessons it declares.
/// </para>
/// <para>
/// <b>Nothing here is a rule.</b> The multipliers are content's numbers; which of them applies to which
/// operation, how the party's own skill and standing adjust them, and whether the party may be served at
/// all are the ruleset's answers through <see cref="IServiceRule"/>. Two services of one kind differ only
/// by this record, which is what makes a new shop, temple, or guild a matter of content rather than code.
/// </para>
/// <para>
/// The access requirement is a party-wide effect's identity, not a flag the mechanism keeps: membership is
/// state the party carries — an effect acting on the band, which a save already records — and the service
/// reads it back through the same identity the lesson that grants it names.
/// </para>
/// </remarks>
public sealed record ServiceDefinition
{
    /// <summary>Creates a service definition.</summary>
    /// <param name="id">The service's identity in content.</param>
    /// <param name="kind">What sort of service content calls this.</param>
    /// <param name="name">What a person reads on the sign, which must not be blank.</param>
    /// <param name="operations">Which operations the service offers; none means it offers nothing yet.</param>
    /// <param name="stock">The lines its shelves hold when they are full.</param>
    /// <param name="lessons">What it teaches.</param>
    /// <param name="proprietor">The name of whoever keeps it, or empty when content names nobody.</param>
    /// <param name="hours">When it serves, or null when content states no hours and it serves always.</param>
    /// <param name="refreshInterval">
    /// How much game time passes before its shelves are restocked, or null when content states no schedule
    /// and the shelves keep what they were laid out with.
    /// </param>
    /// <param name="priceMultiplier">The multiplier content puts on its prices, which must be positive.</param>
    /// <param name="skillPriceMultiplier">
    /// The multiplier content puts on what it teaches, which must be positive; it is the same number by
    /// default, which is what a service that prices teaching like goods gets.
    /// </param>
    /// <param name="membership">
    /// The party-wide effect the service requires the party to carry, or empty when it serves anybody. It
    /// is a membership when the same service also teaches the effect.
    /// </param>
    /// <exception cref="ArgumentException">The name is blank, or a multiplier is not a finite, positive number.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The refresh interval is no time at all, which would restock on every advance.</exception>
    public ServiceDefinition(
        ServiceId id,
        ServiceKind kind,
        string name,
        IReadOnlyList<ServiceOperationKind> operations,
        IReadOnlyList<ServiceStockLine> stock,
        IReadOnlyList<ServiceLesson> lessons,
        string proprietor = "",
        ServiceHours? hours = null,
        GameDuration? refreshInterval = null,
        double priceMultiplier = 1,
        double? skillPriceMultiplier = null,
        string membership = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(operations);
        ArgumentNullException.ThrowIfNull(stock);
        ArgumentNullException.ThrowIfNull(lessons);
        RequireMultiplier(priceMultiplier, nameof(priceMultiplier));
        double skillMultiplier = skillPriceMultiplier ?? priceMultiplier;
        RequireMultiplier(skillMultiplier, nameof(skillPriceMultiplier));
        if (refreshInterval is { IsNone: true })
        {
            throw new ArgumentOutOfRangeException(
                nameof(refreshInterval),
                refreshInterval,
                "A shelf that refreshes after no time at all would restock on every advance and could never be found empty.");
        }

        Id = id;
        Kind = kind;
        Name = name;
        Operations = operations;
        Stock = stock;
        Lessons = lessons;
        Proprietor = proprietor;
        Hours = hours;
        RefreshInterval = refreshInterval;
        PriceMultiplier = priceMultiplier;
        SkillPriceMultiplier = skillMultiplier;
        Membership = membership;
    }

    /// <summary>The service's identity in content.</summary>
    public ServiceId Id { get; init; }

    /// <summary>What sort of service content calls this.</summary>
    public ServiceKind Kind { get; init; }

    /// <summary>What a person reads on the sign.</summary>
    public string Name { get; init; }

    /// <summary>The name of whoever keeps it, or empty when content names nobody.</summary>
    public string Proprietor { get; init; }

    /// <summary>Which operations the service offers.</summary>
    public IReadOnlyList<ServiceOperationKind> Operations { get; init; }

    /// <summary>The lines its shelves hold when they are full.</summary>
    public IReadOnlyList<ServiceStockLine> Stock { get; init; }

    /// <summary>What it teaches.</summary>
    public IReadOnlyList<ServiceLesson> Lessons { get; init; }

    /// <summary>When it serves, or null when content states no hours.</summary>
    public ServiceHours? Hours { get; init; }

    /// <summary>How much game time passes before its shelves are restocked, or null when content states no schedule.</summary>
    public GameDuration? RefreshInterval { get; init; }

    /// <summary>The multiplier content puts on its prices.</summary>
    public double PriceMultiplier { get; init; }

    /// <summary>The multiplier content puts on what it teaches.</summary>
    public double SkillPriceMultiplier { get; init; }

    /// <summary>The party-wide effect the service requires the party to carry, or empty when it serves anybody.</summary>
    public string Membership { get; init; }

    /// <summary>Whether the service offers one operation at all.</summary>
    /// <param name="operation">The operation to look for.</param>
    public bool Offers(ServiceOperationKind operation) => Operations.Contains(operation);

    /// <summary>How the service reads in a sentence: its name, and who keeps it when content names somebody.</summary>
    public string Describe() => Proprietor.Length > 0 ? $"{Name}, kept by {Proprietor}" : Name;

    /// <summary>Refuses a multiplier that is not a finite, positive number.</summary>
    /// <remarks>
    /// A multiplier of zero would make everything free and a negative one would pay the party for shopping,
    /// so neither is content this mechanism can price; a content defect naming the field is better than a
    /// price nobody can explain.
    /// </remarks>
    private static void RequireMultiplier(double multiplier, string parameterName)
    {
        if (!double.IsFinite(multiplier) || multiplier <= 0)
        {
            throw new ArgumentException(
                $"A price multiplier must be a finite, positive number, and {multiplier} is neither; content that states one states a factor prices are scaled by.",
                parameterName);
        }
    }
}
