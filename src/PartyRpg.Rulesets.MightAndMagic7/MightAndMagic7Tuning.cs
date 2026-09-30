using PartyRpg.Kit.Content;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// The values this game lets a tuning pack adjust, each with its default, its range, and what it decides.
/// </summary>
/// <remarks>
/// <para>
/// These are the numbers this game chose where no table states one, and the balance knobs a retune would
/// reach for first: what an errand pays, what a bounty pays per level, what a lesson and a passage cost before
/// a counter's own multiplier, how many lines a shelf holds, how long a night lasts and what a roof costs in
/// provisions. The defaults are the values these rules played with before tuning existed; where a default is
/// the donor's own number the handle says so.
/// </para>
/// <para>
/// A bundle names a tuning pack; the pack states entries of kind <c>tuning</c>, one per handle it changes, and a
/// handle it does not state keeps its default. Every rule that reads one of these reads it from the profile of
/// the catalog it is composed over, so a changed value needs no rebuild.
/// </para>
/// </remarks>
internal static class MightAndMagic7Tuning
{
    /// <summary>What one errand pays in experience (ours: the donor's quests pay from event programs this build does not run).</summary>
    internal static readonly TuningHandle ErrandExperience = new("errand.experience", 4000, 0, 1_000_000, "the experience one errand pays");

    /// <summary>What one errand pays in coin (ours, for the same reason).</summary>
    internal static readonly TuningHandle ErrandCoins = new("errand.coins", 250, 0, 1_000_000, "the coin one errand pays");

    /// <summary>What a bounty pays per level of its beast (the donor's hundred, OpenEnroth src/GUI/UI/Houses/TownHall.cpp:143-176).</summary>
    internal static readonly TuningHandle BountyPerLevel = new("bounty.per-level", 100, 0, 100_000, "the coin a bounty pays per level of its beast");

    /// <summary>A lesson's base price before the counter's multiplier (the donor's five hundred, src/Engine/PriceCalculator.cpp:150-160).</summary>
    internal static readonly TuningHandle LessonBasePrice = new("lesson.base-price", 500, 0, 1_000_000, "a lesson's price before the counter's multiplier");

    /// <summary>How many lines a shop's shelves hold (ours: the donor's shop floor shows twelve slots).</summary>
    internal static readonly TuningHandle ShopStockLines = new("shop.stock-lines", 8, 1, 64, "how many lines a shop's shelves hold");

    /// <summary>What a coach seat costs before the stable's multiplier.</summary>
    internal static readonly TuningHandle CoachFare = new("fare.coach", 25, 0, 100_000, "a coach seat's price before the stable's multiplier");

    /// <summary>What a berth costs before the dock's multiplier.</summary>
    internal static readonly TuningHandle BoatFare = new("fare.boat", 50, 0, 100_000, "a berth's price before the dock's multiplier");

    /// <summary>How long a night's sleep lasts, in hours (the manual's eight, docs/research/mm7-manual-outline.md p.24).</summary>
    internal static readonly TuningHandle SleepHours = new("rest.sleep-hours", 8, 1, 24, "how many hours a night's sleep lasts");

    /// <summary>What a night under a roof costs in provisions (the donor's two, src/GUI/UI/UIRest.cpp:46-48).</summary>
    internal static readonly TuningHandle RoofedRestRations = new("rest.roofed-rations", 2, 0, 100, "the provisions a night under a roof costs");

    /// <summary>Every value this game lets a tuning pack adjust.</summary>
    internal static readonly IReadOnlyList<TuningHandle> Handles =
    [
        ErrandExperience, ErrandCoins, BountyPerLevel, LessonBasePrice, ShopStockLines, CoachFare, BoatFare, SleepHours, RoofedRestRations,
    ];

    /// <summary>The values the selected content states, with every other handle at its default.</summary>
    /// <param name="catalog">The selected content, or null for every default.</param>
    /// <exception cref="ContentValidationException">A stated value is for no handle, not a number, or out of range; every problem is named.</exception>
    internal static TuningProfile Read(ContentCatalog? catalog) => TuningProfile.Read(catalog, Handles);
}
