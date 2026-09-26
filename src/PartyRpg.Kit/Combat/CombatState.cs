using System.Globalization;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using Rusty.Engine;

namespace PartyRpg.Kit.Combat;

/// <summary>
/// The one combat state: who is fighting, on which side, and how long each actor must recover before it may
/// act again.
/// </summary>
/// <remarks>
/// <para>
/// <b>It stands over the live world and owns nothing of it.</b> There is no battle scene, no encounter world,
/// and no second population: entering a fight changes nothing about where the party is, what exists, or what
/// place it is in. What changes is that something in the place is an enemy and actors act by recovery. The
/// state reads the party's own members and the place's live entities through <see cref="ICombatWorld"/> and
/// keeps only what a fight adds — a side and a recovery quantity per actor.
/// </para>
/// <para>
/// <b>Recovery is the only pacing.</b> One quantity per actor, advanced from the session's one clock inside
/// the admitted update and by nothing else; every attack initiation is judged against it, whatever kind the
/// attack is, and a recovering actor cannot act. This is the quantity the turn-based pacing will derive its
/// order from — see <see cref="Combatant.Recovery"/> for that contract — which is why there is no second
/// timer, no per-action cooldown, and no scheduler here.
/// </para>
/// <para>
/// <b>Hostility is world state.</b> Every step re-reads what stands in the place: a creature is an enemy
/// because of what it is — its ruleset answer notices the party from where the party stands — or because of
/// what the party has done, which is remembered here and outlives any change of pacing. Nothing is a flag
/// saying "in combat": the fight is exactly the set of actors whose side is
/// <see cref="CombatSide.Opposition"/>, recomputed from the world every update.
/// </para>
/// <para>
/// <b>The mechanism is the kit's and the numbers are the ruleset's.</b> Attack initiation, recovery
/// accounting, targeting, and the state machine live here with no game words; recovery values, what
/// hostility means, how far an attack reaches, and — later — what an attack does to its target are answered
/// by <see cref="ICombatRule"/>.
/// </para>
/// </remarks>
public sealed class CombatState : IGameTimeObserver
{
    private readonly ICombatRule _rule;
    private readonly PartyEntity _party;
    private readonly ICombatWorld? _world;
    private readonly GameClock? _clock;
    private readonly IDiagnosticsService? _diagnostics;
    private readonly Dictionary<CombatantId, Combatant> _byId = [];
    private readonly HashSet<CombatantId> _provoked = [];
    private readonly List<Combatant> _combatants = [];
    private AttackInitiation? _lastAttack;
    private CombatResult? _lastOrder;

    /// <summary>Creates a fight over the party and the world it stands in.</summary>
    /// <param name="rule">This game's answers about recovery, hostility, reach, and what an actor is.</param>
    /// <param name="party">
    /// The party, which is the fight's own side: its members are combatants whether or not anything is
    /// hostile, so a party with nobody to fight still paces its own actions.
    /// </param>
    /// <param name="world">
    /// The world the fight stands over. Without one the party fights alone: there is nothing to be hostile
    /// and nothing to attack, and every action is an attack at nothing rather than an invented target.
    /// </param>
    /// <param name="clock">
    /// The session's one clock, read only to say when an attack happened. Recovery is advanced by the
    /// advances this state is handed and never by reading the clock, so a session without one still fights
    /// — it simply has no game time for recovery to elapse in, and says so by its initiations carrying no
    /// moment.
    /// </param>
    /// <param name="diagnostics">Where every order and refusal is reported. Optional, and nothing depends on it.</param>
    /// <exception cref="ArgumentNullException">No rule or no party was supplied.</exception>
    public CombatState(
        ICombatRule rule,
        PartyEntity party,
        ICombatWorld? world = null,
        GameClock? clock = null,
        IDiagnosticsService? diagnostics = null)
    {
        _rule = rule ?? throw new ArgumentNullException(nameof(rule));
        _party = party ?? throw new ArgumentNullException(nameof(party));
        _world = world;
        _clock = clock;
        _diagnostics = diagnostics;
    }

