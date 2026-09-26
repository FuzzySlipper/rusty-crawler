using System.Text.Json;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.World;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// Composes this game's party from the content the product loaded.
/// </summary>
/// <remarks>
/// <para>
/// <b>A party is created from content, and only then.</b> Members, their races and classes, their starting
/// scores, and what the purse and the larder hold are scenario state and belong to a pack, not to a
/// compiled ruleset: a ruleset that filled in four adventurers of its own would be inventing the party the
/// player is supposed to build. Content that declares no party therefore runs with none, exactly as the
/// world runs with no places when content declares none, and nothing here fabricates one.
/// </para>
/// <para>
/// <b>This is the scenario's path, not the creation screen.</b> <see cref="MightAndMagic7Creation"/> is the
/// player-facing flow, whose members are chosen a step at a time and which a session will hold while a
/// party is being created. A scenario that fixes the party — a scripted start, a live check, a test — is
/// not a conversation with a player, so it states its members directly, and this reads that document. Both
/// paths end at the same <see cref="PartyEntityFactory"/> over the same
/// <see cref="PartyCreation"/>, so there is one way a party comes into being and two ways it is described.
/// </para>
/// <para>
/// <b>Content that declares a party it cannot build is a defect.</b> An entry with no members, a member
/// without a name, a race, a class, or hit points, a starting amount that is not a whole number, or a
/// duplicate attribute, skill, or condition is refused by name while the session is being composed. A
/// party that silently lost a member would be a game that starts one character short, which is far harder
/// to notice than a load that states what is wrong.
/// </para>
/// <para>
/// What a race or class identity resolves to — its modifiers, its permitted equipment, its skill ceilings
/// — is the ruleset's later work over the definitions content carries. Creation records the identities the
/// scenario named; nothing here pretends to have resolved them.
/// </para>
/// </remarks>
internal static class MightAndMagic7Party
{
    /// <summary>The definition kind a scenario's party entry uses.</summary>
    internal const string DefinitionKind = "scenario-party";

    /// <summary>The field naming the members a party is created from.</summary>
    internal const string MembersField = "members";

    /// <summary>
    /// Reads the party a scenario declares, or null when no content declares one.
    /// </summary>
    /// <param name="catalog">The validated content the product loaded, when it loaded any.</param>
    /// <returns>The created party, or null when content supplies nothing to create one from.</returns>
    /// <exception cref="ContentValidationException">Content declares a party that cannot be created, naming every problem found.</exception>
    internal static PartyEntity? Compose(ContentCatalog? catalog)
    {
        if (catalog is null) return null;
        foreach ((LoadedPack pack, ContentDocument document, ContentEntry entry) in catalog.Entries(DefinitionKind))
        {
            // The first entry wins, exactly as the scenario's starting place does: two parties would leave
            // which band the player leads to the order the packs happened to load in.
            return Create(pack, document, entry);
        }

        return null;
    }

    /// <summary>Creates the party one content entry describes.</summary>
    /// <exception cref="ContentValidationException">The entry does not describe a party that can be created.</exception>
    private static PartyEntity Create(LoadedPack pack, ContentDocument document, ContentEntry entry)
    {
        List<ContentValidationIssue> issues = [];
        void Defect(string code, string message) =>
            issues.Add(new ContentValidationIssue(code, message, pack.PackId, document.DocumentId));

        List<MemberCreation> members = Members(entry, Defect);
        if (members.Count == 0)
        {
            Defect(
                "party-members-missing",
                $"party '{entry.Id}' declares no members, and a party holds at least one; a session with nobody to lead is not a party to create.");
        }

        // Every value is read before anything is created: a party that is built from a defective entry
        // would be half this scenario and half a fallback, and the defects are what the caller has to see.
        int coins = Amount(entry, "coins", 0, Defect);
        int food = Amount(entry, "food", 0, Defect);
        int reputation = Amount(entry, "reputation", 0, Defect);
        int fame = Amount(entry, "fame", 0, Defect);

        if (issues.Count > 0)
        {
            throw new ContentValidationException(
                $"The scenario's party cannot be created: {issues[0].Message}",
                issues);
        }

        // The rules the party obeys are the item and skill owners' policy — equipment gating, pack
        // capacity, stacking, the hired limit — and none of them exists yet. A factory composed with no
        // rule gates nothing, which is the honest state of a product whose ruleset has not answered: no
        // constant here stands in for an answer nobody gave.
        return Factory().Create(new PartyCreation(
            members,
            coins,
            food,
            ProvisionUnit.Portions,
            reputation,
            fame));
    }

    /// <summary>
    /// Rebuilds a party from a save the product wrote.
    /// </summary>
    /// <remarks>
    /// A load composes the party through the same factory a creation does, so the two ends of a session's
    /// life build the same shape: the rules a party obeys are policy and are supplied here rather than
    /// carried in a save, and what the save recorded is not re-judged.
    /// </remarks>
    /// <param name="save">The recorded party.</param>
    /// <returns>The restored party, owning the store its entities live in.</returns>
    /// <exception cref="ArgumentException">The save cannot be rebuilt; the message names every problem found.</exception>
    internal static PartyEntity Restore(PartySave save) => Factory().Restore(save);

