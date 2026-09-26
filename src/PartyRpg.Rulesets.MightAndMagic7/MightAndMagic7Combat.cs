using System.Globalization;
using System.Text.Json;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Magic;
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
internal sealed class MightAndMagic7Combat : ICombatRule, ICombatResolutionRule, ICombatAbilityResolutionRule, IFallenCreatureObserver
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

    /// <summary>The monster row field that states its AI class, which is what decides whether it runs.</summary>
    internal const string AiTypeField = "aiType";

    /// <summary>The monster row field that states how the creature moves.</summary>
    internal const string MovementField = "movement";

    /// <summary>The monster row field that states how fast it covers ground.</summary>
    internal const string SpeedField = "speed";

    /// <summary>The definition kind a spell's own entry is declared under.</summary>
    internal const string SpellDefinitionKind = "spell";

    /// <summary>The spell entry field that states the kind of harm the spell does.</summary>
    internal const string SpellResistField = "resist";

    /// <summary>The placement kind a person standing in a place stands under.</summary>
    internal const string PersonPlacementKind = "person";

    /// <summary>The definition kind a person's own entry is declared under.</summary>
    internal const string PersonDefinitionKind = "person";

    /// <summary>The placement field that names the people standing at a placement.</summary>
    internal const string PeopleField = "people";

    /// <summary>The monster row field that states its level.</summary>
    internal const string LevelField = "level";

    /// <summary>
    /// The monster row field that states what bringing it down is worth to the party.
    /// </summary>
    /// <remarks>
    /// The importer writes the shipped table's own experience column here
    /// (<c>src/Engine/Objects/Monsters.h:78</c>, <c>exp</c>), which is the number the donor awards on a kill
    /// (<c>src/Engine/Objects/Actor.cpp:3164-3167</c>).
    /// </remarks>
    internal const string ExperienceField = "experience";

    /// <summary>The monster row field that states its hit points.</summary>
    internal const string HitPointsField = "hitPoints";

    /// <summary>The monster row field that states its armor class.</summary>
    internal const string ArmorClassField = "armorClass";

    /// <summary>The name this game gives a creature's first attack, as an order and a resolution both spell it.</summary>
    internal const string AbilityAttack1 = "attack1";

    /// <summary>The name this game gives a creature's second attack.</summary>
    internal const string AbilityAttack2 = "attack2";

    /// <summary>The name this game gives the first spell a creature casts.</summary>
    internal const string AbilitySpell1 = "spell1";

    /// <summary>The name this game gives the second spell a creature casts.</summary>
    internal const string AbilitySpell2 = "spell2";

    /// <summary>The monster row column that states what its first attack does, and of what kind.</summary>
    /// <remarks>
    /// The table's own columns, named in its header row and read by the donor at the same positions
    /// (OpenEnroth <c>src/Engine/Objects/Monsters.cpp:534-555</c>): 16 the special attack, 17 the first
    /// attack's damage type, 18 its dice, 19 its missile, 28 to 37 the ten resistances.
    /// </remarks>
    internal const int SpecialAttackColumn = 16;

    /// <summary>The column that states the damage type of a monster's first attack.</summary>
    internal const int AttackTypeColumn = 17;

    /// <summary>The column that states the dice of a monster's first attack.</summary>
    internal const int AttackDamageColumn = 18;

    /// <summary>The column that states what a monster's first attack throws, empty when it is a blow.</summary>
    internal const int AttackMissileColumn = 19;

    /// <summary>The column that states how often a monster uses its second attack, in percent.</summary>
    /// <remarks>The table's own <c>Att%</c> header, read by the donor at <c>src/Engine/Objects/Monsters.cpp:540</c>.</remarks>
    internal const int SecondAttackChanceColumn = 20;

    /// <summary>The column that states the damage type of a monster's second attack.</summary>
    internal const int SecondAttackTypeColumn = 21;

    /// <summary>The column that states the dice of a monster's second attack.</summary>
    internal const int SecondAttackDamageColumn = 22;

    /// <summary>The column that states what a monster's second attack throws, empty when it is a blow.</summary>
    internal const int SecondAttackMissileColumn = 23;

    /// <summary>The column that states how often a monster casts its first spell, in percent.</summary>
    /// <remarks>The table's own first <c>Use%</c> header, read by the donor at <c>src/Engine/Objects/Monsters.cpp:542</c>.</remarks>
    internal const int FirstSpellChanceColumn = 24;

    /// <summary>The column that states which spell a monster casts first, with its mastery and skill.</summary>
    internal const int FirstSpellColumn = 25;

    /// <summary>The column that states how often a monster casts its second spell, in percent.</summary>
    internal const int SecondSpellChanceColumn = 26;

    /// <summary>The column that states which spell a monster casts second.</summary>
    internal const int SecondSpellColumn = 27;

    /// <summary>The name the shipped table gives the people it places in the world.</summary>
    /// <remarks>
    /// A person standing in a level is an actor with a monster row in the donor, and the rows it uses are
    /// named <c>Peasant</c> (OpenEnroth <c>src/Engine/Objects/MonsterEnumFunctions.cpp:60-80</c>, the
    /// peasant monster types with their race and sex). Our people content comes from the NPC table, which
    /// states no monster row and no tier, so this game reads the first shipped peasant row for every person
    /// and says so where a person's hit points are read.
    /// </remarks>
    internal const string PersonRowName = "Peasant";

    /// <summary>The scope this game's attack rolls are drawn under, so they cannot collide with another owner's.</summary>
    internal const string AttackRollScope = "mm7.combat.attack";

    /// <summary>How many resistance checks one hit may fail before harm stops being halved.</summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/Objects/Actor.cpp:3750-3756</c> and
    /// <c>src/Engine/Objects/Character.cpp:1101-1108</c>: four checks at most, each of which halves what is
    /// left, so a well-resisted hit can be reduced to a sixteenth.
    /// </remarks>
    internal const int ResistanceChecks = 4;

    /// <summary>What a resistance check must roll at or above to halve the harm.</summary>
    /// <remarks>
    /// The donor's own threshold in both formulas: the check rolls over the resistance plus thirty, and a
    /// roll below thirty ends the halving (OpenEnroth <c>src/Engine/Objects/Actor.cpp:3751-3755</c>).
    /// </remarks>
    internal const int ResistanceThreshold = 30;

    /// <summary>How far off a shot stops being able to hit at all.</summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/Objects/Actor.cpp:3025-3034</c>: a projectile further than 5120 world units
    /// from the party never resolves against a monster that is not in its full AI state, which is the same
    /// radius this game gives a shot its reach with.
    /// </remarks>
    internal const double ProjectileLimit = 5120;

    /// <summary>The distance at which a shot is a medium-range one rather than a close one.</summary>
    /// <remarks>OpenEnroth <c>src/Engine/Objects/Actor.cpp:3030-3033</c>: 2560 world units and beyond.</remarks>
    internal const double MediumRange = 2560;

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

    /// <summary>The attribute a character's chance to land a blow is priced from.</summary>
    internal static readonly AttributeId AccuracyAttribute = new("Accuracy");

    /// <summary>The attribute a character's blow is made heavier by.</summary>
    internal static readonly AttributeId MightAttribute = new("Might");

    /// <summary>The attribute a character's armour class and their saving throws are priced from.</summary>
    internal static readonly AttributeId LuckAttribute = new("Luck");

    /// <summary>The attribute a curse tests and a drained spell point is measured against.</summary>
    internal static readonly AttributeId PersonalityAttribute = new("Personality");

    /// <summary>The attribute a drained spell point is measured against.</summary>
    internal static readonly AttributeId IntellectAttribute = new("Intellect");

    /// <summary>The skill that turns blows aside when nothing is worn.</summary>
    internal static readonly SkillId DodgeSkill = new("Dodging");

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
    internal static readonly int[] ParameterThresholds =
        [500, 400, 350, 300, 275, 250, 225, 200, 175, 150, 125, 100, 75, 50, 40, 35, 30, 25, 21, 19, 17, 15, 13, 11, 9, 7, 5, 3, 0];

    /// <summary>What each threshold above is worth, in ticks of recovery the character gets back.</summary>
    internal static readonly int[] ParameterBonuses =
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
    private readonly MonsterFacts? _person;
    private readonly IRandomService? _random;
    private readonly IFallenCreatureObserver? _fallen;
    private readonly MightAndMagic7Spells? _spells;

    private MightAndMagic7Combat(
        Dictionary<int, MonsterFacts> monsters,
        Dictionary<string, string> people,
        MonsterFacts? person,
        IRandomService? random,
        IFallenCreatureObserver? fallen,
        MightAndMagic7Spells? spells)
    {
        _monsters = monsters;
        _people = people;
        _person = person;
        _random = random;
        _fallen = fallen;
        _spells = spells;
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
    /// <param name="fallen">
    /// Whoever is told what the party brought down, when a session has anybody: a session hands the owner
    /// that keeps the bodies, which is also where the award for a death is made. A fight that is told nobody
    /// reports nothing, and everything about the fight itself is unchanged.
    /// </param>
    /// <param name="spells">
    /// This game's magic, which states what each spell a creature casts is worth and rolls. A caller that
    /// composed none gets one read here, so a creature's spell still lands with the spell's own numbers.
    /// </param>
    /// <returns>This game's combat policy.</returns>
    /// <exception cref="ContentValidationException">Content declares a monster or a creature this game cannot fight; every problem is named.</exception>
    internal static MightAndMagic7Combat Compose(
        ContentCatalog? catalog,
        IRandomService? random,
        IFallenCreatureObserver? fallen = null,
        MightAndMagic7Spells? spells = null)
    {
        if (catalog is null) return new MightAndMagic7Combat([], [], null, random, fallen, spells);
        List<ContentValidationIssue> issues = [];
        Dictionary<int, MonsterFacts> monsters = ReadMonsters(catalog, spells ?? MightAndMagic7Spells.Read(catalog), issues);
        Dictionary<string, string> people = ReadPeople(catalog);
        ValidateCreatures(catalog, monsters, issues);

        if (issues.Count > 0)
        {
            throw new ContentValidationException(
                $"This game's monsters cannot be fought: {issues[0].Message}",
                issues);
        }

        // A person standing in the world is an actor with a peasant's monster row in the donor, and the
        // shipped table carries those rows; the first one is what this game reads for every person, because
        // neither our people content nor the donor's placement states which tier a given person is.
        MonsterFacts? person = monsters.Values
            .Where(row => string.Equals(row.Name, PersonRowName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(row => row.Id)
            .FirstOrDefault();

        return new MightAndMagic7Combat(monsters, people, person, random, fallen, spells ?? MightAndMagic7Spells.Read(catalog));
    }

    /// <summary>What the fight read as down in one place, handed to whoever keeps what the fallen left.</summary>
    /// <remarks>
    /// The kit's fight owns no body: it states what it read and this game decides what that means — which is
    /// what makes a kill leave a searchable thing without the fight learning what loot is.
    /// </remarks>
    /// <param name="place">The place the fight read.</param>
    /// <param name="fallen">Every creature it read as down there, in the order it read them.</param>
    /// <returns>The bodies lying in that place now.</returns>
    public IReadOnlyList<Corpse> Observe(PlaceId place, IReadOnlyList<FallenCreature> fallen) =>
        _fallen?.Observe(place, fallen) ?? [];

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

        // A creature whose row states a missile throws it (the table's own `Miss` column, read as the
        // donor's attack1MissileType, OpenEnroth src/Engine/Objects/Monsters.cpp:539); everything else
        // swings, and a person's peasant row states none.
        return Facts(subject) is { Throws: true } ? AttackKind.Ranged : AttackKind.Melee;
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

        // A character's spell is paced by the spell's own row at the character's mastery, which is the donor's
        // own recovery column (<c>src/Engine/Spells/Spells.cpp:162-168</c>, <c>recovery_per_skill</c>), read
        // for the spell the character keeps in its quick slot — the one the donor's own act key casts
        // (<c>src/Engine/Objects/Character.cpp:3361-3400</c>). The fight's pacing contract asks one actor one
        // number for one kind of attack, so a named cast of another spell is paced by this same quantity:
        // per-spell pacing needs an order that carries the spell, which is a change to the kit's contract
        // rather than a rule this game can state on its own.
        if (kind == AttackKind.Spell && subject.Member is { } caster && _spells is { } spells &&
            caster.Spells.QuickSpell is { } quick && spells.Spell(quick.Value) is { } chosen)
        {
            int ticks = spells.RecoveryTicks(caster, chosen) - AttributeBonus(caster.Attributes[SpeedAttribute]);
            return Ticks(Math.Max(MinimumRangedTicks, ticks));
        }

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
    /// <para>
    /// Everything reaches as far as the kind of attack it makes: hand-to-hand at arm's length, and a shot, a
    /// throw, or a spell as far as the donor's own closest-target pick searches.
    /// </para>
    /// <para>
    /// <b>A creature's swing is short and its throw is long.</b> The donor's own melee test is a distance
    /// less than the melee range (<c>src/Engine/Objects/Character.cpp:6378</c>) and it is the same test for an
    /// actor's blow, while an actor's missile and its spells are resolved against targets within its own
    /// notice radius (<c>src/Engine/Objects/Actor.cpp:2698-2760</c>, where the ability decides whether the
    /// creature pursues or throws). Reading one reach for everything would have a creature swing at
    /// something five thousand units away, which is what its throw is for.
    /// </para>
    /// </remarks>
    public double ReachOf(CombatSubject subject, AttackKind kind)
    {
        ArgumentNullException.ThrowIfNull(subject);
        if (kind == AttackKind.Melee) return MeleeReach;
        return Facts(subject) is not null ? CreatureReach : RangedReach;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The attack's rolls are drawn under one key that names the attack inside the fight, from the same seed
    /// and service this game's first recoveries are drawn from. Nothing about a fight has to be recorded for
    /// a save: the same fight, replayed, draws the same hit and the same harm.
    /// </remarks>
    public IAttackRolls? RollsFor(CombatSubject attacker, string key)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        return _random is null ? null : new AttackRolls(_random, RollSeed, AttackRollScope, key);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <b>A character's chance to hit is the donor's own test.</b> OpenEnroth
    /// <c>src/Engine/Objects/Character.cpp:6263-6300</c> (<c>characterHitOrMiss</c>): the roll is uniform over
    /// the target's armor class plus twice the attacker's attack bonus plus thirty, and it lands when it
    /// beats the armor class plus fifteen at close range — or a further, worse band when a shot flies far
    /// (<c>src/Engine/Objects/Actor.cpp:3025-3034</c>, which turns a distance of 2560 or more into the
    /// medium band and refuses a projectile past 5120 for a monster that is not in its full AI state). This
    /// states exactly that ratio as a chance in ten-thousandths.
    /// </para>
    /// <para>
    /// <b>A monster's chance to hit a character is the donor's other test.</b> OpenEnroth
    /// <c>src/Engine/Objects/Actor.cpp:3691-3707</c> (<c>Actor::ActorHitOrMiss</c>): the roll is uniform over
    /// the character's armor class plus twice the monster's level plus ten, and it lands when it beats the
    /// armor class plus five. A creature attacking a character therefore does not use the character formula,
    /// and stating both here is what keeps a monster's blow as likely to land as the donor makes it.
    /// </para>
    /// </remarks>
    public AttackPlan PlanOf(CombatSubject attacker, CombatSubject target, AttackKind kind)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(target);

        // What the blow is: a character's own hands, or the row the attacker is. A creature's row states what
        // kind of harm its attack does — physical, an element, energy — which is why its kind is read from
        // the table rather than assumed.
        DamageKindId damageKind = attacker.Member is not null
            ? kind == AttackKind.Spell ? MightAndMagic7Damage.Magic : MightAndMagic7Damage.Physical
            : Facts(attacker)?.AttackKind ?? MightAndMagic7Damage.Physical;
        DamageRoll damage = attacker.Member is { } striker
            ? CharacterDamage(striker)
            : Facts(attacker)?.Attack ?? DamageRoll.Flat(0);
        int armor = ArmorClassOf(target);
        HitChance chance = attacker.Member is { } character
            ? CharacterHitChance(character, armor, kind, Distance(attacker, target))
            : CreatureHitChance(Facts(attacker)?.Level ?? 0, armor);

        return new AttackPlan(chance, damageKind, damage, ResistanceOf(target, damageKind));
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <b>A creature's own ways of attacking are the table's.</b> Its second attack has its own dice and its
    /// own kind of harm and its own projectile (<c>monsters.txt</c> columns 20-23, read by the donor at
    /// <c>src/Engine/Objects/Monsters.cpp:540-541</c>), so an order naming one resolves as that attack
    /// rather than as another swing of the first.
    /// </para>
    /// <para>
    /// <b>A monster's spell lands with the row's own dice.</b> The donor takes a spell's harm from its own
    /// per-spell table (<c>CalcSpellDamage</c>, <c>src/Engine/Spells/Spells.cpp:813</c>, over
    /// <c>pSpellDatas</c> at <c>Spells.cpp:193</c>), which the imported spell content states as a kind of
    /// harm and a description rather than as dice. What this game can state is the kind — read from the
    /// spell's own <c>Resist</c> column, so a creature's fire bolt is fire and its mind blast is mind — and
    /// the dice are the row's first attack until the stone that brings spells brings their numbers. The hit
    /// test is the creature's own, because a spell this build casts is aimed like any other attack.
    /// </para>
    /// <para>
    /// A member of the party has no such abilities: what a character's attack is worth is answered for the
    /// kind alone, which is where a weapon, a bow, and a quick spell will each state their own.
    /// </para>
    /// </remarks>
    public AttackPlan PlanOfAbility(CombatSubject attacker, CombatSubject target, AttackKind kind, string ability)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(ability);

        // A party member's own way of attacking with magic is a spell the casting workflow ordered: the order
        // names the spell's content identity, and this reads that spell's own dice and kind of harm out of the
        // table this game states them in. A name no spell answers falls back to the kind's own answer, which
        // is what a weapon, a bow, and a creature's blow do.
        if (attacker.Member is not null && _spells?.Spell(ability) is { } cast)
        {
            return SpellPlan(attacker, target, cast);
        }

        if (Facts(attacker) is not { } facts) return PlanOf(attacker, target, kind);
        (DamageRoll damage, DamageKindId damageKind) = ability switch
        {
            // A row that states no second attack at all — no dice and no bonus — is a row with one attack,
            // and an order naming a second one resolves as the first rather than as a blow for nothing.
            AbilityAttack2 when facts.Second.Roll.Maximum > 0 => (facts.Second.Roll, facts.Second.Kind),

            // A creature's spell lands with the spell's own dice now that this game states them: the row names
            // the spell and its own mastery and skill, and the damage is read from the spell's row rather than
            // borrowed from the creature's first attack.
            AbilitySpell1 when facts.FirstSpell.IsUsable => (facts.FirstSpell.Roll ?? facts.Attack, facts.FirstSpell.Damage),
            AbilitySpell2 when facts.SecondSpell.IsUsable => (facts.SecondSpell.Roll ?? facts.Attack, facts.SecondSpell.Damage),
            _ => (facts.Attack, facts.AttackKind),
        };

        return new AttackPlan(
            CreatureHitChance(facts.Level, ArmorClassOf(target)),
            damageKind,
            damage,
            ResistanceOf(target, damageKind));
    }

    /// <summary>What one casting of a spell is worth against a target, from this game's own table.</summary>
    /// <remarks>
    /// The spell's own kind of harm comes from its shipped row and its dice from this game's authored table —
    /// the donor's base damage plus one die per level of the caster's skill in the spell's school
    /// (<c>CalcSpellDamage</c>, <c>OpenEnroth/src/Engine/Spells/Spells.cpp:813-838</c>) — and the hit test is
    /// the caster's own, because a spell is aimed like any other attack. A spell whose row states no harm at
    /// all still resolves as the kind of harm magic does in general, which is the honest reading for a spell
    /// this table describes no element for.
    /// </remarks>
    private AttackPlan SpellPlan(CombatSubject attacker, CombatSubject target, SpellDefinition spell)
    {
        PartyMember caster = attacker.Member!;
        DamageKindId kind = _spells?.Harm(spell) ?? MightAndMagic7Damage.Magic;
        DamageRoll damage = _spells?.Damage(spell, MightAndMagic7Spells.SkillLevelOf(caster, spell)) ?? DamageRoll.Flat(0);
        return new AttackPlan(
            CharacterHitChance(caster, ArmorClassOf(target), AttackKind.Spell, Distance(attacker, target)),
            kind,
            damage,
            ResistanceOf(target, kind));
    }

    /// <summary>What one actor's own row is worth to a fight, which is what a policy reads to decide with.</summary>
    /// <remarks>
    /// This is the one place a creature's numbers leave this policy: the AI answers what a creature does,
    /// and it can only answer it from the same row the fight prices it by — how it is classed, how it moves,
    /// how fast, what its chances are, and what its spells are. Nothing outside this assembly sees it, and
    /// the kit never sees it at all.
    /// </remarks>
    /// <param name="subject">The actor to read.</param>
    /// <returns>The row, or null when nothing states one.</returns>
    internal MonsterFacts? FactsOf(CombatSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);
        return Facts(subject);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// The donor's resistance check, in both of its forms. A monster's harm is halved once per failed check,
    /// four checks at most, where a check rolls over the resistance plus thirty and fails at thirty or above
    /// (OpenEnroth <c>src/Engine/Objects/Actor.cpp:3743-3758</c>, <c>CalcMagicalDamageToActor</c>). A
    /// character's is the same loop with their luck bonus added to the resistance, and it runs only when that
    /// total is above zero (OpenEnroth <c>src/Engine/Objects/Character.cpp:1097-1108</c>,
    /// <c>CalculateIncommingDamage</c>), which is why a character with no resistance and no luck is never
    /// spared a halving.
    /// </para>
    /// <para>
    /// A resistant target therefore takes measurably less and an immune one takes nothing at all, and both
    /// are visible in what the fight reports rather than folded into a final number.
    /// </para>
    /// </remarks>
    public int DamageAfterResistance(CombatSubject target, DamageKindId kind, int damage, IAttackRolls rolls)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(rolls);
        ArgumentOutOfRangeException.ThrowIfNegative(damage);

        Resistance reading = ResistanceOf(target, kind);
        if (reading.IsImmune) return 0;

        int points = reading.Points;
        if (target.Member is { } member) points += AttributeBonus(member.Attributes[LuckAttribute]);
        if (points <= 0) return damage;

        int divisor = points + ResistanceThreshold;
        int left = damage;
        for (int check = 0; check < ResistanceChecks; check++)
        {
            if (rolls.Roll($"resistance/{check}", 0, divisor - 1) < ResistanceThreshold) break;
            left /= 2;
        }

        return Math.Max(0, left);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <b>What a monster's blow leaves on a character is the donor's own two rolls.</b> The attack lands its
    /// special attack when a hundred-sided roll comes in under the monster's level times the attack's own
    /// level (OpenEnroth <c>src/Engine/Objects/Character.cpp:5905-5909</c>), and the character then saves
    /// against it with a roll under thirty over their luck bonus, the stat the attack tests, and thirty
    /// (<c>Character.cpp:1451-1453</c>). Which stat is tested is the attack's own
    /// (<c>Character.cpp:1333-1388</c>): a poison or a disease is resisted with body, fear, insanity, and
    /// paralysis with mind, petrification with earth, a curse with personality, and the rest with endurance.
    /// </para>
    /// <para>
    /// <b>Only characters take conditions.</b> A game's conditions are the party's own state, and the donor
    /// applies them to characters — an actor's paralysis is a buff, which belongs to the monsters-and-AI
    /// stone, so nothing here invents a condition store for the world.
    /// </para>
    /// <para>
    /// The special attacks that leave something other than a condition are named and not invented: breaking
    /// an item, stealing one, and aging belong to the stones that own items and progression, and a drained
    /// spell point is not a condition at all.
    /// </para>
    /// </remarks>
    public CombatCondition? ConditionOf(CombatSubject attacker, CombatSubject target, DamageKindId kind, IAttackRolls rolls)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(rolls);

        if (target.Member is not { } member) return null;
        if (Facts(attacker) is not { } facts || facts.Special.Kind == MightAndMagic7SpecialAttackKind.None) return null;
        if (MightAndMagic7SpecialAttacks.Condition(facts.Special) is not { } condition) return null;

        int chance = facts.Level * facts.Special.Level;
        if (chance <= 0 || rolls.Roll("special", 0, 99) >= chance) return null;

        int save = AttributeBonus(member.Attributes[LuckAttribute]) + SaveBonus(member, facts.Special.Kind) + ResistanceThreshold;
        if (rolls.Roll("save", 0, save - 1) >= ResistanceThreshold) return null;

        return new CombatCondition(condition, 1, string.Create(CultureInfo.InvariantCulture, $"{NameOf(attacker)}'s attack"));
    }

    /// <inheritdoc />
    /// <remarks>
    /// OpenEnroth <c>src/Engine/Objects/Character.cpp:350-357</c> (<c>Character::CanAct</c>): sleep,
    /// paralysis, unconsciousness, death, petrification, and eradication stop a character acting. The weaker
    /// conditions do not: a weak, poisoned, diseased, insane, frightened, drunk, or cursed character still
    /// fights, which is why weakness halves what their blows are worth rather than taking their turn.
    /// </remarks>
    public bool CanAct(CombatSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);
        if (subject.Member is not { } member) return true;
        return !member.Conditions.Has(MightAndMagic7Conditions.Sleep) &&
               !member.Conditions.Has(MightAndMagic7Conditions.Paralyzed) &&
               !member.Conditions.Has(MightAndMagic7Conditions.Unconscious) &&
               !member.Conditions.Has(MightAndMagic7Conditions.Dead) &&
               !member.Conditions.Has(MightAndMagic7Conditions.Petrified) &&
               !member.Conditions.Has(MightAndMagic7Conditions.Eradicated);
    }

    /// <inheritdoc />
    /// <remarks>
    /// A member's pool is the party's own and the fight reads it live, so this states what that pool can
    /// hold. Everything else is the row it is: a creature's own hit points from the monster table
    /// (<c>monsters.txt</c> column 5, the donor's <c>MonsterInfo::hp</c>), and a person's from the shipped
    /// peasant row, which is the donor's own reading of a person standing in a level. A placement this game
    /// has no row for states nothing and takes nothing, which is a world actor that cannot be brought down
    /// rather than one that dies at zero.
    /// </remarks>
    public int HitPointsOf(CombatSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);
        if (subject.Member is { } member) return member.Resources.HitPoints.Maximum;
        return Facts(subject)?.HitPoints ?? 0;
    }

    /// <summary>
    /// What a character resists of one kind of harm.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The donor's resistance is a sum of what the character was born with — nothing: it is their hired
    /// enchanter, a grandmaster's leather armour, and the temporary bonuses items and spells carry
    /// (OpenEnroth <c>src/Engine/Objects/Character.cpp:1945-1990</c>, <c>GetActualResistance</c>). This
    /// build's party can wear nothing, carries no enchantment, and knows no spell, so every resistance is
    /// zero and an unarmed party is hurt by everything in full.
    /// </para>
    /// <para>
    /// The equipment and spell terms belong to the stones that bring items and magic, and this is where they
    /// go: the sum, in the donor's own order.
    /// </para>
    /// </remarks>
    private static Resistance CharacterResistance(PartyMember member, DamageKindId kind)
    {
        ArgumentNullException.ThrowIfNull(member);
        _ = kind;
        return Resistance.Of(0);
    }

    /// <summary>What a target resists of one kind of harm, read from whatever states it.</summary>
    private Resistance ResistanceOf(CombatSubject target, DamageKindId kind) => target.Member is { } member
        ? CharacterResistance(member, kind)
        : Facts(target)?.ResistanceOf(kind) ?? Resistance.Of(0);

    /// <summary>
    /// What a character's own body contributes to a landing blow, and what the table's rows state for
    /// anything else.
    /// </summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/Objects/Character.cpp:814-856</c> (<c>CalculateMeleeDamageTo</c>): an unarmed
    /// character rolls a three-sided die and adds a point, then their might bonus and the armsmaster
    /// reduction, and a landed blow never does less than one point. The donor's other terms — a weapon's own
    /// dice, an enchantment, a spell — are read off an equipped figure this build cannot fill, and they
    /// belong in this sum with the items stone.
    /// </remarks>
    private static DamageRoll CharacterDamage(PartyMember member)
    {
        int bonus = Bonus(member, MightAttribute);
        bonus += Multiplier(Armsmaster(member), 0, 0, 1, 2) * Armsmaster(member).Level;
        return new DamageRoll(dice: 1, sides: 3, bonus: bonus, floor: 1);
    }

    /// <summary>What a character's attack bonus is worth, in the donor's own sum.</summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/Objects/Character.cpp:768-778</c> (<c>GetActualAttack</c>) and
    /// <c>Character.cpp:2666-2688</c> (<c>GetSkillBonus(ATTRIBUTE_ATTACK)</c>): the accuracy bonus, plus an
    /// unarmed character's unarmed skill at the multiplier their mastery is worth, plus what armsmaster adds
    /// to every attack. A weapon skill, a weapon's own bonus, and a spell's blessing are terms of an equipped
    /// figure and belong here with the items stone.
    /// </remarks>
    private static int AttackBonus(PartyMember member)
    {
        int bonus = Bonus(member, AccuracyAttribute);
        if (!member.Skills.TryGet(UnarmedSkill, out SkillEntry unarmed) || unarmed.Level <= 0) return bonus;
        return bonus + (Multiplier(Armsmaster(member), 0, 1, 1, 2) * Armsmaster(member).Level) +
               (Multiplier(unarmed, 1, 1, 2, 2) * unarmed.Level);
    }

    /// <summary>What a character's armor class is worth, in the donor's own sum.</summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/Objects/Character.cpp:1875-1887</c> (<c>GetActualAC</c>): the speed bonus
    /// plus what the character is wearing or dodging with, never below zero. This build wears nothing, so
    /// what is left is the speed bonus and the dodging skill, which the donor adds at its own multipliers
    /// while no armour is worn (<c>Character.cpp:2596-2647</c>).
    /// </remarks>
    private static int CharacterArmorClass(PartyMember member)
    {
        int armor = Bonus(member, SpeedAttribute);
        if (member.Skills.TryGet(DodgeSkill, out SkillEntry dodge) && dodge.Level > 0)
        {
            armor += Multiplier(dodge, 1, 2, 3, 3) * dodge.Level;
        }

        return Math.Max(0, armor);
    }

    /// <summary>What an actor's armor class is, from its body or its row.</summary>
    private int ArmorClassOf(CombatSubject target) => target.Member is { } member
        ? CharacterArmorClass(member)
        : Facts(target)?.ArmorClass ?? 0;

    /// <summary>A character's chance to land a blow or a shot on a target of a stated armor class.</summary>
    private static HitChance CharacterHitChance(PartyMember member, int armor, AttackKind kind, double distance)
    {
        // A projectile past the donor's own limit never resolves against anything that is not in its full
        // AI state, so it cannot land at all rather than landing on a worse band.
        if (kind != AttackKind.Melee && distance >= ProjectileLimit) return HitChance.Never;

        int needed = kind != AttackKind.Melee && distance >= MediumRange
            ? ((armor + 15) / 2) + armor + 15
            : armor + 15;
        int outcomes = armor + (2 * AttackBonus(member)) + 30;
        return HitChance.Of(outcomes - needed, Math.Max(1, outcomes));
    }

    /// <summary>A creature's chance to land a blow on a character of a stated armor class.</summary>
    private static HitChance CreatureHitChance(int level, int armor)
    {
        int outcomes = armor + (2 * level) + 10;
        return HitChance.Of(outcomes - (armor + 5), Math.Max(1, outcomes));
    }

    /// <summary>What the stat a special attack tests is worth to a character's saving throw.</summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/Objects/Character.cpp:1333-1388</c>: the attack names the stat, and the
    /// saving throw is the luck bonus plus this plus thirty. The resistance terms are what a character
    /// resists, which this build states as nothing at all.
    /// </remarks>
    private static int SaveBonus(PartyMember member, MightAndMagic7SpecialAttackKind kind) => kind switch
    {
        MightAndMagic7SpecialAttackKind.Curse => Bonus(member, PersonalityAttribute),
        MightAndMagic7SpecialAttackKind.Insane or
        MightAndMagic7SpecialAttackKind.Paralyzed or
        MightAndMagic7SpecialAttackKind.Fear => CharacterResistance(member, MightAndMagic7Damage.Mind).Points,
        MightAndMagic7SpecialAttackKind.Petrified => CharacterResistance(member, MightAndMagic7Damage.Earth).Points,
        MightAndMagic7SpecialAttackKind.PoisonWeak or
        MightAndMagic7SpecialAttackKind.PoisonMedium or
        MightAndMagic7SpecialAttackKind.PoisonSevere or
        MightAndMagic7SpecialAttackKind.Dead or
        MightAndMagic7SpecialAttackKind.Eradicated => CharacterResistance(member, MightAndMagic7Damage.Body).Points,
        MightAndMagic7SpecialAttackKind.ManaDrain =>
            (Bonus(member, IntellectAttribute) + Bonus(member, PersonalityAttribute)) / 2,
        _ => Bonus(member, MightAndMagic7Health.EnduranceAttribute),
    };

    /// <summary>What one of a character's attributes is worth, by the donor's table.</summary>
    /// <remarks>
    /// A fight reads five attributes — accuracy for the blow's aim, might for its weight, luck for the
    /// saving throw and the resistance check, speed for the armour class, and endurance for the death
    /// threshold — and a character who carries none of them is content this game cannot price. That is the
    /// party model's own behaviour, which fails on the read rather than inventing a score; the message names
    /// what is missing because a member created by character creation or by this game's default party always
    /// states all seven attributes the shipped table lists.
    /// </remarks>
    private static int Bonus(PartyMember member, AttributeId attribute) =>
        AttributeBonus(member.Attributes.TryGet(attribute, out int score)
            ? score
            : throw new InvalidOperationException(
                $"{member.Profile.Name} has no '{attribute}' attribute, so this game cannot price what their fights are worth."));

    /// <summary>The armsmaster entry a member has, or a none entry when they have not learned it.</summary>
    private static SkillEntry Armsmaster(PartyMember member) =>
        member.Skills.TryGet(ArmsmasterSkill, out SkillEntry entry) ? entry : default;

    /// <summary>What one rung of a skill's ladder is worth, as the donor's own multiplier table.</summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/Objects/Character.cpp:2748-2763</c> (<c>GetMultiplierForSkillLevel</c>): the
    /// value is the one the character's mastery rung names, and an untrained skill is worth nothing.
    /// </remarks>
    private static int Multiplier(SkillEntry skill, int novice, int expert, int master, int grandmaster) =>
        skill.Level <= 0
            ? 0
            : skill.Tier.Value switch
            {
                1 => novice,
                2 => expert,
                3 => master,
                4 => grandmaster,
                _ => 0,
            };

    /// <summary>The monster row a subject is, whichever kind of actor it is, or null when nothing states one.</summary>
    private MonsterFacts? Facts(CombatSubject subject) =>
        Creature(subject) ?? PersonFacts(subject);

    /// <summary>
    /// The monster row a person standing in the world is, or null when the subject is not a person.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A person the map places is the row the map's own record names.</b> The donor reads a person
    /// standing in a level as an actor with a monster row of their own
    /// (OpenEnroth <c>src/Engine/Objects/MonsterEnumFunctions.h:56-58</c>, <c>isPeasant</c>), and the
    /// shipped levels use that to give a guard, an adept, and a peasant their own hit points and armor
    /// class: the actor record's embedded monster info states the row, and the importer carries it onto the
    /// placement as its <c>monster</c> field.
    /// </para>
    /// <para>
    /// <b>A person the NPC table places in a building states no row.</b> The table's own placement column
    /// says who lives there and not what any of them fights as, so this reads the first shipped peasant row
    /// for them — which is the donor's own reading of a person, and the only one this game can make where
    /// the data says nothing. A person whose placement names a row the content does not carry reads the
    /// same, because a missing row is not a fight nobody can price: it is a person whose own record states
    /// nothing this build can read.
    /// </para>
    /// </remarks>
    /// <param name="subject">The actor to read.</param>
    private MonsterFacts? PersonFacts(CombatSubject subject) =>
        IsPerson(subject) && subject.Placement is { } placement ? PersonFacts(placement) : null;

    /// <summary>The monster row a person placement fights as, which is the row it names or the shipped peasant.</summary>
    private MonsterFacts? PersonFacts(PlacementDefinition placement)
    {
        if (!string.Equals(placement.Content.Kind, PersonPlacementKind, StringComparison.Ordinal)) return null;
        if (int.TryParse(placement.Source.GetId(MonsterField), NumberStyles.None, CultureInfo.InvariantCulture, out int id) &&
            _monsters.TryGetValue(id, out MonsterFacts? stated))
        {
            return stated;
        }

        return _person;
    }

    /// <summary>Whether a subject is a person standing in the world rather than a creature of a row.</summary>
    private static bool IsPerson(CombatSubject subject) =>
        string.Equals(subject.Placement?.Content.Kind, PersonPlacementKind, StringComparison.Ordinal);

    /// <summary>How far apart two actors stand, in the place's own units.</summary>
    private static double Distance(CombatSubject from, CombatSubject to)
    {
        double x = to.Pose.X - from.Pose.X;
        double y = to.Pose.Y - from.Pose.Y;
        double z = to.Pose.Z - from.Pose.Z;
        return Math.Sqrt((x * x) + (y * y) + (z * z));
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

            ticks -= AttributeBonus(member.Attributes[SpeedAttribute]);
        }

        int minimum = kind == AttackKind.Melee ? MinimumMeleeTicks : MinimumRangedTicks;
        if (ticks < minimum) ticks = minimum;
        return Ticks(ticks);
    }

    /// <summary>
    /// What an attribute is worth, by the donor's own threshold table.
    /// </summary>
    /// <remarks>
    /// One table prices might, endurance, speed, accuracy, luck, and the rest
    /// (OpenEnroth <c>src/Engine/Objects/Character.cpp:234-243</c>, <c>GetParameterBonus</c>): the first
    /// threshold the attribute reaches decides what it is worth, and everything this game derives from an
    /// attribute — a recovery, a hit chance, a damage bonus, a saving throw, a death threshold — reads it
    /// here rather than restating it.
    /// </remarks>
    internal static int AttributeBonus(int attribute)
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
    private static Dictionary<int, MonsterFacts> ReadMonsters(
        ContentCatalog catalog,
        MightAndMagic7Spells? spells,
        List<ContentValidationIssue> issues)
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

            MonsterFacts? facts = Facts(entry, id, recovery, hostility == 0 ? 0 : NoticeRanges[hostility], spells, Defect);
            if (facts is not null) monsters[id] = facts;
        }

        return monsters;
    }

    /// <summary>
    /// Reads what one monster row is worth to a fight: its body, its blow, and what it resists.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The columns are the table's own and are read at the positions its header row names, which is where the
    /// donor reads them too (OpenEnroth <c>src/Engine/Objects/Monsters.cpp:534-555</c>): the special attack,
    /// the first attack's damage type and dice, its missile, and the ten resistances. The importer types the
    /// columns a fight is paced by and carries the whole row verbatim, so this is the reading of the part of
    /// the row nothing typed yet — and a row too short to hold those columns is a content defect rather than
    /// a creature with a resistance nobody read.
    /// </para>
    /// <para>
    /// The special attack is judged here as well: every non-zero spelling the operator's own table carries is
    /// one this game knows, and a cell nothing recognizes is refused while the session is composed rather
    /// than becoming a monster that hits for harm and nothing else.
    /// </para>
    /// </remarks>
    private static MonsterFacts? Facts(
        ContentEntry entry,
        int id,
        int recoveryTicks,
        double noticeRange,
        MightAndMagic7Spells? spells,
        Action<string, string> defect)
    {
        IReadOnlyList<JsonElement> columns = entry.GetArray("columns");
        List<string> cells = [];
        foreach (JsonElement cell in columns) cells.Add(cell.ValueKind == JsonValueKind.String ? cell.GetString() ?? string.Empty : cell.ToString());
        string name = entry.GetString(NameField);
        int level = entry.GetInt32(LevelField) ?? 0;
        int hitPoints = entry.GetInt32(HitPointsField) ?? 0;
        int armorClass = entry.GetInt32(ArmorClassField) ?? 0;
        long experience = Math.Max(0, entry.GetInt32(ExperienceField) ?? 0);

        // A row that carries no raw columns at all is a hand-authored one: it states the columns a fight is
        // paced by and nothing else, so it has no blow of its own, no special attack, and no resistance. The
        // imported table's rows carry the whole row and are read below; a row that carries some of it but not
        // the columns the table's header names is a defect rather than a creature with a resistance nobody
        // read.
        if (cells.Count == 0)
        {
            return new MonsterFacts(
                id,
                name,
                recoveryTicks,
                noticeRange,
                level,
                hitPoints,
                armorClass,
                experience,
                DamageRoll.Flat(0),
                MightAndMagic7Damage.Physical,
                Throws: false,
                new MonsterAttack(DamageRoll.Flat(0), MightAndMagic7Damage.Physical, Throws: false),
                SecondChance: 0,
                MonsterSpell.None,
                MonsterSpell.None,
                new MonsterSpecialAttack(MightAndMagic7SpecialAttackKind.None, 1),
                new Dictionary<DamageKindId, Resistance>(),
                AiType: string.Empty,
                Movement: string.Empty,
                Speed: 0);
        }

        if (cells.Count < MightAndMagic7Damage.MonsterColumns)
        {
            defect(
                "monster-row-short",
                $"monster '{name}' ({id}) carries {cells.Count} columns where the shipped table states {MightAndMagic7Damage.MonsterColumns}, so its attack and its resistances cannot be read.");
            return null;
        }

        MonsterSpecialAttack? special = MightAndMagic7SpecialAttacks.Parse(cells[SpecialAttackColumn]);
        if (special is null)
        {
            defect(
                "monster-special-attack-unknown",
                $"monster '{name}' ({id}) states '{cells[SpecialAttackColumn]}' as its special attack, and this game knows no such attack.");
            return null;
        }

        DamageKindId attackKind = MightAndMagic7Damage.Known(cells[AttackTypeColumn].Trim()) ?? MightAndMagic7Damage.Physical;
        if (!TryReadDice(cells[AttackDamageColumn], out DamageRoll attack))
        {
            defect(
                "monster-attack-unreadable",
                $"monster '{name}' ({id}) states '{cells[AttackDamageColumn]}' as its damage, which is not dice this game can roll.");
            return null;
        }

        DamageKindId secondKind = MightAndMagic7Damage.Known(cells[SecondAttackTypeColumn].Trim()) ?? MightAndMagic7Damage.Physical;
        if (!TryReadDice(cells[SecondAttackDamageColumn], out DamageRoll second))
        {
            defect(
                "monster-attack-unreadable",
                $"monster '{name}' ({id}) states '{cells[SecondAttackDamageColumn]}' as its second attack's damage, which is not dice this game can roll.");
            return null;
        }

        // A monster's spells are named the way the table names them and their harm is read from the spell
        // table the same content carries. A spell that table describes no harm for — a shield, a cure, a
        // dispel — is a spell this build cannot cast, so the creature keeps it on its row and never chooses
        // it; it is content the data carries rather than a defect in it.
        MonsterSpell first = MonsterSpell.Read(cells[FirstSpellColumn], cells[FirstSpellChanceColumn], spells);
        MonsterSpell secondSpell = MonsterSpell.Read(cells[SecondSpellColumn], cells[SecondSpellChanceColumn], spells);

        Dictionary<DamageKindId, Resistance> resistances = [];
        foreach (DamageKindId kind in new[]
        {
            MightAndMagic7Damage.Physical, MightAndMagic7Damage.Fire, MightAndMagic7Damage.Air,
            MightAndMagic7Damage.Water, MightAndMagic7Damage.Earth, MightAndMagic7Damage.Mind,
            MightAndMagic7Damage.Spirit, MightAndMagic7Damage.Body, MightAndMagic7Damage.Light,
            MightAndMagic7Damage.Dark,
        })
        {
            try
            {
                resistances[kind] = MightAndMagic7Damage.ReadOrNone(cells, kind);
            }
            catch (ArgumentException unreadable)
            {
                defect("monster-resistance-unreadable", $"monster '{name}' ({id}): {unreadable.Message}");
                return null;
            }
        }

        return new MonsterFacts(
            id,
            name,
            recoveryTicks,
            noticeRange,
            level,
            hitPoints,
            armorClass,
            experience,
            attack,
            attackKind,
            cells[AttackMissileColumn].Trim().Length > 0 && cells[AttackMissileColumn].Trim() != "0",
            new MonsterAttack(second, secondKind, HasMissile(cells[SecondAttackMissileColumn])),
            ChanceFrom(cells[SecondAttackChanceColumn]),
            first,
            secondSpell,
            special,
            resistances,
            entry.GetString(AiTypeField),
            entry.GetString(MovementField),
            entry.GetInt32(SpeedField) ?? 0);
    }

    /// <summary>Whether a missile column states a projectile rather than a blow.</summary>
    private static bool HasMissile(string cell) => cell.Trim().Length > 0 && cell.Trim() != "0";

    /// <summary>Reads a percentage column, which the shipped table writes as a number and may pad.</summary>
    private static int ChanceFrom(string cell) =>
        int.TryParse(cell.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int chance) ? chance : 0;

    /// <summary>
    /// What kind of harm each spell in this content does, by the spell's own name.
    /// </summary>
    /// <remarks>
    /// The spell table the packs carry states, per spell, the kind of harm it does — its own <c>Resist</c>
    /// column, which is the same vocabulary the monster table's attack columns use. A monster row names the
    /// spells it casts, so this is the join between the two: what a creature casts is content, and what that
    /// spell does to a target is content too. A spell the table describes no harm for casts nothing this
    /// build can resolve, and is left out rather than counted as physical.
    /// </remarks>
    private static Dictionary<string, DamageKindId> ReadSpells(ContentCatalog catalog)
    {
        Dictionary<string, DamageKindId> spells = new(StringComparer.OrdinalIgnoreCase);
        foreach ((_, _, ContentEntry entry) in catalog.Entries(SpellDefinitionKind))
        {
            string name = entry.GetString(NameField);
            if (name.Length == 0) continue;
            if (MightAndMagic7Damage.Known(entry.GetString(SpellResistField).Trim()) is { } kind) spells[name] = kind;
        }

        return spells;
    }

    /// <summary>
    /// Reads a monster table's damage dice, which are written as <c>&lt;dice&gt;D&lt;sides&gt;[+bonus]</c>.
    /// </summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/Objects/Monsters.cpp</c>, <c>ParseDamage</c>: the shipped table writes
    /// everything from <c>2D2</c> to <c>2D8+10</c>, and a cell of zero or one character states no attack at
    /// all. The importer's item table writes the same shape for a weapon's dice, so this reads the one shape
    /// both tables use rather than a monster-only spelling.
    /// </remarks>
    private static bool TryReadDice(string cell, out DamageRoll roll)
    {
        roll = DamageRoll.Flat(0);
        string text = cell.Trim();
        if (text.Length <= 1) return true;

        int mark = text.IndexOf('D', StringComparison.OrdinalIgnoreCase);
        if (mark <= 0) return false;
        string dice = text[..mark];
        string rest = text[(mark + 1)..];
        int bonusMark = rest.IndexOf('+', StringComparison.Ordinal);
        string sides = bonusMark >= 0 ? rest[..bonusMark] : rest;
        string bonus = bonusMark >= 0 ? rest[(bonusMark + 1)..] : "0";
        if (!int.TryParse(dice, NumberStyles.None, CultureInfo.InvariantCulture, out int count) ||
            !int.TryParse(sides, NumberStyles.None, CultureInfo.InvariantCulture, out int faces) ||
            !int.TryParse(bonus, NumberStyles.None, CultureInfo.InvariantCulture, out int added))
        {
            return false;
        }

        if (count < 0 || faces < 1 || added < 0) return false;
        roll = new DamageRoll(count, faces, added);
        return true;
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
                // An identity, not a string: the pack writes the row as the number it is.
                string named = ContentEntry.ReadId(placement, MonsterField);
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
    private MonsterFacts? Creature(CombatSubject subject) =>
        subject.Placement is { } placement ? Creature(placement) : null;

    /// <summary>The monster row a creature placement names, or null when the placement is not a creature.</summary>
    private MonsterFacts? Creature(PlacementDefinition placement)
    {
        if (!string.Equals(placement.Content.Kind, CreaturePlacementKind, StringComparison.Ordinal)) return null;
        // The row is read as an identity rather than as text: the importer writes it as the number it is, and
        // hand-authored content may write it as a string, and both name the same row.
        string named = placement.Source.GetId(MonsterField);
        return int.TryParse(named, NumberStyles.None, CultureInfo.InvariantCulture, out int id)
            ? _monsters.GetValueOrDefault(id)
            : null;
    }

    /// <summary>
    /// What the row behind one placement states its death is worth to the party, or zero when nothing does.
    /// </summary>
    /// <remarks>
    /// This is the award path's one reading of a creature's worth, and it reads the same row the fight reads
    /// the creature's body from: a monster placement names its row, and a person standing in the world
    /// fights as the row their own record names or as the shipped peasant row when it names none — which is
    /// the donor's own reading of a person going about their day, whose death awards that row's experience
    /// (<c>src/Engine/Objects/Actor.cpp:3164-3167</c>, reached for a person exactly as for a creature).
    /// </remarks>
    /// <param name="placement">The placement the creature was created from.</param>
    /// <returns>How much experience bringing it down is worth, zero when no row states one.</returns>
    /// <exception cref="ArgumentNullException">No placement was supplied.</exception>
    internal long ExperienceOf(PlacementDefinition placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        MonsterFacts? facts = Creature(placement) ?? PersonFacts(placement);
        return facts?.Experience ?? 0;
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

    /// <summary>What one monster row is worth to a fight: its body, its blow, and what it resists.</summary>
    /// <param name="Id">The row's identity, which a creature placement names.</param>
    /// <param name="Name">The row's name, which the panel shows.</param>
    /// <param name="RecoveryTicks">The row's recovery, in the donor's ticks, as it was read.</param>
    /// <param name="NoticeRange">How far off the creature notices the party, zero when it starts no fights.</param>
    /// <param name="Level">The row's level, which prices what its special attack can do.</param>
    /// <param name="HitPoints">The row's hit points, which is how much harm it takes to put one down.</param>
    /// <param name="ArmorClass">The row's armor class, which its attacker's hit chance is measured against.</param>
    /// <param name="Experience">What bringing the creature down is worth to the party.</param>
    /// <param name="Attack">What the row's first attack rolls.</param>
    /// <param name="AttackKind">What kind of harm that attack does.</param>
    /// <param name="Throws">Whether the row's first attack is thrown rather than swung.</param>
    /// <param name="Second">What the row's second attack rolls, and of what kind.</param>
    /// <param name="SecondChance">How often the row uses its second attack instead, in percent.</param>
    /// <param name="FirstSpell">The spell the row casts first, when its content names one it can cast.</param>
    /// <param name="SecondSpell">The spell it casts second.</param>
    /// <param name="Special">What the row's attack leaves on a character, if anything.</param>
    /// <param name="Resistances">What the row resists, by kind of harm.</param>
    /// <param name="AiType">The row's AI class, which is what decides whether it runs when hurt.</param>
    /// <param name="Movement">How the row moves, which is what decides whether it closes or holds.</param>
    /// <param name="Speed">How fast it covers ground, in place units per second.</param>
    internal sealed record MonsterFacts(
        int Id,
        string Name,
        int RecoveryTicks,
        double NoticeRange,
        int Level,
        int HitPoints,
        int ArmorClass,
        long Experience,
        DamageRoll Attack,
        DamageKindId AttackKind,
        bool Throws,
        MonsterAttack Second,
        int SecondChance,
        MonsterSpell FirstSpell,
        MonsterSpell SecondSpell,
        MonsterSpecialAttack Special,
        IReadOnlyDictionary<DamageKindId, Resistance> Resistances,
        string AiType,
        string Movement,
        int Speed)
    {
        /// <summary>The row's recovery as the game time a fight advances by.</summary>
        public GameDuration Recovery { get; } = Ticks(RecoveryTicks);

        /// <summary>Whether the row moves at all: a stationary creature holds its post.</summary>
        /// <remarks>
        /// OpenEnroth <c>src/Engine/Objects/MonsterEnums.h:445</c> — <c>MONSTER_MOVEMENT_TYPE_STATIONARY</c>,
        /// which the shipped table spells <c>stand</c> (<c>src/Engine/Objects/Monsters.cpp:264</c>).
        /// </remarks>
        public bool IsStationary => string.Equals(Movement.Trim(), "stand", StringComparison.OrdinalIgnoreCase);

        /// <summary>What the row resists of one kind of harm, nothing when no column covers it.</summary>
        public Resistance ResistanceOf(DamageKindId kind) =>
            Resistances.TryGetValue(kind, out Resistance reading) ? reading : Resistance.Of(0);

        /// <summary>The first attack, as an ability rather than as two fields.</summary>
        public MonsterAttack First => new(Attack, AttackKind, Throws);
    }

    /// <summary>
    /// One way a monster hurts somebody: what it rolls, what kind of harm it does, and whether it is thrown.
    /// </summary>
    /// <param name="Roll">What the attack rolls for harm.</param>
    /// <param name="Kind">What kind of harm it does.</param>
    /// <param name="Throws">Whether it is thrown rather than swung, which is what makes it reach.</param>
    internal sealed record MonsterAttack(DamageRoll Roll, DamageKindId Kind, bool Throws)
    {
        /// <summary>How the attack is made, as a fight spells it: a missile is thrown, anything else is swung.</summary>
        public AttackKind AttackKind => Throws ? AttackKind.Ranged : AttackKind.Melee;
    }

    /// <summary>
    /// One spell a monster casts: which spell it is, how often it is chosen, and what harm it does.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The cell the table writes is <c>&lt;name&gt;,&lt;mastery&gt;,&lt;skill&gt;</c> — the donor's own three
    /// parts (<c>OpenEnroth src/Engine/Objects/Monsters.cpp:269-291</c>, <c>parseSpellEntry</c>) — and the
    /// harm the spell does is read from the spell's own entry in the same content.
    /// </para>
    /// <para>
    /// <b>What this build cannot cast is left out rather than faked.</b> A spell whose content states no
    /// kind of harm is a buff, a cure, or a utility: the donor applies those to the caster or their allies
    /// out of its own spell data and actor buffs (<c>src/Engine/Spells/Spells.cpp:193</c>,
    /// <c>pSpellDatas</c>, and <c>src/Engine/Objects/Actor.cpp:3593-3641</c>,
    /// <c>_427102_IsOkToCastSpell</c>), neither of which exists here yet, so a creature does not choose one.
    /// The stone that brings spells and actor effects is where those become castable, and it is the same
    /// place the numbers of a damaging spell belong: the donor takes them from its own per-spell table
    /// (<c>CalcSpellDamage</c>, <c>src/Engine/Spells/Spells.cpp:813</c>), which this build's imported spell
    /// content states as a kind and a description rather than as dice.
    /// </para>
    /// </remarks>
    /// <param name="Name">The spell's name, as the table writes it, empty when the row casts none.</param>
    /// <param name="UseChance">How often the creature chooses it, in percent.</param>
    /// <param name="Kind">What kind of harm it does, which is what the spell's own content states.</param>
    /// <param name="Roll">
    /// What one casting rolls, read from the spell's own row at the mastery and skill the creature's cell
    /// states, or null when the creature's row names a spell this game states no harm for.
    /// </param>
    internal sealed record MonsterSpell(string Name, int UseChance, DamageKindId? Kind, DamageRoll? Roll = null)
    {
        /// <summary>The spell this game cannot cast yet: a row that casts none, or one whose spell harms nobody.</summary>
        public static MonsterSpell None { get; } = new(string.Empty, 0, MightAndMagic7Damage.Physical);

        /// <summary>What the spell does to a target, for the one harm this build can state of it.</summary>
        public DamageKindId Damage => Kind ?? MightAndMagic7Damage.Magic;

        /// <summary>
        /// Whether the creature may choose this spell at all.
        /// </summary>
        /// <remarks>
        /// A spell whose own content states a kind of harm is one a fight can resolve; one that states none —
        /// a shield, a cure, a dispel, a ward — is a spell the donor applies to the caster or their allies out
        /// of spell data and actor buffs this build does not have yet, so a creature never chooses it. That is
        /// the same answer the donor's own gate gives a spell it is not useful to cast
        /// (<c>src/Engine/Objects/Actor.cpp:3593-3641</c>, <c>_427102_IsOkToCastSpell</c>), stated where this
        /// build's own lack is: the stone that brings spells and actor effects is where these become
        /// castable, and the creature keeps them on its row until then.
        /// </remarks>
        public bool IsUsable => Name.Length > 0 && UseChance > 0 && Kind is not null && Roll is not null;

        /// <summary>Reads one spell cell and its chance, and what the spell's own content says it does.</summary>
        /// <param name="cell">The table's own spell cell.</param>
        /// <param name="chanceCell">The table's own use-chance cell.</param>
        /// <param name="spells">
        /// What each spell in this content does, by name; a spell this content does not describe harms nobody
        /// this build can state, which is a spell the creature keeps and never chooses.
        /// </param>
        internal static MonsterSpell Read(string cell, string chanceCell, MightAndMagic7Spells? spells)
        {
            string text = cell.Trim();
            int chance = ChanceFrom(chanceCell);
            if (text.Length == 0 || text == "0") return None;

            string[] parts = text.Split(',');
            string spell = parts[0].Trim();
            if (spell.Length == 0) return None;
            if (spells?.SpellByName(spell) is not { } known || spells.Harm(known) is not { } kind) return new MonsterSpell(spell, chance, null);

            // The cell's own two numbers are the mastery and the skill the creature casts at
            // (OpenEnroth src/Engine/Objects/Monsters.cpp:269-291, parseSpellEntry), and the damage is the
            // spell's own row read at that skill — the same expression a character's cast rolls.
            int skill = parts.Length > 2 && int.TryParse(parts[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int levels)
                ? levels
                : 0;
            return new MonsterSpell(spell, chance, kind, spells.Damage(known, skill));
        }
    }
}
