using PartyRpg.Kit;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Input;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Journal;
using PartyRpg.Kit.Knowledge;
using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Progression;
using PartyRpg.Kit.Quests;
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

        // The owners the session will compose exist before anything else, empty: this game's answers that need
        // one of them — a death counted toward an errand, a find written in the journal, a spell that reads the
        // world — are handed these owners and read them when an act arrives, which is always after the session
        // composed them. Nothing here reaches into a session that is still being built.
        SessionOwners owners = new(clock, context.Engine?.Diagnostics);

        // This game's skills are read once, here, and the same reading is handed to the service mechanism,
        // which needs the ceilings and the fees to offer a mastery lesson, to the progression owner, which
        // judges a raise against them, and to the equipment gate, which resolves what an item's row names.
        // One reading of one table is what keeps a lesson, a raise, and an equip in step.
        // This game's ranks are read first, before its skills, because the ceilings answer with them: a class
        // that took one alternative of a second promotion closes the other's school, and the sentence that
        // refuses a caster says which choice did it. One reading of one ladder is what keeps a ceiling, a
        // lesson, a book, and a casting in step about which schools a character may hold.
        MightAndMagic7Promotions promotions = MightAndMagic7Promotions.Read(Declared(context.Content));
        MightAndMagic7Skills? skills = MightAndMagic7Skills.Read(Declared(context.Content), promotions);

        // This game's magic is read once, here, beside its skills and before its services: a guild's spell
        // books are lessons whose requirements are this game's learning rule, a creature's spell lands with
        // the spell's own numbers, and the session's casting workflow judges mastery and price against the
        // same reading. One reading of one table is what keeps a book, a cast, and a creature's spell in step.
        // This game's alchemy is read once, here, beside its skills and before its magic: the mixtures its own
        // potion table states, the rung each result asks for, and what a mixture that goes off costs. The
        // reading is handed to the magic below, because a potion's effect and a potion's mixture are two
        // readings of one table and a second one would be free to disagree.
        //
        // Whether a character may act at all is this game's one answer about its own conditions — the same
        // answer the fight's own gate reads — so a character a blow laid out can neither swing nor mix, and
        // the two can never disagree about which condition that is.
        MightAndMagic7Alchemy? alchemy = MightAndMagic7Alchemy.Read(
            Declared(context.Content),
            context.Engine?.Random,
            MightAndMagic7Conditions.CanAct);

        MightAndMagic7Spells? spells = MightAndMagic7Spells.Read(Declared(context.Content), skills, alchemy);


        // The party and the world the effects act on do not exist yet on the path that creates its party, so
        // the effect path reads the owners' own when a cast actually arrives. A product with no world gives it
        // nothing to travel through, and a spell that travels says so rather than moving anybody.
        PartyEntity? party = null;
        SessionWorld? world = null;
        // What a spell leaves on a creature is judged by that creature's own row — what it is immune to, whether it
        // is undead — which the fight policy composed below reads, so the effect path is handed it as a provider.
        MightAndMagic7Combat? composed = null;
        MightAndMagic7SpellEffects? spellEffects = spells is null ? null : new MightAndMagic7SpellEffects(spells, clock, () => owners.World, () => composed);
        // This game's automap is read beside them: how far a walking party sees, what each place's own map
        // squares and features are drawn as, the zoom ladder, and what a detection reveals over it. Both halves
        // are read from the content the product loaded — the maps themselves come from the placed-map document
        // the importer writes — so a product composed over content that carries no maps keeps no automap, and
        // its projection says so rather than drawing an empty rectangle.
        ContentCatalog? mapped = Declared(context.Content);
        MightAndMagic7Automap? automap = mapped is null ? null : new MightAndMagic7Automap(() => owners.World);
        MightAndMagic7MapSource? mapSource = mapped is null ? null : new MightAndMagic7MapSource(mapped);

        // This game's quests are read once, here, before its services: an errand is stated over the shipped
        // quest table, its giver is the ladder's own, and what its objectives name is the world's and the
        // monster table's own — so a town hall's bounty and the errand its keeper offers are one reading of
        // one encounter row rather than two that could advertise different beasts.
        // This game's answer about the encounters a level's spawn records ask for is composed once, here, before
        // the quests: which grade each creature is and how many stand on the field are drawn from the engine's
        // keyed random service under the place and the record, and remembered per placement, so the population a
        // world builds and the count an errand takes of "every one in that place" are one resolution.
        MightAndMagic7Spawns spawns = MightAndMagic7Spawns.Compose(Declared(context.Content), context.Engine?.Random);
        MightAndMagic7Quests? quests = MightAndMagic7Quests.Read(Declared(context.Content), promotions, spawns);
        if (quests is not null)
        {
            foreach (string note in quests.Notes)
            {
                context.Engine?.Diagnostics?.Publish(new DiagnosticsPublishRequest(
                    DiagnosticsSeverity.Info,
                    DiagnosticsDisposition.Accepted,
                    Source: "quest",
                    Code: "quest-note",
                    Message: note,
                    Correlation: string.Empty));
            }
        }

        // This game's services are read once, here, and the same answers are handed to the world — which
        // needs them to describe a counter the party talks to — and to the session, which serves it. One
        // reading of one placement is what keeps what a use offers and what a transaction does in step.
        // The quests travel with them, because a counter's bounty notice and a counter's refusal to buy
        // what an errand still needs are both answers about the party's own quest state.
        MightAndMagic7Services? services = MightAndMagic7Services.Read(
            Declared(context.Content),
            skills,
            spells,
            quests,
            () => owners.Quests);

        // This game's answers about people are read once here, for the same reason: the world needs them to
        // say who stands at a placement the party faces, and the session needs the one instance to speak
        // with them, so what a use reaches and who answers can never be two readings of one placement.
        MightAndMagic7Conversation? conversation = MightAndMagic7Conversation.Read(
            Declared(context.Content),
            services,
            promotions,
            quests,
            () => owners.Quests);
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

        // What this game makes of a party's standing is read once, here, over the tables that write what a
        // party accomplishes: the ladder that names a rank and its record, the quest definitions that name an
        // errand and its record, the counted deeds the ladder asks for, and the memberships the counters sell.
        // It is a policy and not state — the reputation and the records are the party's own — and the same
        // instance answers the conversation's standing line, the composition's projection, and the reading of
        // what an event does to the world's opinion, so one table states every threshold this game has.
        MightAndMagic7Standing standing = new(promotions, quests, services);

        // What reading the ladder noticed is reported where the other composition notes are: how many ranks
        // are stated, how many people give them, and how many of the classes they name content declares.
        foreach (string note in promotions.Notes)
        {
            context.Engine?.Diagnostics?.Publish(new DiagnosticsPublishRequest(
                DiagnosticsSeverity.Info,
                DiagnosticsDisposition.Accepted,
                Source: "promotion",
                Code: "promotion-note",
                Message: note,
                Correlation: string.Empty));
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

        // This game's fixtures are read once, here, over the map events and the discovery table content carries:
        // what a well gives, what an obelisk says, and what a sign reads are steps of the place's own events, run
        // through the owners that keep what each step names. The knowledge owner is read through a call because
        // the session composes it after the world, and a temporary resistance a well leaves is the same running
        // effect a ward is, so it goes into the ledger the fight reads resistances from.
        MightAndMagic7Fixtures fixtures = new(
            MightAndMagic7MapEvents.Read(Declared(context.Content)),
            () => owners.Knowledge,
            spellEffects,
            context.Engine?.Random,
            MightAndMagic7Tuning.Read(Declared(context.Content)),
            loot,
            spells,
            () => owners.Progression,
            person => conversation?.PersonOf(person));

        // This game's journal policy is read once, here, over the loot reading that knows which item rows the
        // shipped table hands out as artifacts and relics: that is the one threshold this game states about
        // what is worth writing down, and the words for its five books are stated beside it rather than in a
        // screen. It is a policy and not state — the owner that keeps the record is composed by the session
        // when it has a party and a clock — so a session that creates its party states it before the party
        // exists.
        MightAndMagic7Journal journal = new(loot);

        // This game's knowledge is read beside it and over the same loot reading, for the same reason: which
        // item rows are artifacts and relics is the one threshold this game states about a find, and a find
        // is one of the discoveries a party keeps. The two policies are separate records of one moment — a
        // dated line about what happened, and the fact the party can look up again — and both read the same
        // mark in the shipped table rather than each keeping its own reading of the rows.
        MightAndMagic7Knowledge knowledge = new(loot);

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
        // The fight reads what spells have left — a ward where a resistance is asked, a haste where recovery is
        // charged, a blessing where a chance to land is priced — so it is handed the party and the effects'
        // own ledger the same way: as providers, read at the moment the quantity is wanted rather than
        // captured when the policy was composed. What a ward on one character is worth is that character's own
        // reading, which is why the ledger travels beside the party rather than the party's effects alone.
        // A death is reported once, at the blow that caused it, to each of four observers named here in order:
        // the body is laid and its loot rolled, the errands a party has taken count it, a peaceful person's death is
        // a deed against the party (and a townsperson's is fined), and what it was worth is awarded under the word the crime path names for it — one report of
        // one death rather than four readings of the place. The fine is told before the award because it reads
        // the standing the deed is about to lower, which is the donor's own order.
        MightAndMagic7Crimes crimes = new(Townsperson, MightAndMagic7Combat.IsPerson, () => owners.Party, () => owners.Accounts);
        ICreatureDeathObserver[] deaths =
        [
            corpseAnswers,
            new QuestDeaths(() => owners.Quests),
            crimes,
            new ProgressionAwards(Worth, () => owners.Progression, crimes.SourceOf),
        ];
        // What each member wears is read through this game's figure, once: the fight reads its weapons and armour
        // into every sum it prices a character by, and the session's equipment owner offers its slots to the panel.
        MightAndMagic7Figure? figure = MightAndMagic7Figure.Read(Declared(context.Content));
        composed = MightAndMagic7Combat.Compose(Declared(context.Content), context.Engine?.Random, spells, () => owners.Party, () => spellEffects, figure, clock);
        MightAndMagic7Combat combat = composed;

        long Worth(PlacementDefinition placement) =>
            composed is { } fight
                ? fight.ExperienceOf(placement)
                : throw new InvalidOperationException(
                    "A death was reported before this session's fight was composed, so what it was worth could not be read.");

        int? Townsperson(PlacementDefinition placement) =>
            composed is { } fight
                ? fight.TownspersonLevel(placement)
                : throw new InvalidOperationException(
                    "A death was reported before this session's fight was composed, so whose it was could not be read.");

        // This game's answers about how a monster behaves are read once here, beside them: who hates whom is
        // the shipped hostility matrix as content, and what a creature does with its moment is its own row's
        // AI class, speed, attacks, and spells. The driver that asks these questions is the kit's, so the
        // other side of every fight is decided by this game's data rather than by a class per monster.
        MightAndMagic7MonsterAi monsterAi = MightAndMagic7MonsterAi.Compose(combat, context.Engine?.Random);

        SessionRules rules = new()
        {
            Service = services,
            Rest = rest,
            Conversation = conversation,
            Combat = new CombatRules(combat, monsterAi, Resolution: combat, Abilities: combat, Weapons: combat, Deaths: deaths, Reflection: combat),
            Progression = new ProgressionRules(MightAndMagic7Progression.Instance, promotions),
            Standing = standing,
            Skills = skills,
            Names = MightAndMagic7Names.Read(Declared(context.Content)),
            Magic = spells is null
                ? null
                : new MagicRules(
                    spells,
                    spellEffects,
                    Running: spellEffects,
                    Time: spellEffects,
                    Aim: spellEffects,
                    Members: spellEffects,
                    Sight: spellEffects,
                    Items: spells),
            Alchemy = alchemy is null ? null : new AlchemyRules(alchemy, alchemy.Catalog, Kinds: alchemy),
            Quests = quests,
            Journal = journal,
            Knowledge = knowledge,
            Map = automap is null || mapSource is null ? null : new MapRules(automap, mapSource),
            Equipment = figure,
        };

        EngineSessionSaveStore? store = MightAndMagic7Persistence.Store(context.Engine);
        try
        {
            SessionComposition composition = SessionComposition.From(ruleset) with
            {
                Bundle = context.Selection.BundleId,
                ContentPacks = context.Selection.PackCount,
            };
            SessionControls controls = new()
            {
                Movement = Movement(context),
                Creation = context.Creation,
                Save = context.Save,
                Use = context.Use,
                Service = context.Service,
                Rest = context.Rest,
                Conversation = context.Conversation,
                Combat = context.Combat,
                Skills = context.Skills,
                Cast = context.Cast,
                Mix = context.Mix,
                Equip = context.Equip,
                Keys = context.Keys,
            };

            SessionParty start;
            if (resume is { } save)
            {
                // The whole document is judged before anything moves, so every problem is named at once and a
                // defective save leaves no clock moved and no party restored behind it.
                MightAndMagic7Persistence.RequireLoadable(save, context.Content, quests, fixtures);

                // The clock takes the recorded game time before the world is composed, because the world's
                // places are read against the day the session stands on: a resumed session that restored its
                // ledger and then moved its clock would spend its first update crossing a boundary it had
                // already crossed.
                save.Clock.ApplyTo(clock);
                party = Capacity(MightAndMagic7Party.Restore(save.Party, Declared(context.Content)), spells)!;
                PartyResourceLedger ledger = Ledger(party);
                world = MightAndMagic7World.Compose(context.Content, context, clock, ledger, party, save, services, conversation, corpseAnswers, loot, quests, () => owners.Journal, combat, spawns, fixtures);
                start = new SessionParty.Playing(world, party, ledger, new SessionRecords(save.Quests, save.Journal, save.Knowledge, save.Maps));
            }
            else if (context.Creation is not null)
            {
                // The host declared a creation screen, so a new session creates its party. The flow is this
                // game's, the factory is the one every party comes from, and the world is composed with the
                // created party so the provisions a road costs come out of the larder the player's own
                // characters filled.
                ContentCatalog? declared = Declared(context.Content);
                start = new SessionParty.Creating(new SessionCreation(
                    MightAndMagic7Creation.Start(declared),
                    description => Capacity(MightAndMagic7Party.Factory(declared).Create(description), spells, fill: true)!,
                    created => MightAndMagic7World.Compose(declared, context, clock, Ledger(created), created, services: services, conversation: conversation, corpses: corpseAnswers, loot: loot, journal: () => owners.Journal, vitals: combat, spawns: spawns, fixtures: fixtures)));
            }
            else
            {
                // No creation screen was declared, so this session plays the party its scenario fixes: the
                // scripted path — a live check, a test, or a product that offers no creation.
                //
                // The scenario is read in the order it plays: which place the party begins in is the world's
                // own start rule, and who the party is is the party rule, so a selection wrong about both hears
                // about the start. A party refusal is therefore held where the party is read, and the world is
                // composed over no party so the start rule can speak first.
                ContentValidationException? parties = null;
                try
                {
                    party = Capacity(MightAndMagic7Party.Compose(context.Content), spells, fill: true);
                }
                catch (ContentValidationException refusal)
                {
                    parties = refusal;
                }

                PartyResourceLedger? accounts = party is null ? null : Ledger(party);
                world = MightAndMagic7World.Compose(context.Content, context, clock, accounts, party, services: services, conversation: conversation, corpses: corpseAnswers, loot: loot, journal: () => owners.Journal, vitals: combat, spawns: spawns, fixtures: fixtures);
                if (parties is not null) throw parties;
                start = new SessionParty.Playing(world, party, accounts);
            }

            // One session, composed one way: the resumed, created, and scenario paths differ only in how the
            // session starts, so every mechanism is composed over each of them by the same sequence.
            _session = new PartyRpgSession(
                composition,
                context.Projection,
                owners,
                start,
                rules,
                controls,
                store is null ? null : new SessionSaving(store, MightAndMagic7Persistence.SaveSlot));
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
    /// — the combat director gives one on its own under this game's monster AI, through that same entry.
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
    /// The quest owner this session keeps its journal through, or null until the party exists.
    /// </summary>
    /// <remarks>
    /// The session the product composes owns it, and this is that same owner read one layer out rather than
    /// a second one: the fight asks for it the moment a death is reported and a counter asks for it the
    /// moment the party tries to sell something, both of which happen after the party the owner was composed
    /// over exists.
    /// </remarks>
    internal PartyQuests? Quests => _session.Quests;

    /// <summary>
    /// The party's journal, or null until the session holds a clock and a party to write about.
    /// </summary>
    /// <remarks>
    /// The session the product composes owns it, and this is that same owner read one layer out rather than a
    /// second one: the world's interaction answers ask for it the moment a search yields something, which
    /// happens long after the party the owner was composed over exists. Reading it through a call is what
    /// lets this world be composed first and a party that is created later still have its finds written down.
    /// </remarks>
    internal PartyJournal? Journal => _session.Journal;

    /// <summary>
    /// What the party knows, or null until the session holds a clock and its ruleset stated discoveries.
    /// </summary>
    /// <remarks>
    /// It is read one layer out exactly as the journal beside it is: the owners that report a discovery —
    /// the mixing workflow this session composes, and any mechanism a use reaches — write through the owner
    /// the session holds rather than through one composed here, so there is one record and not two.
    /// </remarks>
    internal PartyKnowledge? Knowledge => _session.Knowledge;

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
    public ProductUpdateResult Update(ProductUpdate update) => _session.Update(update);

    /// <inheritdoc />
    public SessionSnapshot Inspect() => _session.Inspect();

    /// <inheritdoc />
    public Refusal? Look(double yawDegrees, double pitchDegrees) => _session.Look(yawDegrees, pitchDegrees);

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
