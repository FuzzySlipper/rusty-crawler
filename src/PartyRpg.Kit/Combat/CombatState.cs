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
public sealed partial class CombatState : IGameTimeObserver
{
    private readonly ICombatRule _rule;
    private readonly ICombatResolutionRule? _resolution;
    private readonly ICombatAbilityResolutionRule? _abilities;
    private readonly ICombatWeaponRule? _weapons;
    private readonly ICombatReflectionRule? _reflection;
    private readonly ICombatProvocationRule? _provocation;
    private readonly IReadOnlyList<ICreatureDeathObserver> _deaths;
    private readonly IReadOnlyList<ICombatHitObserver> _hits;
    private readonly PartyEntity _party;
    private readonly ICombatWorld? _world;
    private readonly GameClock? _clock;
    private readonly IDiagnosticsService? _diagnostics;
    private readonly Dictionary<CombatantId, Combatant> _byId = [];
    private readonly HashSet<CombatantId> _provoked = [];
    private readonly List<Combatant> _combatants = [];
    private readonly List<CombatBlow> _blows = [];
    private long _blowSerial;
    private AttackInitiation? _lastAttack;
    private CombatResult? _lastOrder;
    private CombatResolution? _lastResolution;
    private long _attacksResolved;
    private long _stamp = ChangeStamp.Next();
    private bool _suppressStamp;

    /// <summary>Creates a fight over the party and the world it stands in.</summary>
    /// <param name="rules">
    /// This game's answers about recovery, hostility, reach, and what an actor is, with each further capability
    /// named. A fight given a resolution resolves what its attacks do; one given only pacing fights without
    /// resolving anything, and every attack is recorded as an attempt that came to nothing.
    /// </param>
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
    /// <exception cref="ArgumentNullException">No rules or no party was supplied.</exception>
    public CombatState(
        CombatRules rules,
        PartyEntity party,
        ICombatWorld? world = null,
        GameClock? clock = null,
        IDiagnosticsService? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _saving = rules.Saving;
        _corpses = rules.Corpses;
        _rule = rules.Rule ?? throw new ArgumentNullException(nameof(rules));
        _hits = rules.Hits ?? [];
        _resolution = rules.Resolution;
        _abilities = rules.Abilities;
        _weapons = rules.Weapons;
        _reflection = rules.Reflection;
        _provocation = rules.Provocation;
        _deaths = rules.Deaths ?? [];
        _party = party ?? throw new ArgumentNullException(nameof(party));
        _world = world;
        _clock = clock;
        _diagnostics = diagnostics;
        Turns = new TurnBasedPacing(this);
    }

    /// <summary>Which of the two pacings this one fight is being played in.</summary>
    /// <remarks>
    /// The pacing is the only thing a switch changes. Health, position, conditions, recovery, corpses, and
    /// what the party has provoked are the fight's state, and both pacings read exactly as it stands — which
    /// is why a fight can be switched in the middle of a round and continued in the other mode.
    /// </remarks>
    public CombatPacing Pacing { get; private set; } = CombatPacing.RealTime;

    /// <summary>Moves whenever saved fight state changes.</summary>
    public long Stamp => _stamp;

    /// <summary>Marks a canonical fight mutation for the session save boundary.</summary>
    internal void Touch()
    {
        if (!_suppressStamp) _stamp = ChangeStamp.Next();
    }

    /// <summary>The party this fight is fought by.</summary>
    public PartyEntity Party => _party;

    /// <summary>The turn-based pacing of this same fight: the order, the round, and the two phases.</summary>
    /// <remarks>
    /// It is always present and always reads this state; it holds nothing while the pacing is real time,
    /// because a round exists only while the mode is on. It is not a second combat: what it keeps is the
    /// round's own bookkeeping — whose turn it is, how much of the round has passed, and what is left of the
    /// party's movement phase — and every quantity it orders or prices an actor by is read from here.
    /// </remarks>
    public TurnBasedPacing Turns { get; }

    /// <summary>
    /// Switches the pacing, and changes nothing else about the fight.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the whole of what the toggle does. No actor is re-read, no recovery is reset, no condition is
    /// cleared, and no body is removed: the same state that was being paced in real time is paced in rounds
    /// from the recovery it already holds, so a party that switches mid-fight keeps the recovery each of its
    /// members owed and the creatures keep the distance they had closed.
    /// </para>
    /// <para>
    /// A round begins when the pacing is switched on into a fight, or by itself when a fight starts while the
    /// mode is already on. Switching the mode off abandons the round, never the fight.
    /// </para>
    /// </remarks>
    /// <returns>The pacing the fight is in now.</returns>
    public CombatPacing TogglePacing()
    {
        Pacing = Pacing == CombatPacing.TurnBased ? CombatPacing.RealTime : CombatPacing.TurnBased;
        if (Pacing == CombatPacing.TurnBased) Turns.Enter();
        else Turns.Leave();
        Touch();

        Report(
            "combat-pacing",
            Pacing == CombatPacing.TurnBased
                ? "The fight is now paced turn-based: its actors act one at a time in the order their own remaining recovery states, in rounds, and the session waits for each of the party's turns."
                : "The fight is now paced in real time: its actors act as their own recovery elapses and the world keeps stepping.");
        return Pacing;
    }

    /// <summary>
    /// Every actor the last <see cref="Step"/> found: the party's members first, in roster order, then the
    /// place's creatures in the order content declared them.
    /// </summary>
    /// <remarks>
    /// The order is stable and content's own rather than an ordering the fight invents, so a projection and a
    /// test read the same fight the same way twice. It is deliberately not an initiative order: which actor
    /// acts first is the pacing's business, and <see cref="Turns"/> states that order from the recovery each
    /// actor holds.
    /// </remarks>
    public IReadOnlyList<Combatant> Combatants => _combatants;

