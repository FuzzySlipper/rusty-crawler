namespace PartyRpg.Kit.Party;

/// <summary>The conditions acting on one character.</summary>
/// <remarks>
/// Conditions are content identities with a severity, so the kit holds them without knowing what any of
/// them does: a game's list of afflictions, its cures, and what rest clears all live in its definitions
/// and rules. Recovery — how a condition ends, and what a severity means for a character's numbers — is
/// the ruleset's work over this state.
/// </remarks>
public sealed class CharacterConditions
{
    private readonly List<ActiveCondition> _active = [];

    /// <summary>Creates a character's conditions.</summary>
    /// <param name="active">The conditions already acting, in the order they were applied.</param>
    /// <exception cref="ArgumentException">A condition is listed twice, which would give it two severities.</exception>
    public CharacterConditions(IEnumerable<ActiveCondition>? active = null)
    {
        if (active is null) return;

        HashSet<ConditionId> seen = [];
        foreach (ActiveCondition condition in active)
        {
            if (!seen.Add(condition.Condition))
            {
                throw new ArgumentException(
                    $"Condition '{condition.Condition}' is listed more than once, so which severity is acting would be ambiguous.",
                    nameof(active));
            }

            _active.Add(condition);
        }
    }

    /// <summary>The conditions acting, in the order they were applied.</summary>
    public IReadOnlyList<ActiveCondition> Active => _active;

    /// <summary>How many conditions are acting.</summary>
    public int Count => _active.Count;

    /// <summary>Whether a condition is acting.</summary>
    /// <param name="condition">The condition to look for.</param>
    public bool Has(ConditionId condition) => IndexOf(condition) >= 0;

    /// <summary>How severe a condition is, or zero when it is not acting.</summary>
    /// <param name="condition">The condition to read.</param>
    public int SeverityOf(ConditionId condition)
    {
        int index = IndexOf(condition);
        return index >= 0 ? _active[index].Severity : 0;
    }

    /// <summary>Applies a condition, replacing the severity of one already acting.</summary>
    /// <param name="condition">The condition to apply.</param>
    public void Apply(ActiveCondition condition)
    {
        int index = IndexOf(condition.Condition);
        if (index < 0)
        {
            _active.Add(condition);
            return;
        }

        _active[index] = condition;
    }

    /// <summary>Ends one condition.</summary>
    /// <param name="condition">The condition to clear.</param>
    /// <returns>Whether the condition was acting.</returns>
    public bool Clear(ConditionId condition)
    {
        int index = IndexOf(condition);
        if (index < 0) return false;
        _active.RemoveAt(index);
        return true;
    }

    /// <summary>Ends every condition, which is what a complete recovery does.</summary>
    public void ClearAll() => _active.Clear();

    private int IndexOf(ConditionId condition)
    {
        for (int index = 0; index < _active.Count; index++)
        {
            if (_active[index].Condition == condition) return index;
        }

        return -1;
    }
}
