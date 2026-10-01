using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using MightAndMagic7.Import.Events;
using MightAndMagic7.Import.Tables;

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

    /// <summary>How many item rows the fixture's random-loot table weighs, which the shipped one does too.</summary>
    private const int RandomItemRows = 618;
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
                    LodFixture.TextTable("POTION.TXT", Potions()),
                    LodFixture.TextTable("POTNOTES.TXT", PotionNotes()),
                    LodFixture.TextTable("RNDITEMS.TXT", RandomItems()),
                    LodFixture.TextTable("QUESTS.TXT", Quests()),
                    LodFixture.TextTable("npcdata.txt", Npcs()),
                    LodFixture.TextTable("npcgreet.txt", Greetings()),
                    LodFixture.TextTable("npctopic.txt", Topics()),
                    LodFixture.TextTable("npctext.txt", TopicTexts()),
                    LodFixture.TextTable("AUTONOTE.TXT", Discoveries()),
                    ("dtile.bin", LodFixture.Compressed(TileTable())),
                    .. events,
                ]));
        if (withMaps)
        {
            // One payload per map file the per-map table names, so every place decodes and each is
            // identified by its own name. No two neighbouring maps share a shape: each region's model and
            // terrain and each plain interior's face, lights and door slots follow the map's own number
            // (<see cref="Region"/>, <see cref="Interior"/>), so a decoder that carried one map's counts into
            // the next, or assumed a fixed record size, would misread every other map rather than none.
            // The container map raises one event per chest on one face each, so both of the delta's records
            // are placed, and its faces stand the map's own spacing apart, which is inside the spread a
            // container's faces may have.
            // The service fixture's interior faces raise the events the service rows are opened by, so the
            // emission has a door to stand each counter at; the container fixture's raise the two chest
            // events instead, and the plain fixture's raise none.
            List<int> interiorEvents = withServices
                ? [.. serviceRows.Select(row => (int)row.Event)]
                : withContainers ? [176, 177] : [];
            List<(string Name, byte[] Payload)> maps = [];
            for (int map = 1; map <= MapRows; map++)
            {
                (string level, string delta, byte[] payload, byte[] deltaPayload) = map <= Regions
                    ? ($"out{map:D2}.odm", $"out{map:D2}.ddm", RegionPayload(map), MapDecoderTests.OutdoorDeltaPayload(withPeople))
                    : ($"d{map - Regions:D2}.blv", $"d{map - Regions:D2}.dlv", InteriorPayload(map - Regions, interiorEvents), InteriorDeltaPayload(map - Regions, interiorEvents));

                // Both of the compressed wrapper's forms carry maps: every third level is deflated, and every
                // third delta beside a different third of the levels, so the pipeline inflates levels and
                // deltas alike and reads the rest as stored.
                maps.Add((level, PayloadDeflated(map) ? LodFixture.Compressed(payload) : LodFixture.CompressedStored(payload)));
                maps.Add((delta, DeltaDeflated(map) ? LodFixture.Compressed(deltaPayload) : LodFixture.CompressedStored(deltaPayload)));
            }

            File.WriteAllBytes(Path.Combine(root, "DATA", "Games.lod"), LodFixture.Archive("GameMMVI", [.. maps]));
        }

        return root;
    }

    /// <summary>How many of the fixture's map rows are regions; the rest are interiors.</summary>
    internal const int Regions = 13;

    /// <summary>What one fixture region's outdoor payload holds besides what every region shares.</summary>
    /// <param name="ExtraVertices">Model vertices beyond the three its one face uses.</param>
    /// <param name="PeakHeight">
    /// The height byte at the terrain's centre cell. It varies within a few steps only: a much taller peak
    /// changes which regions the collision check admits, which is a different question from whether each
    /// map is decoded as its own.
    /// </param>
    internal readonly record struct RegionShape(int ExtraVertices, byte PeakHeight);

    /// <summary>What one plain fixture interior's indoor payload holds besides what every interior shares.</summary>
    /// <param name="Corners">How many corners its one face has, which sizes its vertex list and face pool.</param>
    /// <param name="Lights">How many lights it holds, which sizes its light pool and its sector's light list.</param>
    /// <param name="DoorSlots">How many door slots it declares, the first of them in use.</param>
    /// <param name="ChestSpacing">How far apart the container fixture's faces stand in it.</param>
    internal readonly record struct InteriorShape(int Corners, int Lights, int DoorSlots, int ChestSpacing);

    /// <summary>The shape of region <paramref name="region"/> (1 to 13); the first is the decoder suite's default.</summary>
    internal static RegionShape Region(int region) => new((region - 1) % 4, (byte)(5 + ((region - 1) % 3)));

    /// <summary>The shape of interior <paramref name="interior"/> (1 to 63); the first is the decoder suite's default.</summary>
    internal static InteriorShape Interior(int interior) =>
        new(3 + (interior % 5), 1 + (interior % 3), 1 + (interior % 4), 100 - (((interior - 1) % 5) * 10));

    /// <summary>Whether map row <paramref name="map"/>'s level payload is deflated rather than stored.</summary>
    internal static bool PayloadDeflated(int map) => map % 3 == 0;

    /// <summary>Whether map row <paramref name="map"/>'s delta is deflated rather than stored.</summary>
    internal static bool DeltaDeflated(int map) => map % 3 == 1;

    private static byte[] RegionPayload(int region)
    {
        RegionShape shape = Region(region);
        return MapDecoderTests.OutdoorPayload(shape.ExtraVertices, shape.PeakHeight, waterRow: true);
    }

    /// <summary>
    /// A terrain tile table in the game's own layout: nothing at zero, a dirt base, a water base flagged as water, and
    /// the shore tile after it flagged as shore — which is enough for the fixture's water row to be water but its last
    /// square.
    /// </summary>
    internal static byte[] TileTable()
    {
        (string Name, ushort Tileset, ushort Variant, ushort Flags)[] records =
        [
            ("pending", 255, 255, 0x40),
            ("dirttyl", 4, 0, 0),
            ("wtrtyl", 5, 0, 0x2),
            ("wtrdrNE", 5, 12, 0x300),
        ];
        byte[] bytes = new byte[4 + (records.Length * 26)];
        BitConverter.TryWriteBytes(bytes.AsSpan(0, 4), records.Length);
        for (int index = 0; index < records.Length; index++)
        {
            Span<byte> record = bytes.AsSpan(4 + (index * 26), 26);
            System.Text.Encoding.ASCII.GetBytes(records[index].Name).CopyTo(record);
            BitConverter.TryWriteBytes(record[20..], records[index].Tileset);
            BitConverter.TryWriteBytes(record[22..], records[index].Variant);
            BitConverter.TryWriteBytes(record[24..], records[index].Flags);
        }

        return bytes;
    }

    private static byte[] InteriorPayload(int interior, IReadOnlyList<int> events)
    {
        InteriorShape shape = Interior(interior);
        return events.Count > 0
            ? ContainerDecoderTests.ContainerIndoorPayload(events, shape.ChestSpacing)
            : MapDecoderTests.IndoorPayload(shape.Corners, lightCount: shape.Lights, doorSlots: shape.DoorSlots);
    }

    private static byte[] InteriorDeltaPayload(int interior, IReadOnlyList<int> events) =>
        events.Count > 0
            ? ContainerDecoderTests.ContainerIndoorDeltaPayload(events.Count)
            : MapDecoderTests.IndoorDeltaPayload(Interior(interior).DoorSlots);

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
        text.Append("#\tName\tPicture\tLVL\t HP \tAC\t EXP \tTreasure\tQuest\tFly\tMove\tAI Type\tHst\tSpd\tRec\tPref\tBonus\tType\tDamage\tMiss\tAtt%\tType\tDamage\tMiss\tUse%\tSpell\tUse%\tSpell\tFire\tAir\tWater\tEarth\tMind\tSpirit\tBody\tLight\tDark\tPhys\tSpecial\n");
        text.Append(new string('\t', 38)).Append('\n');
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
            // The treasure cells cover the shapes the shipped file states: a creature that drops nothing,
            // one that drops coin alone, one that states a chance, coin, a level and a kind, and one that
            // states no chance at all and so drops its item every time.
            string treasure = (monster % 4) switch
            {
                0 => "0",
                1 => "2D6",
                2 => "10%10D20+L3Sword",
                _ => "5D10+L1Cape",
            };
            // The combat cells cover the shapes the shipped file states: a special attack that is a word, one
            // with a strength, one with a count, and one with both; a spell cell with its three parts and the
            // one row that joins the rung and the skill; and a resistance written as an immunity.
            string special = (monster % 5) switch { 0 => "0", 1 => "Poison2", 2 => "Stealx2", 3 => "Poison3x2", _ => "Afraid" };
            string spell = (monster % 7) switch { 0 => "Fire Bolt,M,6", 1 => "Lightning Bolt,M10", _ => "0" };
            string fire = monster % 9 == 0 ? "Imm" : "10";
            string second = monster % 6 == 0 ? "20\tFire\t3D4+2\tFireAr" : "0\t0\t0\t0";
            text.Append($"{monster}\tMonster {monster}\tMonster {((monster - 1) / 3) + 1} {variant}\t{monster}\t{hp}\t{monster % 40}\t{experience}\t{treasure}\t0\tN\tLong\tAggress\t3\t140\t100\t0\t{special}\tPhys\t2D8\t0\t{second}\t{(spell == "0" ? 0 : 25)}\t{spell}\t0\t0\t{fire}\t5\t5\t5\t0\t0\t0\t0\t0\t15\t0\n");
        }

        return text.ToString();
    }

    /// <summary>
    /// The shipped hostility matrix's shape: a header naming every kind of monster and one row per kind,
    /// each carrying a band per column.
    /// </summary>
    /// <remarks>
    /// The fixture is the shipped size: the party and the 88 kinds <see cref="HostilityTable.ExpectedKinds"/>
    /// states, so a reader or a writer that stopped early, strode a row by the wrong width, or dropped the last
    /// row or column is visible, and every cell is <see cref="HostilityBand"/> of its position, so a test can
    /// check any cell without a copy of the table. The first three kinds keep one feud — the second and third
    /// hate each other with the two widest bands and are friendly to the first — which is what a test reads to
    /// prove the matrix came from content rather than from code. One row, <see cref="MisnamedHostilityRow"/>,
    /// spells its kind with spacing the header does not repeat, which is the shipped file's own inconsistency
    /// and the reason the matrix is read by position.
    /// </remarks>
    private static string Hostility()
    {
        int kinds = HostilityTable.ExpectedKinds;
        StringBuilder text = new("\tParty");
        for (int kind = 1; kind <= kinds; kind++) text.Append($"\tMonster {kind}");
        text.Append('\n');
        for (int row = 0; row <= kinds; row++)
        {
            text.Append(row switch
            {
                0 => "Party",
                MisnamedHostilityRow => $"Monster  {row}",
                _ => $"Monster {row}",
            });
            for (int column = 0; column <= kinds; column++) text.Append('\t').Append(HostilityBand(row, column).ToString(CultureInfo.InvariantCulture));
            text.Append('\n');
        }

        return text.ToString();
    }

    /// <summary>The fixture matrix row whose kind is spelled differently from its column's header.</summary>
    internal const int MisnamedHostilityRow = 50;

    /// <summary>
    /// The band the fixture matrix states at a row and a column, both counted from the party's own at zero.
    /// </summary>
    /// <remarks>
    /// The party's row and column, a kind's own cell, and the first three kinds among themselves are friendly
    /// except for the one feud; everything else follows a pattern that reaches every band from zero to four.
    /// </remarks>
    internal static int HostilityBand(int row, int column) => (row, column) switch
    {
        (2, 3) => 4,
        (3, 2) => 3,
        _ when row == 0 || column == 0 || row == column || (row <= 3 && column <= 3) => 0,
        _ => ((row * 7) + (column * 3)) % 5,
    };

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

    /// <summary>The first reagent row id the fixture's potion table declares.</summary>
    private const int FirstReagent = 200;

    /// <summary>The last reagent row id the fixture's potion table declares.</summary>
    private const int LastReagent = 219;

    /// <summary>The empty bottle's row id, which is the other half of a reagent's own recipe.</summary>
    private const int Bottle = 220;

    /// <summary>The catalyst's row id, whose own description is the gray the gray reagents are mixed into.</summary>
    private const int Catalyst = 221;

    /// <summary>The first real potion's row id, which is where the mixture matrix begins.</summary>
    private const int FirstPotion = 222;

    /// <summary>The last real potion's row id.</summary>
    private const int LastPotion = 271;

    /// <summary>
    /// What one reagent's own power is, in the shipped table's own ascending pattern.
    /// </summary>
    private static int ReagentPower(int reagent) => reagent switch
    {
        219 => 75,
        // The braces matter: a switch expression binds tighter than %, so an unparenthesised remainder would
        // be read as `% (5 switch {...})` and every reagent would come out at its own remainder.
        _ => ((reagent - FirstReagent) % 5) switch { 0 => 1, 1 => 5, 2 => 10, 3 => 20, _ => 50 },
    };

    /// <summary>The colour word one reagent is mixed into, which the potion rows' own descriptions state.</summary>
    private static string ReagentColour(int reagent) => reagent switch
    {
        <= 204 => "Red",
        <= 209 => "Blue",
        <= 214 => "Yellow",
        _ => "Gray",
    };

    /// <summary>
    /// The fixture's potion table, in the shape the shipped one states: four label columns, three colour-unit
    /// cells on the real potion rows, and the mixture matrix behind them.
    /// </summary>
    /// <remarks>
    /// The matrix is sparse and symmetric, and a cell is written exactly as the shipped table writes one: the
    /// id of what the pair makes, <c>no</c> for the diagonal, or <c>E</c> and a strength for a pair that goes
    /// off. The last thirty-two rows are repeated with the number column blank and the unit cells omitted,
    /// which is the second layout the shipped file carries and which the reader checks rather than trusts.
    /// </remarks>
    private static string Potions()
    {
        StringBuilder text = new("\tName\tDescription\tEffect\t\t\t");
        for (int potion = FirstPotion; potion <= LastPotion; potion++) text.Append('\t');
        text.Append('\n');

        for (int row = FirstReagent; row <= LastPotion; row++)
        {
            text.Append(Row(row, withUnits: true));
        }

        for (int row = 240; row <= LastPotion; row++) text.Append(Row(row, withUnits: false));
        return text.ToString();
    }

    /// <summary>One row of the fixture's potion table, in either of the two layouts the shipped file uses.</summary>
    private static string Row(int row, bool withUnits)
    {
        StringBuilder text = new();
        text.Append(withUnits ? row.ToString(CultureInfo.InvariantCulture) : string.Empty);
        text.Append('\t');
        text.Append(row switch
        {
            Bottle => "Potion Bottle",
            Catalyst => "Catalyst",
            _ => row <= LastReagent ? $"Reagent {row}" : $"Potion {row}",
        });
        text.Append('\t');
        text.Append(row switch
        {
            Bottle => "Empty Bottle",
            Catalyst => "Gray Potion",
            >= FirstPotion and <= 224 => $"{new[] { "Red", "Blue", "Yellow" }[row - FirstPotion]} Potion",
            >= FirstPotion => $"White Potion {row}",
            _ => "Reagent",
        });
        text.Append('\t');
        text.Append(row switch
        {
            Bottle => "None",
            Catalyst => "Boost Potion",
            _ when row <= LastReagent => $"+ Bottle = {ReagentColour(row)} Potion +{ReagentPower(row).ToString(CultureInfo.InvariantCulture)}",
            _ => $"Effect {row}",
        });

        if (withUnits)
        {
            for (int unit = 0; unit < 3; unit++)
            {
                text.Append('\t');
                if (row >= FirstPotion) text.Append(((row + unit) % 4).ToString(CultureInfo.InvariantCulture));
            }

            // A reagent has one recipe and no matrix; the real potion rows state the matrix itself.
            if (row <= LastReagent) text.Append('\t').Append(Bottle.ToString(CultureInfo.InvariantCulture));
            else if (row == Catalyst) text.Append('\t').Append(FirstPotion.ToString(CultureInfo.InvariantCulture));
            else
            {
                for (int other = FirstPotion; other <= LastPotion; other++) text.Append('\t').Append(Cell(row, other));
            }
        }

        return text.Append('\n').ToString();
    }

    /// <summary>One cell of the fixture's mixture matrix, which is a function of the pair and nothing else.</summary>
    private static string Cell(int row, int other)
    {
        if (row == other) return "no";
        int sum = row + other;
        if (sum % 7 == 0) return sum % 14 == 0 ? Math.Min(row, other).ToString(CultureInfo.InvariantCulture) : Math.Max(row, other).ToString(CultureInfo.InvariantCulture);
        if (sum % 11 == 0) return $"E{(sum % 4) + 1}";
        return string.Empty;
    }

    /// <summary>
    /// The fixture's discovery table: the potion table's own layout with a note index in each stated cell.
    /// </summary>
    private static string PotionNotes()
    {
        StringBuilder text = new("\tName\tDescription\tEffect\t\t\t");
        for (int potion = FirstPotion; potion <= LastPotion; potion++) text.Append('\t');
        text.Append('\n');

        for (int row = FirstReagent; row <= LastPotion; row++)
        {
            text.Append(Row(row, withUnits: true).TrimEnd('\n'));
            text.Append('\n');
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

            // One row of the fixture is a spell book, because the item table's own reference column carries
            // the spell a book teaches as the letter S and the spell's id: item 400 is "Fire Bolt" with S2,
            // which is the join the importer writes out for the ruleset to read.
            if (item == 400)
            {
                text.Append($"{item}\titem{item:D3}\tFire Bolt\t200\tBook\tMisc\tS2\t1\t3\t1\tBook of Learning\t{item % 300}\t0\t0\n");
                continue;
            }

            // The shipped reagent rows carry their own power in the table's damage column, which is where the
            // donor reads it from; the potion table's reader checks its own reading of the same number against
            // it, so the fixture states the two rows consistently.
            if (item is >= FirstReagent and <= LastReagent)
            {
                text.Append($"{item}\titem{item:D3}\tReagent {item}\t10\tReagent\tMisc\t{ReagentPower(item)}\t0\t1\t0\tReagent\t{item % 300}\t0\t0\n");
                continue;
            }

            text.Append($"{item}\titem{item:D3}\tItem {item}\t{item * 10}\tWeapon\tSword\t1D6\t2\tSteel\t10\tUnidentified {item}\t{item % 300}\t0\t0\n");
        }

        return text.ToString();
    }

    /// <summary>
    /// What the fixture's items weigh at each treasure level, in the shape the shipped table states it.
    /// </summary>
    /// <remarks>
    /// The shipped file weighs 618 items in its first section and holds the enchantment chances below them;
    /// the fixture states the same shape, with a second section whose rows carry no item id so a reader that
    /// read past the section boundary instead of stopping at it would be visible. The weights themselves are
    /// chosen so every level has something: the first level weighs the earliest items only, and each level
    /// below reaches further into the table.
    /// </remarks>
    private static string RandomItems()
    {
        StringBuilder text = new("Random Item Generation By Treasure Level 1 - 6\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\n");
        text.Append("\t\tChance By Level\t\t\t\t\t\t\t\t\t\t\t\t\t\t\n");
        text.Append("Item #\tPic File\t1\t2\t3\t4\t5\t6\t\t\tBackup from MM6\t\t\t\t\t\t\n");
        text.Append("0\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\n");
        for (int item = 1; item <= RandomItemRows; item++)
        {
            // Item 1 weighs at every level and every later item weighs nothing at the first: the fixture's
            // level one pool is one item, which is what a suite can assert a draw against.
            string weights = item == 1
                ? "5\t5\t5\t5\t5\t5"
                : string.Join('\t', Enumerable.Range(1, 6).Select(level => level == 1 ? "0" : ((item + level) % 7 == 0 ? "10" : "1")));
            text.Append($"{item}\titem{item:D3}\t{weights}\t\t\tBackup {item}\t\t\t\t\t\t\n");
        }

        text.Append("\n\n\n\n");
        text.Append("Bonus chance by level %\t\t1\t2\t3\t4\t5\t6\n");
        text.Append("\tStandard\t0\t40\t50\t60\t70\t80\n");
        text.Append("\tSpecial\t0\t0\t10\t20\t30\t40\n");
        text.Append("Weapons\tSpecial %\t0\t0\t10\t20\t30\t40\n");
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

    /// <summary>
    /// The discovery table: a note of each category the shipped table uses, a placeholder row whose text is
    /// <c>0</c> and an empty row, which are not notes, and the table's own padding after its last row.
    /// </summary>
    private static string Discoveries() =>
        "Note bit\tAutonote Text\tCategory\t\t\n"
        + "1\tAccepted a wand.\tMisc\t1\t\n"
        + "2\t50 points of temporary Fire resistance from the town well.\tStat\t1\tStat\n"
        + "3\t0\tStat\t\n"
        + "4\tObelisk message #1: abc\tObelisk\tArea 2\n"
        + "5\t\"Cure Wounds (Red) = Widoweeps Berries + Empty Bottle.\"\tpotion\n"
        + "6\t \t\n"
        + "\t\t\n";
}
