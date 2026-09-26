using System.Globalization;
using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Alchemy;

/// <summary>One mixture a screen asked for: who is mixing, and which two things they are mixing.</summary>
/// <remarks>
/// The member is the party's own index rather than a durable identity, exactly as a casting's member and a
/// service lesson's are: the screen was shown the party in its order and names the row it drew, and the
/// session resolves that row against the party it holds inside the same update. The two things are named by
/// the durable identity of the instances in the pack rather than by their definitions, because two potions of
/// one kind are two things at two strengths and which of them was spent is a fact the party must keep.
/// </remarks>
/// <param name="Member">The mixing character's place in the party, counted from zero.</param>
/// <param name="First">One ingredient, as the pack holds it.</param>
/// <param name="Second">The other ingredient, as the pack holds it.</param>
public readonly record struct MixingRequest(int Member, ItemInstanceId First, ItemInstanceId Second);

/// <summary>What one attempt at mixing did, or why nothing was attempted, as a report a panel can show.</summary>
/// <remarks>
/// <para>
/// A result always says who mixed, what they mixed, and what came of it, so a caller can read it
/// unconditionally. A refusal changes nothing at all: the ingredients stay exactly where they lay and nobody
/// is hurt. An attempt that went ahead always consumed both ingredients — that is what mixing is — and says
/// which of the three outcomes the game's own table stated.
/// </para>
/// <para>
/// <b>What the party learned travels with the outcome.</b> A game's mixture table records a discovery per
/// pair, and this carries that record's own number rather than a sentence: whether the party may know it, and
/// where a discovery is written, belongs to whatever owner keeps what the party knows. A result with no note
/// is a mixture the game states no discovery for.
/// </para>
/// </remarks>
public sealed record MixingResult
{
    private MixingResult(
        bool isMixed,
        string code,
        string message,
        int member,
        string mixer,
        string first,
        string second,
        string outcome,
        string result,
        string resultName,
        int power,
        int note,
        int burst,
        int harm,
        string condition,
        PartyRefusal? refusal)
    {
        IsMixed = isMixed;
        Code = code;
        Message = message;
        Member = member;
        Mixer = mixer;
        First = first;
        Second = second;
        Outcome = outcome;
        Result = result;
        ResultName = resultName;
        Power = power;
        Note = note;
        Burst = burst;
        Harm = harm;
        Condition = condition;
        Refusal = refusal;
    }

    /// <summary>Whether the mixture was attempted, which is true of a burst and of a mixture that did nothing.</summary>
    public bool IsMixed { get; }

    /// <summary>The outcome's own code, which is what a test and a diagnostic compare.</summary>
    public string Code { get; }

    /// <summary>What happened, in a sentence a person reads.</summary>
    public string Message { get; }

    /// <summary>The mixing character's place in the party, counted from zero.</summary>
    public int Member { get; }

    /// <summary>What the mixing character is called, empty when the party has no such member.</summary>
    public string Mixer { get; }

    /// <summary>What the first ingredient is called.</summary>
    public string First { get; }

    /// <summary>What the second ingredient is called.</summary>
    public string Second { get; }

    /// <summary>
    /// What came of the attempt: <c>potion</c> when the pair made one, <c>burst</c> when it went off, and
    /// <c>nothing</c> when the table states that the pair does nothing.
    /// </summary>
    public string Outcome { get; }

    /// <summary>The definition of what the mixture made, empty when it made nothing.</summary>
    public string Result { get; }

    /// <summary>What the mixture made is called, empty when it made nothing.</summary>
    public string ResultName { get; }

    /// <summary>The strength the result came out at, zero when the mixture made nothing.</summary>
    public int Power { get; }

    /// <summary>The discovery the mixture records, zero when the game states none.</summary>
    public int Note { get; }

    /// <summary>How strong the burst was, zero when the mixture did not burst.</summary>
    public int Burst { get; }

    /// <summary>What the burst took from the mixing character, zero when it took nothing.</summary>
    public int Harm { get; }

    /// <summary>The condition the burst left on the mixing character, empty when it left none.</summary>
    public string Condition { get; }

    /// <summary>Why nothing was attempted, or null when the mixture was attempted.</summary>
    public PartyRefusal? Refusal { get; }

