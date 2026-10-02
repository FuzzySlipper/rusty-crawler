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
/// <b>A character is priced by what they wear.</b> The donor's recovery, armour class, chance to land, blow,
/// shot and resistances are sums of terms read off the equipped figure and the character's own body, and each
/// is stated below as such a sum, term by term in the donor's order and through this game's figure
/// (<see cref="MightAndMagic7Figure"/>): what each sum reads faithfully, what it approximates, and which term
/// waits for another owner (item enchantments, #8513) is said beside it.
/// </para>
/// </remarks>
internal sealed partial class MightAndMagic7Combat : ICombatRule, ICombatResolutionRule, ICombatAbilityResolutionRule, ICombatWeaponRule, ICombatReflectionRule, ICombatProvocationRule
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

    /// <summary>How many years an ageing blow adds: one (OpenEnroth <c>src/Engine/Objects/Character.cpp:1606</c>).</summary>
    private const int AgeingBlowYears = 1;

    /// <summary>
    /// The monster row field that states the table's own internal name, which is what the game finds a row by when
    /// it creates a creature no map places (OpenEnroth <c>src/Engine/Objects/Monsters.cpp:560-566</c>).
    /// </summary>
    internal const string InternalNameField = "internalName";

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

    /// <summary>The field that states a monster's first attack: its kind of harm, its dice, and what it throws.</summary>
    /// <remarks>
    /// Every combat field a monster row carries is typed by the importer from the table's own cells, so this
    /// reads names, kinds, dice, chances and counts and knows nothing of where the table keeps them.
    /// </remarks>
    internal const string AttackField = "attack";

    /// <summary>The field that states a monster's second attack, with how often it uses it.</summary>
    internal const string SecondAttackField = "secondAttack";

    /// <summary>The field that states the first spell a monster casts, when it casts one.</summary>
    internal const string FirstSpellField = "firstSpell";

    /// <summary>The field that states the second spell a monster casts, when it casts one.</summary>
    internal const string SecondSpellField = "secondSpell";

    /// <summary>The field that states a monster's resistance to each kind of harm it is not immune to.</summary>
    internal const string ResistancesField = "resistances";

    /// <summary>The field that lists the kinds of harm a monster is immune to.</summary>
    internal const string ImmunitiesField = "immunities";

    /// <summary>The field that states what a monster's blow leaves besides harm, when it leaves anything.</summary>
    internal const string SpecialAttackField = "specialAttack";

    /// <summary>The field that states which column of the hostility matrix a monster's feelings are read from.</summary>
    internal const string HostilityKindField = "hostilityKind";

    /// <summary>The field a creature stood from a level's own actor record names that record's placement by.</summary>
    internal const string ActorPlacementField = "actorPlacement";

    /// <summary>The field that carries an actor record's attribute bits as the record stores them.</summary>
    internal const string AttributesField = "attributes";

    /// <summary>The field that carries the kind an actor record says it counts as, when it names one.</summary>
    internal const string HostilityGroupField = "hostilityGroup";

    /// <summary>The attribute bit that makes an actor the party's enemy whatever its kind (OpenEnroth <c>ActorEnums.h:109</c>).</summary>
    internal const int AggressorAttribute = 0x0008_0000;

    /// <summary>The kind an actor record names to say it is of the party's own faction (<c>EntitySnapshots.h:801</c>).</summary>
    internal const int PartyFaction = 9999;

    /// <summary>What a hand-authored row that names no hostility kind is, which is nobody's enemy.</summary>
    internal const int NoHostilityKind = -1;

    private const string KindField = "kind";
    private const string DiceField = "dice";
    private const string CountField = "count";
    private const string SidesField = "sides";
    private const string BonusField = "bonus";
    private const string MissileField = "missile";
    private const string ChanceField = "chance";
    private const string StrengthField = "strength";
    private const string TimesField = "times";
    private const string SkillField = "skill";
    private const string MasteryField = "mastery";

    /// <summary>The rung a monster row's spell letter names: N, E, M or G, novice to grand master.</summary>
    private static int RungOf(string letter) => letter.Trim().ToUpperInvariant() switch
    {
        "G" => 4,
        "M" => 3,
        "E" => 2,
        _ => 1,
    };

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
    /// Every one of the operator's 276 monster rows states a band of one to four, and no creature reads it toward the
    /// party: the donor overwrites it with friendly whenever it stands a creature, so every creature — a level's own
    /// record, an encounter's, a summoning's — reads its band toward the party from the matrix
    /// (<see cref="OwnNature"/>), indexed into the same ranges. The row's own band is what a creature already in the
    /// fight looks for other creatures at (<see cref="MonsterFacts.NoticeRange"/>). A band this game has no range for
    /// is a content defect refused where the policy is composed, rather than a creature that is silently armed with
    /// somebody else's distance.
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

    /// <summary>The rung a dagger starts tripling a blow at: master.</summary>
    /// <remarks>OpenEnroth <c>src/Engine/Objects/Character.cpp:902-904</c>.</remarks>
    private const int MasterRung = 3;

    /// <summary>How many times a master dagger's lucky blow counts what the dagger rolled.</summary>
    /// <remarks>OpenEnroth <c>src/Engine/Objects/Character.cpp:905</c>.</remarks>
    private const int DaggerFactor = 3;

    /// <summary>The draw a master dagger's level is a chance out of: a hundred, a point of chance per level.</summary>
    /// <remarks>OpenEnroth <c>src/Engine/Objects/Character.cpp:899-904</c> — the donor's corrected reading.</remarks>
    private const int DaggerChanceOutOf = 100;

    /// <summary>The rung a sword, an axe or a bow starts taking its level off a recovery at: expert.</summary>
    /// <remarks>OpenEnroth <c>src/Engine/Objects/Character.cpp:1695-1705</c>.</remarks>
    private const int ExpertRung = 2;

    // The item table's own skill words, lower-case as the importer writes them, which a worn item's terms are
    // decided by (OpenEnroth src/Engine/Tables/ItemTable.cpp:117-131, the donor's equipSkillMap).
    private const string StaffWord = "staff";
    private const string SwordWord = "sword";
    private const string DaggerWord = "dagger";
    private const string AxeWord = "axe";
    private const string SpearWord = "spear";
    private const string BowWord = "bow";
    private const string MaceWord = "mace";
    private const string BlasterWord = "blaster";
    private const string ShieldWord = "shield";
    private const string LeatherWord = "leather";
    private const string ChainWord = "chain";
    private const string PlateWord = "plate";
    private const string ClubWord = "club";

    /// <summary>
    /// How many levels of a spell's school a charged item fires at, which is the donor's own fixed value.
    /// </summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/Spells/CastSpellInfo.h:61</c>: a wand casts at novice mastery of the eighth
    /// level, whatever its bearer's own skill in that school is — which is why a wand is worth carrying by a
    /// character who has never learned the school at all.
    /// </remarks>
    internal const int WandSkillLevel = 8;

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
    private readonly MightAndMagic7Spells? _spells;
    private readonly Func<PartyEntity?> _party;
    private readonly Func<IMemberSpellEffects?> _memberEffects;
    private readonly MightAndMagic7Figure? _figure;
    private readonly GameClock? _clock;
    private readonly MightAndMagic7Hostility _hostility;
    private readonly Dictionary<string, int> _internalNames;

    private MightAndMagic7Combat(
        Dictionary<int, MonsterFacts> monsters,
        Dictionary<string, string> people,
        MonsterFacts? person,
        IRandomService? random,
        MightAndMagic7Spells? spells,
        Func<PartyEntity?>? party,
        Func<IMemberSpellEffects?>? memberEffects,
        MightAndMagic7Figure? figure,
        GameClock? clock,
        MightAndMagic7Hostility hostility,
        Dictionary<string, int>? internalNames = null)
    {
        _internalNames = internalNames ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        _clock = clock;
        _hostility = hostility;
        _monsters = monsters;
        _people = people;
        _person = person;
        _random = random;
        _spells = spells;
        _party = party ?? (() => null);
        _memberEffects = memberEffects ?? (() => null);
        _figure = figure;
    }

    /// <summary>
    /// What a spell has left acting on the party, which the fight's own readings consult.
    /// </summary>
    /// <remarks>
    /// A ward or a blessing is party-carried state, so the fight reads it from the party the same way it reads
    /// a member's skills: through the owner that holds it, at the moment it is asked. The provider is a
    /// function rather than the party itself because this policy is composed before the session's party
    /// exists on the path that creates one, and because a product playing no party has none to read — and a
    /// fight with no party reads no spells, which is the honest answer rather than an invented ward.
    /// </remarks>
    private ActiveEffects? SpellEffects => _party()?.Effects;

    /// <summary>What a spell has left acting under one identity, or zero when nothing is.</summary>
    private int SpellWard(EffectId effect) => SpellEffects?.MagnitudeOf(effect) ?? 0;

    /// <summary>
    /// What a spell has left acting on one character, read from that character's own effects.
    /// </summary>
    /// <remarks>
    /// A ward the donor's own buff puts on a character is that character's, so the fight reads it where that
    /// character is in hand — their resistance, their luck, what their blow is worth, their chance to land —
    /// and never from the party's own carried effects: a blessing cast on one member is that member's, and a
    /// fight that read it for everybody would give the whole band a spell one of them paid for.
    /// </remarks>
    private int MemberWard(PartyMember member, EffectId effect) =>
        _memberEffects()?.MagnitudeOn(member, effect) ?? 0;

    /// <summary>
    /// What a spell has left acting for one character under one identity, on them and on the party together.
    /// </summary>
    /// <remarks>
    /// The donor's own <c>GetMagicalBonus</c> is that sum: the character's own buff and the party's buff of the same
    /// name, added (<c>OpenEnroth/src/Engine/Objects/Character.cpp:2322-2395</c>). Reading both here is what lets one
    /// identity be cast at one member by a potion and at the whole band by a spell and be read the same way.
    /// </remarks>
    private int Buffed(PartyMember member, EffectId effect) => SpellWard(effect) + MemberWard(member, effect);

    /// <summary>
    /// What one of a character's scores reads as in a fight: the score they carry, and what a spell has raised it by.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The donor's <c>GetActualStat</c> (<c>OpenEnroth/src/Engine/Objects/Character.cpp:729-765</c>) is a sum of
    /// terms, and this states the ones this build carries, in its order: the score itself at the share the character's
    /// age leaves of it (<see cref="MightAndMagic7Ageing"/>), then the magical bonus — the character's own boost of that
    /// score (a potion's, <c>CHARACTER_BUFF_STRENGTH</c> and its siblings) and the party's day of the gods, which adds
    /// to all seven (<c>:2360-2387</c>). Faithful for those terms.
    /// </para>
    /// <para>
    /// The others are not invented: the conditions' multiplier waits for this game's condition table, item bonuses
    /// wait for enchantments (#8513), and a follower's luck for followers (#8514). Each is one more line here when its
    /// owner lands.
    /// </para>
    /// </remarks>
    /// <param name="member">The character.</param>
    /// <param name="attribute">The score to read.</param>
    /// <returns>What the score reads as.</returns>
    /// <exception cref="InvalidOperationException">The character carries no such score, which this game cannot price.</exception>
    internal int ActualAttribute(PartyMember member, AttributeId attribute)
    {
        ArgumentNullException.ThrowIfNull(member);
        int score = member.Attributes.TryGet(attribute, out int carried)
            ? MightAndMagic7Ageing.Aged(attribute, carried, MightAndMagic7Ageing.AgeOf(member, _clock))
            : throw new InvalidOperationException(
                $"{member.Profile.Name} has no '{attribute}' attribute, so this game cannot price what their fights are worth.");
        score += MemberWard(member, SpellEffectIds.Attribute(attribute));
        score += SpellWard(SpellEffectIds.DayOfTheGods);
        return score;
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
    /// <param name="spells">
    /// This game's magic, which states what each spell a creature casts is worth and rolls. A caller that
    /// composed none gets one read here, so a creature's spell still lands with the spell's own numbers.
    /// </param>
    /// <param name="party">
    /// The party whose carried spell effects the fight's own readings consult — a ward when it lets a target's
    /// resistance off a blow, a blessing when it prices a chance to land, a haste when it charges recovery.
    /// It is a provider because this policy is composed before a created party exists.
    /// </param>
    /// <param name="memberEffects">
    /// The effects spells have left on the party's own characters, which is what a ward cast on one member is
    /// read from — their resistance, their luck, what their blow is worth, their chance to land. It is a
    /// provider for the same reason the party is: the ledger that holds the deadlines is composed before the
    /// party it applies them to. A fight composed with none reads no character's own effects, which is what a
    /// session whose ruleset answered no magic gets.
    /// </param>
    /// <param name="figure">
    /// This game's figure, which says what each worn item is: a caller that composed one passes it so the session
    /// reads the item table once, and one read here otherwise.
    /// </param>
    /// <param name="clock">
    /// The session's one clock, which a character's natural age is read from; without one every character is the
    /// age they started at.
    /// </param>
    /// <param name="hostileGroups">
    /// Whether a place's own events have turned one of its groups of creatures hostile, which is the world's
    /// per-place state a fixture writes (<see cref="MightAndMagic7Fixtures.IsGroupHostile"/>); without it no group
    /// is.
    /// </param>
    /// <returns>This game's combat policy.</returns>
    /// <exception cref="ContentValidationException">Content declares a monster or a creature this game cannot fight; every problem is named.</exception>
    internal static MightAndMagic7Combat Compose(
        ContentCatalog? catalog,
        IRandomService? random,
        MightAndMagic7Spells? spells = null,
        Func<PartyEntity?>? party = null,
        Func<IMemberSpellEffects?>? memberEffects = null,
        MightAndMagic7Figure? figure = null,
        GameClock? clock = null,
        Func<PlaceId, int, bool>? hostileGroups = null)
    {
        if (catalog is null) return new MightAndMagic7Combat([], [], null, random, spells, party, memberEffects, figure: null, clock, MightAndMagic7Hostility.Empty) { HostileGroups = hostileGroups };
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
        // shipped table carries several of those rows; the lowest-numbered one is what this game reads for
        // every person, because neither our people content nor the donor's placement states which tier a
        // given person is. The order is the row's own number rather than the catalog's, so which row wins
        // does not move with the order the packs happen to load in.
        MonsterFacts? person = monsters.Values
            .Where(row => string.Equals(row.Name, PersonRowName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(row => row.Id)
            .FirstOrDefault();

        return new MightAndMagic7Combat(
            monsters,
            people,
            person,
            random,
            spells ?? MightAndMagic7Spells.Read(catalog),
            party,
            memberEffects,
            figure ?? MightAndMagic7Figure.Read(catalog),
            clock,
            MightAndMagic7Hostility.Read(catalog),
            ReadInternalNames(catalog, monsters))
        {
            HostileGroups = hostileGroups,
            _savedLoot = catalog.Entries(MightAndMagic7Spells.ItemDefinitionKind).Select(e => new ItemDefinitionId(e.Entry.Id)).ToHashSet(),
        };
    }

    /// <summary>Whether a place's own events have turned one of its groups of creatures hostile, or null when nothing says.</summary>
    private Func<PlaceId, int, bool>? HostileGroups { get; init; }

    /// <summary>
    /// The rows each internal name the monster table states finds, the lowest-numbered where two rows share one.
    /// </summary>
    /// <remarks>
    /// The shipped table carries a few internal names more than once (three copies each of four arena beasts), and
    /// the donor's own lookup takes the first row that matches (OpenEnroth <c>src/Engine/Objects/Monsters.cpp:560-566</c>),
    /// which is the lowest-numbered one.
    /// </remarks>
    private static Dictionary<string, int> ReadInternalNames(ContentCatalog catalog, Dictionary<int, MonsterFacts> monsters)
    {
        Dictionary<string, int> named = new(StringComparer.OrdinalIgnoreCase);
        foreach ((_, _, ContentEntry entry) in catalog.Entries(MonsterDefinitionKind))
        {
            string internalName = entry.GetString(InternalNameField).Trim();
            if (internalName.Length == 0) continue;
            if (!int.TryParse(entry.Id, NumberStyles.None, CultureInfo.InvariantCulture, out int id) || !monsters.ContainsKey(id)) continue;
            if (!named.TryGetValue(internalName, out int known) || id < known) named[internalName] = id;
        }

        return named;
    }

    /// <summary>The monster row the table's own internal name finds, or null when content states no row by that name.</summary>
    /// <param name="internalName">The internal name, matched without regard to case as the donor matches it.</param>
    internal int? RowNamed(string internalName) =>
        _internalNames.TryGetValue(internalName, out int id) ? id : null;

    /// <summary>What a monster row is called, or null when content states no such row.</summary>
    /// <param name="row">The row.</param>
    internal string? NameOfRow(int row) => _monsters.TryGetValue(row, out MonsterFacts? facts) ? facts.Name : null;

    /// <summary>The level a creature's own row states, zero when the subject is not a creature of a row.</summary>
    /// <param name="subject">The creature.</param>
    internal int LevelOf(CombatSubject subject) => Facts(subject)?.Level ?? 0;

    /// <summary>How many monster rows this policy can fight.</summary>
    internal int MonsterCount => _monsters.Count;

    /// <summary>What every kind of monster thinks of every other kind, read once from content.</summary>
    internal MightAndMagic7Hostility MonsterKinds => _hostility;

    /// <summary>Whether a creature is one of the undead, by the kind its own row belongs to.</summary>
    /// <param name="subject">The creature.</param>
    internal bool IsUndead(CombatSubject subject) =>
        Facts(subject) is { } facts && _hostility.IsUndead(facts.HostilityKind);

    /// <summary>Whether a creature's own row makes it immune to one kind of harm.</summary>
    /// <param name="subject">The creature.</param>
    /// <param name="kind">The kind of harm.</param>
    internal bool ImmuneTo(CombatSubject subject, DamageKindId kind) =>
        Facts(subject) is { } facts && facts.ResistanceOf(kind).IsImmune;

    /// <summary>What a spell has left on a creature under one identity, zero when nothing runs or it is not a creature.</summary>
    /// <param name="subject">The creature.</param>
    /// <param name="effect">The effect to read.</param>
    internal static int OnCreature(CombatSubject subject, EffectId effect) =>
        subject.Member is null && subject.Entity is { } entity ? CreatureEffects.Find(entity.Actor)?.MagnitudeOf(effect) ?? 0 : 0;

    /// <summary>Whether a creature runs from what it fights because a spell made it afraid.</summary>
    /// <param name="subject">The creature.</param>
    internal static bool IsAfraid(CombatSubject subject) => OnCreature(subject, SpellEffectIds.CreatureAfraid) > 0;

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
    /// is what the party turns into an enemy by attacking them; a creature a level's own actor record stands is
    /// hostile as that record and the shipped matrix say, and a creature an encounter or a summoning stands as the
    /// matrix says of its kind (<see cref="OwnNature"/>). Anything else — a door, a chest, a light — is not a creature
    /// at all.
    /// </remarks>
    public Hostility NatureOf(CombatSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);
        if (subject.Member is not null) return Hostility.Peaceful;
        if (Creature(subject) is { } creature)
        {
            // What a spell made a creature outranks what it is: a charmed or bound creature stands with the party
            // (OpenEnroth src/Engine/Objects/Actor.cpp:2097-2104, where the charm and the binding are read before the
            // creature's own enmity), and a berserk one is the enemy of everything as far as it can see
            // (Actor.cpp:2134, HOSTILITY_LONG).
            if (OnCreature(subject, SpellEffectIds.CreatureCharmed) > 0 || OnCreature(subject, SpellEffectIds.CreatureEnslaved) > 0)
            {
                return Hostility.Allied;
            }

            // A creature a spell called up or stood back up is the party's own for as long as it stands: the donor
            // makes it friendly and of no kind but the party's (src/Engine/Objects/Actor.cpp:4184-4186 for a summoned
            // elemental, :1740-1748 for a raised body). A berserk spell is the one thing read before it, as it is
            // read before a charm.
            // A level's own creature whose record puts it in the party's own faction stands with the party the same
            // way: the donor reads such an actor's kind as the party's (OpenEnroth src/Engine/Objects/Actor.h:73,
            // src/Engine/Snapshots/EntitySnapshots.cpp:1494-1495).
            if ((MightAndMagic7Summons.IsSummoned(subject.Placement) || OfPartyFaction(subject)) &&
                OnCreature(subject, SpellEffectIds.CreatureBerserk) <= 0)
            {
                return Hostility.Allied;
            }

            if (OnCreature(subject, SpellEffectIds.CreatureBerserk) > 0) return Hostility.Aggressive(NoticeRanges[^1]);

            // An invisible party is not noticed: the donor's own invisibility is what stops a creature from
            // seeing the party at all, so a creature whose hostility is its notice band reads as one that
            // starts no fight (OpenEnroth src/Engine/Objects/Actor.cpp:2080-2090, where the band is tested
            // against what the actor can see). A creature the party has already attacked stays hostile,
            // because that is the fight's own memory of what the party did rather than of what it looks like.
            if (SpellWard(SpellEffectIds.Invisibility) > 0) return Hostility.Peaceful;

            // A creature whose group a map event turned hostile is the party's enemy at the longest band, whatever
            // its row says (the donor's aggressor bit, OpenEnroth src/Engine/Objects/Actor.cpp:2155-2156).
            if (InHostileGroup(subject)) return Hostility.Aggressive(NoticeRanges[^1]);

            return OwnNature(subject, creature);
        }

        if (!IsPerson(subject)) return Hostility.Inert;

        // A person is an actor of the level as a creature is, and is not noticed by an invisible party either.
        if (SpellWard(SpellEffectIds.Invisibility) > 0) return Hostility.Peaceful;

        // A person whose group a map event turned hostile — the guards of a place the party broke into — is an
        // enemy the same way.
        if (InHostileGroup(subject)) return Hostility.Aggressive(NoticeRanges[^1]);

        // Otherwise a person is what their own actor record and the matrix say, read exactly as a level's own
        // creature is (OwnNature): the donor keeps no separate standing for an actor that names somebody.
        return PersonFacts(subject) is { } facts ? OwnNature(subject, facts) : Hostility.Peaceful;
    }

    /// <summary>
    /// What a creature is toward the party by itself — by the record or the row that stands it — before a spell, an
    /// invisible party or a map event changes it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A level's own creature is what its record and the matrix say.</b> The donor stands every actor of a level
    /// friendly by its own row (it overwrites the row's hostility with friendly on load, OpenEnroth
    /// <c>src/Engine/Graphics/Outdoor.cpp:628-629</c> and <c>src/Engine/Graphics/Indoor.cpp:988</c>) and then lets it
    /// choose the party as a target only when its relation to the party is not friendly
    /// (<c>src/Engine/Objects/Actor.cpp:2097-2116</c>, <c>_SelectTarget</c>). That relation
    /// (<c>Actor.cpp:2122-2166</c>, <c>GetActorsRelation</c> with no other actor) is the longest band when the
    /// record carries the aggressor bit <c>0x80000</c> (<c>ActorEnums.h:109</c>, <c>Actor.h:92-94</c>; MMExtension
    /// names it <c>Hostile</c>, <c>Scripts/Core/ConstAndBits.lua:127</c>), and otherwise what the record's kind
    /// thinks of the party in the shipped matrix — the kind its row belongs to unless the record names another
    /// (<c>src/Engine/Snapshots/EntitySnapshots.cpp:1494-1499</c>; MMExtension's <c>Ally</c>,
    /// <c>Scripts/Structs/01 common structs.lua:1452-1454</c>). The band it finds is also how far off it notices the
    /// party (<c>Actor.cpp:2106-2107</c>, read while the actor still stands friendly by its row). Faithful, with
    /// the distance measured in a straight line rather than per axis as everywhere in this fight. The flag
    /// <c>0x1000000</c> (<c>ACTOR_HOSTILE</c>) is not read: the donor recomputes it from these same facts every
    /// update to colour the map (<c>Actor.cpp:3877-3879</c>), so it decides nothing. Over the operator's install no
    /// record carries the aggressor bit or names another kind, so what decides every shipped record is the matrix.
    /// </para>
    /// <para>
    /// <b>Every other creature is what its kind thinks of the party in the matrix.</b> The donor stands an encounter's
    /// creature with its monster type as its faction and its row's hostility overwritten with friendly
    /// (<c>src/Engine/Objects/Actor.cpp:4331-4334</c>, <c>SpawnEncounter</c>), and a map event's summoning goes through
    /// the same function with nothing set differently (<c>src/Engine/Evt/EvtInterpreter.cpp:77-99</c>: an event's own
    /// point and group, no aggressor). Such a creature carries no record, so no aggressor bit and no other kind: its
    /// relation to the party is its row's kind in the matrix, and the band found is how far off it notices the party,
    /// exactly as for a level's own record. A creature content places directly is read the same way. Faithful; a kind
    /// the matrix keeps friendly to the party walks the world and never starts a fight. Whatever the creature, the
    /// party attacking it makes it the party's enemy from then on, which is the fight's own memory of what the party
    /// did and the donor's aggressor bit set on the creature the party strikes (<c>Actor.cpp:706-708</c>, raised by
    /// the party's blow at <c>Actor.cpp:3153-3154</c>). The rest encounter's ambush, which the donor stands as aggressors
    /// (<c>src/Engine/Graphics/Indoor.cpp:1815</c>), stands nothing here: a broken night is an hour's nap only.
    /// </para>
    /// </remarks>
    /// <param name="subject">The creature.</param>
    /// <param name="creature">Its own monster row.</param>
    private Hostility OwnNature(CombatSubject subject, MonsterFacts creature)
    {
        int kind = creature.HostilityKind;
        if (ActorRecord(subject) is { } record)
        {
            if ((record.GetInt32(AttributesField) is { } attributes) && (attributes & AggressorAttribute) != 0)
            {
                return Hostility.Aggressive(NoticeRanges[^1]);
            }

            if (record.GetInt32(HostilityGroupField) is { } named and not 0) kind = named;
        }

        int band = kind == PartyFaction ? 0 : _hostility.TowardParty(kind);
        return band <= 0 ? Hostility.Peaceful : Hostility.Aggressive(NoticeRanges[Math.Min(band, NoticeRanges.Length - 1)]);
    }

    /// <summary>
    /// Whether a creature is the party's enemy by what it is — its record, its row, or a group a map event turned —
    /// before any spell on it or the party is read; what a bound creature reads to choose whom it fights.
    /// </summary>
    /// <remarks>
    /// Every creature's own nature already reads its kind in the matrix, which is the donor's own reading for every
    /// actor (<c>Actor.cpp:2165</c>).
    /// </remarks>
    /// <param name="subject">The creature.</param>
    /// <param name="facts">Its monster row.</param>
    internal bool AgainstParty(CombatSubject subject, MonsterFacts facts) =>
        !OfPartyFaction(subject) &&
        (InHostileGroup(subject) || OwnNature(subject, facts).AttacksOnSight);

    /// <summary>
    /// The actor record a level's own creature was stood from, or a person's own placement, which is the record that
    /// stands them; null for any other subject.
    /// </summary>
    private static ContentEntry? ActorRecord(CombatSubject subject) =>
        subject.Placement is { } placement &&
        (IsPerson(subject) || placement.Source.GetString(ActorPlacementField) is { Length: > 0 })
            ? placement.Source
            : null;

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <b>The donor's own alarm.</b> When the party hurts an actor, kills it, or is caught stealing from it, the donor
    /// turns every other actor of the same faction standing within 4,096 units of it against the party
    /// (OpenEnroth <c>src/Engine/Objects/Actor.cpp:704-725</c>, <c>AggroSurroundingPeasants</c>; called with the
    /// aggressor flag from a party blow that wounds or kills, <c>Actor.cpp:3152-3165</c>, from a wound turned back
    /// by pain reflection, <c>src/Engine/Objects/Character.cpp:5880-5892</c> and <c>:6046-6058</c>, and from a theft
    /// caught, <c>Character.cpp:1215-1216</c>). Two actors are of one faction when the kind each counts as is the same
    /// kind, or when both are peasant kinds of one race (<c>Actor.cpp:694-702</c>, <c>ArePeasantsOfSameFaction</c>;
    /// the races are the donor's own table, <c>src/Engine/Objects/MonsterEnumFunctions.cpp:60-104</c>, and
    /// <see cref="MightAndMagic7Hostility.PeasantRace"/>). The kind an actor counts as is the one its record names,
    /// else its row's (<c>src/Engine/Snapshots/EntitySnapshots.cpp:1494-1499</c>), read the way
    /// <see cref="NatureOf"/> reads it. An actor that cannot act — one a spell holds still — is passed over, as the
    /// donor passes over one that cannot act (<c>Actor.cpp:712</c>, <c>Actor::CanAct</c>).
    /// </para>
    /// <para>
    /// <b>Faithful in who and how far</b>: the same kinds or the same peasant race, within 4,096 units measured in a
    /// straight line from where the wronged actor stands, as the donor measures it. <b>Ours</b>: the donor raises the
    /// alarm when a blow lands or kills, and this game raises it wherever the fight remembers an act against the
    /// actor — the attack ordered at it, a spell that is an act against it, or a theft caught — so a blow that misses
    /// raises it too; and an actor it turns is the party's enemy for as long as it stands there, which is how this
    /// fight remembers every provocation, where the donor gives it the longest band and the aggressor bit.
    /// </para>
    /// </remarks>
    public bool ProvokedWith(CombatSubject provoked, CombatSubject bystander)
    {
        ArgumentNullException.ThrowIfNull(provoked);
        ArgumentNullException.ThrowIfNull(bystander);
        if (FactionOf(provoked) is not { } wronged || FactionOf(bystander) is not { } other) return false;
        if (!SameFaction(wronged, other) || !CanAct(bystander)) return false;
        return provoked.Pose.DistanceTo(bystander.Pose) < AlarmRadius;
    }

    /// <summary>How far an act against an actor carries to its faction, in place units (<c>Actor.cpp:718</c>).</summary>
    private const double AlarmRadius = 4096;

    /// <summary>
    /// The kind an actor counts as when its faction is read: the kind its record names, the party's own faction for a
    /// record that says so, else its row's kind; null for a subject of no row or a row that names no kind.
    /// </summary>
    private int? FactionOf(CombatSubject subject)
    {
        if (subject.Member is not null || Facts(subject) is not { } facts) return null;
        if (ActorRecord(subject)?.GetInt32(HostilityGroupField) is { } named and not 0) return named;
        return facts.HostilityKind == NoHostilityKind ? null : facts.HostilityKind;
    }

    /// <summary>Whether two kinds are one faction: the same kind, or peasants of one race (<c>Actor.cpp:694-702</c>).</summary>
    private static bool SameFaction(int one, int other) =>
        one == other ||
        (MightAndMagic7Hostility.PeasantRace(one) is { } race && MightAndMagic7Hostility.PeasantRace(other) == race);

    /// <summary>Whether a level's own creature's record puts it in the party's own faction, and nothing made it the aggressor.</summary>
    private static bool OfPartyFaction(CombatSubject subject) =>
        ActorRecord(subject) is { } record &&
        record.GetInt32(HostilityGroupField) == PartyFaction &&
        ((record.GetInt32(AttributesField) ?? 0) & AggressorAttribute) == 0;

    /// <summary>Whether the actor stands in a group its place's own events turned hostile.</summary>
    private bool InHostileGroup(CombatSubject subject) =>
        HostileGroups is { } hostile &&
        subject.Placement?.Source.GetInt32(MightAndMagic7MonsterAi.GroupField) is { } group and not 0 &&
        hostile(subject.Place, group);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// A creature whose row carries a missile throws it and everything else swings. A member's charged wand is
    /// answered by <see cref="WeaponOf"/>, and a member with a working bow on the figure shoots it.
    /// </para>
    /// <para>
    /// <b>A bow is the member's attack, not a choice per target — that is ours.</b> The donor swings at a target
    /// inside melee range and shoots the bow otherwise (<c>Character.cpp:6367-6397</c>); the kit asks one kind of
    /// attack of an actor rather than one per target, so a member who wears a bow shoots it at whatever the
    /// party's pick finds, near or far. A member without a bow swings, armed or not.
    /// </para>
    /// </remarks>
    public AttackKind AttackKindFor(CombatSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);
        if (subject.Member is { } member)
        {
            return _figure?.Functional(member, MightAndMagic7Figure.Bow) is not null ? AttackKind.Ranged : AttackKind.Melee;
        }

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
        // A slowed creature takes twice as long over everything it does: the donor doubles its recovery while the
        // buff runs (OpenEnroth src/Engine/Objects/Actor.cpp:1296, 1370, 1445, 1516).
        if (Creature(subject) is { } creature)
        {
            return OnCreature(subject, SpellEffectIds.CreatureSlowed) > 0
                ? GameDuration.FromMilliseconds(checked(creature.Recovery.Milliseconds * SlowedRecovery))
                : creature.Recovery;
        }

        // A character's spell is paced by the spell's own row at the character's mastery, which is the donor's
        // own recovery column (<c>src/Engine/Spells/Spells.cpp:162-168</c>, <c>recovery_per_skill</c>). Which
        // spell that is: the one a charged item in hand carries when there is one — the donor's own wand shot
        // is paced as the spell the wand fires — and otherwise the spell the character keeps in its quick
        // slot, which is the one the donor's own act key casts
        // (<c>src/Engine/Objects/Character.cpp:3361-3400</c>, <c>:6323-6380</c> for the wand in the main
        // hand). The fight's pacing contract asks one actor one number for one kind of attack, so a named cast
        // of another spell is still paced by this same quantity: per-spell pacing needs an order that carries
        // the spell to the pacing answer, which is a change to the kit's contract rather than a rule this game
        // can state on its own.
        if (kind == AttackKind.Spell && subject.Member is { } caster && _spells is { } spells)
        {
            SpellDefinition? chosen = Wielded(caster) is { } wand
                ? spells.Spell(wand.Reading.Spell.Value)
                : caster.Spells.QuickSpell is { } quick ? spells.Spell(quick.Value) : null;
            if (chosen is { } paced)
            {
                int ticks = spells.RecoveryTicks(caster, paced) - Bonus(caster, SpeedAttribute) - HasteTicks(caster);
                return Ticks(Math.Max(MinimumRangedTicks, ticks));
            }
        }

        return CharacterRecovery(subject.Member, kind);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <b>A charged item in hand is the weapon, and it strikes as the spell it carries.</b> The donor's own
    /// act order is a quick spell, then a bow or a wand, then hand-to-hand
    /// (<c>OpenEnroth/src/Engine/Objects/Character.cpp:6323-6380</c>): a wand in the main hand is fired as the
    /// spell the item table gives it, one charge of it is spent, and the shot is paced as that spell. This is
    /// that answer — the kind of attack, the spell's own identity as the ability the fight resolves with, and
    /// the instance whose charge is spent — so a wand meets the fight's own order path and nothing about its
    /// attack is resolved beside the fight.
    /// </para>
    /// <para>
    /// <b>The wand is the one in the main hand.</b> The donor reads <c>ITEM_SLOT_MAIN_HAND</c> for it
    /// (<c>Character.cpp:6326-6342</c>), and so does this: a wand is shaped for the main hand alone
    /// (<see cref="MightAndMagic7Figure"/>), so a charged item anywhere else on the figure is not fired.
    /// </para>
    /// </remarks>
    /// <param name="attacker">The actor whose weapon is read.</param>
    /// <returns>The weapon, or null when the actor brings none this game reads.</returns>
    public CombatWeapon? WeaponOf(CombatSubject attacker)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        if (attacker.Member is not { } member) return null;
        if (Wielded(member) is not { } wand) return null;
        return new CombatWeapon(AttackKind.Spell, wand.Reading.Spell.Value, wand.Item.Id, wand.Reading.Charges);
    }

    /// <summary>
    /// The charged item a member wields, or null when the figure holds none this game reads a spell for.
    /// </summary>
    /// <remarks>
    /// An item that is used up by its one spell is not a weapon — a scroll is read, not wielded — and an item
    /// whose charges are all spent is not functional, which is the donor's own test of a wand
    /// (<c>OpenEnroth/src/Engine/Objects/Item.cpp:755-757</c>, <c>isFunctional</c>: broken, or a wand with no
    /// charges, is not). Both are read here rather than in each caller, so the weapon answer and the spell a
    /// shot is paced by cannot disagree about which item is in hand.
    /// </remarks>
    private (SpellItemReading Reading, ItemInstance Item)? Wielded(PartyMember member)
    {
        if (_spells is not ISpellItemRule items) return null;
        if (member.Equipment.ItemIn(MightAndMagic7Figure.MainHand) is not { State.Damage: <= 0 } held) return null;
        if (items.Reading(held.Definition) is not { ConsumedByUse: false } reading) return null;
        if (reading.Charges - held.State.ChargesSpent <= 0) return null;
        return (reading, held);
    }

    /// <summary>How much of a character's recovery a haste takes off, or nothing when none acts.</summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/Objects/Character.cpp:1723-1728</c> (<c>GetAttackRecoveryTime</c>): a haste,
    /// the character's or the party's, takes exactly twenty-five ticks off whatever the action costs, whatever
    /// power the buff was raised at. The donor applies it to every action rather than to one kind, which is why
    /// it is subtracted from the whole recovery here and not from a named attack's own row.
    /// </remarks>
    private int HasteTicks(PartyMember member) => Buffed(member, SpellEffectIds.Haste) > 0 ? HastedTicks : 0;

    /// <summary>What a haste takes off an action, in ticks: <c>Character.cpp:1723-1728</c>.</summary>
    private const int HastedTicks = 25;

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
        return _random is null ? null : new KeyedRolls(_random, RollSeed, AttackRollScope, key);
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
            ? kind == AttackKind.Ranged ? BowDamage(striker) : CharacterDamage(striker)
            : Facts(attacker)?.Attack ?? DamageRoll.Flat(0);
        int armor = ArmorClassOf(target);
        HitChance chance = attacker.Member is { } character
            ? CharacterHitChance(character, armor, kind, attacker.Pose.DistanceTo(target.Pose))
            : CreatureHitChance(Facts(attacker)?.Level ?? 0, armor);

        return new AttackPlan(chance, damageKind, damage, ResistanceOf(target, damageKind), DivisorOf(attacker, target, kind));
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
    /// <b>A monster's spell lands with the spell's own dice.</b> The donor takes a spell's harm from its own
    /// per-spell table (<c>CalcSpellDamage</c>, <c>src/Engine/Spells/Spells.cpp:813</c>, over
    /// <c>pSpellDatas</c> at <c>Spells.cpp:193</c>), and so does this game: the dice are this game's per-spell
    /// reading at the mastery and skill the creature's row states, and the kind is the spell's own
    /// <c>Resist</c> column, so a creature's fire bolt is fire and its mind blast is mind. The hit test is the
    /// creature's own, because a spell this build casts is aimed like any other attack.
    /// </para>
    /// <para>
    /// A member of the party has no such abilities: what a character's attack is worth is answered for the
    /// kind alone — the weapon in hand for a blow and the bow for a shot — and a spell it names by its own row.
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
            ResistanceOf(target, damageKind),
            DivisorOf(attacker, target, kind));
    }

    /// <summary>What a defence divides a creature's blow by before the target's resistance has its say.</summary>
    /// <remarks>
    /// <para>
    /// <b>A shield turns a missile aside.</b> A character who carries a shield — their own or the party's — takes half
    /// of what a creature's missile does: the donor halves a monster projectile's damage when the buff runs
    /// (<c>OpenEnroth/src/Engine/Objects/Character.cpp:5987-6009</c>, <c>CHARACTER_BUFF_SHIELD</c> or
    /// <c>PARTY_BUFF_SHIELD</c>, <c>dmgToReceive &gt;&gt;= 1</c>), and a monster's projectile is what its row's missile
    /// column makes it throw rather than the spells it casts (<c>SpriteEnumFunctions.h:20-36</c>,
    /// <c>isMonsterProjectileSprite</c>). Here that is a creature's ranged attack. Faithful; the items and artifacts
    /// that shield their wearer the same way wait for item enchantments (#8513), and a grand master's shield for
    /// the shield skill's own owner.
    /// </para>
    /// </remarks>
    private int DivisorOf(CombatSubject attacker, CombatSubject target, AttackKind kind)
    {
        int divisor = 1;
        if (attacker.Member is null && target.Member is { } member && kind == AttackKind.Ranged &&
            Buffed(member, SpellEffectIds.Shield) > 0)
        {
            divisor *= ShieldDivisor;
        }

        // A shrunk creature's blow is divided by the ray's power (Character.cpp:5842-5846, and :6013-6016 for a
        // missile), whatever it strikes.
        if (OnCreature(attacker, SpellEffectIds.CreatureShrunk) is > 1 and int shrunk) divisor *= shrunk;
        return divisor;
    }

    /// <summary>How many times its own recovery a slowed creature takes: twice, <c>Actor.cpp:1296</c>.</summary>
    private const int SlowedRecovery = 2;

    /// <summary>What a shield divides a missile's harm by: half, <c>Character.cpp:6007-6008</c>.</summary>
    private const int ShieldDivisor = 2;

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <b>Pain reflection turns a creature's blow back on it.</b> A character carrying it sends the harm they took
    /// back onto the creature that dealt it, through that creature's own resistance to the same kind of harm
    /// (<c>OpenEnroth/src/Engine/Objects/Character.cpp:5875-5900</c> for a blow and <c>:6042-6062</c> for a missile,
    /// <c>CalcMagicalDamageToActor(damageType, dmgToReceive)</c>). The creature's checks are drawn under their own
    /// name in the attack's rolls, so they are not the same draws the character's own resistance took. Faithful.
    /// </para>
    /// <para>
    /// Only a character reflects: a creature's own pain reflection is a buff the donor's monsters cast on
    /// themselves, which this build's creatures do not carry.
    /// </para>
    /// </remarks>
    public int ReflectedOnto(CombatSubject attacker, CombatSubject target, DamageKindId kind, int harm, IAttackRolls rolls)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(rolls);
        if (harm <= 0 || attacker.Member is not null || target.Member is not { } member) return 0;
        if (Buffed(member, SpellEffectIds.PainReflection) <= 0) return 0;
        Resistance reading = ResistanceOf(attacker, kind);
        return reading.IsImmune ? 0 : Resisted(reading.Points, harm, rolls, "reflection");
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

        // A spell fired from a charged item in the caster's hand is worth the item's own reading rather than
        // the caster's school: the donor fires a wand at a fixed eighth level of novice mastery
        // (OpenEnroth <c>src/Engine/Spells/CastSpellInfo.h:61</c>, <c>WANDS_SKILL_VALUE</c>), which is why a
        // fighter's wand is worth what a mage's is. The one case this cannot tell apart is a spell cast from
        // the caster's own spellbook while they hold a wand of that very spell: telling those apart needs the
        // order to carry the casting's own skill reading to this answer.
        bool fromWand = Wielded(caster) is { } wand && string.Equals(wand.Reading.Spell.Value, spell.Id.Value, StringComparison.Ordinal);
        int level = fromWand ? WandSkillLevel : MightAndMagic7Spells.SkillLevelOf(caster, spell);
        int rung = fromWand ? 1 : MightAndMagic7Spells.Rung(caster, spell);
        DamageRoll damage = _spells?.Damage(spell, level, rung) ?? DamageRoll.Flat(0);
        return new AttackPlan(
            CharacterHitChance(caster, ArmorClassOf(target), AttackKind.Spell, attacker.Pose.DistanceTo(target.Pose)),
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
        if (target.Member is { } member) points += LuckOf(member);
        return Resisted(points, damage, rolls, "resistance");
    }

    /// <summary>The donor's four halving checks over a resistance, drawn under one purpose of the attack's rolls.</summary>
    private static int Resisted(int points, int damage, IAttackRolls rolls, string purpose)
    {
        if (points <= 0) return damage;

        int divisor = points + ResistanceThreshold;
        int left = damage;
        for (int check = 0; check < ResistanceChecks; check++)
        {
            if (rolls.Roll($"{purpose}/{check}", 0, divisor - 1) < ResistanceThreshold) break;
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
    /// applies them to characters — an actor's paralysis is a buff, and nothing here holds a creature's
    /// running effects, so nothing here invents a condition store for the world.
    /// </para>
    /// <para>
    /// <b>An ageing blow leaves years, not a condition.</b> It lands and is saved against exactly as the rest
    /// do, against endurance (<c>Character.cpp:1351-1360</c>), and a character who fails the save is a year
    /// older than their natural age, written on the character's own progression, which a save carries
    /// (<c>Character.cpp:1604-1610</c>, <c>++sAgeModifier</c>). The donor sets no ceiling on it, and neither
    /// does this. Faithful. Nothing is reported as a condition, because none was left.
    /// </para>
    /// <para>
    /// The special attacks that leave something other than a condition or years are named and not invented:
    /// breaking an item and stealing one are not applied yet, and a drained spell point is not a condition at
    /// all.
    /// </para>
    /// </remarks>
    public CombatCondition? ConditionOf(CombatSubject attacker, CombatSubject target, DamageKindId kind, IAttackRolls rolls)
    {
        ArgumentNullException.ThrowIfNull(attacker);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(rolls);

        if (target.Member is not { } member) return null;
        if (Facts(attacker) is not { } facts || facts.Special.Kind == MightAndMagic7SpecialAttackKind.None) return null;
        ConditionId? condition = MightAndMagic7SpecialAttacks.Condition(facts.Special);
        bool ages = facts.Special.Kind == MightAndMagic7SpecialAttackKind.Aging;
        if (condition is null && !ages) return null;

        int chance = facts.Level * facts.Special.Level;
        if (chance <= 0 || rolls.Roll("special", 0, 99) >= chance) return null;

        int save = Bonus(member, LuckAttribute) + SaveBonus(member, facts.Special.Kind) + ResistanceThreshold;
        if (rolls.Roll("save", 0, save - 1) >= ResistanceThreshold) return null;

        if (ages)
        {
            member.Progression.Age(AgeingBlowYears);
            return null;
        }

        return new CombatCondition(condition!.Value, 1, string.Create(CultureInfo.InvariantCulture, $"{NameOf(attacker)}'s attack"));
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

        // A creature a spell paralysed stands where it is and does nothing until it lets go (OpenEnroth
        // src/Engine/Objects/Actor.cpp:169-176, src/Engine/TurnEngine/TurnEngine.cpp:806-810).
        if (subject.Member is not { } member) return OnCreature(subject, SpellEffectIds.CreatureParalyzed) <= 0;
        return MightAndMagic7Conditions.CanAct(member);
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
    /// The donor's resistance is a sum (OpenEnroth <c>src/Engine/Objects/Character.cpp:1945-1993</c>,
    /// <c>GetActualResistance</c>, over <c>GetBaseResistance</c> at <c>:1900-1942</c>): a hired enchanter, a
    /// grand master's leather armour, the spells running on the character and the party, and the base — the
    /// character's own stat, a racial bonus, and what worn items' enchantments add. This reads, in that order:
    /// the leather term, faithfully — a grand master of leather wearing working leather armour adds the leather
    /// level to fire, air, water, and earth — the ward a spell leaves running, and the base
    /// (<see cref="MightAndMagic7BaseResistance"/>): the race's bonus and a Lich's own floor, faithfully, with a
    /// Lich's whole resistance capped at two hundred (<c>:1988-1990</c>). Followers are not in this build (#8514)
    /// and items carry no enchantment yet (#8513), so those terms are nothing here rather than a number invented.
    /// </para>
    /// <para>
    /// Each term is its own line, so a later term — a buff another owner reads, an enchantment — is one more
    /// line in the same sum rather than a second place resistance is decided.
    /// </para>
    /// </remarks>
    private Resistance CharacterResistance(PartyMember member, DamageKindId kind)
    {
        ArgumentNullException.ThrowIfNull(member);
        int points = 0;
        points += LeatherResistance(member, kind);
        points += Buffed(member, SpellEffectIds.Resistance(kind));
        points += MightAndMagic7BaseResistance.Of(member, kind);
        if (MightAndMagic7BaseResistance.IsLich(member)) points = Math.Min(points, MightAndMagic7BaseResistance.LichCeiling);
        return points <= 0 ? Resistance.Of(0) : Resistance.Of(points);
    }

    /// <summary>
    /// What a grand master's leather armour adds to one of the four elements, or nothing.
    /// </summary>
    /// <remarks>OpenEnroth <c>src/Engine/Objects/Character.cpp:1954-1961</c>.</remarks>
    private int LeatherResistance(PartyMember member, DamageKindId kind)
    {
        if (kind != MightAndMagic7Damage.Fire && kind != MightAndMagic7Damage.Air &&
            kind != MightAndMagic7Damage.Water && kind != MightAndMagic7Damage.Earth)
        {
            return 0;
        }

        if (Worn(member, MightAndMagic7Figure.Armour) is not { } armour || !armour.IsSkill(LeatherWord)) return 0;
        SkillEntry leather = SkillOf(member, LeatherWord);
        return leather.Tier.Value >= GrandMasterRung ? leather.Level : 0;
    }

    /// <summary>What a character's luck is worth, with whatever a spell has added to it.</summary>
    /// <remarks>
    /// A fate is the donor's own luck buff, read at ATTRIBUTE_LUCK, and luck is the term the resistance check
    /// and the saving throw both carry (OpenEnroth <c>src/Engine/Objects/Character.cpp:2380-2384</c>).
    /// </remarks>
    private int LuckOf(PartyMember member) =>
        Bonus(member, LuckAttribute) + Buffed(member, SpellEffectIds.Fate);

    /// <summary>What a target resists of one kind of harm, read from whatever states it.</summary>
    private Resistance ResistanceOf(CombatSubject target, DamageKindId kind) => target.Member is { } member
        ? CharacterResistance(member, kind)
        : Facts(target)?.ResistanceOf(kind) ?? Resistance.Of(0);

    /// <summary>
    /// What a character's own body contributes to a landing blow, and what the table's rows state for
    /// anything else.
    /// </summary>
    /// <remarks>
    /// <para>
    /// OpenEnroth <c>src/Engine/Objects/Character.cpp:814-856</c> (<c>CalculateMeleeDamageTo</c>), read term by
    /// term in the donor's order. <b>The hands:</b> an unarmed character — nothing working in the main hand,
    /// and nothing but a shield in the off hand (<c>:1131-1135</c>) — rolls a three-sided die; otherwise the
    /// main hand's weapon rolls its row's own dice and adds its modifier, with one more die for a spear held
    /// with the off hand empty (<c>:826-831</c>, <c>:861-875</c>), and a second weapon in the off hand adds its
    /// own (<c>:834-841</c>). <b>The bonus:</b> the skill bonus the weapon's own skill is worth
    /// (<see cref="MeleeSkillBonus"/>), the might bonus, and what a spell adds. A landed blow does at least one.
    /// Faithful, except where stated: a second weapon whose dice differ from the first is added as its
    /// average, because one roll of the kit's states one kind of die — that is ours.
    /// </para>
    /// <para>
    /// <b>The master dagger</b> (<c>:899-905</c>): each hand holding a dagger, for a master of the dagger, triples
    /// what that weapon rolled — its own dice and modifier, not the skill or might added after — at a chance of
    /// one in a hundred per dagger level, drawn once per hand. It is the kit's <see cref="DamageMultiplier"/> over
    /// that hand's run of the roll's dice. The chance is the donor's corrected reading, as its own comment there
    /// records: the original executable fixed it at ten in a hundred whatever the level, and we take the corrected
    /// one, which matches the manual's "% chance" (<c>docs/research/mm7-manual-outline.md</c>, skills). A second dagger added as an average (above) is tripled as that average.
    /// </para>
    /// <para>
    /// Not read: a slaying enchantment's double damage (<c>:877-897</c>) waits for item enchantments (#8513).
    /// </para>
    /// </remarks>
    private DamageRoll CharacterDamage(PartyMember member)
    {
        int dice = 0;
        int sides = 0;
        int bonus = 0;
        List<DamageMultiplier> daggers = [];
        if (IsUnarmed(member))
        {
            dice = 1;
            sides = 3;
        }
        else
        {
            if (Worn(member, MightAndMagic7Figure.MainHand) is { IsMeleeWeapon: true } main)
            {
                bool twoHandedSpear = main.IsSkill(SpearWord) && member.Equipment.ItemIn(MightAndMagic7Figure.OffHand) is null;
                dice = main.Dice + (twoHandedSpear ? 1 : 0);
                sides = main.Sides;
                bonus += main.Modifier;
                if (DaggerTriple(member, main, 0, dice, main.Modifier) is { } tripled) daggers.Add(tripled);
            }

            if (Worn(member, MightAndMagic7Figure.OffHand) is { IsMeleeWeapon: true } off)
            {
                if (dice == 0 || off.Sides == sides)
                {
                    if (DaggerTriple(member, off, dice, off.Dice, off.Modifier) is { } tripled) daggers.Add(tripled);
                    dice += off.Dice;
                    sides = off.Sides;
                }
                else
                {
                    int average = off.Dice * (off.Sides + 1) / 2;
                    if (DaggerTriple(member, off, 0, 0, average + off.Modifier) is { } tripled) daggers.Add(tripled);
                    bonus += average;
                }

                bonus += off.Modifier;
            }
        }

        bonus += MeleeSkillBonus(member);
        bonus += Bonus(member, MightAttribute);

        // Heroism and hammerhands are the donor's own bonuses to what a blow is worth: heroism at
        // ATTRIBUTE_MELEE_DMG_BONUS and hammerhands on the damage of a character's own hands
        // (OpenEnroth src/Engine/Objects/Character.cpp:2355-2357 and CastSpellInfo.cpp:2366-2380).
        bonus += Buffed(member, SpellEffectIds.Heroism);
        bonus += Buffed(member, SpellEffectIds.Hammerhands);
        DamageRoll blow = new(dice, sides, bonus, floor: 1);
        foreach (DamageMultiplier dagger in daggers) blow = blow.WithMultiplier(dagger);
        return blow;
    }

    /// <summary>
    /// The master dagger's triple blow over one hand's part of the roll, or nothing when that hand holds no
    /// dagger or its bearer is no master of it.
    /// </summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/Objects/Character.cpp:899-905</c>: a master or grand master of the dagger whose
    /// weapon is a dagger has a chance of the dagger level in a hundred (<c>grng->random(100) &lt; level</c>) to
    /// triple that weapon's dice and modifier. Faithful to the donor's corrected chance; see
    /// <see cref="CharacterDamage"/>.
    /// </remarks>
    private static DamageMultiplier? DaggerTriple(PartyMember member, MightAndMagic7WornItem weapon, int firstDie, int dice, int bonus)
    {
        if (!weapon.IsSkill(DaggerWord)) return null;
        SkillEntry dagger = SkillOf(member, DaggerWord);
        if (dagger.Level <= 0 || dagger.Tier.Value < MasterRung) return null;
        return new DamageMultiplier(firstDie, dice, bonus, HitChance.Of(dagger.Level, DaggerChanceOutOf), DaggerFactor);
    }

    /// <summary>What a bow's shot is worth, in the donor's own sum.</summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/Objects/Character.cpp:954-987</c> (<c>CalculateRangedDamageTo</c>): the bow's row
    /// rolls its own dice and adds its modifier, and a grand master of the bow adds the bow level
    /// (<c>GetSkillBonus(ATTRIBUTE_RANGED_DMG_BONUS)</c>, <c>:2576-2582</c>). Might does not add to a shot, and a
    /// shot has no floor. Faithful; the slaying enchantments wait for #8513.
    /// </remarks>
    private DamageRoll BowDamage(PartyMember member)
    {
        if (Worn(member, MightAndMagic7Figure.Bow) is not { } bow) return CharacterDamage(member);
        SkillEntry skill = SkillOf(member, BowWord);
        int bonus = bow.Modifier + (Multiplier(skill, 0, 0, 0, 1) * skill.Level);
        return new DamageRoll(bow.Dice, bow.Sides, bonus);
    }

    /// <summary>What a weapon skill adds to a blow, the donor's <c>GetSkillBonus(ATTRIBUTE_MELEE_DMG_BONUS)</c>.</summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/Objects/Character.cpp:2693-2742</c>: an unarmed character's unarmed skill at
    /// (0, 1, 2, 2) per rung; otherwise the first melee weapon on the figure, in slot order, decides — a staff
    /// adds armsmaster (or the unarmed bonus for a grand master of the staff who has learned unarmed), a dagger
    /// adds armsmaster and its level at grand master, a sword armsmaster alone, a mace or a spear armsmaster and
    /// its level from expert, an axe armsmaster and its level from master. Armsmaster is worth (0, 0, 1, 2) per
    /// rung here (<c>:2562-2573</c>). Faithful.
    /// </remarks>
    private int MeleeSkillBonus(PartyMember member)
    {
        if (IsUnarmed(member))
        {
            SkillEntry unarmed = SkillOf(member, UnarmedSkill.Value);
            return Multiplier(unarmed, 0, 1, 2, 2) * unarmed.Level;
        }

        int armsmaster = Multiplier(Armsmaster(member), 0, 0, 1, 2) * Armsmaster(member).Level;
        foreach (MightAndMagic7WornItem weapon in WornBy(member))
        {
            if (!weapon.IsMeleeWeapon) continue;
            SkillEntry skill = SkillOf(member, weapon.Skill);
            return weapon.Skill switch
            {
                StaffWord when skill.Tier.Value >= GrandMasterRung && SkillOf(member, UnarmedSkill.Value).Level > 0 =>
                    Multiplier(SkillOf(member, UnarmedSkill.Value), 0, 1, 2, 2) * SkillOf(member, UnarmedSkill.Value).Level,
                StaffWord => armsmaster,
                DaggerWord => armsmaster + (Multiplier(skill, 0, 0, 0, 1) * skill.Level),
                SwordWord => armsmaster,
                MaceWord or SpearWord => armsmaster + (Multiplier(skill, 0, 1, 1, 1) * skill.Level),
                AxeWord => armsmaster + (Multiplier(skill, 0, 0, 1, 1) * skill.Level),
                _ => 0,
            };
        }

        return 0;
    }

    /// <summary>What a character's attack bonus is worth for a blow, in the donor's own sum.</summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/Objects/Character.cpp:768-778</c> (<c>GetActualAttack</c>), in its order: the
    /// accuracy bonus; the skill bonus (<c>:2650-2675</c>) — for an unarmed character armsmaster at (0, 1, 1, 2)
    /// and unarmed at (1, 1, 2, 2) per rung, and otherwise the first melee weapon on the figure: its skill level
    /// plus armsmaster, a blaster at (1, 2, 3, 5), and a grand master's staff with the unarmed bonus beside it;
    /// and the items bonus (<c>:2217-2232</c>) — the modifier of the weapon in each hand. The blessing a spell
    /// adds is read where the chance is priced. Faithful; enchantments wait for #8513.
    /// </remarks>
    private int AttackBonus(PartyMember member)
    {
        int bonus = Bonus(member, AccuracyAttribute);
        bonus += AttackSkillBonus(member);
        if (!IsUnarmed(member))
        {
            if (Worn(member, MightAndMagic7Figure.MainHand) is { IsMeleeWeapon: true } main) bonus += main.Modifier;
            if (Worn(member, MightAndMagic7Figure.OffHand) is { IsMeleeWeapon: true } off) bonus += off.Modifier;
        }

        return bonus;
    }

    /// <summary>The donor's <c>GetSkillBonus(ATTRIBUTE_ATTACK)</c>.</summary>
    private int AttackSkillBonus(PartyMember member)
    {
        int armsmaster = Multiplier(Armsmaster(member), 0, 1, 1, 2) * Armsmaster(member).Level;
        SkillEntry unarmed = SkillOf(member, UnarmedSkill.Value);
        if (IsUnarmed(member))
        {
            return unarmed.Level <= 0 ? 0 : armsmaster + (Multiplier(unarmed, 1, 1, 2, 2) * unarmed.Level);
        }

        foreach (MightAndMagic7WornItem weapon in WornBy(member))
        {
            if (!weapon.IsMeleeWeapon) continue;
            SkillEntry skill = SkillOf(member, weapon.Skill);
            if (weapon.IsSkill(BlasterWord)) return Multiplier(skill, 1, 2, 3, 5) * skill.Level;
            if (weapon.IsSkill(StaffWord) && skill.Tier.Value >= GrandMasterRung)
            {
                return (Multiplier(unarmed, 1, 1, 2, 2) * unarmed.Level) + armsmaster + skill.Level;
            }

            return armsmaster + skill.Level;
        }

        return 0;
    }

    /// <summary>What a character's attack bonus is worth for a shot, in the donor's own sum.</summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/Objects/Character.cpp:911-922</c> (<c>GetRangedAttack</c>): the bow's modifier,
    /// the accuracy bonus, and the bow level at every rung (<c>:2677-2691</c>). Faithful.
    /// </remarks>
    private int RangedAttackBonus(PartyMember member)
    {
        if (Worn(member, MightAndMagic7Figure.Bow) is not { } bow) return AttackBonus(member);
        return bow.Modifier + Bonus(member, AccuracyAttribute) + SkillOf(member, BowWord).Level;
    }

    /// <summary>What a character's armor class is worth, in the donor's own sum.</summary>
    /// <remarks>
    /// <para>
    /// OpenEnroth <c>src/Engine/Objects/Character.cpp:1875-1887</c> (<c>GetActualAC</c>), in its order, never below
    /// zero: the speed bonus; the items bonus — every working passive piece's dice and modifier, so leather
    /// armour's <c>4</c> is four points and chain's <c>8</c> eight (<c>:2299-2304</c>); the skill bonus
    /// (<see cref="ArmourSkillBonus"/>); and the stone skin a spell adds. Faithful; the enchantment half of the
    /// items bonus waits for #8513.
    /// </para>
    /// <para>
    /// Each term is its own line, so a later term — a buff another owner reads — is one more line in this sum.
    /// </para>
    /// </remarks>
    private int CharacterArmorClass(PartyMember member)
    {
        int armor = Bonus(member, SpeedAttribute);
        armor += WornBy(member).Sum(worn => worn.ArmourClass);
        armor += ArmourSkillBonus(member);

        // A stone skin is the donor's own armour-class buff, and it is read here for the reason the donor
        // reads it here: it is armour, not a resistance, and a blow's chance to land is what it changes
        // (OpenEnroth src/Engine/Objects/Character.cpp:2388-2392, ATTRIBUTE_AC_BONUS).
        armor += Buffed(member, SpellEffectIds.Armour);
        return Math.Max(0, armor);
    }

    /// <summary>The donor's <c>GetSkillBonus(ATTRIBUTE_AC_BONUS)</c>: what the skills behind worn things add.</summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/Objects/Character.cpp:2596-2648</c>: for every working piece, its skill's level at
    /// the piece's multiplier — a staff (0, 1, 1, 1), a sword or a spear (0, 0, 0, 1), a shield and leather
    /// (1, 1, 2, 2), chain and plate (1, 1, 1, 1) — and dodging at (1, 2, 3, 3) while no shield, chain or plate
    /// is worn and either no leather is or dodging is at grand master. Faithful.
    /// </remarks>
    private int ArmourSkillBonus(PartyMember member)
    {
        bool armour = false;
        bool leather = false;
        int sum = 0;
        foreach (MightAndMagic7WornItem worn in WornBy(member))
        {
            SkillEntry skill = SkillOf(member, worn.Skill);
            switch (worn.Skill)
            {
                case StaffWord:
                    sum += Multiplier(skill, 0, 1, 1, 1) * skill.Level;
                    break;
                case SwordWord or SpearWord:
                    sum += Multiplier(skill, 0, 0, 0, 1) * skill.Level;
                    break;
                case ShieldWord:
                    armour = true;
                    sum += Multiplier(skill, 1, 1, 2, 2) * skill.Level;
                    break;
                case LeatherWord:
                    leather = true;
                    sum += Multiplier(skill, 1, 1, 2, 2) * skill.Level;
                    break;
                case ChainWord or PlateWord:
                    armour = true;
                    sum += Multiplier(skill, 1, 1, 1, 1) * skill.Level;
                    break;
                default:
                    break;
            }
        }

        SkillEntry dodge = SkillOf(member, DodgeSkill.Value);
        if (!armour && (!leather || dodge.Tier.Value >= GrandMasterRung))
        {
            sum += Multiplier(dodge, 1, 2, 3, 3) * dodge.Level;
        }

        return sum;
    }

    /// <summary>What the figure holds working in one slot, or null.</summary>
    private MightAndMagic7WornItem? Worn(PartyMember member, EquipmentSlot slot) => _figure?.Functional(member, slot);

    /// <summary>Every working worn item, in the figure's slot order.</summary>
    private IEnumerable<MightAndMagic7WornItem> WornBy(PartyMember member) =>
        _figure?.Functional(member) ?? [];

    /// <summary>
    /// Whether a character fights with their own hands: nothing working in the main hand, and nothing but a shield
    /// in the off hand (OpenEnroth <c>src/Engine/Objects/Character.cpp:1131-1135</c>, <c>IsUnarmed</c>).
    /// </summary>
    private bool IsUnarmed(PartyMember member) =>
        Worn(member, MightAndMagic7Figure.MainHand) is null &&
        Worn(member, MightAndMagic7Figure.OffHand) is not { Kind: not MightAndMagic7WornKind.Shield };

    /// <summary>The entry a member has in the skill a word names, or a none entry when they have not learned it.</summary>
    private static SkillEntry SkillOf(PartyMember member, string word)
    {
        foreach (SkillEntry entry in member.Skills.Entries)
        {
            if (string.Equals(entry.Skill.Value, word, StringComparison.OrdinalIgnoreCase)) return entry;
        }

        return default;
    }

    /// <summary>What an actor's armor class is, from its body or its row.</summary>
    private int ArmorClassOf(CombatSubject target) => target.Member is { } member
        ? CharacterArmorClass(member)
        : Facts(target)?.ArmorClass ?? 0;

    /// <summary>A character's chance to land a blow or a shot on a target of a stated armor class.</summary>
    private HitChance CharacterHitChance(PartyMember member, int armor, AttackKind kind, double distance)
    {
        // A projectile past the donor's own limit never resolves against anything that is not in its full
        // AI state, so it cannot land at all rather than landing on a worse band.
        if (kind != AttackKind.Melee && distance >= ProjectileLimit) return HitChance.Never;

        int needed = kind != AttackKind.Melee && distance >= MediumRange
            ? ((armor + 15) / 2) + armor + 15
            : armor + 15;
        // A blessing is added to the attack bonus it is a blessing of: the donor's own buff is read at
        // ATTRIBUTE_ATTACK, which is the quantity this test is built on (OpenEnroth
        // src/Engine/Objects/Character.cpp:2351-2354).
        int attack = kind == AttackKind.Ranged ? RangedAttackBonus(member) : AttackBonus(member);
        int outcomes = armor + (2 * (attack + Buffed(member, SpellEffectIds.Bless))) + 30;
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
    /// saving throw is the luck bonus plus this plus thirty. The resistance terms are what the character
    /// resists of that kind of harm, the same sum a blow of it meets (<see cref="CharacterResistance"/>).
    /// </remarks>
    private int SaveBonus(PartyMember member, MightAndMagic7SpecialAttackKind kind) => kind switch
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
    private int Bonus(PartyMember member, AttributeId attribute) => AttributeBonus(ActualAttribute(member, attribute));

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


    /// <summary>
    /// What a character's attack recovery is worth, in the donor's sum and floored at the donor's minimum.
    /// </summary>
    /// <remarks>
    /// <para>
    /// OpenEnroth <c>src/Engine/Objects/Character.cpp:1636-1750</c> (<c>GetAttackRecoveryTime</c>), term by term in
    /// the donor's order. <b>The weapon's base:</b> a shot is the bow's; an unarmed character who has learned to
    /// fight unarmed has sixty ticks; otherwise the main hand's weapon, or a staff's hundred when it holds none
    /// (<c>:1637-1653</c>). <b>The off hand:</b> a shield adds its ten ticks at novice and nothing above it, and a
    /// second weapon replaces the first's base when it is slower (<c>:1658-1671</c>). <b>Armour:</b> leather adds
    /// ten ticks at novice, chain twenty at novice and ten at expert, plate thirty at novice and fifteen at expert
    /// and master, and nothing at grand master (<c>:1673-1691</c>). <b>Less:</b> the speed bonus; a sword, an axe or
    /// a bow at expert takes its level off (<c>:1695-1705</c>); armsmaster its level, twice at grand master, except
    /// for a shot or a blaster (<c>:1710-1719</c>); and a haste twenty-five ticks. The floor is the donor's thirty
    /// for a blow and five for a shot or a blaster (<c>:1739-1746</c>). The base table is the donor's own
    /// (<c>src/Engine/mm7_data.cpp:355-378</c>).
    /// </para>
    /// <para>
    /// Faithful, with two statements. A swift or a darkness weapon's twenty ticks (<c>:1727-1733</c>) wait for item
    /// enchantments (#8513). A piece worn without its skill — which this game's use rule refuses, but which
    /// content declaring no skill table can stage — is read at novice, where the donor never meets one.
    /// </para>
    /// </remarks>
    private GameDuration CharacterRecovery(PartyMember? member, AttackKind kind)
    {
        if (member is null)
        {
            // A person standing in the world is paced as a character with nothing in hand, and nothing the
            // party carries hastens them.
            int bare = UnarmedBaseTicks;
            return Ticks(Math.Max(kind == AttackKind.Melee ? MinimumMeleeTicks : MinimumRangedTicks, bare));
        }

        // The weapon's base.
        MightAndMagic7WornItem? weapon = null;
        int weaponTicks = UnarmedBaseTicks;
        bool shooting = kind == AttackKind.Ranged && Worn(member, MightAndMagic7Figure.Bow) is not null;
        if (shooting)
        {
            weapon = Worn(member, MightAndMagic7Figure.Bow);
            weaponTicks = BaseTicks(weapon!.Value.Skill);
        }
        else if (IsUnarmed(member) && SkillOf(member, UnarmedSkill.Value).Level > 0)
        {
            weaponTicks = TrainedUnarmedBaseTicks;
        }
        else if (Worn(member, MightAndMagic7Figure.MainHand) is { IsWeapon: true } main)
        {
            weapon = main;
            weaponTicks = BaseTicks(main.Skill);
        }

        // The off hand: a shield's own ticks, or a slower second weapon's base.
        int shieldTicks = 0;
        if (Worn(member, MightAndMagic7Figure.OffHand) is { } off)
        {
            if (off.Kind == MightAndMagic7WornKind.Shield)
            {
                shieldTicks = BaseTicks(off.Skill) * ArmourShare(member, off.Skill, 2, 0, 0, 0) / 2;
            }
            else if (off.IsWeapon && BaseTicks(off.Skill) > weaponTicks)
            {
                weapon = off;
                weaponTicks = BaseTicks(off.Skill);
            }
        }

        // Body armour, at the share its own skill's rung leaves (in halves).
        int armourTicks = 0;
        if (Worn(member, MightAndMagic7Figure.Armour) is { } body)
        {
            armourTicks = body.Skill switch
            {
                LeatherWord => BaseTicks(body.Skill) * ArmourShare(member, body.Skill, 2, 0, 0, 0) / 2,
                ChainWord => BaseTicks(body.Skill) * ArmourShare(member, body.Skill, 2, 1, 0, 0) / 2,
                PlateWord => BaseTicks(body.Skill) * ArmourShare(member, body.Skill, 2, 1, 1, 0) / 2,
                _ => 0,
            };
        }

        int ticks = weaponTicks + armourTicks + shieldTicks;

        // What shortens it.
        bool blaster = weapon is { } held && held.IsSkill(BlasterWord);
        if (weapon is { } trained && (trained.IsSkill(SwordWord) || trained.IsSkill(AxeWord) || trained.IsSkill(BowWord)))
        {
            SkillEntry skill = SkillOf(member, trained.Skill);
            if (skill.Level > 0 && skill.Tier.Value >= ExpertRung) ticks -= skill.Level;
        }

        if (!shooting && !blaster && Armsmaster(member) is { Level: > 0 } armsmaster)
        {
            ticks -= armsmaster.Tier.Value >= GrandMasterRung ? armsmaster.Level * 2 : armsmaster.Level;
        }

        // A haste shortens every action, standing or swinging, which is where the donor subtracts it.
        ticks -= HasteTicks(member);
        ticks -= Bonus(member, SpeedAttribute);

        int minimum = shooting || blaster || kind != AttackKind.Melee ? MinimumRangedTicks : MinimumMeleeTicks;
        return Ticks(Math.Max(minimum, ticks));
    }

    /// <summary>
    /// The share of an armour piece's ticks a character carries at their rung of its skill, in halves: the donor's
    /// <c>GetArmorRecoveryMultiplierFromSkillLevel</c> (<c>Character.cpp:1757-1772</c>).
    /// </summary>
    private static int ArmourShare(PartyMember member, string skill, int novice, int expert, int master, int grandmaster)
    {
        SkillEntry entry = SkillOf(member, skill);
        return entry.Level <= 0
            ? novice
            : entry.Tier.Value switch
            {
                1 => novice,
                2 => expert,
                3 => master,
                _ => grandmaster,
            };
    }

    /// <summary>The donor's base recovery for a weapon's or a piece's skill, in ticks.</summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/mm7_data.cpp:355-378</c>. A club reads the hundred ticks the donor carries for it,
    /// which it notes is the earlier game's value; this game's item table files its clubs under a skill nobody can
    /// learn, so no character of this game swings one.
    /// </remarks>
    private static int BaseTicks(string skill) => skill switch
    {
        StaffWord => 100,
        SwordWord => 90,
        DaggerWord => 60,
        AxeWord => 100,
        SpearWord => 80,
        BowWord => 100,
        MaceWord => 80,
        BlasterWord => 30,
        ShieldWord => 10,
        LeatherWord => 10,
        ChainWord => 20,
        PlateWord => 30,
        ClubWord => 100,
        _ => 0,
    };

    /// <summary>
    /// What an attribute is worth, by the donor's own threshold table.
    /// </summary>
    /// <remarks>
    /// One table prices might, endurance, speed, accuracy, luck, and the rest
    /// (<see cref="MightAndMagic7AttributeBonus"/>, the donor's <c>GetParameterBonus</c>): the first
    /// threshold the attribute reaches decides what it is worth, and everything this game derives from an
    /// attribute — a recovery, a hit chance, a damage bonus, a saving throw, a death threshold — reads it
    /// here rather than restating it.
    /// </remarks>
    internal static int AttributeBonus(int attribute) => MightAndMagic7AttributeBonus.Of(attribute);

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
        string name = entry.GetString(NameField);
        int level = entry.GetInt32(LevelField) ?? 0;
        int hitPoints = entry.GetInt32(HitPointsField) ?? 0;
        int armorClass = entry.GetInt32(ArmorClassField) ?? 0;
        long experience = Math.Max(0, entry.GetInt32(ExperienceField) ?? 0);
        int hostilityKind = entry.GetInt32(HostilityKindField) ?? NoHostilityKind;

        // A row that states no attack is a hand-authored one: it states the fields a fight is paced by and
        // nothing else, so it has no blow of its own, no special attack, and no resistance. The imported table's
        // rows state every combat field typed, and one that states an attack but not the rest is a defect rather
        // than a creature with a resistance nobody read.
        if (!entry.Payload.TryGetProperty(AttackField, out JsonElement attackField))
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
                Speed: 0,
                hostilityKind);
        }

        string row = $"monster '{name}' ({id})";
        if (!entry.Payload.TryGetProperty(SecondAttackField, out JsonElement secondField) ||
            !entry.Payload.TryGetProperty(ResistancesField, out JsonElement resistanceField) ||
            !entry.Payload.TryGetProperty(ImmunitiesField, out JsonElement immunityField) ||
            hostilityKind == NoHostilityKind)
        {
            defect(
                "monster-combat-incomplete",
                $"{row} states an attack and not every other combat field ({SecondAttackField}, {ResistancesField}, {ImmunitiesField}, {HostilityKindField}), so what it can do in a fight cannot be read whole.");
            return null;
        }

        MonsterSpecialAttack special = new(MightAndMagic7SpecialAttackKind.None, 1);
        if (entry.Payload.TryGetProperty(SpecialAttackField, out JsonElement specialField))
        {
            string word = specialField.TryGetProperty(KindField, out JsonElement kindCell) ? kindCell.GetString() ?? string.Empty : string.Empty;
            int strength = specialField.TryGetProperty(StrengthField, out JsonElement strengthCell) && strengthCell.TryGetInt32(out int stated) ? stated : 0;
            int times = specialField.TryGetProperty(TimesField, out JsonElement timesCell) && timesCell.TryGetInt32(out int count) ? count : 1;
            if (MightAndMagic7SpecialAttacks.From(word, strength, times) is not { } known)
            {
                defect(
                    "monster-special-attack-unknown",
                    $"{row} states '{word}' at strength {strength} as its special attack, and this game knows no such attack.");
                return null;
            }

            special = known;
        }

        (DamageRoll attack, DamageKindId attackKind, bool throws) = Attack(attackField);
        (DamageRoll second, DamageKindId secondKind, bool secondThrows) = Attack(secondField);
        int secondChance = secondField.TryGetProperty(ChanceField, out JsonElement chanceCell) && chanceCell.TryGetInt32(out int chance) ? chance : 0;

        // A monster's spells are named the way the table names them and their harm is read from the spell
        // table the same content carries. A spell that table describes no harm for — a shield, a cure, a
        // dispel — is a spell this build cannot cast, so the creature keeps it on its row and never chooses
        // it; it is content the data carries rather than a defect in it.
        MonsterSpell first = MonsterSpell.Read(entry.Payload, FirstSpellField, spells);
        MonsterSpell secondSpell = MonsterSpell.Read(entry.Payload, SecondSpellField, spells);

        Dictionary<DamageKindId, Resistance> resistances = [];
        foreach (JsonProperty stated in resistanceField.EnumerateObject())
        {
            if (MightAndMagic7Damage.Known(stated.Name) is not { } kind || !stated.Value.TryGetInt32(out int points))
            {
                defect("monster-resistance-unreadable", $"{row} states '{stated.Name}': {stated.Value} as a resistance, which is not a kind of harm and a number this game reads.");
                return null;
            }

            resistances[kind] = Resistance.Of(points);
        }

        foreach (JsonElement immune in immunityField.EnumerateArray())
        {
            if (MightAndMagic7Damage.Known(immune.GetString() ?? string.Empty) is not { } kind)
            {
                defect("monster-resistance-unreadable", $"{row} states an immunity to '{immune}', which is not a kind of harm this game knows.");
                return null;
            }

            resistances[kind] = Resistance.Immune;
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
            throws,
            new MonsterAttack(second, secondKind, secondThrows),
            secondChance,
            first,
            secondSpell,
            special,
            resistances,
            entry.GetString(AiTypeField),
            entry.GetString(MovementField),
            entry.GetInt32(SpeedField) ?? 0,
            hostilityKind);
    }

    /// <summary>An attack as the row states it: its dice, its kind of harm, and whether it throws something.</summary>
    /// <remarks>An attack that states no dice does no harm, and one that states no kind is a physical blow.</remarks>
    private static (DamageRoll Dice, DamageKindId Kind, bool Throws) Attack(JsonElement attack)
    {
        DamageRoll dice = attack.TryGetProperty(DiceField, out JsonElement roll)
            && roll.TryGetProperty(CountField, out JsonElement count) && count.TryGetInt32(out int rolled)
            && roll.TryGetProperty(SidesField, out JsonElement sides) && sides.TryGetInt32(out int faces) && faces >= 1
            ? new DamageRoll(rolled, faces, roll.TryGetProperty(BonusField, out JsonElement bonus) && bonus.TryGetInt32(out int added) ? added : 0)
            : DamageRoll.Flat(0);
        DamageKindId kind = attack.TryGetProperty(KindField, out JsonElement word)
            ? MightAndMagic7Damage.Known(word.GetString() ?? string.Empty) ?? MightAndMagic7Damage.Physical
            : MightAndMagic7Damage.Physical;
        return (dice, kind, attack.TryGetProperty(MissileField, out _));
    }


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

        // A creature a spell created is worth nothing to the party that created it: the donor zeroes a summoned
        // elemental's experience (src/Engine/Objects/Actor.cpp:4175), and a raised body's worth was already paid
        // when it fell the first time — that second half is ours.
        if (MightAndMagic7Summons.IsSummoned(placement)) return 0;
        MonsterFacts? facts = Creature(placement) ?? PersonFacts(placement);
        return facts?.Experience ?? 0;
    }

    /// <summary>Whether a placement holds a person a place's own records stand there, whom the fight reads as peaceful.</summary>
    /// <param name="placement">The placement the actor was created from.</param>
    /// <returns>True for a person placement, whatever row the person fights as.</returns>
    /// <exception cref="ArgumentNullException">No placement was supplied.</exception>
    internal static bool IsPerson(PlacementDefinition placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        return string.Equals(placement.Content.Kind, PersonPlacementKind, StringComparison.Ordinal);
    }

    /// <summary>
    /// The level of the townsperson a placement holds, or null when what stands there is not one.
    /// </summary>
    /// <remarks>
    /// A townsperson is whoever fights as one of the shipped peasant rows, which is the donor's own test for
    /// whose death is a crime (OpenEnroth <c>src/Engine/Objects/Actor.cpp:1219-1221</c>, <c>IsPeasant</c>,
    /// over the peasant monster types of <c>src/Engine/Objects/MonsterEnumFunctions.h:48-54</c>). That is
    /// every person standing in the world whose own record names no row — this game reads them as the
    /// shipped peasant — and any creature placed as a peasant row; a person whose record names a guard's or
    /// an adept's row is a person the town does not fine the party for, as in the donor. The level is the
    /// row's own, which is what the donor's fine is priced from.
    /// </remarks>
    /// <param name="placement">The placement the actor was created from.</param>
    /// <returns>The row's level when the placement is a townsperson, otherwise null.</returns>
    /// <exception cref="ArgumentNullException">No placement was supplied.</exception>
    internal int? TownspersonLevel(PlacementDefinition placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        MonsterFacts? facts = Creature(placement) ?? PersonFacts(placement);
        return facts is { } row && string.Equals(row.Name, PersonRowName, StringComparison.OrdinalIgnoreCase)
            ? row.Level
            : null;
    }

    /// <summary>
    /// The level of the row a person placement fights as — the row its record names, or the shipped peasant — or
    /// null when the placement is not a person or no row answers for it.
    /// </summary>
    /// <remarks>
    /// This is what a hand in a person's purse is measured against: the donor adds the person's own level to what
    /// being caught costs (OpenEnroth <c>src/Engine/Objects/Character.cpp:1212</c>, <c>StealFromActor</c>).
    /// </remarks>
    /// <param name="placement">The placement to read.</param>
    /// <returns>The row's level, or null.</returns>
    /// <exception cref="ArgumentNullException">No placement was supplied.</exception>
    internal int? PersonLevel(PlacementDefinition placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        return PersonFacts(placement)?.Level;
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
    internal static GameDuration Ticks(int ticks) =>
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
        int Speed,
        int HostilityKind)
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
    /// <c>_427102_IsOkToCastSpell</c>), and nothing here holds a creature's running effects, so a creature
    /// does not choose one. A damaging spell's numbers are the spell's own row, read at the rung and skill
    /// the creature's cell states.
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
        /// a shield, a cure, a dispel, a ward — is a spell the donor applies to the caster or their allies
        /// through actor buffs, and nothing here holds a creature's running effects, so a creature never chooses
        /// it. That is the same answer the donor's own gate gives a spell it is not useful to cast
        /// (<c>src/Engine/Objects/Actor.cpp:3593-3641</c>, <c>_427102_IsOkToCastSpell</c>), and the creature
        /// keeps them on its row until an owner of a creature's effects makes them castable.
        /// </remarks>
        public bool IsUsable => Name.Length > 0 && UseChance > 0 && Kind is not null && Roll is not null;

        /// <summary>Reads one spell a row states, and what the spell's own content says it does.</summary>
        /// <param name="row">The monster row.</param>
        /// <param name="field">Which of its two spells to read.</param>
        /// <param name="spells">
        /// What each spell in this content does, by name; a spell this content does not describe harms nobody
        /// this build can state, which is a spell the creature keeps and never chooses.
        /// </param>
        internal static MonsterSpell Read(JsonElement row, string field, MightAndMagic7Spells? spells)
        {
            if (!row.TryGetProperty(field, out JsonElement cast)) return None;
            string spell = cast.TryGetProperty(NameField, out JsonElement named) ? named.GetString()?.Trim() ?? string.Empty : string.Empty;
            if (spell.Length == 0) return None;
            int chance = cast.TryGetProperty(ChanceField, out JsonElement percent) && percent.TryGetInt32(out int stated) ? stated : 0;
            if (spells?.SpellByName(spell) is not { } known || spells.Harm(known) is not { } kind) return new MonsterSpell(spell, chance, null);

            // The row states the rung and the skill the creature casts at (OpenEnroth
            // src/Engine/Objects/Monsters.cpp:269-291, parseSpellEntry), and the damage is the spell's own row
            // read at that skill — the same expression a character's cast rolls.
            int skill = cast.TryGetProperty(SkillField, out JsonElement levels) && levels.TryGetInt32(out int stated2) ? stated2 : 0;
            string mastery = cast.TryGetProperty(MasteryField, out JsonElement rungCell) ? rungCell.GetString() ?? string.Empty : string.Empty;
            return new MonsterSpell(spell, chance, kind, spells.Damage(known, skill, RungOf(mastery)));
        }
    }
}
