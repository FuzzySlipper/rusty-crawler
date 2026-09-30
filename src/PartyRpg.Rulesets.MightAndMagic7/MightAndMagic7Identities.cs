using System.Globalization;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.World;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// Every identity this game composes from parts, in one place: the prefixes it names things under, and the
/// identities that carry more than a name.
/// </summary>
/// <remarks>
/// <para>
/// This game names topics, party-carried records, keepers and errands by a prefix and the thing's own id, and
/// a few identities carry what they need inside them — a bounty the month it was posted in, a beacon the place
/// it stands in. Writing each shape once here, and reading it back only through the record that wrote it, is
/// what keeps two owners from spelling one identity two ways, and lets a reader see every prefix this game
/// uses side by side, so none collides with another.
/// </para>
/// <para>
/// Topic identities and party-carried records are separate namespaces, but no two prefixes here share a
/// spelling across them either: an errand's offer is a <c>quest:</c> topic and the record a finished errand
/// leaves is an <c>errand:</c> record.
/// </para>
/// </remarks>
internal static class MightAndMagic7Identities
{
    /// <summary>The prefix an authored potion effect's identity carries, so it cannot collide with a spell's.</summary>
    internal const string PotionEffectPrefix = "potion:";

    /// <summary>The prefix a promotion's own record carries on the party.</summary>
    internal const string PromotionAwardPrefix = "promotion:";

    /// <summary>
    /// The prefix a rank's own record carries, which is also the prefix of the counted deeds the ladder keeps.
    /// </summary>
    /// <remarks>
    /// The two families are told apart by what the ladder states about them rather than by the prefix: a
    /// record a rank leaves is named by the rank, and a counted deed is named by a requirement. Reading the
    /// ladder's own rows is what keeps this naming in step with the ladder that wrote them.
    /// </remarks>
    internal const string DeedPrefix = "award:";

    /// <summary>The prefix a rank's own offer carries as a topic.</summary>
    /// <remarks>
    /// A rank is offered by the person this game's ladder says gives it, and the offer is composed from the
    /// ladder — one topic per rank this person gives — so taking it hands the party to the progression owner
    /// with the rank to give.
    /// </remarks>
    internal const string PromotionTopicPrefix = "promote:";

    /// <summary>The identity prefix a building's keeper is carried under.</summary>
    /// <remarks>
    /// A counter's keeper is not an NPC row: the building table names them and the original answers them from
    /// strings inside its executable. This game gives them an identity built from the placement they stand at,
    /// so a keeper can be spoken with, offered an errand, and named as its giver without being mistaken for
    /// somebody the NPC table describes.
    /// </remarks>
    internal const string KeeperIdPrefix = "keeper:";

    /// <summary>The topic prefix an errand's own offer carries.</summary>
    /// <remarks>
    /// Hearing an errand and agreeing to it are two topics rather than one, because they are two facts — what
    /// the party was told, and what it took on — and the journal shows both.
    /// </remarks>
    internal const string QuestTopicPrefix = "quest:";

    /// <summary>The topic prefix the agreement to an errand already heard carries.</summary>
    internal const string AcceptTopicPrefix = "accept:";

    /// <summary>The topic prefix handing a finished errand back to its giver carries.</summary>
    internal const string TurnInTopicPrefix = "turn-in:";

    /// <summary>The party-carried prefix a person the party has met is recorded under.</summary>
    internal const string MetFlagPrefix = "met:";

    /// <summary>The party-carried prefix a line the party has heard is recorded under.</summary>
    internal const string HeardFlagPrefix = "heard:";

    /// <summary>The party-carried prefix a finished errand's record is written under.</summary>
    /// <remarks>
    /// The quest owner writes it when an errand is handed back, and a topic's condition and a rank's
    /// requirement name it, which is how a person's line and a promotion wait on an errand being done.
    /// </remarks>
    internal const string ErrandFlagPrefix = "errand:";

    /// <summary>The prefix a town hall's bounty errand is named under.</summary>
    internal const string BountyPrefix = "bounty:";

    /// <summary>The prefix every beacon identity starts with, which is how a set beacon is found again.</summary>
    internal const string BeaconPrefix = "spell.beacon.";
}

/// <summary>
/// A town hall's bounty errand: the counter it is taken at and the month it was posted in.
/// </summary>
/// <remarks>
/// The identity carries everything the errand needs and nothing else — the counter's placement, which is the
/// keeper the errand is taken from and the place its encounter row is read from, and the year and month, which
/// decide the beast and what it pays — so a save that records it resolves the same errand after a month has
/// turned.
/// </remarks>
/// <param name="Placement">The counter's placement identity, as the world carries it.</param>
/// <param name="Year">The year the bounty was posted in.</param>
/// <param name="Month">The month the bounty was posted in.</param>
internal readonly record struct BountyIdentity(string Placement, int Year, int Month)
{
    /// <summary>The quest identity this bounty is carried under.</summary>
    internal string Value => string.Create(
        CultureInfo.InvariantCulture,
        $"{MightAndMagic7Identities.BountyPrefix}{Placement}:{Year:D4}-{Month:D2}");

    /// <summary>The bounty a quest identity names, or null when it names none.</summary>
    /// <param name="quest">The quest identity to read.</param>
    internal static BountyIdentity? Read(string quest)
    {
        if (!quest.StartsWith(MightAndMagic7Identities.BountyPrefix, StringComparison.Ordinal)) return null;
        string[] parts = quest[MightAndMagic7Identities.BountyPrefix.Length..].Split(':');
        if (parts.Length != 2 || parts[0].Length == 0) return null;
        string[] when = parts[1].Split('-');
        return when.Length == 2 &&
            int.TryParse(when[0], NumberStyles.None, CultureInfo.InvariantCulture, out int year) &&
            int.TryParse(when[1], NumberStyles.None, CultureInfo.InvariantCulture, out int month)
                ? new BountyIdentity(parts[0], year, month)
                : null;
    }
}

/// <summary>A beacon the party has set, at the place it was set in.</summary>
/// <param name="Place">The place the beacon stands in.</param>
internal readonly record struct BeaconIdentity(PlaceId Place)
{
    /// <summary>The party record a set beacon is kept under.</summary>
    internal string Record => string.Concat(MightAndMagic7Identities.BeaconPrefix, Place.Value);

    /// <summary>The beacon a record names, or null when it is not a beacon's.</summary>
    /// <param name="record">The record's name.</param>
    internal static BeaconIdentity? Read(string record) =>
        record.StartsWith(MightAndMagic7Identities.BeaconPrefix, StringComparison.Ordinal) &&
        record.Length > MightAndMagic7Identities.BeaconPrefix.Length
            ? new BeaconIdentity(new PlaceId(record[MightAndMagic7Identities.BeaconPrefix.Length..]))
            : null;
}
