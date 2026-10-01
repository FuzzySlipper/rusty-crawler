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

        // The six operands are signed, as the donor stores them (OpenEnroth src/Engine/Evt/EvtInstruction.h:127-132):
        // a destination below a map's zero height is a negative z, not four billion.
        move = new MoveToMapInstruction(
            BinaryPrimitives.ReadInt32LittleEndian(operands),
            BinaryPrimitives.ReadInt32LittleEndian(operands[4..]),
            BinaryPrimitives.ReadInt32LittleEndian(operands[8..]),
            BinaryPrimitives.ReadInt32LittleEndian(operands[12..]),
            BinaryPrimitives.ReadInt32LittleEndian(operands[16..]),
            BinaryPrimitives.ReadInt32LittleEndian(operands[20..]),
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

    /// <summary>
    /// Reads this instruction as a variable comparison, addition, subtraction or assignment when it is one.
    /// </summary>
    /// <remarks>
    /// The four share one layout: a 16-bit variable code and a signed 32-bit value, and a comparison carries
    /// the step it jumps to when it holds as one more byte (OpenEnroth
    /// <c>src/Engine/Evt/EvtInstruction.cpp:979-995</c>).
    /// </remarks>
    /// <param name="variable">The decoded instruction.</param>
    public bool TryReadVariable(out VariableInstruction variable)
    {
        variable = default;
        if (Opcode is not (EvtOpcodes.Compare or EvtOpcodes.Add or EvtOpcodes.Subtract or EvtOpcodes.Set)) return false;
        ReadOnlySpan<byte> operands = Operands.Span;
        int required = Opcode == EvtOpcodes.Compare ? 7 : 6;
        if (operands.Length < required) return false;

        variable = new VariableInstruction(
            BinaryPrimitives.ReadUInt16LittleEndian(operands),
            BinaryPrimitives.ReadInt32LittleEndian(operands[2..]),
            Opcode == EvtOpcodes.Compare ? operands[6] : null);
        return true;
    }

    /// <summary>Reads the line of the map's own text this instruction names, when it names one.</summary>
    /// <remarks>
    /// A status line and a message name a 32-bit index into the map's string table; a mouse-over hint names a
    /// one-byte index into the same table (OpenEnroth <c>src/Engine/Evt/EvtInstruction.cpp:919-924</c> and
    /// <c>1056-1063</c>).
    /// </remarks>
    /// <param name="text">The index of the line in the map's string table.</param>
    public bool TryReadText(out int text)
    {
        text = 0;
        ReadOnlySpan<byte> operands = Operands.Span;
        switch (Opcode)
        {
            case EvtOpcodes.MouseOver when operands.Length >= 1:
                text = operands[0];
                return true;
            case EvtOpcodes.StatusText or EvtOpcodes.ShowMessage when operands.Length >= 4:
                text = BinaryPrimitives.ReadInt32LittleEndian(operands);
                return true;
            default:
                return false;
        }
    }

    /// <summary>Reads the step this instruction jumps to, when it is a plain jump.</summary>
    /// <param name="target">The step.</param>
    public bool TryReadJump(out int target)
    {
        target = 0;
        if (Opcode != EvtOpcodes.Jmp || Operands.Length < 1) return false;
        target = Operands.Span[0];
        return true;
    }

    /// <summary>Reads which characters the following steps act on, when this instruction chooses them.</summary>
    /// <remarks>
    /// The byte is the donor's character choice: one of the four characters by position, the active one, the
    /// whole party, or one at random (OpenEnroth <c>src/Engine/Evt/EvtEnums.h:261-270</c>).
    /// </remarks>
    /// <param name="who">The choice.</param>
    public bool TryReadForPartyMember(out byte who)
    {
        who = 0;
        if (Opcode != EvtOpcodes.ForPartyMember || Operands.Length < 1) return false;
        who = Operands.Span[0];
        return true;
    }

    /// <summary>Reads the steps a random jump chooses among, when this instruction is one.</summary>
    /// <remarks>
    /// Six one-byte steps are stored and the ones that are not zero are the choices (OpenEnroth
    /// <c>src/Engine/Evt/EvtInstruction.cpp:1036-1050</c>).
    /// </remarks>
    /// <param name="targets">The steps, in the order the record stores them.</param>
    public bool TryReadRandomGoTo(out IReadOnlyList<int> targets)
    {
        targets = [];
        if (Opcode != EvtOpcodes.RandomGoTo || Operands.Length < 6) return false;
        List<int> read = [];
        foreach (byte step in Operands.Span[..6])
        {
            if (step != 0) read.Add(step);
        }

        targets = read;
        return read.Count > 0;
    }

    /// <summary>Reads who is harmed, by what, and how much, when this instruction harms characters.</summary>
    /// <remarks>
    /// A character choice byte, a damage kind byte and a 32-bit amount (OpenEnroth
    /// <c>src/Engine/Evt/EvtInstruction.cpp:951-956</c>; the kinds are <c>src/Engine/Objects/ItemEnums.h:10-23</c>).
    /// </remarks>
    /// <param name="damage">The decoded instruction.</param>
    public bool TryReadReceiveDamage(out DamageInstruction damage)
    {
        damage = default;
        if (Opcode != EvtOpcodes.ReceiveDamage || Operands.Length < 6) return false;
        ReadOnlySpan<byte> operands = Operands.Span;
        damage = new DamageInstruction(operands[0], operands[1], BinaryPrimitives.ReadInt32LittleEndian(operands[2..]));
        return true;
    }

    /// <summary>Reads the season a jump waits for and the step it jumps to, when this instruction is one.</summary>
    /// <remarks>
    /// A one-byte season — spring, summer, autumn, winter — and the one-byte step (OpenEnroth
    /// <c>src/Engine/Evt/EvtInstruction.cpp:1193-1197</c>; the seasons are <c>src/Engine/Evt/EvtEnums.h:252-258</c>).
    /// </remarks>
    /// <param name="season">The season's number.</param>
    /// <param name="target">The step.</param>
    public bool TryReadCheckSeason(out int season, out int target)
    {
        season = 0;
        target = 0;
        if (Opcode != EvtOpcodes.CheckSeason || Operands.Length < 2) return false;
        season = Operands.Span[0];
        target = Operands.Span[1];
        return true;
    }

    /// <summary>Reads a timer trigger's period, when this instruction is one.</summary>
    /// <remarks>
    /// Three one-byte flags — yearly, monthly, weekly — then the hour, minute and second of a daily timer and
    /// a 16-bit interval in half minutes that, when it is not zero, replaces all of them (OpenEnroth
    /// <c>src/Engine/Evt/EvtInstruction.cpp:1064-1075</c>, read as <c>src/Engine/Evt/Processor.cpp:99-118</c>
    /// registers it).
    /// </remarks>
    /// <param name="timer">The decoded trigger.</param>
    public bool TryReadTimer(out TimerInstruction timer)
    {
        timer = default;
        if (Opcode is not (EvtOpcodes.OnTimer or EvtOpcodes.OnLongTimer) || Operands.Length < 8) return false;
        ReadOnlySpan<byte> operands = Operands.Span;
        timer = new TimerInstruction(
            operands[0] != 0,
            operands[1] != 0,
            operands[2] != 0,
            operands[3],
            operands[4],
            operands[5],
            BinaryPrimitives.ReadUInt16LittleEndian(operands[6..]));
        return true;
    }
}

