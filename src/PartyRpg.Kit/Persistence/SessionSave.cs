using PartyRpg.Kit.Journal;
using PartyRpg.Kit.Knowledge;
using PartyRpg.Kit.Maps;
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
/// progression and portraits, the shared inventory with each instance's custody, damage, charges and strength,
/// the purse, the larder, reputation, effects, records, holdings, passages, memberships, and the identity cursors — the clock's elapsed
/// game time, where the party stands, what each place remembers, every quest the party has a state about,
/// every line of the party's own history, every fact the party has learned, and every place it holds a map
/// of. The quest section carries
/// the instances the party took — their stage, their recorded progress, and the place each offer was taken
/// in — and deliberately not the quests themselves, which are read back from the game's own content when the
/// document is loaded. The journal and knowledge sections carry the party's own two records — dated lines
/// about what happened, and the facts it can look up again — and both carry them as the game time they were
/// written at rather than as dates, for the same reason the clock is saved that way: a date stored beside a
/// ruleset that composes another calendar or starting date would silently move what a party did and when it
/// learned things. Neither section is keyed by a place, which is what makes them survive a place reset. The
/// map section is the one that is keyed by place, and deliberately so: what a party has seen of a place is
/// ground rather than a fact about a thing, so it records the grid the cells were seen on and the cells, and
/// a place the world restores touches none of it.
/// Sections arrive with the owners that hold their state: containers and loose world items, and scenario
/// flags have no owner in the product yet, so a save has nothing of theirs to carry and this document does
/// not pretend otherwise by holding a section nobody fills.
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
    /// <param name="journal">Every line of the party's own history, or null when it has written none.</param>
    /// <param name="knowledge">Every fact the party has learned, or null when it has learned none.</param>
    /// <param name="maps">Every place the party holds a map of, or null when it has mapped none.</param>
    /// <exception cref="ArgumentNullException">A section is null, which is not a session a load could rebuild.</exception>
    public SessionSave(
        PartySave party,
        ClockSave clock,
        WorldSave world,
        QuestSave? quests = null,
        JournalSave? journal = null,
        KnowledgeSave? knowledge = null,
        MapSave? maps = null)
    {
        ArgumentNullException.ThrowIfNull(party);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(world);
        Party = party;
        Clock = clock;
        World = world;
        Quests = quests ?? QuestSave.None;
        Journal = journal ?? JournalSave.None;
        Knowledge = knowledge ?? KnowledgeSave.None;
        Maps = maps ?? MapSave.None;
    }

    /// <summary>The party, its items, its accounts, and its identity cursors.</summary>
    public PartySave Party { get; }

    /// <summary>How much game time had elapsed since the session began.</summary>
    public ClockSave Clock { get; }

    /// <summary>Where the party stands and what each place remembers.</summary>
    public WorldSave World { get; }

    /// <summary>Every quest the party has a state about, which is empty for a party that has taken none.</summary>
    public QuestSave Quests { get; }

    /// <summary>Every line of the party's own history, which is empty for a party that has written none.</summary>
    public JournalSave Journal { get; }

    /// <summary>Every fact the party has learned, which is empty for a party that has learned none.</summary>
    public KnowledgeSave Knowledge { get; }

    /// <summary>Every place the party holds a map of, which is empty for a party that has mapped none.</summary>
    public MapSave Maps { get; }

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

        // The schema carries no fight, so a save taken while one has left state behind — a debt of recovery,
        // a creature provoked or wounded, a round in progress — would load with that state silently gone. It is
        // refused by name instead, and a save once the fight is over succeeds.
        if (session.Combat?.UnsavedFight() is { Count: > 0 } fight)
        {
            List<string> problems = [.. fight.Select(left => $"the fight has {left}, which a save cannot carry yet")];
            throw new SessionSaveException($"The session cannot be saved during a fight: {string.Join("; ", problems)}.", problems);
        }

        // The quest owner is absent from a session that holds one for no party and from one whose ruleset
        // stated no quests at all: both are a party with no quest state, which is what an empty section
        // records rather than a section nobody filled. The journal and the knowledge beside it are absent on
        // the same terms — a session whose ruleset stated neither, or that holds no clock to date a line or a
        // note by, has written nothing down and learned nothing it keeps.
        return new SessionSave(
            held.Capture(),
            ClockSave.Capture(time, session.DeadlineOwners),
            place.Capture(),
            session.Quests?.Capture() ?? QuestSave.None,
            session.Journal?.Capture() ?? JournalSave.None,
            session.Knowledge?.Capture() ?? KnowledgeSave.None,
            session.Maps?.Capture() ?? MapSave.None);
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
    /// <param name="calendar">
    /// The calendar the recorded game time is counted in, which is what the world's recorded day is checked
    /// against. Without it the two day counts are not compared.
    /// </param>
    /// <returns>Every problem found, in the order the document records them.</returns>
    /// <exception cref="ArgumentNullException">The world's places or the party factory are null.</exception>
    public IReadOnlyList<string> Problems(
        PlaceGraph places,
        PartyEntityFactory parties,
        PlacePoseAdmission? admission = null,
        IQuestRule? quests = null,
        GameCalendar? calendar = null)
    {
        ArgumentNullException.ThrowIfNull(places);
        ArgumentNullException.ThrowIfNull(parties);

        List<string> problems = [.. parties.Problems(Party)];
        if (World.Places.ElapsedGameDays < 0)
        {
            problems.Add($"the world has reached day {World.Places.ElapsedGameDays}, which is before the session began");
        }

        // The world's day is the clock's own day count, written down twice: a document where the two disagree
        // would restore places against one day and advance them against another, so both are named.
        if (calendar is not null && Clock.ElapsedMilliseconds >= 0)
        {
            long clockDays = Clock.ElapsedMilliseconds / calendar.DayMilliseconds;
            if (clockDays != World.Places.ElapsedGameDays)
            {
                problems.Add($"the world has reached day {World.Places.ElapsedGameDays} while the clock has lived {clockDays} whole day(s), and the two are the same count");
            }
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

        // The journal is judged against the clock the save itself recorded, which is the one thing that says
        // how far into the session the party had got: a line dated after it happened in a future the party
        // never lived. Its own bound is asked of the journal rather than spelled here, so the rule that keeps
        // a history from growing without limit is stated in one place and enforced on both paths.
        problems.AddRange(Journal.Problems(Clock.ElapsedMilliseconds, JournalHistory.MaxEntries));

        // What the party knows is judged against the same recorded clock, for the same reason: a note dated
        // after the game time the save had reached was learned in a future the party never lived. Its own
        // bound is asked of the owner rather than spelled here, so the rule that keeps a party's knowledge
        // from growing without limit is stated in one place and enforced on both paths.
        problems.AddRange(Knowledge.Problems(Clock.ElapsedMilliseconds, PartyKnowledge.MaxNotes));

        // The maps are judged against the world they would be resumed into, and against their own bound: a
        // place the world does not have, a grid that is not a grid, and a cell beyond the grid it was written
        // on are contradictions rather than rules the product might refuse, and the bound is asked of the
        // owner so the rule that keeps a party's maps finite is stated in one place.
        problems.AddRange(Maps.Problems(places, PartyMaps.MaxPlaces));
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
