using System.Text.Json;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Loot;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Persistence;

/// <summary>A durable reference to one combatant, never an Engine entity or runtime combatant id.</summary>
/// <param name="Member">The party member, when this is a member.</param>
/// <param name="Placement">The resident placement, when this is a creature.</param>
public readonly record struct CombatActorSave(PartyMemberId? Member = null, PlacementContentId? Placement = null);

/// <summary>One member's remaining debt to the fight's single recovery quantity.</summary>
/// <param name="Member">The durable member identity.</param>
/// <param name="RecoveryMilliseconds">Remaining recovery, in game milliseconds.</param>
public sealed record MemberCombatSave(PartyMemberId Member, long RecoveryMilliseconds);

/// <summary>What an effect still running on a creature is worth and how long it has left.</summary>
/// <param name="Effect">The effect's ruleset identity.</param>
/// <param name="Magnitude">Its magnitude.</param>
/// <param name="RemainingMilliseconds">Remaining game time.</param>
public sealed record CreatureEffectSave(EffectId Effect, int Magnitude, long RemainingMilliseconds);

/// <summary>A resident creature's changed state over its durable placement and ruleset kind.</summary>
/// <param name="Placement">The placement identity, not the current runtime entity.</param>
/// <param name="Kind">The creature kind as the ruleset names it.</param>
/// <param name="Pose">Its actual feet pose.</param>
/// <param name="Health">Its remaining health.</param>
/// <param name="RecoveryMilliseconds">Its remaining recovery debt.</param>
/// <param name="Provoked">Whether what the party did makes it hostile.</param>
/// <param name="Summoned">Whether it was created during this visit.</param>
/// <param name="RemainingMilliseconds">Remaining lifetime, or null for the visit.</param>
/// <param name="Origin">The created placement's content entry; absent for resident content.</param>
/// <param name="Effects">Effects on its actual entity.</param>
public sealed record CreatureCombatSave(
    PlacementContentId Placement, string Kind, PlacePose Pose, int Health, long RecoveryMilliseconds,
    bool Provoked, bool Summoned, long? RemainingMilliseconds, JsonElement? Origin,
    IReadOnlyList<CreatureEffectSave> Effects);

/// <summary>A body's identity and held yield, preserving its search incarnation without rolling again.</summary>
/// <param name="Placement">The creature's placement.</param>
/// <param name="Pose">Where it fell.</param>
/// <param name="Name">Its displayed name.</param>
/// <param name="Serial">The death incarnation used by ordinary container interaction.</param>
/// <param name="Held">The yield already drawn, or null when none was held.</param>
public sealed record CorpseSave(PlacementContentId Placement, PlacePose Pose, string Name, long Serial, LootYield? Held);

/// <summary>The bookkeeping of the one turn-based pacing, using durable actor references.</summary>
/// <param name="Round">The current round.</param>
/// <param name="Phase">The current phase.</param>
/// <param name="Current">The actor whose turn was handed out.</param>
/// <param name="DueMilliseconds">Time until that turn.</param>
/// <param name="ElapsedMilliseconds">Time spent in the action phase.</param>
/// <param name="LengthMilliseconds">The round's action length.</param>
/// <param name="MovementMilliseconds">Remaining movement allowance.</param>
/// <param name="Acted">Actors that acted.</param>
/// <param name="Skipped">Actors that skipped.</param>
/// <param name="Waiting">Actors waiting once behind the order.</param>
/// <param name="Waited">Actors that already used their wait.</param>
public sealed record TurnSave(
    int Round, TurnPhase Phase, CombatActorSave? Current, long DueMilliseconds, long ElapsedMilliseconds,
    long LengthMilliseconds, long MovementMilliseconds, IReadOnlyList<CombatActorSave> Acted,
    IReadOnlyList<CombatActorSave> Skipped, IReadOnlyList<CombatActorSave> Waiting, IReadOnlyList<CombatActorSave> Waited);

/// <summary>A ruleset's content-only reading of a creature, used before any entity is rebuilt.</summary>
/// <param name="Kind">The recognized kind.</param>
/// <param name="Health">The canonical maximum health.</param>
/// <param name="ActionRecoveryLimitMilliseconds">The largest ordinary action length for a round.</param>
/// <param name="RecoveryLimitMilliseconds">The largest recovery debt the game can have left by this save.</param>
public sealed record SavedCreatureDefinition(string Kind, int Health, long RecoveryLimitMilliseconds, long ActionRecoveryLimitMilliseconds = 0);

