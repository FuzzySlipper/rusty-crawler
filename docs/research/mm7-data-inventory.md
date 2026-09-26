# Might and Magic VII — extracted data inventory

Read-only pass over the operator-supplied GOG install at `/home/research/old-games/game-mm7/`. Nothing under `/home/research/` was modified; all artifacts live under the git-ignored `local/` tree. Tool: `local/tools/mm7lod.py` (a local research tool kept out of the repository) (Python 3, stdlib only). Extracted tables: `local/extracted/events/` (34 files); derived listings in `local/extracted/`. Format knowledge: OpenEnroth `src/Library/Lod/*`, `src/Library/LodFormats/*`; table semantics: `src/Engine/Tables/*`, `src/Engine/Objects/*`. Provenance is marked **[data]** (observed in the extracted bytes), **[donor]** (documented at a named donor path), or **[uncertain]** (my reading, not confirmed).

## Containers

All five archives share one container: 256-byte `LodHeader_MM6`, one 32-byte root directory entry at offset 288, then `numItems` × 32-byte `LodEntry_MM6` records. Signature `"LOD\0"`, `numDirectories` 1 everywhere, no duplicate entry names (`src/Library/Lod/LodSnapshots.h`) **[donor]**.

| archive | bytes | version field | root dir | entries | payload storage |
| --- | --- | --- | --- | --- | --- |
| `GAMES.LOD` | 19,608,695 | `GameMMVI` | `maps` | 152 | all 152 `mvii`-header zlib |
| `Events.lod` | 432,573 | `MMVI` | `icons` | 198 | all 198 pseudo-entry + zlib |
| `ICONS.LOD` | 45,354,112 | `MMVI` | `icons` | 4,241 | 4,180 verbatim, 61 pseudo-entry |
| `SPRITES.LOD` | 76,300,007 | `MMVI` | `sprites08` | 9,343 | all verbatim |
| `BITMAPS.LOD` | 29,350,976 | `MMVI` | `bitmaps` | 1,614 | all verbatim |

Two payload wrappers, sniffed as `lod::decodeMaybeCompressed` does **[donor]**: (1) `LodCompressionHeader_MM6`, 16 B — `u32 version==91969`, `char[4] "mvii"`, `u32 dataSize`, `u32 decompressedSize` (0 = stored); the writer set `dataSize` to the whole record size rather than the payload size and `LodFormats.cpp` works around it, as does the tool. (2) `LodImageHeader_MM6`, 48 B, `flags == 0x100` ("not an image, but a text file") — `+0x14` compressed byte count, `+0x28` exact decompressed byte count, `blob_size == 48 + dataSize`; every text table in `Events.lod` uses this.

Entry families **[data]**: `GAMES.LOD` = 13 `.odm` + 13 `.ddm` + 63 `.blv` + 63 `.dlv`, each map file having exactly one same-stem companion. `Events.lod` = 34 `.txt` + 77 `.evt` + 76 `.str` + 11 `.bin` (`global.evt` is the one `.evt` with no `.str`). `ICONS.LOD` = 4,165 extension-less icon frames + 14 `.fnt` + 42 `.pcx` + strays; `SPRITES.LOD`/`BITMAPS.LOD` = extension-less image frames. **The version field does not identify the game** — all five MM7 archives say `MMVI`/`GameMMVI`, leftovers from the MM6 data the MM7 team reused. **[data]**

**Edition trap.** `ICONS.LOD` holds a second, MM6-era copy of tables that also exist in `Events.lod` (`Class.txt`, `MapStats.txt`, `Spells.txt`, `hostile.txt`, `Merchant.txt`, `RNDITEMS.TXT`, `SPCITEMS.TXT`, plus a stray `errorlog.txt`). These are not MM7 data: the `ICONS.LOD` `Class.txt` lists MM6 names (`Scout`, `Battle Mage`, `Fire Mage`, `Tracker`, `Sun Cleric`, `Moon Priest`) and its `MapStats.txt` has 67 map rows with a different column set (`First Visit Group`, `Refil Days`, `Lock 0-10`). An importer that resolves a table name from whichever archive it opens first will silently import MM6 classes into an MM7 ruleset. **Use `Events.lod`, never `ICONS.LOD`.** **[data]**

