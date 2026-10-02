using PartyRpg.Kit;
using System.Globalization;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using Rusty.Engine;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// This game's answers about stopping: what a night costs, where it may be taken, what breaks it, what it
/// restores, and what going without sleep does.
/// </summary>
/// <remarks>
/// <para>
/// <b>A sleep is eight hours and a wait is whatever it says.</b> The manual's rest menu offers "Rest &amp;
/// Heal 8 Hours" beside waits to dawn, an hour, and five minutes, and its card is explicit that waiting
/// passes time without healing (<c>docs/research/mm7-manual-outline.md</c> p.24 and RefCard p.2). This game
/// therefore takes the eight hours for a sleep and leaves the waits to the kit's own clock reads, and a
/// completed sleep fills both pools and clears the weakness the party carries — the donor's
/// <c>Party::restAndHeal</c> fills health and mana and resets the weak condition
/// (OpenEnroth <c>src/Engine/Party.cpp:699-757</c>).
/// </para>
/// <para>
/// <b>Sleeping under a roof and camping in the open are different acts here.</b> The donor has one rest
/// command whose food cost depends on where the party stands — two units indoors and the ground's own price
/// outdoors (<c>src/GUI/UI/UIRest.cpp:44-49</c>, and the per-terrain table in
/// <c>src/Engine/Data/TileEnumFunctions.cpp:92-110</c>). This game states that difference as two commands
/// instead: a rest is taken under a roof, and a camp is taken in the open. Both cost provisions and both
/// restore the party; what a camp adds is the ground's price, the party's refusal to lie down with hostiles
/// near, and the chance that the night is broken.
/// </para>
/// <para>
/// <b>The ground is priced from content at the party's pose.</b> A region's normalized <c>ground</c> grid
/// states the terrain underfoot, and the words use the donor's prices: one
/// unit on grass, three on snow or swamp, four on badlands, five in the desert, and two on anything else —
/// dirt, road, water, and every ground the donor's table does not name
/// (<c>src/Engine/Data/TileEnumFunctions.cpp:92-110</c>, <c>foodRequiredForTileset</c>). The donor reads
/// the tile at the party's position (<c>src/Engine/Graphics/OutdoorTerrain.cpp:102-111</c>). When finer
/// data is absent or does not cover the pose, the place's <c>terrain</c> remains the fallback; no named
/// terrain costs the donor's default of two.
/// </para>
/// <para>
/// <b>Hostiles near keep a camp from being made at all.</b> The donor refuses every rest with a living
/// hostile within 5120 units in the open and 2560 indoors
/// (<c>src/Engine/Objects/Actor.cpp:3458-3481</c>, <c>CheckActors_proximity</c>), and this game applies the
/// same distances to the place's own living spawns — the placements the level spawns creatures from, whose
/// object reference is the actor kind the donor's <c>OBJECT_Actor = 0x3</c> names
/// (<c>src/Engine/Pid.h:12</c>).
/// </para>
/// <para>
/// <b>The risk of a broken night is a keyed draw, not an unrecorded generator.</b> The donor rolls its
/// encounter chance when a rest begins and leaves the party with an hour's nap when it fires
/// (<c>src/Application/Game.cpp:1147-1170</c>). This game draws the same roll from the engine's own random
/// service, keyed by the place and the game time the sleep began at, so the roll is the engine's while the
/// night is the clock's: the same night in the same place resolves the same way however often it is
/// replayed, and nothing about the risk has to be carried in a save. A product with no random service will
/// not take the risk at all, and says so rather than sleeping soundly by accident.
/// </para>
/// <para>
/// <b>Fatigue is a day.</b> The donor counts the days played without rest and puts the weak condition on
/// every character once the count passes one, at the day boundary (<c>src/Engine/Engine.cpp:1046-1052</c>),
/// starting the session already past that line (<c>src/Engine/Party.cpp:106</c>). This game registers the
/// same debt as a deadline on the one clock, a day long, and a completed sleep pays it.
/// </para>
/// </remarks>
internal sealed class MightAndMagic7Rest : IRestRule
{
    /// <summary>The placement kind a creature standing in a place is.</summary>
    /// <remarks>
    /// The same kind a fight reads a creature from, so what keeps a party from camping is exactly what a
    /// fight can be had with: a spawn point is where a level puts a creature and not the creature itself,
    /// and the creatures emitted from those records are what stands there now.
    /// </remarks>
    internal const string CreaturePlacementKind = MightAndMagic7Combat.CreaturePlacementKind;

