using System.Globalization;
using PartyRpg.Kit.Alchemy;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Skills;
using Rusty.Engine;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// This game's alchemy: the mixtures its own table states, the rung of Alchemy each result asks for, what a
/// mixture comes out at, and what one that goes off costs the character who tried it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The recipes are content and the arithmetic is policy.</b> Every pair a mixture is possible for, what it
/// makes, and whether it goes off instead all arrive from the pack the importer wrote, which read them from
/// <c>POTION.TXT</c>'s own matrix. What is <em>not</em> in that table is what the mixing is worth: the donor
/// adds the mixer's Alchemy level to the reagent's stated power, averages the strengths of two potions, and
/// lets a catalyst replace one — all of it in the executable
/// (<c>OpenEnroth/src/GUI/UI/UIPopup.cpp:2141-2162, 2267-2268</c>) — and all of it stated here with its
/// citation rather than hidden in the workflow.
/// </para>
/// <para>
/// <b>The failure is the donor's own, and it is not a no-op.</b> A pair the table states a burst for destroys
/// both ingredients and costs the mixing character the donor's own harm: ten to twenty hit points at the first
/// strength, thirty to a hundred at the second, fifty to two hundred and fifty at the third, and eradication
/// at the fourth (<c>src/GUI/UI/UIPopup.cpp:2114-2139</c>, over
/// <c>grng->random()</c>). The harm is rolled through the engine's keyed random service, so the same mixture
/// attempted twice in one session does not draw the same number by accident and a replay draws the same
/// numbers as the run it replays.
/// </para>
/// <para>
/// <b>The donor's own under-skilled mixture explodes; this game refuses it by name instead.</b> The donor
/// derives a burst strength from the rung a mixture's result asks for and lets a character who has not
/// reached it take the explosion (<c>src/GUI/UI/UIPopup.cpp:2092-2112</c>). This game refuses before anything
/// is spent and says which rung the result needs and what would raise the character's — the same shape its
/// skill ceilings and its casting refusals take — and keeps the donor's explosion for the pairs the table
/// itself states as incompatible. That difference is deliberate and is recorded here rather than rounded.
/// </para>
/// </remarks>
internal sealed class MightAndMagic7Alchemy : IAlchemyRule, PartyRpg.Kit.Presentation.IAlchemyKinds
{
    /// <summary>The definition kind the potion table is declared under.</summary>
    internal const string PotionDefinitionKind = "potion";

    /// <summary>The kind a row of the potion table has when it is a potion or the catalyst.</summary>
    internal const string PotionKind = "potion";

    /// <summary>The kind a row of the potion table has when it is the catalyst.</summary>
    internal const string CatalystKind = "catalyst";

    /// <summary>The kind a row of the potion table has when it is a reagent.</summary>
    internal const string ReagentKind = "reagent";

    /// <summary>The word a mixture's outcome carries when the pair does nothing at all.</summary>
    internal const string NoReaction = "none";

    /// <summary>The word a mixture's outcome carries when the pair goes off, before its strength.</summary>
    internal const string Burst = "burst";

    /// <summary>The skill mixing is judged by, which is the shipped skill table's own name for it.</summary>
    private static readonly SkillId Alchemy = new("Alchemy");

    /// <summary>How many rungs of the donor's ladder its four bands of mixtures ask for.</summary>
    private const int Rungs = 4;

    private readonly AlchemyCatalog _catalog;
    private readonly Dictionary<ItemDefinitionId, PotionRow> _potions;
    private readonly Dictionary<ItemDefinitionId, string> _names;
    private readonly MightAndMagic7Skills? _skills;
    private readonly IRandomService? _random;
    private readonly Func<PartyMember, bool>? _mayAct;

