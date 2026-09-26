using PartyRpg.Kit.Content;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.World;
using Rusty.Engine;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// This game's save meaning: which storage a session's saves live in, and what makes a save loadable into
/// this game's world.
/// </summary>
/// <remarks>
/// <para>
/// The kit states the document, the explicit boundary, and what a save must contain to be rebuilt at all;
/// this game states where the bytes live and judges a loaded document against the content it would be
/// resumed in. There is no version here and no migration: one current schema exists, and a document that
/// does not fit this world is refused with every problem named rather than being adapted to fit it.
/// </para>
/// <para>
/// The world's own admission rule for a pose is supplied by whoever owns the places' bounds, and nothing in
/// the product supplies one yet, so a save's pose is judged by the kit's own terms — the place has to exist
/// and the pose has to be made of numbers. A bounds rule, when one lands, is passed to
/// <see cref="SessionSave.Problems"/> here and nowhere else.
/// </para>
/// </remarks>
internal static class MightAndMagic7Persistence
{
    /// <summary>
    /// The product storage scope session saves live in, resolved against the absolute persistence root the
    /// host selects before the product is created.
    /// </summary>
    /// <remarks>
    /// The scope names one durable area within that root, so the product's saves share the host's storage
    /// without inventing an absolute path of their own — where the root is belongs to the host that owns
    /// the machine's directories.
    /// </remarks>
    internal const string StoreScope = "sessions";

    /// <summary>
    /// The slot this game's session is written to and resumed from.
    /// </summary>
    /// <remarks>
    /// One slot is this game's policy for now: a save replaces the one before it, and the switch a host
    /// starts a run with reads the same slot the product writes. Named slots are a decision for the stone
    /// that offers a player more than one, and nothing here pretends to choose between saves that do not
    /// exist.
    /// </remarks>
    internal const string SaveSlot = "session";

    /// <summary>The engine-backed store a session's saves are read and written through, or null without an engine.</summary>
    /// <param name="engine">The engine the host is running inside, when it is running inside one.</param>
    internal static EngineSessionSaveStore? Store(IEngineContext? engine) =>
        engine is null ? null : new EngineSessionSaveStore(engine, StoreScope);

    /// <summary>Reads the session saved in a slot.</summary>
    /// <param name="engine">The engine the host is running inside, which owns the persistence service.</param>
    /// <param name="slot">The slot to read.</param>
    /// <returns>The saved session.</returns>
    /// <exception cref="SessionSaveException">There is no persistence to read, the slot holds nothing, or the slot does not decode.</exception>
    internal static SessionSave Load(IEngineContext? engine, string slot = SaveSlot)
    {
        if (engine is null)
        {
            throw new SessionSaveException(
                "No session can be resumed: the host is not running inside an engine, so there is no persistence to read a save from.");
        }

        using EngineSessionSaveStore store = new(engine, StoreScope);
        return new SessionSaveBoundary(store, slot).Load()
            ?? throw new SessionSaveException($"No session is saved in slot '{slot}', so there is nothing to resume.");
    }

    /// <summary>
    /// Refuses a save this world cannot resume, naming every problem at once.
    /// </summary>
    /// <remarks>
    /// The whole document is judged before any of it is rebuilt, so a defective save leaves no half-composed
    /// session behind: the graph, the party's own rules of rebuildability, and where the party stands are all
    /// checked here, and what a caller then builds is known to fit.
    /// </remarks>
    /// <param name="save">The document that would be resumed.</param>
    /// <param name="places">The world's places, which the save's recorded places and pose must belong to.</param>
    /// <param name="content">The validated content the rules of rebuildability are read over, or null when none loaded.</param>
    /// <exception cref="SessionSaveException">The save cannot be resumed; the message names every problem found.</exception>
    internal static void RequireLoadable(SessionSave save, PlaceGraph places, ContentCatalog? content)
    {
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(places);
        IReadOnlyList<string> problems = save.Problems(places, MightAndMagic7Party.Factory(content));
        if (problems.Count > 0)
        {
            throw new SessionSaveException(
                $"The save cannot be loaded: {string.Join("; ", problems)}.",
                problems);
        }
    }
}
