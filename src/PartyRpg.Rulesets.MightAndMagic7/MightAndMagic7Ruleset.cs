using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// The compiled Might and Magic VII ruleset. This is the composition point where this game's policy
/// and content are assembled over the kit's mechanisms: the session shell, the one clock and calendar
/// every duration is stated against, the party its scenario content describes, the larder policy that
/// prices a day, the travel cost every transition is quoted through, and this game's save meaning.
/// </summary>
public sealed class MightAndMagic7Ruleset : IGameRuleset
{
    /// <summary>The ruleset's stable identity.</summary>
    public static readonly RulesetId Identity = new("mightandmagic7");

    /// <summary>The compiled ruleset instance the host selects as its built-in.</summary>
    public static MightAndMagic7Ruleset Instance { get; } = new();

    /// <inheritdoc />
    public RulesetId Id => Identity;

    /// <inheritdoc />
    public string Title => "Might and Magic VII: For Blood and Honor";

    /// <inheritdoc />
    /// <remarks>
    /// The host's start decision is answered here, at this ruleset's one composition entry: a run that
    /// resumes reads the save this game's storage holds and composes a session from it, and a run that
    /// starts fresh composes a new one. A resume that finds nothing saved fails by name rather than
    /// composing a new game in its place.
    /// </remarks>
    /// <exception cref="SessionSaveException">The run resumes and there is no saved session, or the save cannot be resumed.</exception>
    public IGameSession CreateSession(RulesetSessionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new MightAndMagic7Session(
            this,
            context,
            context.Start == SessionStart.Resume ? MightAndMagic7Persistence.Load(context.Engine) : null);
    }

    /// <summary>
    /// Composes a session from the save this game's storage holds, over the same content a new session is
    /// composed from.
    /// </summary>
    /// <remarks>
    /// A resumed session is an ordinary session whose durable half came from a save: the party, where it
    /// stands, what each place remembers, and the game time that had passed are the save's, and everything
    /// transient is composed fresh. The save is judged against the content being resumed in before anything
    /// is built, so a save that does not fit this world fails with every problem named rather than producing
    /// a session that is half of one game and half of another. This is the same path the host's start switch
    /// takes through <see cref="CreateSession"/>, named for a caller that wants a resume and nothing else.
    /// </remarks>
    /// <param name="context">What the host hands the ruleset, which carries the engine and the content to resume in.</param>
    /// <returns>The resumed session.</returns>
    /// <exception cref="ArgumentNullException">The context is null.</exception>
    /// <exception cref="SessionSaveException">There is no saved session to resume, or the save cannot be resumed.</exception>
    public IGameSession ResumeSession(RulesetSessionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return CreateSession(context with { Start = SessionStart.Resume });
    }

    /// <summary>
    /// Writes a live session to this game's save slot, at the explicit boundary the session holds.
    /// </summary>
    /// <remarks>
    /// The session is saved here because this ruleset composed it and owns its save meaning. A session
    /// another ruleset composed has that ruleset's schema, so it is refused by name rather than written
    /// under a shape this game's load could not read back.
    /// </remarks>
    /// <param name="session">The session to save, which must be one this ruleset composed.</param>
    /// <returns>The document that was written.</returns>
    /// <exception cref="ArgumentNullException">The session is null.</exception>
    /// <exception cref="ArgumentException">The session was composed by another ruleset.</exception>
    /// <exception cref="InvalidOperationException">The session was composed without a save store.</exception>
    /// <exception cref="SessionSaveException">The session holds nothing a load could rebuild, or the write failed.</exception>
    public SessionSave Save(IGameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session as MightAndMagic7Session is { } own
            ? own.Save()
            : throw new ArgumentException(
                "Only a session this ruleset composed can be saved by it; another ruleset's session has its own save meaning.",
                nameof(session));
    }
}
