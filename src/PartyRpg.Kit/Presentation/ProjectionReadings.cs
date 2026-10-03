using PartyRpg.Kit.Alchemy;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Conversation;
using PartyRpg.Kit.Journal;
using PartyRpg.Kit.Knowledge;
using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Maps;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Progression;
using PartyRpg.Kit.Quests;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Presentation;

/// <summary>
/// The blocks a session's projection keeps between readings, each beside the owner stamps it was read under, so a
/// block whose owners have not changed is handed back as it was instead of being read again.
/// </summary>
/// <remarks>
/// <para>
/// A running session reads its projection on every admitted update, because the clock and the simulation move on
/// every one. Most blocks do not: what the party knows, carries and has written down changes when somebody acts or
/// the clock delivers something, not sixty times a second. Each block kept here states, in its own key, every input
/// its reading depends on — the <see cref="ChangeStamp"/> of each owner it reads (<see cref="PartyEntity.Stamp"/>
/// for everything the party holds), the identity of each owner, and the few live facts it shows beyond them, such
/// as the clock's minute or the party's pose. The block is read again exactly when one of those has moved.
/// </para>
/// <para>
/// The keys are written per block, by hand, beside the reading they guard: a fact a block shows that is not in its
/// key would be shown stale, so a block that gains a reader gains a key entry in the same change. Blocks whose facts
/// move with every update — the session's own measures, the clock, the movement, the fight, the counter, the rest
/// stop, the conversation, the controls — and blocks whose reading is a handful of fields are read every time and
/// are not kept here. What is kept is a reading, never state: a block is not saved, and a session that is rebuilt
/// starts with nothing kept.
/// </para>
/// </remarks>
internal sealed class ProjectionReadings
{
    private Kept<PartyKey, PartySnapshot>? _party;
    private Kept<PartyKey, CreationSnapshot>? _members;
    private Kept<ProgressionKey, SkillsSnapshot>? _skills;
    private Kept<ProgressionKey, PromotionSnapshot>? _promotion;
    private Kept<MagicKey, MagicSnapshot>? _magic;
    private Kept<AlchemyKey, AlchemySnapshot>? _alchemy;
    private Kept<EquipmentKey, EquipmentSnapshot>? _equipment;
    private Kept<CharacterKey, CharacterSnapshot>? _character;
    private Kept<QuestsKey, QuestSnapshot>? _quests;
    private Kept<JournalKey, JournalSnapshot>? _journal;
    private Kept<MapKey, MapSnapshot>? _map;

    /// <summary>The party block: its accounts, pools, conditions and standing, all of them the party's own.</summary>
    /// <param name="party">The party, when the session has one.</param>
    /// <param name="stamp">The party's change stamp, read once for the whole projection.</param>
    /// <param name="standing">The game's reading of the party's standing, which reads the party alone.</param>
    /// <param name="portraits">The images the members' portraits are drawn with, which a composition does not change.</param>
    public PartySnapshot Party(PartyEntity? party, long stamp, IStandingRule? standing, IFollowerConversationRule? followers,
        ContentImages? portraits = null) =>
        Read(ref _party, new PartyKey(party, stamp, party?.Roster.SelectedMember), () => PartySnapshot.From(party, standing, followers, portraits));

    /// <summary>The members a played party was made with, which the creation block shows once creation is over.</summary>
    /// <param name="party">The party, when the session has one.</param>
    /// <param name="stamp">The party's change stamp.</param>
    public CreationSnapshot Members(PartyEntity? party, long stamp) =>
        Read(ref _members, new PartyKey(party, stamp, null), () => CreationSnapshot.OfParty(party));

    /// <summary>The skills block: every member's skills, ceilings and what a raise would cost, and the last raise.</summary>
    /// <param name="progression">The progression owner, when the session has one.</param>
    /// <param name="party">The party's change stamp: the skills, points and class each row is read from.</param>
    /// <param name="names">The game's words for a rung, which a composition does not change.</param>
    public SkillsSnapshot Skills(PartyProgression? progression, long party, IGameNames? names) =>
        Read(ref _skills, new ProgressionKey(progression, progression?.Stamp ?? 0, party), () => SkillsSnapshot.From(progression, names));

    /// <summary>The promotion block: every member's ladder and the report the last rank left.</summary>
    /// <param name="progression">The progression owner, when the session has one.</param>
    /// <param name="party">The party's change stamp: the class and rank each ladder is read from.</param>
    public PromotionSnapshot Promotion(PartyProgression? progression, long party) =>
        Read(ref _promotion, new ProgressionKey(progression, progression?.Stamp ?? 0, party), () => PromotionSnapshot.From(progression));

