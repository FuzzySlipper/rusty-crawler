namespace MightAndMagic7.Import.Maps;

/// <summary>A map that did not decode, and the field that stopped it.</summary>
/// <remarks>
/// A failure is recorded rather than thrown away so an import can report every unreadable map at once
/// instead of stopping at the first. The reason names the field and the byte offset inside the entry,
/// which is what makes a payload that a later release changed diagnosable.
/// </remarks>
/// <param name="MapId">The map id the per-map table gives it.</param>
/// <param name="FileName">The map file's entry name.</param>
/// <param name="Reason">What failed, naming the field and offset.</param>
public sealed record MapDecodeFailure(int MapId, string FileName, string Reason);
