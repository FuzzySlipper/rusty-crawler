using PartyRpg.Kit;
using System.Globalization;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Promotion;
using PartyRpg.Kit.Skills;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>Which school one alternative of a second promotion takes, and which it leaves to the other.</summary>
/// <remarks>
/// <para>
/// <b>Ours, over the donor's mastery table.</b> The shipped class table carries thirty-six rank rows and no
/// flag saying which alternative is which; what it <em>does</em> carry, in the donor's transcription of the
/// executable, is the per-class mastery of every skill
/// (<c>OpenEnroth/src/Engine/mm7_data.cpp:763</c>, <c>skillMaxMasteryPerClass</c>). Four families' pairs
/// differ in exactly the two magic schools and in nothing else — Paladin's Hero and Villain, Archer's Master
/// Archer and Sniper, Cleric's Priest of the Light and Priest of the Dark, and Sorcerer's Arch Mage and Lich
/// — which is precisely the four the manual names when it describes the split
/// ([`docs/research/mm7-manual-outline.md`](../../../docs/research/mm7-manual-outline.md) §1 and §8,
/// printed pp.14–16 and p.36). The other five families' pairs differ elsewhere or nowhere, so their paths
/// take no school at all and this records that rather than claiming a magic split they do not have.
/// </para>
/// <para>
/// The words <c>light</c> and <c>dark</c> are the manual's own description of the choice ("the second
/// promotion offers two alternative ranks per class … the pairs differ in Light versus Dark Magic access"),
/// and the two the donor's grand-master gate names when it asks whether a caster is one of the two light
/// classes or one of the two dark ones (<c>src/GUI/UI/NPCTopics.cpp:512-519</c>, <c>CLASS_ARCHAMGE</c>,
/// <c>CLASS_PRIEST_OF_SUN</c>, <c>CLASS_LICH</c>, <c>CLASS_PRIEST_OF_MOON</c>).
/// </para>
/// </remarks>
/// <param name="Choice">Which alternative it is: <c>light</c> or <c>dark</c>.</param>
/// <param name="Opens">The school this alternative takes, or null when it takes none.</param>
/// <param name="Closes">
/// The school it leaves to the other alternative, or null when the pair splits on no school. It is what a
/// refusal names when a caster asks for the magic their own choice put out of reach.
/// </param>
internal readonly record struct PromotionPath(string Choice, SkillId? Opens, SkillId? Closes);

/// <summary>Reads the authored promotion ladder and applies this game's eligibility policy.</summary>
/// <remarks>
/// Loaded promotion content names each previous/resulting class, giver, quest proof, and path.
/// The ordinary world's imported topic events judge their quest and grant through the same ladder;
/// the ladder's class edges make the second alternatives exclusive per character.
/// The class family order is documented in OpenEnroth src/Engine/Objects/CharacterEnumFunctions.h,
/// while NPC/topic/quest identities come from the operator's imported tables. The rank requirements
/// and fallback offer wording are this product's adaptations, authored in the normal game pack.
/// </remarks>
internal sealed class MightAndMagic7Promotions : IPromotionRule
{
    /// <summary>The word this game's ladder uses for the light alternative of a second promotion.</summary>
    internal const string LightChoice = "light";

    /// <summary>The word this game's ladder uses for the dark alternative of a second promotion.</summary>
    internal const string DarkChoice = "dark";


    /// <summary>The school the light alternatives of the four magic-splitting families take.</summary>
    internal static readonly SkillId LightSchool = new("Light");

    /// <summary>The school the dark alternatives of the four magic-splitting families take.</summary>
    internal static readonly SkillId DarkSchool = new("Dark");

    private readonly Dictionary<string, PromotionPath> _paths;
    private readonly IReadOnlyList<string> _notes;

    private MightAndMagic7Promotions(PromotionLadder ladder, Dictionary<string, PromotionPath> paths, IReadOnlyList<string> notes)
    {
        Ladder = ladder;
        _paths = paths;
        _notes = notes;
    }

    /// <inheritdoc />
    public PromotionLadder Ladder { get; }

    /// <summary>The owner's policy: recovery precedes every route into a promotion.</summary>
    /// <remarks>
    /// Ours by owner decision. OpenEnroth Character.cpp:4028-4029 changes class directly without a condition
    /// gate; its CanAct at :350-357 names these incapacities. We deliberately require recovery before rank.
    /// </remarks>
    public string? Ineligible(PartyMember member)
    {
        foreach (ConditionId condition in new[] { MightAndMagic7Conditions.Eradicated, MightAndMagic7Conditions.Dead, MightAndMagic7Conditions.Petrified, MightAndMagic7Conditions.Unconscious })
        {
            if (!member.Conditions.Has(condition)) continue;
            string recovery = condition == MightAndMagic7Conditions.Unconscious
                ? "rest or healing"
                : condition == MightAndMagic7Conditions.Dead
                    ? "a temple cure or a raising spell"
                    : condition == MightAndMagic7Conditions.Petrified
                        ? "a temple cure or Stone to Flesh"
                        : "a temple cure";
            return $"recovery from {condition} through {recovery} before promotion";
        }
        return null;
    }

