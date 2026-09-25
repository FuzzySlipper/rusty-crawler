using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Services;

/// <summary>
/// What a counter keeps for the party: coins left with it, held as party-carried state under a name content
/// states.
/// </summary>
/// <remarks>
/// <para>
/// <b>A holding is the party's, not the counter's.</b> The party hands coin over at a counter and the
/// counter owes it back; which counter, and how much, is party state exactly as a guild membership is, so it
/// is carried as a party-wide effect whose identity content names and whose magnitude is the coins held. A
/// save already records effects, so a deposit survives one, and there is no second ledger beside the party's
/// own purse for a save to forget.
/// </para>
/// <para>
/// <b>The kit moves the state; the game decides what a holding is.</b> What a counter calls the state, when
/// it may be opened, whether a deposit earns interest or pays a fee, and how much may be left are this
/// game's answers; the kit supplies the two operations — leave coin, take coin back — that any counter
/// keeping value needs, so a bank is content and policy rather than a class.
/// </para>
/// </remarks>
public static class ServiceHolding
{
    /// <summary>How many coins a counter holds for the party under one name.</summary>
    /// <param name="party">The party whose carried state is read.</param>
    /// <param name="holding">The state's name, as content states it.</param>
    /// <returns>The coins held, or zero when nothing is held.</returns>
    /// <exception cref="ArgumentNullException">The party is null.</exception>
    /// <exception cref="ArgumentException">The name is blank, which names no holding.</exception>
    public static int Coins(PartyEntity party, string holding)
    {
        ArgumentNullException.ThrowIfNull(party);
        EffectId effect = Effect(holding);
        return party.Effects.Has(effect) ? party.Effects.MagnitudeOf(effect) : 0;
    }

    /// <summary>Records what a counter now holds for the party.</summary>
    /// <remarks>
    /// A holding that reaches zero is removed rather than kept at zero: a counter that owes the party
    /// nothing is not a counter the party has an account with, and an effect left at zero would say in a
    /// save that it did.
    /// </remarks>
    /// <param name="party">The party the coins belong to.</param>
    /// <param name="holding">The state's name, as content states it.</param>
    /// <param name="coins">The coins held, which cannot be negative.</param>
    /// <exception cref="ArgumentNullException">The party is null.</exception>
    /// <exception cref="ArgumentException">The name is blank, which names no holding.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The amount is negative, which is not a holding.</exception>
    public static void Set(PartyEntity party, string holding, int coins)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentOutOfRangeException.ThrowIfNegative(coins);
        EffectId effect = Effect(holding);
        if (coins == 0)
        {
            party.Effects.Remove(effect);
            return;
        }

        party.Effects.Apply(new PartyEffect(effect, coins));
    }

    /// <summary>The effect identity a holding is carried under.</summary>
    /// <param name="holding">The state's name, as content states it.</param>
    /// <exception cref="ArgumentException">The name is blank, which names no holding.</exception>
    public static EffectId Effect(string holding)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(holding);
        return new EffectId(string.Concat("holding:", holding));
    }
}
