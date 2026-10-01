using System.Globalization;
using PartyRpg.Kit;
using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// This game's zombie: a dead character stood back up as one, and what the state does to them for as long as they
/// carry it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The state is a condition, and the condition is the member's.</b> A zombie is the condition this game names
/// <see cref="MightAndMagic7Conditions.Zombie"/>, held by the member's own condition state, so a save carries it with
/// every other condition and a load restores it with nothing beside it. What it does is read here from that one fact
/// by every owner a zombie differs in — the healing a spell gives, the night a rest gives, the drift of game time, and
/// the counter that ends it — rather than kept as a second flag anywhere.
/// </para>
/// <para>
/// <b>What it does, read from the donor's own condition handling</b>, each stated faithful or ours:
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>Rising.</b> Only a dead character rises, and not one eradicated, a Lich, or a zombie already; every condition
/// they carried ends, their health is filled and their spell points emptied (OpenEnroth
/// <c>src/Engine/Objects/Character.cpp:485-505</c>, <c>Character::SetCondition</c> for <c>CONDITION_ZOMBIE</c>, reached
/// from Reanimate at <c>src/Engine/Spells/CastSpellInfo.cpp:2632-2640</c>). Faithful, with two differences that are
/// ours: a casting at a member who cannot rise is refused before anything is spent, where the donor spends the points
/// and changes nothing (and, for a dead Lich, leaves the zombie mark on a body that stays dead); and the zombie
/// portrait and voice the donor swaps in are not drawn — the panel names the condition instead.
/// </description></item>
/// <item><description>
/// <b>Acting.</b> A zombie acts, casts, and fights: the donor's <c>Character::CanAct</c> does not read the condition
/// (<c>Character.cpp:350-357</c>), so <see cref="MightAndMagic7Conditions.CanAct"/> does not either. Faithful.
/// </description></item>
/// <item><description>
/// <b>Healing.</b> Health a spell or a potion gives a zombie stops at half their maximum (<c>Character.cpp:1283-1297</c>,
/// <c>Character::Heal</c>). Approximated: the donor clamps a zombie already above half down to half on any heal; this
/// gives nothing to one above half and takes nothing either, and the drift below brings them down. A regeneration is
/// not a heal in the donor — its tick fills to the full maximum (<c>Character.cpp:6494-6519</c>) and the drift then
/// pulls a zombie back — so it is not held at half here either. Faithful.
/// </description></item>
/// <item><description>
/// <b>Time.</b> For every five minutes of game time a zombie's health falls by one toward half its maximum (never
/// below it, and never raised to it) and their spell points by one toward nothing (<c>src/Engine/Engine.cpp:1236</c>,
/// <c>:1425-1429</c>). Faithful, counted on the calendar's own boundaries.
/// </description></item>
/// <item><description>
/// <b>Rest.</b> A night fills a zombie's pools as it fills anyone's, and then empties their spell points and halves
/// their health (<c>src/Engine/Party.cpp:737-739</c>, <c>Party::restAndHeal</c>). Faithful.
/// </description></item>
/// <item><description>
/// <b>Temples.</b> A temple's healing ends the state with every other condition and fills both pools
/// (<c>src/GUI/UI/Houses/Temple.cpp:33-81</c>), priced as an ordinary affliction because the donor's price table
/// reads the zombie as neither death nor eradication (<c>src/Engine/PriceCalculator.cpp:102-133</c>). The three
/// temples of the dark powers do not: they turn a zombie away and raise the dead, petrified, and eradicated as zombies
/// (<c>Temple.cpp:44-55</c>, <c>:178-188</c>). Faithful, with one difference that is ours: a dark temple still
/// heals a zombie's other afflictions, where the donor's turns the zombie away whole, because this build sells a cure
/// per family rather than one healing per member (<see cref="MightAndMagic7Conditions.Cures"/>).
/// </description></item>
/// </list>
/// </remarks>
internal sealed class MightAndMagic7Undeath : IGameTimeObserver
{
    /// <summary>How often a zombie's pools drift: every five minutes of game time, <c>Engine.cpp:1236</c>.</summary>
    private static readonly GameDuration DriftInterval = GameDuration.FromMinutes(5);

    private readonly Func<PartyEntity?> _party;
    private readonly GameCalendar _calendar;

    /// <summary>Creates the drift of game time on the party's zombies.</summary>
    /// <param name="party">The party the session plays, read when the clock moves because it may not exist yet.</param>
    /// <param name="calendar">The one clock's calendar, whose boundaries the drift is counted on.</param>
    internal MightAndMagic7Undeath(Func<PartyEntity?> party, GameCalendar calendar)
    {
        _party = party ?? throw new ArgumentNullException(nameof(party));
        _calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
    }

    /// <summary>Whether a member carries the zombie state.</summary>
    /// <param name="member">The member.</param>
    internal static bool IsZombie(PartyMember member)
    {
        ArgumentNullException.ThrowIfNull(member);
        return member.Conditions.Has(MightAndMagic7Conditions.Zombie);
    }

