using PartyRpg.Kit.Alchemy;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Conversation;
using PartyRpg.Kit.Input;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Journal;
using PartyRpg.Kit.Knowledge;
using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Maps;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Progression;
using PartyRpg.Kit.Promotion;
using PartyRpg.Kit.Quests;
using PartyRpg.Kit.Services;
using PartyRpg.Kit.Skills;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Sessions;

/// <summary>
/// A game's answers, grouped by the mechanism each one is composed into.
/// </summary>
/// <remarks>
/// <para>
/// A rule that only means something beside another one travels with it: a creature's behaviour with the fight it
/// acts in, a rank ladder with the progression owner that moves a rank, a mixture table with the answers about
/// mixing, and a map source with the automap's rule. So a combination that could compose nothing — a map rule
/// with no maps, an AI with no fight — is a shape this record does not have, rather than a branch the session
/// quietly takes.
/// </para>
/// <para>
/// Every group is optional: a game that has not answered for a mechanism composes none of it, and the
/// projection says the mechanism is not there rather than showing an empty one.
/// </para>
/// </remarks>
public sealed record SessionRules
{
    /// <summary>A session composed over no answers at all.</summary>
    public static SessionRules None { get; } = new();

    /// <summary>The answers about services a counter serves.</summary>
    public IServiceRule? Service { get; init; }

    /// <summary>The answers about sleeping, camping, waiting, and going without sleep.</summary>
    public IRestRule? Rest { get; init; }

    /// <summary>The answers about who stands in a place and what they say.</summary>
    public IConversationRule? Conversation { get; init; }

    /// <summary>The answers about fighting, and about how a creature behaves in a fight.</summary>
    public CombatRules? Combat { get; init; }

    /// <summary>The answers about experience, levels, and ranks.</summary>
    public ProgressionRules? Progression { get; init; }

    /// <summary>The game's words for a party's standing and its accomplishments.</summary>
    public IStandingRule? Standing { get; init; }

    /// <summary>The answers about the game's skills: the catalog, the ceilings, and what a raise costs.</summary>
    public ISkillRule? Skills { get; init; }

    /// <summary>The answers about spells: what they cost and require, and what their effects do.</summary>
    public MagicRules? Magic { get; init; }

    /// <summary>The answers about mixing, with the mixtures the game states.</summary>
    public AlchemyRules? Alchemy { get; init; }

    /// <summary>The quests the game states.</summary>
    public IQuestRule? Quests { get; init; }

    /// <summary>The answers about the journal: its books and how a line reads.</summary>
    public IJournalRule? Journal { get; init; }

    /// <summary>The answers about what the party learns.</summary>
    public IKnowledgeRule? Knowledge { get; init; }

    /// <summary>The answers about the automap, with the places' own maps.</summary>
    public MapRules? Map { get; init; }
}

/// <summary>A game's answers about fighting, and the policy its creatures act by.</summary>
/// <param name="Rule">What each actor is worth in recovery, what hostility means, and what an attack reaches.</param>
/// <param name="Ai">How a creature decides, or null for a fight whose other side never acts.</param>
public sealed record CombatRules(ICombatRule Rule, IMonsterAiPolicy? Ai = null);

/// <summary>A game's answers about growth, and the ladder of ranks it states.</summary>
/// <param name="Rule">The experience curve and what a level grants.</param>
/// <param name="Promotions">The ranks a class leads to, or null for a game that states none.</param>
public sealed record ProgressionRules(IProgressionRule Rule, IPromotionRule? Promotions = null);

/// <summary>A game's answers about spells, and the path their effects take.</summary>
/// <param name="Spells">What each spell costs, requires, and aims at.</param>
/// <param name="Effects">What a spell that lands does, or null for a game whose spells change nothing yet.</param>
/// <param name="Running">
/// The effects a spell leaves running on the one clock, which a detection's drawing and a panel's list read,
/// or null when the effect path keeps none.
/// </param>
/// <param name="Time">
/// The part of the effect path that hears the one clock, so a ward ends in the advance that reaches its
/// deadline; null when the effect path keeps nothing that lasts.
/// </param>
public sealed record MagicRules(
    ISpellRule Spells,
    ISpellEffectRule? Effects = null,
    IRunningSpellEffects? Running = null,
    IGameTimeObserver? Time = null);

/// <summary>A game's answers about mixing, and the mixtures its own table states.</summary>
/// <param name="Rule">What mixing asks of a character and what a mixture that goes off costs.</param>
/// <param name="Mixtures">The mixtures the game states.</param>
public sealed record AlchemyRules(IAlchemyRule Rule, AlchemyCatalog Mixtures);

