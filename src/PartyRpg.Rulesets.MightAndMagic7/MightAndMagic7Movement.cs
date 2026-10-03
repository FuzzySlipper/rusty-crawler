using PartyRpg.Kit.Movement;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using Rusty.Engine;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// How this game's party moves: the engine's spatial mechanisms in this game's own units and this game's
/// own body.
/// </summary>
/// <remarks>
/// <para>
/// The values here are the game's, not the engine's, because the engine's defaults are written for a
/// roughly human-scale world while this game measures a place in map units — the party is 192 units tall
/// and walks at 384 units a second. Two of them are the movement owner's data-only values and are stated
/// once, here, because nothing the importer emits carries them yet:
/// <see cref="RadiansAtZeroFacing"/> and <see cref="BodyHeight"/>, the latter through the body centre it
/// puts above the party's pose.
/// </para>
/// <para>
/// The mechanical profile — step height, floor snap distance, solver budgets, slope, gravity — is the
/// engine's own default controller configuration scaled from the body the engine wrote it for to this
/// game's party. Scaling it keeps the engine's tuning posture instead of restating every field as a
/// number nobody could check, and it stays right when the engine retunes its own defaults.
/// </para>
/// </remarks>
internal static class MightAndMagic7Movement
{
    /// <summary>The party's body height in place units (donor default).</summary>
    /// <remarks>OpenEnroth <c>src/Engine/Party.h:280</c> — "Party height, 192 by default".</remarks>
    internal const double BodyHeight = 192;

    /// <summary>The party's body radius in place units (donor default).</summary>
    /// <remarks>OpenEnroth <c>src/Engine/Party.h:282</c> — "Party radius, 37 by default".</remarks>
    internal const double BodyRadius = 37;

    /// <summary>How fast the party walks, in place units per second (donor default).</summary>
    /// <remarks>
    /// OpenEnroth <c>src/Application/GameConfig.h</c> <c>party_walk_speed</c> (384) with
    /// <c>fWalkSpeedMultiplier</c> and <c>fBackwardWalkSpeedMultiplier</c> both 1.0
    /// (<c>src/Engine/mm7_data.cpp:2289-2290</c>), so forward, backward, and strafe share one speed.
    /// </remarks>
    internal const double WalkSpeed = 384;

    /// <summary>How fast a held turn control turns the party, in place facing units per second.</summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/Party.h:285</c> states the turn speed in degrees per second and
    /// <c>src/Engine/Party.cpp:68</c> sets it to 90, which is 512 of this world's 2048 facing units per
    /// second (<c>src/Engine/Graphics/Indoor.cpp:1505</c> converts exactly that way). Turning left
    /// increases the facing (<c>src/Engine/Graphics/Outdoor.cpp:1027</c>), which is the sign the movement
    /// owner expects.
    /// </remarks>
    internal const double TurnRatePerSecond = 512;

    /// <summary>The greatest drop, in place units, that costs the party nothing.</summary>
    /// <remarks>
    /// OpenEnroth applies fall damage only past 512 units
    /// (<c>src/Engine/Graphics/Outdoor.cpp:1432</c>, <c>src/Engine/Graphics/Indoor.cpp:1477</c>).
    /// </remarks>
    internal const double FallThreshold = 512;

    /// <summary>
    /// How far above its feet the ground over a buried creature may lie and the creature still be stood on it, in
    /// place units: the whole height a region's ground can state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The maps' spawn and actor records state a height the donor does not keep outdoors: an outdoor actor below
    /// the floor level is lifted onto it on its next update (OpenEnroth <c>src/Engine/Graphics/Outdoor.cpp:1648-1649</c>),
    /// and a spawn stands its creatures at the record's own point (<c>src/Engine/Objects/Actor.cpp:4327-4328</c>). So a
    /// record at height zero under a hillside is ordinary content — Harmondale's goblin spawns sit up to a body
    /// height under its slopes, and some regions' records lie more than a thousand units under the ground
    /// (<c>docs/evidence/creature-settling.md</c>).
    /// </para>
    /// <para>
    /// A region's ground is a byte per cell times 32 (<c>src/Engine/Graphics/OutdoorTerrain.h:146</c>), so no ground
    /// stands more than 8160 units above a record at height zero; the reach is that range rounded up to the next
    /// power of two. The kit stands a creature on ground over its feet only when the engine has refused to step it
    /// where it is, so a creature standing clear is never moved by this.
    /// </para>
    /// </remarks>
    internal const double CreatureSettleReach = 8192;

