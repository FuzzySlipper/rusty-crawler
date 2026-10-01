namespace PartyRpg.Kit;

/// <summary>
/// The one source of change stamps: every stamp it hands out is later than every stamp it handed out before.
/// </summary>
/// <remarks>
/// An owner whose state a reader rebuilds something from takes a new stamp whenever that state changes, and takes
/// one when it is made. A reader that kept what it built beside the stamps it read then knows, by comparing
/// numbers, whether anything it read has changed since — without reading the state again. Because a stamp is
/// later than every earlier one, the latest stamp over several owners also moves when any one of them changes,
/// when one of them is replaced by a newly made owner, and when one leaves the set (the owner that let it go
/// changed too), so a reader may keep only that latest stamp. A stamp says nothing else: it is neither a count of
/// changes nor a time, and it is never saved.
/// </remarks>
public static class ChangeStamp
{
    private static long _last;

    /// <summary>A stamp later than every stamp taken before it.</summary>
    /// <returns>The new stamp, which is never zero.</returns>
    public static long Next() => Interlocked.Increment(ref _last);
}
