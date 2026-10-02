using System.Text.Json.Serialization;

namespace PartyRpg.Kit.Party;

/// <summary>The content identity of a person who can accompany the party.</summary>
public readonly record struct FollowerDefinitionId
{
    /// <summary>Names the person in content, not an Engine runtime entity.</summary>
    [JsonConstructor]
    public FollowerDefinitionId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>The authored identity.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}

/// <summary>Whether a companion occupies a hired place or accompanies the party through its story.</summary>
public enum FollowerKind
{
    /// <summary>A companion hired through the ordinary conversation flow.</summary>
    Hired,
    /// <summary>A companion admitted by content or quest state, outside the hired limit.</summary>
    Story,
}

/// <summary>One accompanying person's identity and how they joined; policy and content facts are not saved.</summary>
public sealed record PartyFollower(FollowerDefinitionId Definition, FollowerKind Kind);

/// <summary>Named ordinary refusals from the party's companion owner.</summary>
public static class FollowerCodes
{
    /// <summary>The named person is already with the party.</summary>
    public const string AlreadyJoined = "follower-already-joined";
    /// <summary>Every hired place the ruleset permits is occupied.</summary>
    public const string HiredLimit = "follower-hired-limit";
    /// <summary>The named person is not accompanying the party.</summary>
    public const string NotJoined = "follower-not-joined";
}

/// <summary>
/// The companions attached to the one party entity. They share its pack and resources and have no
/// independent inventory, purse, actor graph or clock. Content descriptions and charging policy belong
/// to the ruleset; this owner records only presence, kind, order and changes.
/// </summary>
public sealed class PartyFollowers
{
    private readonly List<PartyFollower> _all;
    private readonly int? _hiredLimit;

    /// <summary>Creates the owner from current state and the explicitly supplied hired limit.</summary>
    /// <remarks>Restoration preserves presence rather than re-judging a previously admitted companion.</remarks>
    public PartyFollowers(int? hiredLimit = null, IReadOnlyList<PartyFollower>? followers = null)
    {
        if (hiredLimit is < 0) throw new ArgumentOutOfRangeException(nameof(hiredLimit));
        _hiredLimit = hiredLimit;
        _all = followers is null ? [] : [.. followers];
        All = _all.AsReadOnly();
    }

    /// <summary>The accompanying people, in joining order.</summary>
    public IReadOnlyList<PartyFollower> All { get; }

    /// <summary>The latest change to this component.</summary>
    public long Stamp { get; private set; } = ChangeStamp.Next();

    /// <summary>How many accompanying people occupy hired places.</summary>
    public int HiredCount => _all.Count(follower => follower.Kind == FollowerKind.Hired);

    /// <summary>The accompanying person of this identity, or none.</summary>
    public PartyFollower? Find(FollowerDefinitionId definition) => _all.FirstOrDefault(follower => follower.Definition == definition);

    /// <summary>Judges presence and the hired limit without changing either.</summary>
    public Refusal? CanJoin(FollowerDefinitionId definition, FollowerKind kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(definition.Value);
        if (kind is not (FollowerKind.Hired or FollowerKind.Story)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (Find(definition) is not null)
            return new Refusal(FollowerCodes.AlreadyJoined, $"The companion '{definition}' is already with the party.");
        return kind == FollowerKind.Hired && _hiredLimit is { } limit && HiredCount >= limit
            ? new Refusal(FollowerCodes.HiredLimit, $"The party already has its {limit} hired companions; a hired place must be freed first.")
            : null;
    }

    /// <summary>Admits one person after the caller judges its content and charge.</summary>
    public Refusal? Join(FollowerDefinitionId definition, FollowerKind kind)
    {
        if (CanJoin(definition, kind) is { } refusal) return refusal;
        _all.Add(new PartyFollower(definition, kind));
        Stamp = ChangeStamp.Next();
        return null;
    }

    /// <summary>Removes a companion's presence, freeing a hired place when it occupied one.</summary>
    public Refusal? Dismiss(FollowerDefinitionId definition)
    {
        if (Find(definition) is not { } follower)
            return new Refusal(FollowerCodes.NotJoined, $"The companion '{definition}' is not with the party, so nobody leaves.");
        _all.Remove(follower);
        Stamp = ChangeStamp.Next();
        return null;
    }
}
