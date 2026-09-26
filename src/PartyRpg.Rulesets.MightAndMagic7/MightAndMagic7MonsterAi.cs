using System.Globalization;
using System.Text.Json;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.World;
using Rusty.Engine;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// This game's answer about how a monster behaves: who it hates, how fast it moves, and what it does with
/// each moment of a fight.
/// </summary>
/// <remarks>
/// <para>
/// <b>Everything here is a reading of the shipped tables.</b> What a creature is worth, how far it notices,
/// whether it runs when hurt, whether it closes or holds, which of its attacks and spells it uses and how
/// often, and which kinds of monster it counts as enemies all come from the monster table, the map table,
/// and the hostility matrix the importer wrote. Nothing in this class is a per-monster branch: a new kind of
/// monster is a row of content, and a change to how one behaves is a change to that row.
/// </para>
/// <para>
/// <b>The donor's own order of decisions.</b> A creature runs first when its AI class and its wounds say so,
/// then it picks the nearest thing it hates, then it decides which of its abilities to use, and only then
/// does it act or close — which is the order of <c>Actor::UpdateActorAI</c>
/// (<c>src/Engine/Objects/Actor.cpp:2660-2810</c>): the fear check, the flee thresholds, the target, the
/// ability, and the melee-versus-pursue choice.
/// </para>
/// <para>
/// <b>Every chance is drawn from the engine's keyed service.</b> The ability a creature uses is a draw keyed
/// by the place, the creature, and how many decisions it has made, so the same fight replays identically and
/// nothing about a creature's choice has to be carried in a save. A product composed without a random
/// service takes no chances at all: a creature then uses its first attack and never casts, which is a
/// creature whose behavior can be stated without drawing rather than one whose behavior is random.
/// </para>
/// </remarks>
internal sealed class MightAndMagic7MonsterAi : IMonsterAiPolicy
{
    /// <summary>The AI class whose creature always runs: the donor's <c>MONSTER_AI_WIMP</c>.</summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/Objects/MonsterEnums.h:505-512</c>: suicide never runs, a wimp always runs,
    /// a normal creature runs at twenty percent of its hit points and an aggressive one at ten. The shipped
    /// table spells them <c>Suicidal</c>, <c>Wimp</c>, <c>Normal</c>, and <c>Aggress</c>
    /// (<c>src/Engine/Objects/Monsters.cpp:271-276</c>).
    /// </remarks>
    internal const string WimpAiType = "Wimp";

    /// <summary>The AI class that runs when badly hurt.</summary>
    internal const string NormalAiType = "Normal";

    /// <summary>The AI class that runs only when nearly finished.</summary>
    internal const string AggressiveAiType = "Aggress";

    /// <summary>At how much of its hit points a normal creature runs.</summary>
    /// <remarks>OpenEnroth <c>src/Engine/Objects/Actor.cpp:2695</c>: <c>hp * 0.2</c>.</remarks>
    internal const double NormalFleeFraction = 0.2;

    /// <summary>At how much of its hit points an aggressive creature runs.</summary>
    /// <remarks>OpenEnroth <c>src/Engine/Objects/Actor.cpp:2697</c>: <c>hp * 0.1</c>.</remarks>
    internal const double AggressiveFleeFraction = 0.1;

    /// <summary>How far off a hurt creature still bothers to run, in place units.</summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/Objects/Actor.cpp:2699</c>: the flee branch is taken only while the target is
    /// within 10240 units, which is the widest notice band the game has.
    /// </remarks>
    internal const double FleeRange = 10240;

    /// <summary>How far off a creature treats another creature as an enemy, by the matrix's band.</summary>
    /// <remarks>
    /// The donor turns a matrix band into a distance in <c>Actor::UpdateActorAI</c>
    /// (<c>src/Engine/Objects/Actor.cpp:2666-2675</c>): the closest band means at any distance, then 1024,
    /// 2560, and 5120 units. Those are the same values the notice ranges are built from
    /// (<c>Actor.cpp:55-61</c>).
    /// </remarks>
    private static readonly double[] BandRanges = [0, 0, 1024, 2560, 5120];

    /// <summary>The scope this game's ability rolls are drawn under, so they cannot collide with another owner's.</summary>
    internal const string AbilityRollScope = "mm7.combat.ability";

    /// <summary>The seed every ability roll of this game's fights is drawn from.</summary>
    internal const ulong AbilityRollSeed = 0x5C1E_7A11_5EE9_0003;

    /// <summary>The placement field a creature's own group is stated in, which is who fights beside it.</summary>
    /// <remarks>
    /// A spawn record's own group is carried onto the creatures it puts on the field, and the donor skips an
    /// actor of the same non-zero group when it looks for somebody to attack
    /// (<c>src/Engine/Objects/Actor.cpp:2062-2065</c>): creatures placed as one band do not turn on each
    /// other.
    /// </remarks>
    internal const string GroupField = "group";