    /// <summary>Whether a casting can raise a member as a zombie, judged before anything is spent.</summary>
    /// <param name="member">The member the casting named.</param>
    /// <param name="spell">What the casting is called, for the sentence.</param>
    /// <returns>The refusal, or null when the member rises.</returns>
    internal static Refusal? JudgeRising(PartyMember member, string spell)
    {
        ArgumentNullException.ThrowIfNull(member);
        string name = member.Profile.Name;
        if (!member.Conditions.Has(MightAndMagic7Conditions.Dead))
        {
            return new Refusal(MightAndMagic7Codes.SpellNotABody, $"{spell} stands up the dead, and {name} is not dead.");
        }

        if (member.Conditions.Has(MightAndMagic7Conditions.Eradicated))
        {
            return new Refusal(MightAndMagic7Codes.SpellCannotRise, $"{spell} cannot raise {name}: nothing is left of an eradicated body to stand up.");
        }

        if (IsZombie(member))
        {
            return new Refusal(MightAndMagic7Codes.SpellCannotRise, $"{spell} cannot raise {name}, who is a zombie already.");
        }

        return MightAndMagic7BaseResistance.IsLich(member)
            ? new Refusal(MightAndMagic7Codes.SpellCannotRise, $"{spell} cannot raise {name}: a Lich does not rise as a zombie.")
            : null;
    }

    /// <summary>Stands a dead member back up as a zombie: every condition ends, health is filled, spell points emptied.</summary>
    /// <param name="member">The member, already judged able to rise.</param>
    /// <returns>What the member holds now, as the facts a panel shows.</returns>
    internal static IReadOnlyList<SpellEffectFact> Raise(PartyMember member)
    {
        ArgumentNullException.ThrowIfNull(member);
        member.Conditions.ClearAll();
        member.Conditions.Apply(new ActiveCondition(MightAndMagic7Conditions.Zombie));
        member.Resources.RestoreAll();
        member.Resources.TrySpendSpellPoints(member.Resources.SpellPoints.Current);
        return
        [
            new SpellEffectFact("zombie", member.Profile.Name),
            new SpellEffectFact("hitPoints", member.Resources.HitPoints.ToString()),
            new SpellEffectFact("spellPoints", member.Resources.SpellPoints.ToString()),
        ];
    }

    /// <summary>Gives a member health through their own pool, never past what this game lets them hold.</summary>
    /// <remarks>A zombie holds no more than half their maximum by healing (<c>Character.cpp:1287-1288</c>).</remarks>
    /// <param name="member">The member healed.</param>
    /// <param name="amount">How much the healing is worth.</param>
    /// <returns>How much was given.</returns>
    internal static int Heal(PartyMember member, int amount)
    {
        ArgumentNullException.ThrowIfNull(member);
        ResourcePool health = member.Resources.HitPoints;
        int ceiling = IsZombie(member) ? health.Maximum / 2 : health.Maximum;
        int given = Math.Clamp(amount, 0, Math.Max(0, ceiling - health.Current));
        if (given > 0) member.Resources.RestoreHitPoints(given);
        return given;
    }

    /// <summary>What a zombie keeps of a night's rest: no spell points, and half the health the night filled.</summary>
    /// <param name="member">The member who has just recovered.</param>
    internal static void Rested(PartyMember member)
    {
        ArgumentNullException.ThrowIfNull(member);
        if (!IsZombie(member)) return;
        CharacterResources pools = member.Resources;
        pools.TrySpendSpellPoints(pools.SpellPoints.Current);
        int half = pools.HitPoints.Current / 2;
        if (pools.HitPoints.Current - half > 0) pools.TakeDamage(pools.HitPoints.Current - half);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The donor skips the dead and the eradicated in the same pass (<c>Engine.cpp:1353-1354</c>), so a zombie that
    /// has been laid out again does not drift.
    /// </remarks>
    public void Observe(ClockAdvance advance)
    {
        ArgumentNullException.ThrowIfNull(advance);
        if (!advance.Moved || _party() is not { } party) return;
        long ticks = _calendar.Boundaries(advance.From, advance.To, DriftInterval);
        if (ticks <= 0) return;
        int step = (int)Math.Min(int.MaxValue, ticks);
        foreach (PartyMember member in party.Members)
        {
            if (!IsZombie(member)) continue;
            if (member.Conditions.Has(MightAndMagic7Conditions.Dead) || member.Conditions.Has(MightAndMagic7Conditions.Eradicated)) continue;
            CharacterResources pools = member.Resources;
            int current = pools.HitPoints.Current;
            int floor = pools.HitPoints.Maximum / 2;
            int lowered = Math.Min(current, Math.Max(floor, current - step));
            if (lowered < current) pools.TakeDamage(current - lowered);
            int spent = Math.Min(pools.SpellPoints.Current, step);
            if (spent > 0) pools.TrySpendSpellPoints(spent);
        }
    }

    /// <summary>How a zombie's pools read in a sentence.</summary>
    internal static string Describe(PartyMember member) =>
        string.Create(CultureInfo.InvariantCulture, $"{member.Profile.Name} rises as a zombie ({member.Resources.HitPoints} health, no spell points)");
}
