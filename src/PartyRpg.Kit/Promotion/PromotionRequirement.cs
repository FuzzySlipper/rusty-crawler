namespace PartyRpg.Kit.Promotion;

/// <summary>What kind of thing a rank asks for before it is given, as the vocabulary this mechanism judges.</summary>
/// <remarks>
/// <para>
/// Four kinds, and each is a shape of state rather than a rule: <see cref="Giver"/> is a person, and it is
/// the person the party is taking the rank from; <see cref="Item"/> is something the party carries, counted
/// in the one inventory a party has; <see cref="Award"/> is a record of a deed the party carries, with how
/// much of it; and <see cref="Quest"/> is a finished errand, which is a kind a ruleset may state and this
/// build cannot judge — nothing yet owns an errand's state, so a rank that asks for one is refused by name
/// and the requirement travels to the owner that will judge it.
/// </para>
/// <para>
/// The kinds are deliberately few and deliberately closed: a rank's requirement is a named thing of one of
/// these shapes, so a game states its ladder as data and this mechanism judges it without a branch per
/// class, per rank, or per game. A requirement kind nobody could answer would be a vocabulary of guesses.
/// </para>
/// </remarks>
public enum PromotionRequirementKind
{
    /// <summary>The person the rank is taken from.</summary>
    Giver,

    /// <summary>Something the party must carry, by the item definition's own identity and a count.</summary>
    Item,

    /// <summary>A record of a deed the party carries, by the record's own identity and how much of it.</summary>
    Award,

    /// <summary>A finished errand, by the errand's own identity. Stated by a ruleset; judged by the quest owner.</summary>
    Quest,
}

/// <summary>One thing a rank asks for: a kind, the identity the owner resolves, and how much of it.</summary>
/// <remarks>
/// <para>
/// The name is an identity and never a resolved rule: an item's definition, a person's identity among the
/// people the world carries, a record's own name, and an errand's own name are all content's or the
/// ruleset's, and this type only carries what was stated. The label is what a person reads, because a
/// refusal that listed a definition's number would leave a player unable to tell what was missing.
/// </para>
/// <para>
/// <b>An amount is meaningful per kind.</b> An item's amount is how many the party must carry, an award's is
/// the magnitude the record must have reached, and a giver or an errand asks for exactly one. The amount is
/// at least one whichever kind it is, so a requirement that asks for nothing is inexpressible rather than
/// quietly always met.
/// </para>
/// </remarks>
public sealed record PromotionRequirement
{
    /// <summary>Creates a requirement.</summary>
    /// <param name="kind">What kind of thing the rank asks for.</param>
    /// <param name="name">The identity the owner of that kind resolves, which must not be blank.</param>
    /// <param name="amount">How much of it is asked for, which is at least one.</param>
    /// <param name="label">How it reads to a person, or empty to read as the identity itself.</param>
    /// <exception cref="ArgumentException">The name or the label is blank where one is required.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The amount is below one.</exception>
    public PromotionRequirement(PromotionRequirementKind kind, string name, int amount = 1, string label = "")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfLessThan(amount, 1);
        Kind = kind;
        Name = name;
        Amount = amount;
        Label = label ?? string.Empty;
    }

    /// <summary>What kind of thing the rank asks for.</summary>
    public PromotionRequirementKind Kind { get; }

    /// <summary>The identity the owner of that kind resolves.</summary>
    public string Name { get; }

    /// <summary>How much of it the rank asks for.</summary>
    public int Amount { get; }

    /// <summary>How it reads to a person, or empty to read as the identity itself.</summary>
    public string Label { get; }

    /// <summary>A rank given by one person, named by that person's own identity.</summary>
    /// <param name="giver">The person's identity among the people the world carries.</param>
    /// <param name="label">How the person reads to a player, or empty to read as the identity.</param>
    /// <returns>The requirement.</returns>
    public static PromotionRequirement FromGiver(string giver, string label = "") =>
        new(PromotionRequirementKind.Giver, giver, 1, label);

    /// <summary>A rank that asks the party to carry something.</summary>
    /// <param name="item">The item definition's own identity.</param>
    /// <param name="count">How many the party must carry.</param>
    /// <param name="label">How the item reads to a player, or empty to read as the identity.</param>
    /// <returns>The requirement.</returns>
    public static PromotionRequirement ForItem(string item, int count = 1, string label = "") =>
        new(PromotionRequirementKind.Item, item, count, label);

    /// <summary>A rank that asks for a deed the party has on record, at least at a magnitude.</summary>
    /// <param name="award">The record's own identity.</param>
    /// <param name="amount">The magnitude the record must have reached.</param>
    /// <param name="label">How the deed reads to a player, or empty to read as the identity.</param>
    /// <returns>The requirement.</returns>
    public static PromotionRequirement ForAward(string award, int amount = 1, string label = "") =>
        new(PromotionRequirementKind.Award, award, amount, label);

    /// <summary>A rank that asks for a finished errand, which the quest owner judges.</summary>
    /// <param name="quest">The errand's own identity.</param>
    /// <param name="label">How the errand reads to a player, or empty to read as the identity.</param>
    /// <returns>The requirement.</returns>
    public static PromotionRequirement ForQuest(string quest, string label = "") =>
        new(PromotionRequirementKind.Quest, quest, 1, label);

    /// <summary>How this requirement reads inside a list of what a rank asks for.</summary>
    /// <returns>The words a person reads.</returns>
    public override string ToString() => Kind switch
    {
        PromotionRequirementKind.Giver => $"granted by {Word()}",
        PromotionRequirementKind.Item => Amount > 1 ? $"{Amount} × {Word()}" : Word(),
        PromotionRequirementKind.Award => Amount > 1 ? $"{Word()} ({Amount})" : Word(),
        _ => $"the errand '{Word()}' finished",
    };

    /// <summary>The word a person reads for this requirement's own thing.</summary>
    private string Word() => Label.Length > 0 ? Label : Name;
}
