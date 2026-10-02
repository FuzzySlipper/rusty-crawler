using PartyRpg.Kit;
using PartyRpg.Kit.Conversation;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Magic;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>Immutable facts from the same person catalog that conversation reads.</summary>
internal sealed record MightAndMagic7FollowerFacts(string Name, string Portrait, int Profession, bool CanHire, int? HirePrice, string Join, string Dismiss);

/// <summary>
/// This game's hiring and departure policy over the party's canonical companion component and ledger.
/// Fees come from imported person/profession facts, not a parallel NPC registry. The two hired places
/// follow OpenEnroth src/GUI/UI/NPCTopics.cpp:762-814; story joins do not occupy them.
/// </summary>
internal sealed class MightAndMagic7Followers : IFoundGoldRule
{
    private readonly Func<string, MightAndMagic7FollowerFacts?> _describe;
    private readonly Func<PartyEntity?> _party;
    private readonly Func<PartyResourceLedger?> _accounts;

    internal MightAndMagic7Followers(Func<string, MightAndMagic7FollowerFacts?> describe,
        Func<PartyEntity?> party, Func<PartyResourceLedger?> accounts)
    {
        _describe = describe;
        _party = party;
        _accounts = accounts;
    }

    internal MightAndMagic7FollowerFacts? Describe(string id) => _describe(id);

    internal bool Joined(string id) => _party()?.Followers.Find(new FollowerDefinitionId(id)) is not null;

    internal Verdict HireOffer(string id)
    {
        if (_describe(id) is not { CanHire: true } facts) return Verdict.Unmet("this person does not offer to be hired");
        if (HirePrice(facts) is not { } fee) return Verdict.Unmet($"{facts.Name}'s profession has no authored hiring price");
        if (_party() is not { } party || _accounts() is not { } accounts) return Verdict.Unmet("there is no travelling party to hire a companion");
        if (party.Followers.CanJoin(new FollowerDefinitionId(id), FollowerKind.Hired) is { } refusal) return Verdict.Unmet(refusal.Message);
        return accounts.Judge(PartyCost.OfGold(fee)) is { } shortfall ? Verdict.Unmet(shortfall.Message) : Verdict.Met;
    }

    internal ConversationAnswer Hire(string id)
    {
        Verdict offered = HireOffer(id);
        if (!offered.IsMet) return new ConversationAnswer($"Nobody joins: {offered}.");
        MightAndMagic7FollowerFacts facts = _describe(id)!;
        int fee = HirePrice(facts)!.Value;
        _accounts()!.Settle(PartyCost.OfGold(fee));
        _party()!.Followers.Join(new FollowerDefinitionId(id), FollowerKind.Hired);
        return new ConversationAnswer($"{facts.Name} joins the party for {fee} gold. {facts.Join}".Trim());
    }

    internal ConversationAnswer Dismiss(string id)
    {
        if (_party() is not { } party || _describe(id) is not { } facts)
            return new ConversationAnswer("There is nobody here to dismiss.");
        return party.Followers.Dismiss(new FollowerDefinitionId(id)) is { } refusal
            ? new ConversationAnswer(refusal.Message)
            : new ConversationAnswer($"{facts.Name} leaves the party. {facts.Dismiss}".Trim());
    }

    internal Refusal? JoinStory(PartyEntity party, string id)
    {
        if (_describe(id) is null) return new Refusal("follower-unknown", $"This world declares no person '{id}' to accompany the party.");
        return party.Followers.Find(new FollowerDefinitionId(id)) is not null ? null : party.Followers.Join(new FollowerDefinitionId(id), FollowerKind.Story);
    }

    internal static Refusal? Depart(PartyEntity party, string id) => party.Followers.Dismiss(new FollowerDefinitionId(id));

    internal Refusal? CanSacrifice(PartyEntity party, string id)
    {
        if (string.IsNullOrWhiteSpace(id) || _describe(id) is null || party.Followers.Find(new FollowerDefinitionId(id)) is not { } follower)
            return new Refusal("spell-follower-absent", "Choose a hired companion who is travelling with the party.");
        return follower.Kind == FollowerKind.Story
            ? new Refusal("spell-follower-story", $"{_describe(id)!.Name} is a story companion and cannot be given up by Sacrifice.") : null;
    }

    internal IReadOnlyList<SpellAim> SacrificeAims() => _party() is { } party
        ? [.. party.Followers.All.Where(follower => follower.Kind == FollowerKind.Hired)
            .Select(follower => new SpellAim(follower.Definition.Value, _describe(follower.Definition.Value)?.Name ?? follower.Definition.Value, "hired companion"))] : [];

    internal static IReadOnlyList<SaveProblem> Problems(PartySave party, ContentCatalog? content)
    {
        HashSet<string> people = content is null ? [] : content.Entries(MightAndMagic7Conversation.PersonDefinitionKind)
            .Select(row => row.Entry.Id).ToHashSet(StringComparer.Ordinal);
        return [.. party.Followers.Where(follower => !people.Contains(follower.Definition.Value)).Select(follower =>
            new SaveProblem("save-follower-unknown", follower.Definition.Value ?? string.Empty, $"companion '{follower.Definition}' is not declared by this world's person catalog"))];
    }

    // Profession identities: OpenEnroth src/Engine/Objects/NPCEnums.h. Hiring's free burglar
    // exception is NPCTopics.cpp:762-814; finding bonuses and shares are Party.cpp:859-902.
    internal static int? HirePrice(MightAndMagic7FollowerFacts facts) => facts.Profession == 51 ? 0 : facts.HirePrice;

    public FoundGoldDivision Divide(PartyEntity party, int found)
    {
        if (found == 0) return new(0, 0);
        MightAndMagic7FollowerFacts[] companions = party.Followers.All
            .Select(follower => _describe(follower.Definition.Value)).OfType<MightAndMagic7FollowerFacts>().ToArray();
        long total = found;
        foreach ((int profession, int percent) in new[] { (31, 10), (32, 20), (45, 10) })
            if (companions.Any(facts => facts.Profession == profession)) total += total * percent / 100;
        long salary = companions.Sum(facts => (long)(facts.HirePrice ?? 0));
        long share = salary == 0 ? 0 : salary >= 10000 ? total : Math.Max(1, total * salary / 10000);
        // A pathological authored salary never creates debt or a negative purse.
        return new(checked((int)total), checked((int)Math.Min(total, share)));
    }
}