    /// <summary>The magic block: every member's spellbook and pool, what can be aimed at, and what is running.</summary>
    /// <remarks>
    /// Beyond the party and the casting owner, a spellbook shows four live facts, each read whole and compared by
    /// value because each is a handful of rows or one word: what spells have left running (whose ends are the
    /// clock's), who stands in the fight to be aimed at and the distance step each stands within, where the party stands among the places it has visited,
    /// which is what a travel spell offers, and what the party sees by, which follows the clock's daylight.
    /// </remarks>
    /// <param name="casting">The casting owner, when the session has one.</param>
    /// <param name="party">The party's change stamp: the pools, spellbooks, skills and items the rows are read from.</param>
    /// <param name="world">Where the party stands and how many places it has visited.</param>
    public MagicSnapshot Magic(Spellcasting? casting, long party, WorldSnapshot world)
    {
        MagicKey key = new(
            casting,
            casting?.Stamp ?? 0,
            party,
            world.Place,
            world.Visited,
            new Rows<RunningSpellEffect>(casting?.Magic.Running?.Running),
            new Rows<RunningSpellEffect>(casting?.Magic.Members?.RunningOnMembers),
            new Rows<(CombatantId, string, CombatSide, double)>(casting?.Fight?.Combatants.Select(combatant => (combatant.Id, combatant.Name, combatant.Side, MagicSnapshot.DistanceBand(combatant.Distance))).ToArray()),
            casting?.Magic.Sight?.Sight);
        return Read(ref _magic, key, () => MagicSnapshot.From(casting));
    }

    /// <summary>The alchemy block: who may mix, what in the pack mixes with what, and the last mixture.</summary>
    /// <param name="mixing">The mixing owner, when the session has one.</param>
    /// <param name="party">The party's change stamp: the pack and the masteries the rows are read from.</param>
    /// <param name="kinds">The game's kinds of reagent, which a composition does not change.</param>
    public AlchemySnapshot Alchemy(PotionMixing? mixing, long party, IAlchemyKinds? kinds) =>
        Read(ref _alchemy, new AlchemyKey(mixing, mixing?.Stamp ?? 0, party), () => AlchemySnapshot.From(mixing, kinds));

    /// <summary>The equipment block: what every member wears, what in the pack could be worn, and the last change.</summary>
    /// <param name="outfitting">The outfitting owner, when the session has one.</param>
    /// <param name="party">The party's change stamp: the figures and the pack the rows are read from.</param>
    /// <remarks>
    /// A pack row says why the party may not part with a thing, which is the quest owner's answer, so the key holds
    /// that owner's stamp: accepting an errand moves it without moving the party's.
    /// </remarks>
    public EquipmentSnapshot Equipment(PartyOutfitting? outfitting, long party, PartyItemUse? uses = null,
        IItemReadingRule? readings = null, ContentImages? pictures = null, PartyQuests? quests = null) =>
        Read(ref _equipment, new EquipmentKey(outfitting, outfitting?.Stamp ?? 0, party, uses, uses?.Stamp ?? 0, quests, quests?.Stamp ?? 0),
            () => EquipmentSnapshot.From(outfitting, uses, readings, pictures));

    /// <summary>
    /// The character block: each member's sheet as the game reads it. The readings fold in worn things, running
    /// effects and age, so the key holds the party's stamp and the clock's minute.
    /// </summary>
    public CharacterSnapshot Character(PartyEntity? party, long stamp, ICharacterSheetRule? sheet, GameClock? clock) =>
        Read(ref _character, new CharacterKey(party, stamp, sheet, Minute(clock)), () => CharacterSnapshot.From(party, sheet));

    /// <summary>The quests block: every errand with its objectives, and the last outcome.</summary>
    /// <remarks>
    /// An objective counts what the party carries and has recorded, and a completion condition may name an hour, so
    /// the block is read again when the party changes and when the clock's hour turns as well as when an errand does.
    /// </remarks>
    /// <param name="quests">The quest owner, when the session has one.</param>
    /// <param name="party">The party's change stamp.</param>
    /// <param name="clock">The session's clock, when it has one.</param>
    public QuestSnapshot Quests(PartyQuests? quests, long party, GameClock? clock, IGameNames? names = null) =>
        Read(ref _quests, new QuestsKey(quests, quests?.Stamp ?? 0, party, Hour(clock)), () => QuestSnapshot.From(quests, names));

    /// <summary>The journal block: its books, each read from the owner that fills it.</summary>
    /// <remarks>
    /// The calendar book shows the clock to the minute and whether it is day, so the journal is read again when the
    /// clock's minute turns; the other books move with their owners, and the place names they show with the world.
    /// </remarks>
    /// <param name="journal">The journal, when the session has one.</param>
    /// <param name="quests">The quest owner, whose errands the quests book counts.</param>
    /// <param name="world">The live world, whose places the books name.</param>
    /// <param name="clock">The session's clock, which the calendar book shows.</param>
    /// <param name="knowledge">The notes the party has taken.</param>
    /// <param name="maps">The party's maps, which the maps book lists.</param>
    public JournalSnapshot Journal(
        PartyJournal? journal,
        PartyQuests? quests,
        SessionWorld? world,
        GameClock? clock,
        PartyKnowledge? knowledge,
        PartyMaps? maps)
    {
        JournalKey key = new(
            journal,
            journal?.Stamp ?? 0,
            quests,
            quests?.Stamp ?? 0,
            knowledge,
            knowledge?.Stamp ?? 0,
            maps,
            maps?.Stamp ?? 0,
            world,
            world?.Place ?? default,
            Minute(clock),
            clock?.IsDaylight ?? false,
            clock?.ElapsedGameDays ?? 0);
        return Read(ref _journal, key, () => JournalSnapshot.From(journal, quests, world, clock, knowledge, maps));
    }

