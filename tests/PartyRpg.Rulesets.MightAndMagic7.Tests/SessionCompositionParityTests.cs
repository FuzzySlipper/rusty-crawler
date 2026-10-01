using System.Reflection;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Testing;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

/// <summary>
/// A party the player creates and a party the scenario fixes are played by the same owners, and every one of them
/// answers about the party the session ended up playing.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> Three stones in a row shipped a mechanism the normal play path could not reach: a
/// conversation composed before the created party existed, a created party with no mixing, and one with no quest
/// owner. Each was a one-line omission in one of two composition lists, invisible to the feature's own tests, and
/// found only by a live check. The two ways a party comes into being now share one composition — the ruleset's
/// <c>Play</c> composes the party's accounts and world for both, and the kit's <see cref="SessionOwners"/> composes
/// every owner over it through the same <c>Take</c> — and this suite is what keeps it that way: it composes a session
/// both ways through the ruleset's one public entry and asks each owner to answer.
/// </para>
/// <para>
/// <b>It fails on a missing owner, by name and path.</b> Each owner is probed on its own, so an owner composed on
/// one path and not the other reads as "<c>Mixing</c> is missing on the created path" rather than as two lists that
/// differ. A probe asks the owner something it can only answer when it was composed over the session's own party —
/// the party it serves, the fight it is paced by, the ledger it settles through — so an owner composed over nothing,
/// or over a party the session no longer plays, fails as plainly as an absent one.
/// </para>
/// <para>
/// <b>What differs by design is stated, not papered over.</b> A created party is the creation flow's band and a
/// scenario's party is the one its content fixes, so their members differ; a created session passes through
/// creation first and a scenario session never shows the screen. Neither changes which owners play the party.
/// </para>
/// </remarks>
public sealed class SessionCompositionParityTests
{
    /// <summary>
    /// Every owner the session composes, and the question that proves it answers about the session's own party.
    /// </summary>
    /// <remarks>
    /// A probe returns null when the owner answered, or the reason it did not. Presence is judged first, so a probe
    /// only runs over an owner that exists.
    /// </remarks>
    private static readonly (string Owner, Func<SessionOwners, object?> Present, Func<SessionOwners, PartyEntity, string?> Answers)[] Owners =
    [
        (nameof(SessionOwners.Clock), owners => owners.Clock, (owners, _) =>
            owners.Clock!.Observers > 0 ? null : "tells no owner about an advance"),
        (nameof(SessionOwners.World), owners => owners.World, (owners, _) =>
            owners.World!.Place.Value != Start ? $"stands the party in '{owners.World.Place.Value}' rather than the scenario's start '{Start}'"
            : owners.World.Interaction is null ? "composed no interaction for the party to use what it faces"
            : !ReferenceEquals(owners.World.Accounts, owners.Accounts) ? "charges a journey to a ledger other than the one a shop settles through"
            : null),
        (nameof(SessionOwners.Accounts), owners => owners.Accounts, (owners, _) =>
            owners.Accounts!.Judge(PartyCost.Free) is { } refusal ? $"refuses a free charge: {refusal.Message}" : null),
        (nameof(SessionOwners.Progression), owners => owners.Progression, (owners, party) =>
            !ReferenceEquals(owners.Progression!.Party, party) ? "grows a party the session does not play"
            : owners.Progression.ExperienceForNextLevel(party.Members[0]) <= 0 ? "states no experience for the next level"
            : null),
        (nameof(SessionOwners.Rest), owners => owners.Rest, (owners, party) =>
            !ReferenceEquals(owners.Rest!.Party, party) ? "rests a party the session does not play"
            : !owners.Rest.Available ? "has no clock or no place to stop in"
            : null),
        (nameof(SessionOwners.Services), owners => owners.Services, (owners, party) =>
            !ReferenceEquals(owners.Services!.Party, party) ? "serves a party the session does not play"
            : owners.Services.Coins != party.Purse.Coins ? "reads a purse other than the party's"
            : null),
        (nameof(SessionOwners.Conversations), owners => owners.Conversations, (owners, party) =>
            !ReferenceEquals(owners.Conversations!.Party, party) ? "reads a person's topics against a party the session does not play"
            : null),
        (nameof(SessionOwners.Quests), owners => owners.Quests, (owners, party) =>
            !ReferenceEquals(owners.Quests!.Party, party) ? "keeps the errands of a party the session does not play" : null),
        (nameof(SessionOwners.Journal), owners => owners.Journal, (owners, _) =>
            owners.Journal!.Capture() is null ? "cannot state what it holds" : null),
        (nameof(SessionOwners.Knowledge), owners => owners.Knowledge, (owners, _) =>
            owners.Knowledge!.Capture() is null ? "cannot state what it holds" : null),
        (nameof(SessionOwners.Maps), owners => owners.Maps, (owners, _) =>
            owners.Maps!.Capture() is null ? "cannot state what it holds" : null),
        (nameof(SessionOwners.Combat), owners => owners.Combat, (owners, party) =>
            !ReferenceEquals(owners.Combat!.Party, party) ? "is fought by a party the session does not play"
            : party.Members.FirstOrDefault(member => owners.Combat.Find(CombatantId.Of(member.Id)) is null) is { } absent
                ? $"holds no combatant for member '{absent.Profile.Name}'"
                : null),
        (nameof(SessionOwners.Director), owners => owners.Director, (owners, party) =>
            owners.Director!.Rounds(CombatantId.Of(party.Members[0].Id)) != 0 ? "counts rounds nobody fought" : null),
        (nameof(SessionOwners.Casting), owners => owners.Casting, (owners, party) =>
            !ReferenceEquals(owners.Casting!.Party, party) ? "casts for a party the session does not play"
            : !ReferenceEquals(owners.Casting.Fight, owners.Combat) ? "is paced by a fight other than the session's"
            : null),
        (nameof(SessionOwners.Mixing), owners => owners.Mixing, (owners, party) =>
            !ReferenceEquals(owners.Mixing!.Party, party) ? "mixes for a party the session does not play" : null),
        (nameof(SessionOwners.Outfitting), owners => owners.Outfitting, (owners, party) =>
            !ReferenceEquals(owners.Outfitting!.Party, party) ? "dresses a party the session does not play" : null),
        // The standing policy is a rule rather than an owner, read over the party the session plays.
        ("Standing", owners => owners.Rules.Standing, (owners, party) =>
            owners.Rules.Standing!.Read(party).Band.Length == 0 ? "reads no standing band for the party" : null),
    ];