## Maps

**76 maps total: 13 outdoor, 63 indoor.** **[data]** `MAPSTATS.TXT` has 3 header rows then 76 data rows; every row's `File name` resolves 1:1 to a `GAMES.LOD` entry — no dangling references, no unreferenced map files. The linking field is **column 3 (`File name`)** (`Out01.Odm`, `d25.blv`), lowercased by the tool to match container entry names. **[data]**

Column meanings (1-based, `src/Engine/Tables/MapTable.cpp` `MapTable::Initialize`) **[donor]**: `#` map id; `Name` display name; `File name`; `#` reset count; `Day` first-visit day; `0-20` perception difficulty; `Days` respawn interval; `Days` alert duration; `Perm` base stealing fine; `0-20` disarm difficulty; `0-10` trap damage (D20 dice count); `0-6` map treasure level; four `%` columns (base encounter chance plus one per monster slot); a `Mon1/2/3 Pic` + `Mon1/2/3` name + `1-5` count-range triplet with a `#` per slot; `Track` music id; `EAX Environments`; `Map Designer`; `Notes`; `in area`. Observed ranges **[data]**: respawn interval 672 days on 62 rows / 336 on 7 / 168 on 4 / 0 on 3; alert duration 7 on all 76; first-visit day and reset count 0 on all 76; encounter chance 10 % on 69 rows, 0 % on 5, 100 % on 2; treasure level 0–5.

Grouping **[data]**: **13 outdoor regions** — Emerald Island (1), Harmondale (2), Erathia (3), The Tularean Forest (4), Deyja (5), The Bracada Desert (6), Evenmorn Island (9), Mount Nighon (10), The Barrow Downs (11), The Land of the Giants (12), Tatalia (13), Avlee (14), Shoals (15); there is no `Out07`/`Out08`. **2 indoor towns** — Celeste (7 → `d25.blv`), The Pit (8 → `d26.blv`). **5 castles** — Harmondale (21), Gryphonheart (48), Navan (49), Lambent (50), Gloaming (51). **6 temples** — of the Moon (20), of the Light (32), of the Dark (34), Grand Temple of the Moon (35), Grand Temple of the Sun (36), of Baa (45). **15 Barrows** (53–67), named `Barrow <roman numeral>`. **35 other indoor levels** — remaining dungeons, towers, caves, mines, strongholds, plus `The Small House` (75) and `The Arena` (76). Indoor file names use five families **[data]**: `dNN.blv` ×37; `mdtNN.blv` ×11 (present 01–05, 09–12, 14, 15 — `mdt06`/`07`/`08`/`13` absent); `mdkNN.blv` ×5 and `mdrNN.blv` ×5, which split the 15 Barrows between them; `tNN.blv` ×4; `nwc.blv` ×1.

## Services and houses (`2DEvents.txt`)

525 data rows, 66 columns, under 2 group/header rows. **[data]** Columns (0-based, `src/Engine/Tables/HouseTable.cpp`) **[donor]**: `0` house id; `1` per-type sequence; `2` type string (not localized); `3` map id; `4` animated-room/picture index; `5` name; `6` proprietor name; `7` proprietor title; `8` unused picture; `9` unused state; `10` unused reputation; `11` unused; `12` `Val` shop price multiplier (float); `13` `A` skill/spell price multiplier; `14` `B` always empty; `15` `C` item-generation interval in days; `16` notes / alternate room index; `17` `Notes(2)` max trainable level for Training houses; `18` `Open` hour; `19` `Closed` hour; `20` `Pic` exit picture; `21` `Map` exit map; `22` `Restrictions` exit quest bit; `23` `Text` exit text. Row 1 labels columns 18–19 `Schedules`, 20–21 `Other Exits`, 22 `Questbit`, 23 `Enter`.

