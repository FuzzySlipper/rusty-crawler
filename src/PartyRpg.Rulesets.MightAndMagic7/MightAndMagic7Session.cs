using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Input;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Progression;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using Rusty.Engine;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// This ruleset's session: it composes the kit's session shell with this game's identity, this game's one
/// clock, and the party its content describes or its creation flow builds.
/// </summary>
/// <remarks>
/// <para>
/// Composition happens in one order because the pieces depend on each other in exactly that order: the
/// clock is this game's policy and comes first, the party comes from content or from creation, the ledger is
/// the one path into the party's accounts, and the world is composed over the clock and the ledger so a
/// journey can charge both. The session then holds the party and the clock, publishes their facts, and
/// releases them with itself.
/// </para>
/// <para>
/// <b>A new session is created when the host declares a creation screen; otherwise it plays what its
/// scenario fixes.</b> This game's creation screen is the player-facing path a new game takes, so a host that
/// declared its controls gets a session holding this game's creation flow, starting in creation: its party
/// and the world that party walks into are composed when the player accepts them, which is what makes the
/// ledger the world charges the created party's own accounts. A host that declared none offers no creation
/// screen, and its session plays the party its content fixes — the scripted path a live check, a test, or a
/// product without creation takes, composed through the same factory creation ends at. A session resuming
/// from a save already holds both and creates nothing.
/// </para>
/// <para>
/// Anything this composition creates and then fails to hand over is released here, so a session that
/// cannot be composed leaves no party, no engine scene, and no store behind it.
/// </para>
/// </remarks>
internal sealed class MightAndMagic7Session : IGameSession
{
    private readonly PartyRpgSession _session;