    /// <summary>
    /// The actors currently fighting the party, in combatant order.
    /// </summary>
    /// <remarks>
    /// An actor that is down is not fighting: it keeps its side and is still published, because a reader
    /// that could not see a body could not tell one from a place it never was, but nothing here counts it as
    /// an enemy still to be dealt with.
    /// </remarks>
    public IReadOnlyList<Combatant> Opposition =>
        [.. _combatants.Where(combatant => combatant.Side == CombatSide.Opposition && !IsDown(combatant))];

    /// <summary>Whether anything in the place is fighting the party right now.</summary>
    public bool IsEngaged => _combatants.Any(combatant => combatant.Side == CombatSide.Opposition && !IsDown(combatant));

    /// <summary>
    /// What a fight has left behind that a save taken now would drop, each named by what it is — <c>recovery</c>,
    /// <c>provoked</c>, <c>wounded</c>, <c>fallen</c>, or <c>round</c> — beside a phrase; empty when nothing.
    /// </summary>
    /// <remarks>
    /// A place full of creatures nobody has touched is not a fight a save loses: the population is rebuilt
    /// from the same placements on load, each creature with the recovery it starts with. What is lost is what
    /// the fight changed — a member still owing recovery, a creature the party provoked, a living creature it
    /// wounded, a body in a place the fight has not cleared, and a paced round in progress — so those are
    /// what this names.
    /// </remarks>
    public IReadOnlyList<(string Subject, string Phrase)> UnsavedFight()
    {
        List<(string Subject, string Phrase)> left = [];
        int recovering = _combatants.Count(combatant => combatant.Side == CombatSide.Party && !combatant.IsReady && !IsDown(combatant));
        if (recovering > 0) left.Add(("recovery", $"{recovering} member(s) still owing recovery"));
        int provoked = _combatants.Count(combatant => _provoked.Contains(combatant.Id) && !IsDown(combatant));
        if (provoked > 0) left.Add(("provoked", $"{provoked} living creature(s) the party provoked"));

        int wounded = 0;
        int fallen = 0;
        foreach (Combatant combatant in _combatants)
        {
            if (combatant.Side != CombatSide.Opposition || Health(combatant.Subject) is not { IsMortal: true } health) continue;
            if (health.IsDown) fallen++;
            else if (health.Current < health.Maximum) wounded++;
        }

        if (wounded > 0) left.Add(("wounded", $"{wounded} living creature(s) wounded"));
        if (fallen > 0 && IsEngaged) left.Add(("fallen", $"{fallen} creature(s) laid out in a place not yet cleared"));
        int spelled = _combatants.Count(combatant =>
            combatant.Subject.Entity is { } entity && !IsDown(combatant) && CreatureEffects.Find(entity.Actor) is { Any: true });
        if (spelled > 0) left.Add(("spelled", $"{spelled} creature(s) a spell is still acting on"));
        if (Pacing == CombatPacing.TurnBased && Turns.IsHolding) left.Add(("round", "a turn-based round in progress"));
        return left;
    }

    /// <summary>Where the party stands, which is what every actor's distance is measured from.</summary>
    /// <remarks>
    /// It is the world's own position, read here rather than copied: a driver that has to decide what a
    /// creature does about the party needs the same point the fight measured every distance from, and a
    /// second reading of it could disagree with the fight's by one step.
    /// </remarks>
    public PlacePose PartyPose => _world?.Pose ?? PlacePose.Origin;

    /// <summary>What the last accepted attack was, or null before anything has attacked.</summary>
    /// <remarks>
    /// This is what an attack <em>was</em>: who acted, how, against what, when, and what it cost. What came
    /// of it is <see cref="LastResolution"/>, which is a separate record because an attempt and its outcome
    /// are separate facts — a fight whose ruleset resolves nothing still says what was attempted.
    /// </remarks>
    public AttackInitiation? LastAttack => _lastAttack;

    /// <summary>
    /// What the last attack did, or null before anything has resolved one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is where an attack becomes an outcome: whether it landed, what it rolled, what the target's
    /// resistance took off it, what harm was left, what condition followed, and where the target now stands.
    /// It is the record the panel shows and diagnostics report, and it is read together with
    /// <see cref="LastAttack"/> — the initiation says what was attempted, this says what it came to.
    /// </para>
    /// <para>
    /// It is null for a fight whose ruleset answers no resolution, and for an attack at nothing: an actor
    /// with nothing in reach has still acted, and there is nothing there to resolve against.
    /// </para>
    /// </remarks>
    public CombatResolution? LastResolution => _lastResolution;

    /// <summary>
    /// What the last order to attack did, whether it applied or was refused, or null before any was given.
    /// </summary>
    /// <remarks>
    /// A refusal leaves no initiation, so the fight keeps the last answer as its own fact: an actor ignored
    /// while recovering must be readable as a refusal rather than as a fight in which nothing was asked.
    /// </remarks>
    public CombatResult? LastOrder => _lastOrder;

    /// <summary>The latest party member's attack answer, including refusals, or null before one was given.</summary>
    /// <remarks>
    /// Opposition turns may follow a member's order in the same admitted update. Keep that member's answer
    /// available to action feedback even when <see cref="LastOrder"/> already describes the retaliation.
    /// This is transient presentation history, not saved combat state.
    /// </remarks>
    public CombatResult? LastPartyOrder { get; private set; }

