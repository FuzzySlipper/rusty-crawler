using Rusty.Engine;

namespace PartyRpg.Kit.Movement;

/// <summary>
/// Everything a party's vertical and terrain behaviour is tuned by.
/// </summary>
/// <remarks>
/// The mechanical half of vertical policy — how tall a step the party will take, how steep a slope it
/// will stand on, how high it jumps, how hard gravity pulls, how fast it walks — is the engine's own
/// controller configuration, carried here unchanged and in the engine's units. The half the engine
/// leaves to the product is here too: what a fall costs, and what a kind of ground does to the party's
/// motion. Both are values a ruleset tunes; neither is a constant in a call site, and neither is a local
/// physics rule that would disagree with the controller doing the collision.
/// </remarks>
public sealed class MovementTuning
{
    private readonly Dictionary<string, SurfaceEffect> _surfaces;

    /// <summary>Creates a tuning profile.</summary>
    /// <param name="controller">The engine controller configuration the party is moved with.</param>
    /// <param name="falls">What a fall costs the party past the height this profile allows.</param>
    /// <param name="surfaces">
    /// The surfaces this profile knows and what each does to the party's motion. A surface that is not
    /// listed is ordinary ground, so a place may name a surface before the tuning prices it and nothing
    /// stops moving; it simply moves normally.
    /// </param>
    /// <exception cref="ArgumentNullException">The controller configuration is missing.</exception>
    /// <exception cref="ArgumentException">Two surfaces share a name, or one has no name.</exception>
    public MovementTuning(CharacterControllerConfig controller, FallPolicy falls, IEnumerable<SurfaceEffect>? surfaces = null)
    {
        Controller = controller;
        Falls = falls;
        _surfaces = [];
        if (surfaces is null) return;
        foreach (SurfaceEffect surface in surfaces)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(surface.Id);
            if (!_surfaces.TryAdd(surface.Id, surface))
            {
                throw new ArgumentException(
                    $"Two surface effects are named '{surface.Id}', so which one applies to that ground would depend on the order they were written.",
                    nameof(surfaces));
            }
        }
    }

    /// <summary>The engine controller configuration the party is moved with.</summary>
    public CharacterControllerConfig Controller { get; }

    /// <summary>What a fall costs the party.</summary>
    public FallPolicy Falls { get; }

    /// <summary>The surfaces this profile names, in no particular order.</summary>
    public IReadOnlyCollection<SurfaceEffect> Surfaces => _surfaces.Values;

    /// <summary>The effect of a named surface, or ordinary ground when this profile does not name it.</summary>
    /// <param name="surfaceId">The surface's name, as a classifier reported it or as a party's current ground remembers it.</param>
    public SurfaceEffect Surface(string? surfaceId) =>
        surfaceId is not null && _surfaces.TryGetValue(surfaceId, out SurfaceEffect effect)
            ? effect
            : SurfaceEffect.Ordinary;

    /// <summary>
    /// The controller configuration to step the party with while it stands on a surface.
    /// </summary>
    /// <remarks>
    /// A surface changes how fast the party may wish to move, so it is applied to the configuration the
    /// engine solves the step with rather than to the position afterwards: acceleration, braking, slope
    /// response, and step-up then all act on the speed the surface allows, and nothing has to correct a
    /// displacement the engine already resolved. Only ground speeds and the jump change; the shape,
    /// the solver budgets, and the rest of the controller are the profile's, not the ground's.
    /// </remarks>
    /// <param name="effect">The effect of the ground the party is standing on.</param>
    public CharacterControllerConfig ControllerOn(SurfaceEffect effect)
    {
        if (effect.SpeedMultiplier == 1 && effect.JumpMultiplier == 1) return Controller;
        CharacterGroundConfig ground = Controller.Ground;
        CharacterVerticalConfig vertical = Controller.Vertical;
        return Controller with
        {
            Ground = ground with
            {
                ForwardSpeed = ground.ForwardSpeed * (float)effect.SpeedMultiplier,
                BackwardSpeed = ground.BackwardSpeed * (float)effect.SpeedMultiplier,
                StrafeSpeed = ground.StrafeSpeed * (float)effect.SpeedMultiplier,
            },
            Vertical = vertical with
            {
                JumpSpeed = vertical.JumpSpeed * (float)effect.JumpMultiplier,
            },
        };
    }
}
