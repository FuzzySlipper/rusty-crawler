using PartyRpg.Kit.Content;
using PartyRpg.Kit.World;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// Per-place runtime state: what a visit leaves behind, what a reset is allowed to touch, and what
/// makes a population come back.
/// </summary>
public sealed class PlaceStateTests
{
    private static readonly ContentLayout Layout = new("packs", "imports", "bundles");

    /// <summary>A region that resets after three game days.</summary>
    private static readonly PlaceId Home = new("1");

    /// <summary>An interior that resets after ten.</summary>
    private static readonly PlaceId Cave = new("2");

    /// <summary>An interior whose content states no interval at all.</summary>
    private static readonly PlaceId Crypt = new("3");

    /// <summary>An interior whose content states a zero-day interval.</summary>
    private static readonly PlaceId Arena = new("4");

    [Fact]
    public void A_place_keeps_its_state_while_the_party_is_somewhere_else()
    {
        PlaceStateLedger ledger = Ledger();
        ledger.MarkVisited(Home);
        ledger.MarkCleared(Home);
        ledger.MarkVisited(Cave);

        // The ledger holds a place's state whether or not the party is standing in it: where the party
        // is now is the session's business, and it never deletes what another place remembers.
        PlaceState home = ledger.StateOf(Home);
        Assert.True(home.Visited);
        Assert.True(home.Cleared);
        Assert.True(ledger.StateOf(Cave).Visited);
        Assert.False(ledger.StateOf(Cave).Cleared);
        Assert.Equal(2, ledger.States.Count);
    }

    [Fact]
    public void Clearing_a_place_survives_leaving_it_and_coming_back()
    {
        PlaceStateLedger ledger = Ledger();
        ledger.MarkVisited(Home);
        ledger.MarkCleared(Home);

        ledger.AdvanceTo(2); // two of the place's three days pass while the party is elsewhere
        ledger.MarkVisited(Home); // and the party returns

        Assert.True(ledger.StateOf(Home).Cleared);
        Assert.Equal(0, ledger.StateOf(Home).RespawnCount);
    }

    [Fact]
    public void Respawn_fires_from_elapsed_game_time_and_not_from_the_number_of_visits()
    {
        PlaceStateLedger ledger = Ledger();
        ledger.MarkVisited(Home);
        ledger.MarkCleared(Home);

        // Ten visits inside one day leave the place exactly as cleared as one visit did.
        for (int visit = 0; visit < 10; visit++) ledger.MarkVisited(Home);
        Assert.True(ledger.StateOf(Home).Cleared);
        Assert.Equal(0, ledger.StateOf(Home).RespawnCount);

        // One advance to the day its interval reaches restores it, with no visit involved at all.
        PlaceState restored = Assert.Single(ledger.AdvanceTo(3));
        Assert.Equal(Home, restored.Place);
        Assert.False(ledger.StateOf(Home).Cleared);
        Assert.Equal(1, ledger.StateOf(Home).RespawnCount);
    }

    [Fact]
    public void Resetting_a_place_restores_its_population_and_never_erases_party_knowledge()
    {
        PlaceStateLedger ledger = Ledger();
        ledger.MarkVisited(Home);
        ledger.MarkCleared(Home);
        ledger.MarkDiscovered(Crypt);

        Assert.Single(ledger.AdvanceTo(3));

        PlaceState home = ledger.StateOf(Home);
        Assert.False(home.Cleared); // the population is back
        Assert.Equal(1, home.RespawnCount);
        Assert.Equal(3, home.LastResetDay);
        Assert.True(home.Visited); // and everything the party learned outlives the reset
        Assert.True(home.Discovered);

        PlaceState crypt = ledger.StateOf(Crypt);
        Assert.True(crypt.Discovered);
        Assert.False(crypt.Visited);
    }

    [Fact]
    public void A_place_the_party_has_never_entered_is_on_no_schedule()
    {
        PlaceStateLedger ledger = Ledger();
        ledger.MarkDiscovered(Cave);

        Assert.Empty(ledger.AdvanceTo(500));

        PlaceState cave = ledger.StateOf(Cave);
        Assert.Null(cave.LastResetDay);
        Assert.True(cave.Discovered);
        Assert.False(cave.Visited);
    }

