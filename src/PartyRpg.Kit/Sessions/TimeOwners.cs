using PartyRpg.Kit.Time;

namespace PartyRpg.Kit.Sessions;

/// <summary>
/// The owners that keep something against the session's one clock, told about every advance in one place.
/// </summary>
/// <remarks>
/// <para>
/// Two owners of time exist — the admitted update, which advances the clock by the interval it measured, and
/// a journey, which charges the time its transition quoted — and both hand their advance to the same
/// observer. That observer is this: a list of the mechanisms that keep a deadline, so a shelf that restocks
/// and a debt of sleep that falls due hear the same advance whether the party stood at a counter or spent a
/// week on the road.
/// </para>
/// <para>
/// <b>It is a delivery list, not a scheduler.</b> Nothing here decides when time moves, holds a clock of its
/// own, or runs anything between advances: it exists because the world tells one owner about its charges,
/// and a session may have more than one mechanism that keeps a schedule.
/// </para>
/// <para>
/// A mechanism that advances the clock itself hands the advance to whoever it owes it to rather than through
/// this list, so an advance is never delivered to its own author twice.
/// </para>
/// </remarks>
public sealed class TimeOwners : IGameTimeObserver
{
    private readonly List<IGameTimeObserver> _owners = [];

    /// <summary>How many owners are told about an advance.</summary>
    public int Count => _owners.Count;

    /// <summary>Adds an owner, which is told about every advance from then on.</summary>
    /// <param name="owner">The mechanism that keeps a schedule against the one clock.</param>
    /// <exception cref="ArgumentNullException">The owner is null.</exception>
    /// <exception cref="ArgumentException">The owner is already told, which would deliver an advance to it twice.</exception>
    public void Add(IGameTimeObserver owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (_owners.Contains(owner))
        {
            throw new ArgumentException(
                "That owner is already told about the session's advances, so adding it again would land every deadline on it twice.",
                nameof(owner));
        }

        _owners.Add(owner);
    }

    /// <summary>Hands one advance of the session's one clock to every owner, in the order they were added.</summary>
    /// <param name="advance">Where the clock was, where it went, and what it brought due.</param>
    /// <exception cref="ArgumentNullException">The advance is null.</exception>
    public void Observe(ClockAdvance advance)
    {
        ArgumentNullException.ThrowIfNull(advance);
        foreach (IGameTimeObserver owner in _owners) owner.Observe(advance);
    }
}
