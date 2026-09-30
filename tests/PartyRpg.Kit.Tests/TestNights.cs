using System.Text.Json;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.World;
using PartyRpg.Kit.Time;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// A night's rules for cases that sleep or wait: eight hours under a roof for no provisions, nothing wanders in,
/// every night ends weakness, and going a day without sleep leaves it.
/// </summary>
internal sealed class TestNights : IRestRule
{
    internal static readonly ConditionId Weakness = new("Weak");

    public RestQuote Quote(RestRequest request) => RestQuote.Planned(GameDuration.FromHours(8), Provisions.None);

    public RestInterruption? Interrupt(RestRequest request) => null;

    public IReadOnlyList<ConditionId> RecoveredBy(RestRequest request) => [Weakness];

    public ActiveCondition Fatigue => new(Weakness, 1);

    public GameDuration SleepInterval => GameDuration.FromHours(24);
}

/// <summary>A room to sleep or wait in: an interior with nobody else in it.</summary>
internal sealed class TestRoom : IRestSite
{
    public PlaceDefinition Place { get; } = new(
        new PlaceId("inn"),
        PlaceKind.Interior,
        "the inn",
        [],
        new ContentEntry("inn", JsonDocument.Parse("""{ "id": "inn", "kind": "interior" }""").RootElement));

    public PlacePose Pose => PlacePose.Origin;

    public IReadOnlyList<PlacePopulationEntity> Population => [];
}
