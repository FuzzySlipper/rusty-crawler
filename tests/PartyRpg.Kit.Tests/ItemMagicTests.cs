using System.Globalization;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// The magic a party carries in its pack and the effects a spell leaves on one character: a ward that lands
/// on the member the casting named, a duration that is a deadline on the one clock, a scroll the one casting
/// workflow reads and uses up, and a wand the fight fires as the weapon it is.
/// </summary>
/// <remarks>
/// <para>
/// Everything here is the mechanism's own. The effect identities are the suite's, the spell table is the
/// suite's, and the weapon answer is the suite's, because the kit holds none of those: what it must do is
/// land a per-character effect on that character, end it when the clock passes its deadline or when the game
/// says the character no longer carries it, cast a spell from the item a request names, spend an item's
/// charge through the party it belongs to, and fire a charged item through the fight's own order path.
/// </para>
/// <para>
/// The readings the product's own ruleset states are proved in <c>tests/PartyRpg.Host.Tests</c>, over shipped
/// spells and the operator's own content; this suite proves the kit's half, where the numbers are the test's
/// and a boundary is exact.
/// </para>
/// </remarks>
public sealed class ItemMagicTests
{
    private static readonly ContentLayout Layout = new("packs", "imports", "bundles");
    private static readonly PlaceId Hall = new("1");
    private static readonly RaceId TestRace = new("testfolk");
    private static readonly ClassId Mage = new("mage");
    private static readonly EffectId Ward = new("test.ward");
    private static readonly EffectId Haste = new("test.haste");
    private static readonly SpellId Bolt = new("1");
    private static readonly SpellId Portal = new("31");
    private static readonly SkillId FireSkill = new("Fire");
    private static readonly ConditionId Dead = new("dead");
    private static readonly ConditionId Unconscious = new("unconscious");
    private static readonly ItemDefinitionId ScrollOfBolt = new("scroll-of-bolt");
    private static readonly ItemDefinitionId WandOfBolt = new("wand-of-bolt");
    private static readonly ItemDefinitionId Token = new("token");
    private static readonly DamageKindId Steel = new("steel");
    private static readonly EquipmentSlot Hand = new("main hand");
    private static readonly AttributeId Intellect = new("Intellect");

    [Fact]
    public void A_ward_cast_on_one_character_lands_on_them_and_on_nobody_else()
    {
        using PartyEntity party = Party();
        GameClock clock = Clock();
        RunningSpellEffects effects = new(party, clock, member => !LaidOut(member));

        // The donor's own buff, cast on one character: it is that character's entry, with its own deadline.
        RunningSpellEffect ward = effects.StartOn(party.Members[0], Ward, magnitude: 12, GameDuration.FromHours(2));

        Assert.Equal(12, effects.MagnitudeOn(party.Members[0], Ward));
        Assert.Equal(0, effects.MagnitudeOn(party.Members[1], Ward));
        Assert.Equal(0, effects.MagnitudeOn(party.Members[2], Ward));

        // The party does not carry it: an effect on one character is written under their own identity, so a
        // reader of the party's own effects — a haste, a light — finds nothing.
        Assert.False(party.Effects.Has(Ward));
        Assert.False(effects.IsRunning(Ward));
        Assert.True(effects.IsRunningOn(party.Members[0].Id, Ward));

        // What a panel reads names the character the effect runs on, and says when it ends.
        RunningSpellEffect row = Assert.Single(effects.RunningOnMembers);
        Assert.Equal(party.Members[0].Id, row.Member);
        Assert.Equal(Ward, row.Effect);
        Assert.Equal(12, row.Magnitude);
        Assert.Equal(new GameDate(1168, 1, 1, 11, 0), row.EndsAt);
        Assert.Equal(row.EndsAt, ward.EndsAt);

        // And only that character's own entry moved: the party's own carried effects are untouched.
        Assert.Empty(effects.Running);
    }

