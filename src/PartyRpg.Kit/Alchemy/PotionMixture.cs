using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Alchemy;

/// <summary>What combining two things does, as the game's own rows state it.</summary>
/// <remarks>
/// <para>
/// A game's mixture table states one of three things for a pair of ingredients: the thing the pair makes, a
/// burst of a stated strength when the pair is a mixture that should not have been attempted, or nothing at
/// all. All three are content — the row is the answer — so this type exists to carry that answer to the
/// workflow that carries it out, and nothing here decides which of the three is right.
/// </para>
/// <para>
/// <b>The failure strength is the game's own number, not a rule about failures.</b> A table states one
/// strength per pair rather than one per kind of mistake, which is why it travels here as a value: what a
/// burst of strength three costs a character is the ruleset's answer, and this only says the row stated one.
/// </para>
/// </remarks>
public readonly record struct MixtureOutcome
{
    private MixtureOutcome(ItemDefinitionId? result, int burst, bool reacts)
    {
        Result = result;
        Burst = burst;
        Reacts = reacts;
    }

    /// <summary>The nothing a pair does, which a table states for ingredients that do not combine.</summary>
    public static MixtureOutcome Nothing => default;

    /// <summary>What the pair makes, when the row states a result.</summary>
    public ItemDefinitionId? Result { get; }

    /// <summary>How strong a burst the pair makes, zero when the row states no burst.</summary>
    public int Burst { get; }

    /// <summary>Whether the row states anything at all for this pair.</summary>
    /// <remarks>
    /// A pair a table states nothing for and a pair it states "nothing happens" for are different facts: the
    /// first is two things that were never a mixture, the second is a mixture that does nothing. Only the
    /// second is a mixture a workflow may act on, which is what keeps an unstated pair's ingredients where
    /// they lie.
    /// </remarks>
    public bool Reacts { get; }

    /// <summary>The pair produces a thing.</summary>
    /// <param name="result">What the mixture makes.</param>
    /// <returns>The outcome.</returns>
    public static MixtureOutcome Produces(ItemDefinitionId result) => new(result, burst: 0, reacts: true);

    /// <summary>The pair is a mixture that bursts at a stated strength.</summary>
    /// <param name="strength">How strong the burst is, which must be at least one.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The strength is below one, which states no burst.</exception>
    public static MixtureOutcome Bursts(int strength)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(strength, 1);
        return new(null, strength, reacts: true);
    }

    /// <summary>The pair is a mixture that does nothing at all.</summary>
    /// <returns>The outcome.</returns>
    public static MixtureOutcome NoReaction() => new(null, burst: 0, reacts: true);

    /// <inheritdoc />
    public override string ToString() => !Reacts
        ? "not a mixture"
        : Result is { } made ? $"makes {made}"
        : Burst > 0 ? $"bursts at strength {Burst}"
        : "does nothing";
}

/// <summary>One pair of ingredients a game's own table states a mixture for.</summary>
/// <remarks>
/// <para>
/// <b>A mixture is a pair, and the order of the two does not matter.</b> The table this is read from is a
/// matrix, and the tables that carry one are symmetric — the row for the first ingredient and the column for
/// the second states what the reverse states — so the pair is held unordered and looked up that way. That is
/// what keeps one mixture from being two entries that could disagree, and it is why <see cref="First"/> and
/// <see cref="Second"/> are ordered only so a report can print the same pair the same way twice.
/// </para>
/// <para>
/// <b>What the mixture requires is the result's own fact.</b> A mixture's tier is the rung of the game's
/// ladder its <em>result</em> needs, because that is what the games gate on: whether a character may make a
/// thing is a fact about the thing. The workflow therefore asks the catalog for the rung of the potion the
/// pair makes, and a pair that makes nothing requires nothing.
/// </para>
/// </remarks>
/// <param name="First">One ingredient, as content names it.</param>
/// <param name="Second">The other ingredient, as content names it.</param>
/// <param name="Outcome">What the pair does, or <see cref="MixtureOutcome.Nothing"/> when the table states nothing.</param>
/// <param name="Tier">The rung of the game's ladder the result requires, which is unstated for a pair that makes nothing.</param>
/// <param name="Power">The strength the table states for the mixture itself, zero when it states none.</param>
/// <param name="Note">The discovery the mixture records, zero when the table states none.</param>
public readonly record struct PotionMixture(
    ItemDefinitionId First,
    ItemDefinitionId Second,
    MixtureOutcome Outcome,
    SkillTier Tier = default,
    int Power = 0,
    int Note = 0)
{
    /// <summary>Whether the pair is a mixture at all, which a table's "nothing happens" cell still is.</summary>
    public bool IsMixture => Outcome.Reacts;

    /// <summary>Whether the mixture makes a thing rather than bursting or doing nothing.</summary>
    public bool Produces => Outcome.Result is not null;

    /// <summary>Whether the two ingredients are the same definition, which is a table's own diagonal.</summary>
    public bool IsSameDefinition => First == Second;

    /// <summary>Whether a definition is one of the mixture's ingredients.</summary>
    /// <param name="definition">The definition to look for.</param>
    public bool Has(ItemDefinitionId definition) => First == definition || Second == definition;

    /// <summary>The mixture with its two ingredients in a stated order, which is how a report prints it.</summary>
    /// <param name="first">One ingredient.</param>
    /// <param name="second">The other ingredient.</param>
    /// <returns>The mixture, or this one when neither ingredient matches.</returns>
    public PotionMixture Ordered(ItemDefinitionId first, ItemDefinitionId second) =>
        First == second && Second == first ? this with { First = first, Second = second } : this;

    /// <inheritdoc />
    public override string ToString() =>
        $"{First} + {Second}: {Outcome}" + (Tier.IsNone ? string.Empty : $", at rung {Tier.Value}");
}