    /// <summary>
    /// The engine heading, in radians, that a place facing of zero means.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A place's facing of zero points along its first ground axis and grows toward its second: the
    /// donor's view yaw is "0 is positive X, 512 (pi/2) is positive Y" (OpenEnroth
    /// <c>src/Engine/Party.h:291</c>), and the kit's axis rule lays the first ground axis on the engine's
    /// X and the negated second on its Z. The engine's controller walks a heading of zero along its own
    /// negative Z (<c>engine-spatial/src/character_controller.rs</c> <c>wish_velocity</c>), which that
    /// rule maps onto the place's second ground axis — a quarter turn from the first. So a place facing
    /// of zero is a quarter turn: the value follows from the kit's own axis rule and this game's facing
    /// convention rather than from any place data, and it is stated here because the kit cannot know
    /// either of them.
    /// </para>
    /// <para>
    /// Nothing the importer emits carries a per-place heading either, and it should not: every place in
    /// this game counts 2048 facing units to a turn and stores its facing the same way, so one value
    /// serves them all and a per-place field would be the same number written 76 times.
    /// </para>
    /// </remarks>
    internal const double RadiansAtZeroFacing = Math.PI / 2;

    /// <summary>The facing rule this game's data uses: 2048 units to a turn, as the maps store it.</summary>
    internal static readonly FacingRule Facing = new(unitsPerTurn: 2048, minimumPitch: -512, maximumPitch: 512);

    /// <summary>How a place's own coordinates and facing unit become the engine's world axes and radians.</summary>
    /// <remarks>
    /// The body centre is half the body height above the pose because the engine sweeps a capsule about
    /// its centre while a pose says where the party stands; it is derived here rather than written twice
    /// so the two values cannot drift apart.
    /// </remarks>
    internal static readonly PlaceSpace Space = PlaceSpace.HeightIsThird(Facing, RadiansAtZeroFacing, BodyHeight / 2, EyeLevel);

    /// <summary>
    /// How far above the feet the party looks from: the donor's default <c>eyeLevel</c> (OpenEnroth
    /// <c>src/Engine/Party.h:281</c>, read from <c>PartyEyeLevel</c> at <c>src/Engine/Party.cpp:64</c>). The drawn world is
    /// seen from it; a use's reach stays measured from the body centre, the interaction contract the elevated-container
    /// reading pinned (docs/evidence/elevation-reach.md), so the reticle and the eye share a heading and differ in height.
    /// </summary>
    internal const double EyeLevel = 160;

    /// <summary>
    /// How the collision scene is chunked and what size its voxels are, in place units.
    /// </summary>
    /// <remarks>
    /// Ours, not the donor's: the original collides against map faces rather than a voxel scene, so there
    /// is no donor value to take. A voxel six to a party height keeps a body's own shape representable
    /// without chunking a whole region into one collider.
    /// </remarks>
    internal static readonly SpatialSessionConfig Session = new(
        CollisionVoxelSize: BodyHeight / 6,
        CollisionChunkSize: 16,
        VoxelSurfaceMode.GreedyCubes);

