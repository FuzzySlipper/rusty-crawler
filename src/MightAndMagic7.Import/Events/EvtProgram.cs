using MightAndMagic7.Import.Lod;
using MightAndMagic7.Import.Tables;

namespace MightAndMagic7.Import.Events;

/// <summary>One map's event program: a flat list of instructions, grouped by event id and step.</summary>
public sealed class EvtProgram
{
    /// <summary>The smallest record the format allows: the size byte plus a four-byte instruction header.</summary>
    public const int MinimumRecordSize = 5;

    private EvtProgram(string name, EvtInstruction[] instructions)
    {
        Name = name;
        Instructions = instructions;
    }

    /// <summary>The program's entry name, which identifies the map it belongs to.</summary>
    public string Name { get; }

    /// <summary>Every instruction, in file order.</summary>
    public IReadOnlyList<EvtInstruction> Instructions { get; }

    /// <summary>The opcodes present, for a report of what this reader does not decode.</summary>
    public IReadOnlyDictionary<byte, int> OpcodeCounts =>
        Instructions
            .GroupBy(instruction => instruction.Opcode)
            .ToDictionary(group => group.Key, group => group.Count());

    /// <summary>Reads a program from an entry's bytes.</summary>
    public static EvtProgram Read(string name, byte[] bytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(bytes);
        List<EvtInstruction> instructions = [];
        int position = 0;
        while (position < bytes.Length)
        {
            int recordSize = bytes[position] + 1;
            if (recordSize < MinimumRecordSize)
            {
                throw new LodFormatException($"{name}: event record at offset {position} declares {recordSize} bytes, below the minimum.");
            }

            if (position + recordSize > bytes.Length)
            {
                throw new LodFormatException(
                    $"{name}: event record at offset {position} declares {recordSize} bytes but only {bytes.Length - position} remain.");
            }

            // The instruction header is the event id, the step, and the opcode; everything after it is
            // opcode-specific.
            ushort eventId = (ushort)(bytes[position + 1] | (bytes[position + 2] << 8));
            byte step = bytes[position + 3];
            byte opcode = bytes[position + 4];
            instructions.Add(new EvtInstruction(eventId, step, opcode, bytes.AsMemory(position + 5, recordSize - 5)));
            position += recordSize;
        }

        return new EvtProgram(name, [.. instructions]);
    }

    /// <summary>Reads every event program in an installation's rules archive.</summary>
    public static IReadOnlyList<EvtProgram> ReadAll(LodInstall install)
    {
        ArgumentNullException.ThrowIfNull(install);
        LodArchive archive = install.Archive(Mm7TableSources.RulesArchive);
        List<EvtProgram> programs = [];
        foreach (LodEntry entry in archive.Entries)
        {
            if (!entry.Name.EndsWith(".evt", StringComparison.OrdinalIgnoreCase)) continue;
            LodPayload payload = archive.Read(entry);
            programs.Add(Read(entry.Name, payload.Bytes));
        }

        return programs;
    }
}
