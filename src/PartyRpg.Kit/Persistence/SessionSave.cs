using PartyRpg.Kit.Party;
using PartyRpg.Kit.Quests;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Persistence;

/// <summary>
/// One session as a save records it, under the product's one current schema.
/// </summary>
/// <remarks>
/// <para>
/// <b>This document is the whole schema, and the schema has no version.</b> There is one current shape and
/// no other: a save is read by this type or it is not read at all, and a document that does not fit is a
/// defect to fix rather than an older save to migrate. Nothing here names a schema number, and nothing on
/// this path carries a compatibility fingerprint, a migration branch, or a reader for another layout —
/// including the original games', which the product never reads or writes.
/// </para>
/// <para>
/// <b>What is here is what the session owns as state:</b> the party — members with their skills, spells,
/// progression and portraits, the shared inventory with each instance's custody, damage and enchantments,
/// the purse, the larder, reputation, followers, effects, and the identity cursors — the clock's elapsed
/// game time, where the party stands, what each place remembers, and every quest the party has a state
/// about. The quest section carries the instances the party took — their stage, their recorded progress,
/// and the place each offer was taken in — and deliberately not the quests themselves, which are read back
/// from the game's own content when the document is loaded. Sections arrive with the owners that hold their
/// state: knowledge, containers and loose world items, and scenario flags have no owner in the product yet,
/// so a save has nothing of theirs to carry and this document does not pretend otherwise by holding a
/// section nobody fills.
/// </para>
/// <para>
/// <b>What is deliberately absent is as decided as what is here.</b> In-flight movement outcomes, cached
/// projection values, the mode and hold the shell reports, the admitted simulation it measures, the
/// population's runtime entities, engine handles, and every store-local entity identity are transient:
/// they describe the visit that produced them, and a load rebuilds them from content and from the state
/// above rather than restoring them. The save carries durable identity only — the member and item
/// identities the party minted — and never the engine identity of an entity, which is why a restored
/// session's entities are new while the people and the artifacts are the same.
/// </para>
/// </remarks>
public sealed record SessionSave
{
    /// <summary>Records a session's durable state.</summary>
    /// <param name="party">The party, its items, its accounts, and its identity cursors.</param>
    /// <param name="clock">How much game time had elapsed since the session began.</param>
    /// <param name="world">Where the party stands and what each place remembers.</param>
    /// <param name="quests">Every quest the party has a state about, or null when it has none.</param>
    /// <exception cref="ArgumentNullException">A section is null, which is not a session a load could rebuild.</exception>
    public SessionSave(PartySave party, ClockSave clock, WorldSave world, QuestSave? quests = null)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(world);
        Party = party;
        Clock = clock;
        World = world;
        Quests = quests ?? QuestSave.None;
    }

    /// <summary>The party, its items, its accounts, and its identity cursors.</summary>
    public PartySave Party { get; }

    /// <summary>How much game time had elapsed since the session began.</summary>
    public ClockSave Clock { get; }

    /// <summary>Where the party stands and what each place remembers.</summary>
    public WorldSave World { get; }

    /// <summary>Every quest the party has a state about, which is empty for a party that has taken none.</summary>
    public QuestSave Quests { get; }

    /// <summary>
    /// Reads a live session into the current schema, without writing anything anywhere.
    /// </summary>
    /// <remarks>
    /// This is the one composition of the document, so the shape a save has is decided in one place rather
    /// than assembled differently by each caller. A session that holds no party, no clock, or no world has
    /// nothing a load could rebuild, and is refused by name here instead of being written as a save that
    /// loads empty.
    /// </remarks>
    /// <param name="session">The live session to read.</param>
    /// <returns>The session as the current schema records it.</returns>
    /// <exception cref="ArgumentNullException">The session is null.</exception>
    /// <exception cref="SessionSaveException">The session holds nothing to save, naming every missing part.</exception>
    public static SessionSave Capture(PartyRpgSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        PartyEntity? held = session.Party;
        GameClock? time = session.Clock;
        SessionWorld? place = session.LiveWorld;

        List<string> missing = [];
        if (held is null) missing.Add("the session holds no party, so a save would load as an expedition nobody leads");
        if (time is null) missing.Add("the session holds no clock, so a save would load with no game time");
        if (place is null) missing.Add("the session holds no world, so a save would name no place to resume in");
        if (held is null || time is null || place is null)
        {
            throw new SessionSaveException(
                $"The session cannot be saved: {string.Join("; ", missing)}.",
                missing);
        }

        // The quest owner is absent from a session that holds one for no party and from one whose ruleset
        // stated no quests at all: both are a party with no quest state, which is what an empty section
        // records rather than a section nobody filled.
        return new SessionSave(
            held.Capture(),
            ClockSave.Capture(time),
            place.Capture(),
            session.Quests?.Capture() ?? QuestSave.None);
    }

    /// <summary>
    /// Every part of this save that is missing or contradicts the world it would be loaded into, or an empty
    /// list when it can be rebuilt.
    /// </summary>
    /// <remarks>
    /// The whole document is judged before anything is built, and every problem is reported at once: a
    /// half-built session is worse than a refused one, and fixing a defective save one problem per attempt
    /// wastes the only thing a failure is good for. The problems are contradictions between what the save
    /// records and what the world is — a place the graph does not have, a pose the place will not admit, a
    /// day the world has not reached, an item worn by nobody — rather than rules the product might refuse,
    /// because a load must not re-judge a session the product itself saved.
    /// </remarks>
    /// <param name="places">The world's places, which the save's recorded places and pose must belong to.</param>
    /// <param name="parties">The factory whose rules of rebuildability the party section is judged by.</param>
    /// <param name="admission">
    /// The place rule the party's pose is admitted by, when the world composes one. Without it the kit can
    /// only refuse a pose that is not made of numbers, because where a place's bounds are is the ruleset's
    /// answer and not this layer's to invent.
    /// </param>
    /// <param name="quests">
    /// This game's quests, when its ruleset states any, which is what an instance's own quest is judged
    /// against: a save that records an errand the game no longer states names an errand nothing can finish,
    /// and that is a contradiction to refuse rather than a quest to guess at.
    /// </param>
    /// <returns>Every problem found, in the order the document records them.</returns>
    /// <exception cref="ArgumentNullException">The world's places or the party factory are null.</exception>
    public IReadOnlyList<string> Problems(
        PlaceGraph places,
        PartyEntityFactory parties,
        PlacePoseAdmission? admission = null,
        IQuestRule? quests = null)
    {
        ArgumentNullException.ThrowIfNull(places);
        ArgumentNullException.ThrowIfNull(parties);

        List<string> problems = [.. parties.Problems(Party)];
        if (World.Places.ElapsedGameDays < 0)
        {
            problems.Add($"the world has reached day {World.Places.ElapsedGameDays}, which is before the session began");
        }

        HashSet<PlaceId> recorded = [];
        foreach (PlaceState state in World.Places.States)
        {
            if (places.Find(state.Place) is null)
            {
                problems.Add($"place '{state.Place}' is recorded as visited, and the world has no such place");
                continue;
            }

            if (!recorded.Add(state.Place))
            {
                problems.Add($"place '{state.Place}' is recorded twice");
            }

            if (state.RespawnCount < 0)
            {
                problems.Add($"place '{state.Place}' is recorded with {state.RespawnCount} restorations of its population");
            }

            if (state.LastResetDay is { } resetDay && resetDay > World.Places.ElapsedGameDays)
            {
                problems.Add($"place '{state.Place}' was last restored on day {resetDay}, after the day {World.Places.ElapsedGameDays} the save had reached");
            }
        }

        problems.AddRange(PoseProblems(places, admission));
        problems.AddRange(QuestProblems(places, quests));
        return problems;
    }

    /// <summary>
    /// Every problem with the quests the save records.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three contradictions are possible, and each is named rather than repaired. An instance whose quest
    /// this game's content no longer states is an errand nothing can judge, finish, or pay; one whose stage
    /// word this build does not have is a document written by something else; and one whose recorded place
    /// the world does not have is provenance pointing at nowhere — which is what a quest taken in a place
    /// the content no longer carries looks like, and it is refused here rather than silently kept.
    /// </para>
    /// <para>
    /// <b>An instance is not tied to its place.</b> Only the place an offer was taken in is recorded, and
    /// only as provenance: an instance whose place still exists but whose party has long since walked away
    /// is exactly what a quest is, so nothing here refuses a quest for being somewhere else.
    /// </para>
    /// </remarks>
    private IEnumerable<string> QuestProblems(PlaceGraph places, IQuestRule? quests)
    {
        HashSet<QuestId> recorded = [];
        foreach (QuestInstanceSave instance in Quests.Instances)
        {
            if (!recorded.Add(instance.Quest))
            {
                yield return $"the quest '{instance.Quest}' is recorded twice, so which instance the party's history belongs to would be ambiguous";
            }

            if (instance.Stage is not ("offered" or "accepted" or "turned-in"))
            {
                yield return $"the quest '{instance.Quest}' is recorded at the stage '{instance.Stage}', which is not one this build has";
            }

            if (quests is not null && quests.Definition(instance.Quest) is null)
            {
                yield return $"the quest '{instance.Quest}' is recorded and this game states no such quest, so nothing could judge, finish, or pay it";
            }

            if (instance.OfferedIn.Length > 0 && places.Find(new PlaceId(instance.OfferedIn)) is null)
            {
                yield return $"the quest '{instance.Quest}' was recorded as offered in place '{instance.OfferedIn}', which the world does not have";
            }

            if (string.IsNullOrWhiteSpace(instance.Giver))
            {
                yield return $"the quest '{instance.Quest}' records no giver, so nothing says who offered it or who it is finished with";
            }
        }
    }

    /// <summary>Every problem with where the save says the party is.</summary>
    private IEnumerable<string> PoseProblems(PlaceGraph places, PlacePoseAdmission? admission)
    {
        if (places.Find(World.Pose.Place) is null)
        {
            yield return $"the party is recorded in place '{World.Pose.Place}', which the world does not have";
            yield break;
        }

        if (!Finite(World.Pose.Pose))
        {
            yield return $"the party's recorded pose in place '{World.Pose.Place}' is not made of numbers, so there is nowhere to stand";
            yield break;
        }

        // The place's own rule is asked last because it is the only answer that can adjust the pose: a rule
        // that refuses it is what "the pose is outside the place" means, and the kit holds no bounds of its
        // own to second-guess that with.
        if (admission is not null && !admission(World.Pose.Place, World.Pose.Pose, out _))
        {
            yield return $"place '{World.Pose.Place}' does not admit the party's recorded pose {World.Pose.Pose}, so the party would resume outside the place";
        }
    }

    /// <summary>Whether a recorded pose is made of numbers, which is the one rule the kit owns about it.</summary>
    private static bool Finite(PlacePose pose) =>
        double.IsFinite(pose.X) &&
        double.IsFinite(pose.Y) &&
        double.IsFinite(pose.Z) &&
        double.IsFinite(pose.Yaw) &&
        double.IsFinite(pose.Pitch);
}
