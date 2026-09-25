using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// The compiled Might and Magic VII ruleset. This is the composition point where this game's policy
/// and content are assembled over the kit's mechanisms: the session shell, the one clock and calendar
/// every duration is stated against, the party its scenario content describes, the larder policy that
/// prices a day, and the travel cost every transition is quoted through.
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
    public IGameSession CreateSession(RulesetSessionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new MightAndMagic7Session(this, context);
    }
}