    private MightAndMagic7Alchemy(
        AlchemyCatalog catalog,
        Dictionary<ItemDefinitionId, PotionRow> potions,
        Dictionary<ItemDefinitionId, string> names,
        MightAndMagic7Skills? skills,
        IRandomService? random,
        Func<PartyMember, bool>? mayAct)
    {
        _catalog = catalog;
        _potions = potions;
        _names = names;
        _skills = skills;
        _random = random;
        _mayAct = mayAct;
    }

    /// <summary>The mixtures this game's own table states.</summary>
    internal AlchemyCatalog Catalog => _catalog;

    /// <summary>How many mixtures the table states, which is what a report counts.</summary>
    internal int MixtureCount => _catalog.Count;

    /// <summary>How many potions and reagents the table carries.</summary>
    internal int PotionCount => _potions.Count;

    /// <summary>The potion rows this game read, in content's own order.</summary>
    internal IReadOnlyList<PotionRow> Potions => [.. _potions.Values.OrderBy(row => row.Id)];

    /// <inheritdoc />
    public SkillId Skill => Alchemy;

    /// <summary>
    /// Reads this game's alchemy over the content the product loaded, or null when it loaded none.
    /// </summary>
    /// <param name="catalog">The validated content, or null when no bundle supplied any.</param>
    /// <param name="skills">This game's skill policy, so a refusal names a rung in the game's own words.</param>
    /// <param name="random">
    /// The engine's keyed random service, which a burst's harm is rolled through, or null when the product has
    /// none — a burst then does the least its strength states rather than drawing.
    /// </param>
    /// <param name="mayAct">
    /// Whether this game's own conditions leave a character able to act, as the fight's rule answers it, or
    /// null when the product composed no such answer — a character is then taken to be able to mix.
    /// </param>
    /// <returns>This game's alchemy, or null when there is no content to read it over.</returns>
    /// <exception cref="ContentValidationException">Content states a potion or a mixture this game cannot read; every problem is named.</exception>
    internal static MightAndMagic7Alchemy? Read(
        ContentCatalog? catalog,
        MightAndMagic7Skills? skills = null,
        IRandomService? random = null,
        Func<PartyMember, bool>? mayAct = null)
    {
        if (catalog is null) return null;

        List<ContentValidationIssue> issues = [];
        Dictionary<ItemDefinitionId, PotionRow> potions = [];
        Dictionary<ItemDefinitionId, string> names = [];
        Dictionary<ItemDefinitionId, int> tiers = [];

        foreach ((LoadedPack pack, ContentDocument document, ContentEntry entry) in catalog.Entries(PotionDefinitionKind))
        {
            void Defect(string code, string message) =>
                issues.Add(new ContentValidationIssue(code, message, pack.PackId, document.DocumentId));

            string name = entry.GetString("name").Trim();
            if (entry.Id.Length == 0 || name.Length == 0)
            {
                Defect("potion-incomplete", $"a potion entry states id '{entry.Id}' and name '{name}', and a potion needs both.");
                continue;
            }

            if (!int.TryParse(entry.Id, NumberStyles.None, CultureInfo.InvariantCulture, out int id))
            {
                Defect("potion-id-unreadable", $"potion '{entry.Id}' ({name}) is not a row number this game can read.");
                continue;
            }

            ItemDefinitionId definition = new(entry.Id);

            // A potion's tier is what its own mixing asks for. Content states it because the shipped table does
            // not carry it at all: it is the donor's four id bands
            // (OpenEnroth src/GUI/UI/UIPopup.cpp:2092-2112) written into the pack by the importer, and a game
            // whose mixtures ask for something else states that instead.
            int tier = entry.GetInt32("tier") ?? 0;
            if (tier < 0 || tier > Rungs)
            {
                Defect("potion-tier-unstated", $"potion '{entry.Id}' ({name}) states tier {tier}, and this game's ladder has {Rungs} rungs.");
                continue;
            }

            string kind = entry.GetString("kind").Trim();
            int power = entry.GetInt32("power") ?? 0;
            potions[definition] = new PotionRow(definition, id, name, kind, new SkillTier(tier), power)
            {
                MixtureCells = Cells(entry, "mixtures"),
                NoteCells = Notes(entry, "notes"),
            };
            names[definition] = name;
            tiers[definition] = tier;
        }

        // Every item the pack declares is named here too, because a mixture's message names both of its
        // ingredients and one of them may be an item this game states no potion row for.
        foreach ((_, _, ContentEntry entry) in catalog.Entries("item"))
        {
            string name = entry.GetString("name").Trim();
            if (name.Length > 0) names.TryAdd(new ItemDefinitionId(entry.Id), name);
        }

        List<PotionMixture> mixtures = [];
        HashSet<(string First, string Second)> seen = [];
        foreach (PotionRow row in potions.Values.OrderBy(row => row.Id))
        {
            foreach ((int other, string outcome) in row.MixtureCells)
            {
                // The matrix is symmetric, so the same pair arrives from both of its rows: the lower id's row
                // is the one that states it, and the other is checked against it rather than stated twice.
                (string first, string second) = row.Id <= other ? (row.Definition.Value, other.ToString(CultureInfo.InvariantCulture)) : (other.ToString(CultureInfo.InvariantCulture), row.Definition.Value);
                if (!seen.Add((first, second))) continue;
                if (!potions.TryGetValue(new ItemDefinitionId(second), out PotionRow twin))
                {
                    issues.Add(new ContentValidationIssue(
                        "mixture-row-unknown",
                        $"potion {row.Id} ({row.Name}) states a mixture with row {second}, which this content does not declare.",
                        PackId: string.Empty,
                        DocumentId: null));
                    continue;
                }

                if (twin.MixtureCells.TryGetValue(row.Id, out string? stated) && !string.Equals(stated, outcome, StringComparison.Ordinal))
                {
                    issues.Add(new ContentValidationIssue(
                        "mixture-asymmetric",
                        $"row {row.Id} states that mixing it with {second} does '{outcome}', and row {second} states '{stated}' for the same pair.",
                        PackId: string.Empty,
                        DocumentId: null));
                    continue;
                }

                if (Parse(row, outcome, tiers, row.NoteCell(other), out MixtureOutcome parsed, out int resultTier, out string? problem))
                {
                    mixtures.Add(new PotionMixture(
                        new ItemDefinitionId(first),
                        new ItemDefinitionId(second),
                        parsed,
                        resultTier == 0 ? SkillTier.None : new SkillTier(resultTier),
                        row.Power,
                        row.NoteCell(other)));
                }
                else
                {
                    issues.Add(new ContentValidationIssue("mixture-unreadable", problem!, PackId: string.Empty, DocumentId: null));
                }
            }
        }

        if (issues.Count > 0)
        {
            throw new ContentValidationException(
                $"This game's alchemy cannot be read: {issues[0].Message}",
                issues);
        }

        return new MightAndMagic7Alchemy(new AlchemyCatalog(mixtures), potions, names, skills, random, mayAct);
    }

