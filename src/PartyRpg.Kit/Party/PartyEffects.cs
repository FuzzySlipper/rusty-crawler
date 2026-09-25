namespace PartyRpg.Kit.Party;

/// <summary>The effects acting on the whole party.</summary>
/// <remarks>
/// One entry per effect definition, in the order effects were first applied, so applying one that is
/// already acting replaces its magnitude rather than stacking an unbounded pile of the same effect. What
/// each magnitude does to a roll, a resistance, or a recovery time is read by the ruleset from this state;
/// the kit only holds it and hands it back.
/// </remarks>
public sealed class PartyEffects
{
    private readonly List<PartyEffect> _active = [];

    /// <summary>Creates the party's effects.</summary>
    /// <param name="active">The effects already acting, in the order they were applied.</param>
    /// <exception cref="ArgumentException">An effect is listed twice, which would give it two magnitudes.</exception>
    public PartyEffects(IEnumerable<PartyEffect>? active = null)
    {
        if (active is null) return;

        HashSet<EffectId> seen = [];
        foreach (PartyEffect effect in active)
        {
            if (!seen.Add(effect.Effect))
            {
                throw new ArgumentException(
                    $"Effect '{effect.Effect}' is listed more than once, so which magnitude is acting would be ambiguous.",
                    nameof(active));
            }

            _active.Add(effect);
        }
    }

    /// <summary>The effects acting, in the order they were applied.</summary>
    public IReadOnlyList<PartyEffect> Active => _active;

    /// <summary>How many effects are acting.</summary>
    public int Count => _active.Count;

    /// <summary>Whether an effect is acting.</summary>
    /// <param name="effect">The effect to look for.</param>
    public bool Has(EffectId effect) => IndexOf(effect) >= 0;

    /// <summary>The magnitude an effect acts at, or zero when it is not acting.</summary>
    /// <param name="effect">The effect to read.</param>
    public int MagnitudeOf(EffectId effect)
    {
        int index = IndexOf(effect);
        return index >= 0 ? _active[index].Magnitude : 0;
    }

    /// <summary>Applies an effect, replacing the magnitude of one already acting.</summary>
    /// <param name="effect">The effect to apply.</param>
    public void Apply(PartyEffect effect)
    {
        int index = IndexOf(effect.Effect);
        if (index < 0)
        {
            _active.Add(effect);
            return;
        }

        _active[index] = effect;
    }

    /// <summary>Ends one effect, which is what a deadline passing or a dispelling does.</summary>
    /// <param name="effect">The effect to end.</param>
    /// <returns>Whether the effect was acting.</returns>
    public bool Remove(EffectId effect)
    {
        int index = IndexOf(effect);
        if (index < 0) return false;
        _active.RemoveAt(index);
        return true;
    }

    /// <summary>Ends every effect.</summary>
    public void Clear() => _active.Clear();

    private int IndexOf(EffectId effect)
    {
        for (int index = 0; index < _active.Count; index++)
        {
            if (_active[index].Effect == effect) return index;
        }

        return -1;
    }
}
