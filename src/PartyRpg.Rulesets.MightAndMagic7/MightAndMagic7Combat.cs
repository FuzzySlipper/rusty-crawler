using System.Globalization;
using System.Text.Json;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using Rusty.Engine;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// This game's combat policy: how long an actor recovers after an attack, what hostility means, how far an
/// attack reaches, and what each actor is called.
/// </summary>
/// <remarks>
/// <para>
/// <b>The mechanism is the kit's and these are the numbers.</b> The kit owns the fight — who is in it, which
/// side they are on, whose recovery has elapsed, and what an accepted attack is — and this answers the
/// questions it asks, in this game's own terms. The two quantities a fight is paced by are the donor's: a
/// monster's recovery is the monster table's own column, and a character's is the attack recovery that
/// character's skills and attributes are worth.
/// </para>
/// <para>
/// <b>Recovery is stated in the donor's ticks and handed over as game time.</b> The donor measures recovery
/// in ticks of real time, 128 to the second, and runs its clock thirty game seconds to one real second
/// (OpenEnroth <c>src/Core/Time/Duration.h:28-29</c>). Every value here is converted through those two
/// donor constants, so what the kit advances is the same length of a game day the donor's recovery was
/// worth — a hundred-tick swing is about twenty-three game seconds, and at the shipped scale that is most of
/// a real second of play.
/// </para>
/// <para>
/// <b>What is approximated is stated.</b> The donor's character recovery is a sum of terms read off the
/// equipped figure — a weapon's skill, armour, a shield, enchantments, and haste — and this build's party
/// has no way to wear anything yet: no slot vocabulary, no equipment rule, and no item a character starts
/// with. What is left is the two terms a character's own body states — its speed, which the donor reads
/// through its attribute-bonus table, and the armsmaster skill — plus the unarmed base, which is the donor's
/// own branch for a character holding nothing. The equipment terms belong to the stone that brings items and
/// equipment, and their place is the sum below.
/// </para>
/// </remarks>
internal sealed class MightAndMagic7Combat : ICombatRule
{
    /// <summary>The definition kind a monster row is imported under.</summary>
    internal const string MonsterDefinitionKind = "monster";

    /// <summary>The placement kind a living creature stands in a place under.</summary>
    /// <remarks>
    /// <para>
    /// <b>This is the interface the monsters-and-AI owner fills.</b> A creature is a world entity the ruleset
    /// can name a monster row for, and the contract is two fields: the placement's kind is
    /// <c>monster</c> and its <c>monster</c> field names the row's id. Whoever creates the live entities says
    /// that much and nothing else is needed — the fight reads the row's hostility band, its recovery, and its
    /// name from the imported table, and paces the creature exactly as it paces a character. A creature
    /// carrying the row on a component of its engine actor instead needs one branch in
    /// <see cref="Creature(CombatSubject)"/>, which is the only place a creature is recognized.
    /// </para>
    /// <para>
    /// The shipped spawn placements are deliberately not creatures. A spawn point is where a level puts a
    /// creature and not the creature itself — the same distinction this game's camping rule states when it
    /// prices the risk of sleeping near one (see <see cref="MightAndMagic7Rest"/>) — and treating one as an
    /// enemy would put a fight into a place where nothing has been created yet.
    /// </para>
    /// </remarks>
    internal const string CreaturePlacementKind = "monster";

    /// <summary>The placement field that names the monster row a creature is.</summary>
    internal const string MonsterField = "monster";

    /// <summary>The monster row field that states its hostility band.</summary>
    internal const string HostilityField = "hostility";

    /// <summary>The monster row field that states its recovery, in the donor's ticks.</summary>
    internal const string RecoveryField = "recovery";

    /// <summary>The monster row field that states its name.</summary>
    internal const string NameField = "name";

    /// <summary>The placement kind a person standing in a place stands under.</summary>
    internal const string PersonPlacementKind = "person";