    /// <summary>Turns one stated outcome into the outcome itself and the rung of the thing it makes.</summary>
    private static bool Parse(
        PotionRow row,
        string outcome,
        IReadOnlyDictionary<ItemDefinitionId, int> tiers,
        int note,
        out MixtureOutcome parsed,
        out int tier,
        out string? problem)
    {
        parsed = MixtureOutcome.Nothing;
        tier = 0;
        problem = null;

        if (string.Equals(outcome, NoReaction, StringComparison.OrdinalIgnoreCase))
        {
            parsed = MixtureOutcome.NoReaction();
            return true;
        }

        if (outcome.StartsWith(Burst, StringComparison.OrdinalIgnoreCase))
        {
            string strength = outcome[Burst.Length..].TrimStart(':');
            if (!int.TryParse(strength, NumberStyles.None, CultureInfo.InvariantCulture, out int level) || level < 1)
            {
                problem = $"potion {row.Id} ({row.Name}) states the mixture outcome '{outcome}', which is not a burst strength.";
                return false;
            }

            parsed = MixtureOutcome.Bursts(level);
            return true;
        }

        ItemDefinitionId result = new(outcome);
        if (!tiers.TryGetValue(result, out tier))
        {
            problem = $"potion {row.Id} ({row.Name}) states that mixing it makes '{outcome}', which this content does not declare as a potion.";
            return false;
        }

        parsed = MixtureOutcome.Produces(result);
        return true;
    }