    /// <summary>
    /// The fight's most recent applied orders, oldest first, each with a serial that only grows: a typed record of
    /// completed blows that a presentation reads to show who struck, whom, and whether it landed, without re-deciding
    /// anything. Bounded to the last <see cref="RecentBlowLimit"/>; nothing saves it.
    /// </summary>
    public IReadOnlyList<CombatBlow> RecentBlows => _blows;

    /// <summary>How many applied orders <see cref="RecentBlows"/> keeps.</summary>
    public const int RecentBlowLimit = 32;

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
        Dictionary<CombatantId, CombatSide> beforeSides = _combatants.ToDictionary(actor => actor.Id, actor => actor.Side);
        List<Combatant> live = [];
        PlaceId place = _world?.Place ?? default;
        PlacePose partyPose = _world?.Pose ?? PlacePose.Origin;

        foreach (PartyMember member in _party.Members)
        {
            if (!member.IsAlive) continue;
            CombatSubject subject = new(CombatantId.Of(member.Id), place, partyPose, member, entity: null);
            string name = _rule.NameOf(subject);
            // What a member attacks with is its weapon's own answer where the game states one — a wand in
            // hand makes its attack a spell — and the kind alone otherwise.
            AttackKind kind = _weapons?.WeaponOf(subject)?.Kind ?? _rule.AttackKindFor(subject);
            // A member of the party begins ready: standing somewhere is not a reason for a character to be
            // unable to act, and a member enters turn-based pacing with its recovery as it stands, which is
            // how a party enters a turn-based fight with the recovery it already owed.
            Combatant combatant = Existing(subject.Id) ??
                new Combatant(subject, CombatSide.Party, name, kind, distance: 0, GameDuration.None);
            combatant.Observe(subject, CombatSide.Party, name, kind, distance: 0);
            live.Add(combatant);
        }