    [Fact]
    public void A_duration_ends_when_the_clock_passes_its_deadline_and_not_before()
    {
        using PartyEntity party = Party();
        GameClock clock = Clock();
        RunningSpellEffects effects = new(party, clock, member => !LaidOut(member));
        effects.StartOn(party.Members[0], Ward, magnitude: 5, GameDuration.FromHours(3));

        // Two hours in, the ward stands: nothing here counts updates, so what ends it is the clock reaching
        // the moment it was given.
        effects.Observe(clock.Advance(GameDuration.FromHours(2)));
        Assert.Equal(5, effects.MagnitudeOn(party.Members[0], Ward));
        EffectId entry = Assert.Single(party.Effects.Active).Effect;

        // The advance that reaches the deadline exactly ends it — and the last minute before it does not.
        effects.Observe(clock.Advance(GameDuration.FromMinutes(59)));
        Assert.Equal(5, effects.MagnitudeOn(party.Members[0], Ward));

        effects.Observe(clock.Advance(GameDuration.FromMinutes(1)));
        Assert.Equal(0, effects.MagnitudeOn(party.Members[0], Ward));
        Assert.Empty(effects.RunningOnMembers);
        Assert.False(party.Effects.Has(entry));
    }

    [Fact]
    public void Death_a_dispelling_and_the_clock_each_end_what_one_character_carries()
    {
        // Death: a character the game reports as no longer carrying their effects loses them where the ledger
        // next reads, which is the only place anything is told that a wound laid somebody out.
        using (PartyEntity party = Party())
        {
            GameClock clock = Clock();
            RunningSpellEffects effects = new(party, clock, member => !LaidOut(member));
            effects.StartOn(party.Members[0], Ward, magnitude: 7, GameDuration.FromHours(4));
            effects.Start(Haste, magnitude: 25, GameDuration.FromHours(4));

            party.Members[0].Conditions.Apply(new ActiveCondition(Dead));

            Assert.Equal(0, effects.MagnitudeOn(party.Members[0], Ward));
            Assert.Empty(effects.RunningOnMembers);

            // The party's own haste is not a character's effect and death does not end it.
            Assert.True(effects.IsRunning(Haste));
        }

        // The deadline, which is what the clock brings due.
        using (PartyEntity party = Party())
        {
            GameClock clock = Clock();
            RunningSpellEffects effects = new(party, clock, member => !LaidOut(member));
            effects.StartOn(party.Members[1], Ward, magnitude: 3, GameDuration.FromHours(1));
            effects.Observe(clock.Advance(GameDuration.FromHours(1)));
            Assert.Empty(effects.RunningOnMembers);
        }

        // A dispelling ends what spells left running, on the characters and on the party alike, and leaves
        // what a counter sold exactly where it was.
        using (PartyEntity party = Party())
        {
            RunningSpellEffects effects = new(party, Clock(), member => !LaidOut(member));
            effects.StartOn(party.Members[0], Ward, magnitude: 9, GameDuration.FromHours(2));
            effects.Start(Haste, magnitude: 25, GameDuration.FromHours(2));
            EffectId passage = new("passage:2");
            party.Effects.Apply(new PartyEffect(passage, 3));

            IReadOnlyList<RunningSpellEffect> ended = effects.EndAll();

            Assert.Equal(2, ended.Count);
            Assert.Empty(effects.RunningOnMembers);
            Assert.Empty(effects.Running);
            Assert.True(party.Effects.Has(passage));
        }
    }

    [Fact]
    public void A_scroll_is_cast_once_from_the_item_which_is_used_up()
    {
        using PartyEntity party = Party();
        GameClock clock = Clock();
        Effects effects = new(party, clock, member => !LaidOut(member));
        Spellcasting casting = Casting(party, effects);
        ItemInstance scroll = Take(party, ScrollOfBolt);
        int points = party.Members[0].Resources.SpellPoints.Current;

        // The caster has never learned the spell: an item is how a character casts what is not theirs, so the
        // spellbook and the school's mastery are not asked about.
        Assert.False(party.Members[0].Spells.Knows(Bolt));

        SpellCastResult result = casting.Cast(new SpellCastRequest(0, Bolt, "beast", scroll.Id));

        Assert.True(result.IsCast);
        Assert.Equal(0, result.Cost);
        Assert.Equal(points, party.Members[0].Resources.SpellPoints.Current);
        Assert.Contains("from Scroll of a bolt", result.Message, StringComparison.Ordinal);
        Assert.Equal("Scroll of a bolt", result.Source);

        // One use: the scroll has left the party, through the inventory's own custody rather than by an edit
        // to a member's figure.
        Assert.Null(party.FindItem(scroll.Id));
        Assert.False(scroll.IsHeld);
        Assert.DoesNotContain(party.Items, item => item.Id == scroll.Id);

        // A second casting from the same scroll names an item the party no longer holds.
        SpellCastResult again = casting.Cast(new SpellCastRequest(0, Bolt, "beast", scroll.Id));
        Assert.False(again.IsCast);
        Assert.Equal("spell-item-not-held", again.Code);
    }

