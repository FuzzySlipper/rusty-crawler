using System.Globalization;
using System.Text.Json;
using PartyRpg.Kit;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// This game's creatures a spell creates: an elemental called up to stand with the party, and a body stood back up
/// to fight for it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The world's population creates them, and nothing else does.</b> What is created is stated as a placement of
/// the kind the fight recognises, naming its monster row as a placement content states one does, and handed to the
/// population's own <see cref="PlacePopulation.Summon"/>: the same store, the same composition of health and
/// effects, the same pose, the same reading by the fight, the creatures' decisions, the reticle and the panel.
/// There is no second list of creatures and no actor graph beside the population's.
/// </para>
/// <para>
/// <b>It stands with the party because of what made it.</b> The placement carries who called it up or stood it
/// back up, and the fight's own reading of a creature puts such a placement on the ally side (the donor makes both
/// friendly and of the party's own kind: <c>OpenEnroth/src/Engine/Objects/Actor.cpp:4184-4186</c> for the
/// elemental, <c>:1740-1748</c> for a raised body). It fights what fights the party through the same decisions a
/// charmed creature takes, is worth no experience and drops nothing when it falls, and a fight never counts it as
/// something the party must clear.
/// </para>
/// <para>
/// <b>How long it stays is the spell's.</b> An elemental is given the spell's own length and the population ends it
/// when the session's one clock has run that long; a raised body has no length in the donor and stands until the
/// visit ends, when the place's population is rebuilt from content. Neither is carried by a save — the schema
/// carries no population — so a save taken while one stands is refused by name (#8658 holds carrying a fight and
/// what stands in it).
/// </para>
/// </remarks>
internal sealed class MightAndMagic7Summons
{
    /// <summary>The placement field naming the character who called a creature up.</summary>
    internal const string SummonerField = "summoner";

    /// <summary>The placement field naming the character who stood a body back up.</summary>
    internal const string RaisedByField = "raisedBy";

    /// <summary>The placement field naming the body a raised creature was.</summary>
    internal const string RaisedFromField = "raisedFrom";

    /// <summary>
    /// The donor's summoned light elemental: the grade the caster's mastery calls, how many may stand at once, and
    /// how long each stays.
    /// </summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/Spells/CastSpellInfo.cpp:2410-2445</c>: one at expert for five minutes a level, three
    /// at master and five at grand master for fifteen minutes a level; <c>src/Engine/Objects/Actor.cpp:4150-4156</c>
    /// names the row by the table's internal name, grade A at expert, B at master, and C at grand master. The spell
    /// cannot be cast at novice (the donor asserts it never is), so a novice reads as expert. Faithful.
    /// </remarks>
    internal static readonly SummonReading LightElemental = new(
        ["Elemental Light A", "Elemental Light A", "Elemental Light B", "Elemental Light C"],
        [1, 1, 3, 5],
        (level, mastery) => mastery >= 3 ? GameDuration.FromMinutes(15 * level) : GameDuration.FromMinutes(5 * level));

    /// <summary>
    /// The donor's reanimation: a body whose row's level is no higher than two, three, four, or five times the
    /// caster's level by mastery rises, with at most ten hit points for every one of those levels
    /// (OpenEnroth <c>src/Engine/Spells/CastSpellInfo.cpp:2613-2631</c>, <c>:2659-2661</c>). Faithful.
    /// </summary>
    internal static readonly ReanimateReading Reanimation = new((level, mastery) => (Math.Clamp(mastery, 1, 4) + 1) * level, HitPointsPerLevel: 10);

    /// <summary>
    /// How far from the party an elemental stands out of doors and indoors: the donor's own radii
    /// (<c>src/Engine/Objects/Actor.cpp:4165</c>).
    /// </summary>
    private const double OutdoorReach = 128;

    /// <summary>How far from the party an elemental stands indoors.</summary>
    private const double IndoorReach = 64;

    private readonly Func<SessionWorld?> _world;
    private readonly Func<MightAndMagic7Combat?> _combat;
    private readonly CorpseGround? _corpses;
    private long _serial;