    /// <summary>The place-entry field that states the ground the party would camp on.</summary>
    internal const string TerrainField = "terrain";

    /// <summary>The place-entry field that states how often something wanders into the place.</summary>
    internal const string EncounterChanceField = "encounterPercent";



    /// <summary>What a camp costs when the ground states no price of its own.</summary>
    /// <remarks>OpenEnroth <c>src/Engine/Data/TileEnumFunctions.cpp:106-107</c>: the table's default is two.</remarks>
    internal const int DefaultTerrainRations = 2;

    /// <summary>How near a living creature keeps a party from lying down in the open, in place units.</summary>
    /// <remarks>OpenEnroth <c>src/Engine/Objects/Actor.cpp:3459-3461</c>: 5120 outdoors.</remarks>
    internal const double OutdoorHostileRange = 5120;

    /// <summary>How near a living creature keeps a party from lying down under a roof, in place units.</summary>
    /// <remarks>OpenEnroth <c>src/Engine/Objects/Actor.cpp:3459-3461</c>: 2560 indoors.</remarks>
    internal const double IndoorHostileRange = 2560;

    /// <summary>How many minutes past the first hour a broken night can last.</summary>
    /// <remarks>
    /// OpenEnroth <c>src/Application/Game.cpp:1165</c>: an interrupted rest lasts an hour plus up to six
    /// minutes (<c>Duration::fromHours(1) + Duration::fromMinutes(grng->random(6))</c>), which is the same
    /// hour and the same range stated here.
    /// </remarks>
    internal const int InterruptionMinutes = 6;

    /// <summary>The base hour a broken night leaves the party with.</summary>
    internal const int InterruptionHours = 1;

    /// <summary>The seed every roll of this game's nights is drawn from.</summary>
    /// <remarks>
    /// The engine's random service takes an explicit seed and reads no wall clock, so the product states one.
    /// A constant is deliberate: the draw is keyed by the place and the game time the sleep began at, so a
    /// fixed seed is what makes a night reproducible rather than arbitrary, and one game's nights are still
    /// unrelated to the next game's because the key carries the calendar.
    /// </remarks>
    internal const ulong RollSeed = 0x5C1E_7A11_5EE9_0001;

    /// <summary>The scope this game's sleeping rolls are drawn under, so they cannot collide with another owner's.</summary>
    internal const string RollScope = "mm7.rest.night";

    /// <summary>The scope the minutes a broken night lasts are drawn under.</summary>
    internal const string RollMinutesScope = "mm7.rest.broken-minutes";