    [Fact]
    public void A_scroll_is_refused_when_the_item_carries_nothing_this_game_reads_or_its_caster_cannot_cast()
    {
        using PartyEntity party = Party();
        Effects effects = new(party, Clock(), member => !LaidOut(member));
        Spellcasting casting = Casting(party, effects);

        // An item nothing reads a spell for is refused by name, and stays in the pack.
        ItemInstance token = Take(party, Token);
        SpellCastResult unreadable = casting.Cast(new SpellCastRequest(0, Bolt, "beast", token.Id));
        Assert.False(unreadable.IsCast);
        Assert.Equal("spell-item-carries-none", unreadable.Code);
        Assert.NotNull(party.FindItem(token.Id));

        // An item that carries a spell this game's content does not declare is refused by name too, and stays
        // in the pack: nothing was cast, so nothing is spent.
        ItemInstance orphan = party.CreateItem(new ItemDefinitionId("scroll-of-unknown"));
        Assert.True(party.AcquireItem(orphan).Admitted);
        SpellCastResult unknown = casting.Cast(new SpellCastRequest(0, new SpellId("404"), "beast", orphan.Id));
        Assert.False(unknown.IsCast);
        Assert.Equal("spell-unknown", unknown.Code);
        Assert.NotNull(party.FindItem(orphan.Id));

        // A caster the game has laid out cannot cast from an item either: the effect path's own judgement
        // stops it before the item is spent.
        ItemInstance scroll = Take(party, ScrollOfBolt);
        party.Members[0].Conditions.Apply(new ActiveCondition(Unconscious));
        SpellCastResult refused = casting.Cast(new SpellCastRequest(0, Bolt, "beast", scroll.Id));
        Assert.False(refused.IsCast);
        Assert.Equal("spell-caster-cannot-act", refused.Code);
        Assert.NotNull(party.FindItem(scroll.Id));
    }

    [Fact]
    public void A_charged_item_spends_one_charge_a_use_is_refused_at_zero_and_the_item_vanishes_through_the_inventory()
    {
        using PartyEntity party = Party();
        GameClock clock = Clock();
        Effects effects = new(party, clock, member => !LaidOut(member));
        Spellcasting casting = Casting(party, effects);

        // A wand in the hand: an item that holds charges is a weapon, so it is wielded before its spell can
        // be aimed, and it spends a charge per use.
        ItemInstance wand = Take(party, WandOfBolt);
        Assert.True(party.Equip(party.Members[0].Id, Hand, wand.Id).Admitted);

        SpellCastResult packed = UseFromPack(party, effects, WandOfBolt);
        Assert.False(packed.IsCast);
        Assert.Equal("spell-item-not-wielded", packed.Code);

        SpellCastResult first = casting.Cast(new SpellCastRequest(0, Bolt, "beast", wand.Id));
        Assert.True(first.IsCast);
        Assert.Equal(1, wand.State.ChargesSpent);
        Assert.Equal(2, WandCharges - wand.State.ChargesSpent);
        Assert.True(party.FindItem(wand.Id) is not null);

        SpellCastResult second = casting.Cast(new SpellCastRequest(0, Bolt, "beast", wand.Id));
        Assert.True(second.IsCast);
        Assert.Equal(2, wand.State.ChargesSpent);

        // The last charge takes the item with it: it is out of the member's hand and out of the party, through
        // the same custody route a release takes.
        SpellCastResult last = casting.Cast(new SpellCastRequest(0, Bolt, "beast", wand.Id));
        Assert.True(last.IsCast);
        Assert.Null(party.FindItem(wand.Id));
        Assert.False(wand.IsHeld);
        Assert.False(party.Members[0].Equipment.Has(Hand));

        // A third use names an item nobody holds.
        SpellCastResult gone = casting.Cast(new SpellCastRequest(0, Bolt, "beast", wand.Id));
        Assert.False(gone.IsCast);
        Assert.Equal("spell-item-not-held", gone.Code);

        // An item whose charges are already spent — what a save or a piece of content may record — refuses a
        // use by name and stays exactly where it lies, which is the discharged wand the donor calls not
        // functional (OpenEnroth src/Engine/Objects/Item.cpp:755-757).
        using PartyEntity discharged = Party();
        ItemInstance spent = new ItemInstance(
            discharged.Identity.MintItemId(),
            WandOfBolt,
            stackCount: 1,
            state: new ItemState(isIdentified: true, damage: 0, enchantments: null, chargesSpent: WandCharges));
        Assert.True(discharged.AcquireItem(spent).Admitted);
        Assert.True(discharged.Equip(discharged.Members[0].Id, Hand, spent.Id).Admitted);

        ItemChargeSpend refused = discharged.SpendItemCharge(spent.Id, WandCharges);
        Assert.False(refused.Spent);
        Assert.Equal("item-no-charges", refused.Refusal!.Code);
        Assert.True(discharged.Members[0].Equipment.Has(Hand));
    }