    /// <summary>A mixture was attempted and made the thing its row states.</summary>
    /// <param name="mixer">What the mixing character is called.</param>
    /// <param name="member">The mixing character's place in the party.</param>
    /// <param name="first">What the first ingredient is called.</param>
    /// <param name="second">What the second ingredient is called.</param>
    /// <param name="result">What the mixture made.</param>
    /// <param name="resultName">What the mixture made is called.</param>
    /// <param name="power">The strength it came out at.</param>
    /// <param name="note">The discovery the mixture records.</param>
    /// <returns>The result.</returns>
    public static MixingResult Mixed(
        string mixer,
        int member,
        string first,
        string second,
        ItemDefinitionId result,
        string resultName,
        int power,
        int note)
    {
        string message = note > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{mixer} mixes {first} with {second} into {resultName} of strength {power}, and learns something worth remembering.")
            : string.Create(CultureInfo.InvariantCulture, $"{mixer} mixes {first} with {second} into {resultName} of strength {power}.");
        return new MixingResult(
            isMixed: true,
            "mixture-mixed",
            message,
            member,
            mixer,
            first,
            second,
            "potion",
            result.Value,
            resultName,
            power,
            note,
            burst: 0,
            harm: 0,
            condition: string.Empty,
            refusal: null);
    }

    /// <summary>A mixture was attempted and went off, as its own row states.</summary>
    /// <param name="mixer">What the mixing character is called.</param>
    /// <param name="member">The mixing character's place in the party.</param>
    /// <param name="first">What the first ingredient is called.</param>
    /// <param name="second">What the second ingredient is called.</param>
    /// <param name="strength">How strong the burst was.</param>
    /// <param name="backfire">What it did to the mixing character.</param>
    /// <returns>The result.</returns>
    public static MixingResult Backfired(
        string mixer,
        int member,
        string first,
        string second,
        int strength,
        MixtureBackfire backfire)
    {
        ArgumentNullException.ThrowIfNull(backfire);
        return new MixingResult(
            isMixed: true,
            "mixture-burst",
            string.Create(
                CultureInfo.InvariantCulture,
                $"{mixer} mixes {first} with {second} and the mixture goes off at strength {strength}: both ingredients are gone and {mixer} takes {backfire.Describe()}."),
            member,
            mixer,
            first,
            second,
            "burst",
            result: string.Empty,
            resultName: string.Empty,
            power: 0,
            note: 0,
            burst: strength,
            backfire.Harm,
            backfire.Condition?.Value ?? string.Empty,
            refusal: null);
    }

    /// <summary>A mixture was attempted and its own row states that nothing happens.</summary>
    /// <param name="mixer">What the mixing character is called.</param>
    /// <param name="member">The mixing character's place in the party.</param>
    /// <param name="first">What the first ingredient is called.</param>
    /// <param name="second">What the second ingredient is called.</param>
    /// <returns>The result.</returns>
    public static MixingResult Nothing(string mixer, int member, string first, string second) =>
        new(
            isMixed: true,
            "mixture-nothing",
            string.Create(CultureInfo.InvariantCulture, $"{mixer} mixes {first} with {second}, and nothing comes of it; both are still where they were."),
            member,
            mixer,
            first,
            second,
            "nothing",
            result: string.Empty,
            resultName: string.Empty,
            power: 0,
            note: 0,
            burst: 0,
            harm: 0,
            condition: string.Empty,
            refusal: null);

    /// <summary>Nothing was attempted, and this is why.</summary>
    /// <param name="member">The mixing character's place in the party.</param>
    /// <param name="mixer">What the mixing character is called, empty when the party has no such member.</param>
    /// <param name="first">What the first ingredient is called, empty when nothing was resolved.</param>
    /// <param name="second">What the second ingredient is called, empty when nothing was resolved.</param>
    /// <param name="refusal">Why nothing was attempted.</param>
    /// <returns>The result.</returns>
    /// <exception cref="ArgumentNullException">No refusal was supplied.</exception>
    public static MixingResult Refused(int member, string mixer, string first, string second, PartyRefusal refusal)
    {
        ArgumentNullException.ThrowIfNull(refusal);
        return new MixingResult(
            isMixed: false,
            refusal.Code,
            refusal.Message,
            member,
            mixer,
            first,
            second,
            "refused",
            result: string.Empty,
            resultName: string.Empty,
            power: 0,
            note: 0,
            burst: 0,
            harm: 0,
            condition: string.Empty,
            refusal);
    }

    /// <inheritdoc />
    public override string ToString() => IsMixed ? $"{Code}: {Message}" : $"{Code}: {Message}";
}

