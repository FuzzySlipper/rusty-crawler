using System.Text;
using PartyRpg.Kit.Movement;
using Rusty.Engine;

namespace PartyRpg.Kit.Input;

/// <summary>
/// The movement controls a product declares, by the intent name each one arrives on.
/// </summary>
/// <remarks>
/// Names are data rather than vocabulary: the kit claims exactly what a product declares, so a product
/// that declares no movement has no movement instead of an invented key. Every name here must also be
/// declared to the engine's product manifest, because the engine admits an event only on an intent its
/// manifest carries; a name that is claimed here and not declared there is a control the player can
/// never press.
/// </remarks>
public sealed record MovementIntentNames
{
    /// <summary>Creates the declared control names.</summary>
    /// <param name="forward">The intent a full forward walk arrives on.</param>
    /// <param name="back">The intent a full backward walk arrives on.</param>
    /// <param name="strafeLeft">The intent a strafe to the party's left arrives on.</param>
    /// <param name="strafeRight">The intent a strafe to the party's right arrives on.</param>
    /// <param name="turnLeft">The intent a turn to the party's left arrives on.</param>
    /// <param name="turnRight">The intent a turn to the party's right arrives on.</param>
    /// <param name="jump">The intent a jump arrives on.</param>
    /// <exception cref="ArgumentException">A control has no name, so nothing could ever claim it.</exception>
    public MovementIntentNames(
        string forward,
        string back,
        string strafeLeft,
        string strafeRight,
        string turnLeft,
        string turnRight,
        string jump)
    {
        Forward = Require(forward, nameof(forward));
        Back = Require(back, nameof(back));
        StrafeLeft = Require(strafeLeft, nameof(strafeLeft));
        StrafeRight = Require(strafeRight, nameof(strafeRight));
        TurnLeft = Require(turnLeft, nameof(turnLeft));
        TurnRight = Require(turnRight, nameof(turnRight));
        Jump = Require(jump, nameof(jump));
    }

    /// <summary>The intent a full forward walk arrives on.</summary>
    public string Forward { get; }

    /// <summary>The intent a full backward walk arrives on.</summary>
    public string Back { get; }

    /// <summary>The intent a strafe to the party's left arrives on.</summary>
    public string StrafeLeft { get; }

    /// <summary>The intent a strafe to the party's right arrives on.</summary>
    public string StrafeRight { get; }

    /// <summary>The intent a turn to the party's left arrives on.</summary>
    public string TurnLeft { get; }

    /// <summary>The intent a turn to the party's right arrives on.</summary>
    public string TurnRight { get; }

    /// <summary>The intent a jump arrives on.</summary>
    public string Jump { get; }

    private static string Require(string name, string parameterName) =>
        !string.IsNullOrWhiteSpace(name)
            ? name
            : throw new ArgumentException(
                $"The movement control '{parameterName}' declares no intent name, so no event could ever be claimed for it.",
                parameterName);
}

/// <summary>
/// What the player is asking the party to do, read from the admitted input of each update.
/// </summary>
/// <remarks>
/// <para>
/// A held key is two events — a press and a later release — so what the player holds is remembered
/// between updates and only an intent's own events change it. That memory is the whole reason this is a
/// class rather than a function: reading one update in isolation cannot tell a key that is still down
/// from a key that was never pressed.
/// </para>
/// <para>
/// An event on an intent this reader does not claim does nothing at all: the controls are exactly the
/// names a product declared, and a foreign intent is somebody else's event. A direct interface claim
/// carries neither an edge nor a release, so it asks for its control for the one update it arrives in
/// rather than leaving it held — a claim is a press with nobody to send the release.
/// </para>
/// <para>
/// The turn rate is supplied here rather than read from input because it is tuning: input says which
/// way the player is turning, and the world says how fast a turn of that kind goes. Analogue values are
/// not read yet: every declared control is digital, and a device axis would need a declared axis intent
/// and a reader that believes its value.
/// </para>
/// </remarks>
public sealed class MovementInput
{
    private readonly byte[] _forward;
    private readonly byte[] _back;
    private readonly byte[] _strafeLeft;
    private readonly byte[] _strafeRight;
    private readonly byte[] _turnLeft;
    private readonly byte[] _turnRight;
    private readonly byte[] _jump;
    private readonly double _turnRate;
    private Controls _held;

