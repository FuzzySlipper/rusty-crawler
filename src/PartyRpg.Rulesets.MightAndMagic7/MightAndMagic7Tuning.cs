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

    /// <summary>
    /// How many game days a coach journey takes (ours: the donor's own routes run one to seven days,
    /// OpenEnroth src/GUI/UI/Houses/Transport.cpp:38-78, and this game states one length per network).
    /// </summary>
    /// <remarks>
    /// A town that keeps both a stable and a dock reaching one place is told apart by the days on the ticket, so
    /// a tuning that sets this equal to <see cref="BoatDays"/> makes such a boarding ambiguous, and the world
    /// refuses it by name rather than guessing.
    /// </remarks>
    internal static readonly TuningHandle CoachDays = new("fare.coach-days", 2, 1, 30, "how many game days a coach journey takes");

    /// <summary>How many game days a sea passage takes (ours, for the same reason).</summary>
    internal static readonly TuningHandle BoatDays = new("fare.boat-days", 3, 1, 30, "how many game days a sea passage takes");

    /// <summary>How long a night's sleep lasts, in hours (the manual's eight, docs/research/mm7-manual-outline.md p.24).</summary>
    internal static readonly TuningHandle SleepHours = new("rest.sleep-hours", 8, 1, 24, "how many hours a night's sleep lasts");

    /// <summary>What a night under a roof costs in provisions (the donor's two, src/GUI/UI/UIRest.cpp:46-48).</summary>
    internal static readonly TuningHandle RoofedRestRations = new("rest.roofed-rations", 2, 0, 100, "the provisions a night under a roof costs");

    /// <summary>Every value this game lets a tuning pack adjust.</summary>
    /// <summary>
    /// How long a temporary bonus a fixture gives lasts (ours: the donor keeps it until the party next rests,
    /// OpenEnroth src/Engine/Party.cpp:712, and this build ends a running effect on the clock, so a day — one
    /// night's rest — is the length chosen).
    /// </summary>
    internal static readonly TuningHandle FixtureBonusHours = new("fixture.bonus-hours", 24, 1, 24 * 336, "how many game hours a well's or fountain's temporary bonus lasts");

    /// <summary>
    /// How long a counter that caught the party stealing will not serve it, in hours (the donor's one day,
    /// OpenEnroth src/GUI/UI/Houses/Shops.cpp:1164).
    /// </summary>
    internal static readonly TuningHandle TheftBanHours = new("theft.ban-hours", 24, 1, 24 * 336, "how many game hours a counter that caught a thief stays shut against the party");

    /// <summary>Enchant success percent per school rank; donor ten, CastSpellInfo.cpp:1390.</summary>
    internal static readonly TuningHandle EnchantChancePerRank = new("item.enchant-chance-per-rank", 10, 0, 100, "enchant success percent per school rank");
    /// <summary>Minimum value for a weapon enchant; donor threshold, CastSpellInfo.cpp:1420.</summary>
    internal static readonly TuningHandle EnchantWeaponValue = new("item.enchant-weapon-value", 250, 0, 1_000_000, "minimum weapon value for enchantment");
    /// <summary>Minimum value for passive gear; donor threshold, same branch.</summary>
    internal static readonly TuningHandle EnchantEquipmentValue = new("item.enchant-equipment-value", 450, 0, 1_000_000, "minimum passive equipment value for enchantment");
    /// <summary>Master's minimum strength; ours, informed by the donor's explicitly guessed range.</summary>
    internal static readonly TuningHandle EnchantMasterLow = new("item.enchant-master-low", 3, 1, 1_000, "minimum strength at master, with a five-point range");
    /// <summary>Grand master's minimum strength; ours, informed by the same guessed range.</summary>
    internal static readonly TuningHandle EnchantGrandMasterLow = new("item.enchant-grandmaster-low", 6, 1, 1_000, "minimum strength at grand master, with a six-point range");
    /// <summary>Permanent ordinary-property trade premium per strength; donor standard bonus arithmetic, Item.cpp:159.</summary>
    internal static readonly TuningHandle EnchantValuePerStrength = new("item.enchant-value-per-strength", 100, 0, 100_000, "trade premium per permanent item property strength");

    /// <summary>The two hired places of the source game; story companions are outside this ceiling.</summary>
    internal static readonly TuningHandle HiredLimit = new("followers.hired-limit", 2, 0, 16, "the accompanying people who may occupy hired places");
    /// <summary>The approximate Knight bout size; donor bouts draw ten through twenty opponents.</summary>
    internal static readonly TuningHandle ArenaKnightOpponents = new("arena.knight-opponents", 10, 1, 20, "opponents in the compiled Knight bout");
    /// <summary>The donor's Knight reward per highest party level.</summary>
    internal static readonly TuningHandle ArenaKnightGold = new("arena.knight-gold-per-level", 200, 0, 1_000_000, "Knight reward per highest party level");

    /// <summary>The camera's vertical field of view in degrees (ours: the donor draws a fixed 640×480 window).</summary>
    internal static readonly TuningHandle ViewFieldOfView = new("view.field-of-view", 60, 30, 120, "the camera's vertical field of view in degrees");
    /// <summary>How far the camera sees, in place units (ours: across a whole 127-square region).</summary>
    internal static readonly TuningHandle ViewDistance = new("view.distance", 96_000, 4_000, 200_000, "how far the camera sees, in place units");
    /// <summary>The fill light that reaches every outdoor surface at noon (ours, like every light value).</summary>
    /// <summary>Where outdoor distance starts to fade into the sky's colour (ours: the donor fades only in foggy weather).</summary>
    internal static readonly TuningHandle ViewFogStart = new("view.fog-start", 24_000, 0, 200_000, "how far outdoors distance starts to fade into the sky's colour, in place units");

    /// <summary>Where outdoor distance has wholly faded; never beyond what the camera sees.</summary>
    internal static readonly TuningHandle ViewFogEnd = new("view.fog-end", 90_000, 100, 200_000, "how far outdoors distance has wholly faded, in place units");

    /// <summary>Where interior distance starts to fade into darkness.</summary>
    internal static readonly TuningHandle ViewInteriorFogStart = new("view.interior-fog-start", 3_000, 0, 200_000, "how far indoors distance starts to fade into darkness, in place units");

    /// <summary>Where interior distance has wholly faded into darkness.</summary>
    internal static readonly TuningHandle ViewInteriorFogEnd = new("view.interior-fog-end", 14_000, 100, 200_000, "how far indoors distance has wholly faded into darkness, in place units");

    internal static readonly TuningHandle LightDayAmbient = new("light.day-ambient", 0.8, 0, 4, "the outdoor fill light at full day", Whole: false);
    /// <summary>The fill light outdoors at night.</summary>
    internal static readonly TuningHandle LightNightAmbient = new("light.night-ambient", 0.25, 0, 4, "the outdoor fill light at night", Whole: false);
    /// <summary>The sun's strength at noon.</summary>
    internal static readonly TuningHandle LightSun = new("light.sun", 1.4, 0, 8, "the sun's strength at noon", Whole: false);
    /// <summary>The fill light that reaches every interior surface.</summary>
    internal static readonly TuningHandle LightInteriorAmbient = new("light.interior-ambient", 0.45, 0, 4, "the fill light indoors", Whole: false);
    /// <summary>The strength of the light the party carries indoors and at night.</summary>
    internal static readonly TuningHandle LightCarried = new("light.carried", 1.6, 0, 8, "the strength of the light the party carries", Whole: false);
    /// <summary>How far the light the party carries reaches, in place units.</summary>
    internal static readonly TuningHandle LightCarriedRange = new("light.carried-range", 2400, 100, 20_000, "how far the light the party carries reaches, in place units");

    internal static readonly IReadOnlyList<TuningHandle> Handles =
    [
        ViewFieldOfView, ViewDistance, ViewFogStart, ViewFogEnd, ViewInteriorFogStart, ViewInteriorFogEnd, LightDayAmbient, LightNightAmbient, LightSun, LightInteriorAmbient, LightCarried, LightCarriedRange,
        HiredLimit,
        ArenaKnightOpponents, ArenaKnightGold,
        EnchantChancePerRank, EnchantWeaponValue, EnchantEquipmentValue, EnchantMasterLow, EnchantGrandMasterLow, EnchantValuePerStrength,
        FixtureBonusHours, TheftBanHours,
        ErrandExperience, ErrandCoins, BountyPerLevel, LessonBasePrice, ShopStockLines, CoachFare, BoatFare, CoachDays, BoatDays, SleepHours, RoofedRestRations,
    ];


    /// <summary>The values the selected content states, with every other handle at its default.</summary>
    /// <param name="catalog">The selected content, or null for every default.</param>
    /// <exception cref="ContentValidationException">A stated value is for no handle, not a number, or out of range; every problem is named.</exception>
    internal static TuningProfile Read(ContentCatalog? catalog) => TuningProfile.Read(catalog, Handles);
}
