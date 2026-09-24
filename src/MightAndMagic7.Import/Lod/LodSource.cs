namespace MightAndMagic7.Import.Lod;

/// <summary>
/// Where one logical table lives: the archive that owns it and the entry name inside it.
/// </summary>
/// <remarks>
/// A source is declared, never inferred. The operator's installation carries more than one copy of
/// several rule tables — an older set from the previous game in the family sits in another archive
/// under the same names — so resolving a table by searching the archives in turn would silently
/// import another game's classes.
/// </remarks>
/// <param name="LogicalName">The name the rest of the importer uses for this table.</param>
/// <param name="ArchiveName">The archive that owns it.</param>
/// <param name="EntryName">The entry name inside that archive.</param>
public sealed record LodSource(string LogicalName, string ArchiveName, string EntryName)
{
    /// <inheritdoc />
    public override string ToString() => $"{LogicalName} = {ArchiveName}:{EntryName}";
}
