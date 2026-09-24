namespace MightAndMagic7.Import.Media;

/// <summary>The CRC-32 that PNG chunks are checked with.</summary>
/// <remarks>
/// PNG fixes the polynomial (reflected <c>0xEDB88320</c>) and the initial and final xor values, so this
/// is the whole of the checksum rather than a choice. It lives here instead of beside the writer because
/// a test that reads a PNG back needs the same function the writer used.
/// </remarks>
internal static class Crc32
{
    private static readonly uint[] Table = CreateTable();

    /// <summary>Computes the CRC of two spans as if they were concatenated.</summary>
    public static uint Compute(ReadOnlySpan<byte> first, ReadOnlySpan<byte> second)
    {
        uint crc = 0xFFFFFFFFu;
        foreach (byte value in first) crc = Table[(int)((crc ^ value) & 0xFF)] ^ (crc >> 8);
        foreach (byte value in second) crc = Table[(int)((crc ^ value) & 0xFF)] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFFu;
    }

    private static uint[] CreateTable()
    {
        uint[] table = new uint[256];
        for (uint index = 0; index < table.Length; index++)
        {
            uint value = index;
            for (int bit = 0; bit < 8; bit++) value = (value & 1) != 0 ? 0xEDB88320u ^ (value >> 1) : value >> 1;
            table[index] = value;
        }

        return table;
    }
}