    /// <summary>The vertical and terrain profile this game's party moves by.</summary>
    /// <remarks>
    /// Fall damage has no rate here on purpose. The donor's damage is proportional to each character's own
    /// maximum health (OpenEnroth <c>src/Engine/Party.cpp:1028</c>), so no single rate in this profile
    /// could be faithful: the profile measures the fall, and <see cref="Falls"/> prices it per member.
    /// </remarks>
    /// <param name="spatial">The engine service whose default controller profile this game's profile is scaled from.</param>
    internal static MovementTuning Tuning(ISpatialService spatial) =>
        new(Controller(spatial), new FallPolicy(FallThreshold, damagePerUnit: 0), flight: Flight());

    /// <summary>The ground the importer writes a region's water squares under, which this game drowns a party on.</summary>
    /// <remarks>
    /// Water here is ground, not a volume: the donor's party stands on its water squares and drowns there, and never
    /// swims (OpenEnroth <c>src/Engine/Graphics/Outdoor.cpp:1389-1395</c>), so the party's mover walks on water as on any
    /// floor and the engine's swimming mode — buoyancy and drag inside a water box the product names each step — is not
    /// asked for. It stays available should a place ever want water a party sinks into.
    /// </remarks>
    internal const string WaterSurface = "water";

    /// <summary>How often water drowns a party standing in it: thirty game seconds.</summary>
    /// <remarks>
    /// OpenEnroth sets its water timer 128 ticks ahead each time it drowns the party (<c>src/Engine/Engine.cpp:1085-1086</c>),
    /// and 128 ticks are one real second (<c>src/Core/Time/Duration.h:28</c>) of a clock that runs thirty times real time
    /// (<c>:29</c>). Faithful in length; the intervals are counted on the calendar's own boundaries rather than from
    /// the moment the party stepped in, so the first comes up to thirty seconds early or late.
    /// </remarks>
    internal static readonly GameDuration DrowningInterval = GameDuration.FromSeconds(30);

    /// <summary>What standing on this game's ground does to the party: water drowns it.</summary>
    /// <param name="party">The party whose carried effects can spare it, or null for a world without one.</param>
    /// <param name="mover">The party's mover, which says whether a flight is holding it above the water now.</param>
    internal static IGroundHazardRule Hazards(PartyEntity? party, IPartyMover? mover = null) => new Drowning(party, mover);

    /// <summary>What this game calls the effects that spare a party its water, which is what the panel reads.</summary>
    /// <remarks>Ours: the spells' and the potion's own names, written once where the shelters are read.</remarks>
    private const string WaterWalkName = "Water Walk";

    /// <inheritdoc cref="WaterWalkName"/>
    private const string WaterBreathingName = "Water Breathing";

    /// <inheritdoc cref="WaterWalkName"/>
    private const string FlyName = "Fly";

    /// <summary>How many times the walk a flying party moves: the donor's rise, sink, and running flight.</summary>
    /// <remarks>
    /// OpenEnroth sets a rise and a sink to four times the walk (<c>src/Engine/Graphics/Outdoor.cpp:1013</c>,
    /// <c>:1223</c>) and a flying party running forward or back to four times its walk (<c>:1119</c>, <c>:1160</c>).
    /// The engine's flying mode bounds every direction by one speed, so this one value is all of them.
    /// </remarks>
    internal const double FlightSpeedMultiple = 4;

    /// <summary>The height a flying party rises no higher than, in place units (donor default).</summary>
    /// <remarks>OpenEnroth <c>src/Application/GameConfig.h:214</c>, <c>max_flight_height</c>, 4000.</remarks>
    internal const double FlightCeiling = 4000;

    /// <summary>How long a flying party takes to reach its flight speed from a hover, in seconds (ours).</summary>
    internal const double FlightSpeedUpSeconds = 0.1;

    /// <summary>
    /// The flight profile: the donor's flying speed and ceiling, reached in a tenth of a second, and kept without drag.
    /// </summary>
    /// <remarks>
    /// The donor sets a flying party's speed outright each frame, so there is no donor acceleration to take; the
    /// engine's flying mode accelerates toward the speed asked for, and a tenth of a second to get there — near the
    /// donor's at once without a jolt — is ours. No drag, because the donor's flying party stops when its keys are
    /// released and the acceleration already brakes it.
    /// </remarks>
    private static FlightTuning Flight() =>
        new(
            speed: WalkSpeed * FlightSpeedMultiple,
            acceleration: WalkSpeed * FlightSpeedMultiple / FlightSpeedUpSeconds,
            drag: 0,
            ceiling: FlightCeiling);