    /// <summary>Which school each alternative of a second promotion takes, by the class it belongs to.</summary>
    internal IReadOnlyDictionary<string, PromotionPath> Paths => _paths;

    /// <summary>What reading this game's ladder noticed, for a report.</summary>
    internal IReadOnlyList<string> Notes => _notes;

    /// <summary>How many ranks the ladder states: every class's first promotion and both of its seconds.</summary>
    internal int RankCount => Ladder.Ranks.Count;

    /// <summary>How many people the ladder names as givers of a rank.</summary>
    internal int GiverCount => Ladder.Ranks.Select(rank => rank.Giver).Distinct(StringComparer.Ordinal).Count();

    /// <summary>How many ranks ask for an errand this game states a quest for.</summary>
    /// <remarks>
    /// An errand is stated as the record a finished quest leaves, so it is counted by the record's own
    /// prefix rather than by a requirement kind: what the rank asks for is the party's own state, and the
    /// kind it is carried in is the same one a deed the original keeps as an award uses.
    /// </remarks>
    internal int QuestRequirementCount => Ladder.Ranks.Sum(
        rank => rank.Requirements.Count(requirement =>
            requirement.Kind == PromotionRequirementKind.Award &&
            requirement.Name.StartsWith(MightAndMagic7Identities.ErrandFlagPrefix, StringComparison.Ordinal)));

    /// <summary>How many ranks ask for something the party carries, and could be given today.</summary>
    internal int ItemRequirementCount =>
        Ladder.Ranks.Sum(rank => rank.Requirements.Count(requirement => requirement.Kind == PromotionRequirementKind.Item));