    /// <summary>
    /// Every actor the last <see cref="Step"/> found: the party's members first, in roster order, then the
    /// place's creatures in the order content declared them.
    /// </summary>
    /// <remarks>
    /// The order is stable and content's own rather than an ordering the fight invents, so a projection and a
    /// test read the same fight the same way twice. It is deliberately not an initiative order: which actor
    /// acts first is the pacing's business, and the second pacing will state its own.
    /// </remarks>
    public IReadOnlyList<Combatant> Combatants => _combatants;

    /// <summary>The actors currently fighting the party, in combatant order.</summary>
    public IReadOnlyList<Combatant> Opposition =>
        [.. _combatants.Where(combatant => combatant.Side == CombatSide.Opposition)];

    /// <summary>Whether anything in the place is fighting the party right now.</summary>
    public bool IsEngaged => _combatants.Any(combatant => combatant.Side == CombatSide.Opposition);

    /// <summary>What the last accepted attack was, or null before anything has attacked.</summary>
    /// <remarks>
    /// This is where what an attack <em>was</em> becomes readable, and it is the record a later stone
    /// consumes to resolve one: who acted, how, against what, when, and what it cost. Nothing here resolves
    /// anything, so an initiation is a fact about a fight and not a promise about its outcome.
    /// </remarks>
    public AttackInitiation? LastAttack => _lastAttack;

    /// <summary>
    /// What the last order to attack did, whether it applied or was refused, or null before any was given.
    /// </summary>
    /// <remarks>
    /// A refusal leaves no initiation, so the fight keeps the last answer as its own fact: an actor ignored
    /// while recovering must be readable as a refusal rather than as a fight in which nothing was asked.
    /// </remarks>
    public CombatResult? LastOrder => _lastOrder;

    /// <summary>
    /// Re-reads the world: which actors are in the fight, on which side, how far off, and how they attack.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the whole of what a fight does without being asked: the party's members are always combatants,
    /// and the place's creatures join as what they are decides — a creature that attacks on sight is an enemy
    /// once the party is inside its notice range, and a creature the party has already attacked stays one.
    /// An actor that was already here keeps its recovery, which is what makes this a re-read of the world
    /// rather than a new fight every update.
    /// </para>
    /// <para>
    /// An actor that is gone — the party left the place, or the world restored it — is dropped here, because
    /// the entities a population creates live only for the visit that made them. What the party had done to
    /// them goes with them: a place the world rebuilt is a place nobody has attacked yet.
    /// </para>
    /// </remarks>
    public void Step()
    {
        List<Combatant> live = [];
        PlaceId place = _world?.Place ?? default;
        PlacePose partyPose = _world?.Pose ?? PlacePose.Origin;

        foreach (PartyMember member in _party.Members)
        {
            if (!member.IsAlive) continue;
            CombatSubject subject = new(CombatantId.Of(member.Id), place, partyPose, member, entity: null);
            string name = _rule.NameOf(subject);
            AttackKind kind = _rule.AttackKindFor(subject);
            // A member of the party begins ready: standing somewhere is not a reason for a character to be
            // unable to act, and the donor's own party members enter turn-based mode with their recovery as
            // it stands, which is how a party enters a turn-based fight with the recovery it already owed.
            Combatant combatant = Existing(subject.Id) ??
                new Combatant(subject, CombatSide.Party, name, kind, distance: 0, GameDuration.None);
            combatant.Observe(CombatSide.Party, name, kind, distance: 0);
            live.Add(combatant);
        }

        if (_world is { } world)
        {
            foreach (PlacePopulationEntity entity in world.Population)
            {
                if (!entity.IsAlive) continue;
                CombatSubject subject = new(CombatantId.Of(entity.Id), world.Place, entity.Pose, member: null, entity);
                if (_rule.NatureOf(subject) is not { IsCreature: true } nature) continue;

                double distance = Distance(world.Pose, subject.Pose);
                CombatSide side = _provoked.Contains(subject.Id) || nature.Notices(distance)
                    ? CombatSide.Opposition
                    : CombatSide.Neutral;
                string name = _rule.NameOf(subject);
                AttackKind kind = _rule.AttackKindFor(subject);
                Combatant combatant = Existing(subject.Id) ??
                    new Combatant(subject, side, name, kind, distance, _rule.InitialRecovery(subject, kind));
                combatant.Observe(side, name, kind, distance);
                live.Add(combatant);
            }
        }

        _combatants.Clear();
        _byId.Clear();
        foreach (Combatant combatant in live)
        {
            _combatants.Add(combatant);
            _byId[combatant.Id] = combatant;
        }

        // What the party has done is remembered for the actors that are here to be angry about it. A
        // provocation that outlived its actor would make the next visit's entity hostile for something done
        // to a creature that no longer exists.
        _provoked.RemoveWhere(id => !_byId.ContainsKey(id));
    }