    /// <summary>
    /// Whether this game's party may fly now: somebody standing carries a flight they can still pay for, and the party
    /// stands under the open sky.
    /// </summary>
    /// <param name="party">The party whose members carry a flight, or null for a world without one.</param>
    /// <param name="graph">The places, which say whether the party's place has a roof.</param>
    /// <param name="pose">The party's pose, which says which place it stands in now.</param>
    internal static IFlightRule FlightRule(PartyEntity? party, PlaceGraph graph, PartyPoseOwner pose) => new Flying(party, graph, pose);

    /// <summary>The engine's default controller profile, expressed for this game's party.</summary>
    /// <remarks>
    /// Lengths scale with the party's height against the body the engine's own default was written for;
    /// angles, durations, counts, and dimensionless factors do not. The speeds and the body's own shape
    /// are this game's (donor values), not scaled defaults.
    /// </remarks>
    private static CharacterControllerConfig Controller(ISpatialService spatial)
    {
        CharacterControllerConfig engine = spatial.DefaultCharacterControllerConfig();
        double scale = BodyHeight / engine.Shape.StandingHeight;
        double Length(double value) => value * scale;

        return engine with
        {
            Shape = engine.Shape with
            {
                StandingHeight = (float)BodyHeight,
                CrouchedHeight = (float)Length(engine.Shape.CrouchedHeight),
                Radius = (float)BodyRadius,
                ContactSkin = (float)Length(engine.Shape.ContactSkin),
                ClearancePadding = (float)Length(engine.Shape.ClearancePadding),
            },
            Ground = engine.Ground with
            {
                ForwardSpeed = (float)WalkSpeed,
                BackwardSpeed = (float)WalkSpeed,
                StrafeSpeed = (float)WalkSpeed,
                Acceleration = (float)Length(engine.Ground.Acceleration),
                Braking = (float)Length(engine.Ground.Braking),
                Friction = (float)Length(engine.Ground.Friction),
                StopSpeed = (float)Length(engine.Ground.StopSpeed),
            },
            Air = engine.Air with
            {
                MaximumSpeed = (float)Length(engine.Air.MaximumSpeed),
                Acceleration = (float)Length(engine.Air.Acceleration),
                Braking = (float)Length(engine.Air.Braking),
                WishSpeedCap = (float)Length(engine.Air.WishSpeedCap),
            },
            Vertical = engine.Vertical with
            {
                Gravity = (float)Length(engine.Vertical.Gravity),
                TerminalRiseSpeed = (float)Length(engine.Vertical.TerminalRiseSpeed),
                TerminalFallSpeed = (float)Length(engine.Vertical.TerminalFallSpeed),
                JumpSpeed = (float)Length(engine.Vertical.JumpSpeed),
            },
            Surface = engine.Surface with
            {
                SteepSlideAcceleration = (float)Length(engine.Surface.SteepSlideAcceleration),
                SteepSlideSpeed = (float)Length(engine.Surface.SteepSlideSpeed),
                MaximumStepHeight = (float)Length(engine.Surface.MaximumStepHeight),
                MinimumStepWidth = (float)Length(engine.Surface.MinimumStepWidth),
                FloorSnapDistance = (float)Length(engine.Surface.FloorSnapDistance),
                FloorSnapSpeedLimit = (float)Length(engine.Surface.FloorSnapSpeedLimit),
            },
            Recovery = engine.Recovery with
            {
                MaximumDistance = (float)Length(engine.Recovery.MaximumDistance),
                MaximumSpeed = (float)Length(engine.Recovery.MaximumSpeed),
                NormalNudge = (float)Length(engine.Recovery.NormalNudge),
                UnresolvedTolerance = (float)Length(engine.Recovery.UnresolvedTolerance),
            },
            Platform = engine.Platform with
            {
                CrushTolerance = (float)Length(engine.Platform.CrushTolerance),
            },
            Solver = engine.Solver with
            {
                MaximumDisplacementPerStep = (float)Length(engine.Solver.MaximumDisplacementPerStep),
            },
        };
    }

