using PartyRpg.Kit;
using PartyRpg.Kit.Conversation;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Combat;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>Immutable facts from the same person catalog that conversation reads.</summary>
/// <summary>What joined companions add to one reading, and who adds it, in the people's own names.</summary>
internal readonly record struct FollowerContribution(int Amount, string Sources)
{
    internal static FollowerContribution None => default;
}

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

    // Profession identities and their passive terms: OpenEnroth src/Engine/Objects/NPCEnums.h:26-87 (declared
    // benefits) and the readers that grant them — Character.cpp:624-639 (learningPercent), :754-760 (Luck in
    // GetActualStat), :1953 (Enchanter in GetActualResistance), :2398-2531 (actualSkillLevel) and :2534-2541
    // (getActualSkillValue: a positive level reads at least Novice). Each profession counts once however many
    // joined people share it (NPC.cpp:53-65, CheckHiredNPCSpeciality). Our presence is PartyFollowers, read on
    // every question; nothing derived is stored or saved.
    private const int Scholar = 4, Teacher = 13, Instructor = 14, ArmsmasterProfession = 15, Weaponsmaster = 16,
        Apprentice = 17, Mystic = 18, Spellmaster = 19, Trader = 20, MerchantProfession = 21, Scout = 22,
        Herbalist = 23, Apothecary = 24, Tinker = 25, Locksmith = 26, Fool = 27, ChimneySweep = 28, Factor = 31,
        Banker = 32, Enchanter = 37, Pirate = 45, Psychic = 47, Gypsy = 48, Duper = 50, Burglar = 51,
        Acolyte = 53, Initiate = 54, Prelate = 55, Monk = 56;

    private static readonly (int Profession, int Amount)[] LuckTerms = [(Fool, 5), (ChimneySweep, 20), (Psychic, 10)];
    private static readonly (int Profession, int Amount)[] LearningTerms = [(Teacher, 10), (Instructor, 15), (Scholar, 5)];
    private static readonly (int Profession, int Amount)[] ResistanceTerms = [(Enchanter, 20)];
    private static readonly (int Profession, int Amount)[] ElementTerms = [(Apprentice, 2), (Mystic, 3), (Spellmaster, 4)];
    private static readonly (int Profession, int Amount)[] SelfTerms = [(Acolyte, 2), (Initiate, 3), (Prelate, 4)];

    private static readonly Dictionary<string, (int Profession, int Amount)[]> SkillTerms = new(StringComparer.Ordinal)
    {
        ["Armsmaster"] = [(ArmsmasterProfession, 2), (Weaponsmaster, 3)],
        ["Stealing"] = [(Burglar, 8)],
        ["Alchemy"] = [(Herbalist, 4), (Apothecary, 8)],
        ["Unarmed"] = [(Monk, 2)],
        ["Dodging"] = [(Monk, 2)],
        ["Fire"] = ElementTerms, ["Air"] = ElementTerms, ["Water"] = ElementTerms, ["Earth"] = ElementTerms,
        ["Spirit"] = SelfTerms, ["Mind"] = SelfTerms, ["Body"] = SelfTerms,
        ["Merchant"] = [(Trader, 4), (MerchantProfession, 6), (Gypsy, 3), (Duper, 8)],
        ["Perception"] = [(Scout, 6), (Psychic, 5)],
        ["Disarm Traps"] = [(Tinker, 4), (Locksmith, 6), (Burglar, 8)],
    };

    // The resistances the donor's reader is asked for; Spirit reads Body's base there, and both take the term.
    private static readonly HashSet<DamageKindId> Resisted =
    [
        MightAndMagic7Damage.Fire, MightAndMagic7Damage.Air, MightAndMagic7Damage.Water, MightAndMagic7Damage.Earth,
        MightAndMagic7Damage.Mind, MightAndMagic7Damage.Spirit, MightAndMagic7Damage.Body,
    ];

    /// <summary>What joined companions add to Luck, for <see cref="MightAndMagic7Combat.ActualAttribute"/>.</summary>
    internal FollowerContribution Luck => Contribution(LuckTerms);

    /// <summary>What joined companions add to a resistance, for <see cref="MightAndMagic7Combat.CharacterResistance"/>.</summary>
    internal FollowerContribution Resistance(DamageKindId kind) =>
        Resisted.Contains(kind) ? Contribution(ResistanceTerms) : FollowerContribution.None;

    /// <summary>The percent joined tutors add to every member's share of an award.</summary>
    internal int LearningBonus => Contribution(LearningTerms).Amount;

    /// <summary>The levels joined companions add to one skill.</summary>
    internal int SkillBonus(SkillId skill) =>
        SkillTerms.TryGetValue(skill.Value, out (int Profession, int Amount)[]? terms) ? Contribution(terms).Amount : 0;

    /// <summary>
    /// A member's skill as this game's rules read it: the purchased level plus what joined companions add, capped at
    /// the donor's sixty, at no less than Novice once the level is positive. The purchased entry is never changed.
    /// </summary>
    internal SkillEntry Actual(PartyMember member, SkillId skill)
    {
        ArgumentNullException.ThrowIfNull(member);
        SkillEntry carried = member.Skills.TryGet(skill, out SkillEntry entry) ? entry : new SkillEntry(skill, 0, SkillTier.None, 0);
        int bonus = SkillBonus(skill);
        if (bonus == 0) return carried;
        int level = Math.Min(MightAndMagic7Skills.DonorLevelCap, Math.Max(0, carried.Level) + bonus);
        return carried with { Level = level, Tier = carried.Tier.IsNone ? new SkillTier(1) : carried.Tier };
    }

    /// <summary>The actual skill when a reader may run without a follower owner (a session without people).</summary>
    internal static SkillEntry Actual(MightAndMagic7Followers? followers, PartyMember member, SkillId skill) =>
        followers?.Actual(member, skill) ?? (member.Skills.TryGet(skill, out SkillEntry entry) ? entry : new SkillEntry(skill, 0, SkillTier.None, 0));

    private FollowerContribution Contribution((int Profession, int Amount)[] terms)
    {
        if (_party() is not { } party || party.Followers.All.Count == 0) return FollowerContribution.None;
        int amount = 0;
        List<string> sources = [];
        foreach ((int profession, int bonus) in terms)
        {
            MightAndMagic7FollowerFacts? first = party.Followers.All
                .Select(follower => _describe(follower.Definition.Value))
                .FirstOrDefault(facts => facts?.Profession == profession);
            if (first is null) continue;
            amount += bonus;
            sources.Add($"{first.Name} +{bonus}");
        }

        return amount == 0 ? FollowerContribution.None : new FollowerContribution(amount, string.Join(", ", sources));
    }

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
    internal static int? HirePrice(MightAndMagic7FollowerFacts facts) => facts.Profession == Burglar ? 0 : facts.HirePrice;

    public FoundGoldDivision Divide(PartyEntity party, int found)
    {
        if (found == 0) return new(0, 0);
        MightAndMagic7FollowerFacts[] companions = party.Followers.All
            .Select(follower => _describe(follower.Definition.Value)).OfType<MightAndMagic7FollowerFacts>().ToArray();
        long total = found;
        foreach ((int profession, int percent) in new[] { (Factor, 10), (Banker, 20), (Pirate, 10) })
            if (companions.Any(facts => facts.Profession == profession)) total += total * percent / 100;
        long salary = companions.Sum(facts => (long)(facts.HirePrice ?? 0));
        long share = salary == 0 ? 0 : salary >= 10000 ? total : Math.Max(1, total * salary / 10000);
        // A pathological authored salary never creates debt or a negative purse.
        return new(checked((int)total), checked((int)Math.Min(total, share)));
    }
}