    /// <inheritdoc />
    public string RungName(SkillTier tier) =>
        _skills is { } skills ? skills.TierName(tier) : tier.Value.ToString(CultureInfo.InvariantCulture);

    /// <inheritdoc />
    /// <remarks>
    /// The mastery lessons a counter offers raise a rung, which is the same thing this game's own skill ladders
    /// say everywhere else; the sentence names that rather than a number, because a player who reads a refusal
    /// has to know what to go and do.
    /// </remarks>
    public string MasteryRaisedBy(SkillId skill) =>
        $"a mastery lesson in {skill.Value} at a counter that teaches it raises the rung";

    /// <summary>How this game reads one definition's row, for a screen that draws the rows it is offered.</summary>
    /// <param name="definition">The definition to read.</param>
    /// <returns>The kind's word, empty when this game states no potion row for the definition.</returns>
    public string KindOf(ItemDefinitionId definition) =>
        _potions.TryGetValue(definition, out PotionRow row) ? row.Kind : string.Empty;

    /// <summary>The rung of Alchemy a potion's own mixture asks for, which content states.</summary>
    /// <param name="definition">The potion to read.</param>
    /// <returns>The rung, or none when this game states no potion row for the definition.</returns>
    internal SkillTier TierOf(ItemDefinitionId definition) =>
        _potions.TryGetValue(definition, out PotionRow row) ? row.Tier : SkillTier.None;

    /// <summary>
    /// The strength an item states, or null when this game states no potion row for its kind.
    /// </summary>
    /// <remarks>
    /// This is what the effect path reads where a spell reads a caster's school level, so a potion lands at
    /// the strength it was made at rather than at the mastery of whoever drank it. The floor is the same one
    /// the mixture's own arithmetic keeps: a potion that reached the party without a stated strength is read
    /// at one rather than at nothing, because a duration of nothing is not a duration.
    /// </remarks>
    /// <param name="item">The item the casting came from.</param>
    /// <returns>The strength, or null when the item is not one this game reads a strength for.</returns>
    internal int? PotencyOf(ItemInstance item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return _potions.ContainsKey(item.Definition) ? Math.Max(1, item.State.Potency) : null;
    }

    /// <inheritdoc />
    public string NameOf(ItemDefinitionId definition) =>
        _names.TryGetValue(definition, out string? name) && name.Length > 0 ? name : definition.Value;

