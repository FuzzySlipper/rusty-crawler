using PartyRpg.Kit.Content;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.World;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// This game's party creation: the choices a player is offered, the budgets they spend, and the default
/// party they may start from.
/// </summary>
/// <remarks>
/// <para>
/// Creation itself is the kit's mechanism; what a player may choose is this ruleset's, and it is stated here
/// rather than in the kit because races, classes, skills, portraits and starting numbers are Might and Magic
/// vocabulary. The tables are in <see cref="MightAndMagic7CreationTables"/>, which records for each value
/// whether the shipped data carries it or we authored it.
/// </para>
/// <para>
/// <b>Content is consulted for the identities it owns.</b> Classes and skills are imported definitions: the
/// class list a player may be created in, and the skills a class fixes or offers, must be definitions the
/// loaded content actually declares. A mismatch is refused when the choices are assembled — naming the class
/// or skill that is missing — rather than offering a player a character the game's own data cannot carry.
/// Races and portraits have no shipped table at all, so they are this ruleset's definitions outright.
/// </para>
/// <para>
/// A session composed without content still creates a party from the compiled tables: the product's shipped
/// bundle carries no imported packs until the operator generates them, and a game that cannot create a party
/// is not a game.
/// </para>
/// </remarks>
public static class MightAndMagic7Creation
{
    /// <summary>The definition kind that carries classes in content.</summary>
    private const string ClassDefinitionKind = "class";

    /// <summary>The definition kind that carries skills in content.</summary>
    private const string SkillDefinitionKind = "skill";

    /// <summary>How many characters this game's party is created with (manual p.10: a party is four).</summary>
    public const int MemberCount = 4;

    /// <summary>
    /// How many attribute points a character has to spend, spent exactly before creation may finish
    /// (manual pp.12 and 17; the donor's <c>CharacterCreation_GetUnspentAttributePointCount</c> starts the
    /// same count at fifty).
    /// </summary>
    public const int AttributePool = 50;

    /// <summary>How many skills a character chooses beyond the two its class fixes (manual p.12).</summary>
    public const int ChosenSkillCount = 2;

    /// <summary>
    /// The longest name a character may carry. Ours: the manual states no limit, and a limit exists because a
    /// name is drawn in the party row.
    /// </summary>
    public const int NameMaximumLength = 15;

    /// <summary>
    /// The class rank a created character starts on. Ours, and structural rather than tuned: a class is
    /// created at the bottom of its ladder, and the promotions that raise it are earned in play
    /// (manual pp.14–16).
    /// </summary>
    public const int StartingRank = 1;

    /// <summary>The level a created character begins at.</summary>
    public const int StartingLevel = 1;

    /// <summary>
    /// The rung a skill learned at creation stands at: the game's first rung, "normal" mastery
    /// (<c>OpenEnroth/src/Engine/Objects/CharacterEnums.h</c>, where <c>MASTERY_NOVICE = 1</c> and zero means
    /// no mastery at all).
    /// </summary>
    public static readonly SkillTier StartingSkillTier = new(1);

    /// <summary>
    /// What the new party's purse holds. Ours: the manual promises the party begins with "a small amount of
    /// gold" and gives no number ([`docs/research/mm7-manual-outline.md`](../../../docs/research/mm7-manual-outline.md) §1, §9).
    /// </summary>
    public const int StartingCoins = 200;

    /// <summary>
    /// What the new party's larder holds, in portions. Ours: the manual gives no starting amount, and the
    /// day's ration is the food owner's rule, so this is stated in the abstract unit the kit carries.
    /// </summary>
    public const int StartingFoodPortions = 10;

    /// <summary>
    /// The choices creation offers, checked against the game's own definitions when content is loaded.
    /// </summary>
    /// <param name="content">
    /// The validated content the product loaded, when it loaded any. Classes and skills are checked against
    /// it; pass null in a composition that has no content, which is what the shipped bundle is until the
    /// operator generates the imported packs.
    /// </param>
    /// <exception cref="ContentValidationException">
    /// The content contradicts the class or skill definitions creation is built on; the exception carries
    /// every mismatch found.
    /// </exception>
    public static PartyCreationOptions Options(ContentCatalog? content = null) =>
        new(
            MemberCount,
            MightAndMagic7CreationTables.Races,
            Classes(content),
            MightAndMagic7CreationTables.Portraits,
            AttributePool,
            ChosenSkillCount,
            NameMaximumLength,
            StartingSkillTier,
            StartingLevel,
            StartingCoins,
            StartingFoodPortions,
            ProvisionUnit.Portions,
            // A new party is unknown: the world has not met it yet, so neither reputation nor fame starts
            // above zero (manual outline §8: reputation and fame change in play).
            startingReputation: 0,
            startingFame: 0);

