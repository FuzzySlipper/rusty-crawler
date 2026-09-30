using PartyRpg.Kit.Content;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

/// <summary>
/// This game's own answers about clocked places and about stopping: when a door is locked, what a night
/// costs, what a camp refuses, what breaks it, and what going without sleep does.
/// </summary>
/// <remarks>
/// <para>
/// The ruleset's schedule and rest policy are internal because nothing outside the product composes them, so
/// this suite reaches them through the ruleset's own friend declaration. What it proves is what no kit test
/// can: that a place's hours come from the counters standing in it or from the place's own entry, that a
/// door is locked outside them and opens again when the clock comes round, and that the ground, the
/// hostiles, the broken night, and the fatigue debt are this game's numbers rather than a test's.
/// </para>
/// <para>
/// The session is composed through the ruleset's one entry with the use and stop controls the host declares,
/// so the flow under test is the flow a player takes: the clocks are the product's own scale, the door is the
/// one interaction mechanism's, and the commands arrive on the declared intents.
/// </para>
/// </remarks>
public sealed class RestAndSchedulePolicyTests
{
    private static readonly UseIntentNames UseControls = new(
        Declared.UseIntent,
        Declared.UiActionContract);

    private static readonly RestIntentNames RestControls = new(
        Declared.RestIntent,
        Declared.CampIntent,
        Declared.WaitUntilDawnIntent,
        Declared.WaitAnHourIntent,
        Declared.WaitFiveMinutesIntent,
        Declared.UiActionContract);

    /// <summary>What one admitted second of this game is worth, which is the product's own scale of thirty.</summary>
    private static readonly double GameSecondsPerSecond = MightAndMagic7Time.Scale.GameSecondsPerRealSecond;