/// <summary>
/// The one workflow that mixes two things: resolve the character and the ingredients, find the pair's own row,
/// judge the rung its result asks for, take both ingredients out of the shared pack, and put what the row
/// states into it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Mixing is a transfer of the party's own things, so it goes through the party's own two entries.</b> The
/// ingredients leave by <see cref="PartyEntity.ConsumeItem"/> and the potion enters by
/// <see cref="PartyEntity.AcquireItem"/> — the same acquisition path a pickup, a purchase, and a chest's
/// contents take — which is why a pack that cannot take the potion refuses the whole attempt and the
/// ingredients stay exactly where they were. Nothing here reaches into the pack's list.
/// </para>
/// <para>
/// <b>Nothing is consumed before everything that could refuse has been asked.</b> The mixing character must be
/// able to mix at all, both ingredients must be lying in the pack, the pair must be one the game's table
/// states, the character's mastery must reach the rung the result asks for, and the pack must have room for
/// what would come out. Only then do the ingredients go.
/// </para>
/// <para>
/// <b>A mixture that goes off is a consequence, not a no-op.</b> Both ingredients are destroyed — that is
/// what mixing an incompatible pair costs — and what the burst does to the character is the game's own answer
/// through <see cref="IAlchemyRule.Backfire"/>, applied through the member's one damage entry and their own
/// conditions, so a burst leaves the same kind of state a sprung trap or a creature's bite does.
/// </para>
/// </remarks>
public sealed class PotionMixing
{
    private readonly PartyEntity _party;
    private readonly AlchemyCatalog _catalog;
    private readonly IAlchemyRule _rule;

    /// <summary>Creates the mixing workflow over one party and one game's own mixture table.</summary>
    /// <param name="party">The party whose pack holds the ingredients.</param>
    /// <param name="catalog">The mixtures the game's own tables state.</param>
    /// <param name="rule">This game's answers about mixing.</param>
    /// <exception cref="ArgumentNullException">No party, catalog, or rule was supplied.</exception>
    public PotionMixing(PartyEntity party, AlchemyCatalog catalog, IAlchemyRule rule)
    {
        _party = party ?? throw new ArgumentNullException(nameof(party));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _rule = rule ?? throw new ArgumentNullException(nameof(rule));
    }

    /// <summary>The party whose pack holds the ingredients.</summary>
    public PartyEntity Party => _party;

    /// <summary>The mixtures this game's tables state.</summary>
    public AlchemyCatalog Catalog => _catalog;

    /// <summary>This game's answers about mixing, which a panel reads what it shows through.</summary>
    public IAlchemyRule Rule => _rule;

    /// <summary>What the last attempt did, or why it did nothing.</summary>
    public MixingResult? Last { get; private set; }