    [Fact]
    public void Each_place_keeps_its_own_interval_from_content()
    {
        PlaceStateLedger ledger = Ledger();
        ledger.MarkVisited(Cave); // ten days
        ledger.MarkCleared(Cave);
        ledger.MarkVisited(Home); // three days
        ledger.MarkCleared(Home);

        PlaceState first = Assert.Single(ledger.AdvanceTo(3));
        Assert.Equal(Home, first.Place);
        Assert.True(ledger.StateOf(Cave).Cleared);

        ledger.AdvanceTo(10);
        Assert.False(ledger.StateOf(Cave).Cleared);
        Assert.Equal(1, ledger.StateOf(Cave).RespawnCount);
        Assert.Equal(10, ledger.StateOf(Cave).LastResetDay);
    }

    [Fact]
    public void A_place_states_its_own_interval_and_silence_states_no_schedule()
    {
        PlaceGraph world = World();
        PlaceRespawnRule rule = PlaceRespawnRule.FromContent();

        Assert.Equal(3, rule.DaysFor(world.Require(Home)));
        Assert.Equal(10, rule.DaysFor(world.Require(Cave)));
        Assert.Null(rule.DaysFor(world.Require(Crypt)));
        Assert.Equal(0, rule.DaysFor(world.Require(Arena)));
        Assert.Equal(5, PlaceRespawnRule.FromContentOr(5).DaysFor(world.Require(Crypt)));
    }

    [Fact]
    public void An_explicit_rule_value_puts_every_place_on_one_interval()
    {
        PlaceStateLedger ledger = new(World(), PlaceRespawnRule.Every(7));
        ledger.MarkVisited(Home); // content says three days, and the rule overrides it
        ledger.MarkCleared(Home);
        ledger.MarkVisited(Crypt); // content says nothing, and the rule still supplies a schedule
        ledger.MarkCleared(Crypt);

        Assert.Empty(ledger.AdvanceTo(6));
        Assert.Equal(2, ledger.AdvanceTo(7).Count);
        Assert.False(ledger.StateOf(Home).Cleared);
        Assert.False(ledger.StateOf(Crypt).Cleared);
    }

    [Fact]
    public void A_place_whose_content_declares_no_interval_never_resets_on_its_own()
    {
        PlaceStateLedger ledger = Ledger();
        ledger.MarkVisited(Crypt);
        ledger.MarkCleared(Crypt);

        Assert.Empty(ledger.AdvanceTo(1000));

        PlaceState crypt = ledger.StateOf(Crypt);
        Assert.True(crypt.Cleared);
        Assert.Equal(0, crypt.RespawnCount);
    }

    [Fact]
    public void A_zero_day_interval_lasts_only_until_the_next_day()
    {
        PlaceStateLedger ledger = Ledger();
        ledger.MarkVisited(Arena);
        ledger.MarkCleared(Arena);

        // No time has passed, so no interval has elapsed — an update that advances nothing is not a tick.
        Assert.Empty(ledger.AdvanceTo(0));
        Assert.True(ledger.StateOf(Arena).Cleared);

        Assert.Single(ledger.AdvanceTo(1));
        Assert.False(ledger.StateOf(Arena).Cleared);
        Assert.Equal(1, ledger.StateOf(Arena).RespawnCount);

        // And the same day does not restore it a second time.
        Assert.Empty(ledger.AdvanceTo(1));
        Assert.Equal(1, ledger.StateOf(Arena).RespawnCount);
    }

    [Fact]
    public void A_long_absence_restores_a_place_once_instead_of_once_per_interval()
    {
        PlaceStateLedger ledger = Ledger();
        ledger.MarkVisited(Home);
        ledger.MarkCleared(Home);

        PlaceState restored = Assert.Single(ledger.AdvanceTo(1000));

        Assert.Equal(1, restored.RespawnCount);
        Assert.Equal(1000, restored.LastResetDay); // the next interval is measured from the reset that happened
        Assert.False(restored.Cleared);
    }

    [Fact]
    public void Game_time_never_runs_backwards()
    {
        PlaceStateLedger ledger = Ledger();
        ledger.AdvanceTo(5);

        ArgumentOutOfRangeException error = Assert.Throws<ArgumentOutOfRangeException>(() => ledger.AdvanceTo(4));

        Assert.Equal("elapsedGameDays", error.ParamName);
        Assert.Contains("day 5", error.Message);
        Assert.Equal(5, ledger.ElapsedGameDays);
    }

    [Fact]
    public void Clearing_a_place_the_party_has_never_been_in_is_refused()
    {
        PlaceStateLedger ledger = Ledger();

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => ledger.MarkCleared(Home));