    [Fact]
    public void A_charged_item_is_fired_through_the_fights_own_order_path_and_meets_its_recovery()
    {
        Weapons weapons = new();
        Rules rules = new(weapons);
        using PartyEntity party = Party();
        using SessionWorld world = World(party, creatureAt: 100);
        Arrive(world);
        CombatState fight = new(rules, party, world, Clock());

        // What the actor's own hand holds decides how it attacks: the weapon answer names a spell-kind attack
        // with the item's own ability, and the fight reads that where it re-reads its actors.
        ItemInstance wand = Take(party, WandOfBolt);
        weapons.Charge = wand.Id;
        weapons.Charges = WandCharges;
        weapons.Ability = Bolt.Value;
        weapons.Kind = AttackKind.Spell;

        fight.Step();
        Combatant member = fight.Combatants.First(combatant => combatant.Side == CombatSide.Party);
        Assert.Equal(AttackKind.Spell, member.PreferredKind);

        // With nothing to aim at, the attack is refused by name and no charge is spent: an order that names
        // no target is an attack at nothing for a fist, and a spent charge for nothing for a charged item.
        CombatResult nowhere = fight.Order(new AttackOrder(member.Id, AttackKind.Spell, Target: null));
        Assert.False(nowhere.IsApplied);
        Assert.Equal("weapon-no-target", nowhere.Code);
        Assert.Equal(0, wand.State.ChargesSpent);

        // The order the act control gives resolves the wand's own spell through the fight's resolution, spends
        // one charge of the item, and charges the actor's recovery for it.
        rules.Damage = new DamageRoll(dice: 1, sides: 6, bonus: 10);
        CombatResult shot = fight.Engage(member.Id);
        Assert.True(shot.IsApplied);
        Assert.NotNull(shot.Resolution);
        Assert.True(shot.Resolution!.Hit);
        Assert.Equal(AttackKind.Spell, shot.Initiated!.Kind);
        Assert.Equal(Bolt.Value, shot.Initiated.Ability);
        Assert.Equal(11, shot.Resolution.Damage);
        Assert.Equal(1, wand.State.ChargesSpent);
        Assert.Equal(Rules.Shot, shot.Initiated.Recovery);

        // An item the party no longer holds refuses the attack by name rather than firing a charge nobody
        // has — and the actor still owes the recovery of the shot it did make, so the order is asked once that
        // has elapsed.
        party.ConsumeItem(wand.Id);
        fight.Step();
        Advance(fight, Rules.Shot);
        CombatResult missing = fight.Engage(member.Id);
        Assert.False(missing.IsApplied);
        Assert.Equal("item-not-held", missing.Code);
    }