    /// <summary>
    /// The rules every party of this game obeys, composed in one place so a created party and a loaded one
    /// are the same party.
    /// </summary>
    /// <remarks>
    /// Equipment gating, pack capacity, stacking, and the hired limit are the item and skill owners' policy
    /// and none of them exists yet, so this factory gates nothing yet. What it does state is what a wound
    /// leaves on a character, because that answer must reach both creation and a load: a member built by
    /// either path obeys the same thresholds, and a trap sprung in the world lands on the same health rule a
    /// creature's bite does. It is one method rather than two call sites so that when the remaining rules
    /// land they land for both paths at once, instead of one of the two quietly keeping an older answer.
    /// </remarks>
    internal static PartyEntityFactory Factory() => new(health: new MightAndMagic7Health());

    /// <summary>Reads the members a party entry declares, reporting every one it cannot read.</summary>
    private static List<MemberCreation> Members(ContentEntry entry, Action<string, string> defect)
    {
        List<MemberCreation> members = [];
        int position = 0;
        foreach (JsonElement element in Elements(entry.Payload, MembersField))
        {
            position++;
            PartyMemberSeed? seed = Seed(element, position, defect);
            if (seed is not null) members.Add(new MemberCreation(seed));
        }

        return members;
    }

    /// <summary>Reads one member the scenario describes.</summary>
    private static PartyMemberSeed? Seed(JsonElement element, int position, Action<string, string> defect)
    {
        string name = ContentEntry.ReadString(element, "name");
        string race = ContentEntry.ReadId(element, "race");
        string characterClass = ContentEntry.ReadId(element, "class");
        if (name.Length == 0 || race.Length == 0 || characterClass.Length == 0)
        {
            defect(
                "party-member-incomplete",
                $"member {position} does not name a name, a race, and a class, which are the three identities a character is created from.");
            return null;
        }

        // Hit points are required because a character with none is a corpse, and creation is not where a
        // game decides what a character's health should have been. Everything else a member can start with
        // is what a scenario may leave out, and the fallbacks are the values creation itself defaults to
        // rather than numbers invented here.
        if (Required(element, "hitPoints", position, defect) is not { } hitPoints) return null;

        return new PartyMemberSeed(
            name,
            new RaceId(race),
            new ClassId(characterClass),
            Attributes(element, position, defect),
            Skills(element, position, defect),
            Spells(element, position, defect),
            Count(element, "experience", 0, 0, position, defect),
            Count(element, "level", 1, 1, position, defect),
            Count(element, "skillPoints", 0, 0, position, defect),
            Count(element, "classRank", 1, 1, position, defect),
            Conditions(element, position, defect),
            ResourcePool.Full(hitPoints),
            ResourcePool.Full(Count(element, "spellPoints", 0, 0, position, defect)),
            Portrait(element));
    }

    /// <summary>Reads the portrait a scenario gives a member, or null when it states none.</summary>
    /// <remarks>
    /// A scenario that fixes the party is not the creation flow, so it is not obliged to state a face; when it
    /// does, that portrait travels with the member into the party and into a save exactly as a chosen one
    /// does, because the party records the portrait rather than where it came from.
    /// </remarks>
    private static PortraitId? Portrait(JsonElement element)
    {
        string portrait = ContentEntry.ReadId(element, "portrait");
        return portrait.Length == 0 ? null : new PortraitId(portrait);
    }

    /// <summary>Reads a member's attribute scores.</summary>
    private static List<AttributeScore> Attributes(JsonElement element, int position, Action<string, string> defect)
    {
        List<AttributeScore> attributes = [];
        HashSet<AttributeId> seen = [];
        foreach (JsonElement declared in Elements(element, "attributes"))
        {
            string id = ContentEntry.ReadId(declared, "id");
            if (id.Length == 0 || ContentEntry.ReadDouble(declared, "value") is not { } value)
            {
                defect("party-attribute-incomplete", $"member {position} declares an attribute without an id and a value.");
                continue;
            }

            AttributeId attribute = new(id);
            if (!seen.Add(attribute))
            {
                defect("party-attribute-duplicate", $"member {position} declares attribute '{id}' more than once, so which score it has would be ambiguous.");
                continue;
            }

            attributes.Add(new AttributeScore(attribute, (int)value));
        }

        return attributes;
    }