        Assert.Contains("'1'", error.Message);
        Assert.Null(ledger.Find(Home));
        Assert.Equal(PlaceState.Untouched(Home), ledger.StateOf(Home));
    }

    [Fact]
    public void A_place_the_world_does_not_have_fails_by_name()
    {
        PlaceStateLedger ledger = Ledger();

        ContentValidationException error = Assert.Throws<ContentValidationException>(() => ledger.StateOf(new PlaceId("99")));

        Assert.Contains("99", error.Message);
    }

    [Fact]
    public void A_captured_ledger_round_trips_and_carries_on_its_schedule()
    {
        PlaceStateLedger ledger = Ledger();
        ledger.MarkVisited(Home);
        ledger.MarkCleared(Home);
        ledger.MarkDiscovered(Cave);
        ledger.MarkVisited(Crypt);
        ledger.MarkCleared(Crypt);
        ledger.AdvanceTo(2); // the capture lands in the middle of Home's three-day interval

        PlaceStateLedger restored = PlaceStateLedger.Restore(World(), PlaceRespawnRule.FromContent(), ledger.Capture());

        Assert.Equal(ledger.ElapsedGameDays, restored.ElapsedGameDays);
        Assert.Equal(ledger.States, restored.States);
        Assert.True(restored.StateOf(Home).Cleared);

        PlaceState home = Assert.Single(restored.AdvanceTo(3));
        Assert.Equal(Home, home.Place);
        Assert.False(restored.StateOf(Home).Cleared);
        Assert.True(restored.StateOf(Crypt).Cleared); // the place with no interval is still cleared
    }

    [Fact]
    public void A_snapshot_naming_a_place_the_world_lacks_is_refused_by_name()
    {
        PlaceStateLedgerSnapshot snapshot = new(4, [PlaceState.Untouched(new PlaceId("99"))]);

        ContentValidationException error = Assert.Throws<ContentValidationException>(
            () => PlaceStateLedger.Restore(World(), PlaceRespawnRule.FromContent(), snapshot));

        Assert.Contains("99", error.Message);
    }

    [Fact]
    public void A_place_whose_declared_interval_is_not_a_whole_day_count_is_refused_by_name()
    {
        ContentValidationException fractional = Assert.Throws<ContentValidationException>(
            () => LedgerOver("""{ "id": "1", "kind": "region", "name": "Home", "respawnDays": 2.5 }"""));
        Assert.Contains("'1'", fractional.Message);
        Assert.Contains("place-respawn-interval-invalid", string.Join(",", fractional.Issues.Select(issue => issue.Code)));

        ContentValidationException negative = Assert.Throws<ContentValidationException>(
            () => LedgerOver("""{ "id": "1", "kind": "region", "name": "Home", "respawnDays": -1 }"""));
        Assert.Contains("'1'", negative.Message);
    }

    /// <summary>A ledger over the four places the tests in this file use.</summary>
    private static PlaceStateLedger Ledger() => new(World(), PlaceRespawnRule.FromContent());

    /// <summary>A ledger over places written inline, so a content defect is what the construction refuses.</summary>
    private static PlaceStateLedger LedgerOver(params string[] places) =>
        new(Load(Places(places)), PlaceRespawnRule.FromContent());

    private static PlaceGraph World() => Load(Places(
        """{ "id": "1", "kind": "region", "name": "Home", "respawnDays": 3 }""",
        """{ "id": "2", "kind": "interior", "name": "Cave", "respawnDays": 10 }""",
        """{ "id": "3", "kind": "interior", "name": "Crypt" }""",
        """{ "id": "4", "kind": "interior", "name": "Arena", "respawnDays": 0 }"""));

    private static PlaceGraph Load(string places) =>
        PlaceGraphLoader.Load(ContentCatalogLoader.Load(
            new InMemoryContentSource()
                .Add("packs/world/pack.json", Manifest())
                .Add("packs/world/places.json", Document("places", "place", places)),
            Layout).RequireValid());

    private static string Places(params string[] entries) => string.Join(",", entries);

    private static string Manifest() =>
        """
        {
          "schemaVersion": 1,
          "packId": "world",
          "kind": "definitions",
          "provenance": { "description": "test content" },
          "documents": [
            { "path": "places.json", "documentId": "places", "definitionKind": "place" }
          ]
        }
        """;

    private static string Document(string documentId, string definitionKind, string entries) =>
        $$"""
        {
          "documentId": "{{documentId}}",
          "definitionKind": "{{definitionKind}}",
          "entries": [ {{entries}} ]
        }
        """;
}
