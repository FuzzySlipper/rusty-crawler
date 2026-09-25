using System.Buffers.Binary;

namespace MightAndMagic7.Import.Events;

/// <summary>One instruction of a map's event program.</summary>
/// <param name="EventId">The event the instruction belongs to.</param>
/// <param name="Step">The instruction's step inside its event.</param>
/// <param name="Opcode">The instruction's opcode.</param>
/// <param name="Operands">The opcode's operands, exactly as stored.</param>
public readonly record struct EvtInstruction(ushort EventId, byte Step, byte Opcode, ReadOnlyMemory<byte> Operands)
{
    /// <summary>
    /// Reads this instruction as a map move when it is one. The operands are six 32-bit values
    /// (position, facing, and approach speed), a house id, an exit picture id, and a NUL-terminated
    /// destination map file name; an empty destination means the move stays on the current map.
    /// </summary>
    public bool TryReadMoveToMap(out MoveToMapInstruction move)
    {
        move = default;
        if (Opcode != EvtOpcodes.MoveToMap) return false;
        ReadOnlySpan<byte> operands = Operands.Span;
        if (operands.Length < 26) return false;

        int terminator = operands[26..].IndexOf((byte)0);
        if (terminator < 0) return false;

        move = new MoveToMapInstruction(
            BinaryPrimitives.ReadUInt32LittleEndian(operands),
            BinaryPrimitives.ReadUInt32LittleEndian(operands[4..]),
            BinaryPrimitives.ReadUInt32LittleEndian(operands[8..]),
            BinaryPrimitives.ReadUInt32LittleEndian(operands[12..]),
            BinaryPrimitives.ReadUInt32LittleEndian(operands[16..]),
            BinaryPrimitives.ReadUInt32LittleEndian(operands[20..]),
            operands[24],
            operands[25],
            System.Text.Encoding.Latin1.GetString(operands.Slice(26, terminator)));
        return true;
    }

    /// <summary>
    /// Reads this instruction as a container opening when it is one. Its whole operand is the one-byte
    /// index of the container in the map's own container array (OpenEnroth
    /// <c>src/Engine/Evt/EvtInstruction.cpp:942-944</c>).
    /// </summary>
    /// <param name="open">The container the instruction opens.</param>
    public bool TryReadOpenChest(out OpenChestInstruction open)
    {
        open = default;
        if (Opcode != EvtOpcodes.OpenChest) return false;
        ReadOnlySpan<byte> operands = Operands.Span;
        if (operands.Length < 1) return false;

        open = new OpenChestInstruction(operands[0]);
        return true;
    }
}

/// <summary>A decoded container opening.</summary>
/// <param name="ContainerId">
/// The container's index in the map's own container array, which is the chest record's index in the
/// decoded delta. The donor refuses an index of 20 or more, because the runtime holds twenty
/// (OpenEnroth <c>src/Engine/Objects/Chest.cpp:52</c>).
/// </param>
public readonly record struct OpenChestInstruction(byte ContainerId);

/// <summary>A decoded map move.</summary>
/// <param name="X">Destination X coordinate.</param>
/// <param name="Y">Destination Y coordinate.</param>
/// <param name="Z">Destination Z coordinate.</param>
/// <param name="Yaw">Destination facing.</param>
/// <param name="Pitch">Destination pitch.</param>
/// <param name="ZSpeed">Approach speed along Z.</param>
/// <param name="HouseId">The house the move arrives at, when it arrives at one.</param>
/// <param name="ExitPicture">The exit picture shown while arriving.</param>
/// <param name="DestinationMapFile">The destination map's file name, empty for a move within the map.</param>
public readonly record struct MoveToMapInstruction(
    uint X,
    uint Y,
    uint Z,
    uint Yaw,
    uint Pitch,
    uint ZSpeed,
    byte HouseId,
    byte ExitPicture,
    string DestinationMapFile)
{
    /// <summary>
    /// Whether the move stays on the map that issued it. The data writes the placeholder <c>0</c> where
    /// a destination file name would go.
    /// </summary>
    public bool IsWithinMap => DestinationMapFile.Length == 0 || DestinationMapFile == "0";
}
