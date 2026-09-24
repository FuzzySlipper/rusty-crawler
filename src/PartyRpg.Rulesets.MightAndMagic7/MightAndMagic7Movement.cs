using PartyRpg.Kit.Movement;
using PartyRpg.Kit.Party;
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
    internal static readonly PlaceSpace Space = PlaceSpace.HeightIsThird(Facing, RadiansAtZeroFacing, BodyHeight / 2);

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
    /// could be faithful: a fall is reported with its distance and excess, and whoever owns the party's
    /// health prices it from there.
    /// </remarks>
    /// <param name="spatial">The engine service whose default controller profile this game's profile is scaled from.</param>
    internal static MovementTuning Tuning(ISpatialService spatial) =>
        new(Controller(spatial), new FallPolicy(FallThreshold, damagePerUnit: 0));

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
}
