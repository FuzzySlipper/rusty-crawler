using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Alchemy;

/// <summary>Every mixture one game's own tables state, looked up by the pair of ingredients.</summary>
/// <remarks>
/// <para>
/// <b>This is a table, not a rule.</b> Each row is a pair of definitions and what combining them does, read
/// from the shipped mixture table; the catalog indexes those rows so a pair is one lookup rather than a scan,
/// and answers nothing at all for a pair the table never stated. The order of the two definitions is not part
/// of the key, because the table it comes from is symmetric and a mixture of A and B is the same mixture as
/// one of B and A.
/// </para>
/// <para>
/// <b>A duplicate pair is a defect rather than a merge.</b> Two rows for one pair would leave which of them
/// decides ambiguous, which is the same reason the spell and skill catalogs refuse a duplicated identity: the
/// table is the owner of the answer and two answers are one too many.
/// </para>
/// </remarks>
public sealed class AlchemyCatalog
{
    private readonly List<PotionMixture> _mixtures = [];
    private readonly Dictionary<(string First, string Second), PotionMixture> _byPair = [];

    /// <summary>Reads a catalog from the mixture rows a ruleset placed.</summary>
    /// <param name="mixtures">The rows, in content's order.</param>
    /// <exception cref="ArgumentNullException">No rows were supplied.</exception>
    /// <exception cref="ArgumentException">A pair is stated twice, so which outcome it has would be ambiguous.</exception>
    public AlchemyCatalog(IEnumerable<PotionMixture> mixtures)
    {
        ArgumentNullException.ThrowIfNull(mixtures);
        foreach (PotionMixture mixture in mixtures)
        {
            if (!_byPair.TryAdd(Key(mixture.First, mixture.Second), mixture))
            {
                throw new ArgumentException(
                    $"The mixture of '{mixture.First}' and '{mixture.Second}' is stated twice, so what combining them does would be ambiguous.",
                    nameof(mixtures));
            }

            _mixtures.Add(mixture);
        }
    }

    /// <summary>Every mixture the table states, in content's order.</summary>
    public IReadOnlyList<PotionMixture> Mixtures => _mixtures;

    /// <summary>How many mixtures the table states.</summary>
    public int Count => _mixtures.Count;

    /// <summary>
    /// What combining two definitions does, or null when the table states no mixture for them.
    /// </summary>
    /// <remarks>
    /// The pair is unordered, so a caller that names the same two things in the other order gets the same
    /// row back. A row the table states as doing nothing is a mixture — the caller may act on it and nothing
    /// will happen — while a null answer means these two were never a mixture at all.
    /// </remarks>
    /// <param name="first">One ingredient.</param>
    /// <param name="second">The other ingredient.</param>
    /// <returns>The mixture, or null when the table states none.</returns>
    public PotionMixture? Find(ItemDefinitionId first, ItemDefinitionId second) =>
        _byPair.TryGetValue(Key(first, second), out PotionMixture found) ? found : null;

    /// <summary>The mixtures one definition takes part in, in content's order.</summary>
    /// <param name="definition">The definition to look for.</param>
    /// <returns>The mixtures that name it, which is empty for a thing nothing combines with.</returns>
    public IReadOnlyList<PotionMixture> With(ItemDefinitionId definition)
    {
        List<PotionMixture> found = [];
        foreach (PotionMixture mixture in _mixtures)
        {
            if (mixture.Has(definition)) found.Add(mixture);
        }

        return found;
    }

    private static (string First, string Second) Key(ItemDefinitionId first, ItemDefinitionId second) =>
        string.CompareOrdinal(first.Value, second.Value) <= 0
            ? (first.Value, second.Value)
            : (second.Value, first.Value);
}
