using PartyRpg.Kit.Party;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Combat;

public sealed partial class CombatState
{
    private readonly ICombatSaveRule? _saving;
    private readonly CorpseGround? _corpses;

    /// <summary>Reads whether the party's acts, rather than the creature's nature, make it hostile.</summary>
    public bool IsProvoked(CombatantId actor) => _provoked.Contains(actor);

    internal CombatSave Capture(PlacePopulation population)
    {
        Step();
        if (_saving is null)
        {
            IReadOnlyList<(string Subject, string Phrase)> unsaved = UnsavedFight();
            if (unsaved.Count > 0 || population.Entities.Any(e => e.IsSummoned))
                throw new SessionSaveException("The game states no owner for carrying this fight.",
                    [new SaveProblem(SaveCodes.SaveFightUnsaved, "combat", "this game's fight has state and no composed save meaning")]);
            return CombatSave.None;
        }
        PlaceId place = _world?.Place ?? default;
        PartySave party = _party.Capture();
        long elapsed = _clock?.Elapsed.Milliseconds ?? 0;
        List<CreatureCombatSave> creatures = [];
        foreach (Combatant actor in _combatants)
        {
            if (actor.Subject.Entity is not { } entity) continue;
            SavedCreatureDefinition definition = _saving.Creature(entity.Placement, party, elapsed)
                ?? throw new SessionSaveException("A live creature has no saved kind.",
                    [new SaveProblem(SaveCodes.SaveCreatureKindUnknown, $"{place}/{entity.Content}", "the game cannot describe this live creature's kind")]);
            int health = Vitals(actor).Current;
            PlacePose pose = health == 0 && _corpses?.At(place, entity.Content) is { } body ? body.Pose : entity.Pose;
            creatures.Add(new CreatureCombatSave(entity.Content, definition.Kind, pose, health,
                actor.Recovery.Milliseconds, _provoked.Contains(actor.Id), entity.IsSummoned,
                population.RemainingOf(entity)?.Milliseconds, entity.IsSummoned ? entity.Placement.Source.Payload.Clone() : null,
                CreatureEffects.Find(entity.Actor)?.Active.Select(e => new CreatureEffectSave(e.Effect, e.Magnitude, e.Remaining.Milliseconds)).ToArray() ?? []));
        }
        // A body's canonical owner may retain it after a created creature's lifetime ended. Carry that
        // body too; it is not a second live creature and its yield must not be rolled again on load.
        HashSet<PlacementContentId> resident = population.PlacementsOf(place).Select(p => p.Content).ToHashSet();
        foreach (Corpse body in _corpses?.In(place) ?? [])
        {
            if (creatures.Any(c => c.Placement == body.Content)) continue;
            SavedCreatureDefinition definition = _saving.Creature(body.Body, party, elapsed)
                ?? throw new SessionSaveException("A body has no saved creature kind.",
                    [new SaveProblem(SaveCodes.SaveCreatureKindUnknown, $"{place}/{body.Content}", "the game cannot describe this body's kind")]);
            bool created = !resident.Contains(body.Content);
            creatures.Add(new CreatureCombatSave(body.Content, definition.Kind, body.Pose, 0, 0, false,
                created, null, created ? body.Body.Source.Payload.Clone() : null, []));
        }
        return new CombatSave(place,
            _combatants.Where(a => a.Subject.Member is not null).Select(a => new MemberCombatSave(a.Subject.Member!.Id, a.Recovery.Milliseconds)).ToArray(),
            creatures,
            _corpses?.In(place).Select(b => new CorpseSave(b.Content, b.Pose, b.Name, b.Serial, _corpses.Held(b))).ToArray() ?? [],
            _corpses?.Serial ?? 0, _attacksResolved, Pacing,
            Pacing == CombatPacing.TurnBased ? Turns.Capture(SavedActor) : null)
        {
            AbsentResidents = population.PlacementsOf(place)
                .Where(p => _saving.Creature(p, party, elapsed) is not null && creatures.All(c => c.Placement != p.Content))
                .Select(p => p.Content).ToArray(),
        };
    }

    /// <summary>Restores a validated fight into the actual resident owners, after the world is composed.</summary>
    public void Restore(CombatSave save, PlacePopulation population)
    {
        if (string.IsNullOrEmpty(save.Place.Value)) return;
        Step();
        HashSet<PlacementContentId> absent = save.AbsentResidents.ToHashSet();
        foreach (PlacePopulationEntity entity in _combatants.Where(a => a.Subject.Entity is not null).Select(a => a.Subject.Entity!).ToArray())
            if (absent.Contains(entity.Content)) population.Dismiss(entity);
        Dictionary<PlacementContentId, PlacementDefinition> content = population.PlacementsOf(save.Place).ToDictionary(p => p.Content);
        foreach (CreatureCombatSave creature in save.Creatures)
        {
            PlacementDefinition placement = CombatSave.Definition(creature, content)!;
            PlacePopulationEntity? entity = population.Entities.FirstOrDefault(e => e.Content == creature.Placement);
            // Remembered defeat makes the initial content population omit a body. Rebuild that same resident
            // through its canonical composer, rather than summoning it or dropping its held loot.
            entity ??= population.RestoreEntity(placement, creature.Summoned,
                creature.RemainingMilliseconds is { } left ? GameDuration.FromMilliseconds(left) : null);
            entity.MoveTo(creature.Pose);
        }
        Step();
        _provoked.Clear();
        foreach (CreatureCombatSave creature in save.Creatures)
        {
            Combatant actor = _combatants.Single(a => a.Subject.Entity?.Content == creature.Placement);
            Vitals(actor); // Attaches health/effects through the same owner as ordinary combat.
            CreatureHealth.Find(actor.Subject.Entity!.Actor)?.Restore(creature.Health);
            CreatureEffects effects = CreatureEffects.Find(actor.Subject.Entity.Actor) ?? CreatureEffects.Attach(actor.Subject.Entity.Actor);
            effects.Restore(creature.Effects);
            actor.Spend(GameDuration.FromMilliseconds(creature.RecoveryMilliseconds));
            if (creature.Provoked) _provoked.Add(actor.Id);
        }
        foreach (MemberCombatSave member in save.Members)
            _combatants.Single(a => a.Subject.Member?.Id == member.Member).Spend(GameDuration.FromMilliseconds(member.RecoveryMilliseconds));
        _corpses?.Restore(save.Place, save.CorpseSerial, save.Corpses,
            save.Creatures.ToDictionary(c => c.Placement, c => CombatSave.Definition(c, content)!));
        _attacksResolved = save.AttacksResolved;
        Step(); // Re-read side from canonical effects and the restored provocation, without advancing time.
        Pacing = save.Pacing;
        if (save.Turns is { } turns) Turns.Restore(turns, FindSavedActor);
    }

    private CombatActorSave? SavedActor(CombatantId id) => Find(id) is { } actor
        ? actor.Subject.Member is { } member ? new CombatActorSave(Member: member.Id)
            : actor.Subject.Entity is { } entity ? new CombatActorSave(Placement: entity.Content) : null
        : null;

    private Combatant? FindSavedActor(CombatActorSave reference) => _combatants.FirstOrDefault(a =>
        reference.Member is { } member ? a.Subject.Member?.Id == member : reference.Placement is { } placement && a.Subject.Entity?.Content == placement);
}