    /// <summary>
    /// The default party a player may start from, offered through the flow's own steps rather than built
    /// behind them.
    /// </summary>
    public static PartyCreationDefaults Defaults => MightAndMagic7CreationTables.DefaultParty;

    /// <summary>Starts this game's party creation, with the default party already applied.</summary>
    /// <remarks>
    /// This game opens its creation screen on a default party a player may accept or customize
    /// ([`docs/research/mm7-manual-outline.md`](../../../docs/research/mm7-manual-outline.md) §1, pp.10–11),
    /// which is what this composes: a flow whose members are finished and confirmed, ready to be accepted
    /// through <see cref="PartyCreationFlow.ToCreation"/> or reopened one at a time through
    /// <see cref="PartyCreationFlow.SelectMember"/>. It is the seam a session holds while it is creating a
    /// party and steps no world.
    /// </remarks>
    /// <param name="content">The validated content the product loaded, when it loaded any.</param>
    /// <exception cref="ContentValidationException">The content contradicts the class or skill definitions creation is built on.</exception>
    public static PartyCreationFlow Start(ContentCatalog? content = null) => new(Options(content), Defaults);

    /// <summary>Reads the class choices, checking them against content when content was loaded.</summary>
    /// <exception cref="ContentValidationException">The content contradicts the classes and skills creation offers.</exception>
    private static IReadOnlyList<CreationClass> Classes(ContentCatalog? content)
    {
        IReadOnlyList<CreationClass> authored = MightAndMagic7CreationTables.Classes;
        if (content is null) return authored;

        List<ContentValidationIssue> issues = [];
        Dictionary<string, string> declaredClasses = new(StringComparer.Ordinal);
        foreach ((LoadedPack pack, _, ContentEntry entry) in content.Entries(ClassDefinitionKind))
        {
            // A class whose base class is itself is one a character may be created in; the promoted ranks
            // name the base class they belong to, which is the column the table repeats the base name in
            // (docs/research/mm7-data-inventory.md, Characters).
            if (!string.Equals(entry.GetString("baseClass"), entry.Id, StringComparison.Ordinal)) continue;
            declaredClasses.TryAdd(entry.Id, pack.PackId);
        }

        HashSet<string> declaredSkills = [];
        foreach ((_, _, ContentEntry entry) in content.Entries(SkillDefinitionKind)) declaredSkills.Add(entry.Id);

        // Every mismatch points at the pack that carries the class table, because that is the pack an
        // operator would correct; a catalog with no class table at all names itself.
        string owner = declaredClasses.Count > 0
            ? declaredClasses.Values.First()
            : content.Packs.Count > 0 ? content.Packs[0].PackId : "content";

        foreach (CreationClass characterClass in authored)
        {
            if (!declaredClasses.TryGetValue(characterClass.Id.Value, out string? packId))
            {
                packId = owner;
                issues.Add(new ContentValidationIssue(
                    "creation-class-missing",
                    $"creation offers the class '{characterClass.Id}', which the loaded content does not declare as a base class, so a character could be created in a class this game's own data does not carry.",
                    packId));
            }

            foreach (SkillId skill in characterClass.FixedSkills.Concat(characterClass.ChoosableSkills))
            {
                if (declaredSkills.Contains(skill.Value)) continue;
                issues.Add(new ContentValidationIssue(
                    "creation-skill-missing",
                    $"the class '{characterClass.Id}' creation offers uses the skill '{skill}', which the loaded content does not declare, so creation would grant a skill this game's own data does not carry.",
                    packId));
            }
        }

        foreach (string declared in declaredClasses.Keys)
        {
            if (authored.Any(characterClass => string.Equals(characterClass.Id.Value, declared, StringComparison.Ordinal))) continue;
            issues.Add(new ContentValidationIssue(
                "creation-class-unknown",
                $"the loaded content declares the base class '{declared}', which this ruleset has no creation rules for, so nobody could be created in it.",
                declaredClasses[declared]));
        }

        if (issues.Count > 0)
        {
            throw new ContentValidationException(
                $"Creation cannot be offered over this content: {issues[0].Message}",
                issues);
        }

        return authored;
    }
}
