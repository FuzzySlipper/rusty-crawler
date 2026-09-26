using System.Buffers.Binary;
using System.Text;
using MightAndMagic7.Import.Events;

namespace MightAndMagic7.Import.Tests;

/// <summary>
/// Builds a complete synthetic installation: every table at the size the reader expects, and event
/// programs that link its maps. It exists so the whole import path — readers, writer, and the product's
/// loader — is exercised without any of the operator's data.
/// </summary>
internal static class SyntheticInstallation
{
    private const int ClassRanks = 36;
    private const int SkillRows = 37;
    private const int MapRows = 76;
    private const int BuildingRows = 525;
    private const int MonsterRows = 276;
    private const int SpellRows = 99;
    private const int ItemRows = 800;
    private const int QuestRows = 512;

    /// <summary>Writes a synthetic installation and returns its root.</summary>
    /// <param name="withMaps">
    /// Whether the installation carries map payloads. Without them the per-map table names files that
    /// are not there, which is enough for the table and graph readers but not for arrival points.
    /// </param>
    /// <param name="withContainers">
    /// Whether the maps carry chests, loose objects, and the events that open them. It is a separate
    /// choice from <paramref name="withMaps"/> so the suites that are about geometry and doors keep
    /// decoding the map they always did, and the container suites get a map that holds containers.
    /// </param>
    /// <param name="withServices">
    /// Whether the installation's buildings are counters the maps hang an event on, which is what the
    /// service emission reads: rows of several kinds, the programs that open them, and maps whose faces
    /// raise those events. It is a separate choice so the suites about tables, geometry, and containers
    /// keep the fixture they always had.
    /// </param>
    /// <param name="withPeople">
    /// Whether a map's own delta carries somebody standing in the open as well as the game's tables placing
    /// people in buildings. It is a separate choice for the same reason as the others: the people tables are
    /// always present, because every reader of the tables reads them, and this adds the record that makes a
    /// person stand where a map says.
    /// </param>
    /// <param name="emptyEncounterSlots">
    /// Whether the maps' second and third encounter slots name no monster, which is what the shipped maps
    /// state where a level spawns only one kind of creature. It is a separate choice so the suites that
    /// read placed creatures keep the slots they always had.
    /// </param>
    internal static string Create(
        bool withMaps = false,
        bool withContainers = false,
        bool withServices = false,
        bool withPeople = false,
        bool emptyEncounterSlots = false)
    {
        string root = Path.Combine(Path.GetTempPath(), $"mm7-synthetic-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "DATA"));
        File.WriteAllText(Path.Combine(root, "Readme.txt"), "Might and Magic(TM) VII, For Blood and Honor(TM)\nUpdate v. 1.1 ReadMe\nAugust 1999\n");
        List<(string Name, byte[] Payload)> events =
        [
            ("OUT01.EVT", LodFixture.Compressed(EvtProgram(10, "Out02.odm"))),
            ("OUT02.EVT", LodFixture.Compressed(EvtProgram(11, "Out01.odm", 0, 0, 0))),

            // The interior program keeps its within-map move whether or not the fixture holds containers,
            // so the two links the travel graph is built from stay exactly the two the outdoor programs
            // declare, and a chest opening is simply another instruction of the same program.
            ("D01.EVT", LodFixture.Compressed([
                .. EvtProgram(12, "0"),
                .. (withContainers ? ContainerDecoderTests.ChestProgram("0", (176, 0), (177, 1)) : []),
            ])),
        ];
        if (withContainers)
        {
            // Every interior gets a program that opens the two containers its delta holds, so an import
            // over this fixture places a container for every interior rather than for one of them.
            for (int index = 2; index <= MapRows - 13; index++)
            {
                events.Add(($"D{index:D2}.EVT", LodFixture.Compressed(ContainerDecoderTests.ChestProgram("0", (176, 0), (177, 1)))));
            }
        }

        // The service fixture's counters, each on an interior map the fixture can hang an event on: a
        // weapon shop, a stable, a temple, and a house. The map is the one the row's own map column names,
        // so the program named after that map's file is the program that opens the row.
        List<(int Building, ushort Event, string MapFile)> serviceRows =
        [
            (98, 98, $"D{ServiceMap(98) - 13:D2}.blv"),
            (99, 99, $"D{ServiceMap(99) - 13:D2}.blv"),
            (198, 198, $"D{ServiceMap(198) - 13:D2}.blv"),
            (104, 104, $"D{ServiceMap(104) - 13:D2}.blv"),
            (100, 100, $"D{ServiceMap(100) - 13:D2}.blv"),
        ];
        if (withServices)
        {
            foreach (IGrouping<string, (int Building, ushort Event, string MapFile)> byMap in serviceRows.GroupBy(row => row.MapFile))
            {
                events.Add((
                    $"{Path.GetFileNameWithoutExtension(byMap.Key).ToUpperInvariant()}.EVT",
                    LodFixture.Compressed([.. byMap.SelectMany(row => SpeakInHouse(row.Event, row.Building))])));
            }
        }

        File.WriteAllBytes(
            Path.Combine(root, "DATA", "Events.lod"),
            LodFixture.Archive(
                "MMVI",
                [
                    LodFixture.TextTable("CLASS.TXT", Classes()),
                    LodFixture.TextTable("SKILLDES.TXT", Skills()),
                    LodFixture.TextTable("MAPSTATS.TXT", Maps(withMaps, emptyEncounterSlots)),
                    LodFixture.TextTable("2DEvents.txt", Buildings(withServices)),
                    LodFixture.TextTable("MONSTERS.TXT", Monsters()),
                    LodFixture.TextTable("HOSTILE.TXT", Hostility()),
                    LodFixture.TextTable("SPELLS.TXT", Spells()),
                    LodFixture.TextTable("ITEMS.TXT", Items()),
                    LodFixture.TextTable("QUESTS.TXT", Quests()),
                    LodFixture.TextTable("npcdata.txt", Npcs()),
                    LodFixture.TextTable("npcgreet.txt", Greetings()),
                    LodFixture.TextTable("npctopic.txt", Topics()),
                    LodFixture.TextTable("npctext.txt", TopicTexts()),
                    .. events,
                ]));
        if (withMaps)
        {
            // One payload per map file the per-map table names, so every place decodes and each is
            // identified by its own name.
            byte[] outdoor = MapDecoderTests.OutdoorPayload();
            byte[] outdoorDelta = MapDecoderTests.OutdoorDeltaPayload(withPeople);
            // The container map raises one event per chest on one face each, so both of the delta's records
            // are placed, and its fixture positions are a hundred units apart, which is inside the spread a
            // container's faces may have.
            // The service fixture's interior faces raise the events the service rows are opened by, so the
            // emission has a door to stand each counter at; the container fixture's raise the two chest
            // events instead, and the plain fixture's raise none.
            List<int> interiorEvents = withServices
                ? [.. serviceRows.Select(row => (int)row.Event)]
                : withContainers ? [176, 177] : [];
            byte[] indoor = interiorEvents.Count > 0
                ? ContainerDecoderTests.ContainerIndoorPayload(interiorEvents)
                : MapDecoderTests.IndoorPayload();
            byte[] indoorDelta = interiorEvents.Count > 0
                ? ContainerDecoderTests.ContainerIndoorDeltaPayload(interiorEvents.Count)
                : MapDecoderTests.IndoorDeltaPayload();
            List<(string Name, byte[] Payload)> maps = [];
            for (int map = 1; map <= MapRows; map++)
            {
                maps.Add(map <= 13
                    ? ($"out{map:D2}.odm", LodFixture.CompressedStored(outdoor))
                    : ($"d{map - 13:D2}.blv", LodFixture.CompressedStored(indoor)));
                maps.Add(map <= 13
                    ? ($"out{map:D2}.ddm", LodFixture.CompressedStored(outdoorDelta))
                    : ($"d{map - 13:D2}.dlv", LodFixture.CompressedStored(indoorDelta)));
            }

            File.WriteAllBytes(Path.Combine(root, "DATA", "Games.lod"), LodFixture.Archive("GameMMVI", [.. maps]));
        }

        return root;
    }

    /// <summary>The map a building row of the fixture stands on, as the row's own map column computes it.</summary>
    /// <param name="building">The building's id.</param>
    internal static int ServiceMap(int building) => (building % MapRows) + 1;

    /// <summary>
    /// One <c>SpeakInHouse</c> instruction: the donor's opcode for opening a house, whose operand is the
    /// building's own id and whose record is the size byte, the event and step, the opcode, and the operand.
    /// </summary>
    /// <param name="eventId">The event the instruction belongs to.</param>
    /// <param name="building">The building the event opens.</param>
    internal static byte[] SpeakInHouse(ushort eventId, int building)
    {
        byte[] record = new byte[10];
        record[0] = 9;
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(1), eventId);
        record[3] = 0;
        record[4] = 2;
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(5), (uint)building);
        return record;
    }

    /// <summary>A small program with one move record.</summary>
    internal static byte[] EvtProgram(ushort eventId, string destination, uint x = 1234, uint y = 5678, uint z = 0)
    {
        byte[] name = Encoding.Latin1.GetBytes(destination);
        int payload = 31 + name.Length;
        byte[] record = new byte[payload + 1];
        record[0] = (byte)payload;
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(1), eventId);
        record[3] = 0;
        record[4] = EvtOpcodes.MoveToMap;
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(5), x);
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(9), y);
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(13), z);
        record[29] = 0;
        record[30] = 8;
        name.CopyTo(record, 31);
        return record;
    }

    private static string Classes()
    {
        StringBuilder text = new("Class\tDescriptions\tNotes\n");
        for (int rank = 1; rank <= ClassRanks; rank++)
        {
            text.Append($"Class {rank}\tThe description of class {rank}.\tBase {((rank - 1) / 4) + 1}\n");
        }

        return text.ToString();
    }

    private static string Skills()
    {
        StringBuilder text = new("Skill\tDescription\tNormal\tExpert\tMaster\tGrandMaster\n");
        for (int skill = 1; skill <= SkillRows; skill++)
        {
            text.Append($"Skill {skill}\tDescription of skill {skill}.\tn{skill}\te{skill}\tm{skill}\tg{skill}\n");
        }

        return text.ToString();
    }

    private static string Maps(bool withMaps, bool emptyEncounterSlots)
    {
        _ = withMaps;
        string second = emptyEncounterSlots ? "0\t0\t1\t 0-0" : "Monster 2\tMonster 2\t1\t 1-3";
        string third = emptyEncounterSlots ? "0\t0\t1\t 0-0" : "Monster 3\tMonster 3\t5\t 1-3";
        StringBuilder text = new("map stats\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\n");
        text.Append(new string('\t', 32)).Append('\n');
        text.Append("#\tName\tFile name\t#\tDay\t0-20\tDays\tDays\tPerm\t0-20\t0-10\t0-6\t%\t%\t%\t%\tMon1 Pic\tMon 1\t 1-5\t#\tMon2 Pic\tMon 2\t 1-5\t#\tMon3 Pic\tMon 3\t 1-5\t#\tTrack\tEAX\tDesigner\tNotes\n");
        for (int map = 1; map <= MapRows; map++)
        {
            // With maps present, the rows name the two payloads the fixture holds: region rows the
            // outdoor map, interior rows the indoor one. Without maps the names are merely plausible.
            string file = map <= 13 ? $"Out{map:D2}.odm" : $"D{map - 13:D2}.blv";
            // The trap columns (9 and 10) are non-zero so a written container's trap numbers are the ones
            // the map's own row declares rather than a default nothing wrote.
            // The encounter columns are the table's own: each slot names the monster it spawns in both
            // its picture and its name column, states the difficulty its grade odds are read at, and the
            // range of creatures it puts on the field. The slots name the graded groups the monster
            // fixture's rows carry, so a spawn record resolves to a row.
            text.Append($"{map}\tMap {map}\t{file}\t0\t0\t0\t672\t7\t0\t{(map % 20) + 1}\t{(map % 10) + 1}\t1\t0\t10\t100\t0\tMonster 1\tMonster 1\t{(map % 5) + 1}\t 2-5\t{second}\t{third}\t20\tFOREST\tDesigner\tNotes for map {map}\n");
        }

        return text.ToString();
    }

    private static string Buildings(bool withServices)
    {
        StringBuilder text = new("2d events\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\n");
        text.Append(new string('\t', 19)).Append('\n');
        for (int building = 1; building <= BuildingRows; building++)
        {
            // Every seventh row is a weapon shop and the rest are houses, except in the service fixture,
            // which also states a stable and a temple so the fare network and the temple's own policy have
            // a counter each. The last row is a training hall with no numeric cap, which the game writes as
            // free text.
            string type = withServices && building % 99 == 0
                ? "Stables"
                : withServices && building == 104
                    ? "Temple"
                    : building % 7 == 0 ? "Weapon Shop" : $"House R{building}";
            string notes = building == BuildingRows ? "No Max" : "0";
            string sequence = building % 5 == 0 ? string.Empty : (building % 44).ToString();
            // The columns are the table's own: the price multiplier, the skill multiplier, the stock
            // interval a shop restocks on, and the hours it keeps, so an emitted definition carries numbers
            // a ruleset can price and a shelf can be scheduled by.
            text.Append($"{building}\t{sequence}\t{type}\t{(building % MapRows) + 1}\t{building}\tBuilding {building}\tProprietor {building}\tOwner\t0\t0\t0\t0\t1.5\t1\t0\t7\t0\t{notes}\t9\t21\n");
        }

        return text.ToString();
    }

    private static string Monsters()
    {
        StringBuilder text = new("Default Monster Data\t\t\t\t\t\t\t\t\t\t\t\t\t\n");
        text.Append("#\tName\tPicture\tLVL\t HP \tAC\t EXP \tTreasure\tQuest\tFly\tMove\tAI Type\tHst\tSpd\tRec\tPref\tBonus\tType\tDamage\tMiss\tAtt%\n");
        text.Append(new string('\t', 20)).Append('\n');
        text.Append("\tA\t\t0\n");
        for (int monster = 1; monster <= MonsterRows; monster++)
        {
            // The two widest columns are written with thousands separators inside quotes in the real
            // data; one row here carries that shape so the reader keeps handling it.
            string hp = monster % 16 == 0 ? $"\" {monster * 1000:N0} \"" : (monster * 10).ToString();
            string experience = monster % 16 == 0 ? $"\" {monster * 10000:N0} \"" : (monster * 25).ToString();
            // The internal name is the slot's own kind plus the graded variant, which is the shape the real
            // table writes and the join a spawn record is resolved through: three rows share a kind, so a
            // map naming "Monster 2" and a record naming grade A meet at row 4.
            string variant = ((monster - 1) % 3) switch { 0 => "A", 1 => "B", _ => "C" };
            text.Append($"{monster}\tMonster {monster}\tMonster {((monster - 1) / 3) + 1} {variant}\t{monster}\t{hp}\t{monster % 40}\t{experience}\t2D6\t0\tN\tLong\tAggress\t3\t140\t100\t0\t0\tPhys\t2D8\t0\t0\n");
        }

        return text.ToString();
    }

    /// <summary>
    /// The shipped hostility matrix's shape: a header naming every kind of monster and one row per kind,
    /// each carrying a band per column.
    /// </summary>
    /// <remarks>
    /// The fixture states four kinds — the first four graded groups the monster rows belong to — and gives
    /// two of them a feud: the second and third hate each other with the two widest bands and are friendly
    /// to the rest, which is what a test reads to prove the matrix came from content rather than from code.
    /// </remarks>
    private static string Hostility()
    {
        StringBuilder text = new("\tParty\tMonster 1\tMonster 2\tMonster 3\n");
        text.Append("Party\t0\t0\t0\t0\n");
        text.Append("Monster 1\t0\t0\t0\t0\n");
        text.Append("Monster 2\t0\t0\t0\t4\n");
        text.Append("Monster 3\t0\t0\t3\t0\n");
        return text.ToString();
    }

    private static string Spells()
    {
        StringBuilder text = new("Spell Book\t\t\t\t\t\t\t\t\t\t\n");
        text.Append("#\tLvl\tFire Spells\tRes\tShort Name\tSpell Description\tNormal\tExpert\tMaster\tGrand Master\tStats\n");
        string[] schools = ["Fire", "Air", "Water", "Earth", "Spirit", "Mind", "Body", "Light", "Dark"];
        int id = 1;
        foreach (string school in schools)
        {
            text.Append($"\t\t{school} Spells\t\t\t\t\t\t\t\t\n");
            for (int level = 1; level <= SpellRows / schools.Length; level++)
            {
                text.Append($"{id}\t{level}\t{school} {level}\tFire\t{school}{level}\tThe {school} spell of level {level}.\tn\te\tm\tg\t0\n");
                id++;
            }
        }

        return text.ToString();
    }

    private static string Items()
    {
        StringBuilder text = new("Item #\tPic File\tName\tValue\tEquip Stat\tSkill Group\tMod1\tMod2\tMaterial\tID/Rep/St\tNot identified name\tSprite Index\tVarA\tVarB\n");
        for (int item = 0; item < ItemRows; item++)
        {
            if (item == 0)
            {
                text.Append("0\t\t\t\t\t\t\t\t\t\t\t\t\t\n");
                continue;
            }

            text.Append($"{item}\titem{item:D3}\tItem {item}\t{item * 10}\tWeapon\tSword\t1D6\t2\tSteel\t10\tUnidentified {item}\t{item % 300}\t0\t0\n");
        }

        return text.ToString();
    }

    /// <summary>
    /// The people the fixture's world holds: two rows placed in buildings and one with no building, one of
    /// them with dialogue events so a reply's residue has something to be about.
    /// </summary>
    /// <remarks>
    /// The columns are the donor's own (OpenEnroth <c>src/Engine/Tables/NPCTable.cpp:57-80</c>): the row's
    /// number, the name, the portrait, three group columns, the building, the profession, the greeting, the
    /// join flag, and six dialogue events. Row one is placed in building ninety-eight, which the service
    /// fixture hangs an event on and so places a door for, and row three is placed in a building nothing was
    /// placed for, so a reader that dropped an unreachable person instead of reporting one would be visible.
    /// </remarks>
    private static string Npcs()
    {
        StringBuilder text = new("NPC Data (Special)\t\t\tGroup\t\t\tCurrent\tProfession\tGreet\tJoin\tEvent\tEvent\tEvent\tEvent\tEvent\tEvent\t\n");
        text.Append("#\tName\tPic\tA\tB\tC\t2D Location\t 1 - 76\t#\tY / N\t# A\t# B\t# C\t# D\t# E\t# F\tNotes\n");
        text.Append("1\tTester One\t709\t0\t0\t0\t98\t0\t1\tN\t7\t9\t0\t0\t0\t0\tPlaced in the weapon shop the service fixture places\n");
        text.Append("2\tTester Two\t707\t0\t0\t0\t0\t0\t2\tY\t0\t0\t0\t0\t0\t0\tPlaced nowhere\n");
        text.Append("3\tTester Three\t162\t0\t0\t0\t999\t0\t3\tN\t0\t0\t0\t0\t0\t0\tPlaced in a building with no door\n");
        return text.ToString();
    }

    /// <summary>What each person says when met and when met again, keyed by the greeting column.</summary>
    private static string Greetings()
    {
        StringBuilder text = new("#\tGreeting 1\tGreeting 2\tNotes\tOwner\n");
        text.Append("1\t\"Well met, travellers.\"\t\"Back again, are you?\"\t\tTester One\n");
        text.Append("2\t\"A fine day for it.\"\t\"Still at it, then?\"\t\tTester Two\n");
        text.Append("3\t\"Mind the step.\"\t\"Careful, I said.\"\t\tTester Three\n");
        return text.ToString();
    }

    /// <summary>
    /// What the fixture's people can be asked about: one plain line, one the table gates on an errand, and
    /// one the table states with no answer at all.
    /// </summary>
    private static string Topics()
    {
        StringBuilder text = new("#\tTopic\tRequires\tNotes\tText #\tOwner(s)\tOwner #\n");
        text.Append("1\tThe contest\t0\tPlain line\t1\tTester One\t1\n");
        text.Append("2\tThe errand\t1\tGated on an errand\t2\tTester Two\t2\n");
        text.Append("3\tThe stub\t0\tNo answer at all\t\tTester Three\t3\n");
        return text.ToString();
    }

    /// <summary>What the fixture's people answer with, keyed by the number a topic names.</summary>
    private static string TopicTexts()
    {
        StringBuilder text = new("#\tText\tNotes\tOwner\n");
        text.Append("1\t\"The first to bring the items wins.\"\t\tTester One\n");
        text.Append("2\t\"I have work for you, if you are willing.\"\t\tTester Two\n");
        text.Append("3\t\"Nothing to say about that.\"\t\tTester Three\n");
        return text.ToString();
    }

    private static string Quests()
    {
        StringBuilder text = new("Q Bit\tQuest Note Text\tNotes\tOwner\t\n");
        for (int quest = 1; quest <= QuestRows; quest++)
        {
            text.Append($"{quest}\tQuest note {quest}.\t0\tOwner {quest}\t\n");
        }

        return text.ToString();
    }
}