    private readonly MightAndMagic7Combat _combat;
    private readonly MightAndMagic7Hostility _hostility;
    private readonly IRandomService? _random;

    private MightAndMagic7MonsterAi(MightAndMagic7Combat combat, MightAndMagic7Hostility hostility, IRandomService? random)
    {
        _combat = combat;
        _hostility = hostility;
        _random = random;
    }

    /// <summary>How many kinds state a band toward anything, so a report can say the matrix was read.</summary>
    internal int Kinds => _hostility.Kinds;

    /// <summary>
    /// Reads this game's hostility matrix and composes the policy over the fight's own readings.
    /// </summary>
    /// <param name="catalog">The validated content the product loaded, when it loaded any.</param>
    /// <param name="combat">This game's combat policy, which is where a creature's own row is read.</param>
    /// <param name="random">
    /// The engine's random service, which a creature's ability choice is drawn from. Without one no creature
    /// casts and none uses its second attack: a product that cannot draw takes no chances.
    /// </param>
    /// <returns>This game's monster policy.</returns>
    /// <exception cref="ContentValidationException">The content states a hostility matrix this game cannot read; every problem is named.</exception>
    internal static MightAndMagic7MonsterAi Compose(ContentCatalog? catalog, MightAndMagic7Combat combat, IRandomService? random)
    {
        ArgumentNullException.ThrowIfNull(combat);
        return new MightAndMagic7MonsterAi(combat, MightAndMagic7Hostility.Read(catalog), random);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <b>Two creatures are enemies when the shipped matrix says so.</b> The matrix is indexed by kind rather
    /// than by row, so the three graded variants of one monster share a row and a column, and a band of zero
    /// is friendship. A band the matrix states nothing for is friendship too, which is the donor's own
    /// reading: it fills every relation with friendly before it reads a cell.
    /// </para>
    /// <para>
    /// <b>Creatures placed as one band do not fight each other.</b> The donor skips an actor of the same
    /// non-zero group (<c>src/Engine/Objects/Actor.cpp:2062-2065</c>), which is what the spawn record's own
    /// group means here.
    /// </para>
    /// <para>
    /// A member of the party is never an enemy by this question: the party is the fight's own side, and a
    /// creature's hostility toward it is what the monster row's own band states — which is the answer the
    /// fight already asked for when it decided the creature was in the fight at all.
    /// </para>
    /// </remarks>
    public bool AreEnemies(CombatSubject self, CombatSubject other)
    {
        ArgumentNullException.ThrowIfNull(self);
        ArgumentNullException.ThrowIfNull(other);
        if (self.Member is not null || other.Member is not null) return false;
        if (SameGroup(self, other)) return false;
        if (_combat.FactsOf(self) is not { } mine || _combat.FactsOf(other) is not { } theirs) return false;

        return _hostility.IsEnemy(MightAndMagic7Hostility.KindOf(mine.Id), MightAndMagic7Hostility.KindOf(theirs.Id));
    }

    /// <inheritdoc />
    /// <remarks>
    /// The monster table's own speed column, which is the pace the donor moves a creature at
    /// (<c>OpenEnroth src/Engine/Objects/Actor.cpp:2259</c>, <c>moveSpeed</c>) — the one place a creature's
    /// pace is stated, so a slow kind and a fast one differ here and nowhere else. A row that states no speed
    /// leaves the engine's own profile in place rather than freezing the creature.
    /// </remarks>
    public double SpeedOf(CombatSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);
        return _combat.FactsOf(subject)?.Speed ?? 0;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The order of the donor's own AI, one step at a time: run if the creature's class and wounds say so,
    /// otherwise pick the nearest thing it hates, and either act with the ability its own chances choose or
    /// close on the target. A stationary creature never closes: it holds its post and acts when its target
    /// comes to it.
    /// </remarks>
    public CreatureDecision Decide(CreatureSituation situation)
    {
        ArgumentNullException.ThrowIfNull(situation);
        if (_combat.FactsOf(situation.Self.Subject) is not { } facts) return CreatureDecision.Wait;

        CreatureCandidate? target = Target(situation, facts);
        if (target is not { } chosen) return CreatureDecision.Wait;

        if (Runs(situation, facts, chosen.Distance))
        {
            // A creature that holds its post cannot run from it; it stands, which is what the donor's own
            // stationary branch does with a wimp (<c>src/Engine/Objects/Actor.cpp:2678-2684</c>).
            return facts.IsStationary ? CreatureDecision.Wait : CreatureDecision.Retreat(chosen.Actor.Id);
        }

        if (!situation.Ready)
        {
            // It has not lost its intent, only its turn: within reach of the ways it has it holds, which is
            // the donor's own stand while recovering, and out of reach it closes, which is what keeps a
            // creature coming instead of standing still between blows.
            return InReach(facts, situation, chosen, Far(facts)) || facts.IsStationary
                ? CreatureDecision.Wait
                : CreatureDecision.Advance(chosen.Actor.Id);
        }

        string ability = Choose(facts, situation.Round, situation.Self.Subject);
        (AttackKind kind, string named) = Ability(facts, ability);
        return InReach(facts, situation, chosen, kind)
            ? CreatureDecision.Attack(chosen.Actor.Id, kind, named)
            : facts.IsStationary ? CreatureDecision.Wait : CreatureDecision.Advance(chosen.Actor.Id);
    }

    /// <summary>Whether a candidate stands inside what a creature reaches with one of its ways of attacking.</summary>
    private bool InReach(
        MightAndMagic7Combat.MonsterFacts facts,
        CreatureSituation situation,
        CreatureCandidate candidate,
        AttackKind kind)
    {
        _ = facts;
        return candidate.Distance <= _combat.ReachOf(situation.Self.Subject, kind);
    }

    /// <summary>
    /// How far a creature's own ways of attacking reach, whichever of them it ends up using.
    /// </summary>
    /// <remarks>
    /// A creature that can throw or cast stops at that distance while it recovers, and one with nothing but
    /// its hands walks all the way in: how far a creature has to get is a property of what it fights with,
    /// which is why the row's own attack and spell columns answer it.
    /// </remarks>
    private static AttackKind Far(MightAndMagic7Combat.MonsterFacts facts) =>
        facts.First.Throws || facts.Second.Throws || facts.FirstSpell.IsUsable || facts.SecondSpell.IsUsable
            ? AttackKind.Ranged
            : AttackKind.Melee;

    /// <summary>Whether the creature has had enough and runs, by its own class and its own wounds.</summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/Objects/Actor.cpp:2676-2702</c>: a wimp always runs, a normal creature runs
    /// below twenty percent of its hit points, an aggressive one below ten, and a suicide never does; the
    /// hurt creature only runs while its target is within 10240 units.
    /// </remarks>
    private static bool Runs(CreatureSituation situation, MightAndMagic7Combat.MonsterFacts facts, double distance)
    {
        string ai = facts.AiType.Trim();
        if (ai.Equals(WimpAiType, StringComparison.OrdinalIgnoreCase)) return true;
        if (distance >= FleeRange) return false;
        if (ai.Equals(NormalAiType, StringComparison.OrdinalIgnoreCase)) return situation.Fraction < NormalFleeFraction;
        if (ai.Equals(AggressiveAiType, StringComparison.OrdinalIgnoreCase)) return situation.Fraction < AggressiveFleeFraction;
        return false;
    }

    /// <summary>
    /// Which actor the creature goes for: the nearest it hates, with the party counting as hated while the
    /// creature is fighting it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The donor picks the nearest actor it does not call friendly and prefers the party only when the party
    /// is nearer (<c>OpenEnroth src/Engine/Objects/Actor.cpp:2032-2120</c>, <c>_SelectTarget</c>, which
    /// compares squared distances and keeps the smallest). Two kinds of monster standing in one place
    /// therefore fight each other when the matrix says they hate each other and the party is not closer,
    /// which is what makes a mixed population a fight rather than a queue.
    /// </para>
    /// <para>
    /// <b>How far a creature looks for another creature is the matrix's own band.</b> A creature that is
    /// hostile to the party uses its own notice band, because the donor replaces the band with its own
    /// hostility class for a hostile actor (<c>Actor.cpp:2095-2097</c>); a creature that is not uses the
    /// band the matrix states, which the closest band makes unbounded (<c>Actor.cpp:2666-2675</c>).
    /// </para>
    /// </remarks>
    private CreatureCandidate? Target(CreatureSituation situation, MightAndMagic7Combat.MonsterFacts facts)
    {
        CreatureCandidate? nearest = null;
        foreach (CreatureCandidate candidate in situation.Candidates)
        {
            if (!candidate.IsEnemy) continue;
            if (!Within(candidate, situation, facts)) continue;
            if (nearest is not { } found || candidate.Distance < found.Distance) nearest = candidate;
        }

        return nearest;
    }

    /// <summary>Whether a candidate stands inside the distance this creature looks that far.</summary>
    private bool Within(CreatureCandidate candidate, CreatureSituation situation, MightAndMagic7Combat.MonsterFacts facts)
    {
        // The party is as near as it stands: what makes it a target is the creature's own hostility, which
        // is the band its row states and which the fight has already read to decide the creature is in the
        // fight at all.
        if (candidate.IsParty) return true;

        if (situation.Self.Side == CombatSide.Opposition && facts.NoticeRange > 0)
        {
            return candidate.Distance <= facts.NoticeRange;
        }

        int band = Band(situation.Self.Subject, candidate.Actor.Subject);
        return band switch
        {
            1 => true,
            >= 2 and <= 4 => candidate.Distance <= BandRanges[band],
            _ => false,
        };
    }

    /// <summary>The band one creature holds toward another, or zero when the matrix states none.</summary>
    private int Band(CombatSubject self, CombatSubject other)
    {
        if (_combat.FactsOf(self) is not { } mine || _combat.FactsOf(other) is not { } theirs) return 0;
        return _hostility.Band(MightAndMagic7Hostility.KindOf(mine.Id), MightAndMagic7Hostility.KindOf(theirs.Id));
    }

    /// <summary>Whether two actors are placements of the same non-zero group, which makes them allies.</summary>
    private static bool SameGroup(CombatSubject self, CombatSubject other)
    {
        int mine = Group(self);
        return mine != 0 && mine == Group(other);
    }

    /// <summary>The group a placement states, or zero when it states none.</summary>
    private static int Group(CombatSubject subject) =>
        subject.Placement is { } placement ? placement.Source.GetInt32(GroupField) ?? 0 : 0;

    /// <summary>
    /// Which of the creature's own ways of attacking it uses, by the donor's own order of chances.
    /// </summary>
    /// <remarks>
    /// <para>
    /// OpenEnroth <c>src/Engine/Objects/Actor.cpp:3644-3657</c> (<c>special_ability_use_check</c>): the first
    /// spell is rolled for, then the second, then the second attack, and the first attack is what is left. A
    /// spell the creature cannot cast — one whose own content states no harm this build can resolve — is not
    /// rolled for at all, which is what the donor's <c>_427102_IsOkToCastSpell</c> does with a spell that
    /// cannot usefully be cast.
    /// </para>
    /// <para>
    /// Each roll is a keyed draw of the engine's own service, keyed by the place, the creature, how many
    /// decisions it has made, and which ability is being rolled for, so the same fight draws the same
    /// abilities however often it is replayed and no choice has to be recorded. A product with no random
    /// service rolls nothing and falls through to the first attack.
    /// </para>
    /// </remarks>
    private string Choose(MightAndMagic7Combat.MonsterFacts facts, long round, CombatSubject self)
    {
        if (_random is null) return MightAndMagic7Combat.AbilityAttack1;
        if (facts.FirstSpell.IsUsable && Roll(self, round, MightAndMagic7Combat.AbilitySpell1) < facts.FirstSpell.UseChance)
        {
            return MightAndMagic7Combat.AbilitySpell1;
        }

        if (facts.SecondSpell.IsUsable && Roll(self, round, MightAndMagic7Combat.AbilitySpell2) < facts.SecondSpell.UseChance)
        {
            return MightAndMagic7Combat.AbilitySpell2;
        }

        if (facts.SecondChance > 0 && Roll(self, round, MightAndMagic7Combat.AbilityAttack2) < facts.SecondChance)
        {
            return MightAndMagic7Combat.AbilityAttack2;
        }

        return MightAndMagic7Combat.AbilityAttack1;
    }

    /// <summary>One chance in a hundred, drawn for one creature and one ability.</summary>
    private long Roll(CombatSubject self, long round, string ability)
    {
        string key = string.Create(CultureInfo.InvariantCulture, $"{self.Place}/{self.Id}/{round}/{ability}");
        return _random!
            .DrawKeyed(new KeyedRngRequest(AbilityRollSeed, AbilityRollScope, key, 0, 99))
            .Value;
    }

    /// <summary>How the chosen ability is made, and what the order calls it.</summary>
    private static (AttackKind Kind, string Ability) Ability(MightAndMagic7Combat.MonsterFacts facts, string ability) => ability switch
    {
        MightAndMagic7Combat.AbilitySpell1 => (AttackKind.Spell, MightAndMagic7Combat.AbilitySpell1),
        MightAndMagic7Combat.AbilitySpell2 => (AttackKind.Spell, MightAndMagic7Combat.AbilitySpell2),
        MightAndMagic7Combat.AbilityAttack2 when facts.Second.Roll.Maximum > 0 =>
            (facts.Second.AttackKind, MightAndMagic7Combat.AbilityAttack2),
        _ => (facts.First.AttackKind, MightAndMagic7Combat.AbilityAttack1),
    };
}
