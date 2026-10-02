using System.Globalization;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.World;

namespace PartyRpg.Rulesets.MightAndMagic7;

internal sealed partial class MightAndMagic7Combat : ICombatSaveRule
{
    private HashSet<ItemDefinitionId> _savedLoot = [];

    public SavedCreatureDefinition? Creature(PlacementDefinition placement, PartySave party, long elapsedMilliseconds)
    {
        MonsterFacts? facts = Creature(placement) ?? PersonFacts(placement);
        if (facts is null && !IsPerson(placement)) return null;
        string kind = facts?.Id.ToString(CultureInfo.InvariantCulture) ?? PersonPlacementKind;
        long ordinary = facts is null ? Ticks(UnarmedBaseTicks).Milliseconds : checked(facts.Recovery.Milliseconds * SlowedRecovery);
        // Stun adds to the single recovery quantity. Comparing a saved debt with just one attack would
        // reject a legitimate staggered creature. Bound possible additions by the roster's casts over
        // elapsed game time and the existing minimum casting recovery; no new timer or runtime cap.
        Int128 casts = ((Int128)Math.Max(0, elapsedMilliseconds) / Ticks(MinimumRangedTicks).Milliseconds + 1) * party.Members.Count;
        Int128 limit = ordinary + casts * Ticks(MightAndMagic7Spells.MaximumDelayTicks).Milliseconds;
        return new SavedCreatureDefinition(kind, facts?.HitPoints ?? 0, limit > long.MaxValue ? long.MaxValue : (long)limit, ordinary);
    }

    public long MemberRecoveryLimit(PartyMemberSave member)
    {
        // Recovery can still be owed after changing equipment, quick spell, attributes or haste. The
        // bound is over this game's action policies, not the character's newly worn loadout.
        int weaponAndArmour = BaseTicks(StaffWord) + BaseTicks(PlateWord) + BaseTicks(ShieldWord);
        int ticks = Math.Max(weaponAndArmour, MightAndMagic7Spells.MaximumRecoveryTicks) - MightAndMagic7AttributeBonus.Of(int.MinValue);
        return Ticks(ticks).Milliseconds;
    }

    public IEnumerable<string> CreatedProblems(CreatureCombatSave creature, PartySave party)
    {
        if (creature.Origin is not { ValueKind: System.Text.Json.JsonValueKind.Object }) yield break;
        PlacementDefinition placement = PartyRpg.Kit.World.PlacePopulationContent.Definition(creature.Placement, creature.Origin.Value);
        if (!MightAndMagic7Summons.IsCreatedIdentity(creature.Placement, party.Records) ||
            placement.Source.GetId("id") != creature.Placement.Id || placement.Source.GetString("kind") != creature.Placement.Kind ||
            creature.Placement.Kind != CreaturePlacementKind)
            yield return "the created creature's identity or placement origin was not issued by this game's summoning owner";
        string summoner = placement.Source.GetString(MightAndMagic7Summons.SummonerField);
        string raised = placement.Source.GetString(MightAndMagic7Summons.RaisedByField);
        bool byEvent = placement.Source.GetString("positionSource") == "summoned-by-event";
        bool byArena = placement.Source.GetString("positionSource") == "summoned-by-arena";
        if (byArena)
        {
            if (!creature.Placement.Id.StartsWith("arena-", StringComparison.Ordinal) || summoner.Length > 0 || raised.Length > 0 ||
                placement.Source.GetString(MightAndMagic7Arena.BoutField).Length == 0 ||
                placement.Source.GetInt32(MightAndMagic7Arena.SlotField) is not >= 0 || creature.RemainingMilliseconds is not null)
                yield return "the arena-created creature names no bout opponent or contradicts its visit lifetime";
        }
        else if (byEvent)
        {
            if (!creature.Placement.Id.StartsWith("event-", StringComparison.Ordinal) || summoner.Length > 0 || raised.Length > 0 ||
                placement.Source.GetString("sourceField") != "events" || placement.Source.GetString("encounterPlacement").Length == 0 ||
                creature.RemainingMilliseconds is not null)
                yield return "the event-created creature's origin or visit lifetime contradicts the spawning owner";
        }
        else if ((summoner.Length > 0) == (raised.Length > 0) || !party.Members.Any(m => m.Id.ToString() == (summoner.Length > 0 ? summoner : raised)))
            yield return "the created creature does not name exactly one caster in the party";
        if (raised.Length > 0 && placement.Source.GetString(MightAndMagic7Summons.RaisedFromField).Length == 0)
            yield return "the raised creature names no body it came from";
        if (placement.Source.GetId(MonsterField) != creature.Kind)
            yield return "the created creature's row disagrees with its saved kind";
    }

    public bool KnowsEffect(EffectId effect, int magnitude) => magnitude > 0 && MightAndMagic7Spells.CreatureEffectIds.Contains(effect);
    public bool KnowsLoot(ItemDefinitionId item) => _savedLoot.Contains(item);
}
