namespace PartyRpg.Kit.Party;

/// <summary>What one character has left to spend and to lose: hit points and spell points.</summary>
/// <remarks>
/// <para>
/// These are the resources a session debits: a fall, a trap, a spell, and a night's rest all arrive here,
/// and the ruleset decides how much each is worth. Two pools rather than one because a game spends them
/// against different things, and a pool is a value so a captured save holds exactly what the character had
/// at that moment rather than a reference the party goes on changing.
/// </para>
/// <para>
/// The kit states no rule about zero: what happens when hit points run out, whether a condition follows,
/// and how a level-up raises a maximum are the ruleset's calls, which is why the transitions here only add
/// and subtract inside the pool.
/// </para>
/// </remarks>
public sealed class CharacterResources
{
    /// <summary>Creates a character's resource pools.</summary>
    /// <param name="hitPoints">What the character has to lose.</param>
    /// <param name="spellPoints">What the character has to cast with.</param>
    public CharacterResources(ResourcePool hitPoints, ResourcePool spellPoints)
    {
        HitPoints = hitPoints;
        SpellPoints = spellPoints;
    }

    /// <summary>What the character has to lose.</summary>
    public ResourcePool HitPoints { get; private set; }

    /// <summary>What the character has to cast with.</summary>
    public ResourcePool SpellPoints { get; private set; }

    /// <summary>Takes damage, which never carries the pool below empty.</summary>
    /// <param name="amount">How much damage to take, which cannot be negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">The amount is negative, which would heal rather than harm.</exception>
    public void TakeDamage(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        HitPoints = HitPoints.Spent(amount);
    }

    /// <summary>Restores hit points, never past the pool's capacity.</summary>
    /// <param name="amount">How much to restore, which cannot be negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">The amount is negative.</exception>
    public void RestoreHitPoints(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        HitPoints = HitPoints.Restored(amount);
    }

    /// <summary>Records a new hit point capacity, which a level-up or a lost level does.</summary>
    /// <param name="maximum">The new capacity, which cannot be negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">The maximum is negative.</exception>
    public void SetMaximumHitPoints(int maximum)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximum);
        HitPoints = HitPoints.WithMaximum(maximum);
    }

    /// <summary>Spends spell points for a casting.</summary>
    /// <param name="amount">How much the casting costs, which cannot be negative.</param>
    /// <returns>Whether the character had enough; a refused spend leaves the pool untouched.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The amount is negative.</exception>
    public bool TrySpendSpellPoints(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        if (amount > SpellPoints.Current) return false;
        SpellPoints = SpellPoints.Spent(amount);
        return true;
    }

    /// <summary>Restores spell points, never past the pool's capacity.</summary>
    /// <param name="amount">How much to restore, which cannot be negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">The amount is negative.</exception>
    public void RestoreSpellPoints(int amount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        SpellPoints = SpellPoints.Restored(amount);
    }

    /// <summary>Records a new spell point capacity.</summary>
    /// <param name="maximum">The new capacity, which cannot be negative.</param>
    /// <exception cref="ArgumentOutOfRangeException">The maximum is negative.</exception>
    public void SetMaximumSpellPoints(int maximum)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximum);
        SpellPoints = SpellPoints.WithMaximum(maximum);
    }

    /// <summary>Fills both pools, which is what a completed rest does once the ruleset has decided it.</summary>
    public void RestoreAll()
    {
        HitPoints = HitPoints.Filled();
        SpellPoints = SpellPoints.Filled();
    }
}