    /// <summary>Creates the reader for one product's declared movement controls.</summary>
    /// <param name="names">The intent names the controls arrive on.</param>
    /// <param name="turnRatePerSecond">
    /// How fast a held turn control turns the party, in the world's facing units per second. It is a
    /// rate rather than an amount so that the same held key turns the party the same way whatever length
    /// the admitted step happens to be.
    /// </param>
    /// <exception cref="ArgumentNullException">No control names were declared.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The turn rate is not a finite, positive rate.</exception>
    public MovementInput(MovementIntentNames names, double turnRatePerSecond)
    {
        ArgumentNullException.ThrowIfNull(names);
        if (!double.IsFinite(turnRatePerSecond) || turnRatePerSecond <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(turnRatePerSecond),
                turnRatePerSecond,
                "A turn rate must be a finite, positive rate; a party cannot be turned by a rate that is not one.");
        }

        _forward = Encoding.UTF8.GetBytes(names.Forward);
        _back = Encoding.UTF8.GetBytes(names.Back);
        _strafeLeft = Encoding.UTF8.GetBytes(names.StrafeLeft);
        _strafeRight = Encoding.UTF8.GetBytes(names.StrafeRight);
        _turnLeft = Encoding.UTF8.GetBytes(names.TurnLeft);
        _turnRight = Encoding.UTF8.GetBytes(names.TurnRight);
        _jump = Encoding.UTF8.GetBytes(names.Jump);
        _turnRate = turnRatePerSecond;
    }

    /// <summary>
    /// Reads one update's admitted input into the controls the player holds and returns the intent they
    /// ask for.
    /// </summary>
    /// <remarks>
    /// One call per admitted update, before the step that update takes: what the player pressed is
    /// settled here, and the intent that comes back governs exactly that step and no other.
    /// </remarks>
    /// <param name="input">The admitted input slice of one engine update.</param>
    /// <returns>What the player asked the party to do for the step this update takes.</returns>
    public MovementIntent Read(ReadOnlySpan<ProductInputEvent> input)
    {
        Controls claimed = Controls.None;
        bool jumpStarted = false;
        foreach (ProductInputEvent inputEvent in input)
        {
            if (inputEvent.ValueKind != InputValueKind.Digital) continue;
            Controls control = Claim(inputEvent.Intent.Span);
            if (control == Controls.None) continue;

            if (inputEvent.Edge == InputEdge.Pressed)
            {
                _held |= control;
                if (control == Controls.Jump) jumpStarted = true;
            }
            else if (inputEvent.Edge == InputEdge.Released)
            {
                _held &= ~control;
            }
            else if (inputEvent.Phase == InputPhase.DirectUi || inputEvent.Provenance == InputProvenance.DirectUi)
            {
                claimed |= control;
                if (control == Controls.Jump) jumpStarted = true;
            }
        }

        return Intent(claimed, jumpStarted);
    }

    /// <summary>Builds the intent from what is held, plus what a direct claim asked for in this update only.</summary>
    private MovementIntent Intent(Controls claimed, bool jumpStarted)
    {
        Controls held = _held | claimed;
        return new MovementIntent(
            forward: Direction(held, Controls.Forward, Controls.Back),
            strafe: Direction(held, Controls.StrafeRight, Controls.StrafeLeft),
            turnRate: Direction(held, Controls.TurnLeft, Controls.TurnRight) * _turnRate,
            jumpPressed: jumpStarted,
            jumpHeld: (held & Controls.Jump) != 0);
    }

    /// <summary>The control an event's intent names, or none when this reader does not claim it.</summary>
    private Controls Claim(ReadOnlySpan<byte> intent)
    {
        if (intent.SequenceEqual(_forward)) return Controls.Forward;
        if (intent.SequenceEqual(_back)) return Controls.Back;
        if (intent.SequenceEqual(_strafeLeft)) return Controls.StrafeLeft;
        if (intent.SequenceEqual(_strafeRight)) return Controls.StrafeRight;
        if (intent.SequenceEqual(_turnLeft)) return Controls.TurnLeft;
        if (intent.SequenceEqual(_turnRight)) return Controls.TurnRight;
        if (intent.SequenceEqual(_jump)) return Controls.Jump;
        return Controls.None;
    }

    private static double Direction(Controls held, Controls positive, Controls negative) =>
        (held.HasFlag(positive) ? 1 : 0) - (held.HasFlag(negative) ? 1 : 0);

    /// <summary>One movement control, as the flag that remembers whether the player holds it.</summary>
    [Flags]
    private enum Controls : byte
    {
        None = 0,
        Forward = 1,
        Back = 2,
        StrafeLeft = 4,
        StrafeRight = 8,
        TurnLeft = 16,
        TurnRight = 32,
        Jump = 64,
    }
}
