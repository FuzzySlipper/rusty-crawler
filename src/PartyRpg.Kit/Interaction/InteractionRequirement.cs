namespace PartyRpg.Kit.Interaction;

/// <summary>What kind of thing a use can require, as the workflow's vocabulary.</summary>
/// <remarks>
/// <para>
/// The kinds are the shapes a requirement can take, and each one is answered by the ruleset rather than by
/// the kit: what a key is, which member's skill counts, what a flag means, and which hours are day are all
/// this game's policy, while the requirement's own shape — a named thing, and how much of it — is the
/// workflow's.
/// </para>
/// <para>
/// A key is deliberately not a kind of its own. In the games this product is shaped after a key <em>is</em>
/// an item the party carries, and a second kind for it would be two names for one check; what makes an item
/// a key is that a target requires it.
/// </para>
/// </remarks>
public enum InteractionRequirementKind
{
    /// <summary>An item the party must carry, which is what a key is.</summary>
    Item,

    /// <summary>A skill some member must have learned, to the level the requirement states.</summary>
    Skill,

    /// <summary>Something the party must have done or learned, recorded as a named flag.</summary>
    Flag,

    /// <summary>The part of the day the use must fall in, named <c>day</c> or <c>night</c>.</summary>
    TimeOfDay,
}

/// <summary>One thing a use requires before it can happen: a named thing, and how much of it.</summary>
/// <remarks>
/// <para>
/// Requirements come from content and the ruleset and are checked in the order they are stated, so the first
/// one the party does not meet is the one the refusal names. The kit owns this vocabulary and the order the
/// checks happen in; the ruleset owns what each requirement means for its game, which is why judging one is
/// that ruleset's answer and never a comparison here.
/// </para>
/// <para>
/// The label exists so a locked door can say what it needs in the words a person reads, while the name
/// stays the identity the ruleset resolves — an item definition, a skill, a flag, or a part of the day.
/// </para>
/// </remarks>
public sealed record InteractionRequirement
{
    /// <summary>Creates a requirement.</summary>
    /// <param name="kind">What the requirement is about.</param>
    /// <param name="name">The identity the ruleset resolves it by, which must not be blank.</param>
    /// <param name="amount">How many, or what level a skill must reach; at least one.</param>
    /// <param name="label">
    /// What a person reads for the requirement, or empty to read the name itself. A label is presentation
    /// only and is never what the requirement is resolved by.
    /// </param>
    /// <exception cref="ArgumentException">The name is blank, which names nothing a rule could resolve.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The amount is below one, which would require nothing.</exception>
    public InteractionRequirement(InteractionRequirementKind kind, string name, int amount = 1, string label = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfLessThan(amount, 1);
        Kind = kind;
        Name = name;
        Amount = amount;
        Label = string.IsNullOrWhiteSpace(label) ? name : label;
    }

    /// <summary>What kind of thing the requirement is about.</summary>
    public InteractionRequirementKind Kind { get; }

    /// <summary>The identity the ruleset resolves the requirement by.</summary>
    public string Name { get; }

    /// <summary>How many of the named thing, or the skill level it must reach.</summary>
    public int Amount { get; }

    /// <summary>What a person reads for the requirement.</summary>
    public string Label { get; }

    /// <summary>How the requirement reads in a list or in a refusal naming it.</summary>
    /// <remarks>
    /// A skill's amount is the level it must reach, so it reads as a level rather than as a count of skills;
    /// every other kind's amount is a count of the named thing.
    /// </remarks>
    public string Describe() => Kind switch
    {
        InteractionRequirementKind.Skill => Amount <= 1 ? Label : $"{Label} {Amount}",
        _ => Amount <= 1 ? Label : $"{Amount} × {Label}",
    };

    /// <inheritdoc />
    public override string ToString() => $"{Kind}:{Name}";
}