    /// <summary>Reads a member's skills, each at the level and tier the scenario states.</summary>
    private static List<SkillEntry> Skills(JsonElement element, int position, Action<string, string> defect)
    {
        List<SkillEntry> skills = [];
        HashSet<SkillId> seen = [];
        foreach (JsonElement declared in Elements(element, "skills"))
        {
            string id = ContentEntry.ReadId(declared, "id");
            int level = (int)(ContentEntry.ReadDouble(declared, "level") ?? 1);
            if (id.Length == 0 || level < 1)
            {
                defect(
                    "party-skill-incomplete",
                    $"member {position} declares a skill without an id and at least one level; a skill the character has learned has a level.");
                continue;
            }

            SkillId skill = new(id);
            if (!seen.Add(skill))
            {
                defect("party-skill-duplicate", $"member {position} declares skill '{id}' more than once, so which level it has would be ambiguous.");
                continue;
            }

            double pointsSpent = ContentEntry.ReadDouble(declared, "pointsSpent") ?? 0;
            if (pointsSpent < 0)
            {
                defect("party-skill-invalid", $"member {position} declares skill '{id}' with {pointsSpent} points spent, which cannot be negative.");
                continue;
            }

            skills.Add(new SkillEntry(
                skill,
                level,
                new SkillTier((int)(ContentEntry.ReadDouble(declared, "tier") ?? 0)),
                (int)pointsSpent));
        }

        return skills;
    }

    /// <summary>Reads the spells a member knows.</summary>
    private static List<SpellId> Spells(JsonElement element, int position, Action<string, string> defect)
    {
        List<SpellId> spells = [];
        foreach (JsonElement declared in Elements(element, "spells"))
        {
            string id = ContentEntry.ReadId(declared, "id");
            if (id.Length == 0)
            {
                // A bare string is what a list of spell names naturally reads as, so both forms are read
                // rather than refusing the shorter one a scenario author would reach for.
                id = declared.ValueKind == JsonValueKind.String ? declared.GetString() ?? string.Empty : string.Empty;
            }

            if (id.Length == 0)
            {
                defect("party-spell-incomplete", $"member {position} declares a spell with no identity.");
                continue;
            }

            spells.Add(new SpellId(id));
        }

        return spells;
    }

    /// <summary>Reads the conditions a member carries at creation, which a scenario may start one weakened with.</summary>
    private static List<ActiveCondition> Conditions(JsonElement element, int position, Action<string, string> defect)
    {
        List<ActiveCondition> conditions = [];
        HashSet<ConditionId> seen = [];
        foreach (JsonElement declared in Elements(element, "conditions"))
        {
            string id = ContentEntry.ReadId(declared, "id");
            if (id.Length == 0)
            {
                defect("party-condition-incomplete", $"member {position} declares a condition with no identity.");
                continue;
            }

            ConditionId condition = new(id);
            if (!seen.Add(condition))
            {
                defect("party-condition-duplicate", $"member {position} carries condition '{id}' twice, so which severity is acting would be ambiguous.");
                continue;
            }

            conditions.Add(new ActiveCondition(condition, (int)(ContentEntry.ReadDouble(declared, "severity") ?? 0)));
        }

        return conditions;
    }

    /// <summary>Reads a whole-number field a member must have, reporting one that is missing or not a count.</summary>
    private static int? Required(JsonElement element, string field, int position, Action<string, string> defect)
    {
        double? value = ContentEntry.ReadDouble(element, field);
        if (value is null)
        {
            defect("party-member-incomplete", $"member {position} states no '{field}', so the character cannot be created.");
            return null;
        }

        if (value < 0 || value != Math.Floor(value.Value))
        {
            defect("party-member-invalid", $"member {position} states {value} for '{field}', which is not a whole count.");
            return null;
        }

        return (int)value.Value;
    }

    /// <summary>
    /// Reads a whole-number field a member may leave out, which falls back to the value creation defaults to.
    /// </summary>
    /// <param name="element">The member the scenario declares.</param>
    /// <param name="field">The field to read.</param>
    /// <param name="fallback">What the member starts with when the scenario states nothing.</param>
    /// <param name="minimum">The smallest value that means anything, which a stated value below is a defect against.</param>
    /// <param name="position">Which member this is, so a defect names it.</param>
    /// <param name="defect">Where a defect is reported.</param>
    private static int Count(JsonElement element, string field, int fallback, int minimum, int position, Action<string, string> defect)
    {
        double? value = ContentEntry.ReadDouble(element, field);
        if (value is null) return fallback;
        if (value < minimum || value != Math.Floor(value.Value))
        {
            defect(
                "party-member-invalid",
                $"member {position} states {value} for '{field}', which is not a whole count of at least {minimum}.");
            return fallback;
        }

        return (int)value.Value;
    }

    /// <summary>Reads a whole-number field of the party itself, or the fallback when it is absent.</summary>
    private static int Amount(ContentEntry entry, string field, int fallback, Action<string, string> defect)
    {
        if (!entry.Has(field)) return fallback;
        if (entry.GetInt32(field) is not { } value)
        {
            defect("party-value-invalid", $"party '{entry.Id}' states its '{field}' as something other than a whole number.");
            return fallback;
        }

        if (value < 0)
        {
            defect("party-value-invalid", $"party '{entry.Id}' starts its '{field}' at {value}, which cannot be negative.");
            return fallback;
        }

        return value;
    }

    /// <summary>Reads an array property of a JSON element, or nothing when it is absent or not an array.</summary>
    private static IReadOnlyList<JsonElement> Elements(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(property, out JsonElement value) &&
        value.ValueKind == JsonValueKind.Array
            ? [.. value.EnumerateArray()]
            : [];
}