/// <summary>A decoded variable comparison, addition, subtraction or assignment.</summary>
/// <param name="Variable">The donor's variable code (OpenEnroth <c>src/Engine/Evt/EvtEnums.h:82-249</c>).</param>
/// <param name="Value">The operand.</param>
/// <param name="Target">The step a comparison jumps to when it holds, or null for the other three.</param>
public readonly record struct VariableInstruction(ushort Variable, int Value, int? Target);

/// <summary>A decoded harm.</summary>
/// <param name="Who">The donor's character choice.</param>
/// <param name="Kind">The donor's damage kind.</param>
/// <param name="Amount">How much harm.</param>
public readonly record struct DamageInstruction(byte Who, byte Kind, int Amount);

/// <summary>A decoded timer trigger.</summary>
/// <param name="Yearly">Fires once a year.</param>
/// <param name="Monthly">Fires once a month.</param>
/// <param name="Weekly">Fires once a week.</param>
/// <param name="Hour">The hour a daily timer fires at.</param>
/// <param name="Minute">The minute a daily timer fires at.</param>
/// <param name="Second">The second a daily timer fires at.</param>
/// <param name="HalfMinutes">An interval in half minutes that replaces the rest when it is not zero.</param>
public readonly record struct TimerInstruction(bool Yearly, bool Monthly, bool Weekly, int Hour, int Minute, int Second, int HalfMinutes);

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
    int X,
    int Y,
    int Z,
    int Yaw,
    int Pitch,
    int ZSpeed,
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