    /// <summary>Creates this game's summoning over the owners it creates into.</summary>
    /// <param name="world">The world the party stands in, whose population creates.</param>
    /// <param name="combat">This game's fight policy, which finds a row by its internal name and states a creature's level.</param>
    /// <param name="corpses">The ground the fight lays bodies on, which a raised body leaves.</param>
    internal MightAndMagic7Summons(Func<SessionWorld?> world, Func<MightAndMagic7Combat?> combat, CorpseGround? corpses)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _combat = combat ?? throw new ArgumentNullException(nameof(combat));
        _corpses = corpses;
    }

    /// <summary>Whether a placement is a creature a spell created rather than one content placed.</summary>
    /// <param name="placement">The placement, or null.</param>
    internal static bool IsSummoned(PlacementDefinition? placement) =>
        placement is not null &&
        (placement.Source.GetString(SummonerField).Length > 0 || placement.Source.GetString(RaisedByField).Length > 0);

    /// <summary>Whether a casting can call a creature up, judged before anything is spent.</summary>
    /// <remarks>
    /// The donor refuses a caster who already has as many elementals standing as their mastery allows, and spends
    /// nothing (<c>CastSpellInfo.cpp:2431-2443</c>). A product with no place to stand one in, or content that
    /// carries no row for the grade called, is refused the same way rather than paid for.
    /// </remarks>
    /// <param name="application">The casting.</param>
    /// <param name="summon">What it calls up.</param>
    /// <param name="mastery">The caster's rung of mastery in the spell's school.</param>
    /// <returns>The refusal, or null when it can be cast.</returns>
    internal Refusal? Judge(SpellApplication application, SummonReading summon, int mastery)
    {
        string name = application.Spell.Name;
        if (_world() is not { } world || world.Population.Place is null)
        {
            return new Refusal(MightAndMagic7Codes.SpellSummonNowhere, $"{name} has nowhere to call anything into: the party stands in no populated place.");
        }

        string row = RowOf(summon, mastery);
        if (_combat()?.RowNamed(row) is null)
        {
            return new Refusal(MightAndMagic7Codes.SpellSummonNowhere, $"{name} calls up '{row}', and the content carries no monster row by that name.");
        }

        int most = summon.Most[Math.Clamp(mastery, 1, summon.Most.Length) - 1];
        int standing = Standing(world, application.Caster.Id.ToString());
        if (standing >= most)
        {
            return new Refusal(
                MightAndMagic7Codes.SpellSummonLimit,
                string.Create(CultureInfo.InvariantCulture, $"{application.Caster.Profile.Name} cannot call up any more: {standing} already stand(s) with the party, the most their mastery holds."));
        }

        return null;
    }

    /// <summary>Calls a creature up beside the party, for the spell's own length.</summary>
    /// <param name="application">The casting, already judged.</param>
    /// <param name="summon">What it calls up.</param>
    /// <param name="level">The caster's level in the spell's school.</param>
    /// <param name="mastery">The caster's rung of mastery in it.</param>
    /// <returns>What the casting did.</returns>
    internal SpellApplicationOutcome Summon(SpellApplication application, SummonReading summon, int level, int mastery)
    {
        SessionWorld world = _world() ?? throw new InvalidOperationException("A summoning was applied with no world, which its judgement refuses.");
        MightAndMagic7Combat combat = _combat() ?? throw new InvalidOperationException("A summoning was applied with no fight policy, which its judgement refuses.");
        string internalName = RowOf(summon, mastery);
        int row = combat.RowNamed(internalName) ?? throw new InvalidOperationException($"A summoning of '{internalName}' was applied with no such row, which its judgement refuses.");
        string monsterName = combat.NameOfRow(row) ?? internalName;

        // The donor stands the elemental on a circle of its radius around the party at a drawn bearing
        // (Actor.cpp:4165-4166, 4177-4179); this build stands it at a fixed bearing on that circle (ours).
        PlacePose party = world.Party.PlacePose;
        double reach = world.Graph.Require(world.Place).Kind == PlaceKind.Interior ? IndoorReach : OutdoorReach;
        PlacePose pose = party with { X = party.X + reach };
        GameDuration lasts = summon.Lasts(level, mastery);
        string id = string.Create(CultureInfo.InvariantCulture, $"summoned-{++_serial}");
        PlacementDefinition placement = Placement(id, row.ToString(CultureInfo.InvariantCulture), monsterName, pose, writer =>
        {
            writer.WriteString(SummonerField, application.Caster.Id.ToString());
            writer.WriteString("spell", application.Spell.Id.ToString());
            writer.WriteString("positionSource", "summoned");
        });

        PlacePopulationEntity entity = world.Population.Summon(placement, lasts);

        // The fight reads the place again now, so the creature stands on the party's side from the casting that
        // made it rather than from the next update.
        application.Fight?.Step();
        return SpellApplicationOutcome.Expressed(
            application.Spell.Effect,
            string.Create(CultureInfo.InvariantCulture, $"{application.Spell.Name}: {monsterName} stands with the party for {Describe(lasts)}."),
            [new SpellEffectFact("summoned", monsterName), new SpellEffectFact("lasts", Describe(lasts)), new SpellEffectFact("creature", entity.Content.Id)]);
    }

    /// <summary>Whether a casting names a body that can be stood back up, judged before anything is spent.</summary>
    /// <remarks>
    /// The donor spends the points on a casting aimed at nothing, or at a creature that is not dead
    /// (<c>CastSpellInfo.cpp:2641-2652</c>); this build refuses it before anything is spent, because a spell paid for
    /// that could not have done anything tells the player less than a refusal does (ours).
    /// </remarks>
    /// <param name="application">The casting.</param>
    /// <returns>The refusal, or null when it names a body.</returns>
    internal Refusal? JudgeReanimation(SpellApplication application) =>
        BodyOf(application) is null
            ? new Refusal(
                MightAndMagic7Codes.SpellNotABody,
                $"{application.Spell.Name} stands up the dead, and what it was aimed at is not a creature lying dead in this place.")
            : null;

    /// <summary>Stands the body a casting named back up to fight for the party, when it is not too strong to raise.</summary>
    /// <param name="application">The casting, already judged.</param>
    /// <param name="reanimate">How strong a body it raises, and what it leaves it with.</param>
    /// <param name="level">The caster's level in the spell's school.</param>
    /// <param name="mastery">The caster's rung of mastery in it.</param>
    /// <returns>What the casting did.</returns>
    internal SpellApplicationOutcome Reanimate(SpellApplication application, ReanimateReading reanimate, int level, int mastery)
    {
        if (BodyOf(application) is not { } found)
        {
            throw new InvalidOperationException("A reanimation was applied to something that is not a body, which its judgement refuses.");
        }

        (Combatant fallen, PlacePopulationEntity body, Corpse corpse) = found;
        SessionWorld world = _world()!;
        MightAndMagic7Combat? combat = _combat();
        int most = reanimate.MostLevel(level, mastery);
        int strength = combat?.LevelOf(fallen.Subject) ?? 0;

        // A body too strong for the caster takes the casting and stays down: the donor counts the spell cast
        // (CastSpellInfo.cpp:2657-2658).
        if (strength > most)
        {
            return SpellApplicationOutcome.Expressed(
                application.Spell.Effect,
                string.Create(CultureInfo.InvariantCulture, $"{application.Spell.Name}: {fallen.Name} is too strong to raise (level {strength}, and this casting raises up to {most}), and stays down."),
                [new SpellEffectFact("tooStrong", fallen.Name)]);
        }

        string row = body.Placement.Source.GetId(MightAndMagic7Combat.MonsterField);
        string id = string.Create(CultureInfo.InvariantCulture, $"raised-{++_serial}");
        PlacementDefinition placement = Placement(id, row, fallen.Name, body.Pose, writer =>
        {
            writer.WriteString(RaisedByField, application.Caster.Id.ToString());
            writer.WriteString(RaisedFromField, body.Content.Id);
            writer.WriteString("positionSource", "raised");
        });

        // The body leaves the ground, with what it held, and the creature it was leaves the place: what stands
        // there now is the creature it became (Actor.cpp:1740-1748 drops what it carried).
        _corpses?.Remove(corpse);
        world.Population.Dismiss(body);
        PlacePopulationEntity raised = world.Population.Summon(placement);

        // It rises with at most ten hit points a level the casting could raise (CastSpellInfo.cpp:2659-2661).
        int cap = reanimate.HitPointsPerLevel * most;
        if (CreatureHealth.Find(raised.Actor) is { IsMortal: true } health && health.Current > cap) health.Wound(health.Current - cap);
        application.Fight?.Step();
        string left = CreatureHealth.Find(raised.Actor)?.ToString() ?? string.Empty;
        return SpellApplicationOutcome.Expressed(
            application.Spell.Effect,
            string.Create(CultureInfo.InvariantCulture, $"{application.Spell.Name}: {fallen.Name} rises to fight for the party ({left})."),
            [new SpellEffectFact("raised", fallen.Name), new SpellEffectFact("creature", raised.Content.Id)]);
    }

    /// <summary>How many creatures one character has called up that still stand in the party's place.</summary>
    private static int Standing(SessionWorld world, string summoner) =>
        world.Population.Entities.Count(entity =>
            entity.IsAlive &&
            entity.IsSummoned &&
            string.Equals(entity.Placement.Source.GetString(SummonerField), summoner, StringComparison.Ordinal) &&
            CreatureHealth.Find(entity.Actor) is not { IsDown: true });

    /// <summary>The internal name of the row a rung of mastery calls up.</summary>
    private static string RowOf(SummonReading summon, int mastery) => summon.Rows[Math.Clamp(mastery, 1, summon.Rows.Length) - 1];

    /// <summary>The body a casting named: the fight's fallen creature, its entity, and the body the ground holds for it.</summary>
    private (Combatant Fallen, PlacePopulationEntity Body, Corpse Corpse)? BodyOf(SpellApplication application)
    {
        if (application.Fight is not { } fight || application.Target is not { } target || _world() is not { } world) return null;
        if (fight.Find(target) is not { Subject.Entity: { } entity } fallen || !fight.IsDown(fallen)) return null;
        if (!string.Equals(entity.Content.Kind, MightAndMagic7Combat.CreaturePlacementKind, StringComparison.Ordinal)) return null;
        if (entity.Placement.Source.GetId(MightAndMagic7Combat.MonsterField).Length == 0) return null;
        if (_corpses?.At(world.Place, entity.Content) is not { } corpse) return null;
        return (fallen, entity, corpse);
    }

    /// <summary>Writes a created creature as the placement every other reader of a creature reads.</summary>
    private static PlacementDefinition Placement(string id, string row, string name, PlacePose pose, Action<Utf8JsonWriter> provenance)
    {
        using MemoryStream buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString(PlacePopulationContent.IdField, id);
            writer.WriteString(PlacePopulationContent.KindField, MightAndMagic7Combat.CreaturePlacementKind);
            writer.WriteString(MightAndMagic7Combat.MonsterField, row);
            writer.WriteString("monsterName", name);
            writer.WriteNumber("x", pose.X);
            writer.WriteNumber("y", pose.Y);
            writer.WriteNumber("z", pose.Z);
            writer.WriteNumber("yaw", pose.Yaw);
            provenance(writer);
            writer.WriteEndObject();
        }

        using JsonDocument document = JsonDocument.Parse(buffer.ToArray());
        return PlacePopulationContent.Definition(
            new PlacementContentId(MightAndMagic7Combat.CreaturePlacementKind, id),
            document.RootElement.Clone());
    }

    /// <summary>A length of game time in the words a person reads.</summary>
    private static string Describe(GameDuration lasts)
    {
        long minutes = lasts.Milliseconds / GameDuration.MillisecondsPerSecond / GameDuration.SecondsPerMinute;
        return minutes >= GameDuration.MinutesPerHour
            ? string.Create(CultureInfo.InvariantCulture, $"{minutes / GameDuration.MinutesPerHour}h {minutes % GameDuration.MinutesPerHour}m")
            : string.Create(CultureInfo.InvariantCulture, $"{minutes}m");
    }
}