    [Fact]
    public void A_places_hours_lock_its_door_at_closing_and_open_it_again_by_day()
    {
        using IGameSession session = Shop(out RecordingUiService ui, ShopDoor);
        ulong step = 0;

        // Nine in the morning: the shop keeps 6 to 18, so the door is simply a door the party can open, and
        // it requires nothing of anybody.
        session.Update(RulesetTestContext.Update(step++, 1));
        ProjectedNode morning = ProjectedNode.Of(ui.Latest().Value);
        Assert.Equal("The Sword and Shield", morning.Field("world").Field("name").AsString());
        Assert.True(morning.Field("world").Field("open").AsBoolean());
        Assert.Equal("06:00–18:00", morning.Field("world").Field("hours").AsString());
        Assert.Equal("1168-01-01 18:00", morning.Field("world").Field("nextChange").AsString());
        ProjectedNode door = morning.Field("interaction");
        Assert.Equal("A door", door.Field("label").AsString());
        Assert.Equal("open", door.Field("verb").AsString());
        Assert.Equal("the hours 06:00–18:00", door.Field("requires").Item(0).AsString());

        // Six in the evening exactly: the closing hour is excluded, so the same door now requires the shop's
        // hours, and using it is refused with the clock and the hour it opens again named.
        session.Update(RulesetTestContext.Update(step++, 1080, 1.0));
        ProjectedNode evening = ProjectedNode.Of(ui.Latest().Value);
        Assert.Equal("18:00", evening.Field("clock").Field("time").AsString());
        Assert.False(evening.Field("world").Field("open").AsBoolean());
        ProjectedNode shut = evening.Field("interaction");
        Assert.Equal("the hours 06:00–18:00", shut.Field("requires").Item(0).AsString());
        session.Update(RulesetTestContext.Update(step++, 1, RulesetTestContext.Digital(Declared.UseIntent)));
        ProjectedNode refused = ProjectedNode.Of(ui.Latest().Value).Field("interaction");
        Assert.Equal("refused", refused.Field("outcome").AsString());
        Assert.Equal("interaction-requirement-unmet", refused.Field("code").AsString());
        Assert.Contains("06:00–18:00", refused.Field("message").AsString(), StringComparison.Ordinal);
        Assert.Contains("18:00", refused.Field("message").AsString(), StringComparison.Ordinal);
        Assert.Contains("1168-01-02 06:00", refused.Field("message").AsString(), StringComparison.Ordinal);

        // Twelve hours on: the clock comes round to six in the morning, and the same door — nothing about it
        // was ever remembered — is open again and swings open in the same use a player made at midnight.
        session.Update(RulesetTestContext.Update(step++, 1440, 1.0));
        ProjectedNode dawn = ProjectedNode.Of(ui.Latest().Value);
        Assert.Equal("1168-01-02", dawn.Field("clock").Field("date").AsString());
        Assert.Equal("06:00", dawn.Field("clock").Field("time").AsString());
        Assert.True(dawn.Field("world").Field("open").AsBoolean());
        session.Update(RulesetTestContext.Update(step, 1, RulesetTestContext.Digital(Declared.UseIntent)));
        ProjectedNode opened = ProjectedNode.Of(ui.Latest().Value).Field("interaction");
        Assert.Equal("applied", opened.Field("outcome").AsString());
        Assert.Equal("open", opened.Field("state").AsString());
        Assert.Contains("swings open", opened.Field("message").AsString(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_rest_heals_and_moves_the_clock_while_a_wait_moves_it_without_healing()
    {
        using IGameSession session = Shop(out RecordingUiService ui, ShopChest);
        ulong step = 0;

        // The chest at the party's feet is trapped and the party has no sense for traps, so searching it sets
        // the trap off: what a rest has to heal is this, and the panel shows it as the party's own pools.
        session.Update(RulesetTestContext.Update(step++, 1));
        Assert.Equal("A chest", ProjectedNode.Of(ui.Latest().Value).Field("interaction").Field("label").AsString());
        session.Update(RulesetTestContext.Update(step++, 1, RulesetTestContext.Digital(Declared.UseIntent)));
        ProjectedNode sprung = ProjectedNode.Of(ui.Latest().Value);
        Assert.Equal("applied", sprung.Field("interaction").Field("outcome").AsString());
        Assert.Contains("goes off", sprung.Field("interaction").Field("message").AsString(), StringComparison.Ordinal);
        Assert.Equal(25, sprung.Field("party").Field("hitPoints").AsNumber());
        Assert.Equal(40, sprung.Field("party").Field("hitPointsMax").AsNumber());

        // An hour of waiting passes the clock and restores nobody: the wounded party is exactly as wounded.
        session.Update(RulesetTestContext.Update(step++, 1, RulesetTestContext.Digital(Declared.WaitAnHourIntent)));
        ProjectedNode waited = ProjectedNode.Of(ui.Latest().Value);
        Assert.Equal("wait-hour", waited.Field("rest").Field("kind").AsString());
        Assert.Equal("applied", waited.Field("rest").Field("outcome").AsString());
        Assert.False(waited.Field("rest").Field("recovered").AsBoolean());
        Assert.Equal("10:00", waited.Field("clock").Field("time").AsString());
        Assert.Equal(25, waited.Field("party").Field("hitPoints").AsNumber());
        Assert.Contains("nothing was restored", waited.Field("rest").Field("message").AsString(), StringComparison.OrdinalIgnoreCase);

        // A rest of eight hours: the clock moves, the larder pays two portions, and every member is restored.
        session.Update(RulesetTestContext.Update(step, 1, RulesetTestContext.Digital(Declared.RestIntent)));
        ProjectedNode rested = ProjectedNode.Of(ui.Latest().Value);
        Assert.Equal("rest", rested.Field("rest").Field("kind").AsString());
        Assert.Equal("applied", rested.Field("rest").Field("outcome").AsString());
        Assert.True(rested.Field("rest").Field("recovered").AsBoolean());
        Assert.Equal(1, rested.Field("rest").Field("restored").AsNumber());
        Assert.Equal(2, rested.Field("rest").Field("charged").AsNumber());
        Assert.Equal(2, rested.Field("rest").Field("covered").AsNumber());
        Assert.Equal(28800, rested.Field("rest").Field("elapsedSeconds").AsNumber());
        Assert.Equal("1168-01-01 18:00", rested.Field("rest").Field("to").AsString());
        Assert.Equal(40, rested.Field("party").Field("hitPoints").AsNumber());
        Assert.Equal(4, rested.Field("party").Field("provisions").AsNumber());
    }

    [Fact]
    public void A_bundles_tuning_pack_sets_how_long_a_night_is_and_a_value_out_of_range_is_refused_at_composition()
    {
        // The same shop, assembled by a bundle that also names a tuning pack stating a six-hour night: the rest
        // advances the clock by what the pack says rather than by the ruleset's default of eight.
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(Tuned(6, ShopChest));
        using (IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            RulesetTestContext.RulesetContext(context, ui) with { Use = UseControls, Rest = RestControls }))
        {
            session.Start();
            session.Update(RulesetTestContext.Update(0, 1, RulesetTestContext.Digital(Declared.RestIntent)));
            ProjectedNode rested = ProjectedNode.Of(ui.Latest().Value);
            Assert.Equal("applied", rested.Field("rest").Field("outcome").AsString());
            Assert.Equal(6 * 3600, rested.Field("rest").Field("elapsedSeconds").AsNumber());
            Assert.Equal(2, rested.Field("rest").Field("charged").AsNumber());
        }

        // A night the handle does not admit is named where the session is composed, not met in play.
        (ProductCreateContext refused, RecordingUiService refusedUi) = RulesetTestContext.Create(Tuned(30, ShopChest));
        ContentValidationException error = Assert.Throws<ContentValidationException>(() =>
            MightAndMagic7Ruleset.Instance.CreateSession(
                RulesetTestContext.RulesetContext(refused, refusedUi) with { Use = UseControls, Rest = RestControls }));
        Assert.Contains(error.Issues, issue => issue.Code == "tuning-out-of-range");
    }

    [Fact]
    public void A_held_session_passes_no_time_and_applies_no_stop_until_it_is_released()
    {
        using IGameSession session = Shop(out RecordingUiService ui, ShopChest);
        ulong step = 0;
        session.Update(RulesetTestContext.Update(step++, 1));
        string before = ProjectedNode.Of(ui.Latest().Value).Field("clock").Field("time").AsString();

        // A held session is quiescent: the wait asked for while it is held moves no clock, admitted steps move
        // no clock either, and the chest at the party's feet is not searched.
        session.Hold();
        session.Update(RulesetTestContext.Update(step++, 600, RulesetTestContext.Digital(Declared.WaitAnHourIntent)));
        session.Update(RulesetTestContext.Update(step++, 1, RulesetTestContext.Digital(Declared.UseIntent)));
        ProjectedNode held = ProjectedNode.Of(ui.Latest().Value);
        Assert.Equal(before, held.Field("clock").Field("time").AsString());
        Assert.Equal(40, held.Field("party").Field("hitPoints").AsNumber());

        // Released, the same wait is applied as it always is.
        session.ReleaseHold();
        session.Update(RulesetTestContext.Update(step, 1, RulesetTestContext.Digital(Declared.WaitAnHourIntent)));
        ProjectedNode waited = ProjectedNode.Of(ui.Latest().Value);
        Assert.Equal("wait-hour", waited.Field("rest").Field("kind").AsString());
        Assert.Equal("applied", waited.Field("rest").Field("outcome").AsString());
        Assert.NotEqual(before, waited.Field("clock").Field("time").AsString());
    }

    [Fact]
    public void A_camp_costs_the_grounds_rations_and_refuses_where_it_may_not_be_made()
    {
        using IGameSession sand = Region(out RecordingUiService ui, terrain: "desert", encounterPercent: 0);
        sand.Update(RulesetTestContext.Update(1, 1, RulesetTestContext.Digital(Declared.CampIntent)));

        // The desert is the harshest ground the donor's table prices: five provisions, and the clock moves
        // by the night the party slept.
        ProjectedNode camped = ProjectedNode.Of(ui.Latest().Value);
        Assert.Equal("camp", camped.Field("rest").Field("kind").AsString());
        Assert.Equal("applied", camped.Field("rest").Field("outcome").AsString());
        Assert.Equal(5, camped.Field("rest").Field("charged").AsNumber());
        Assert.Equal(5, camped.Field("rest").Field("covered").AsNumber());
        Assert.Equal(28800, camped.Field("rest").Field("elapsedSeconds").AsNumber());
        Assert.Equal(1, camped.Field("party").Field("provisions").AsNumber());

        // A rest is a sleep under a roof, so a party in the open is sent to camp instead, and the clock does
        // not move for a night that was never taken.
        sand.Update(RulesetTestContext.Update(2, 1, RulesetTestContext.Digital(Declared.RestIntent)));
        ProjectedNode open = ProjectedNode.Of(ui.Latest().Value).Field("rest");
        Assert.Equal("refused", open.Field("outcome").AsString());
        Assert.Equal("rest-in-the-open", open.Field("code").AsString());

        // With a creature standing a hundred units away the party will not lie down at all: the camp is
        // refused, nothing is spent, and the clock still stands where it did.
        using IGameSession hostile = Region(out RecordingUiService hostileUi, terrain: "grass", encounterPercent: 0, hostileAt: 100);
        hostile.Update(RulesetTestContext.Update(1, 1, RulesetTestContext.Digital(Declared.CampIntent)));
        ProjectedNode wary = ProjectedNode.Of(hostileUi.Latest().Value);
        Assert.Equal("camp-hostiles-near", wary.Field("rest").Field("code").AsString());
        Assert.Equal("09:00", wary.Field("clock").Field("time").AsString());
        Assert.Equal(6, wary.Field("party").Field("provisions").AsNumber());
        Assert.Equal(0, wary.Field("rest").Field("charged").AsNumber());

        // Camping under a roof is refused for the same reason a rest in the open is: the two are different
        // acts, and this game says which one belongs where.
        using IGameSession roofed = Shop(out RecordingUiService roofedUi, ShopDoor);
        roofed.Update(RulesetTestContext.Update(1, 1, RulesetTestContext.Digital(Declared.CampIntent)));
        Assert.Equal("camp-under-a-roof", ProjectedNode.Of(roofedUi.Latest().Value).Field("rest").Field("code").AsString());
    }

    [Fact]
    public void A_broken_camp_lasts_only_the_hours_it_lasted_and_costs_nothing()
    {
        // A place where something always wanders in: the encounter chance is the place's own content, and
        // the roll is the engine's keyed draw, which this suite's own random service answers with the top of
        // the range — so the night is broken, deterministically.
        using IGameSession session = Region(out RecordingUiService ui, terrain: "grass", encounterPercent: 100);
        session.Update(RulesetTestContext.Update(1, 1, RulesetTestContext.Digital(Declared.CampIntent)));

        ProjectedNode broken = ProjectedNode.Of(ui.Latest().Value);
        Assert.Equal("applied", broken.Field("rest").Field("outcome").AsString());
        Assert.True(broken.Field("rest").Field("interrupted").AsBoolean());
        Assert.False(broken.Field("rest").Field("recovered").AsBoolean());
        Assert.Equal(0, broken.Field("rest").Field("restored").AsNumber());
        Assert.Equal(0, broken.Field("rest").Field("charged").AsNumber());

        // The donor's own consequence: an hour and up to six minutes of the night, no provisions taken, and
        // nothing restored, which is what the panel reports.
        Assert.Equal(3900, broken.Field("rest").Field("elapsedSeconds").AsNumber());
        Assert.Equal("1168-01-01 10:05", broken.Field("rest").Field("to").AsString());
        Assert.Equal(6, broken.Field("party").Field("provisions").AsNumber());
        Assert.Contains("break it", broken.Field("rest").Field("message").AsString(), StringComparison.Ordinal);
        Assert.Contains("Nothing was restored and no provisions were spent", broken.Field("rest").Field("message").AsString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Going_without_sleep_weakens_the_party_on_the_clocks_own_deadline_and_a_night_clears_it()
    {
        using IGameSession session = Shop(out RecordingUiService ui, ShopDoor);

        // A day of game time in one update: the debt of sleep the session registered when it began falls due
        // at the clock's own deadline, and every member carries the state this game names for it.
        session.Update(RulesetTestContext.Update(1, 2880, 1.0));
        ProjectedNode tired = ProjectedNode.Of(ui.Latest().Value);
        Assert.Equal("1168-01-02", tired.Field("clock").Field("date").AsString());
        Assert.Equal("09:00", tired.Field("clock").Field("time").AsString());
        Assert.Equal("weak (1)", tired.Field("party").Field("conditions").AsString());
        Assert.True(tired.Field("rest").Field("tired").AsBoolean());
        Assert.Equal(1, tired.Field("rest").Field("fatigueLanded").AsNumber());
        Assert.Equal("1168-01-03 09:00", tired.Field("rest").Field("fatigueDue").AsString());

        // A night's sleep pays the debt and clears the state, and the next one is due a day after the party
        // wakes rather than a day after the debt landed.
        session.Update(RulesetTestContext.Update(2, 1, RulesetTestContext.Digital(Declared.RestIntent)));
        ProjectedNode rested = ProjectedNode.Of(ui.Latest().Value);
        Assert.False(rested.Field("rest").Field("tired").AsBoolean());
        Assert.Equal(string.Empty, rested.Field("party").Field("conditions").AsString());
        Assert.Equal("weak", rested.Field("rest").Field("cleared").AsString());
        Assert.Equal("1168-01-03 17:00", rested.Field("rest").Field("fatigueDue").AsString());
        Assert.Equal(1, rested.Field("rest").Field("fatigueLanded").AsNumber());
    }

    [Fact]
    public void A_places_hours_come_from_the_counters_in_it_and_from_its_own_entry()
    {
        // Two counters in one place that keep different hours: no one window says when that place's doors
        // are locked, so the place keeps none and the read says so rather than guessing one of the two.
        ContentCatalog catalog = Hosted(Places(
            """
            { "id": "7", "kind": "interior", "name": "The Market Hall", "respawnDays": 7,
              "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
              "placements": [
                { "id": "sword-and-shield", "kind": "service", "x": 100, "y": 0, "z": 0 },
                { "id": "the-lamp", "kind": "service", "x": -100, "y": 0, "z": 0 } ] }
            """));
        PlaceGraph graph = PlaceGraphLoader.Load(catalog, MightAndMagic7FareDays.Read(catalog));
        MightAndMagic7Schedules schedules = MightAndMagic7Schedules.Read(catalog, graph, MightAndMagic7Services.Read(catalog));

        Assert.Null(schedules.HoursOf(new PlaceId("7")));
        Assert.Empty(schedules.Schedule.StateOf(new PlaceId("7"), new GameDate(1168, 1, 1, 12, 0, 0)));
        string note = Assert.Single(schedules.Notes);
        Assert.Contains("'7'", note, StringComparison.Ordinal);

        // A place that states its own hours is clocked by them, which is how a town — a region holding no
        // counter at all — can keep hours of its own.
        ContentCatalog stated = Hosted(Places(
            """
            { "id": "9", "kind": "region", "name": "Harmondale", "respawnDays": 7, "openHour": 8, "closedHour": 20,
              "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ] }
            """));
        MightAndMagic7Schedules read = MightAndMagic7Schedules.Read(stated, PlaceGraphLoader.Load(stated, MightAndMagic7FareDays.Read(stated)), services: null);
        Assert.Equal("08:00–20:00", read.DescribeHours(new PlaceId("9")));
        Assert.True(read.IsOpenAt(new PlaceId("9"), new GameDate(1168, 1, 1, 12, 0, 0)));
        Assert.False(read.IsOpenAt(new PlaceId("9"), new GameDate(1168, 1, 1, 21, 0, 0)));

        // A place that states half a window is a content defect named while the world is built, rather than a
        // door that quietly never locks.
        ContentCatalog incomplete = Hosted(Places(
            """{ "id": "9", "kind": "region", "name": "Harmondale", "respawnDays": 7, "openHour": 8 }"""));
        ContentValidationException error = Assert.Throws<ContentValidationException>(
            () => MightAndMagic7Schedules.Read(incomplete, PlaceGraphLoader.Load(incomplete, MightAndMagic7FareDays.Read(incomplete)), services: null));
        Assert.Contains(error.Issues, issue => issue.Code == "place-hours-incomplete");
    }

    /// <summary>The shop, its party, and the clock a case drives: a place that keeps 6 to 18, and what stands in it.</summary>
    /// <param name="ui">The UI service the session publishes to.</param>
    /// <param name="placements">What the place holds, in content order.</param>
    private static IGameSession Shop(out RecordingUiService ui, string placements)
    {
        (ProductCreateContext context, RecordingUiService service) = RulesetTestContext.Create(Content(
            places:
            $$"""
            { "id": "7", "kind": "interior", "name": "The Sword and Shield", "respawnDays": 672,
              "openHour": 6, "closedHour": 18, "encounterPercent": 0,
              "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
              "placements": [ {{placements}} ] }
            """,
            start: "7",
            services: null));
        ui = service;
        IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            RulesetTestContext.RulesetContext(context, ui) with { Use = UseControls, Rest = RestControls });
        session.Start();
        return session;
    }

    /// <summary>The door the shop's hours lock, and the chest the party can set a trap off in.</summary>
    private const string ShopDoor = """{ "id": "door-0", "kind": "door", "x": 100, "y": 0, "z": 0, "state": 2 }""";

    /// <summary>The trapped chest a rest has something to heal: no sense for traps, and dice that always land.</summary>
    private const string ShopChest =
        """{ "id": "container-0", "kind": "container", "x": 100, "y": 0, "z": 0, "flags": 1, "trapDifficulty": 5, "trapDamageDice": 1 }""";

    /// <summary>A region the party camps in, with the ground and the risk a case states.</summary>
    private static IGameSession Region(
        out RecordingUiService ui,
        string terrain,
        int encounterPercent,
        double? hostileAt = null)
    {
        // A creature is a placement of the creature kind naming its own monster row, which is the shape the
        // importer emits from a level's spawn records: what keeps a party from camping is a creature standing
        // there, not the spawn record it came from.
        string spawn = hostileAt is { } at
            ? $$""", { "id": "monster-0", "kind": "monster", "monster": "7", "monsterName": "A beast", "x": {{at}}, "y": 0, "z": 0 }"""
            : string.Empty;
        (ProductCreateContext context, RecordingUiService service) = RulesetTestContext.Create(Content(
            places:
            $$"""
            { "id": "9", "kind": "region", "name": "The Bracada Desert", "respawnDays": 672,
              "terrain": "{{terrain}}", "encounterPercent": {{encounterPercent}},
              "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
              "placements": [ { "id": "light-0", "kind": "light", "x": -60, "y": 0, "z": 64 }{{spawn}} ] }
            """,
            start: "9",
            services: null));
        ui = service;
        IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            RulesetTestContext.RulesetContext(context, ui) with { Use = UseControls, Rest = RestControls });
        session.Start();
        return session;
    }

    /// <summary>The shop's content, under a bundle that also names a tuning pack stating the night's length.</summary>
    private static (string Path, string Text)[] Tuned(int sleepHours, string placements) =>
    [
        .. Content(
            places:
            $$"""
            { "id": "7", "kind": "interior", "name": "The Sword and Shield", "respawnDays": 672,
              "encounterPercent": 0,
              "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
              "placements": [ {{placements}} ] }
            """,
            start: "7",
            services: null).Skip(1),
        ($"{RulesetTestContext.ContentDirectory}/bundles/partyrpg-default/bundle.json",
            """
            {
              "schemaVersion": 1,
              "bundleId": "partyrpg-default",
              "ruleset": "mightandmagic7",
              "contentPacks": [ "world" ],
              "tuningPack": "tuning",
              "description": "test bundle"
            }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/tuning/pack.json",
            """
            {
              "schemaVersion": 1,
              "packId": "tuning",
              "kind": "tuning",
              "provenance": { "description": "authored for a test" },
              "documents": [ { "path": "tuning.json", "documentId": "tuning", "definitionKind": "tuning" } ]
            }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/tuning/tuning.json",
            $$"""{ "documentId": "tuning", "definitionKind": "tuning", "entries": [ { "id": "{{MightAndMagic7Tuning.SleepHours.Id}}", "value": {{sleepHours}} } ] }"""),
    ];

    /// <summary>A bundle with one world pack holding the places a case declares.</summary>
    private static (string Path, string Text)[] Content(string places, string start, string? services) =>
    [
        RulesetTestContext.Bundle("partyrpg-default", "world"),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/pack.json",
            $$"""
            {
              "schemaVersion": 1,
              "packId": "world",
              "kind": "definitions",
              "provenance": { "description": "authored for a test" },
              "documents": [
                { "path": "places.json", "documentId": "places", "definitionKind": "place" },
                { "path": "start.json", "documentId": "start", "definitionKind": "scenario-start" },
                { "path": "party.json", "documentId": "party", "definitionKind": "scenario-party" },
                { "path": "monsters.json", "documentId": "monsters", "definitionKind": "monster" }
                {{(services is null ? string.Empty : ", { \"path\": \"services.json\", \"documentId\": \"services\", \"definitionKind\": \"service\" }")}}
              ]
            }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/places.json", Places(places)),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/monsters.json",
            $$"""
            {
              "documentId": "monsters",
              "definitionKind": "monster",
              "entries": [
                { "id": "7", "name": "A beast", "level": 2, "hitPoints": 40, "armorClass": 5,
                  "hostility": 2, "recovery": 100, "speed": 140, "movement": "Long", "aiType": "Normal",
                  {{MonsterRows.Combat(7, "Phys", "2D8+10")}} }
              ]
            }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/start.json",
            $$"""
            { "documentId": "start", "definitionKind": "scenario-start", "entries": [ { "id": "start", "place": "{{start}}", "entryPoint": "Party Start" } ] }
            """),
        ($"{RulesetTestContext.ContentDirectory}/content-packs/world/party.json",
            """
            {
              "documentId": "party",
              "definitionKind": "scenario-party",
              "entries": [
                { "id": "party", "coins": 200, "food": 6, "reputation": 0, "fame": 0,
                  "members": [
                    { "name": "Roderick", "race": "Human", "class": "Knight", "level": 1,
                      "hitPoints": 40, "spellPoints": 0,
                      "attributes": [ { "id": "Might", "value": 13 } ],
                      "skills": [], "spells": [], "conditions": [] } ] }
              ]
            }
            """),
        .. services is null
            ? Array.Empty<(string Path, string Text)>()
            : [(($"{RulesetTestContext.ContentDirectory}/content-packs/world/services.json"), services)],
    ];

    /// <summary>One document of places, as a case declares them.</summary>
    private static string Places(string places) =>
        $$"""{ "documentId": "places", "definitionKind": "place", "entries": [ {{places}} ] }""";

    /// <summary>A catalog a case reads directly, over the places it declares.</summary>
    private static ContentCatalog Hosted(string places) =>
        ContentCatalogLoader.Load(
            new InMemoryContentSource()
                .Add(
                    "packs/world/pack.json",
                    """
                    {
                      "schemaVersion": 1,
                      "packId": "world",
                      "kind": "definitions",
                      "provenance": { "description": "test content" },
                      "documents": [
                        { "path": "places.json", "documentId": "places", "definitionKind": "place" },
                        { "path": "services.json", "documentId": "services", "definitionKind": "service" }
                      ]
                    }
                    """)
                .Add("packs/world/places.json", places)
                .Add(
                    "packs/world/services.json",
                    """
                    {
                      "documentId": "services",
                      "definitionKind": "service",
                      "entries": [
                        { "id": "sword-and-shield", "kind": "Weapon Shop", "name": "The Sword and Shield",
                          "operations": [ "buy", "sell" ], "openHour": 6, "closedHour": 18 },
                        { "id": "the-lamp", "kind": "Tavern", "name": "The Lamp",
                          "operations": [ "buy" ], "openHour": 18, "closedHour": 6 }
                      ]
                    }
                    """),
            new ContentLayout("packs", "imports", "bundles")).RequireValid();

}