    /// <summary>Reads this game's ranks over the content the product loaded.</summary>
    /// <remarks>
    /// Content without a promotion document declares no ranks. The normal bundle supplies all nine
    /// ladders as authored data; a focused scenario may supply its own smaller ladder.
    /// </remarks>
    /// <param name="catalog">The validated content, or null when no bundle supplied any.</param>
    /// <returns>This game's ranks.</returns>
    internal static MightAndMagic7Promotions Read(ContentCatalog? catalog)
    {
        Dictionary<string, PromotionPath> paths = new(StringComparer.Ordinal);
        PromotionRank[] ranks = ReadRanks(catalog, paths);
        List<string> notes = [];
        HashSet<string> declared = catalog is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : [.. catalog.Entries(MightAndMagic7Skills.ClassDefinitionKind).Select(entry => entry.Entry.Id)];
        HashSet<string> stated = [.. ranks.SelectMany(rank => new[] { rank.From.Value, rank.To.Value })];

        notes.Add(string.Create(
            CultureInfo.InvariantCulture,
            $"{ranks.Length} ranks are stated over {ranks.Select(rank => rank.Giver).Distinct(StringComparer.Ordinal).Count()} people who give them; {ranks.Count(rank => rank.Choice.Length > 0)} of them are the second promotion's alternatives."));
        notes.Add(string.Create(
            CultureInfo.InvariantCulture,
            $"{paths.Count} classes take a magic path: the pairs of four families split on exactly the two schools this game's mastery table gives them."));
        if (declared.Count > 0)
        {
            int missing = stated.Count(name => !declared.Contains(name));
            notes.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"Content declares {declared.Count} class rows; {stated.Count - missing} of the {stated.Count} classes this ladder names are among them{(missing > 0 ? $", and {missing} are not" : string.Empty)}."));
        }

        return new MightAndMagic7Promotions(new PromotionLadder(ranks), paths, notes);
    }

    /// <summary>
    /// Why one member's own chosen path closes one skill, or null when this game has nothing to add.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two states are worth naming, and they are different facts. A member who has <em>taken</em> an
    /// alternative is told which one: the light path of the Master Archer takes Light and leaves Dark to the
    /// other alternative, so a Master Archer may hold no Dark. A member who stands at the rank the two
    /// alternatives split from but has taken neither is holding the intersection — both alternatives close
    /// that school, so the rank above is what opens it — and is told that instead.
    /// </para>
    /// <para>
    /// A class whose pair splits on no school answers nothing here, and the ceiling falls back to the game's
    /// own general refusal: a Champion may hold no Dark, and its own table is the reason rather than a choice
    /// it made.
    /// </para>
    /// </remarks>
    /// <param name="member">The member whose class, rank, and path are read.</param>
    /// <param name="skill">The skill that is closed to them.</param>
    /// <returns>The refusal that names the choice, or null when the closing is not a path's doing.</returns>
    internal Refusal? ClosedReason(PartyMember member, SkillId skill)
    {
        ArgumentNullException.ThrowIfNull(member);
        string closed = skill.Value;
        if (_paths.TryGetValue(member.Profile.Class.Value, out PromotionPath taken))
        {
            if (taken.Closes is not { } left || !string.Equals(left.Value, closed, StringComparison.Ordinal)) return null;
            string opens = taken.Opens is { } opened ? opened.Value : "nothing";
            return new Refusal(
                MightAndMagic7Codes.SkillClosedByPath,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{member.Profile.Name} took the {taken.Choice} path of the {member.Profile.Class}: it takes {opens} and leaves {closed} to the other alternative, so a {member.Profile.Class} may hold no {closed}."));
        }

        // Neither alternative taken yet. The ceiling here is what both alternatives allow, so a school only
        // one of them takes is not the member's yet — and the sentence names the alternative that would open
        // it, which is the whole of what a player has to do about it. A skill neither alternative takes is
        // the class's own limit rather than a choice nobody has made, and says so through the game's general
        // refusal instead.
        List<PromotionRank> split = [.. Ladder.From(member.Profile.Class).Where(rank => rank.Choice.Length > 0)];
        if (split.Count < 2) return null;
        List<string> openings = [];
        foreach (PromotionRank rank in split)
        {
            if (!_paths.TryGetValue(rank.To.Value, out PromotionPath path)) return null;
            if (path.Opens is { } opened && string.Equals(opened.Value, closed, StringComparison.Ordinal))
            {
                openings.Add($"{rank.To} ({path.Choice})");
            }
        }

        if (openings.Count == 0) return null;
        return new Refusal(
            MightAndMagic7Codes.SkillClosedByUnchosenPath,
            string.Create(
                CultureInfo.InvariantCulture,
                $"{member.Profile.Name} is a {member.Profile.Class} of rank {member.Progression.ClassRank} and has taken neither alternative of the rank above: {string.Join(" or ", openings)} would take {closed}, and until one of them is taken a {member.Profile.Class} may hold none of it."));
    }

    private static PromotionRank[] ReadRanks(ContentCatalog? catalog, Dictionary<string, PromotionPath> paths)
    {
        if (catalog is null) return [];
        List<PromotionRank> ranks = [];
        List<ContentValidationIssue> issues = [];
        foreach (var (pack, document, entry) in catalog.Entries("promotion"))
        {
            void Defect(string message) => issues.Add(new("promotion-content-invalid", message, pack.PackId, document.DocumentId));
            string from = entry.GetString("from"), to = entry.GetString("to"), choice = entry.GetString("choice");
            int rank = entry.GetInt32("rank") ?? 0;
            if (from.Length == 0 || to.Length == 0 || rank < 2 || (choice.Length > 0 && choice is not ("light" or "dark")))
            {
                Defect($"Promotion '{entry.Id}' needs its previous class, resulting class, rank and valid path; otherwise the ladder would silently grant the wrong class.");
                continue;
            }
            List<PromotionRequirement> requirements = [];
            foreach (var row in entry.GetArray("requirements"))
            {
                string kind = ContentEntry.ReadString(row, "kind"), name = ContentEntry.ReadString(row, "name");
                string label = ContentEntry.ReadString(row, "label");
                double statedAmount = ContentEntry.ReadDouble(row, "amount") ?? 1;
                int amount = double.IsFinite(statedAmount) && statedAmount >= 1 && statedAmount <= int.MaxValue && Math.Truncate(statedAmount) == statedAmount ? (int)statedAmount : 0;
                if (name.Length == 0 || amount < 1 || kind is not ("giver" or "item" or "award"))
                {
                    Defect($"Promotion '{entry.Id}' has an invalid requirement; it would allow an unearned promotion.");
                    continue;
                }
                requirements.Add(kind switch
                {
                    "giver" => PromotionRequirement.FromGiver(name, label),
                    "item" => PromotionRequirement.ForItem(name, amount, label),
                    _ => PromotionRequirement.ForAward(name, amount, label),
                });
            }
            if (requirements.Count(r => r.Kind == PromotionRequirementKind.Giver) != 1 || requirements.Count < 2)
                Defect($"Promotion '{entry.Id}' must name one giver and its quest proof; an unearned rank must not be offered.");
            ranks.Add(new(entry.Id, new(from), new(to), rank, requirements, choice, entry.GetString("award"), entry.GetString("words")));
            string opens = entry.GetString("opens"), closes = entry.GetString("closes");
            if (opens.Length > 0 || closes.Length > 0)
            {
                if (opens.Length == 0 || closes.Length == 0 || choice.Length == 0 || !paths.TryAdd(to, new(choice, new(opens), new(closes))))
                    Defect($"Promotion '{entry.Id}' has an incomplete or duplicate magic path; its skill eligibility would be ambiguous.");
            }
        }
        if (issues.Count > 0) throw new ContentValidationException("Promotion content cannot be read.", issues);
        return [.. ranks];
    }
}
