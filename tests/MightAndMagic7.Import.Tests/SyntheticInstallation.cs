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
    internal static string Create(bool withMaps = false)
    {
        string root = Path.Combine(Path.GetTempPath(), $"mm7-synthetic-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "DATA"));
        File.WriteAllText(Path.Combine(root, "Readme.txt"), "Might and Magic(TM) VII, For Blood and Honor(TM)\nUpdate v. 1.1 ReadMe\nAugust 1999\n");
        File.WriteAllBytes(
            Path.Combine(root, "DATA", "Events.lod"),
            LodFixture.Archive(
                "MMVI",
                LodFixture.TextTable("CLASS.TXT", Classes()),
                LodFixture.TextTable("SKILLDES.TXT", Skills()),
                LodFixture.TextTable("MAPSTATS.TXT", Maps(withMaps)),
                LodFixture.TextTable("2DEvents.txt", Buildings()),
                LodFixture.TextTable("MONSTERS.TXT", Monsters()),
                LodFixture.TextTable("SPELLS.TXT", Spells()),
                LodFixture.TextTable("ITEMS.TXT", Items()),
                LodFixture.TextTable("QUESTS.TXT", Quests()),
                ("OUT01.EVT", LodFixture.Compressed(EvtProgram(10, "Out02.odm"))),
                ("OUT02.EVT", LodFixture.Compressed(EvtProgram(11, "Out01.odm", 0, 0, 0))),
                ("D01.EVT", LodFixture.Compressed(EvtProgram(12, "0")))));
        if (withMaps)
        {
            // One payload per map file the per-map table names, so every place decodes and each is
            // identified by its own name.
            byte[] outdoor = MapDecoderTests.OutdoorPayload();
            byte[] outdoorDelta = MapDecoderTests.OutdoorDeltaPayload();
            byte[] indoor = MapDecoderTests.IndoorPayload();
            byte[] indoorDelta = MapDecoderTests.IndoorDeltaPayload();
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

    private static string Maps(bool withMaps)
    {
        _ = withMaps;
        StringBuilder text = new("map stats\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\n");
        text.Append(new string('\t', 32)).Append('\n');
        text.Append("#\tName\tFile name\t#\tDay\t0-20\tDays\tDays\tPerm\t0-20\t0-10\t0-6\t%\t%\t%\t%\tMon1 Pic\tMon 1\t 1-5\t#\tMon2 Pic\tMon 2\t 1-5\t#\tMon3 Pic\tMon 3\t 1-5\t#\tTrack\tEAX\tDesigner\tNotes\n");
        for (int map = 1; map <= MapRows; map++)
        {
            // With maps present, the rows name the two payloads the fixture holds: region rows the
            // outdoor map, interior rows the indoor one. Without maps the names are merely plausible.
            string file = map <= 13 ? $"Out{map:D2}.odm" : $"D{map - 13:D2}.blv";
            text.Append($"{map}\tMap {map}\t{file}\t0\t0\t0\t672\t7\t0\t0\t0\t1\t0\t10\t100\t0\t0\tMonster {map}\tMonster {map}\t1\t 2-5\t0\t0\t1\t 1-3\t0\t0\t1\t 1-3\t20\tFOREST\tDesigner\tNotes for map {map}\n");
        }

        return text.ToString();
    }

    private static string Buildings()
    {
        StringBuilder text = new("2d events\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\n");
        text.Append(new string('\t', 19)).Append('\n');
        for (int building = 1; building <= BuildingRows; building++)
        {
            // Every seventh row is a service, the rest are houses; the last row is a training hall with
            // no numeric cap, which the game writes as free text.
            string type = building % 7 == 0 ? "Weapon Shop" : $"House R{building}";
            string notes = building == BuildingRows ? "No Max" : "0";
            string sequence = building % 5 == 0 ? string.Empty : (building % 44).ToString();
            text.Append($"{building}\t{sequence}\t{type}\t{(building % MapRows) + 1}\t{building}\tBuilding {building}\tProprietor {building}\tOwner\t0\t0\t0\t0\t1.5\t1\t7\t0\t0\t{notes}\t9\t21\n");
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
            text.Append($"{monster}\tMonster {monster}\tzmon\t{monster}\t{hp}\t{monster % 40}\t{experience}\t2D6\t0\tN\tLong\tAggress\t3\t140\t100\t0\t0\tPhys\t2D8\t0\t0\n");
        }

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