**21 distinct building/service types are engine-recognized, covering 172 of 525 rows** **[data]**; type-string → `HouseType` per `HouseTable.cpp` `houseTypeMap` **[donor]**. Counts and one-line meanings: Tavern 21 (inn: rest, drinks, Arcomage; `Open`/`Closed` hours apply); Temple 15 (healing, condition cures, donations); Weapon Shop 14 (buys/sells weapons, `Val` sets prices); Armor Shop 14; Magic Shop 13 (wands, rings, amulets, gems); Training 13 (spend gold for a level); Alchemist 12 (potions, reagents); Boats 11 (paid travel to another coastal town); Bank 11 (deposit/withdraw gold); Stables 9 (paid overland travel); Town Hall 5 (civic services, bounty/monster-hunting board); Fire, Air, Water, Earth, Spirit, Mind and Body Guild 4 each (join to buy that school's spells); Light Guild 2 (`Guild of Illumination`, `Guild of Enlightenment`); Dark Guild 2 (`Guild of Twilight`, `Guild of Night`); Self Guild 2 (MM6-only type; both MM7 rows named `Placeholder`). `Element Guild`, `Mirrored Path Guild` and `Mercenary Guild` are in the engine's map but appear 0 times. **[data]**

The other 353 rows use 267 distinct free-text `Type` strings, none engine-classified as a service: house/entrance markers named by an art-set prefix plus index (`H_`, `E_`, `D_`, `N_`, `Wa_`, `Wz_` at 74/36/15/31/22/29 rows, and the plain `House Pn`/`Rn`/`Mn` form), or named quest-NPC, castle, throne and dungeon-entrance labels (`Dungeon Ent` ×30, `Castle Entrance` ×6, `Throne` ×6, `Lord Markham`, `The Seer`, `Master Thief`, `Jail`, …). **[data]** The prefixes track settlement art sets and the map column agrees **[data]**: `H_` on Erathia/Tatalia/Harmondale/Land of the Giants, `E_` on Tularean Forest/Avlee, `D_` on Stone City/Barrow Downs, `N_` on Deyja/The Pit, `Wa_` on Mount Nighon, `Wz_` on Bracada Desert/Celeste/Evenmorn. Expanding them as Human/Elf/Dwarven/Necromancer/Warlock/Wizard matches the animated-room names in `src/GUI/UI/UIHouses.cpp` (`Human Armor01`, `Elf Tavern`, `Dwarven Bank`, `Necromancer Temple`, `Warlock Tavern`, `Wizard Town Hall`) **[donor]**; the expansion itself is my reading **[uncertain]**. Schedules **[data]**: `Open`/`Closed` take 8 distinct pairs — 6–18 (161 rows), 18–6 (21, night trade), 1–24 (12), 5–2 (11), 8–2 (7); `C` is non-zero on 87 rows and is always 7, 14, 21 or 28 days.

## Characters

**36 classes** in `CLASS.TXT` (header `Class`, `Descriptions`, `Notes`; 36 rows) — exactly 9 base classes × (base + 1st promotion + Light 2nd + Dark 2nd) **[data]**: Knight → Cavalier → Champion / Black Knight; Thief → Rogue → Spy / Assassin; Monk → Initiate → Master / Ninja; Paladin → Crusader → Hero / Villain; Archer → Warrior Mage → Master Archer / Sniper; Ranger → Hunter → Ranger Lord / Bounty Hunter; Cleric → Priest → Priest of the Light / Priest of the Dark; Druid → Great Druid → Arch Druid / Warlock; Sorcerer → Wizard → Arch Mage / Lich. The `Notes` column repeats the base class name, so it is the machine-readable group key; Light/Dark variants are identified only by naming and prose — there is no numeric light/dark flag column. **[data]**

**4 races** **[data]**: `Global.txt` string ids 99 `Human`, 101 `Dwarf`, 103 `Elf`, 106 `Goblin`, matching `src/Engine/Objects/CharacterEnums.h` `Race` (4 entries) **[donor]**. No race table exists in `Events.lod`; per-race creation stat bonuses live in the executable (`Character.cpp` `StatTable`) **[donor]**. **7 primary attributes** **[data]** — the first 7 rows of `STATS.TXT` (header `Stats Descriptions`, `Description`): Might, Intellect, Personality, Endurance, Accuracy, Speed, Luck. The other 19 rows describe derived/UI values (Hit Points, Armor Class, Spell Points, Condition, Quick Spell, Age, Level, Experience, Attack/Shoot Bonus and Damage, six resistances, Skill Points). `Global.txt` is the localization string table (677 indexed English strings) — useful for grounding names, not a structured table. **[data]**

## Skills

**37 skills**, from `SKILLDES.TXT` (header `Skill`, `Description`, `Normal`, `Expert`, `Master`, `GrandMaster`) **[data]**, in file order: Staff, Sword, Dagger, Axe, Spear, Bow, Mace, Blaster, Shield, Leather, Chain, Plate, Fire, Air, Water, Earth, Spirit, Mind, Body, Light, Dark, Identify Item, Merchant, Repair, Bodybuilding, Meditation, Perception, Diplomacy, Thievery, Disarm Traps, Dodging, Unarmed, Identify Monster, Armsmaster, Stealing, Alchemy, Learning.

**Mastery-level columns exist**: `Normal`/`Expert`/`Master`/`GrandMaster` each carry the *effect text* for that rank (Sword: "Skill added to Attack Bonus" / "Skill reduces recovery time" / "Permits use of sword in left hand" / "Skill added to Armor Class"). They are descriptions, not numbers or gates. **[data]** The 37 entries match OpenEnroth's `SKILL_FIRST_VISIBLE..SKILL_LAST_VISIBLE` exactly, index for index **[donor]**; OpenEnroth also carries two hidden skills absent from the table (`SKILL_CLUB = 37`, `SKILL_MISC = 38`) **[donor]** and marks `SKILL_DIPLOMACY` / `SKILL_THIEVERY` "Not used in MM7" — their shipped descriptions still read "Not Used" and placeholder text. **[data]**

**Skills do not map to classes in `Events.lod`.** No class→skill availability or mastery-gate table exists in any extracted file — it is baked into `MM7.exe`. The donor transcribes it in `src/Engine/mm7_data.cpp`: `pSkillAvailabilityPerClass` (9 base classes × 39 skills, denied/available/primary) and `skillMaxMasteryPerClass` (36 classes × 39 skills, none/novice/expert/master/grandmaster), both annotated as read from `MM7.exe::004ED820` **[donor]**. `CLASS.TXT` mentions grandmaster skills in prose ("Champions can grandmaster in the Plate, Shield, Sword, and Spear skills"), which is not machine-readable. **[data]**

## Magic

**9 schools, 11 spells each, 99 spells total** in `SPELLS.TXT` **[data]**. Section header rows (empty first column) name the school: the first 11 spells are Fire (its header is the file's title row), then `Air Spells`, `Water Spells`, `Earth Spells`, `Spirit Spells`, `Mind Spells`, `Body Spells`, `Light Spells`, `Dark Spells`. School order matches `src/Engine/Spells/SpellEnums.h` `MAGIC_SCHOOL_FIRE..MAGIC_SCHOOL_DARK` **[donor]**. Columns **[data]**: `#` global spell id 1–99; `Lvl` level within the school 1–11; `<School> Spells` localized name; `Res` damage/resistance type (`none`, `Fire`, `Air`, `Water`, `Earth`, `Spirit`, `Mind`, `Body`, `Light`, `Dark` — `None` and `none` both appear, and Light's rows 4–5 are out of level order); `Short Name`; `Spell Description`; `Normal`/`Expert`/`Master`/`Grand Master` effect text; `Stats`, a flag string OpenEnroth reads for `m` (castable by monster), `e` (castable by event) and `c`/`x` (shift-click castable) (`Spells.cpp`) **[donor]**.

**A spell book names its spell in the item table's own reference column.** The 99 `Book` rows carry the
spell they teach in `Mod1` as the letter `S` and the spell's global id — item 400 is "Torch Light" with
`S1`, item 498 "Souldrinker" with `S99` — which is the join the donor turns into a spell id by position
(`src/Engine/Objects/ItemEnumFunctions.cpp:282`, `spellForSpellbook`, over a table generated from these
rows). The importer writes that join out as an `spell` field on the item, so no runtime reader parses the
shipped spelling. **[data]**

**Guild association is not in the data** — `SPELLS.TXT` has no guild column. Spells reach guilds through the school: `2dEvents.txt` carries `<School> Guild` houses (Fire/Air/Water/Earth/Spirit/Mind/Body ×4 tiers each, plus Light ×2 and Dark ×2), and the maximum spell level a guild tier may sell is the executable array `GuildSpellLevels` (MM7 house ids 139–170), documented at MMExtension `Scripts/Structs/00 structs.lua:500` **[donor]** — not extracted or verified here. **[uncertain]**

## Items and monsters

**Items: 800 rows** in `ITEMS.TXT` (ids 0–799, id 0 an empty placeholder), 17 columns **[data]**. Per `src/Engine/Tables/ItemTable.cpp` **[donor]**: `Item #`; `Pic File` (icon/sprite name); `Name`; `Value` (base gold); `Equip Stat` (equip type); `Skill Group`; `Mod1` (damage dice); `Mod2` (damage modifier); `material` (rarity); `ID/Rep/St` (identify/repair difficulty); `Not identified name`; `Sprite Index`; `VarA`; `VarB`; `Equip X`; `Equip Y`; `Notes`. Categories by `Equip Stat` via the donor's `equipStatMap` **[data + donor]**: Misc (no type) 206, Spell scroll 99, Book 99, Message scroll 93, Single-handed 64, Potion 52, Wand 25, Reagent 20, Armour 17, Two-handed 16, Helmet 16, Shield 14, Ring 14, Bow 12, Gem 11, Cloak 10, Boots 8, Belt 7, Gauntlets 7, Amulet 7, Gold 3. `material` is numeric by tier (0–10) except the special rarities, spelled out: `Artifact` ×22, `Relic` ×15, `Special` ×17. **[data]** Related: `RNDITEMS.TXT` (625 rows, random-item chance by treasure level 1–6), `STDITEMS.TXT` (35 rows, standard bonus by item type ×11 types), `SPCITEMS.TXT` (79 rows, special bonuses), `POTION.TXT` (104 mixtures), `SCROLL.TXT` (215 rows).

**Monsters: 276 rows** in `MONSTERS.TXT` (ids 1–276), 39 columns **[data]**. Per `src/Engine/Objects/Monsters.cpp` (`MonsterStats::Initialize`) **[donor]**: `#`; `Name`; `Picture`; `LVL`; `HP`; `AC`; `EXP`; `Treasure`; `Quest`; `Fly`; `Move`; `AI Type`; `Hst` (hostility); `Spd`; `Rec`; `Pref` (attack preference); `Bonus`; `Type`/`Damage`/`Miss` for attack 1 and attack 2; `Use%` + `Spl,Mas,Skil` twice for spell attacks; ten resistance columns (`Fire Air Water Earth Mind Spirit Body Light Dark Phys`, `Imm` encoding full immunity as 200); `Special`. Notable distributions **[data]**: AI type — `Wimp` 102, `Aggress` 81, `Suicidal` 55, `Normal` 38, mapped by the donor to `MONSTER_AI_WIMP/AGGRESSIVE/SUICIDE/NORMAL` **[donor]**; hostility (`Hst`) — 4 on 141 rows, 2 on 72, 3 on 45, 1 on 18, with no legend in the data, so the numeric scale is **[uncertain]** (it clearly drives `hostile.txt`, values 0–4); movement — `Long` 127, `Med` 61, `Free` 43, `Short` 42, `stand` 3; `Fly` — `Y` 51, `N` 225; quest flag — 1 on 224, 0 on 52; `Treasure` is a compact dice string such as `10%10D20+L3Misc`, encoding chance + dice + treasure level + item type. Related **[data]**: `Hostile.txt` is an 89×89 monster-vs-monster hostility matrix (row/column headers are monster names; first header cell blank, then `Party`); `PlaceMon.txt` names 13 uniquely placed monsters (`guard`, `Cleric of Baa`, `Morcarack`, `Xenofex`, `William Setag`, `Wromthrax`, `Mega-Dragon`, …) with 17 slots zeroed; `dmonlist.bin` (7,755 B) and `dobjlist.bin` (2,950 B) are binary placement lists I did not decode.

## Time and calendar

No calendar table exists in `Events.lod`. **[data]** What the tables do carry: `MAPSTATS.TXT` — respawn interval in days (672/336/168/0 across the 76 maps), alert duration in days (7 everywhere), reset count and first-visit day (both 0 everywhere), per-map encounter chances. `2DEvents.txt` — `Open`/`Closed` hours and `C` = item-generation interval in days (7/14/21/28 on 87 rows). `HISTORY.TXT` — 84 data rows over a 4-column header (`#`, `Text`, `Time`, `Page Title`), the journal's dated entries. `MERCHANT.TXT` (7 rows) and `NPCNEWS.TXT` (52 rows) are text, not timetables. **[data]** The clock itself is engine state: `src/Engine/Party.h` holds `uCurrentYear`, `uCurrentMonth`, `uCurrentMonthWeek`, `uCurrentDayOfMonth`, `uCurrentHour`, `uCurrentMinute`, `uCurrentTimeSecond` **[donor]**, and the 12 month names are localization strings using real-world names, not table data (`Localization.cpp`) **[donor]**. I found no hours-per-day or days-per-month table. **[uncertain]**

## Travel

Three mechanisms exist; only one lives in the text tables. (1) **`TRANS.TXT`** — 464 data rows, 3 columns (`2D#`, `Transition Description`, an unlocalized name column). Per `src/Engine/Tables/TransitionTable.cpp` this is an index → description string table for arrival text, **not** a link table. **[donor]** (2) **`2dEvents.txt` exit fields** — columns 20 `Pic`, 21 `Map`, 22 `Restrictions` (quest bit), 23 `Text`. **All four are 0 on every one of the 525 rows**, matching OpenEnroth's "(not used in MM7)" annotation: dead columns here. **[data + donor]** (3) **Per-map `.evt` event programs** — where the real map graph lives. The container is a flat record list: a size byte (`record = size + 1`), then `u16 eventId`, `u8 step`, `u8 opcode`, then opcode-specific operands (`src/Engine/Evt/EvtProgram.cpp`, `EvtInstruction.cpp`) **[donor]**. All 77 files walk cleanly — **16,699 instructions total** **[data]**. Travel opcodes are `EVENT_Exit = 1` (1,552 occurrences) and `EVENT_MoveToMap = 6` (271). `MoveToMap` carries six `u32` destination coordinates, `u8 house_id`, `u8 exit_pic_id`, and a null-terminated destination map file name, so the graph is directly recoverable.

**Result: 271 `MoveToMap` instructions — 78 intra-map teleports (empty destination) and 193 inter-map links, giving 165 directed edges over 86 unordered map pairs.** All 74 distinct destination names resolve to `GAMES.LOD` entries, zero unresolved. Outbound and inbound counts agree exactly for 68 of the 76 maps, a strong correctness signal; the eight asymmetries are the Bracada Desert (3 out/8 in), Celeste (9/6), The Pit (5/7), Castle Harmondale (1/2), Walls of Mist (2/1), Breeding Zone (2/1), and The Strange Temple and The Arena (1 each out, 0 in). Two sample rows from `local/extracted/travel-edges.tsv`:

```
src_map  src_name              dst_map  dst_name      event_id  step  house_id  exit_pic
23       The Erathian Sewers   3        Erathia       501       0     0         8
38       The Maze              10       Mount Nighon  501       0     0         8
```

Full data: `travel-edges.tsv` (165 edges), `travel-movetomap.tsv` (all 271 instructions with coordinates), `travel-summary.txt` (per-map in/out counts).

## Extraction limitations

- **Class → skill availability and mastery gates.** Not in `Events.lod` at all;
  compiled into `MM7.exe`. Only the donor's transcription (`mm7_data.cpp`) exists, and
  I did not read the executable.
- **Guild → spell-level gating.** Same: `GuildSpellLevels` is an executable array
  (`MMExtension Scripts/Structs/00 structs.lua:500`). The school↔guild link is
  structural, but which levels each guild tier sells is unverified here.
- **Calendar constants** (hours/day, days/month, days/year, month names) are
  engine/localization data, not tables.
- **`.bin` tables** — 11 in `Events.lod` (`dchest`, `ddeclist`, `dift`, `dmonlist`,
  `dobjlist`, `doverlay`, `dpft`, `dsft`, `dsounds`, `dtft`, `dtile`). Compressed
  binary with no layout documented in the paths I read; `ChestTable.cpp` mentions
  `dchest.bin` but does not give its format.
- **`.str` files** (76) — binary per-map string/decoration blobs; counted only.
- **Map geometry** — `.odm`, `.ddm`, `.blv`, `.dlv` are listed with sizes and
  decompress cleanly, but their internal structure was not decoded; byte-exact map
  geometry is explicitly out of scope for the product.
- **`ICONS.LOD`/`SPRITES.LOD`/`BITMAPS.LOD` pixel data** — storage kinds were sniffed
  and counted and the few text entries decoded, but `LodImageHeader` /
  `LodSpriteHeader` image payloads were not decoded.
- **`.evt` operands beyond `MoveToMap`** — event id, step and opcode are read, and
  `MoveToMap` operands are read; other opcodes' operands are not, so opcode
  *frequencies* are known but opcode *semantics* rest on the donor enum names alone.
- **`MoveToMap` coordinates** are raw `u32`; negative values appear as large unsigned
  numbers (e.g. `4294967295` = −1). The donor parses them the same way, so no
  interpretation was attempted.
- **Editorial vs shipped data.** `2DEvents.txt` has rows explicitly suffixed
  `(not used)` (`H_House P54(not used)`, `H_House R25(not used)`) and houses named
  `Placeholder`, and `ICONS.LOD` holds a whole MM6-era duplicate table set. Nothing
  here separates shipped-live rows from leftovers beyond those literal markers; treat
  counts as "rows in the file", not "reachable in play".

## Artifacts written

`local/tools/mm7lod.py` (reader: `info`, `list`, `cat`, `extract-text`); `local/extracted/events/` (34 byte-exact tables); `local/extracted/` — `map-inventory.tsv` (76 maps × 29 columns joined to `GAMES.LOD` sizes), `games-entries.tsv` (152 entries with size/offset/role/map link), `events-entries.tsv` (198 entries with decode kind), `container-summary.txt`, `2devents-types.tsv` (288 distinct type strings with counts), `classes.tsv`, `skills.tsv`, `spells.tsv`, `items.tsv`, `monsters.tsv`, `evt-event-ids.tsv`, `travel-edges.tsv`, `travel-movetomap.tsv`, `travel-summary.txt`.