    internal MightAndMagic7Session(IGameRuleset ruleset, RulesetSessionContext context, SessionSave? resume = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        GameClock clock = MightAndMagic7Time.Compose();

        // The clock takes the recorded game time before the world is composed, because the world's places
        // are read against the day the session stands on: a resumed session that restored its ledger and
        // then moved its clock would spend its first update crossing a boundary it had already crossed.
        resume?.Clock.ApplyTo(clock);

        // This game's skills are read once, here, and the same reading is handed to the service mechanism,
        // which needs the ceilings and the fees to offer a mastery lesson, to the progression owner, which
        // judges a raise against them, and to the equipment gate, which resolves what an item's row names.
        // One reading of one table is what keeps a lesson, a raise, and an equip in step.
        MightAndMagic7Skills? skills = MightAndMagic7Skills.Read(Declared(context.Content));

        // This game's magic is read once, here, beside its skills and before its services: a guild's spell
        // books are lessons whose requirements are this game's learning rule, a creature's spell lands with
        // the spell's own numbers, and the session's casting workflow judges mastery and price against the
        // same reading. One reading of one table is what keeps a book, a cast, and a creature's spell in step.
        MightAndMagic7Spells? spells = MightAndMagic7Spells.Read(Declared(context.Content), skills);

        // The party and the world the effects act on do not exist yet on the path that creates its party, so
        // the effect path is handed providers rather than the state itself: it reads them when a cast actually
        // arrives, which is always after the composition that created them. A product with no world gives it
        // nothing to travel through, and a spell that travels says so rather than moving anybody.
        PartyEntity? party = null;
        SessionWorld? world = null;
        MightAndMagic7SpellEffects? spellEffects = spells is null ? null : new MightAndMagic7SpellEffects(spells, clock, () => world);

        // This game's services are read once, here, and the same answers are handed to the world — which
        // needs them to describe a counter the party talks to — and to the session, which serves it. One
        // reading of one placement is what keeps what a use offers and what a transaction does in step.
        MightAndMagic7Services? services = MightAndMagic7Services.Read(Declared(context.Content), skills, spells);

        // This game's answers about people are read once here, for the same reason: the world needs them to
        // say who stands at a placement the party faces, and the session needs the one instance to speak
        // with them, so what a use reaches and who answers can never be two readings of one placement.
        MightAndMagic7Conversation? conversation = MightAndMagic7Conversation.Read(Declared(context.Content), services);
        if (conversation is not null)
        {
            // What reading the people tables noticed is reported where the other composition notes are: a
            // person row nothing can be placed for, a topic without an answer, and the counts themselves are
            // facts about the operator's data rather than defects, so they are published and the session
            // starts.
            foreach (string note in conversation.Notes)
            {
                context.Engine?.Diagnostics?.Publish(new DiagnosticsPublishRequest(
                    DiagnosticsSeverity.Info,
                    DiagnosticsDisposition.Accepted,
                    Source: "people",
                    Code: "people-note",
                    Message: note,
                    Correlation: string.Empty));
            }
        }

        // This game's answers about stopping are read once, here, beside its service answers: what a night
        // costs, where it may be taken, what breaks it, and what going without sleep does. The engine's
        // random service travels with them, because a camp's risk is a keyed draw of the engine's own
        // randomness rather than a generator this product keeps. A session that holds no engine takes no
        // risk, and a camp where something could find the party is then refused by name.
        MightAndMagic7Rest rest = MightAndMagic7Rest.Compose(Declared(context.Content), context.Engine?.Random);

        // This game's combat policy is read once, here, beside its rest and service answers: what each monster
        // row is worth in recovery, what its hostility band notices, what an attack reaches, and what every
        // actor is called. It is composed whether or not the host declared an act control, because the fight
        // is what reads the world — what is hostile, who is ready — and the control is only how a player
        // gives an order.
        // This game's loot is read once, here, and the same reading answers both halves of a session: what a
        // death left, which the fight's report generates, and what a container's random reference resolves
        // to, which the world's interaction answers read. One loot owner means one table read and one seed.
        MightAndMagic7Loot loot = MightAndMagic7Loot.Compose(Declared(context.Content), context.Engine?.Random);

        // What the party brings down is kept in one place, and both halves hold it: the fight reports the
        // creatures it read as down, and the world's interaction answers describe what is lying there. It is
        // composed here because the ruleset is the one point both halves are composed over.
        CorpseGround corpses = new();
        MightAndMagic7Corpses corpseAnswers = new(corpses, loot);

        // What a death pays the party is composed beside them: the award is the kit's one path, the worth is
        // the monster row's own experience column, and the owner is the one the session composes over
        // whichever party it ends up playing. The fight is what reads the row, so it is reached through the
        // local below — a death cannot be reported before the fight that reports it exists, which is what
        // makes reading it here honest rather than a second reading of the table.
        // The fight reads what spells have left on the party — a ward where a resistance is asked, a haste
        // where recovery is charged — so it is handed the party the same way: as a provider, read at the
        // moment the quantity is wanted rather than captured when the policy was composed.
        MightAndMagic7Combat? composed = null;
        ProgressionAwards awards = new(Worth, () => Progression, corpseAnswers);
        composed = MightAndMagic7Combat.Compose(Declared(context.Content), context.Engine?.Random, awards, spells, () => party);
        MightAndMagic7Combat combat = composed;

        long Worth(PlacementDefinition placement) =>
            composed is { } fight
                ? fight.ExperienceOf(placement)
                : throw new InvalidOperationException(
                    "A death was reported before this session's fight was composed, so what it was worth could not be read.");

        // This game's answers about how a monster behaves are read once here, beside them: who hates whom is
        // the shipped hostility matrix as content, and what a creature does with its moment is its own row's
        // AI class, speed, attacks, and spells. The driver that asks these questions is the kit's, so the
        // other side of every fight is decided by this game's data rather than by a class per monster.
        MightAndMagic7MonsterAi monsterAi = MightAndMagic7MonsterAi.Compose(Declared(context.Content), combat, context.Engine?.Random);

        EngineSessionSaveStore? store = MightAndMagic7Persistence.Store(context.Engine);
        try
        {
            SessionComposition composition = SessionComposition.From(ruleset) with
            {
                Bundle = context.Selection.BundleId,
                ContentPacks = context.Selection.PackCount,
            };
            MovementInput? movement = Movement(context);
            InteractionUseInput? use = Use(context);
            if (resume is { } save)
            {
                party = Capacity(MightAndMagic7Party.Restore(save.Party, Declared(context.Content)), spells)!;
                PartyResourceLedger ledger = Ledger(party);
                world = MightAndMagic7World.Compose(context.Content, context, clock, ledger, party, save, services, conversation, corpseAnswers, loot);
                _session = new PartyRpgSession(
                    composition,
                    context.Projection,
                    world,
                    movement,
                    clock,
                    party,
                    context.Engine?.Diagnostics,
                    store,
                    MightAndMagic7Persistence.SaveSlot,
                    saveInput: context.Save,
                    resumed: true,
                    useInput: use,
                    service: services,
                    accounts: ledger,
                    serviceInput: context.Service,
                    rest: rest,
                    restInput: context.Rest,
                    conversation: conversation,
                    conversationInput: context.Conversation,
                    combat: combat,
                    combatInput: context.Combat,
                    monsterAi: monsterAi,
                    progression: MightAndMagic7Progression.Instance,
                    skills: skills,
                    skillInput: context.Skills,
                    spells: spells,
                    spellEffects: spellEffects,
                    castInput: context.Cast);
                return;
            }

            if (Creation(context) is { } creation)
            {
                // The host declared a creation screen, so a new session creates its party. The flow is this
                // game's, the factory is the one every party comes from, and the world is composed with the
                // created party so the provisions a road costs come out of the larder the player's own
                // characters filled.
                ContentCatalog? declared = Declared(context.Content);
                _session = new PartyRpgSession(
                    composition,
                    context.Projection,
                    movementInput: movement,
                    clock: clock,
                    diagnostics: context.Engine?.Diagnostics,
                    saveStore: store,
                    saveSlot: MightAndMagic7Persistence.SaveSlot,
                    creationInput: creation,
                    creation: new SessionCreation(
                        MightAndMagic7Creation.Start(declared),
                        description => Capacity(MightAndMagic7Party.Factory(declared).Create(description), spells, fill: true)!,
                        created => MightAndMagic7World.Compose(declared, context, clock, Ledger(created), created, services: services, conversation: conversation, corpses: corpseAnswers, loot: loot)),
                    saveInput: context.Save,
                    useInput: use,
                    service: services,
                    serviceInput: context.Service,
                    rest: rest,
                    restInput: context.Rest,
                    conversation: conversation,
                    conversationInput: context.Conversation,
                    combat: combat,
                    combatInput: context.Combat,
                    monsterAi: monsterAi,
                    progression: MightAndMagic7Progression.Instance,
                    skills: skills,
                    skillInput: context.Skills,
                    spells: spells,
                    spellEffects: spellEffects,
                    castInput: context.Cast);
                return;
            }

            // No creation screen was declared, so this session plays the party its scenario fixes: the
            // scripted path — a live check, a test, or a product that offers no creation. Handing that party
            // to the world here is the same composition order the created path takes, one accept earlier.
            party = Capacity(MightAndMagic7Party.Compose(context.Content), spells, fill: true);
            PartyResourceLedger? accounts = party is null ? null : Ledger(party);
            world = MightAndMagic7World.Compose(context.Content, context, clock, accounts, party, services: services, conversation: conversation, corpses: corpseAnswers, loot: loot);
            _session = new PartyRpgSession(
                composition,
                context.Projection,
                world,
                movement,
                clock,
                party,
                context.Engine?.Diagnostics,
                store,
                MightAndMagic7Persistence.SaveSlot,
                saveInput: context.Save,
                useInput: use,
                service: services,
                accounts: accounts,
                serviceInput: context.Service,
                rest: rest,
                restInput: context.Rest,
                conversation: conversation,
                conversationInput: context.Conversation,
                combat: combat,
                combatInput: context.Combat,
                monsterAi: monsterAi,
                progression: MightAndMagic7Progression.Instance,
                skills: skills,
                skillInput: context.Skills,
                spells: spells,
                spellEffects: spellEffects,
                castInput: context.Cast);
        }
        catch
        {
            // The world owns the engine's spatial session and the population's entities, and the party owns
            // the store it was created in: a composition that failed after building either must release it
            // rather than leaving a party nobody plays and a scene nobody walks.
            world?.Dispose();
            party?.Dispose();
            store?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Gives a party the spell points its class, level, and scores add up to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the one moment a member's capacity is stated, and it is stated from the ruleset's own formula
    /// rather than from a number content happens to carry: creation seeds each class's base and every level
    /// adds the class's own per-level grant, so a party that exists holds exactly what
    /// <see cref="MightAndMagic7Spells.SpellPointCapacity"/> says it holds — a level-one character just
    /// created, one raised by a training hall, and one restored from a save are one arithmetic. Attributes do
    /// not grow in this build yet, so nothing re-states the capacity afterwards; the stone that grows them
    /// applies this same answer where it moves them.
    /// </para>
    /// <para>
    /// A party that has just come into being holds all of it, exactly as creation leaves its pools full; a
    /// party restored from a save keeps what it had left, because what a save records is what a member had
    /// spent and re-filling it would hand back points the player had used.
    /// </para>
    /// </remarks>
    /// <param name="party">The party that came into being, or null when content declares none.</param>
    /// <param name="spells">This game's magic, or null when there is no content to read it over.</param>
    /// <param name="fill">Whether this party is new, so its pools are filled rather than left as recorded.</param>
    /// <returns>The same party.</returns>
    private static PartyEntity? Capacity(PartyEntity? party, MightAndMagic7Spells? spells, bool fill = false)
    {
        if (party is null || spells is null) return party;
        foreach (PartyMember member in party.Members)
        {
            int capacity = spells.SpellPointCapacity(member);
            member.Resources.SetMaximumSpellPoints(capacity);
            if (fill) member.Resources.RestoreSpellPoints(capacity);
        }

        return party;
    }

    /// <summary>
    /// The party's own accounts as the one settlement path, so a journey charges what the party carries.
    /// </summary>
    /// <remarks>
    /// The larder's policy is this game's rule over the party it feeds: what a day costs and what hunger does
    /// are answered here, and the party is what the rule weakens and feeds again. The ledger is composed over
    /// whichever party the session holds — a restored one or the one creation just built — so both paths
    /// charge the same accounts through the same rule.
    /// </remarks>
    private static PartyResourceLedger Ledger(PartyEntity party) =>
        new(party, provisioning: new MightAndMagic7Provisions(party));

    /// <summary>
    /// The reader for the movement controls the host declared, when it declared any.
    /// </summary>
    /// <remarks>
    /// The host owns the intent names and the ruleset owns how fast a held turn control turns the party,
    /// which is why the reader is composed here from both: a product that declares no movement controls
    /// gets no reader, and the session then never asks the world to move anything.
    /// </remarks>
    private static MovementInput? Movement(RulesetSessionContext context) =>
        context.Movement is { } controls
            ? new MovementInput(controls, MightAndMagic7Movement.TurnRatePerSecond)
            : null;

    /// <summary>
    /// The reader for the use controls the host declared, when it declared any.
    /// </summary>
    /// <remarks>
    /// A host that declares no use control gets a session that never uses anything by itself, exactly as it
    /// gets no creation screen and no save key: the mechanism is still composed and still publishes what the
    /// party faces, and only the player's way of asking for a use is missing.
    /// </remarks>
    private static InteractionUseInput? Use(RulesetSessionContext context) =>
        context.Use is { } controls ? new InteractionUseInput(controls) : null;

    /// <summary>
    /// The reader for the creation controls the host declared, when it declared any.
    /// </summary>
    /// <remarks>
    /// A host that declares no creation controls offers no creation screen, and a session without one plays
    /// the party its scenario fixes instead — so the reader is also the declaration that this product creates
    /// its parties. The flow and the reader arrive together: a session creating a party with no way to choose
    /// anything is refused where it is composed rather than composed as a screen nobody can drive.
    /// </remarks>
    private static CreationInput? Creation(RulesetSessionContext context) =>
        context.Creation is { } controls ? new CreationInput(controls) : null;

    /// <summary>
    /// The content creation is offered over: the packs that loaded, or nothing when none did.
    /// </summary>
    /// <remarks>
    /// An empty catalog is the absence of content rather than content that contradicts creation. The
    /// product's shipped bundle names no packs until the operator generates them, and creation's own
    /// contract is to be composed without content then — from the compiled tables, because a game that
    /// cannot create a party is not a game. The world and the scenario's party already read an empty catalog
    /// as nothing; this is where creation is told the same thing instead of being refused by a catalog that
    /// declares no classes because it declares nothing at all.
    /// </remarks>
    private static ContentCatalog? Declared(ContentCatalog? content) =>
        content is { Packs.Count: > 0 } ? content : null;

    /// <inheritdoc />
    public SessionMode Mode => _session.Mode;

    /// <inheritdoc />
    public void Start() => _session.Start();

    /// <inheritdoc />
    public void Pause() => _session.Pause();

    /// <inheritdoc />
    public void Resume() => _session.Resume();

    /// <inheritdoc />
    public void Hold() => _session.Hold();

    /// <inheritdoc />
    public void ReleaseHold() => _session.ReleaseHold();

    /// <summary>
    /// The fight this session plays, or null when it plays none.
    /// </summary>
    /// <remarks>
    /// The session the product composes owns the fight, and this is that same state read one layer out
    /// rather than a second one: the wrapper exists to compose this game's world, clock, services, and people
    /// and to forward the lifecycle, and what the session it composed holds is part of what it holds. A
    /// creature's order is given through here, which is the one gated entry the player's control uses as well
    /// — the AI that will give one on its own is the monsters-and-AI stone's, and until it exists nothing else
    /// drives the other side of a fight.
    /// </remarks>
    internal CombatState? Combat => _session.Combat;

    /// <summary>The party this session plays, or null when it holds none yet.</summary>
    internal PartyEntity? Party => _session.Party;

    /// <summary>
    /// The progression owner this session's party grows through, or null until the party exists.
    /// </summary>
    /// <remarks>
    /// The session the product composes owns it, and this is that same owner read one layer out rather than
    /// a second one: the kill award asks for it the moment a fight reports a death, which is after the party
    /// the owner was composed over exists.
    /// </remarks>
    internal PartyProgression? Progression => _session.Progression;

    /// <summary>
    /// The world this session stands in, or null when it holds none.
    /// </summary>
    /// <remarks>
    /// The session the product composes owns the world, and this is that same world read one layer out
    /// rather than a second one: what a live check or a suite reads about a place — its state, its
    /// population, where the party stands — is what the session it plays is standing in.
    /// </remarks>
    internal SessionWorld? World => _session.LiveWorld;

    /// <inheritdoc />
    public void PublishInitial() => _session.PublishInitial();

    /// <inheritdoc />
    public ProductUpdateResult Update(ProductUpdate update) => _session.Update(update);

    /// <inheritdoc />
    public void Dispose() => _session.Dispose();

    /// <summary>
    /// Writes the live session at this game's explicit save boundary, and returns the document written.
    /// </summary>
    /// <remarks>
    /// The session owns where a save goes: the player's request to save reaches this call, and no update,
    /// mode change, or shutdown reaches it by itself.
    /// </remarks>
    /// <returns>The document that was written.</returns>
    /// <exception cref="InvalidOperationException">The session was composed without a save store.</exception>
    /// <exception cref="SessionSaveException">The session holds nothing a load could rebuild, or the write failed.</exception>
    internal SessionSave Save() => _session.Save();
}