    /// <summary>
    /// Mixes two things the party holds, or refuses by name and changes nothing.
    /// </summary>
    /// <param name="request">Who mixes, and which two instances they mix.</param>
    /// <returns>What the attempt did, or why nothing was attempted.</returns>
    public MixingResult Mix(MixingRequest request)
    {
        if (request.Member < 0 || request.Member >= _party.Members.Count)
        {
            return Record(MixingResult.Refused(
                request.Member,
                mixer: string.Empty,
                first: string.Empty,
                second: string.Empty,
                new PartyRefusal(
                    "mixture-no-member",
                    string.Create(CultureInfo.InvariantCulture, $"The party has no member {request.Member}, so nobody mixed anything."))));
        }

        PartyMember mixer = _party.Members[request.Member];

        // Whether a character may do anything at all is the game's own answer about its own conditions, and it
        // is asked before the ingredients are looked at: a character asleep is in no condition to mix, whatever
        // the pack happens to hold.
        if (_rule.MayMix(mixer) is { } unable)
        {
            return Record(MixingResult.Refused(request.Member, mixer.Profile.Name, string.Empty, string.Empty, unable));
        }

        if (request.First == request.Second)
        {
            return Record(Refuse(
                request,
                mixer,
                "mixture-same-instance",
                $"{mixer.Profile.Name} named one instance twice, and a thing mixed with itself is one thing rather than a mixture."));
        }

        // Both ingredients must be lying in the shared pack: mixing takes what the party carries loose, and an
        // item worn or wielded is a character's figure rather than something in the pack to be spent.
        if (_party.Inventory.Find(request.First) is not { } first)
        {
            return Record(Refuse(
                request,
                mixer,
                "mixture-ingredient-missing",
                string.Create(CultureInfo.InvariantCulture, $"The pack holds no item {request.First}, so there was nothing to mix.")));
        }

        if (_party.Inventory.Find(request.Second) is not { } second)
        {
            return Record(Refuse(
                request,
                mixer,
                "mixture-ingredient-missing",
                string.Create(CultureInfo.InvariantCulture, $"The pack holds no item {request.Second}, so there was nothing to mix.")));
        }

        string firstName = _rule.NameOf(first.Definition);
        string secondName = _rule.NameOf(second.Definition);

        // The pair's own row. A pair the table never stated is not a mixture at all — the ingredients are not
        // "an incompatible mixture" but two things nothing said to combine — so nothing is spent.
        if (_catalog.Find(first.Definition, second.Definition) is not { } mixture || !mixture.IsMixture)
        {
            return Record(MixingResult.Refused(
                request.Member,
                mixer.Profile.Name,
                firstName,
                secondName,
                new PartyRefusal(
                    "mixture-unknown",
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{firstName} and {secondName} are not a mixture this game states, so nothing was combined and both are still where they were."))));
        }

        return mixture.Produces
            ? Produce(request, mixer, first, second, firstName, secondName, mixture)
            : React(request, mixer, first, second, firstName, secondName, mixture);
    }

    /// <summary>Carries out a pair whose row states a thing to make.</summary>
    private MixingResult Produce(
        MixingRequest request,
        PartyMember mixer,
        ItemInstance first,
        ItemInstance second,
        string firstName,
        string secondName,
        PotionMixture mixture)
    {
        ItemDefinitionId result = mixture.Outcome.Result!.Value;
        string resultName = _rule.NameOf(result);

        // The rung a mixture asks for is a fact about what it makes, so it is judged against the character's
        // own mastery of the mixing skill — through the same skill entry a ceiling, a lesson, and a casting's
        // mastery all read — and a character who has not reached it is told what would raise it.
        SkillTier held = mixer.Skills.TierOf(_rule.Skill);
        if (held.Value < mixture.Tier.Value)
        {
            return Record(MixingResult.Refused(
                request.Member,
                mixer.Profile.Name,
                firstName,
                secondName,
                new PartyRefusal(
                    "mixture-mastery-too-low",
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{mixer.Profile.Name} stands at {_rule.RungName(held)} in {_rule.Skill} and {resultName} is mixed at {_rule.RungName(mixture.Tier)}; {_rule.MasteryRaisedBy(_rule.Skill)}."))));
        }

        int power = _rule.Strength(mixer, mixture, first, second);
        if (power < 1)
        {
            // A mixture that states a result and comes out at no strength at all is a defect in the game's own
            // answer rather than something to hand the party: it is refused by name instead of minting a potion
            // whose effect nothing could read.
            return Record(MixingResult.Refused(
                request.Member,
                mixer.Profile.Name,
                firstName,
                secondName,
                new PartyRefusal(
                    "mixture-strength-unstated",
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{resultName} would come out of mixing {firstName} with {secondName} at strength {power}, and this game states no strength below one for a mixture."))));
        }

        // The pack is asked before anything is consumed, so a party whose pack cannot take the potion keeps
        // both ingredients. Nothing between this judgement and the acquisition can take the room away: the two
        // ingredients leave the pack first, and a pack with more room is never a pack that refuses.
        if (_party.Inventory.Judge(result, count: 1) is { } refused)
        {
            return Record(MixingResult.Refused(request.Member, mixer.Profile.Name, firstName, secondName, refused));
        }

        ItemInstance made = _party.CreateItem(
            result,
            ItemState.Unidentified.WithPotency(power).Identified(),
            stackCount: 1);

        _party.ConsumeItem(first.Id);
        _party.ConsumeItem(second.Id);
        ItemAcquisition acquisition = _party.AcquireItem(made);
        if (!acquisition.Admitted)
        {
            // The judgement above and this admission ask the pack's own rule the same question, so a refusal
            // here is a rule that changed its answer between two calls inside one of them: it is reported
            // rather than swallowed, and what it reports is the pack's own words.
            return Record(MixingResult.Refused(request.Member, mixer.Profile.Name, firstName, secondName, acquisition.Refusal!));
        }

        return Record(MixingResult.Mixed(
            mixer.Profile.Name,
            request.Member,
            firstName,
            secondName,
            result,
            resultName,
            power,
            mixture.Note));
    }

    /// <summary>Carries out a pair whose row states that it goes off or does nothing.</summary>
    private MixingResult React(
        MixingRequest request,
        PartyMember mixer,
        ItemInstance first,
        ItemInstance second,
        string firstName,
        string secondName,
        PotionMixture mixture)
    {
        if (mixture.Outcome.Burst == 0)
        {
            // The table states that this pair does nothing: both ingredients stay exactly where they are,
            // which is the donor's own answer for two things that do not combine.
            return Record(MixingResult.Nothing(mixer.Profile.Name, request.Member, firstName, secondName));
        }

        MixtureBackfire backfire = _rule.Backfire(mixture.Outcome.Burst);
        _party.ConsumeItem(first.Id);
        _party.ConsumeItem(second.Id);

        if (backfire.Harm > 0) mixer.TakeDamage(backfire.Harm);
        if (backfire.Condition is { } condition) mixer.Conditions.Apply(new ActiveCondition(condition));

        return Record(MixingResult.Backfired(
            mixer.Profile.Name,
            request.Member,
            firstName,
            secondName,
            mixture.Outcome.Burst,
            backfire));
    }

    private static MixingResult Refuse(MixingRequest request, PartyMember mixer, string code, string message) =>
        MixingResult.Refused(request.Member, mixer.Profile.Name, string.Empty, string.Empty, new PartyRefusal(code, message));

    private MixingResult Record(MixingResult result)
    {
        Last = result;
        return result;
    }
}