    /// <summary>The definition kind a person's own entry is declared under.</summary>
    internal const string PersonDefinitionKind = "person";

    /// <summary>The placement field that names the people standing at a placement.</summary>
    internal const string PeopleField = "people";

    /// <summary>The donor's own tick rate, which its recovery values are counted in.</summary>
    /// <remarks>OpenEnroth <c>src/Core/Time/Duration.h:28</c> — <c>TICKS_PER_REALTIME_SECOND = 128</c>.</remarks>
    internal const int TicksPerRealSecond = 128;

    /// <summary>
    /// How far off a creature notices the party, indexed by the monster table's hostility column.
    /// </summary>
    /// <remarks>
    /// <para>
    /// OpenEnroth <c>src/Engine/Objects/Actor.cpp:55-61</c> — <c>_4DF380_hostilityRanges</c>: friendly zero,
    /// then 1024, 2560, 5120, and 10240 world units as the bands widen. The donor uses the same number for
    /// two things, and so does this: what the creature is (a band of zero never starts a fight) and how far
    /// off it notices the party (the band itself, tested per axis in <c>Actor.cpp:2080-2090</c>). This game
    /// measures the distance between the same two points in a straight line rather than per axis, which is a
    /// slightly larger radius at the corners and the same fight everywhere else.
    /// </para>
    /// <para>
    /// Every one of the operator's 276 monster rows states a band of one to four, so every shipped creature
    /// is one that starts fights; a band of zero is the donor's friendly case and is honoured for content
    /// that states it. A band this game has no range for is a content defect refused where the policy is
    /// composed, rather than a creature that is silently armed with somebody else's distance.
    /// </para>
    /// </remarks>
    private static readonly double[] NoticeRanges = [0, 1024, 2560, 5120, 10240];

    /// <summary>How far a monster may reach, since the donor's own rows state no range.</summary>
    /// <remarks>
    /// A monster's melee reach is its own size in the donor rather than a number (<c>Actor.cpp</c> resolves
    /// it from the model and the target's radius), and nothing in this build carries a creature's size yet.
    /// The value is the donor's own search radius for the nearest actor, which is what its closest-target
    /// pick uses when nothing is aimed (OpenEnroth <c>src/Engine/Objects/Character.cpp:6347</c>,
    /// <c>FindClosestActor(5120, 0, 0)</c>), so a creature reaches exactly as far as the party's own act key
    /// searches.
    /// </remarks>
    private const double CreatureReach = 5120;

    /// <summary>How far hand-to-hand reaches, which is the donor's own melee range.</summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/Objects/Character.cpp:6378</c> — a melee attack lands while the distance to
    /// the actor, less its radius, is at most 407.2 world units. No radius is carried here yet, so the whole
    /// distance is compared against the donor's own number.
    /// </remarks>
    private const double MeleeReach = 407.2;

    /// <summary>How far a shot or a spell reaches when nothing more specific is known.</summary>
    /// <remarks>
    /// The donor does not range-check a bow shot: the act key shoots at whatever the closest-target pick
    /// found within 5120 units, and a wand is used the same way
    /// (OpenEnroth <c>src/Engine/Objects/Character.cpp:6311-6399</c>). This is that radius, so a shot reaches
    /// as far as the party can pick a target.
    /// </remarks>
    private const double RangedReach = 5120;

    /// <summary>The base recovery of a character holding nothing, in the donor's ticks.</summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/mm7_data.cpp:355-357</c> — <c>base_recovery_times_per_weapon_type</c>: the
    /// staff's hundred ticks is the base for staffs <em>and for unarmed combat without the unarmed skill</em>,
    /// which is exactly a character with nothing in hand.
    /// </remarks>
    private const int UnarmedBaseTicks = 100;