/// <summary>The game's content meaning for a saved fight, without constructing a second actor graph.</summary>
public interface ICombatSaveRule
{
    /// <summary>Reads a placement's creature kind and limits, or null when it is not a recognized creature.</summary>
    SavedCreatureDefinition? Creature(PlacementDefinition placement, PartySave party, long elapsedMilliseconds);
    /// <summary>The largest recovery this game's character actions can have charged.</summary>
    long MemberRecoveryLimit(PartyMemberSave member);
    /// <summary>Every contradiction in a created placement's provenance.</summary>
    IEnumerable<string> CreatedProblems(CreatureCombatSave creature, PartySave party);
    /// <summary>Whether the game can have left this effect and magnitude on a creature.</summary>
    bool KnowsEffect(EffectId effect, int magnitude);
    /// <summary>Whether a held body's item still names an item this game can create.</summary>
    bool KnowsLoot(ItemDefinitionId item);
}

/// <summary>The fight as the current save carries it: one resident visit, both pacings and canonical owners.</summary>
/// <param name="Place">The resident place.</param>
/// <param name="Members">Member recovery.</param>
/// <param name="Creatures">Resident creatures, including wounded, spelled and fallen ones.</param>
/// <param name="Corpses">Bodies and their already-held yield.</param>
/// <param name="CorpseSerial">The body's identity cursor.</param>
/// <param name="AttacksResolved">The keyed attack sequence cursor.</param>
/// <param name="Pacing">Which pacing was selected.</param>
/// <param name="Turns">The bookkeeping of that pacing.</param>
public sealed record CombatSave(
    PlaceId Place, IReadOnlyList<MemberCombatSave> Members, IReadOnlyList<CreatureCombatSave> Creatures,
    IReadOnlyList<CorpseSave> Corpses, long CorpseSerial, long AttacksResolved, CombatPacing Pacing, TurnSave? Turns)
{
    /// <summary>
    /// Resident creature placements explicitly absent from this visit (for example, hidden or previously defeated).
    /// Together with Creatures this states the whole resident creature set; omission never authorizes removal.
    /// </summary>
    public IReadOnlyList<PlacementContentId> AbsentResidents { get; init; } = [];

    /// <summary>No composed combat owner and no fight state.</summary>
    public static CombatSave None { get; } = new(new PlaceId(""), [], [], [], 0, 0, CombatPacing.RealTime, null);

    /// <summary>Reads the whole fight once at an explicit save boundary.</summary>
    public static CombatSave Capture(CombatState combat, PlacePopulation population) => combat.Capture(population);

    /// <summary>Names all contradictions before any saved owner is restored.</summary>
    public IReadOnlyList<SaveProblem> Problems(
        WorldSave world, PartySave party, PlacePopulationContent content, ICombatSaveRule? rule, long elapsedMilliseconds)
    {
        List<SaveProblem> problems = [];
        void Bad(string code, string subject, string why) => problems.Add(new SaveProblem(code, subject, why));
        if (Members is null || Creatures is null || Corpses is null || AbsentResidents is null)
        {
            Bad(SaveCodes.SaveCombatInvalid, "combat", "the fight omits a member, creature or body collection");
            return problems;
        }
        bool empty = Members.Count == 0 && Creatures.Count == 0 && Corpses.Count == 0 && AbsentResidents.Count == 0 && CorpseSerial == 0 && AttacksResolved == 0 && Turns is null && Pacing == CombatPacing.RealTime;
        if (string.IsNullOrEmpty(Place.Value))
        {
            if (!empty) Bad(SaveCodes.SaveCombatInvalid, "combat", "the fight has state but names no resident place");
            return problems;
        }
        if (Place != world.Pose.Place) Bad(SaveCodes.SaveCombatInvalid, "combat", "the fight is not in the party's resident place");
        if (!Enum.IsDefined(Pacing) || CorpseSerial < 0 || AttacksResolved < 0)
            Bad(SaveCodes.SaveCombatInvalid, "combat", "the pacing or a combat identity cursor is invalid");
        if (rule is null && !empty)
            Bad(SaveCodes.SaveCombatInvalid, "combat", "this game states no owner for restoring the recorded fight");

        Dictionary<PartyMemberId, PartyMemberSave> members = party.Members.DistinctBy(m => m.Id).ToDictionary(m => m.Id);
        long roundLimit = rule is null ? long.MaxValue : members.Values.Select(rule.MemberRecoveryLimit).DefaultIfEmpty(0).Max();
        HashSet<PartyMemberId> recordedMembers = [];
        HashSet<CombatActorSave> actors = [];
        foreach (MemberCombatSave member in Members)
        {
            if (member is null) { Bad(SaveCodes.SaveCombatInvalid, "members", "a recorded member is null"); continue; }
            if (!members.TryGetValue(member.Member, out PartyMemberSave? stated) || !recordedMembers.Add(member.Member))
                Bad(SaveCodes.SaveCombatInvalid, $"member:{member.Member}", "the recorded combat member is absent or repeated");
            else if (member.RecoveryMilliseconds < 0 || rule is not null && member.RecoveryMilliseconds > rule.MemberRecoveryLimit(stated))
                Bad(SaveCodes.SaveCombatRecoveryInvalid, $"member:{member.Member}", "the member's recovery exceeds what the game's actions can leave");
            actors.Add(new CombatActorSave(Member: member.Member));
        }
        if (!recordedMembers.SetEquals(members.Keys))
            Bad(SaveCodes.SaveCombatInvalid, "members", "the fight does not carry recovery for the whole roster");

        IReadOnlyList<PlacementDefinition> placements;
        try { placements = content.PlacementsOf(Place); }
        catch (Exception error) when (error is ArgumentException or KeyNotFoundException or InvalidOperationException or ContentValidationException)
        { Bad(SaveCodes.SaveCreatureMissing, $"{Place}", "the saved fight's place is absent from the resident world"); return problems; }
        Dictionary<PlacementContentId, PlacementDefinition> known = placements.ToDictionary(p => p.Content);
        HashSet<PlacementContentId> residents = rule is null ? [] : known.Values
            .Where(p => rule.Creature(p, party, elapsedMilliseconds) is not null).Select(p => p.Content).ToHashSet();
        Dictionary<PlacementContentId, CreatureCombatSave> recorded = [];
        foreach (CreatureCombatSave creature in Creatures)
        {
            if (creature is null) { Bad(SaveCodes.SaveCombatInvalid, "creatures", "a recorded creature is null"); continue; }
            string subject = $"{Place}/{creature.Placement}";
            if (!recorded.TryAdd(creature.Placement, creature)) Bad(SaveCodes.SaveCombatInvalid, subject, "the creature is recorded twice");
            actors.Add(new CombatActorSave(Placement: creature.Placement));
            PlacementDefinition? placement = Definition(creature, known);
            if (placement is null)
                Bad(SaveCodes.SaveCreatureMissing, subject, "the saved resident creature has no placement to rebuild; the fight cannot be silently dropped");
            SavedCreatureDefinition? definition = placement is null ? null : rule?.Creature(placement, party, elapsedMilliseconds);
            if (rule is not null && (definition is null || definition.Kind != creature.Kind))
                Bad(SaveCodes.SaveCreatureKindUnknown, subject, "the saved kind is unknown or disagrees with the resident placement's creature kind");
            if (definition is not null) roundLimit = Math.Max(roundLimit, definition.ActionRecoveryLimitMilliseconds);
            if (!Finite(creature.Pose)) Bad(SaveCodes.SaveCombatInvalid, subject, "the creature's saved pose is not made of finite numbers");
            if (creature.Health < 0 || definition is not null && creature.Health > definition.Health)
                Bad(SaveCodes.SaveCombatInvalid, subject, "the creature's health is outside its canonical pool");
            if (creature.RecoveryMilliseconds < 0 || definition is not null && creature.RecoveryMilliseconds > definition.RecoveryLimitMilliseconds)
                Bad(SaveCodes.SaveCombatRecoveryInvalid, subject, "the creature's recovery exceeds what the rule and elapsed game time can leave");
            if (creature.Summoned)
            {
                if (known.ContainsKey(creature.Placement)) Bad(SaveCodes.SaveCombatInvalid, subject, "a created creature shadows a resident placement");
                if (creature.RemainingMilliseconds is <= 0) Bad(SaveCodes.SaveCombatInvalid, subject, "the created creature's lifetime has already ended");
                if (creature.Origin is not { ValueKind: JsonValueKind.Object }) Bad(SaveCodes.SaveCombatInvalid, subject, "the created creature has no placement origin");
                if (rule is not null) foreach (string why in rule.CreatedProblems(creature, party)) Bad(SaveCodes.SaveCombatInvalid, subject, why);
            }
            else
            {
                if (creature.Origin is not null || creature.RemainingMilliseconds is not null)
                    Bad(SaveCodes.SaveCombatInvalid, subject, "a resident creature carries created-creature origin or lifetime");
                bool defeated = world.Interaction.Places.Any(p => p.Place == Place && p.Deaths.Contains(creature.Placement));
                if (definition is { Health: > 0 } && defeated != (creature.Health == 0))
                    Bad(SaveCodes.SaveCombatInvalid, subject, "the creature's health contradicts the world's defeated-placement record");
            }
            HashSet<EffectId> effects = [];
            if (creature.Effects is null) { Bad(SaveCodes.SaveCombatInvalid, subject, "the creature omits its effect collection"); continue; }
            foreach (CreatureEffectSave effect in creature.Effects)
                if (effect is null || !effects.Add(effect.Effect) || effect.RemainingMilliseconds <= 0 || rule is not null && !rule.KnowsEffect(effect.Effect, effect.Magnitude))
                    Bad(SaveCodes.SaveCombatInvalid, subject, "an effect is repeated, ended, or not one this game can leave on a creature");
        }
        HashSet<PlacementContentId> bodies = [];
        HashSet<PlacementContentId> absent = [];
        foreach (PlacementContentId missing in AbsentResidents)
        {
            if (!residents.Contains(missing) || !absent.Add(missing) || recorded.ContainsKey(missing))
                Bad(SaveCodes.SaveCombatInvalid, $"{Place}/{missing}", "the visit's absent resident is unknown, repeated, or also present");
        }
        foreach (PlacementContentId resident in residents)
            if (!recorded.ContainsKey(resident) && !absent.Contains(resident))
                Bad(SaveCodes.SaveCreatureMissing, $"{Place}/{resident}", "the resident creature is neither carried nor explicitly absent from the saved visit");
        HashSet<long> serials = [];
        foreach (CorpseSave body in Corpses)
        {
            if (body is null) { Bad(SaveCodes.SaveCombatInvalid, "bodies", "a recorded body is null"); continue; }
            string subject = $"{Place}/{body.Placement}";
            if (!recorded.TryGetValue(body.Placement, out CreatureCombatSave? creature) || creature.Health != 0 ||
                !bodies.Add(body.Placement) || body.Serial <= 0 || body.Serial > CorpseSerial || !serials.Add(body.Serial) || !Finite(body.Pose) ||
                creature is not null && body.Pose != creature.Pose)
                Bad(SaveCodes.SaveCombatInvalid, subject, "the body, its pose, or its death incarnation contradicts the recorded creature");
            if (body.Held is { } held && (held.Coins < 0 || held.Items is null || held.Items.Any(i => i.Count <= 0 || rule is not null && !rule.KnowsLoot(i.Definition))))
                Bad(SaveCodes.SaveCombatInvalid, subject, "the body's held yield names an invalid item, count or purse");
        }
        if (Turns is { } turns)
        {
            if (turns.Acted is null || turns.Skipped is null || turns.Waiting is null || turns.Waited is null)
            {
                Bad(SaveCodes.SaveCombatInvalid, "round", "the round omits an actor collection");
                return problems;
            }
            if (Pacing != CombatPacing.TurnBased || !Enum.IsDefined(turns.Phase) || turns.Round < 0 ||
                turns.DueMilliseconds < 0 || turns.ElapsedMilliseconds < 0 || turns.LengthMilliseconds < 0 || turns.LengthMilliseconds > roundLimit || turns.MovementMilliseconds < 0 ||
                turns.ElapsedMilliseconds > turns.LengthMilliseconds || turns.DueMilliseconds > turns.LengthMilliseconds || turns.MovementMilliseconds > turns.LengthMilliseconds ||
                (turns.Phase == TurnPhase.None) != (turns.Round == 0))
                Bad(SaveCodes.SaveCombatInvalid, "round", "the turn-based bookkeeping contradicts its pacing or game-time lengths");
            if (turns.Phase == TurnPhase.None && (turns.Current is not null || turns.LengthMilliseconds != 0 || turns.DueMilliseconds != 0 || turns.ElapsedMilliseconds != 0 || turns.MovementMilliseconds != 0 || turns.Acted.Count + turns.Skipped.Count + turns.Waiting.Count + turns.Waited.Count != 0))
                Bad(SaveCodes.SaveCombatInvalid, "round", "an inactive round carries active bookkeeping");
            if (turns.Current is { } current && !actors.Contains(current)) Bad(SaveCodes.SaveCombatInvalid, "round", "the current turn names no combatant");
            foreach (IReadOnlyList<CombatActorSave> set in new[] { turns.Acted, turns.Skipped, turns.Waiting, turns.Waited })
                if (set.Distinct().Count() != set.Count || set.Any(a => !actors.Contains(a))) Bad(SaveCodes.SaveCombatInvalid, "round", "a round's actor set is repeated or names no combatant");
        }
        else if (Pacing == CombatPacing.TurnBased) Bad(SaveCodes.SaveCombatInvalid, "round", "the turn-based pacing has no recorded bookkeeping");
        return problems;
    }

    internal static PlacementDefinition? Definition(CreatureCombatSave creature, IReadOnlyDictionary<PlacementContentId, PlacementDefinition> known) =>
        creature.Summoned ? creature.Origin is { ValueKind: JsonValueKind.Object } origin ? PlacePopulationContent.Definition(creature.Placement, origin) : null
            : known.GetValueOrDefault(creature.Placement);

    private static bool Finite(PlacePose pose) => double.IsFinite(pose.X) && double.IsFinite(pose.Y) && double.IsFinite(pose.Z) && double.IsFinite(pose.Yaw) && double.IsFinite(pose.Pitch);
}