/// <summary>A game's answers about the automap, and where each place's own map comes from.</summary>
/// <param name="Rule">How far a walking party sees and how the map is drawn.</param>
/// <param name="Source">Each place's own map, as content carries it.</param>
public sealed record MapRules(IMapRule Rule, IPlaceMapSource Source);

/// <summary>What a save recorded of the party's own records, handed back to the owners that keep them.</summary>
/// <param name="Quests">The errands the party held.</param>
/// <param name="Journal">The lines the party wrote down.</param>
/// <param name="Knowledge">What the party had learned.</param>
/// <param name="Maps">What the party had mapped.</param>
public sealed record SessionRecords(
    QuestSave? Quests = null,
    JournalSave? Journal = null,
    KnowledgeSave? Knowledge = null,
    MapSave? Maps = null);

/// <summary>
/// Whether a session plays a party it was handed or creates one: the two shapes a session starts in.
/// </summary>
/// <remarks>
/// A session holds at most one party. One that creates holds the flow and composes the party and its world
/// when the player accepts them; one that plays holds them from the start. The two are distinct types, so a
/// session handed both a flow and a party is a shape that cannot be written rather than one refused at run
/// time.
/// </remarks>
public abstract record SessionParty
{
    private SessionParty()
    {
    }

    /// <summary>A session that plays nobody: no party, and no world for one.</summary>
    public static SessionParty Nobody { get; } = new Playing();

    /// <summary>A session that plays the party it was handed.</summary>
    /// <param name="World">The live world the party walks in, or null when content supplied no places.</param>
    /// <param name="Party">The party, or null when content supplied none.</param>
    /// <param name="Accounts">
    /// The party's one settlement path, or null to use the world's own, so a shop and a road can never be two
    /// ledgers.
    /// </param>
    /// <param name="Resumed">
    /// What a save recorded of the party's own records, when the session was composed from a save. Its presence
    /// is what makes the session a resumed one: an empty slot and a session playing on from one are otherwise
    /// indistinguishable.
    /// </param>
    public sealed record Playing(
        SessionWorld? World = null,
        PartyEntity? Party = null,
        PartyResourceLedger? Accounts = null,
        SessionRecords? Resumed = null) : SessionParty;

    /// <summary>A session that creates its party, and composes it and its world when creation is accepted.</summary>
    /// <param name="Creation">The flow, and the two factories that end it.</param>
    public sealed record Creating(SessionCreation Creation) : SessionParty;
}

/// <summary>The controls a host declared, by the names the player's requests arrive on.</summary>
/// <remarks>
/// Each one is optional: a host that declares no control of a kind gets a session whose mechanism is still
/// composed and still published, and that never acts on that kind by itself.
/// </remarks>
public sealed record SessionControls
{
    /// <summary>A session no player can ask anything of.</summary>
    public static SessionControls None { get; } = new();

    /// <summary>What reads the movement controls, with the turn rate the game states.</summary>
    public MovementInput? Movement { get; init; }

    /// <summary>The creation controls, which a session that creates its party cannot do without.</summary>
    public CreationIntentNames? Creation { get; init; }

    /// <summary>The save controls.</summary>
    public SaveIntentNames? Save { get; init; }

    /// <summary>The use controls.</summary>
    public UseIntentNames? Use { get; init; }

    /// <summary>The service controls.</summary>
    public ServiceIntentNames? Service { get; init; }

    /// <summary>The stop controls.</summary>
    public RestIntentNames? Rest { get; init; }

    /// <summary>The conversation controls.</summary>
    public ConversationIntentNames? Conversation { get; init; }

    /// <summary>The act control, and the turn controls a paced fight is taken with.</summary>
    public CombatIntentNames? Combat { get; init; }

    /// <summary>The skill-spend control.</summary>
    public SkillRaiseIntentNames? Skills { get; init; }

    /// <summary>The casting controls.</summary>
    public CastIntentNames? Cast { get; init; }

    /// <summary>The mixing control.</summary>
    public MixIntentNames? Mix { get; init; }
}

/// <summary>Where a session's saves go, when the product has somewhere to keep them.</summary>
/// <param name="Store">The store the session owns and releases with itself.</param>
/// <param name="Slot">The slot the session saves under.</param>
public sealed record SessionSaving(ISessionSaveStore Store, string Slot = SessionSaveBoundary.DefaultSlot);