    /// <summary>The base recovery of a character who has learned to fight unarmed, in the donor's ticks.</summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/mm7_data.cpp:359-361</c> — sixty ticks, the dagger's own value, which the
    /// donor reads for a character whose unarmed skill is learned (<c>Character.cpp:1643-1644</c>).
    /// </remarks>
    private const int TrainedUnarmedBaseTicks = 60;

    /// <summary>The recovery floor for hand-to-hand, whatever the reductions add up to.</summary>
    /// <remarks>
    /// OpenEnroth <c>src/Application/GameConfig.h:205</c> — <c>minimum_recovery_melee</c>, thirty ticks.
    /// </remarks>
    private const int MinimumMeleeTicks = 30;

    /// <summary>The recovery floor for a shot or a spell.</summary>
    /// <remarks>
    /// OpenEnroth <c>src/Application/GameConfig.h:208,211</c> — <c>minimum_recovery_ranged</c> and
    /// <c>minimum_recovery_blasters</c>, five ticks each.
    /// </remarks>
    private const int MinimumRangedTicks = 5;

    /// <summary>The skill a character fights unarmed with, as the shipped skill table names it.</summary>
    internal static readonly SkillId UnarmedSkill = new("Unarmed");

    /// <summary>The skill that shortens a melee recovery, as the shipped skill table names it.</summary>
    internal static readonly SkillId ArmsmasterSkill = new("Armsmaster");

    /// <summary>The attribute that shortens every recovery, as the shipped attribute table names it.</summary>
    internal static readonly AttributeId SpeedAttribute = new("Speed");

    /// <summary>The rung the donor doubles the armsmaster reduction at.</summary>
    /// <remarks>OpenEnroth <c>src/Engine/Objects/Character.cpp:1710-1716</c> — grand master doubles it.</remarks>
    private const int GrandMasterRung = 4;

    /// <summary>
    /// The donor's attribute-bonus table: the attribute's own thresholds, and the ticks each is worth.
    /// </summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/Objects/Character.cpp:234-243</c> — <c>param_to_bonus_table</c> and
    /// <c>parameter_to_bonus_value</c> read by <c>GetParameterBonus</c>: the first threshold an attribute
    /// reaches decides the bonus, and the same table prices might, endurance, and speed. They are stated
    /// together here because the pairing is the table.
    /// </remarks>
    private static readonly int[] ParameterThresholds =
        [500, 400, 350, 300, 275, 250, 225, 200, 175, 150, 125, 100, 75, 50, 40, 35, 30, 25, 21, 19, 17, 15, 13, 11, 9, 7, 5, 3, 0];

    /// <summary>What each threshold above is worth, in ticks of recovery the character gets back.</summary>
    private static readonly int[] ParameterBonuses =
        [30, 25, 20, 19, 18, 17, 16, 15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1, 0, -1, -2, -3, -4, -5, -6];

    /// <summary>The seed every roll of this game's fights is drawn from.</summary>
    /// <remarks>
    /// The engine's random service takes an explicit seed and reads no wall clock, so the product states one.
    /// A constant is deliberate: a creature's first recovery is keyed by the place and the actor, so a fixed
    /// seed is what makes a fight reproducible rather than arbitrary, and one fight is still unrelated to the
    /// next because the key carries the actor.
    /// </remarks>
    internal const ulong RollSeed = 0x5C1E_7A11_5EE9_0002;

    /// <summary>The scope this game's first-recovery rolls are drawn under, so they cannot collide with another owner's.</summary>
    internal const string RollScope = "mm7.combat.initial-recovery";

    private readonly Dictionary<int, MonsterFacts> _monsters;
    private readonly Dictionary<string, string> _people;
    private readonly IRandomService? _random;

    private MightAndMagic7Combat(
        Dictionary<int, MonsterFacts> monsters,
        Dictionary<string, string> people,
        IRandomService? random)
    {
        _monsters = monsters;
        _people = people;
        _random = random;
    }

