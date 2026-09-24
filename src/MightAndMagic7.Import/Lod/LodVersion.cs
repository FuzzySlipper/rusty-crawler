namespace MightAndMagic7.Import.Lod;

/// <summary>
/// The version string an archive header carries.
/// </summary>
/// <remarks>
/// This is deliberately not "which game the archive belongs to": every archive in the operator's
/// Might and Magic VII installation spells its version field <c>MMVI</c> or <c>GameMMVI</c>, leftovers
/// from the data the seventh game reused. It selects the entry record size and nothing else, and no
/// code may pick a rule table by it.
/// </remarks>
public enum LodVersion
{
    /// <summary>The header carries a version string this reader does not know.</summary>
    Unknown,

    /// <summary>The header says <c>MMVI</c>.</summary>
    Mm6,

    /// <summary>The header says <c>GameMMVI</c>.</summary>
    Mm6Game,

    /// <summary>The header says <c>MMVII</c>.</summary>
    Mm7,

    /// <summary>The header says <c>MMVIII</c>, whose file entries are wider.</summary>
    Mm8,
}