    /// <summary>The place the scenario starts every party in.</summary>
    private const string Start = "2";

    [Fact]
    public void A_created_party_and_a_scenario_party_are_each_played_by_every_owner_and_each_owner_answers()
    {
        List<string> problems = [];
        foreach ((string path, string word) in Paths)
        {
            (MightAndMagic7Session session, _) = Compose(word);
            using (session)
            {
                problems.AddRange(Problems(path, session.Owners));
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Fact]
    public void Both_paths_register_the_same_number_of_owners_with_the_one_clock()
    {
        // Every advance reaches every owner registered with the clock, so the two paths must register the same
        // ones: a time owner composed on one path alone is a deadline that path never hears.
        Dictionary<string, int> observers = [];
        foreach ((string path, string word) in Paths)
        {
            (MightAndMagic7Session session, _) = Compose(word);
            using (session)
            {
                observers[path] = session.Owners.Clock!.Observers;
            }
        }

        Assert.True(
            observers["created"] == observers["scenario"],
            $"The clock tells {observers["created"]} owners about an advance on the created path and {observers["scenario"]} on the scenario path.");
    }

    [Fact]
    public void Every_owner_the_session_composes_is_probed_here()
    {
        // A new owner added to the kit's composition and not to this suite is an owner neither path is proved to
        // compose, so the suite names it rather than passing without it.
        HashSet<string> probed = [.. Owners.Select(owner => owner.Owner)];
        string[] unprobed =
        [
            .. typeof(SessionOwners).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.Name is not nameof(SessionOwners.Rules) and not nameof(SessionOwners.Party))
                .Select(property => property.Name)
                .Where(name => !probed.Contains(name)),
        ];

        Assert.True(unprobed.Length == 0, $"SessionOwners composes {string.Join(", ", unprobed)}, which this suite does not ask to answer on either path.");
    }

    [Fact]
    public void What_differs_by_design_is_who_the_party_is_and_whether_creation_was_shown()
    {
        (MightAndMagic7Session created, RecordingUiService createdUi) = Compose("creation");
        (MightAndMagic7Session scenario, RecordingUiService scenarioUi) = Compose("scenario");
        using (created)
        using (scenario)
        {
            // The created party is the creation flow's band; the scenario's is the one its content fixes.
            Assert.Equal(MightAndMagic7Creation.Defaults.Members.Count, created.Owners.Party!.Members.Count);
            Assert.Equal("Beta", Assert.Single(scenario.Owners.Party!.Members).Profile.Name);

            // A created session passed through creation and says so; a scenario session never showed the screen.
            Assert.Equal("creation", ProjectedNode.Of(createdUi.Latest().Value).Field("composition").Field("partyStart").AsString());
            Assert.Equal("scenario", ProjectedNode.Of(scenarioUi.Latest().Value).Field("composition").Field("partyStart").AsString());
            Assert.True(createdUi.Projections.Any(projection => ProjectedNode.Of(projection.Value).Field("creation").Field("active").AsBoolean()));
            Assert.DoesNotContain(scenarioUi.Projections, projection => ProjectedNode.Of(projection.Value).Field("creation").Field("active").AsBoolean());

            // Neither is a different session past that point: both are running, over the scenario's start.
            Assert.Equal(SessionMode.Running, created.Mode);
            Assert.Equal(SessionMode.Running, scenario.Mode);
        }
    }

    /// <summary>The two ways a party comes into being, by the path name a failure reports and the start word that takes it.</summary>
    private static readonly (string Path, string Word)[] Paths = [("created", "creation"), ("scenario", "scenario")];

    /// <summary>Everything wrong with one path's owners, each naming the owner and the path.</summary>
    private static IEnumerable<string> Problems(string path, SessionOwners owners)
    {
        if (owners.Party is not { } party)
        {
            yield return $"Party is missing on the {path} path, so no owner can be asked about it.";
            yield break;
        }

        foreach ((string owner, Func<SessionOwners, object?> present, Func<SessionOwners, PartyEntity, string?> answers) in Owners)
        {
            if (present(owners) is null)
            {
                yield return $"{owner} is missing on the {path} path.";
                continue;
            }

            if (answers(owners, party) is { } reason) yield return $"{owner} on the {path} path {reason}.";
        }
    }

    /// <summary>
    /// A session composed through the ruleset's one public entry by a host that offers creation, over a scenario
    /// whose start states the given party word — and, for creation, with the default party accepted.
    /// </summary>
    private static (MightAndMagic7Session Session, RecordingUiService Ui) Compose(string word)
    {
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(Content(word));
        IGameSession composed = MightAndMagic7Ruleset.Instance.CreateSession(RulesetTestContext.RulesetContext(context, ui, creation: true));
        MightAndMagic7Session session = Assert.IsType<MightAndMagic7Session>(composed);
        session.Start();
        if (word == "creation")
        {
            Assert.Equal(SessionMode.Creating, session.Mode);
            session.Update(RulesetTestContext.Update(1, 1, RulesetTestContext.Payload("""{"action":"creation.accept"}""")));
        }

        session.Update(RulesetTestContext.Update(2, 1));
        Assert.Equal(SessionMode.Running, session.Mode);
        return (session, ui);
    }

    /// <summary>
    /// Places, the creation tables, and one scenario that carries a start and a party: the shape a scenario pack has
    /// in the operator's own root, so both starts are taken over the same content and differ only in the start word.
    /// </summary>
    private static (string Path, string Text)[] Content(string word) =>
    [
        RulesetTestContext.Bundle(RulesetTestContext.BundleId, "world", "creation-tables", "scenario"),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/pack.json", TestPacks.PlacesOnly),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/places.json",
            TestPacks.Document("places", "place",
                """{ "id": "1", "kind": "interior", "name": "Alpha Keep", "respawnDays": 1, "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ] }""",
                """{ "id": "2", "kind": "interior", "name": "Beta Keep", "respawnDays": 1, "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ] }""")),
        .. RulesetTestContext.CreationTables(),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/scenario/pack.json",
            TestPacks.Manifest("scenario", ("scenario-start", "scenario-start"), ("scenario-party", "scenario-party"))),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/scenario/scenario-start.json",
            TestPacks.Document("scenario-start", "scenario-start",
                $$"""{ "id": "start", "place": "{{Start}}", "entryPoint": "Party Start", "party": "{{word}}" }""")),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/scenario/scenario-party.json",
            TestPacks.Document("scenario-party", "scenario-party",
                """{ "id": "party", "coins": 22, "food": 6, "reputation": 0, "fame": 0, "members": [ { "name": "Beta", "race": "Human", "class": "Knight", "level": 1, "hitPoints": 40, "spellPoints": 0 } ] }""")),
    ];
}