    /// <summary>
    /// Reads this game's monsters and people, and judges every creature the content places against them.
    /// </summary>
    /// <remarks>
    /// Everything is read and judged before anything fights, so a creature naming a monster row nothing
    /// describes fails while the session is being composed rather than at the moment the party walks up to
    /// it. A product with no content gets a policy with no monsters in it, which is a world where only the
    /// party and the people the packs carry are creatures.
    /// </remarks>
    /// <param name="catalog">The validated content the product loaded, when it loaded any.</param>
    /// <param name="random">
    /// The engine's random service, which a creature's first recovery is drawn from. Without one every
    /// creature takes its whole recovery before it first acts, which is deterministic and is the honest
    /// answer for a product that cannot draw.
    /// </param>
    /// <returns>This game's combat policy.</returns>
    /// <exception cref="ContentValidationException">Content declares a monster or a creature this game cannot fight; every problem is named.</exception>
    internal static MightAndMagic7Combat Compose(ContentCatalog? catalog, IRandomService? random)
    {
        if (catalog is null) return new MightAndMagic7Combat([], [], random);
        List<ContentValidationIssue> issues = [];
        Dictionary<int, MonsterFacts> monsters = ReadMonsters(catalog, issues);
        Dictionary<string, string> people = ReadPeople(catalog);
        ValidateCreatures(catalog, monsters, issues);

        if (issues.Count > 0)
        {
            throw new ContentValidationException(
                $"This game's monsters cannot be fought: {issues[0].Message}",
                issues);
        }

        return new MightAndMagic7Combat(monsters, people, random);
    }

    /// <summary>How many monster rows this policy can fight.</summary>
    internal int MonsterCount => _monsters.Count;

    /// <inheritdoc />
    public string NameOf(CombatSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);
        if (subject.Member is { } member) return member.Profile.Name;
        if (Creature(subject) is { } creature) return creature.Name;
        if (string.Equals(subject.Placement?.Content.Kind, PersonPlacementKind, StringComparison.Ordinal))
        {
            return PersonName(subject) ?? "Somebody";
        }

