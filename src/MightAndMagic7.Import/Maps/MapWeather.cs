namespace MightAndMagic7.Import.Maps;

/// <summary>The weather a delta records.</summary>
/// <remarks>
/// Indoor deltas carry this block too and never use it. The block's last 24 bytes are consumed but not
/// surfaced: no donor reads them, so naming them would be invention.
/// </remarks>
/// <param name="SkyTexture">The sky texture's name, empty when the map has none.</param>
/// <param name="Flags">The weather flags; bit 1 marks fog.</param>
/// <param name="FogDistance1">The first fog distance.</param>
/// <param name="FogDistance2">The second fog distance.</param>
public readonly record struct MapWeather(string SkyTexture, int Flags, int FogDistance1, int FogDistance2);