    [Fact]
    public void Charges_survive_a_save_round_trip_and_a_running_duration_is_refused_by_name_rather_than_dropped()
    {
        using PartyEntity party = Party();
        GameClock clock = Clock();
        RunningSpellEffects effects = new(party, clock, member => !LaidOut(member));

        // A party with a half-spent wand and a read scroll: both are item state, so both travel in the save.
        ItemInstance wand = Take(party, WandOfBolt);
        Assert.True(party.Equip(party.Members[0].Id, Hand, wand.Id).Admitted);
        party.SpendItemCharge(wand.Id, WandCharges);
        ItemInstance scroll = Take(party, ScrollOfBolt);
        party.ConsumeItem(scroll.Id);

        PartySave save = party.Capture();
        using PartyEntity restored = new PartyEntityFactory().Restore(save);
        ItemInstance? carried = restored.FindItem(wand.Id);

        Assert.NotNull(carried);
        Assert.Equal(1, carried!.State.ChargesSpent);
        Assert.True(carried.Custody.IsEquipped);
        Assert.Null(restored.FindItem(scroll.Id));

        // A ward is carried by the party's own effect state, under the character it was cast on, so what a
        // save would carry is the effect's existence and magnitude.
        effects.StartOn(party.Members[0], Ward, magnitude: 8, GameDuration.FromHours(1));
        PartySave warded = party.Capture();
        Assert.Contains(
            warded.Effects,
            effect => effect.Magnitude == 8 && effect.Effect.Value.StartsWith(Ward.Value, StringComparison.Ordinal));
        using PartyEntity resumed = new PartyEntityFactory().Restore(warded);
        RunningSpellEffects reloaded = new(resumed, Clock(), member => !LaidOut(member));
        Assert.Equal(8, reloaded.MagnitudeOn(resumed.Members[0], Ward));

        // What the save does not carry is the deadline, and today that is a refusal rather than a silent
        // loss: the clock holding a registered deadline cannot be captured at all, so a session with a ward
        // running says so by name (Den task #8617 owns carrying deadlines in the save).
        SessionSaveException refused = Assert.Throws<SessionSaveException>(() => ClockSave.Capture(clock));
        Assert.Contains("deadline", refused.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(refused.Problems);
    }

    [Fact]
    public void The_panel_publishes_what_each_character_carries_and_the_magic_the_pack_holds()
    {
        using PartyEntity party = Party();
        GameClock clock = Clock();
        Effects effects = new(party, clock, member => !LaidOut(member));
        Spellcasting casting = Casting(party, effects);
        effects.StartOn(party.Members[1], Ward, magnitude: 12, GameDuration.FromHours(1));
        effects.Start(Haste, magnitude: 25, GameDuration.FromHours(1));
        ItemInstance wand = Take(party, WandOfBolt);
        Assert.True(party.Equip(party.Members[0].Id, Hand, wand.Id).Admitted);
        Take(party, ScrollOfBolt);

        MagicSnapshot magic = MagicSnapshot.From(casting);

        // The party's own carried effect stays on the party's own row, and the character's own effect is a row
        // naming them: the two are never mixed, which is what lets a panel show that one member is warded.
        SpellRunningSnapshot haste = Assert.Single(magic.Running);
        Assert.Equal(Haste.Value, haste.Effect);
        SpellMemberRunningSnapshot row = Assert.Single(magic.MemberRunning);
        Assert.Equal(party.Members[1].Id.ToString(), row.Member);
        Assert.Equal("Borin", row.Name);
        Assert.Equal(Ward.Value, row.Effect);
        Assert.Equal(12, row.Magnitude);
        Assert.Equal("1168-01-01 10:00", row.EndsAt);

        // The pack's own carried magic: the wand with how much of it is left and who is wearing it, and the
        // scroll with the one use it holds. Nothing here is the panel's own arithmetic.
        Assert.Equal(2, magic.Items.Count);
        SpellItemSnapshot carriedWand = magic.Items.Single(item => item.Item == wand.Id.ToString());
        Assert.Equal("wand of a bolt", carriedWand.Name);
        Assert.Equal("charged", carriedWand.Kind);
        Assert.Equal(WandCharges, carriedWand.Charges);
        Assert.Equal(WandCharges, carriedWand.ChargesMax);
        Assert.True(carriedWand.Wielded);
        Assert.Equal("Nyx", carriedWand.Member);
        SpellItemSnapshot carriedScroll = magic.Items.Single(item => item.Kind == "consumed");
        Assert.Equal("Scroll of a bolt", carriedScroll.Name);
        Assert.Equal(0, carriedScroll.Charges);
        Assert.False(carriedScroll.Wielded);
    }

    /// <summary>How many charges this suite's wand holds, which is the reading its own rule states.</summary>
    private const int WandCharges = 3;

    /// <summary>Casts from a wand lying in the pack, so the refusal about wielding is what is proved.</summary>
    private static SpellCastResult UseFromPack(PartyEntity party, Effects effects, ItemDefinitionId definition)
    {
        ItemInstance inPack = party.CreateItem(definition);
        Assert.True(party.AcquireItem(inPack).Admitted);
        Spellcasting casting = Casting(party, effects);
        return casting.Cast(new SpellCastRequest(0, Bolt, "beast", inPack.Id));
    }

    /// <summary>Takes one item of a definition into the party's pack and hands the instance back.</summary>
    private static ItemInstance Take(PartyEntity party, ItemDefinitionId definition)
    {
        ItemInstance item = party.CreateItem(definition);
        Assert.True(party.AcquireItem(item).Admitted);
        return item;
    }

    /// <summary>The one casting workflow, over this suite's spell rule and effect path.</summary>
    private static Spellcasting Casting(PartyEntity party, Effects effects) => new(party, new Spells(), effects);

    /// <summary>Whether a character the game has laid out carries nothing a spell left.</summary>
    private static bool LaidOut(PartyMember member) =>
        member.Conditions.Has(Dead) || member.Conditions.Has(Unconscious);

    /// <summary>A party of three, so a ward that landed on everybody would be visible.</summary>
    private static PartyEntity Party() =>
        new PartyEntityFactory(health: new Thresholds()).Create(new PartyCreation(
            [
                Member("Nyx"),
                Member("Borin"),
                Member("Aelina"),
            ],
            coins: 0,
            foodPortions: 6,
            reputation: 0,
            fame: 0));

    private static MemberCreation Member(string name) =>
        new(new PartyMemberSeed(
            name,
            TestRace,
            Mage,
            [new AttributeScore(Intellect, 20)],
            [new SkillEntry(FireSkill, 4, new SkillTier(1), 0)],
            [],
            experience: 0,
            level: 1,
            skillPoints: 0,
            classRank: 1,
            conditions: [],
            hitPoints: ResourcePool.Full(30),
            spellPoints: ResourcePool.Full(20)));

    /// <summary>The session's one clock, at this game's own rate and on its own calendar.</summary>
    private static GameClock Clock() => new(
        GameCalendar.TwelveMonthsOfFourWeeks,
        new GameDate(1168, 1, 1, 9, 0),
        new GameTimeScale(30),
        new DaylightWindow(new TimeOfDay(5, 0), new TimeOfDay(21, 0)));

    /// <summary>Lets an actor's recovery elapse, as the session's own clock advances it.</summary>
    private static void Advance(CombatState fight, GameDuration elapsed) => fight.Observe(new ClockAdvance(
        new GameDate(1168, 1, 1, 9, 0),
        new GameDate(1168, 1, 1, 9, 0),
        elapsed,
        PeriodCrossings.None,
        []));

    /// <summary>Puts the party in the hall and populates it, as one admitted update of the session does.</summary>
    private static void Arrive(SessionWorld world)
    {
        world.ArriveAt(Hall, new PlacePose(0, 0, 0, 0, 0));
        world.Populate();
    }

    /// <summary>A world holding one creature a hundred units off, so the fight has something to aim at.</summary>
    private static SessionWorld World(PartyEntity party, double creatureAt)
    {
        ContentCatalog catalog = ContentCatalogLoader.Load(
            new InMemoryContentSource()
                .Add(
                    "packs/world/pack.json",
                    """
                    {
                      "schemaVersion": 1, "packId": "world", "kind": "definitions",
                      "provenance": { "description": "test content" },
                      "documents": [ { "path": "places.json", "documentId": "places", "definitionKind": "place" } ]
                    }
                    """)
                .Add(
                    "packs/world/places.json",
                    $$"""
                    {
                      "documentId": "places",
                      "definitionKind": "place",
                      "entries": [
                        { "id": "1", "kind": "region", "name": "Hall", "respawnDays": 7,
                          "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
                          "placements": [
                            { "id": "beast", "kind": "creature", "x": {{creatureAt.ToString(CultureInfo.InvariantCulture)}}, "y": 0, "z": 0, "monster": "7",
                              "hitPoints": 40 }
                          ] }
                      ]
                    }
                    """),
            Layout).RequireValid();

        PlaceGraph graph = PlaceGraphLoader.Load(catalog);
        PartyPoseOwner pose = new(
            new PartyPose(Hall, new PlacePose(0, 0, 0, 0, 0)),
            new FacingRule(unitsPerTurn: 2048, minimumPitch: -512, maximumPitch: 512));
        return new SessionWorld(
            graph,
            pose,
            new PlaceStateLedger(graph, PlaceRespawnRule.FromContent()),
            new FreeTravel(),
            Clock(),
            mover: null,
            diagnostics: null,
            entrances: null,
            clock: Clock(),
            resources: null,
            partyEntity: party,
            interaction: null,
            schedule: null);
    }

    /// <summary>Walking is free: nothing in these tests is about what a road costs.</summary>
    private sealed class FreeTravel : ITravelCostRule
    {
        public TravelCostQuote Quote(TransitionRequest request) => TravelCostQuote.Payable(TravelCost.Free);
    }

    /// <summary>
    /// The spell table this suite casts from: two spells content declares, one item that carries each, and
    /// nothing else.
    /// </summary>
    private sealed class Spells : ISpellRule, ISpellItemRule, ISpellItemNames
    {
        private static readonly SpellCatalog Declared = new(
        [
            new SpellDefinition(Bolt, "a bolt", "fire", FireSkill, new SkillTier(1), Cost: 3, SpellTargeting.None, "damage"),
            new SpellDefinition(Portal, "a portal", "fire", FireSkill, new SkillTier(1), Cost: 5, SpellTargeting.None, "travel"),
        ]);

        public SpellCatalog Catalog => Declared;

        public int SpellPointCapacity(PartyMember member) => 20;

        public int CostFor(PartyMember member, SpellDefinition spell) => spell.Cost;

        public SpellRefusal? MayLearn(PartyMember member, SpellDefinition spell) => null;

        /// <summary>
        /// What this suite's items carry: a scroll used up by its one spell, a wand holding three uses, a
        /// trinket carrying nothing, and a scroll of a spell content never declares.
        /// </summary>
        public SpellItemReading? Reading(ItemDefinitionId definition)
        {
            if (definition == ScrollOfBolt) return SpellItemReading.Consumed(Bolt);
            if (definition == WandOfBolt) return SpellItemReading.Charged(Bolt, WandCharges);
            if (definition.Value == "scroll-of-unknown") return SpellItemReading.Consumed(new SpellId("404"));
            return null;
        }

        public string NameOf(ItemDefinitionId definition) => definition switch
        {
            _ when definition == ScrollOfBolt => "Scroll of a bolt",
            _ when definition == WandOfBolt => "wand of a bolt",
            _ => string.Empty,
        };
    }

    /// <summary>
    /// The effect path this suite casts through: it applies what the suite states, leaves a per-character ward
    /// running with a deadline, and answers what one character carries.
    /// </summary>
    private sealed class Effects : ISpellEffectRule, IRunningSpellEffects, IMemberSpellEffects
    {
        private readonly RunningSpellEffects _running;

        internal Effects(PartyEntity party, GameClock clock, Func<PartyMember, bool> carries) =>
            _running = new RunningSpellEffects(party, clock, carries);

        /// <summary>Leaves an effect on the party, as a spell aimed at the band does.</summary>
        internal RunningSpellEffect Start(EffectId effect, int magnitude, GameDuration? lasts) =>
            _running.Start(effect, magnitude, lasts);

        /// <summary>Leaves an effect on one character, as a spell aimed at them does.</summary>
        internal RunningSpellEffect StartOn(PartyMember member, EffectId effect, int magnitude, GameDuration? lasts) =>
            _running.StartOn(member, effect, magnitude, lasts);

        /// <summary>Hands one advance of the clock to the effects it ends.</summary>
        internal void Observe(ClockAdvance advance) => _running.Observe(advance);

        /// <summary>Whether one character carries one effect.</summary>
        internal bool IsRunningOn(PartyMemberId member, EffectId effect) => _running.IsRunningOn(member, effect);

        /// <summary>Ends everything a spell left running, which is what a dispelling does.</summary>
        internal IReadOnlyList<RunningSpellEffect> EndAll() => _running.EndAll();

        public IReadOnlyList<RunningSpellEffect> Running => _running.Running;

        public IReadOnlyList<RunningSpellEffect> RunningOnMembers => _running.RunningOnMembers;

        public bool IsRunning(EffectId effect) => _running.IsRunning(effect);

        public int MagnitudeOn(PartyMember member, EffectId effect) => _running.MagnitudeOn(member, effect);

        public SpellRefusal? Judge(SpellApplication application) =>
            application.Caster.Conditions.Count > 0
                ? SpellRefusal.CannotAct(application.Caster.Profile.Name, "what is acting on them leaves them unable to cast")
                : null;

        public SpellApplicationOutcome Apply(SpellApplication application) => SpellApplicationOutcome.Expressed(
            application.Spell.Effect,
            $"{application.Spell.Name} was applied to '{application.TargetName}'.",
            [new SpellEffectFact("target", application.TargetName)]);
    }

    /// <summary>The weapon answer this suite's fight reads, which names the instance it spends a charge of.</summary>
    private sealed class Weapons
    {
        internal AttackKind Kind { get; set; } = AttackKind.Melee;

        internal string Ability { get; set; } = string.Empty;

        internal ItemInstanceId? Charge { get; set; }

        internal int Charges { get; set; }
    }

    /// <summary>
    /// The readings this suite's fights are resolved with: a chance, dice, what a hit leaves, who may act, what
    /// a target can take, and the weapon an actor brings.
    /// </summary>
    private sealed class Rules : ICombatRule, ICombatResolutionRule, ICombatAbilityResolutionRule, ICombatWeaponRule
    {
        private readonly Weapons _weapons;

        internal Rules(Weapons weapons) => _weapons = weapons;

        internal DamageRoll Damage { get; set; } = new(dice: 1, sides: 4);

        /// <summary>What one shot with a charged item costs the actor, so the recovery it charged is readable.</summary>
        internal static GameDuration Shot => GameDuration.FromSeconds(2);

        public string NameOf(CombatSubject subject) => subject.Member?.Profile.Name ??
            (subject.Placement?.Content.Kind == "creature" ? "a test creature" : "something else");

        public Hostility NatureOf(CombatSubject subject) => subject.Member is not null
            ? Hostility.Peaceful
            : subject.Placement?.Content.Kind == "creature" ? Hostility.Aggressive(500) : Hostility.Inert;

        public AttackKind AttackKindFor(CombatSubject subject) => AttackKind.Melee;

        /// <summary>The weapon the suite states, which the fight reads where a hand's own weapon decides.</summary>
        public CombatWeapon? WeaponOf(CombatSubject attacker) => _weapons.Charge is { } charge
            ? new CombatWeapon(_weapons.Kind, _weapons.Ability, charge, _weapons.Charges)
            : null;

        public GameDuration RecoveryAfter(CombatSubject subject, AttackKind kind) =>
            kind == AttackKind.Spell ? Shot : GameDuration.FromSeconds(1);

        public GameDuration InitialRecovery(CombatSubject subject, AttackKind kind) => GameDuration.None;

        public double ReachOf(CombatSubject subject, AttackKind kind) => 1000;

        public IAttackRolls? RollsFor(CombatSubject attacker, string key) => new Scripted(roll: 0, face: 1);

        public AttackPlan PlanOf(CombatSubject attacker, CombatSubject target, AttackKind kind) =>
            new(HitChance.Always, Steel, Damage, Resistance.Of(0));

        /// <summary>A named ability resolves with the suite's own dice, which is what a wand's shot is.</summary>
        public AttackPlan PlanOfAbility(CombatSubject attacker, CombatSubject target, AttackKind kind, string ability) =>
            new(HitChance.Always, Steel, Damage, Resistance.Of(0));

        public int DamageAfterResistance(CombatSubject target, DamageKindId kind, int damage, IAttackRolls rolls) => damage;

        public CombatCondition? ConditionOf(CombatSubject attacker, CombatSubject target, DamageKindId kind, IAttackRolls rolls) => null;

        public bool CanAct(CombatSubject subject) => true;

        public int HitPointsOf(CombatSubject subject) => subject.Member is { } member
            ? member.Resources.HitPoints.Maximum
            : subject.Placement?.Source.GetInt32("hitPoints") ?? 0;
    }

    /// <summary>The thresholds this suite's wounds are judged against, standing in for a game's own.</summary>
    private sealed class Thresholds : ICharacterHealthRule
    {
        public CharacterCollapse Collapse(PartyMember member, int hitPoints, int deficit)
        {
            if (hitPoints - deficit >= 1) return CharacterCollapse.None;
            return deficit < 6
                ? new CharacterCollapse(new ActiveCondition(Unconscious))
                : new CharacterCollapse(new ActiveCondition(Dead), [Unconscious]);
        }
    }

    /// <summary>The rolls a test states: one value for every purpose, so a boundary is exact.</summary>
    private sealed class Scripted(int roll, int face) : IAttackRolls
    {
        public int Roll(string purpose, int minimum, int maximum) => Math.Clamp(roll, minimum, maximum);

        public DamageRoll Face(string purpose, int sides) => new(dice: 1, sides: face);
    }
}
