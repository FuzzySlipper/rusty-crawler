using System.Globalization;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using PartyRpg.Rulesets.MightAndMagic7;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Host.Tests;

/// <summary>
/// What a spell does, category by category, through the whole product: a heal that restores a wounded member,
/// a condition cured and one left behind, a resistance that changes what the fight's own reading makes of a
/// blow, a light that follows the clock's own daylight, a portal taken through the world's own transition
/// path, a report read from the places the world holds, and a haste that shortens what the fight charges for
/// an action.
/// </summary>
/// <remarks>
/// <para>
/// Every case here casts a shipped spell by its own global id through the product's own session — the same
/// casting workflow, the same effect path, the same fight, the same world, and the same projection a player
/// meets — and reads what happened off the panel rather than out of the ruleset. What a suite of the kit
/// alone cannot prove is exactly this: that a spell's category is applied through the owner that already
/// holds the state it changes.
/// </para>
/// <para>
/// The values the casts are worth are the donor's own, cited where the ruleset states them, so a case that
/// asserted a different number would be asserting a different game.
/// </para>
/// </remarks>
public sealed class SpellEffectPolicyTests
{
    private static readonly PlaceId Home = new("1");
    private static readonly PlaceId Cave = new("2");

    [Fact]
    public void A_healing_spell_restores_a_wounded_member_through_their_own_pool()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(Content());
        using IGameSession session = Casting(context, ui);
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        PartyMember wounded = live.Party!.Members[1];
        wounded.Resources.TakeDamage(10);
        int before = wounded.Resources.HitPoints.Current;

        // Heal is the donor's first aid: the caster's level in the school times the mastery rung plus five
        // (OpenEnroth src/Engine/Spells/CastSpellInfo.cpp:2244-2262). A body skill of two at the basic rung is
        // therefore seven hit points, and the pool it lands in is the member's own.
        Cast(session, ui, 1, "68", Target(wounded));

        ProjectedNode magic = Magic(ui);
        Assert.Equal("cast", magic.Field("outcome").AsString());
        Assert.Equal("healing", magic.Field("effect").AsString());
        Assert.Equal(2d, magic.Field("cost").AsNumber());
        Assert.Equal(before + 7, wounded.Resources.HitPoints.Current);
        AssertFact(magic, "hitPoints", $"{wounded.Profile.Name} {before + 7}/40");
        Assert.Contains("hit point(s) each", magic.Field("message").AsString(), StringComparison.Ordinal);