    /// <summary>What a landing past the threshold does to each member of a party, as this game prices it.</summary>
    /// <param name="party">The party whose carried effects can spare it a fall, or null for a world without one.</param>
    internal static IFallRule Falls(PartyEntity? party) => new FallDamage(party);

    /// <summary>
    /// How this game's places are navigated: artifacts projected into one grid in cubic chunks of sixteen cells
    /// and a creature steering half a tile ahead with a thousand-cell query budget. Derivation has its own
    /// 131,072-column budget, covering the imported interiors; derived edges use the actual controller's step and body.
    /// </summary>
    /// <remarks>Ours: no donor states a steering distance, and these are the values the kit used to assume.</remarks>
    internal static PlaceNavigationPolicy Navigation { get; } = new(GridId: 0, ChunkSize: 16, MaxStepCells: 0, SteeringStep: 512, SteeringBudget: 1024, MaximumCells: 131072);

    /// <summary>
    /// The donor's flight conditions: the flight runs, the place has no roof, and its caster can keep paying.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The donor flies only while its flight buff runs (OpenEnroth <c>src/Engine/Graphics/Outdoor.cpp:960-964</c>),
    /// never indoors (<c>src/Engine/Graphics/Indoor.cpp:1451</c>), and takes off or keeps flying on a key only while
    /// the caster has spell points left or the flight was cast at grand master (<c>Outdoor.cpp:1000-1004</c>,
    /// <c>:1215-1218</c>). Here the caster carries the flight and its magnitude is what it costs them, so a flight
    /// that costs nothing needs nothing in the pool. A caster the game has laid out holds nobody up.
    /// </para>
    /// <para>
    /// <b>An adaptation, stated.</b> The donor's party with an empty caster hovers until a flight key is pressed; here
    /// it comes down at the next step, under the fall rule.
    /// </para>
    /// </remarks>
    private sealed class Flying(PartyEntity? party, PlaceGraph graph, PartyPoseOwner pose) : IFlightRule
    {
        public bool MayFly
        {
            get
            {
                if (party is null || graph.Find(pose.Place) is not { Kind: not PlaceKind.Interior }) return false;
                foreach (PartyMember member in party.Members)
                {
                    if (!member.Effects.Has(SpellEffectIds.Fly) || MightAndMagic7SpellEffects.LaidOut(member)) continue;

                    if (member.Effects.MagnitudeOf(SpellEffectIds.Fly) == 0 || member.Resources.SpellPoints.Current > 0) return true;
                }

                return false;
            }
        }
    }

