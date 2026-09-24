using System.Buffers.Binary;
using System.Text;

namespace MightAndMagic7.Import.Media;

/// <summary>
/// What a decoded sound sample's RIFF header says about it.
/// </summary>
/// <remarks>
/// The shipped bank holds IMA/DVI ADPCM rather than PCM, so the extractor keeps the compressed RIFF
/// bytes and records the facts instead of pretending to have written playable samples. Reading the
/// format out of the file, rather than assuming the block arithmetic, is deliberate: the header is
/// where the samples-per-block number lives, and the arithmetic that derives it does not hold for every
/// block size in the bank.
/// </remarks>
/// <param name="FormatTag">The <c>wFormatTag</c> the format chunk declares; 17 is IMA/DVI ADPCM.</param>
/// <param name="Codec">The codec name that tag corresponds to.</param>
/// <param name="Channels">Channel count.</param>
/// <param name="SampleRate">Sample rate in hertz.</param>
/// <param name="BlockAlign">Bytes per compressed block.</param>
/// <param name="BitsPerSample">Bits per sample.</param>
/// <param name="SamplesPerBlock">Samples one block carries, or zero when the header does not say.</param>
/// <param name="Samples">Exact sample count from the <c>fact</c> chunk, or zero when there is none.</param>
public readonly record struct WaveFacts(
    int FormatTag,
    string Codec,
    int Channels,
    int SampleRate,
    int BlockAlign,
    int BitsPerSample,
    int SamplesPerBlock,
    long Samples)
{
    /// <summary>Whether the samples are stored compressed rather than as PCM.</summary>
    public bool IsCompressed => FormatTag != 1;

    /// <summary>Reads the facts of a RIFF/WAVE payload, or reports why it is not one.</summary>
    /// <param name="riff">The sample's bytes.</param>
    /// <param name="facts">The facts, when the bytes are a RIFF/WAVE file.</param>
    /// <param name="reason">Why the bytes are not a RIFF/WAVE file, when they are not.</param>
    public static bool TryRead(byte[] riff, out WaveFacts facts, out string? reason)
    {
        ArgumentNullException.ThrowIfNull(riff);
        facts = default;
        reason = null;
        if (riff.Length < 12 || !riff.AsSpan(0, 4).SequenceEqual("RIFF"u8) || !riff.AsSpan(8, 4).SequenceEqual("WAVE"u8))
        {
            reason = "the payload does not begin with a RIFF/WAVE header";
            return false;
        }

        int formatTag = 0;
        int channels = 0;
        int sampleRate = 0;
        int blockAlign = 0;
        int bitsPerSample = 0;
        int samplesPerBlock = 0;
        long samples = 0;
        bool haveFormat = false;
        bool haveData = false;

        // Chunks are word-aligned, so an odd-sized chunk is followed by a pad byte.
        int at = 12;
        while (at + 8 <= riff.Length)
        {
            string id = Encoding.ASCII.GetString(riff, at, 4);
            int size = (int)BinaryPrimitives.ReadUInt32LittleEndian(riff.AsSpan(at + 4));
            int body = at + 8;
            if (size < 0 || body + (long)size > riff.Length) break;

            if (id == "fmt " && size >= 16)
            {
                formatTag = BinaryPrimitives.ReadUInt16LittleEndian(riff.AsSpan(body));
                channels = BinaryPrimitives.ReadUInt16LittleEndian(riff.AsSpan(body + 2));
                sampleRate = (int)BinaryPrimitives.ReadUInt32LittleEndian(riff.AsSpan(body + 4));
                blockAlign = BinaryPrimitives.ReadUInt16LittleEndian(riff.AsSpan(body + 12));
                bitsPerSample = BinaryPrimitives.ReadUInt16LittleEndian(riff.AsSpan(body + 14));
                // The extension word at +16 is cbSize; the samples-per-block count follows it, and only
                // exists when cbSize says there is an extension.
                if (size >= 20 && BinaryPrimitives.ReadUInt16LittleEndian(riff.AsSpan(body + 16)) >= 2)
                {
                    samplesPerBlock = BinaryPrimitives.ReadUInt16LittleEndian(riff.AsSpan(body + 18));
                }

                haveFormat = true;
            }
            else if (id == "fact" && size >= 4)
            {
                samples = BinaryPrimitives.ReadUInt32LittleEndian(riff.AsSpan(body));
            }
            else if (id == "data")
            {
                haveData = true;
            }

            at = body + size + (size & 1);
        }

        if (!haveFormat)
        {
            reason = "the RIFF file has no format chunk";
            return false;
        }

        if (!haveData)
        {
            reason = "the RIFF file has no data chunk";
            return false;
        }

        facts = new WaveFacts(formatTag, CodecName(formatTag), channels, sampleRate, blockAlign, bitsPerSample, samplesPerBlock, samples);
        return true;
    }

    private static string CodecName(int formatTag) => formatTag switch
    {
        1 => "pcm",
        2 => "adpcm_ms",
        17 => "adpcm_ima_wav",
        85 => "mpeg_layer3",
        _ => $"tag_{formatTag}",
    };
}