        // The caster paid for it out of their own pool, and the fact the panel published is the pool's own
        // reading rather than a number the effect path kept beside it.
        Assert.Equal(
            live.Party.Members[0].Resources.SpellPoints.Current,
            (int)magic.Field("members").Item(0).Field("spellPoints").AsNumber());
    }

    [Fact]
    public void A_curing_spell_lifts_a_condition_and_a_raising_leaves_the_weakness_it_states()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(Content());
        using IGameSession session = Casting(context, ui);
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        PartyMember afflicted = live.Party!.Members[1];
        afflicted.Conditions.Apply(new ActiveCondition(MightAndMagic7Conditions.Paralyzed, 1));

        // A cure clears what the member's own condition state holds, which is the same state a creature's
        // paralysis and a trap's poison land in.
        Cast(session, ui, 1, "61", Target(afflicted));
        ProjectedNode cured = Magic(ui);
        Assert.Equal("cast", cured.Field("outcome").AsString());
        Assert.False(afflicted.Conditions.Has(MightAndMagic7Conditions.Paralyzed));
        Assert.Equal(string.Empty, Fact(cured, "conditions"));

        // A raising is the one healing shape that touches two owners: it stands the member up in their own
        // pool and lifts the conditions that laid them out — and leaves the weakness the donor's own raising
        // leaves (OpenEnroth src/Engine/Spells/CastSpellInfo.cpp:1766-1800).
        afflicted.Resources.TakeDamage(1000);
        afflicted.Conditions.Apply(new ActiveCondition(MightAndMagic7Conditions.Dead, 1));
        Cast(session, ui, 1, "53", Target(afflicted));
        ProjectedNode raised = Magic(ui);
        Assert.Equal("cast", raised.Field("outcome").AsString());
        Assert.Equal(1, afflicted.Resources.HitPoints.Current);
        Assert.False(afflicted.Conditions.Has(MightAndMagic7Conditions.Dead));
        Assert.True(afflicted.Conditions.Has(MightAndMagic7Conditions.Weak));
        Assert.Equal(0, afflicted.Conditions.SeverityOf(MightAndMagic7Conditions.Weak));
        Assert.Contains("left weak", raised.Field("message").AsString(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_resistance_spell_changes_the_fights_own_reading_of_a_blow()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(Content());
        using IGameSession session = Casting(context, ui);
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        MightAndMagic7Combat policy = Fight(context, live.Party!);
        CombatSubject defender = Defender(live, policy);
        CombatSubject attacker = Attacker(live, policy);

        // Before the ward the fight reads no resistance at all, which is this build's unarmoured party: a
        // hundred points of fire lands whole.
        AttackPlan bare = policy.PlanOf(attacker, defender, AttackKind.Melee);
        Assert.Equal(MightAndMagic7Damage.Fire, bare.Kind);
        Assert.Equal(0, bare.Resistance.Points);
        Assert.Equal(100, policy.DamageAfterResistance(defender, MightAndMagic7Damage.Fire, 100, Rolls));

        // Fire resistance is the donor's own ward: the school's level times the mastery rung, for an hour a
        // level (OpenEnroth src/Engine/Spells/Spells.cpp:748-762).
        Cast(session, ui, 1, "3", Target(live.Party!.Members[0]));
        ProjectedNode magic = Magic(ui);
        Assert.Equal("cast", magic.Field("outcome").AsString());
        Assert.Equal("resistance", magic.Field("effect").AsString());
        Assert.Equal(12d, Running(magic, "spell.resist.Fire").Field("magnitude").AsNumber());

        // The same blow, read by the same fight, is now resisted: it takes its share four times over, which is
        // measurable in what lands rather than merely stated.
        AttackPlan warded = policy.PlanOf(attacker, defender, AttackKind.Melee);
        Assert.Equal(12, warded.Resistance.Points);
        Assert.Equal(6, policy.DamageAfterResistance(defender, MightAndMagic7Damage.Fire, 100, Rolls));
    }

    [Fact]
    public void A_light_follows_the_clocks_own_daylight_and_ends_when_its_deadline_does()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(Content());
        using IGameSession session = Casting(context, ui);

        // Noon is daylight, so the panel says the party sees by the day before anything is cast.
        Assert.Equal("daylight", Magic(ui).Field("sight").AsString());

        // The clock moves to the last hour before dusk: twelve game hours at this game's thirty-to-one rate.
        Advance(session, 12);
        Assert.Equal("21:00", ProjectedNode.Of(ui.Latest().Value).Field("clock").Field("time").AsString());
        Assert.Equal("dark", Magic(ui).Field("sight").AsString());

        // The donor's torch light lasts an hour per level of the school, and it stands in for the sun: six
        // levels of fire are six hours, which ends at three in the morning, and the panel says so.
        Cast(session, ui, 1, "1", string.Empty);
        ProjectedNode lit = Magic(ui);
        Assert.Equal("light", lit.Field("sight").AsString());
        ProjectedNode running = Running(lit, "spell.light");
        Assert.Equal(3d, running.Field("magnitude").AsNumber());
        Assert.Equal("1168-01-02 03:00", running.Field("endsAt").AsString());

        // The deadline is the clock's own: five hours later the light is still running and the panel still
        // reads the party as carrying it.
        Advance(session, 5);
        Assert.Equal("02:00", ProjectedNode.Of(ui.Latest().Value).Field("clock").Field("time").AsString());
        Assert.Equal("light", Magic(ui).Field("sight").AsString());

        // The clock reaching the deadline is what ends it, and nothing counted an update to find out: the
        // party is in the dark again the moment the hour arrives.
        Advance(session, 1);
        Assert.Equal("03:00", ProjectedNode.Of(ui.Latest().Value).Field("clock").Field("time").AsString());
        ProjectedNode after = Magic(ui);
        Assert.Equal("dark", after.Field("sight").AsString());
        Assert.Equal(0d, after.Field("running").Length());

        // And the day's own return is daylight with nothing carried: the panel reads the clock's window rather
        // than a light that outlived the night.
        Advance(session, 2);
        Assert.Equal("05:00", ProjectedNode.Of(ui.Latest().Value).Field("clock").Field("time").AsString());
        Assert.Equal("daylight", Magic(ui).Field("sight").AsString());
    }

    [Fact]
    public void A_travel_spell_takes_the_worlds_own_transition_path_to_a_place_the_party_knows()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(Content());
        using IGameSession session = Casting(context, ui);
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        SessionWorld world = live.World!;

        // The party walks into the cave through the world's own transition path, which is how a place becomes
        // one the party has been to. A portal may reach a place it knows and no other.
        world.Travel(Assert.Single(world.Graph.TransitionsFrom(Home)), TransitionKind.Entrance);
        Assert.Equal(Cave, world.Place);
        Assert.True(world.Places.StateOf(Home).Visited);
        Advance(session, 0);
        string road = ProjectedNode.Of(ui.Latest().Value).Field("clock").Field("date").AsString();

        // The panel offers the places a portal may name, which is the visited ones and not the place the party
        // stands in.
        ProjectedNode aims = Magic(ui).Field("members").Item(0).Field("spells").Item(AimIndex(ui, "31")).Field("aims");
        Assert.Equal(1d, aims.Length());
        Assert.Equal(Home.Value, aims.Item(0).Field("aim").AsString());
        Assert.Equal("place", aims.Item(0).Field("kind").AsString());

        Cast(session, ui, 1, "31", Home.Value);
        ProjectedNode magic = Magic(ui);
        Assert.Equal("cast", magic.Field("outcome").AsString());
        Assert.Equal("travel", magic.Field("effect").AsString());
        Assert.Equal(Home, world.Place);
        Assert.Equal(Cave.Value, Fact(magic, "from"));
        Assert.Equal(Home.Value, Fact(magic, "to"));
        Assert.Contains("portal opens", magic.Field("message").AsString(), StringComparison.Ordinal);

        // A portal crosses no ground, so it charges no road time: the entry the party bought with provisions
        // and a day on the road stands, and the spell's own points were the whole price of coming back.
        Assert.Equal(road, ProjectedNode.Of(ui.Latest().Value).Field("clock").Field("date").AsString());
    }

    [Fact]
    public void A_detection_spell_reports_over_the_places_and_the_population_the_world_holds()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(Content());
        using IGameSession session = Casting(context, ui);
        MightAndMagic7Session live = (MightAndMagic7Session)session;

        Cast(session, ui, 1, "12", string.Empty);
        ProjectedNode magic = Magic(ui);
        Assert.Equal("cast", magic.Field("outcome").AsString());
        Assert.Equal("detection", magic.Field("effect").AsString());

        // Every number is the world's own: how many places the party's state says it has been to, how many the
        // world holds, and what stands in the place it is in.
        Assert.Equal("1", Fact(magic, "places.visited"));
        Assert.Equal("2", Fact(magic, "places.total"));
        Assert.Equal("The Guild of Fire", Fact(magic, "here"));
        Assert.Equal("1", Fact(magic, "here.creatures"));
        Assert.Equal("0", Fact(magic, "here.people"));
        Assert.Contains("1 of 2 places are known", magic.Field("message").AsString(), StringComparison.Ordinal);

        // What it reports is the state and not a memory: a second casting after the party walks into the cave
        // reports the other place, because the world it reads is the world the party stands in.
        live.World!.Travel(Assert.Single(live.World.Graph.TransitionsFrom(Home)), TransitionKind.Entrance);
        Advance(session, 0);
        Cast(session, ui, 3, "12", string.Empty);
        ProjectedNode again = Magic(ui);
        Assert.Equal("2", Fact(again, "places.visited"));
        Assert.Equal("Cave", Fact(again, "here"));
    }

    [Fact]
    public void A_utility_spell_changes_what_the_fight_charges_and_a_dispelling_ends_it()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(Content());
        using IGameSession session = Casting(context, ui);
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        MightAndMagic7Combat policy = Fight(context, live.Party!);
        CombatSubject member = Defender(live, policy);

        // Haste is the donor's own recovery buff: twenty-five ticks off whatever an action costs
        // (OpenEnroth src/Engine/Objects/Character.cpp:1723-1728).
        GameDuration bare = policy.RecoveryAfter(member, AttackKind.Melee);
        Cast(session, ui, 1, "5", string.Empty);
        ProjectedNode magic = Magic(ui);
        Assert.Equal("cast", magic.Field("outcome").AsString());
        Assert.Equal("utility", magic.Field("effect").AsString());
        Assert.Equal(25d, Running(magic, "spell.haste").Field("magnitude").AsNumber());

        GameDuration hasted = policy.RecoveryAfter(member, AttackKind.Melee);
        Assert.True(hasted.Milliseconds < bare.Milliseconds, $"haste left recovery at {hasted.Milliseconds}ms against {bare.Milliseconds}ms");

        // A dispelling ends exactly what spells left running, and the fight's own answer goes back to what it
        // was: the effect path is the ledger of those effects, so nothing else on the party is touched.
        Cast(session, ui, 3, "80", string.Empty);
        ProjectedNode dispelled = Magic(ui);
        Assert.Equal("cast", dispelled.Field("outcome").AsString());
        Assert.Equal(0d, dispelled.Field("running").Length());
        Assert.Equal(bare.Milliseconds, policy.RecoveryAfter(member, AttackKind.Melee).Milliseconds);
    }

    [Fact]
    public void A_spell_whose_target_this_build_cannot_aim_at_is_refused_by_name_before_it_is_paid_for()
    {
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(Content());
        using IGameSession session = Casting(context, ui);
        MightAndMagic7Session live = (MightAndMagic7Session)session;
        int before = live.Party!.Members[0].Resources.SpellPoints.Current;

        // Telekinesis acts on a door or a container across the room. Nothing in this build can aim a spell at
        // one, so the cast is refused where it is judged — before a point is spent — and the sentence names
        // the owner that would have to supply the aim.
        Cast(session, ui, 1, "42", string.Empty);
        ProjectedNode refused = Magic(ui);
        Assert.Equal("refused", refused.Field("outcome").AsString());
        Assert.Equal("spell-target-unavailable", refused.Field("code").AsString());
        Assert.Contains("an item-aim owner", refused.Field("message").AsString(), StringComparison.Ordinal);
        Assert.Equal(before, live.Party.Members[0].Resources.SpellPoints.Current);

        // A travel spell whose destination the party has never been to is refused by name for the same reason
        // a bad aim is: the casting is judged where it is aimed rather than moving anybody.
        Cast(session, ui, 3, "31", Cave.Value);
        ProjectedNode nowhere = Magic(ui);
        Assert.Equal("spell-target-invalid", nowhere.Field("code").AsString());
        Assert.Contains("never been to", nowhere.Field("message").AsString(), StringComparison.Ordinal);
        Assert.Equal(Home, live.World!.Place);
        Assert.Equal(before, live.Party.Members[0].Resources.SpellPoints.Current);
    }

    /// <summary>A roll that always comes in high, which is what a resistance check has to beat.</summary>
    private static IAttackRolls Rolls { get; } = new AlwaysHigh();

    private static IGameSession Casting(ProductCreateContext context, RecordingUiService ui)
    {
        IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            ProductTestContext.RulesetContext(context, ui) with { Cast = CastControls });
        session.Start();
        return session;
    }

    /// <summary>The ruleset's own fight policy over the party the session plays, for reading a blow's worth.</summary>
    private static MightAndMagic7Combat Fight(ProductCreateContext context, PartyEntity party)
    {
        ContentCatalog catalog = ContentCatalogLoader.Load(
            new ProductContentSource(context.Content),
            ContentLayout.Under(ProductTestContext.ContentDirectory)).RequireValid();
        MightAndMagic7Spells spells = MightAndMagic7Spells.Read(catalog)
            ?? throw new InvalidOperationException("The content declares spells, so reading them must produce a table.");
        return MightAndMagic7Combat.Compose(catalog, random: null, fallen: null, spells, () => party);
    }

    /// <summary>A fight over the session's own world, read once, so its subjects are the live ones.</summary>
    private static CombatState FightOf(MightAndMagic7Session live, ICombatRule rule)
    {
        CombatState fight = new(rule, live.Party!, live.World);
        fight.Step();
        return fight;
    }

    /// <summary>The party member a blow lands on, as the fight itself knows it.</summary>
    private static CombatSubject Defender(MightAndMagic7Session live, ICombatRule rule)
    {
        CombatState fight = FightOf(live, rule);
        return fight.Combatants.First(combatant => combatant.Subject.IsMember).Subject;
    }

    /// <summary>The creature a blow comes from, as the fight itself knows it.</summary>
    private static CombatSubject Attacker(MightAndMagic7Session live, ICombatRule rule)
    {
        CombatState fight = FightOf(live, rule);
        return fight.Combatants.First(combatant => !combatant.Subject.IsMember).Subject;
    }

    /// <summary>Casts one spell, as the panel's own control does, and lets the session apply it.</summary>
    private static void Cast(IGameSession session, RecordingUiService ui, ulong step, string spell, string target)
    {
        session.Update(ProductTestContext.Update(
            step,
            1,
            ProductTestContext.Payload(
                string.Create(CultureInfo.InvariantCulture, $$"""{"action":"party.cast","member":0,"spell":"{{spell}}","target":"{{target}}"}"""))));
    }

    /// <summary>Moves the clock by a whole number of game hours, in one admitted update.</summary>
    /// <remarks>
    /// The session advances game time from the admitted steps it measured at this game's own rate, so an hour
    /// of game time is an hour of it however many steps carry it. Advancing in one update is what keeps a case
    /// from depending on how many updates a stretch of time was cut into.
    /// </remarks>
    private static void Advance(IGameSession session, int hours)
    {
        ulong step = 100 + (ulong)hours;
        session.Update(ProductTestContext.Update(step, (uint)(hours * 3600 / GameSecondsPerRealSecond * 60)));
    }

    /// <summary>The identity the party's own projection publishes for one member.</summary>
    private static string Target(PartyMember member) => CombatantId.Of(member.Id).ToString();

    /// <summary>The magic block the panel is showing.</summary>
    private static ProjectedNode Magic(RecordingUiService ui) => ProjectedNode.Of(ui.Latest().Value).Field("magic");

    /// <summary>The position of a spell in the first member's own spellbook, as the panel lists it.</summary>
    private static int AimIndex(RecordingUiService ui, string spell)
    {
        ProjectedNode spells = Magic(ui).Field("members").Item(0).Field("spells");
        for (int index = 0; index < spells.Length(); index++)
        {
            if (spells.Item(index).Field("spell").AsString() == spell) return index;
        }

        throw new InvalidOperationException($"Aelina does not know spell {spell}.");
    }

    /// <summary>One running effect the panel published under an identity.</summary>
    private static ProjectedNode Running(ProjectedNode magic, string effect)
    {
        ProjectedNode running = magic.Field("running");
        for (int index = 0; index < running.Length(); index++)
        {
            if (running.Item(index).Field("effect").AsString() == effect) return running.Item(index);
        }

        throw new InvalidOperationException($"No effect '{effect}' is running; the panel published {running.Length()} of them.");
    }

    /// <summary>What one reading a cast left behind says, empty when the cast published none of it.</summary>
    private static string Fact(ProjectedNode magic, string name)
    {
        ProjectedNode facts = magic.Field("facts");
        for (int index = 0; index < facts.Length(); index++)
        {
            if (facts.Item(index).Field("name").AsString() == name) return facts.Item(index).Field("value").AsString();
        }

        return string.Empty;
    }

    /// <summary>Asserts one reading a cast left behind, and says what was published when it is not there.</summary>
    private static void AssertFact(ProjectedNode magic, string name, string value) =>
        Assert.Equal(value, Fact(magic, name));

    /// <summary>How much real time one game second passes in, which is this game's own rate.</summary>
    private const int GameSecondsPerRealSecond = 30;

    private static readonly CastIntentNames CastControls = new(
        ProductIdentity.CastAction,
        ProductIdentity.QuickSpellAction,
        ProductIdentity.UiActionContract);

    /// <summary>Every roll comes in at its highest, which beats every resistance threshold there is.</summary>
    private sealed class AlwaysHigh : IAttackRolls
    {
        /// <inheritdoc />
        public int Roll(string purpose, int minimum, int maximum)
        {
            _ = purpose;
            _ = minimum;
            return maximum;
        }
    }

    /// <summary>A world of two places, one creature in the first, and a party that knows what a case casts.</summary>
    private static (string Path, string Text)[] Content() =>
    [
        ProductTestContext.Bundle("partyrpg-default", "world"),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/pack.json",
            """
            {
              "schemaVersion": 1,
              "packId": "world",
              "kind": "definitions",
              "provenance": { "description": "authored for a test" },
              "documents": [
                { "path": "places.json", "documentId": "places", "definitionKind": "place" },
                { "path": "links.json", "documentId": "links", "definitionKind": "travel-link" },
                { "path": "start.json", "documentId": "start", "definitionKind": "scenario-start" },
                { "path": "party.json", "documentId": "party", "definitionKind": "scenario-party" },
                { "path": "spells.json", "documentId": "spells", "definitionKind": "spell" },
                { "path": "skills.json", "documentId": "skills", "definitionKind": "skill" },
                { "path": "monsters.json", "documentId": "monsters", "definitionKind": "monster" }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/places.json",
            """
            {
              "documentId": "places",
              "definitionKind": "place",
              "entries": [
                { "id": "1", "kind": "region", "name": "The Guild of Fire", "respawnDays": 1,
                  "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
                  "placements": [ { "id": "beast", "kind": "monster", "monster": "7", "name": "A beast", "x": 5000, "y": 0, "z": 0 } ] },
                { "id": "2", "kind": "interior", "name": "Cave", "respawnDays": 1,
                  "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ] }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/links.json",
            """
            {
              "documentId": "links",
              "definitionKind": "travel-link",
              "entries": [ { "id": "cave", "fromPlace": "1", "toPlace": "2", "entryPoint": "Party Start" } ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/start.json",
            """
            { "documentId": "start", "definitionKind": "scenario-start", "entries": [ { "id": "start", "place": "1", "entryPoint": "Party Start" } ] }
            """),
        Spells(),
        Skills(),
        Monster(),
        Party(),
    ];

    /// <summary>The shipped spell rows these cases cast, by their own global ids.</summary>
    private static (string Path, string Text) Spells() =>
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/spells.json",
            """
            {
              "documentId": "spells",
              "definitionKind": "spell",
              "entries": [
                { "id": "1", "school": "Fire", "level": 1, "name": "Torch Light", "resist": "0" },
                { "id": "3", "school": "Fire", "level": 3, "name": "Fire Resistance", "resist": "Fire" },
                { "id": "5", "school": "Fire", "level": 5, "name": "Haste", "resist": "0" },
                { "id": "12", "school": "Air", "level": 1, "name": "Wizard Eye", "resist": "0" },
                { "id": "31", "school": "Water", "level": 9, "name": "Town Portal", "resist": "0" },
                { "id": "42", "school": "Earth", "level": 9, "name": "Telekinesis", "resist": "0" },
                { "id": "53", "school": "Spirit", "level": 9, "name": "Raise Dead", "resist": "0" },
                { "id": "61", "school": "Mind", "level": 6, "name": "Cure Paralysis", "resist": "0" },
                { "id": "68", "school": "Body", "level": 2, "name": "Heal", "resist": "0" },
                { "id": "80", "school": "Light", "level": 4, "name": "Dispel Magic", "resist": "0" }
              ]
            }
            """);

    /// <summary>The nine schools, so every spell a case casts has the skill its mastery is read from.</summary>
    private static (string Path, string Text) Skills() =>
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/skills.json",
            """
            {
              "documentId": "skills",
              "definitionKind": "skill",
              "entries": [ { "id": "Fire" }, { "id": "Air" }, { "id": "Water" }, { "id": "Earth" },
                            { "id": "Mind" }, { "id": "Spirit" }, { "id": "Body" }, { "id": "Light" }, { "id": "Dark" } ]
            }
            """);

    /// <summary>
    /// A creature that harms with fire and does not notice the party from across the place.
    /// </summary>
    /// <remarks>
    /// Its attack kind is the table's own fire column, which is what lets a case read the party's fire
    /// resistance out of a blow rather than out of a number the effect path kept: the monster table's damage
    /// type is what the fight prices a target's resistance against
    /// (OpenEnroth <c>src/Engine/Objects/Monsters.cpp:545-554</c>).
    /// </remarks>
    private static (string Path, string Text) Monster()
    {
        string[] cells = new string[39];
        for (int index = 0; index < cells.Length; index++) cells[index] = "0";
        cells[0] = "7";
        cells[1] = "A beast";
        cells[3] = "1";
        cells[5] = "200";
        cells[17] = "Fire";
        cells[18] = "1d1+0";
        return ($"{ProductTestContext.ContentDirectory}/content-packs/world/monsters.json",
            $$"""
            {
              "documentId": "monsters",
              "definitionKind": "monster",
              "entries": [
                { "id": "7", "name": "A beast", "hostility": 1, "recovery": 100, "level": 4,
                  "hitPoints": 200, "armorClass": 0, "columns": [ {{string.Join(", ", cells.Select(cell => $"\"{cell}\""))}} ] }
              ]
            }
            """);
    }

    /// <summary>
    /// The scenario's party: a sorcerer who holds every school her spells need, and a companion to heal.
    /// </summary>
    /// <remarks>
    /// The skills are stated at the rungs the spells' own rows require — mastery gates a casting before
    /// anything is paid — and the level gives the caster a pool deep enough for the whole set of cases, since
    /// the ruleset states a member's capacity from the class, the level, and the scores.
    /// </remarks>
    private static (string Path, string Text) Party() =>
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/party.json",
            """
            {
              "documentId": "party",
              "definitionKind": "scenario-party",
              "entries": [
                {
                  "id": "party",
                  "coins": 5000,
                  "food": 30,
                  "reputation": 0,
                  "fame": 0,
                  "members": [
                    { "name": "Aelina", "race": "Elf", "class": "Sorcerer", "level": 20, "hitPoints": 40,
                      "spellPoints": 0,
                      "attributes": [ { "id": "Might", "value": 9 }, { "id": "Intellect", "value": 50 },
                                      { "id": "Personality", "value": 15 }, { "id": "Endurance", "value": 30 },
                                      { "id": "Accuracy", "value": 30 }, { "id": "Speed", "value": 25 },
                                      { "id": "Luck", "value": 13 } ],
                      "skills": [ { "id": "Fire", "level": 6, "tier": 2, "pointsSpent": 1 },
                                  { "id": "Air", "level": 3, "tier": 1, "pointsSpent": 1 },
                                  { "id": "Water", "level": 2, "tier": 3, "pointsSpent": 1 },
                                  { "id": "Earth", "level": 3, "tier": 3, "pointsSpent": 1 },
                                  { "id": "Mind", "level": 4, "tier": 2, "pointsSpent": 1 },
                                  { "id": "Spirit", "level": 4, "tier": 3, "pointsSpent": 1 },
                                  { "id": "Body", "level": 2, "tier": 1, "pointsSpent": 1 },
                                  { "id": "Light", "level": 2, "tier": 1, "pointsSpent": 1 } ],
                      "spells": [ "1", "3", "5", "12", "31", "42", "53", "61", "68", "80" ], "conditions": [] },
                    { "name": "Borin", "race": "Human", "class": "Knight", "level": 1, "hitPoints": 40,
                      "spellPoints": 0,
                      "attributes": [ { "id": "Might", "value": 13 }, { "id": "Intellect", "value": 9 },
                                      { "id": "Personality", "value": 9 }, { "id": "Endurance", "value": 13 },
                                      { "id": "Accuracy", "value": 13 }, { "id": "Speed", "value": 9 },
                                      { "id": "Luck", "value": 9 } ],
                      "skills": [], "spells": [], "conditions": [] }
                  ]
                }
              ]
            }
            """);
}