    /// <summary>
    /// The party attacks what it can reach: every member who is ready acts, and every member who is not is
    /// refused by name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is what the act control means: an attack at the nearest creature in reach, resolved by the
    /// ruleset into a spell, a shot, or a swing. The kit has no notion of an active character yet, so the
    /// order is given to every member at once and each pays its own recovery for it: the party acts, and who
    /// may act is decided per actor by the recovery it has left. A member with nothing in reach still
    /// attacks at nothing and still pays, which is the honest outcome when there is nothing to hit.
    /// </para>
    /// <para>
    /// The target is the nearest creature that is not one of the party's own, within the reach the ruleset
    /// gives that member for the kind of attack it makes. Facing is not consulted: the party's facing and a
    /// world actor's position are in different units until a view stone relates them, and this stone would
    /// rather pick the nearest enemy than pretend to aim.
    /// </para>
    /// </remarks>
    /// <returns>One result per party member, in combatant order.</returns>
    public IReadOnlyList<CombatResult> Engage()
    {
        List<CombatResult> results = [];
        foreach (Combatant combatant in _combatants)
        {
            if (combatant.Side != CombatSide.Party) continue;
            Combatant? target = Nearest(combatant);
            results.Add(Order(new AttackOrder(combatant.Id, combatant.PreferredKind, target?.Id)));
        }

        return results;
    }