    /// <summary>
    /// The donor's drowning: every thirty game seconds a party stands on water, each character not spared loses a tenth
    /// of what they can take.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Faithful to the arithmetic of OpenEnroth <c>src/Engine/Engine.cpp:1083-1099</c>: a character takes
    /// <c>GetMaxHealth() * 0.1</c>, unless they carry the water-walk buff the water breathing potion raises on its
    /// drinker. The donor sets its water damage only on a water square of the terrain, never on a model's or a
    /// dungeon's fluid face (<c>src/Engine/Graphics/Outdoor.cpp:1321-1335</c>, <c>:1389-1395</c>; indoors it is cleared,
    /// <c>src/Engine/Graphics/Indoor.cpp:1708</c>), and never while the party's water walk runs (<c>Outdoor.cpp:1351-1360</c>),
    /// so this drowns only on <see cref="WaterSurface"/> and only without a water walk.
    /// </para>
    /// <para>
    /// <b>What spares whom, published.</b> The shelters this reads are the same facts it judges by: a water walk somebody
    /// standing carries spares everybody, a water breathing spares its drinker, and a flight spares everybody while it
    /// holds the party above the water — the mover reports no footing in the air, so nothing is asked of the ground.
    /// </para>
    /// <para>
    /// <b>Three adaptations, stated.</b> The donor's harm is typed as fire and so meets a character's fire resistance; here
    /// it is plain harm. The donor also spares a character wearing an item of water walking or a relic, and item
    /// enchantments do not exist in this build yet (#8513). And the donor's party cannot walk from land into water
    /// without a water walk at all — it is stopped at the edge and drowns only where it fell in — where here water is
    /// ground a party may walk into.
    /// </para>
    /// </remarks>
    private sealed class Drowning(PartyEntity? party, IPartyMover? mover) : IGroundHazardRule
    {
        public GameDuration? IntervalOn(SurfaceEffect ground) =>
            ground.Id == WaterSurface && !WalksOnWater() ? DrowningInterval : null;

        public int DamageTo(PartyMember member, SurfaceEffect ground) =>
            MightAndMagic7SpellEffects.LaidOut(member) || member.Effects.Has(SpellEffectIds.WaterBreathing)
                ? 0
                : member.Resources.HitPoints.Maximum / 10;

        public IReadOnlyList<GroundShelter> Shelters
        {
            get
            {
                if (party is null) return [];
                List<GroundShelter> shelters = [];
                bool flying = mover?.Flying == true;
                foreach (PartyMember member in party.Members)
                {
                    if (MightAndMagic7SpellEffects.LaidOut(member)) continue;
                    if (flying && member.Effects.Has(SpellEffectIds.Fly)) shelters.Add(new GroundShelter(SpellEffectIds.Fly, FlyName, member.Id, Everybody: true));
                    if (member.Effects.Has(SpellEffectIds.WaterWalk)) shelters.Add(new GroundShelter(SpellEffectIds.WaterWalk, WaterWalkName, member.Id, Everybody: true));
                    if (member.Effects.Has(SpellEffectIds.WaterBreathing))
                    {
                        shelters.Add(new GroundShelter(SpellEffectIds.WaterBreathing, WaterBreathingName, member.Id, Everybody: false));
                    }
                }

                return shelters;
            }
        }

        /// <summary>Whether somebody standing keeps the party walking over the water.</summary>
        private bool WalksOnWater() =>
            party?.Members.Any(member => member.Effects.Has(SpellEffectIds.WaterWalk) && !MightAndMagic7SpellEffects.LaidOut(member)) == true;
    }

    /// <summary>
    /// The donor's fall damage: the whole distance fallen, times a tenth of the member's maximum health, over
    /// 256 — so a fall hurts every member by the same share of what they can take.
    /// </summary>
    /// <remarks>
    /// Faithful to the arithmetic of OpenEnroth <c>src/Engine/Party.cpp:1028-1037</c> (<c>giveFallDamage</c>),
    /// called for a fall of more than 512 units (<c>src/Engine/Graphics/Outdoor.cpp:1426-1432</c>), which is
    /// this game's <see cref="FallThreshold"/>, and never while the party carries a feather fall
    /// (<c>Outdoor.cpp:1426</c>, <c>!partyHasFeatherFall</c>). Two parts are not applied: the donor spares a member
    /// wearing an item of feather falling, and item enchantments do not exist in this build yet (#8513); and it
    /// sets a recovery on each member, which is combat recovery this landing does not charge.
    /// </remarks>
    private sealed class FallDamage(PartyEntity? party) : IFallRule
    {
        public int DamageTo(PartyMember member, FallOutcome fall) =>
            party?.Effects.Has(SpellEffectIds.FeatherFall) == true
                ? 0
                : (int)(fall.Distance * (member.Resources.HitPoints.Maximum / 10)) / 256;
    }
}