    /// <summary>The automap: the drawing of the party's place around where it stands.</summary>
    /// <remarks>
    /// The drawing is the map's seen cells around the party's pose, so it is read again when either moves. While a
    /// detection is marking anything it is read every time and nothing is kept, because what a detection marks
    /// walks about with nothing of the map's own moving.
    /// </remarks>
    /// <param name="maps">The party's maps, when the session has them.</param>
    /// <param name="world">The live world, when the session has one.</param>
    /// <param name="running">What spells have left running, which a detection is.</param>
    public MapSnapshot Map(PartyMaps? maps, SessionWorld? world, IRunningSpellEffects? running)
    {
        if (MapSnapshot.Detecting(maps, world, running))
        {
            _map = null;
            return MapSnapshot.From(maps, world, running);
        }

        MapKey key = new(maps, maps?.Stamp ?? 0, world, world?.Place ?? default, world?.Party.PlacePose ?? PlacePose.Origin);
        return Read(ref _map, key, () => MapSnapshot.From(maps, world, running));
    }

    /// <summary>The block kept under this key, or a fresh reading kept in its place when the key has moved.</summary>
    private TBlock Read<TKey, TBlock>(ref Kept<TKey, TBlock>? kept, TKey key, Func<TBlock> read)
        where TKey : struct, IEquatable<TKey>
    {
        if (kept is { } held && held.Key.Equals(key)) return held.Block;
        TBlock block = read();
        kept = new Kept<TKey, TBlock>(key, block);
        return block;
    }

    /// <summary>The clock's hour as a key: the day and the hour, and nothing finer.</summary>
    private static (int Year, int Month, int Day, int Hour) Hour(GameClock? clock) =>
        clock?.Now is { } now ? (now.Year, now.Month, now.Day, now.Hour) : default;

    /// <summary>The clock's minute as a key: the day, the hour and the minute, and nothing finer.</summary>
    private static (int Year, int Month, int Day, int Hour, int Minute) Minute(GameClock? clock) =>
        clock?.Now is { } now ? (now.Year, now.Month, now.Day, now.Hour, now.Minute) : default;

    /// <summary>One kept block and the key it was read under.</summary>
    private sealed record Kept<TKey, TBlock>(TKey Key, TBlock Block);

    /// <summary>A few rows read whole and compared by value, which a key holds where a list would compare by reference.</summary>
    private readonly struct Rows<T>(IReadOnlyList<T>? rows) : IEquatable<Rows<T>>
    {
        private readonly IReadOnlyList<T> _rows = rows ?? [];

        public bool Equals(Rows<T> other) => _rows.SequenceEqual(other._rows);

        public override bool Equals(object? obj) => obj is Rows<T> other && Equals(other);

        public override int GetHashCode() => _rows.Count;
    }

    /// <summary>A party reading's key: the party, its change stamp, and the member selected, which no stamp moves.</summary>
    private readonly record struct PartyKey(PartyEntity? Party, long Stamp, PartyMemberId? Selected);

    private readonly record struct ProgressionKey(PartyProgression? Owner, long Stamp, long Party);

    private readonly record struct MagicKey(
        Spellcasting? Owner,
        long Stamp,
        long Party,
        string Place,
        int Visited,
        Rows<RunningSpellEffect> Running,
        Rows<RunningSpellEffect> OnMembers,
        Rows<(CombatantId, string, CombatSide, double)> Combatants,
        PartySight? Sight);

    private readonly record struct AlchemyKey(PotionMixing? Owner, long Stamp, long Party);

    private readonly record struct EquipmentKey(PartyOutfitting? Owner, long Stamp, long Party, PartyItemUse? Uses, long UseStamp, PartyQuests? Quests, long QuestStamp);

    private readonly record struct CharacterKey(PartyEntity? Party, long Stamp, ICharacterSheetRule? Sheet, (int, int, int, int, int) Minute);

    private readonly record struct QuestsKey(PartyQuests? Owner, long Stamp, long Party, (int, int, int, int) Hour);

    private readonly record struct JournalKey(
        PartyJournal? Owner,
        long Stamp,
        PartyQuests? Quests,
        long QuestsStamp,
        PartyKnowledge? Knowledge,
        long KnowledgeStamp,
        PartyMaps? Maps,
        long MapsStamp,
        SessionWorld? World,
        PlaceId Place,
        (int, int, int, int, int) Minute,
        bool Daylight,
        int Days);

    private readonly record struct MapKey(PartyMaps? Owner, long Stamp, SessionWorld? World, PlaceId Place, PlacePose Pose);
}