    /// <summary>
    /// Applies one order to attack, if the actor may act.
    /// </summary>
    /// <remarks>
    /// This is the fight's one entry for making an actor act, whoever asked: the player's control through
    /// <see cref="Engage"/>, and whoever drives the creatures on the other side. The actor's own recovery
    /// decides whether it may, so a creature that is not ready cannot be driven to act by a later AI owner
    /// any more than a character can be driven to act by a key — one pacing, one gate.
    /// </remarks>
    /// <param name="order">Who acts, how, and against what.</param>
    /// <returns>What the order did, or why it did not.</returns>
    /// <exception cref="ArgumentNullException">No order was supplied.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The order names no kind of attack this kit knows.</exception>
    public CombatResult Order(AttackOrder order)
    {
        ArgumentNullException.ThrowIfNull(order);
        if (order.Kind is not (AttackKind.Melee or AttackKind.Ranged or AttackKind.Spell))
        {
            throw new ArgumentOutOfRangeException(
                nameof(order),
                order.Kind,
                "An attack is hand-to-hand, at range, or a spell; an order naming any other kind would be paced by an answer no ruleset gave.");
        }

        if (!_byId.TryGetValue(order.Actor, out Combatant? actor))
        {
            return Report(CombatResult.Refused(
                order.Actor,
                actorName: null,
                "unknown-combatant",
                $"No combatant '{order.Actor}' is in this fight, so nothing acted; a fight holds the party's members and the creatures of the place the party stands in."));
        }

        if (!actor.IsReady)
        {
            return Report(CombatResult.Refused(
                actor.Id,
                actor.Name,
                "recovering",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{actor.Name} is still recovering: {actor.Recovery.Milliseconds}ms of game time must pass before it can act again.")));
        }

        Combatant? target = null;
        if (order.Target is { } targetId)
        {
            if (!_byId.TryGetValue(targetId, out target))
            {
                return Report(CombatResult.Refused(
                    actor.Id,
                    actor.Name,
                    "unknown-target",
                    $"No combatant '{targetId}' is in this fight, so {actor.Name} attacked nothing."));
            }

            if (target.Side == CombatSide.Party && actor.Side == CombatSide.Party)
            {
                return Report(CombatResult.Refused(
                    actor.Id,
                    actor.Name,
                    "friendly-target",
                    $"{actor.Name} and {target.Name} are on the party's own side, so the order was refused rather than turned on the party."));
            }
        }

        GameDuration recovery = _rule.RecoveryAfter(actor.Subject, order.Kind);
        actor.Spend(recovery);

        // What the party has done is what puts a creature into the fight: attacking it is remembered, so it
        // stays an enemy for as long as it stands there, whatever pacing is in force.
        if (target is not null && target.Side != CombatSide.Party)
        {
            _provoked.Add(target.Id);
            target.Provoke();
        }

        AttackInitiation initiation = new(
            actor.Id,
            actor.Name,
            order.Kind,
            target?.Id,
            target?.Name ?? string.Empty,
            _clock?.Now,
            recovery);
        _lastAttack = initiation;
        return Report(CombatResult.Applied(initiation));
    }

    /// <summary>The combatant an identity belongs to, or null when no actor in this fight has it.</summary>
    /// <param name="id">The identity to look for.</param>
    public Combatant? Find(CombatantId id) => _byId.GetValueOrDefault(id);

    /// <summary>
    /// Advances every recovering actor by the game time the session's clock just moved.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the only thing that releases an actor, and it is handed to the state by the session's one
    /// clock: the same advance a shelf restocks on and a debt of sleep falls due on, whether it came from an
    /// admitted update, a journey's charge, or a night's sleep. Nothing else may move recovery — no frame, no
    /// timer, and no second loop — and an advance of no time moves nobody, which is what a held session
    /// produces.
    /// </para>
    /// <para>
    /// An actor that has already recovered stays ready: recovery is a debt, not a cycle, so time passing
    /// while an actor is ready does not bank actions for it.
    /// </para>
    /// </remarks>
    /// <param name="advance">Where the clock was, where it went, and how much game time passed.</param>
    /// <exception cref="ArgumentNullException">No advance was supplied.</exception>
    public void Observe(ClockAdvance advance)
    {
        ArgumentNullException.ThrowIfNull(advance);
        if (!advance.Moved) return;
        foreach (Combatant combatant in _combatants) combatant.Recover(advance.Elapsed);
    }

    /// <summary>The combatant already in this fight under an identity, or null when none is.</summary>
    private Combatant? Existing(CombatantId id) => _byId.GetValueOrDefault(id);

    /// <summary>
    /// The creature a party member should attack: the nearest one that is not on the party's own side,
    /// within the reach the ruleset gives that member for the attack it makes.
    /// </summary>
    /// <remarks>
    /// Distance decides, and a tie is broken by the order the combatants were read in — the roster first, then
    /// content's own order — so the same fight picks the same target twice. A neutral creature is a legal
    /// target: the party may attack a person, and doing so is what makes them an enemy.
    /// </remarks>
    private Combatant? Nearest(Combatant actor)
    {
        double reach = _rule.ReachOf(actor.Subject, actor.PreferredKind);
        Combatant? nearest = null;
        foreach (Combatant candidate in _combatants)
        {
            if (candidate.Side == CombatSide.Party || candidate.Distance > reach) continue;
            if (nearest is null || candidate.Distance < nearest.Distance) nearest = candidate;
        }

        return nearest;
    }

    /// <summary>How far apart two actors stand, in the place's own units.</summary>
    private static double Distance(PlacePose from, PlacePose to)
    {
        double x = to.X - from.X;
        double y = to.Y - from.Y;
        double z = to.Z - from.Z;
        return Math.Sqrt((x * x) + (y * y) + (z * z));
    }

    /// <summary>Reports what one order did, whether it applied or was refused.</summary>
    /// <remarks>
    /// Every order is reported, because a control that silently did nothing looks exactly like a control that
    /// worked: an actor ignored while recovering, and a target that was never there, are both facts about the
    /// fight that nothing else on screen states.
    /// </remarks>
    private CombatResult Report(CombatResult result)
    {
        _lastOrder = result;
        _diagnostics?.Publish(new DiagnosticsPublishRequest(
            DiagnosticsSeverity.Info,
            DiagnosticsDisposition.Accepted,
            Source: "combat",
            Code: result.IsApplied ? "combat-attacked" : "combat-refused",
            Message: _world is { } world
                ? $"In place '{world.Place}' at {world.Pose}: {result.Message}"
                : result.Message,
            Correlation: string.Empty));
        return result;
    }
}