    /// <inheritdoc />
    /// <remarks>
    /// The donor asks its own <c>CanAct</c> before it lets anybody handle an item at all
    /// (<c>OpenEnroth/src/Engine/Objects/Character.cpp:350-357</c>), and this reads the same answer the fight
    /// reads, so a character a creature's blow laid out cannot mix a potion in the middle of a fight either.
    /// </remarks>
    public PartyRefusal? MayMix(PartyMember mixer)
    {
        ArgumentNullException.ThrowIfNull(mixer);
        if (_mayAct is not { } mayAct || mayAct(mixer)) return null;
        return new PartyRefusal(
            "mixture-cannot-act",
            string.Create(CultureInfo.InvariantCulture, $"{mixer.Profile.Name} is in no condition to mix anything."));
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// The donor's own arithmetic, in its own three shapes: a reagent's mixture comes out at the reagent's
    /// stated power plus the mixer's level in Alchemy (<c>OpenEnroth/src/GUI/UI/UIPopup.cpp:2267</c>,
    /// <c>potionPower = alchemySkill.level() + reagent.GetReagentPower()</c>), two potions come out at the mean
    /// of the two strengths (<c>:2157</c>, <c>(picked + entry) / 2</c>), and the catalyst either takes the
    /// stronger of two catalysts or replaces the other potion's strength with its own
    /// (<c>:2145-2152</c>).
    /// </para>
    /// <para>
    /// Two catalysts at zero strength and a mixture of two potions that were never filled are the cases the
    /// floor is for: the donor's own potions are minted with a drawn strength and a mixture states one, but a
    /// potion that reached the party another way — bought, found, or handed over by content — may state none,
    /// and a strength of nothing would make a duration of nothing. One is the weakest strength the shipped
    /// table states at all (its first reagent is worth +1), which is ours and is stated here rather than
    /// guessed at the effect.
    /// </para>
    /// </remarks>
    public int Strength(PartyMember mixer, PotionMixture mixture, ItemInstance first, ItemInstance second)
    {
        ArgumentNullException.ThrowIfNull(mixer);
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        int level = Math.Max(0, mixer.Skills.LevelOf(Alchemy));
        bool firstCatalyst = IsCatalyst(first.Definition);
        bool secondCatalyst = IsCatalyst(second.Definition);

        if (firstCatalyst && secondCatalyst) return Math.Max(Floor, Math.Max(first.State.Potency, second.State.Potency));
        if (firstCatalyst) return Math.Max(Floor, first.State.Potency);
        if (secondCatalyst) return Math.Max(Floor, second.State.Potency);

        // A mixture the table states a power for is a reagent's own recipe: the stated power is the reagent's,
        // and the mixer's own level is added to it.
        if (mixture.Power > 0) return Math.Max(Floor, level + mixture.Power);

        return Math.Max(Floor, (first.State.Potency + second.State.Potency) / 2);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The donor's four bursts, with its own dice: <c>random(11) + 10</c>, <c>random(71) + 30</c>,
    /// <c>random(201) + 50</c>, and eradication instead of harm at the deepest strength
    /// (<c>OpenEnroth/src/GUI/UI/UIPopup.cpp:2118-2131</c>). The donor also breaks one, five, or all of the
    /// items the mixing character carries (<c>src/Engine/Objects/Character.cpp:327-347</c>,
    /// <c>ItemsPotionDmgBreak</c>); this build's items carry damage but nothing states what damage breaks one,
    /// so the burst's loss stops at the mixture's own ingredients and that breaking is named with the owner
    /// that would state it.
    /// </remarks>
    public MixtureBackfire Backfire(int strength)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(strength, 1);

        // Eradication is the donor's fourth band rather than a wound, and it is not a number of hit points:
        // the character is beyond dead until a temple's own cure reaches them.
        if (strength >= 4)
        {
            return new MixtureBackfire(
                Harm: 0,
                Label: "the mixture going off",
                MightAndMagic7Conditions.Eradicated);
        }

        (int dice, int flat) = strength switch
        {
            1 => (11, 10),
            2 => (71, 30),
            _ => (201, 50),
        };

        int harm = flat + Roll(strength, dice);
        return new MixtureBackfire(harm, "harm from the mixture going off");
    }

    /// <summary>
    /// One row's mixture cells, read from the object the pack writes the matrix as.
    /// </summary>
    /// <remarks>
    /// The pack mirrors the shipped table's own shape here: a row states what it makes with each other row,
    /// keyed by that row's id, rather than one entry per pair. A cell that is not a number is skipped, because
    /// the key it is written under is what names the other ingredient and a pack that wrote something else
    /// there would be a pack whose matrix cannot be read at all — <see cref="Read"/> names that when it tries
    /// to resolve the pair.
    /// </remarks>
    private static IReadOnlyDictionary<int, string> Cells(ContentEntry entry, string property)
    {
        Dictionary<int, string> cells = [];
        if (entry.Payload.ValueKind != System.Text.Json.JsonValueKind.Object ||
            !entry.Payload.TryGetProperty(property, out System.Text.Json.JsonElement matrix) ||
            matrix.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            return cells;
        }

        foreach (System.Text.Json.JsonProperty cell in matrix.EnumerateObject())
        {
            if (!int.TryParse(cell.Name, NumberStyles.None, CultureInfo.InvariantCulture, out int other)) continue;
            if (cell.Value.ValueKind == System.Text.Json.JsonValueKind.String) cells[other] = cell.Value.GetString() ?? string.Empty;
            else if (cell.Value.ValueKind == System.Text.Json.JsonValueKind.Number) cells[other] = cell.Value.GetRawText();
        }

        return cells;
    }

    /// <summary>The discovery one row's mixtures record, read from the same shape the matrix is written in.</summary>
    private static IReadOnlyDictionary<int, int> Notes(ContentEntry entry, string property)
    {
        Dictionary<int, int> notes = [];
        if (entry.Payload.ValueKind != System.Text.Json.JsonValueKind.Object ||
            !entry.Payload.TryGetProperty(property, out System.Text.Json.JsonElement matrix) ||
            matrix.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            return notes;
        }

        foreach (System.Text.Json.JsonProperty cell in matrix.EnumerateObject())
        {
            if (!int.TryParse(cell.Name, NumberStyles.None, CultureInfo.InvariantCulture, out int other)) continue;
            if (cell.Value.ValueKind == System.Text.Json.JsonValueKind.Number && cell.Value.TryGetInt32(out int note) && note > 0) notes[other] = note;
        }

        return notes;
    }

    /// <summary>The weakest strength a mixture is read at, which is the first reagent's own power.</summary>
    private const int Floor = 1;

    /// <summary>Whether a definition is the catalyst, which is what replaces a mixture's strength.</summary>
    private bool IsCatalyst(ItemDefinitionId definition) =>
        _potions.TryGetValue(definition, out PotionRow row) && string.Equals(row.Kind, CatalystKind, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// One burst's draw, made through the engine's keyed random service under a key that names the strength.
    /// </summary>
    /// <remarks>
    /// A product with no random service draws nothing and states the least its strength is worth, which is the
    /// same answer its own combat resolution gives when it cannot roll: a number that was invented would be a
    /// number no replay and no test could reproduce.
    /// </remarks>
    private int Roll(int strength, int dice)
    {
        if (_random is not { } random) return 0;
        string purpose = string.Create(CultureInfo.InvariantCulture, $"burst/{strength}");
        return (int)random.DrawKeyed(new KeyedRngRequest(Seed, RandomScope, purpose, 1, dice)).Value;
    }

    /// <summary>The scope this game's mixture draws live under, so they cannot collide with a fight's.</summary>
    private const string RandomScope = "mm7-alchemy";

    /// <summary>The seed the mixture draws live under.</summary>
    private const ulong Seed = 0x7A1C_3E5D_9B47_2061UL;

    /// <summary>The potions and reagents this game read from content, keyed by their definitions.</summary>
    internal readonly record struct PotionRow(
        ItemDefinitionId Definition,
        int Id,
        string Name,
        string Kind,
        SkillTier Tier,
        int Power)
    {
        /// <summary>What this row combines with, keyed by the other row's id.</summary>
        internal IReadOnlyDictionary<int, string> MixtureCells { get; init; } = new Dictionary<int, string>();

        /// <summary>The discovery each mixture records, keyed by the other row's id.</summary>
        internal IReadOnlyDictionary<int, int> NoteCells { get; init; } = new Dictionary<int, int>();

        /// <summary>The discovery one mixture records, zero when the table states none.</summary>
        internal int NoteCell(int other) => NoteCells.TryGetValue(other, out int note) ? note : 0;
    }
}
