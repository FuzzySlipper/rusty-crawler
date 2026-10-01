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
    /// <c>src/Engine/Evt/EvtInstruction.cpp:979-995</c>). A topic's offer check compares in the same layout
    /// (<c>src/Engine/Evt/EvtInstruction.cpp:1135-1140</c>).
    /// </remarks>
    /// <param name="variable">The decoded instruction.</param>
    public bool TryReadVariable(out VariableInstruction variable)
    {
        variable = default;
        if (Opcode is not (EvtOpcodes.Compare or EvtOpcodes.CanShowDialogItemCompare or EvtOpcodes.Add or EvtOpcodes.Subtract or EvtOpcodes.Set)) return false;
        bool compares = Opcode is EvtOpcodes.Compare or EvtOpcodes.CanShowDialogItemCompare;
        ReadOnlySpan<byte> operands = Operands.Span;
        int required = compares ? 7 : 6;
        if (operands.Length < required) return false;

        variable = new VariableInstruction(
            BinaryPrimitives.ReadUInt16LittleEndian(operands),
            BinaryPrimitives.ReadInt32LittleEndian(operands[2..]),
            compares ? operands[6] : null);
        return true;
    }

    /// <summary>Reads whether a topic's offer check offers the topic, when this instruction states it.</summary>
    /// <remarks>One byte, non-zero to offer (OpenEnroth <c>src/Engine/Evt/EvtInstruction.cpp:1145-1148</c>).</remarks>
    /// <param name="shows">Whether the topic is offered.</param>
    public bool TryReadCanShow(out bool shows)
    {
        shows = false;
        if (Opcode != EvtOpcodes.SetCanShowDialogItem || Operands.Length < 1) return false;
        shows = Operands.Span[0] != 0;
        return true;
    }

    /// <summary>Reads which person's greeting a greeting step changes and to which row, when this instruction is one.</summary>
    /// <remarks>A 32-bit person id and a 32-bit greeting row (OpenEnroth <c>src/Engine/Evt/EvtInterpreter.cpp:541-545</c>).</remarks>
    /// <param name="person">The person's id.</param>
    /// <param name="greeting">The greeting table row the person greets with afterwards.</param>
    public bool TryReadNpcGreeting(out int person, out int greeting)
    {
        person = 0;
        greeting = 0;
        if (Opcode != EvtOpcodes.SetNpcGreeting || Operands.Length < 8) return false;
        person = BinaryPrimitives.ReadInt32LittleEndian(Operands.Span);
        greeting = BinaryPrimitives.ReadInt32LittleEndian(Operands.Span[4..]);
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

    /// <summary>Reads which door a door step moves and how, when this instruction is one.</summary>
    /// <remarks>
    /// A one-byte door id — the id the interior's door record carries, not its index — and a one-byte action:
    /// open, close, or toggle a door at rest (OpenEnroth <c>src/Engine/Evt/EvtInstruction.cpp:985-988</c>; the
    /// actions are <c>src/Engine/Graphics/FaceEnums.h:70-74</c>).
    /// </remarks>
    /// <param name="door">The door's id.</param>
    /// <param name="action">The action byte.</param>
    public bool TryReadDoor(out int door, out int action)
    {
        door = 0;
        action = 0;
        if (Opcode != EvtOpcodes.ChangeDoorState || Operands.Length < 2) return false;
        door = Operands.Span[0];
        action = Operands.Span[1];
        return true;
    }

    /// <summary>Reads what an item gift gives, when this instruction is one.</summary>
    /// <remarks>
    /// A one-byte treasure level, a one-byte kind of random item, and a 32-bit item id that, when it is not
    /// zero, replaces whatever the level and kind drew (OpenEnroth <c>src/Engine/Evt/EvtInstruction.cpp:1118-1122</c>,
    /// run as <c>src/Engine/Evt/EvtInterpreter.cpp:489-498</c>).
    /// </remarks>
    /// <param name="gift">The decoded instruction.</param>
    public bool TryReadGiveItem(out GiveItemInstruction gift)
    {
        gift = default;
        if (Opcode != EvtOpcodes.GiveItem || Operands.Length < 6) return false;
        ReadOnlySpan<byte> operands = Operands.Span;
        gift = new GiveItemInstruction(operands[0], operands[1], BinaryPrimitives.ReadInt32LittleEndian(operands[2..]));
        return true;
    }

    /// <summary>Reads what a cast step casts, when this instruction is one.</summary>
    /// <remarks>
    /// A one-byte spell id, a one-byte mastery the donor reads one above what is stored, a one-byte skill rank,
    /// and the six 32-bit coordinates the spell flies from and to (OpenEnroth
    /// <c>src/Engine/Evt/EvtInstruction.cpp:1008-1018</c>).
    /// </remarks>
    /// <param name="cast">The decoded instruction.</param>
    public bool TryReadCastSpell(out CastSpellInstruction cast)
    {
        cast = default;
        if (Opcode != EvtOpcodes.CastSpell || Operands.Length < 27) return false;
        ReadOnlySpan<byte> operands = Operands.Span;
        cast = new CastSpellInstruction(
            operands[0],
            operands[1] + 1,
            operands[2],
            BinaryPrimitives.ReadInt32LittleEndian(operands[3..]),
            BinaryPrimitives.ReadInt32LittleEndian(operands[7..]),
            BinaryPrimitives.ReadInt32LittleEndian(operands[11..]),
            BinaryPrimitives.ReadInt32LittleEndian(operands[15..]),
            BinaryPrimitives.ReadInt32LittleEndian(operands[19..]),
            BinaryPrimitives.ReadInt32LittleEndian(operands[23..]));
        return true;
    }

    /// <summary>Reads which person a conversation step opens, when this instruction is one.</summary>
    /// <remarks>A 32-bit person id (OpenEnroth <c>src/Engine/Evt/EvtInstruction.cpp:1020-1022</c>).</remarks>
    /// <param name="person">The person's id.</param>
    public bool TryReadSpeakNpc(out int person)
    {
        person = 0;
        if (Opcode != EvtOpcodes.SpeakNpc || Operands.Length < 4) return false;
        person = BinaryPrimitives.ReadInt32LittleEndian(Operands.Span);
        return true;
    }

    /// <summary>Reads which person a move step moves and to which house, when this instruction is one.</summary>
    /// <remarks>A 32-bit person id and a 32-bit house id, zero for none (OpenEnroth <c>src/Engine/Evt/EvtInstruction.cpp:1113-1117</c>).</remarks>
    /// <param name="person">The person's id.</param>
    /// <param name="house">The house the person moves to, zero for none.</param>
    public bool TryReadMoveNpc(out int person, out int house)
    {
        person = 0;
        house = 0;
        if (Opcode != EvtOpcodes.MoveNpc || Operands.Length < 8) return false;
        person = BinaryPrimitives.ReadInt32LittleEndian(Operands.Span);
        house = BinaryPrimitives.ReadInt32LittleEndian(Operands.Span[4..]);
        return true;
    }

    /// <summary>Reads which topic of which person a topic step changes, when this instruction is one.</summary>
    /// <remarks>
    /// A 32-bit person id, the one-byte slot of the topic, and the 32-bit event the slot raises afterwards
    /// (OpenEnroth <c>src/Engine/Evt/EvtInstruction.cpp:1107-1111</c>).
    /// </remarks>
    /// <param name="topic">The decoded instruction.</param>
    public bool TryReadNpcTopic(out NpcTopicInstruction topic)
    {
        topic = default;
        if (Opcode != EvtOpcodes.SetNpcTopic || Operands.Length < 9) return false;
        ReadOnlySpan<byte> operands = Operands.Span;
        topic = new NpcTopicInstruction(
            BinaryPrimitives.ReadInt32LittleEndian(operands),
            operands[4],
            BinaryPrimitives.ReadInt32LittleEndian(operands[5..]));
        return true;
    }

    /// <summary>Reads which skill, at what rank and mastery, a skill jump waits for, when this instruction is one.</summary>
    /// <remarks>
    /// A one-byte skill, a one-byte mastery stored as the donor's own number, a 32-bit rank and the one-byte step
    /// (OpenEnroth <c>src/Engine/Evt/EvtInstruction.cpp:1128-1133</c>).
    /// </remarks>
    /// <param name="check">The decoded instruction.</param>
    public bool TryReadCheckSkill(out CheckSkillInstruction check)
    {
        check = default;
        if (Opcode != EvtOpcodes.CheckSkill || Operands.Length < 7) return false;
        ReadOnlySpan<byte> operands = Operands.Span;
        check = new CheckSkillInstruction(operands[0], operands[1], BinaryPrimitives.ReadInt32LittleEndian(operands[2..]), operands[6]);
        return true;
    }

    /// <summary>Reads which creatures a kill jump counts, when this instruction is one.</summary>
    /// <remarks>
    /// A one-byte policy — any creature, a group, a kind, a single creature — its 32-bit parameter, a one-byte
    /// count and the one-byte step (OpenEnroth <c>src/Engine/Evt/EvtInstruction.cpp:1169-1175</c>).
    /// </remarks>
    /// <param name="check">The decoded instruction.</param>
    public bool TryReadIsActorKilled(out ActorKilledInstruction check)
    {
        check = default;
        if (Opcode != EvtOpcodes.IsActorKilled || Operands.Length < 7) return false;
        ReadOnlySpan<byte> operands = Operands.Span;
        check = new ActorKilledInstruction(operands[0], BinaryPrimitives.ReadInt32LittleEndian(operands[1..]), operands[5], operands[6]);
        return true;
    }

    /// <summary>Reads which bit of which group a flag step sets or clears, when this instruction is one.</summary>
    /// <remarks>
    /// The faces' and the creatures' flag steps share one layout: a 32-bit group, a 32-bit attribute bit and a
    /// one-byte on or off (OpenEnroth <c>src/Engine/Evt/EvtInstruction.cpp:1024-1028</c> and <c>1198-1202</c>).
    /// </remarks>
    /// <param name="toggle">The decoded instruction.</param>
    public bool TryReadFlagToggle(out FlagToggleInstruction toggle)
    {
        toggle = default;
        if (Opcode is not (EvtOpcodes.SetFacesBit or EvtOpcodes.ToggleActorGroupFlag) || Operands.Length < 9) return false;
        ReadOnlySpan<byte> operands = Operands.Span;
        toggle = new FlagToggleInstruction(
            BinaryPrimitives.ReadInt32LittleEndian(operands),
            BinaryPrimitives.ReadUInt32LittleEndian(operands[4..]),
            operands[8] != 0);
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

/// <summary>A decoded item gift.</summary>
/// <param name="Level">The treasure level an item is drawn at, one to six (OpenEnroth <c>src/Engine/Objects/ItemEnums.h</c>, <c>ItemTreasureLevel</c>).</param>
/// <param name="Kind">The kind of random item drawn (<c>RandomItemType</c>, <c>src/Engine/Objects/ItemEnums.h:1084-1121</c>).</param>
/// <param name="Item">The item id given outright, or zero when the draw stands.</param>
public readonly record struct GiveItemInstruction(int Level, int Kind, int Item);

/// <summary>A decoded spell cast from the map.</summary>
/// <param name="Spell">The spell id.</param>
/// <param name="Mastery">The mastery, as the donor reads it: one novice, four grand master.</param>
/// <param name="Rank">The skill rank the spell is cast at.</param>
/// <param name="FromX">Where the spell flies from, X.</param>
/// <param name="FromY">Where the spell flies from, Y.</param>
/// <param name="FromZ">Where the spell flies from, Z.</param>
/// <param name="ToX">Where it flies to, X.</param>
/// <param name="ToY">Where it flies to, Y.</param>
/// <param name="ToZ">Where it flies to, Z.</param>
public readonly record struct CastSpellInstruction(int Spell, int Mastery, int Rank, int FromX, int FromY, int FromZ, int ToX, int ToY, int ToZ);

/// <summary>A decoded change of a person's topic.</summary>
/// <param name="Person">The person's id.</param>
/// <param name="Slot">Which of the person's topics, counting from zero.</param>
/// <param name="Event">The event the topic raises afterwards.</param>
public readonly record struct NpcTopicInstruction(int Person, int Slot, int Event);

/// <summary>A decoded skill jump.</summary>
/// <param name="Skill">The donor's skill number.</param>
/// <param name="Mastery">The donor's mastery number.</param>
/// <param name="Rank">The rank the skill must reach.</param>
/// <param name="Target">The step it jumps to.</param>
public readonly record struct CheckSkillInstruction(int Skill, int Mastery, int Rank, int Target);

/// <summary>A decoded kill jump.</summary>
/// <param name="Policy">Which creatures are counted.</param>
/// <param name="Parameter">The group, kind or creature the policy names.</param>
/// <param name="Count">How many must be dead.</param>
/// <param name="Target">The step it jumps to.</param>
public readonly record struct ActorKilledInstruction(int Policy, int Parameter, int Count, int Target);

/// <summary>A decoded flag toggle over a group of faces or creatures.</summary>
/// <param name="Group">The face group or creature group.</param>
/// <param name="Flag">The attribute bit.</param>
/// <param name="On">Whether the bit is set rather than cleared.</param>
public readonly record struct FlagToggleInstruction(int Group, uint Flag, bool On);

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