        // A subject that is not a creature is never a combatant, so this is reached only by a caller asking
        // about something inert. Naming the placement is more useful than naming nothing.
        return subject.Placement?.Content.Id ?? string.Empty;
    }

    /// <inheritdoc />
    /// <remarks>
    /// A member of the party is never hostile to it; a person is a creature that does not start fights, and
    /// is what the party turns into an enemy by attacking them; a creature is hostile as its row's band says.
    /// Anything else — a door, a chest, a light — is not a creature at all.
    /// </remarks>
    public Hostility NatureOf(CombatSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);
        if (subject.Member is not null) return Hostility.Peaceful;
        if (Creature(subject) is { } creature)
        {
            // Band zero is the donor's friendly creature: something that walks the world and never starts a
            // fight, which the party can still attack.
            return creature.NoticeRange <= 0
                ? Hostility.Peaceful
                : Hostility.Aggressive(creature.NoticeRange);
        }

        return string.Equals(subject.Placement?.Content.Kind, PersonPlacementKind, StringComparison.Ordinal)
            ? Hostility.Peaceful
            : Hostility.Inert;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Hand-to-hand, for everybody, and that is a statement about this build rather than about the game: a
    /// member with a bow or a wand in hand would shoot with it and a monster whose row carries a missile
    /// would throw one, and neither is reachable yet — the party cannot wear anything, and a creature's row
    /// reaches the fight as a name, a band, and a recovery. The three kinds are the kit's, and this is where
    /// each one becomes reachable.
    /// </remarks>
    public AttackKind AttackKindFor(CombatSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);
        return AttackKind.Melee;
    }

    /// <inheritdoc />
    /// <remarks>
    /// A monster recovers for as long as its row says, whatever it does: the donor paces a creature's every
    /// attack from one value and does not vary it by kind (OpenEnroth <c>src/Engine/Objects/Monsters.h:80</c>,
    /// <c>src/Engine/Objects/Actor.cpp:1295-1300</c>). A person and a member are paced by the character
    /// formula below, which is the same one the donor uses for a character with nothing in hand.
    /// </remarks>
    public GameDuration RecoveryAfter(CombatSubject subject, AttackKind kind)
    {
        ArgumentNullException.ThrowIfNull(subject);
        if (Creature(subject) is { } creature) return creature.Recovery;
        return CharacterRecovery(subject.Member, kind);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// A party member begins ready: it has made no attack and owes nothing for one. A creature begins part
    /// way through its first recovery, drawn from the engine's random service over the actor's own identity,
    /// because a group placed together would otherwise notice the party and strike on the same update
    /// forever. That is the donor's own reason for randomising a monster's first initiative
    /// (OpenEnroth <c>src/Engine/TurnEngine/TurnEngine.cpp:166-172</c>, where a monster's queue order is
    /// drawn and a character's is not).
    /// </para>
    /// <para>
    /// The draw is keyed by the place and the actor, so the same creature in the same place always begins the
    /// same way — a fight is reproducible, and nothing about it has to be recorded for a save. Without a
    /// random service nothing is drawn and the creature takes its whole recovery.
    /// </para>
    /// </remarks>
    public GameDuration InitialRecovery(CombatSubject subject, AttackKind kind)
    {
        ArgumentNullException.ThrowIfNull(subject);
        GameDuration recovery = RecoveryAfter(subject, kind);
        if (subject.Member is not null || _random is null) return recovery;

        string key = string.Create(CultureInfo.InvariantCulture, $"{subject.Place}/{subject.Id}");
        long ticks = _random
            .DrawKeyed(new KeyedRngRequest(RollSeed, RollScope, key, 0, recovery.Milliseconds))
            .Value;
        return GameDuration.FromMilliseconds(ticks);
    }

    /// <inheritdoc />
    /// <remarks>
    /// A creature reaches as far as the donor's closest-target pick searches, and a character reaches as far
    /// as the kind of attack it makes: hand-to-hand at arm's length, a shot or a spell as far as the party
    /// can pick anything to aim at.
    /// </remarks>
    public double ReachOf(CombatSubject subject, AttackKind kind)
    {
        ArgumentNullException.ThrowIfNull(subject);
        if (Creature(subject) is not null) return CreatureReach;
        return kind == AttackKind.Melee ? MeleeReach : RangedReach;
    }

    /// <summary>
    /// What a character's attack recovery is worth, in the donor's sum and floored at the donor's minimum.
    /// </summary>
    /// <remarks>
    /// The donor's sum is the weapon's base plus armour and shield, less the armsmaster reduction, the
    /// weapon's own enchantment, haste, the weapon skill's expert reduction, and the speed bonus
    /// (OpenEnroth <c>src/Engine/Objects/Character.cpp:1636-1750</c>). Of those, this build can state three:
    /// the base for a character holding nothing, the armsmaster reduction, and the speed bonus. The rest are
    /// read off an equipped figure this product cannot fill yet, and they belong to the stone that brings
    /// items and equipment — this sum is where they go, in the donor's own order.
    /// </remarks>
    private static GameDuration CharacterRecovery(PartyMember? member, AttackKind kind)
    {
        int ticks = UnarmedBaseTicks;
        if (member is not null)
        {
            if (member.Skills.TryGet(UnarmedSkill, out SkillEntry unarmed) && unarmed.Level > 0)
            {
                ticks = TrainedUnarmedBaseTicks;
            }

            // Armsmaster shortens a melee recovery only, and only for a character who has learned it; a
            // grand master gets twice its level back (Character.cpp:1710-1716).
            if (kind == AttackKind.Melee &&
                member.Skills.TryGet(ArmsmasterSkill, out SkillEntry armsmaster) &&
                armsmaster.Level > 0)
            {
                ticks -= armsmaster.Tier.Value >= GrandMasterRung ? armsmaster.Level * 2 : armsmaster.Level;
            }

            ticks -= SpeedBonus(member.Attributes[SpeedAttribute]);
        }

        int minimum = kind == AttackKind.Melee ? MinimumMeleeTicks : MinimumRangedTicks;
        if (ticks < minimum) ticks = minimum;
        return Ticks(ticks);
    }

    /// <summary>What an attribute is worth in recovery ticks, by the donor's own threshold table.</summary>
    private static int SpeedBonus(int attribute)
    {
        for (int index = 0; index < ParameterThresholds.Length; index++)
        {
            if (attribute >= ParameterThresholds[index]) return ParameterBonuses[index];
        }

        return 0;
    }

    /// <summary>
    /// Reads the monster table the packs carry, refusing a row a fight could not be paced by.
    /// </summary>
    /// <remarks>
    /// A row with no id, a repeated id, a hostility band this game has no range for, or a recovery that is no
    /// time at all is a defect named where the session is composed: a creature nobody can pace is a fight
    /// that either never acts or acts forever.
    /// </remarks>
    private static Dictionary<int, MonsterFacts> ReadMonsters(ContentCatalog catalog, List<ContentValidationIssue> issues)
    {
        Dictionary<int, MonsterFacts> monsters = [];
        foreach ((LoadedPack pack, ContentDocument document, ContentEntry entry) in catalog.Entries(MonsterDefinitionKind))
        {
            void Defect(string code, string message) =>
                issues.Add(new ContentValidationIssue(code, message, pack.PackId, document.DocumentId));

            if (!int.TryParse(entry.Id, NumberStyles.None, CultureInfo.InvariantCulture, out int id))
            {
                Defect("monster-identity-unreadable", $"monster '{entry.Id}' is not a number, so no creature placement could name it as its row.");
                continue;
            }

            if (monsters.ContainsKey(id))
            {
                Defect("monster-identity-reused", $"monster row {id} is declared more than once, so which creature a placement names would be ambiguous.");
                continue;
            }

            int hostility = entry.GetInt32(HostilityField) ?? 0;
            if (hostility < 0 || hostility >= NoticeRanges.Length)
            {
                Defect(
                    "monster-hostility-unknown",
                    $"monster '{entry.GetString(NameField)}' ({id}) states hostility {hostility}, and this game has a notice range for 0 to {NoticeRanges.Length - 1} only.");
                continue;
            }

            int recovery = entry.GetInt32(RecoveryField) ?? 0;
            if (recovery <= 0)
            {
                Defect(
                    "monster-recovery-missing",
                    $"monster '{entry.GetString(NameField)}' ({id}) states a recovery of {recovery}, so nothing could decide when it acts again.");
                continue;
            }

            monsters[id] = new MonsterFacts(
                id,
                entry.GetString(NameField),
                recovery,
                hostility == 0 ? 0 : NoticeRanges[hostility]);
        }

        return monsters;
    }

    /// <summary>Reads the people the packs carry, by the identity a placement names them under.</summary>
    private static Dictionary<string, string> ReadPeople(ContentCatalog catalog)
    {
        Dictionary<string, string> people = new(StringComparer.Ordinal);
        foreach ((_, _, ContentEntry entry) in catalog.Entries(PersonDefinitionKind))
        {
            string name = entry.GetString(NameField);
            if (name.Length > 0) people[entry.Id] = name;
        }

        return people;
    }

    /// <summary>Judges every creature the content places against the monster rows this game carries.</summary>
    private static void ValidateCreatures(
        ContentCatalog catalog,
        Dictionary<int, MonsterFacts> monsters,
        List<ContentValidationIssue> issues)
    {
        foreach ((LoadedPack pack, ContentDocument document, ContentEntry place) in catalog.Entries(PlaceGraphLoader.PlaceDefinitionKind))
        {
            foreach (JsonElement placement in place.GetArray("placements"))
            {
                if (!string.Equals(ContentEntry.ReadString(placement, "kind"), CreaturePlacementKind, StringComparison.Ordinal)) continue;
                string named = ContentEntry.ReadString(placement, MonsterField);
                if (!int.TryParse(named, NumberStyles.None, CultureInfo.InvariantCulture, out int id) || !monsters.ContainsKey(id))
                {
                    issues.Add(new ContentValidationIssue(
                        "creature-monster-unknown",
                        $"place '{place.Id}' places a creature naming monster '{named}', which no monster row in this content describes, so nothing could say how hard it hits or how fast it recovers.",
                        pack.PackId,
                        document.DocumentId));
                }
            }
        }
    }

    /// <summary>The monster row a world entity is a creature of, or null when it is not one.</summary>
    /// <remarks>
    /// This is the one place a creature is recognized, and therefore the one place the monsters-and-AI owner
    /// has to satisfy: a placement of the creature kind naming a monster row. A row the content does not
    /// carry is refused when the policy is composed, so nothing that reaches here can be missing from the
    /// table.
    /// </remarks>
    private MonsterFacts? Creature(CombatSubject subject)
    {
        if (subject.Placement is not { } placement) return null;
        if (!string.Equals(placement.Content.Kind, CreaturePlacementKind, StringComparison.Ordinal)) return null;
        string named = placement.Source.GetString(MonsterField);
        return int.TryParse(named, NumberStyles.None, CultureInfo.InvariantCulture, out int id)
            ? _monsters.GetValueOrDefault(id)
            : null;
    }

    /// <summary>The name of the person a placement holds, or null when nothing here names one.</summary>
    /// <remarks>
    /// A person placement names the people standing there by the identity their own entry was imported
    /// under, so the name comes from the people document rather than from the placement: one person can
    /// stand in more than one place, and a copy of their name per placement would be that many names.
    /// </remarks>
    private string? PersonName(CombatSubject subject)
    {
        if (subject.Placement is not { } placement) return null;
        foreach (JsonElement person in placement.Source.GetArray(PeopleField))
        {
            string id = person.ValueKind == JsonValueKind.String
                ? person.GetString() ?? string.Empty
                : ContentEntry.ReadString(person, "id");
            if (id.Length > 0 && _people.TryGetValue(id, out string? name)) return name;
        }

        return null;
    }

    /// <summary>Turns a value in the donor's ticks into the game time the kit advances recovery by.</summary>
    /// <remarks>
    /// The donor's two constants, in the one place they meet: 128 ticks to a real second and thirty game
    /// seconds to a real second, so a tick is 234.375 milliseconds of game time
    /// (OpenEnroth <c>src/Core/Time/Duration.h:28-29</c>). A half-millisecond is rounded to the nearest
    /// millisecond rather than truncated, because truncating every tick would make a hundred-tick swing two
    /// milliseconds shorter than the donor's own.
    /// </remarks>
    private static GameDuration Ticks(int ticks) =>
        GameDuration.FromMilliseconds(
            (long)Math.Round(
                ticks * (double)GameDuration.MillisecondsPerSecond * MightAndMagic7Time.Scale.GameSecondsPerRealSecond / TicksPerRealSecond,
                MidpointRounding.AwayFromZero));

    /// <summary>What one monster row is worth to a fight: its name, its recovery, and how far it notices.</summary>
    /// <param name="Id">The row's identity, which a creature placement names.</param>
    /// <param name="Name">The row's name, which the panel shows.</param>
    /// <param name="RecoveryTicks">The row's recovery, in the donor's ticks, as it was read.</param>
    /// <param name="NoticeRange">How far off the creature notices the party, zero when it starts no fights.</param>
    private sealed record MonsterFacts(int Id, string Name, int RecoveryTicks, double NoticeRange)
    {
        /// <summary>The row's recovery as the game time a fight advances by.</summary>
        public GameDuration Recovery { get; } = Ticks(RecoveryTicks);
    }
}
