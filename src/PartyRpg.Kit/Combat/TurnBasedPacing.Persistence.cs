using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Time;

namespace PartyRpg.Kit.Combat;

public sealed partial class TurnBasedPacing
{
    internal TurnSave Capture(Func<CombatantId, CombatActorSave?> reference)
    {
        CombatActorSave[] Saved(HashSet<CombatantId> set) => set.Select(reference).Where(r => r.HasValue).Select(r => r!.Value).ToArray();
        return new TurnSave(Round, Phase, _current is null ? null : reference(_current.Id), _due.Milliseconds,
            _elapsed.Milliseconds, _length.Milliseconds, _movement.Milliseconds,
            Saved(_acted), Saved(_skipped), Saved(_waiting), Saved(_waited));
    }

    internal void Restore(TurnSave save, Func<CombatActorSave, Combatant?> find)
    {
        Round = save.Round;
        Phase = save.Phase;
        _current = save.Current is { } current ? find(current) : null;
        _due = GameDuration.FromMilliseconds(save.DueMilliseconds);
        _elapsed = GameDuration.FromMilliseconds(save.ElapsedMilliseconds);
        _length = GameDuration.FromMilliseconds(save.LengthMilliseconds);
        _movement = GameDuration.FromMilliseconds(save.MovementMilliseconds);
        void Held(HashSet<CombatantId> set, IReadOnlyList<CombatActorSave> saved)
        {
            set.Clear();
            foreach (CombatActorSave actor in saved) set.Add(find(actor)!.Id);
        }
        Held(_acted, save.Acted); Held(_skipped, save.Skipped); Held(_waiting, save.Waiting); Held(_waited, save.Waited);
    }
}