    /// <summary>The ground words this game prices, and what a camp on each costs.</summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/Data/TileEnumFunctions.cpp:92-110</c>, <c>foodRequiredForTileset</c>: grass
    /// one, snow and swamp three, badlands four, desert five, and the default two that dirt, water, and
    /// cobble road all take. The words are the donor's own tileset names, lowercased for content.
    /// </remarks>
    private static readonly Dictionary<string, int> TerrainCosts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["grass"] = 1,
        ["snow"] = 3,
        ["swamp"] = 3,
        ["badlands"] = 4,
        ["desert"] = 5,
        ["dirt"] = DefaultTerrainRations,
        ["water"] = DefaultTerrainRations,
        ["road"] = DefaultTerrainRations,
        ["default"] = DefaultTerrainRations,
    };

    /// <summary>The condition hunger and sleeplessness both put on a member, as this game names it.</summary>
    /// <remarks>
    /// One condition, two causes: the manual lists weakness as what fatigue <em>or</em> hunger brings
    /// (<c>docs/research/mm7-manual-outline.md</c> p.35), and the donor clears the one condition on a night's
    /// rest and applies the same one to a starving party. The larder's rule is the other cause and owns it
    /// from its own side, so a party that sleeps on an empty larder wakes weak — which is why the mechanism
    /// settles the day after it recovers.
    /// </remarks>
    internal static readonly ConditionId Weakness = MightAndMagic7Provisions.Weakness;

    private readonly IRandomService? _random;
    private readonly Func<PlacePopulationEntity, bool> _harmless;

    private MightAndMagic7Rest(IRandomService? random, TuningProfile tuning, Func<PlacePopulationEntity, bool>? harmless, IReadOnlyDictionary<PlaceId, MightAndMagic7Ground>? grounds = null)
    {
        _random = random;
        _tuning = tuning;
        _harmless = harmless ?? (_ => false);
        _grounds = grounds ?? new Dictionary<PlaceId, MightAndMagic7Ground>();
    }

    private readonly TuningProfile _tuning;
    private readonly IReadOnlyDictionary<PlaceId, MightAndMagic7Ground> _grounds;

    /// <summary>Reads this game's rest policy, and judges the ground every place states.</summary>
    /// <remarks>
    /// A place that states a ground this game cannot price is a content defect named at composition, rather
    /// than a camp that quietly costs the default: the ground is the one number a player checks after a
    /// night, and a word nothing resolves would make it wrong without saying so.
    /// </remarks>
    /// <param name="catalog">The validated content the product loaded, when it loaded any.</param>
    /// <param name="random">
    /// The engine's random service, which a camp's risk is drawn from. Without one this game will not take
    /// the risk at all, and refuses a camp in a place where something could find the party.
    /// </param>
    /// <param name="harmless">
    /// Whether a creature is no enemy of the party right now, as the fight reads it: one that stands with the party —
    /// charmed, enslaved, controlled, called up, or stood back up — and one that neither attacks the party on sight nor
    /// has been made its enemy, such as a creature whose kind the matrix keeps friendly to it. Such a creature does not
    /// keep the party from making camp. Without one, only a creature a spell created is known to be harmless.
    /// </param>
    /// <returns>This game's answers about sleeping, camping, waiting, and going without sleep.</returns>
    /// <exception cref="ContentValidationException">A place states a ground this game cannot price.</exception>
    internal static MightAndMagic7Rest Compose(ContentCatalog? catalog, IRandomService? random, Func<PlacePopulationEntity, bool>? harmless = null)
    {
        if (catalog is null) return new MightAndMagic7Rest(random, MightAndMagic7Tuning.Read(null), harmless);
        List<ContentValidationIssue> issues = [];
        Dictionary<PlaceId, MightAndMagic7Ground> grounds = [];
        foreach ((LoadedPack pack, ContentDocument document, ContentEntry entry) in catalog.Entries(PlaceGraphLoader.PlaceDefinitionKind))
        {
            string terrain = entry.GetString(TerrainField);
            if (terrain.Length > 0 && !TerrainCosts.ContainsKey(terrain))
                issues.Add(new ContentValidationIssue(
                    "place-terrain-unknown",
                    $"place '{entry.Id}' camps on '{terrain}', which is not a ground this game prices: it knows {string.Join(", ", TerrainCosts.Keys)}.",
                    pack.PackId,
                    document.DocumentId));
            MightAndMagic7Ground? ground = MightAndMagic7Ground.Read(entry, TerrainCosts.ContainsKey, text =>
                issues.Add(new ContentValidationIssue("place-ground-invalid", $"place '{entry.Id}': {text}.", pack.PackId, document.DocumentId)));
            if (ground is not null) grounds.Add(new PlaceId(entry.Id), ground);
        }

        if (issues.Count > 0)
        {
            throw new ContentValidationException(
                $"This game's camping grounds cannot be read: {issues[0].Message}",
                issues);
        }

        return new MightAndMagic7Rest(random, MightAndMagic7Tuning.Read(catalog), harmless, grounds);
    }

    /// <inheritdoc />
    public ActiveCondition Fatigue => new(Weakness, 1);

    /// <inheritdoc />
    public GameDuration SleepInterval => GameDuration.FromHours(24);

    /// <inheritdoc />
    /// <remarks>
    /// Nobody stops in water, to sleep or to wait: the donor will not open its rest screen for a party standing on
    /// water (OpenEnroth <c>src/Application/Game.cpp:1088-1089</c>), and a stop there would be hours of drowning.
    /// </remarks>
    public Refusal? Stop(RestRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Site.Footing is { Id: MightAndMagic7Movement.WaterSurface }
            ? new Refusal(
                MightAndMagic7Codes.RestInWater,
                $"The party stands in water in {request.Site.Place.Name}: it cannot stop here until it reaches dry ground.")
            : null;
    }

    /// <inheritdoc />
    public RestQuote Quote(RestRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        PlaceDefinition place = request.Site.Place;
        bool roofed = place.Kind == PlaceKind.Interior;

        if (request.Kind == RestKind.Camp)
        {
            if (roofed)
            {
                return RestQuote.Refused(new Refusal(
                    MightAndMagic7Codes.CampUnderARoof,
                    $"The party stands under a roof in {place.Name}: it rests here, or makes camp in the open."));
            }

            double range = OutdoorHostileRange;
            int hostiles = HostilesNear(request, range);
            if (hostiles > 0)
            {
                return RestQuote.Refused(new Refusal(
                    MightAndMagic7Codes.CampHostilesNear,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"There are {hostiles} hostile creature(s) within {range:0} of the party, and it will not make camp with them near.")));
            }

            if (Chance(place) > 0 && _random is null)
            {
                return RestQuote.Refused(new Refusal(
                    MightAndMagic7Codes.CampRiskUnavailable,
                    "Something could find the party in the night here, and this product has no random service to judge the risk with, so the party will not camp."));
            }

            return RestQuote.Planned(
                GameDuration.FromHours(_tuning.Whole(MightAndMagic7Tuning.SleepHours)),
                new Provisions(Rations(place, request.Site.Pose), ProvisionUnit.Portions));
        }

        // A rest is a sleep under a roof: the party that wants to lie down in the open makes camp, where the
        // ground and the night have their say.
        if (!roofed)
        {
            return RestQuote.Refused(new Refusal(
                MightAndMagic7Codes.RestInTheOpen,
                $"The party stands in the open in {place.Name}: it makes camp here, or finds a roof."));
        }

        if (Chance(place) > 0 && _random is null)
        {
            return RestQuote.Refused(new Refusal(
                MightAndMagic7Codes.RestRiskUnavailable,
                "Something could find the party in the night here, and this product has no random service to judge the risk with, so the party will not sleep."));
        }

        return RestQuote.Planned(
            GameDuration.FromHours(_tuning.Whole(MightAndMagic7Tuning.SleepHours)),
            new Provisions(_tuning.Whole(MightAndMagic7Tuning.RoofedRestRations), ProvisionUnit.Portions));
    }

    /// <inheritdoc />
    public RestInterruption? Interrupt(RestRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        PlaceDefinition place = request.Site.Place;
        int chance = Chance(place);
        if (chance <= 0 || _random is null) return null;

        // The key is the night itself: the place, and the game time the sleep began at. A night is therefore
        // the same night however many times it is replayed, and no roll has to be recorded for a save.
        GameDate at = request.Clock.Now;
        string key = string.Create(
            CultureInfo.InvariantCulture,
            $"{place.Id}@{at.DayText}T{at.Hour:00}:{at.Minute:00}");
        long roll = _random.DrawKeyed(new KeyedRngRequest(RollSeed, RollScope, key, 1, 100)).Value;
        if (roll > chance) return null;

        long minutes = _random
            .DrawKeyed(new KeyedRngRequest(RollSeed, RollMinutesScope, key, 0, InterruptionMinutes - 1))
            .Value;
        GameDuration lasted = GameDuration.FromHours(InterruptionHours) + GameDuration.FromMinutes(minutes);
        return RestInterruption.Broke(
            "camp-interrupted",
            string.Create(
                CultureInfo.InvariantCulture,
                $"creatures find the camp in {place.Name} and break it after {Lasted(lasted)}: this build runs no combat, so what the night costs is the rest of it, not a fight."),
            lasted);
    }

    /// <inheritdoc />
    public IReadOnlyList<ConditionId> RecoveredBy(RestRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return MightAndMagic7Conditions.RestClears;
    }

    /// <inheritdoc />
    /// <remarks>OpenEnroth src/Engine/Party.cpp:713-715 skips these members before clearing conditions or filling pools.</remarks>
    public string? Unrestored(RestRequest request, PartyMember member)
    {
        foreach (ConditionId condition in new[] { MightAndMagic7Conditions.Eradicated, MightAndMagic7Conditions.Dead, MightAndMagic7Conditions.Petrified })
            if (member.Conditions.Has(condition)) return $"{condition}; sleep cannot restore this condition, so seek a temple cure";
        return null;
    }

    /// <inheritdoc />
    /// <remarks>A zombie wakes with no spell points and half the health the night filled (<see cref="MightAndMagic7Undeath"/>).</remarks>
    public void Rested(RestRequest request, PartyMember member)
    {
        ArgumentNullException.ThrowIfNull(request);
        MightAndMagic7Undeath.Rested(member);
    }

    /// <summary>How often something wanders into a place, as the place's own content states it.</summary>
    /// <remarks>
    /// The donor's encounter chance is a per-map number and its rest roll is a percentage against it
    /// (<c>src/Application/Game.cpp:1147-1149</c>), which is the same field the importer already carries for
    /// every place, so a quiet shop and a barrow full of creatures state their own risk rather than this
    /// policy guessing one.
    /// </remarks>
    private static int Chance(PlaceDefinition place) =>
        Math.Clamp(place.Source.GetInt32(EncounterChanceField) ?? 0, 0, 100);

    /// <summary>What a camp on a place's ground costs, in provisions.</summary>
    private int Rations(PlaceDefinition place, PlacePose pose)
    {
        string terrain = _grounds.GetValueOrDefault(place.Id)?.At(pose) ?? place.Source.GetString(TerrainField);
        return terrain.Length > 0 && TerrainCosts.TryGetValue(terrain, out int cost) ? cost : DefaultTerrainRations;
    }

    /// <summary>How many living creatures stand within a range of the party, in the place's own units.</summary>
    /// <remarks>
    /// <para>
    /// A creature is a placement of the creature kind, and a creature the party has brought down is not one
    /// the party has to lie down beside: its own health is where that is read from
    /// (<see cref="PartyRpg.Kit.Combat.CreatureHealth"/>), so a corpse does not keep a camp from being made
    /// and a creature nothing has wounded does. A creature whose health nothing has attached yet is alive,
    /// which is what it is.
    /// </para>
    /// <para>
    /// The donor's own check counts the actors standing near the party and refuses the rest
    /// (<c>OpenEnroth src/Engine/Objects/Actor.cpp:3458-3481</c>); this game asks the same question of the
    /// creatures its content places.
    /// </para>
    /// </remarks>
    private int HostilesNear(RestRequest request, double range)
    {
        PlacePose party = request.Site.Pose;
        double squared = range * range;
        int hostiles = 0;
        foreach (PlacePopulationEntity entity in request.Site.Population)
        {
            if (!entity.IsAlive) continue;
            if (!string.Equals(entity.Content.Kind, CreaturePlacementKind, StringComparison.Ordinal)) continue;
            if (CreatureHealth.Find(entity.Actor) is { IsDown: true }) continue;

            // A creature that stands with the party — one a spell created, and one charmed, enslaved, or controlled,
            // whatever made it an ally — is not a hostile, and neither is one the party has not angered whose kind the
            // matrix keeps friendly to it: the donor's check passes over what is friendly to the party
            // (Actor.cpp:3473-3477, where an actor counts only when it is an enemy or its relation to the party is not
            // friendly; Actor.cpp:2097-2104 reads a charm and a binding as friendly, :2122-2166 the matrix otherwise).
            if (MightAndMagic7Summons.IsSummoned(entity.Placement) || _harmless(entity)) continue;
            PlacePose at = entity.Pose;
            double x = at.X - party.X;
            double y = at.Y - party.Y;
            double z = at.Z - party.Z;
            if ((x * x) + (y * y) + (z * z) < squared) hostiles++;
        }

        return hostiles;
    }

    /// <summary>How long a broken night lasted, in the units a person reads.</summary>
    private static string Lasted(GameDuration lasted)
    {
        long minutes = lasted.Milliseconds / (GameDuration.MillisecondsPerSecond * GameDuration.SecondsPerMinute);
        long hours = minutes / GameDuration.MinutesPerHour;
        long rest = minutes % GameDuration.MinutesPerHour;
        return rest == 0 ? $"{hours} hour(s)" : $"{hours} hour(s) {rest} minute(s)";
    }
}