        if (_world is { } world)
        {
            foreach (PlacePopulationEntity entity in world.Population)
            {
                if (!entity.IsAlive) continue;

                // Where a creature stands is its own live position, which starts where its placement put it
                // and moves with every step a mover resolves, so every distance is measured from where it is.
                CombatantId id = CombatantId.Of(entity.Id);
                PlacePose pose = entity.Pose;
                CombatSubject subject = new(id, world.Place, pose, member: null, entity);
                if (_rule.NatureOf(subject) is not { IsCreature: true } nature) continue;

                // What made a creature stand with the party outranks what the party did to it; when it ends, the
                // provocation the fight remembers is what the creature is angry about again.
                double distance = world.Pose.DistanceTo(pose);
                CombatSide side = nature.IsAllied
                    ? CombatSide.Ally
                    : _provoked.Contains(subject.Id) || nature.Notices(distance)
                        ? CombatSide.Opposition
                        : CombatSide.Neutral;
                string name = _rule.NameOf(subject);
                AttackKind kind = _weapons?.WeaponOf(subject)?.Kind ?? _rule.AttackKindFor(subject);
                Combatant combatant = Existing(subject.Id) ??
                    new Combatant(subject, side, name, kind, distance, _rule.InitialRecovery(subject, kind));
                combatant.Observe(subject, side, name, kind, distance);
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
        bool provocationChanged = _provoked.RemoveWhere(id => !_byId.ContainsKey(id)) > 0;
        bool rosterChanged = beforeSides.Count != _byId.Count || beforeSides.Any(before =>
            !_byId.TryGetValue(before.Key, out Combatant? actor) || actor.Side != before.Value);
        if (rosterChanged || provocationChanged) Touch();

        // The turn-based pacing reads the fight after every re-read, so a round begins when a fight the party
        // can take turns in begins and lets go when it ends: what it holds is always this fight, never a
        // remembered copy of it.
        Turns.Reconcile();
        ReconcileSelection();
    }

    /// <summary>
    /// Whether an actor's own nature is to attack the party on sight, which is not the same question as the
    /// side it is on.
    /// </summary>
    /// <remarks>
    /// A side is what a fight has decided by now: a creature out of its notice range is neutral, and a person
    /// the party has attacked is an enemy even though nothing about them starts a fight. Whether something
    /// fights on sight is the ruleset's answer about what it is, which is what tells a place's opposition
    /// from the people standing in it — and it is why a place is cleared of what fought there rather than of
    /// everybody who happened to be indoors.
    /// </remarks>
    /// <param name="combatant">The actor to judge.</param>
    /// <returns>Whether it attacks the party on sight.</returns>
    /// <exception cref="ArgumentNullException">No combatant was supplied.</exception>
    public bool IsHostile(Combatant combatant)
    {
        ArgumentNullException.ThrowIfNull(combatant);
        return _rule.NatureOf(combatant.Subject).AttacksOnSight;
    }

    /// <summary>Orders only the selected member, through the same gate both pacings use.</summary>
    /// <returns>That member's answer, or no answer when nobody is capable of selection.</returns>
    public IReadOnlyList<CombatResult> Engage()
    {
        return EngageSelected() is { } result ? [result] : [];
    }

    /// <summary>One addressed actor attacks what it can reach through its existing recovery gate.</summary>
    /// <remarks>The party's selected order and a creature's AI both reach this same attack path.</remarks>
    /// <param name="actor">The actor whose order this is.</param>
    /// <returns>What the order did, or why it did not.</returns>
    public CombatResult Engage(CombatantId actor)
    {
        if (!_byId.TryGetValue(actor, out Combatant? combatant))
        {
            return Report(CombatResult.Refused(
                actor,
                actorName: null,
                new Refusal(CombatCodes.UnknownCombatant, $"No combatant '{actor}' is in this fight, so nothing acted; a fight holds the party's members and the creatures of the place the party stands in.")));
        }

        Combatant? target = Nearest(combatant);
        return Order(new AttackOrder(combatant.Id, combatant.PreferredKind, target?.Id));
    }

    /// <summary>
    /// How long an actor owes for one action of the kind it makes, which is the length both pacings are
    /// built on.
    /// </summary>
    /// <remarks>
    /// It is the ruleset's own answer, asked the same way every time: the same actor and the same kind are
    /// worth the same length of game time whether it is pricing what an attack costs in real time or how long
    /// a round lasts and whose turn is next in a paced one. Nothing here caches or adjusts it, so a ruleset
    /// whose answer changed between calls would be a defect this exposes rather than one it hides.
    /// </remarks>
    /// <param name="combatant">The actor to price.</param>
    /// <returns>How long it owes after one action of its own kind.</returns>
    /// <exception cref="ArgumentNullException">No combatant was supplied.</exception>
    public GameDuration RecoveryOf(Combatant combatant)
    {
        ArgumentNullException.ThrowIfNull(combatant);
        return _rule.RecoveryAfter(combatant.Subject, combatant.PreferredKind);
    }

    /// <summary>
    /// Charges an actor for the action it did not take, which is what passing a turn costs.
    /// </summary>
    /// <remarks>
    /// A passed turn is not a free one: the actor is charged its attack recovery and then the queue moves on,
    /// which is what keeps skipping
    /// a decision about this round rather than a way to act again sooner.
    /// </remarks>
    /// <param name="combatant">The actor whose turn was passed.</param>
    internal void ChargeTurn(Combatant combatant)
    {
        GameDuration before = combatant.Recovery;
        combatant.Spend(RecoveryOf(combatant));
        if (combatant.Recovery != before) Touch();
    }

    /// <summary>
    /// Applies one order to attack, resolves what it does, if the actor may act.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the fight's one entry for making an actor act, whoever asked: the player's control through
    /// <see cref="Engage"/>, and whoever drives the creatures on the other side. Two gates stand in front of
    /// it and both are the actor's own state: its recovery, which the one clock advances, and what its
    /// conditions leave it able to do, which the ruleset answers. A creature that is not ready, or one that
    /// is asleep or dead, cannot be driven to act by a later AI owner any more than a character can be driven
    /// to act by a key — one pacing, one gate, one rule about who may fight.
    /// </para>
    /// <para>
    /// An accepted attack is initiated and then resolved here, at the one point where
    /// <see cref="AttackInitiation"/> exists: the initiation says what was attempted and what it cost, the
    /// resolution says what it did. A fight whose ruleset answers no resolution produces initiations and no
    /// outcomes, which is a product that paces a fight without yet knowing what a blow is worth.
    /// </para>
    /// </remarks>
    /// <param name="order">Who acts, how, against what, and by which of its own ways of attacking.</param>
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
                new Refusal(CombatCodes.UnknownCombatant, $"No combatant '{order.Actor}' is in this fight, so nothing acted; a fight holds the party's members and the creatures of the place the party stands in.")));
        }

        // Movement is admitted before an order in the same session update. Re-read the subject here so the
        // condition gate, weapon answer, reach gate, and resolution all see the live member/entity pose while
        // retaining this combatant's identity and recovery.
        RefreshLive(actor);

        if (!actor.IsReady)
        {
            return Report(CombatResult.Refused(
                actor.Id,
                actor.Name,
                new Refusal(CombatCodes.Recovering, string.Create(
                    CultureInfo.InvariantCulture,
                    $"{actor.Name} is still recovering: {actor.Recovery.TotalSeconds:0.0}s before they can act again."))));
        }

        // What the actor's conditions leave it able to do is the ruleset's answer, asked before anything is
        // spent: an unconscious, sleeping, paralysed, petrified, dead, or eradicated actor does not act at
        // all, so it owes no recovery for an attack it never made.
        if (_resolution is { } conditions && !conditions.CanAct(actor.Subject))
        {
            return Report(CombatResult.Refused(
                actor.Id,
                actor.Name,
                new Refusal(CombatCodes.Incapacitated, $"{actor.Name} cannot act: {Describe(actor)} leaves them unable to fight, and nothing was spent on an attack they did not make.")));
        }

        Combatant? target = null;
        if (order.Target is { } targetId)
        {
            if (!_byId.TryGetValue(targetId, out target))
            {
                return Report(CombatResult.Refused(
                    actor.Id,
                    actor.Name,
                    new Refusal(CombatCodes.UnknownTarget, $"No combatant '{targetId}' is in this fight, so {actor.Name} attacked nothing.")));
            }

            RefreshLive(target);

            // A world body's health is final. Members instead retain the ruleset's below-empty
            // wound ladder, so incapability alone must not make them immune to further harm.
            if (target.Subject.Entity is not null && IsDown(target))
            {
                return Report(CombatResult.Refused(
                    actor.Id,
                    actor.Name,
                    new Refusal(CombatCodes.TargetDown, $"{target.Name} is already down, so {actor.Name} did not attack its body and nothing was spent.")));
            }

            if (OnPartysSide(target) && OnPartysSide(actor))
            {
                return Report(CombatResult.Refused(
                    actor.Id,
                    actor.Name,
                    new Refusal(CombatCodes.FriendlyTarget, $"{actor.Name} and {target.Name} are on the party's own side, so the order was refused rather than turned on the party.")));
            }
        }

        // What the actor's own weapon makes of this attack, asked here — at the one moment an attack is
        // initiated — and only when the order names no ability of its own: an order that names what it strikes
        // with is a spell the casting workflow resolved, and a wand in the caster's hand must not pay for it.
        // The order of answers is deliberate: what a hand holds decides how the attack is made, and a
        // charged item spends a charge of itself when it is fired.
        CombatWeapon? weapon = order.Ability is { Length: > 0 } ? null : _weapons?.WeaponOf(actor.Subject);
        string ability = order.Ability ?? weapon?.Ability ?? string.Empty;
        AttackKind kind = weapon?.Kind ?? order.Kind;
        if (target is not null && !IsInReach(actor, target, kind, out double distance, out double reach))
        {
            return Report(CombatResult.Refused(
                actor.Id,
                actor.Name,
                new Refusal(CombatCodes.TargetOutOfReach, string.Create(
                    CultureInfo.InvariantCulture,
                    $"{actor.Name} cannot reach {target.Name} with a {AttackKinds.WireName(kind)} at {distance:0.0} units; its reach is {reach:0.0}, so nothing was spent."))));
        }

        if (weapon is { SpendsACharge: true } charged)
        {
            if (target is null)
            {
                return Report(CombatResult.Refused(
                    actor.Id,
                    actor.Name,
                    new Refusal(CombatCodes.WeaponNoTarget, string.Create(
                        CultureInfo.InvariantCulture,
                        $"{actor.Name} has nothing to aim the charged item in hand at, so no charge was spent; a weapon that carries a spell is fired at a target."))));
            }

            ItemChargeSpend spend = _party.SpendItemCharge(charged.Charge!.Value, charged.Charges);
            if (!spend.Spent)
            {
                // The item answered a moment ago, and the answer the state gives now is the one that counts:
                // an emptied wand or one the party no longer holds leaves the attack unmade and the actor's
                // recovery unspent.
                return Report(CombatResult.Refused(actor.Id, actor.Name, spend.Refusal!));
            }
        }

        GameDuration recovery = _rule.RecoveryAfter(actor.Subject, kind);
        actor.Spend(recovery);
        bool durable = !recovery.IsNone;

        // Only a party attack is remembered as party provocation. Creatures also use this order gate
        // against one another; their victim and its kin do not become the party's enemies as a result.
        if (actor.Side == CombatSide.Party && target is not null && !OnPartysSide(target))
            durable |= ProvokeWithOthers(target);

        AttackInitiation initiation = new(
            actor.Id,
            actor.Name,
            kind,
            target?.Id,
            target?.Name ?? string.Empty,
            _clock?.Now,
            recovery,
            ability);
        _lastAttack = initiation;

        // Resolution happens at the moment of initiation and nowhere else: there is one place where an
        // attack becomes an outcome, so a melee swing, a shot, and a spell cannot drift into three paths
        // that damage a target differently.
        CombatResolution? resolution = Resolve(actor, target, kind, ability);
        _lastResolution = resolution;
        durable |= resolution is not null;
        if (durable) Touch();

        return Report(CombatResult.Applied(initiation, resolution));
    }

    /// <summary>
    /// What an actor has left to lose and what it can take altogether, read from wherever that is owned.
    /// </summary>
    /// <remarks>
    /// A member's pool is the party's own and is read live. A world actor's health is the creature's own,
    /// attached to its entity the first time a fight read it, so what the panel shows, what a rest reads
    /// about the creature near the camp, and what the fight acted on are one quantity rather than three
    /// opinions. A world actor nothing has given health to — one a fight never read — is measured against
    /// the ruleset's answer about what it can take, which is the same number it will be attached with.
    /// </remarks>
    /// <param name="combatant">The actor to measure.</param>
    /// <returns>What it has left, and what it can take altogether.</returns>
    /// <exception cref="ArgumentNullException">No combatant was supplied.</exception>
    public (int Current, int Maximum) Vitals(Combatant combatant)
    {
        ArgumentNullException.ThrowIfNull(combatant);
        if (combatant.Subject.Member is { } member)
        {
            return (member.Resources.HitPoints.Current, member.Resources.HitPoints.Maximum);
        }

        if (Health(combatant.Subject) is { } health) return (health.Current, health.Maximum);
        int maximum = _resolution?.HitPointsOf(combatant.Subject) ?? 0;
        return (maximum, maximum);
    }

    /// <summary>
    /// Whether an actor is out of the fight: laid out by what is acting on it, or taken down by harm.
    /// </summary>
    /// <remarks>
    /// A member is out when the ruleset says its conditions leave it unable to act, which is the same answer
    /// the order gate uses, so what a panel shows and what the fight enforces are one fact. A world actor is
    /// out when the harm the fight has done to it has reached what the ruleset says it can take. Neither is
    /// a removal from the fight's actors: an actor that is down still stands where it stood and is still
    /// published, because a reader that could not see it could not tell a body from a place it never was.
    /// </remarks>
    /// <param name="combatant">The actor to judge.</param>
    /// <returns>Whether it is out of the fight.</returns>
    /// <exception cref="ArgumentNullException">No combatant was supplied.</exception>
    public bool IsDown(Combatant combatant)
    {
        ArgumentNullException.ThrowIfNull(combatant);

        // A member is down when what is acting on them leaves them unable to act; a creature is down only when its
        // health is spent. A creature a spell holds still is still standing — it can be attacked, it still counts
        // as what a place holds, and it acts again when the spell lets go — so its gate is the order's, not this.
        if (combatant.Subject.Member is not null) return _resolution is { } rule && !rule.CanAct(combatant.Subject);
        return Health(combatant.Subject)?.IsDown ?? false;
    }

    /// <summary>
    /// Resolves one attack against its target, applying what it does and stating what came of it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The order here is the whole of the mechanism: ask for the attack's rolls under a key that names the
    /// attack, read what the ruleset says the attack is worth against this target, roll the hit, roll the
    /// damage, let the target's resistance take its share, ask what the hit left behind, and apply all of it
    /// to whoever owns the target's health. Every number comes from the ruleset; what the kit decides is when
    /// each question is asked and what is done with the answers.
    /// </para>
    /// <para>
    /// A fight whose ruleset cannot draw resolves nothing rather than resolving against an invented value,
    /// and an attack at nothing resolves nothing because there is nothing there to resolve against.
    /// </para>
    /// </remarks>
    private CombatResolution? Resolve(Combatant actor, Combatant? target, AttackKind kind, string? ability)
    {
        if (_resolution is not { } resolution || target is null) return null;

        // The key names the attack inside the fight, so two swings by one actor at one target are still two
        // different draws, and the same fight replayed draws the same values.
        string key = string.Create(
            CultureInfo.InvariantCulture,
            $"{actor.Subject.Place}/{actor.Id}/{target.Id}/{AttackKinds.WireName(kind)}/{_attacksResolved}");
        if (resolution.RollsFor(actor.Subject, key) is not { } rolls) return null;
        _attacksResolved++;

        // Which of the actor's own ways of attacking this is decides what the blow is worth, when the order
        // named one and the ruleset answers for them: a creature's second attack has its own dice and its own
        // kind of harm, and a spell its own. Nothing here reads the name; it is handed straight back.
        AttackPlan plan = ability is { Length: > 0 } named && _abilities is { } abilities
            ? abilities.PlanOfAbility(actor.Subject, target.Subject, kind, named)
            : resolution.PlanOf(actor.Subject, target.Subject, kind);
        int hitRoll = rolls.Roll("hit", 0, HitChance.Certain - 1);
        (int current, int maximum) = Vitals(target);
        if (!plan.Chance.Hits(hitRoll))
        {
            return CombatResolution.Missed(
                actor.Id,
                actor.Name,
                target.Id,
                target.Name,
                kind,
                plan.Chance,
                hitRoll,
                plan.Kind,
                current,
                maximum);
        }

        // What a defence turns aside is taken off what the dice rolled before the target's resistance has its
        // say, which is the order the plan states: the divisor is part of this blow against this target, and
        // the resistance is the target's own answer about whatever got through.
        int rolled = plan.Damage.Roll(rolls, "damage");
        int through = rolled / plan.Divisor;
        int damage = plan.Resistance.IsImmune
            ? 0
            : Math.Max(0, resolution.DamageAfterResistance(target.Subject, plan.Kind, through, rolls));
        List<CombatDamagePart> additional = [];
        foreach (AttackDamagePart part in plan.Additional ?? [])
        {
            int drawn = part.Damage.Roll(rolls, $"additional/{additional.Count}/{part.Kind}");
            int landed = Math.Max(0, resolution.DamageAfterResistance(target.Subject, part.Kind, drawn, rolls));
            additional.Add(new CombatDamagePart(part.Kind, drawn, landed));
            damage = checked(damage + landed);
        }
        CombatCondition? condition = resolution.ConditionOf(actor.Subject, target.Subject, plan.Kind, rolls);
        bool down = Wound(target, damage, condition);
        foreach (ICombatHitObserver observer in _hits) observer.Observe(new CombatHit(actor.Subject, target.Subject, kind, damage));
        (int reflected, bool actorDown) = Reflect(actor, target, plan.Kind, damage, rolls);
        (current, maximum) = Vitals(target);
        return CombatResolution.Landed(
            actor.Id,
            actor.Name,
            target.Id,
            target.Name,
            kind,
            plan.Chance,
            hitRoll,
            plan.Kind,
            rolled,
            plan.Resistance,
            damage,
            condition,
            down,
            current,
            maximum,
            plan.Divisor,
            reflected,
            actorDown,
            additional);
    }

    /// <summary>
    /// Turns what a landed wound sends back onto the actor that dealt it, through that actor's own health.
    /// </summary>
    /// <remarks>
    /// The ruleset answers how much comes back after the attacker's own defences; the fight lands it the same way
    /// a blow lands, so a reflection that empties a creature is a death its observers hear and one that lands on a
    /// member goes through the party's own damage entry. Nothing comes back from a wound that took nothing, and an
    /// actor cannot be turned against itself.
    /// </remarks>
    private (int Reflected, bool Down) Reflect(Combatant actor, Combatant target, DamageKindId kind, int damage, IAttackRolls rolls)
    {
        if (_reflection is not { } reflection || damage <= 0 || actor.Id == target.Id || IsDown(actor)) return (0, false);
        int reflected = Math.Max(0, reflection.ReflectedOnto(actor.Subject, target.Subject, kind, damage, rolls));
        if (reflected == 0) return (0, false);
        return (reflected, Wound(actor, reflected, condition: null));
    }

    /// <summary>
    /// Lands one hit on its target and reports whether this is what took it down.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A member takes harm through the party's own damage entry, which is where this game's answer about what
    /// a wound leaves is applied: the fight does not decide that a character is unconscious or dead, it
    /// states how much harm landed and the character's own health answers. The condition a landed hit inflicts
    /// besides harm — a bite's poison, a gaze's stone — is applied on top, through the member's own
    /// conditions.
    /// </para>
    /// <para>
    /// A world actor's harm lands on the creature's own health, which is the same handoff in the other
    /// direction: what a creature's own state holds is what the fight subtracts from, and a creature that
    /// has taken everything it can is down. A creature no health was ever attached for cannot be measured,
    /// so the harm is discarded rather than kept as a tally beside a health nobody gave it.
    /// </para>
    /// </remarks>
    private bool Wound(Combatant target, int damage, CombatCondition? condition)
    {
        if (target.Subject.Member is { } member)
        {
            CharacterWound wound = member.TakeDamage(damage);
            bool laid = wound.Fell;
            if (condition is { } left)
            {
                member.Conditions.Apply(new ActiveCondition(left.Condition, left.Severity));
                laid = laid || (_resolution is { } rule && !rule.CanAct(target.Subject));
            }

            return laid;
        }

        if (damage <= 0 || Health(target.Subject) is not { } health || !health.Wound(damage)) return false;

        // This blow is what took the creature down, which is the one moment it dies: every observer the game
        // named hears it now, once, and nothing reads the death back out of the place later.
        if (target.Subject.Entity is { } entity)
        {
            PlacePose fell = entity.Pose;
            CreatureDeath death = new(target.Subject.Place, entity.Placement with { Pose = fell }, target.Name);
            foreach (ICreatureDeathObserver observer in _deaths) observer.Died(death);
        }

        return true;
    }

    /// <summary>The creature's own health, when the actor is a world entity that has any.</summary>
    private static CreatureHealth? Health(CombatSubject subject) =>
        subject.Entity is { } entity ? CreatureHealth.Find(entity.Actor) : null;

    /// <summary>What is acting on an actor, in the words the kit already carries, for a refusal to name.</summary>
    private static string Describe(Combatant actor) => actor.Subject.Member is { } member && member.Conditions.Count > 0
        ? string.Join(", ", member.Conditions.Active.Select(condition => condition.ToString()))
        : "what is acting on them";

    /// <summary>
    /// Remembers that the party has acted against a creature, which puts it into the fight as an attack does.
    /// </summary>
    /// <remarks>
    /// An attack provokes its target where it is ordered; a spell that does no harm — a paralysis, a slowing — is
    /// still an act against the creature, and the game that applied it says so here, so the creature stays an enemy
    /// for as long as it stands there whatever pacing is in force.
    /// </remarks>
    /// <param name="target">The creature acted against.</param>
    /// <returns>Whether a creature in this fight has that identity.</returns>
    public bool Provoke(CombatantId target)
    {
        if (!_byId.TryGetValue(target, out Combatant? combatant) || combatant.Side == CombatSide.Party) return false;
        if (ProvokeWithOthers(combatant)) Touch();
        return true;
    }

    /// <summary>
    /// Remembers that the party acted against a creature, and against every other standing actor the game says that
    /// act turns against it too.
    /// </summary>
    /// <remarks>
    /// An actor already down, one of the party, or one standing with the party is not asked: a body takes no part,
    /// and what made a creature stand with the party outranks what the party did, so it is not turned by this either.
    /// </remarks>
    /// <param name="target">The creature acted against.</param>
    private bool ProvokeWithOthers(Combatant target)
    {
        bool changed = _provoked.Add(target.Id) || target.Side != CombatSide.Opposition;
        target.Provoke();
        if (_provocation is null) return changed;

        foreach (Combatant other in _combatants)
        {
            if (other.Id == target.Id || OnPartysSide(other) || IsDown(other)) continue;
            RefreshLive(other);
            if (!_provocation.ProvokedWith(target.Subject, other.Subject)) continue;
            changed |= _provoked.Add(other.Id) || other.Side != CombatSide.Opposition;
            other.Provoke();
        }
        return changed;
    }

    /// <summary>Adds game time to what an actor must recover before it may act again.</summary>
    /// <remarks>
    /// This is the one way something other than an actor's own action costs it time — a blow that staggers it — and
    /// it moves the same pacing quantity an action charges, so both pacings read it the same way.
    /// </remarks>
    /// <param name="actor">The actor held back.</param>
    /// <param name="by">How much game time it owes beyond what it already did.</param>
    /// <returns>Whether an actor in this fight has that identity.</returns>
    public bool Delay(CombatantId actor, GameDuration by)
    {
        if (!_byId.TryGetValue(actor, out Combatant? combatant)) return false;
        if (by.IsNone) return true;
        combatant.Spend(GameDuration.FromMilliseconds(checked(combatant.Recovery.Milliseconds + by.Milliseconds)));
        Touch();
        return true;
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
        foreach (Combatant combatant in _combatants)
        {
            combatant.Recover(advance.Elapsed);

            // What a spell left on a creature runs out with the same game time its recovery does.
            if (combatant.Subject.Entity is { } entity) CreatureEffects.Find(entity.Actor)?.Elapse(advance.Elapsed);
        }
    }

    /// <summary>The combatant already in this fight under an identity, or null when none is.</summary>
    private Combatant? Existing(CombatantId id) => _byId.GetValueOrDefault(id);

    /// <summary>Whether an actor is one of the party or a creature standing with it.</summary>
    private static bool OnPartysSide(Combatant combatant) => combatant.Side is CombatSide.Party or CombatSide.Ally;

    /// <summary>What an order from this actor would strike now: the nearest standing opponent within its reach.</summary>
    /// <param name="actor">The actor whose order is asked about.</param>
    /// <returns>The target, or null when the actor is not in this fight or nothing stands within its reach.</returns>
    public Combatant? AimOf(CombatantId actor) => _byId.TryGetValue(actor, out Combatant? combatant) ? Nearest(combatant) : null;

    /// <summary>
    /// Reads an actor's current party-relative distance from the live world and refreshes its subject view.
    /// </summary>
    /// <remarks>
    /// The distance a projection last saw is only a convenience value: a creature may have moved after the
    /// fight's last population read, and the party may have moved before an order or cast in this update. This
    /// reader goes to the party pose and the entity's own standing pose, retaining the same combatant and
    /// underlying member/entity references.
    /// </remarks>
    /// <param name="combatant">The actor whose live distance is needed.</param>
    /// <returns>The current distance in the place's units.</returns>
    /// <exception cref="ArgumentNullException">No combatant was supplied.</exception>
    public double DistanceOf(Combatant combatant)
    {
        ArgumentNullException.ThrowIfNull(combatant);
        RefreshLive(combatant);
        return combatant.Distance;
    }

    /// <summary>Whether a named combatant is within the stated actor's reach for one attack kind.</summary>
    /// <remarks>
    /// The distance is read from both actors' current poses, rather than from the party-relative distance a
    /// projection publishes for convenience. The boundary is open: a target exactly at a ruleset's reach is
    /// refused, matching attack resolution's own distance limit. This asks only about attack reach; it does
    /// not add line of sight, notice, or interaction acquisition to the fight's gate.
    /// </remarks>
    /// <param name="actor">The actor that would make the attack.</param>
    /// <param name="target">The combatant the attack names.</param>
    /// <param name="kind">The attack kind whose reach is being checked.</param>
    /// <returns>True when both combatants exist and the target is inside the open reach boundary.</returns>
    public bool IsInReach(CombatantId actor, CombatantId target, AttackKind kind) =>
        _byId.TryGetValue(actor, out Combatant? attacker) &&
        _byId.TryGetValue(target, out Combatant? defender) &&
        IsInReach(attacker, defender, kind, out _, out _);

    /// <summary>Whether an order was given by one of the party's own members rather than by a creature.</summary>
    /// <remarks>
    /// It is read from who gave the order rather than from who stands in the fight now, so a member who has since died
    /// and left the fight's actors is still the author of what they did.
    /// </remarks>
    /// <param name="order">The order's result.</param>
    /// <returns>Whether its actor is one of the party's members.</returns>
    public bool IsPartys(CombatResult order) =>
        order is not null && _party.Members.Any(member => CombatantId.Of(member.Id) == order.Actor);

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
        RefreshLive(actor);
        double reach = _rule.ReachOf(actor.Subject, actor.PreferredKind);
        Combatant? nearest = null;
        double nearestDistance = double.MaxValue;
        foreach (Combatant candidate in _combatants)
        {
            if (OnPartysSide(candidate)) continue;

            double distance = DistanceOf(candidate);
            if (distance >= reach) continue;

            // A body is not a target: an actor this fight has taken down is left where it fell, so the
            // party's next order is spent on what is still standing rather than on what it has already
            // finished.
            if (IsDown(candidate)) continue;
            if (nearest is null || distance < nearestDistance)
            {
                nearest = candidate;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    /// <summary>Reads one named pair's attack distance and the ruleset's reach answer.</summary>
    private bool IsInReach(Combatant actor, Combatant target, AttackKind kind, out double distance, out double reach)
    {
        RefreshLive(actor);
        RefreshLive(target);
        distance = actor.Subject.Pose.DistanceTo(target.Subject.Pose);
        reach = _rule.ReachOf(actor.Subject, kind);
        return distance < reach;
    }

    /// <summary>Refreshes one existing combatant from the live party or population owner.</summary>
    private void RefreshLive(Combatant combatant)
    {
        CombatSubject previous = combatant.Subject;
        PlacePose pose = previous.Member is not null
            ? _world?.Pose ?? previous.Pose
            : previous.Entity?.Pose ?? previous.Pose;
        PlaceId place = _world?.Place ?? previous.Place;
        PlacePose partyPose = _world?.Pose ?? PlacePose.Origin;
        CombatSubject current = new(previous.Id, place, pose, previous.Member, previous.Entity);
        combatant.RefreshSubject(current, previous.Member is not null ? 0 : partyPose.DistanceTo(pose));
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
        if (IsPartys(result)) LastPartyOrder = result;
        if (result.IsApplied && result.Initiated is not null)
        {
            _blows.Add(new CombatBlow(++_blowSerial, result));
            if (_blows.Count > RecentBlowLimit) _blows.RemoveAt(0);
        }

        ReconcileSelection();
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

    /// <summary>Reports something the fight itself changed, which no order carries.</summary>
    /// <remarks>
    /// Changing the pacing is a fact about the fight rather than about an actor, so it cannot travel as a
    /// <see cref="CombatResult"/>; it is reported all the same, because a mode nobody can see in the
    /// diagnostics looks exactly like a toggle that never arrived. The place and the pose travel with it for
    /// the same reason every order's report carries them.
    /// </remarks>
    private void Report(string code, string message)
    {
        _diagnostics?.Publish(new DiagnosticsPublishRequest(
            DiagnosticsSeverity.Info,
            DiagnosticsDisposition.Accepted,
            Source: "combat",
            Code: code,
            Message: _world is { } world ? $"In place '{world.Place}' at {world.Pose}: {message}" : message,
            Correlation: string.Empty));
    }
}
