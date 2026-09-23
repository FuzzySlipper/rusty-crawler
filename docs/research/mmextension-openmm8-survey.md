# Donor survey: MMExtension and OpenMM8

Read-only survey of `/home/research/old-games/{MMExtension,OpenMM8}` for a downstream repo shipping a reusable
**construction kit** plus one compiled **MM6/7/8 ruleset/content loader**. Method: listing/read/grep/glob only; no
builds or tests. Paths are repo-relative. Anything inferred or unverifiable is marked **[uncertain]**.
Snapshots: MMExtension `master` @ `9d73c0f` (2026-04-07), 597 commits; OpenMM8 `master` @ `59937a3` (2021-01-14), 152 commits.

---

## 1. MMExtension (GrayFace / Sergey Rozhenko)

### 1.1 What it is and how it works

MMExtension is a **modding framework that injects itself into the original MM6/7/8 executables** — not a
reimplementation and no standalone engine. `Src/MMExtension/MMExtension.cpp:9-18` shows the whole startup:
`DllMain` → `FindMMVersion()` → `InitPath()` → `WriteHooks()`; it is loaded as an ExeMod by GrayFace's own patches
via `mm6.ini`/`mm7.ini`/`mm8.ini` (`[ExeMods] MMExtension=<repo>\ExeMods\MMExtension.dll`, `README.md`).

- **Version detection / targets.** `Src/MMExtension/common.cpp:15-41` identifies the game by probing one byte
  at `0x41EDE1` (`0xEC`→MM6, `0x45`→MM7, `0x53`→MM8) and then selects per-game pointers (`MainWindow`) and a
  per-game spell-point-stat table (`MM6_SPStatKinds`, `MM7_SPStatKinds`, `MM8_SPStatKinds`, same file lines 7-9).
- **Patch level.** `README.md` requires "GrayFace patch version 2.1+"; `MMEditor Readme.txt` requires patch 2.2; the
  reference doc mentions v2.5.4 features. **The hooks are raw addresses, so MMExtension targets the GrayFace-patched
  binaries, not the vanilla retail executables.** The exact supported build set per game is unstated in-tree **[uncertain]**.
- **Mechanism.** Per-game hook tables `Src/MMExtension/{MM6,MM7,MM8}_HooksList.h` (~32-38 entries each) list
  `{address, replacement, HKT_CALL|HKT_JMP|HKT_DWORD|HKT_PATCH|HKT_NOP, size}` records, e.g. `MM8_HooksList.h`
  hooks `_MM8_ProcessEventStart`, `_MM8_LoadGame`, `_MM8_BeforeLoadMap`, `OnTimer`, `MM8_CalcSpellDamage`, plus
  byte patches; detouring uses the bundled `HDetour` library. An embedded **LuaJIT** runtime
  (`Src/MMExtension/LuaJIT/`) plus a Lua console and `mobdebug.lua` let mods read/write game memory (`mem.*`,
  `Scripts/Core/RSMem.lua`) and declare structures (`structs.*`).
- **Size/maturity.** 87 Lua files / ~50k lines under `Scripts/`; 597 commits, HEAD `9d73c0f` (2026-04-07) —
  actively maintained. Reference manual `MMExtension.htm` (1.1 MB, 4,598 anchors, Russian twin `MMExtensionRu.htm`)
  is generated from the scripts by `Scripts/Help/{HelpParse,HelpWrite,help,RunHelp}.lua`.

### 1.2 Directory structure and ownership

| Path | Owns |
|---|---|
| `Scripts/Core/` | Loader and runtime: `main.lua`, `events.lua` (game hooks), `evt.lua` (in-game script language), `evtdeco.lua` (EVT decompiler), `npc.lua`, `timers.lua`, `ConstAndBits.lua` (`const.*`), `RSMem.lua`, `RSPersist.lua`. Not for user scripts. |
| `Scripts/General/` | Loaded once at start, never unloaded: `DataTables.lua` (text/binary table pipeline), `Build MMExtension.lua`, `Editor Lods.lua`, dev helpers (`FindHelper.lua`). |
| `Scripts/Global/` | Loaded on new game / save load, unloaded on exit to menu; **maps the level editor** (`Editor Base/Data/GUI/Props/Navigate/Import/Export/BSP/Ground/Odm Read/Odm Data.lua`, `Convert Blv.lua`) plus `GodMode.lua`. |
| `Scripts/Structs/` | Structure definitions: `00 structs.lua` (in-memory game objects), `01 common structs.lua` (data tables, maps, LODs, items), `02 Increase Facets Limit.lua`, `After/*` (post-load fixups, limit removals, spells, dialogs, quests/localization, text-table parsers). |
| `Scripts/Modules/`, `Scripts/Help/` | Opt-in modules via `require` (`Faces`, `PaperDoll`, `Arcomage`, `Snow`, `RSParse`, `RSTemplates`, `KeepLogs`) and the doc generator (`HelpParse`/`HelpWrite`/`RunHelp.lua`, `MMExtRefStart.htm`). |
| `Src/` | `MMExtension/` (C++ DLL), `MMExtDialogs/`, `MMEditorDlg/`, `MMExtRunner/`, `RSPak/` (Delphi UI lib). |
| `ExeMods/`, `Data/`, `Misc/` | Prebuilt DLLs (`MMExtension`, `MMExtCore`, `MMExtDialogs`, `MMEditorDlg`, `lua51`, `FASM.DLL` + `FASM License.TXT`); the editor's own `Data/editor.{bitmaps,icons,sprites}.lod` (**not** game data); `LuaConsole`, per-game paper-doll tables (`Misc/Paper Doll/mm{6,7,8}/PaperDol.txt`), `KillObsolete/`. |

Additional loader semantics (`MMExtension.htm`, "General Information"): `Localization/`, `Maps/` and
map-adjacent globals (`<map>.global.lua`) load/unload with the map; unload = removing all of a script's events.

### 1.3 Game data / rules tables it exposes or documents (highest-value section)

Text tables are read from the game's own LODs (`icons.lod` MM6, `events.lod` MM7, `EnglishT.lod` MM8 —
`MMExtension.htm`, "General Information") into memory and wrapped with a struct each. Column meaning, per-field
comments (including engine formulas) live in `Scripts/Structs/01 common structs.lua` and are rendered into the doc.

**Rules/content tables (in-memory `Game.*`, declared in `Scripts/Structs/00 structs.lua`)**

| Table | Declared | Struct / fields | Defines |
|---|---|---|---|
| `Game.ItemsTxt` | `00 structs.lua:301` | `ItemsTxtItem` `01:1653` | Items: picture, names, value, equip stat/skill, dice mods, material (normal/artifact/relic/special), chance-by-level, sprite, equip offsets, `Bonus`/`Bonus2`/`BonusStrength`. |
| `Game.StdItemsTxt`, `Game.SpcItemsTxt` | `00:302`, `00:303` | `StdItemsTxtItem` `01:1710`, `SpcItemsTxtItem` `01:1727` | Item-bonus suffixes: standard ("of Might": bonus stat + per-slot chance Arm…Amul) and special/artifact (12 slot chances, value, level). |
| `Game.ScrollTxt`, `Game.PotionTxt` | `00:304`, `00:308` | pchar/os arrays | Scroll spell ids and potion-mix outcome tables. |
| `Game.MonstersTxt`, `Game.PlaceMonTxt` | `00:314-320` (per-game array bases) | `MonstersTxtItem` `01:1376` | Monster stats/behaviour: level, HP, AC, exp, treasure, quest flag, fly, movement/AI type, `HostileType` (Hst radius 0-4), speed, recovery, preferences, bonuses, two attacks, spell use, all nine resistances; `PlaceMonTxt` (MM7/8) holds per-map placed-monster groups. |
| `Game.MapStats` | `00:323-325` | `MapStatsItem` `01:1605` | Map stats: name/file, monster pictures, refill/reset/alert days, steal permission, perception, `Lock`/`Trap` values + **disarm formulas**, treasure level (`Tres`), encounter chances, monster difficulty bands (Mon1-3 Dif/Low/Hi), redbook track, EAX environments. |
| `Game.Spells` | `00:569` | `SpellInfo` (`01`, between `SpcItemsTxtItem` and `SpellsTxtItem`) | Runtime spell data: spell points and delay per mastery, damage add/dice, flags `CastByMonster`/`CastByEvent`/`CauseDamage`/`SpecialDamage` (the "M/E/C/X" letters of Spells.txt). |
| `Game.SpellsTxt`, `Game.SpellSounds` | `00:570`, `00:571` | `SpellsTxtItem` `01:1796` | Spell names, short names, descriptions, school/level metadata; spell id → Sounds.txt id (`30000 + n*100`). |
| `Game.GlobalTxt`, `Game.Houses` ("2DEvents") | `00:529`, `00:530` | pchar array, `Events2DItem` `01:1821` | Global UI/status strings; town building entries (type, picture, owner, enter/exit text, reputation/personality, open/close hours, exit picture/map, quest bit). |
| `Game.HouseMovies`, `Game.TransTxt` | `00:532`, `00:533` | `HouseMovie` `01:1856` | House interiors (video, background, NPC pic, type, sounds) and TFT bitmap-set names. |
| `Game.NPCDataTxt`, `Game.NPCProfTxt`, `Game.NPCNames/News/Group`, `Game.BTB` | `00:545-564` | `NPC` `01:724`, `NPCProfTxtItem` `01:3137`, `NPCNewsItem` `01:771`, `BTB` `01:3158` | Live NPC table (name, picture, profession, group, topics, events, items), professions (join cost, per-house topic skills), name pools, per-map news/rumours, NPC groups, bribe/threat/beg texts (npcBTB.txt + remap table). |
| `Game.QuestsTxt`, `Game.AwardsTxt`, `Game.AutonoteTxt`, `Game.HistoryTxt` | `00:615-630`, `00:547` | pchar arrays, `HistoryTxtItem` `01:779` | Quest log entries, award names, autonote text, party history entries (per-game array sizes/stride). |
| `Game.MerchantTxt`, `Game.HostileTxt` | `00:635`, `00:652` | pchar array, u1 matrix | Merchant-generated item names by shop type; monster-kind hostility matrix (3 variants per kind, `(id+2)/3` grouping). |
| `Game.Classes`, `Game.ClassKinds`, `Game.Races`; `Game.MonsterKinds`, `Game.ExitMapAction`, `Game.TransportLocations/Index`, `Game.TownPortalInfo`, `Game.LloydBeaconSlot`… | `00 structs.lua` | see `MMExtension.htm#structs.GameClasses`, `#structs.GameRaces`, matching structs | Class HP/SP base+factor, SP stat, per-class skill mastery matrix, starting skills, class-kind and race starting stats; monster-kind table, exit-map actions, transport routes, town-portal targets, Lloyd's beacons. |
| Shops: `Game.ShopItems`, `ShopSpecialItems`, `GuildItems`, `ShopWeaponKinds`, `ShopArmorKinds`, `ShopMagicLevels`, `ShopAlchemistLevels`, `TrainingLevels`, `GuildSpellLevels`, `GuildAwards`, `GeneralStoreItemKinds`, refill counters | `00 structs.lua` | see `MMExtension.htm#structs.GameStructure.ShopItems` … | Shop/guild inventory state and the per-shop-type item-kind level tables that drive generation. |

**Map/content binary tables (`Game.*Bin`) — the actual files the engine loads**

`Scripts/Structs/01 common structs.lua`: `SFT`/`SFTItem` `2452`/`2405` (sprite frame table), `DecListItem` `2507`
(decorations: radius, height, light, SFT group, flicker/fire/smoke/sound bits), `PFTItem` `2540`, `IFTItem` `2553`,
`TFTItem` `2569` (tile bitmaps), `DChestItem` `2584` (chest pictures), `OverlayItem` `2592`, `ObjListItem` `2601`
(object list with bit flags and particle colors), `MonListItem` `2643` (monster frame names, radius/velocity, tint,
sounds), `SoundsItem` `2683`, `TileItem` `2712`, `EventLine` `2736`. `DataTables.lua:91-152` maps generated text to
`DataFiles/d<name>.bin` for Class HP SP, Class Skills, Class Starting Skills/Stats, House Movies, SFT, DecList, PFT,
IFT, TFT, Chest, Overlay, ObjList, MonList, Sounds, Tile/Tile2/Tile3 (MM8 only), Monster Kinds, Shops, plus mod handlers.

**Map geometry / event content**

`structs.GameMap` (`01:138`) exposes `Monsters`, `Objects`, `Sprites`, `Chests`, `Notes`, `Models`, `IDList`,
outdoor (`OdmHeader` `01:387`, `TilesetDef` `01:358`, `MapExtra` `01:407`, `Weather` `01:440`, `SpawnPoint` `01:2346`)
and indoor (`BlvHeader` `01:376`, `MapVertex` `2011`, `MapFacet` `2072`, `FacetData` `2139`, `MapRoom` `2249`,
`MapLight` `2314`, `MapDoor` `2365`, `BSPNode` `2039`, `MapOutlines` `2328`) data. `Game.GlobalEvtLines` /
`Game.MapEvtLines` hold the decompiled event scripts; the ~70-command EVT language (0x00-0x45: `Exit`, `EnterHouse`,
`MoveToMap`, `Cmp`, `Add`, `SummonMonsters`, `CastSpell`, `SetNPCTopic`, `CheckSkill`, `CheckMonstersKilled`,
`Jump`, timers, …) is declared in `Scripts/Core/evt.lua:655-1360` with per-command parameter layouts. Original
`.evt`/`.str` binaries can be decompiled with `evt.Decompile` (`Scripts/Core/evtdeco.lua`).

**Rules expressed as Lua APIs over those tables** (not just data): quests, dialog branches, autonotes, awards —
`Scripts/Structs/After/LocalizationAndQuests.lua` (`Quest`, `KillMonstersQuest`, `QuestBranch`, `NPCTopic`,
`Greeting`, `Autonote`/`AddAutonote`/`CheckAutonote`, `vars.Quests`/`QuestAwards`/`QuestAutonotes`); house/NPC/shop
logic — `Scripts/Core/npc.lua` (`SkillToHouseTopic`, `ShopItemsGenerated`, `GuildItemsGenerated`, `TrainPerWeek`);
constants — `Scripts/Core/ConstAndBits.lua` (`const.Class`, `Race`, `Skills`, `Stats`, `Spells`, `ItemType`,
`ItemSlot`, `HouseType`, `MonsterKind/Bits/Bonus/Buff/Pref`, `FacetBits`, `ChestBits`, `Season`, `AIState`,
`MonsterAction`, `Damage`, `Condition`, `PartyBuff`, `PlayerBuff`, …).

### 1.4 Format documentation for original game files

There is no separate format spec document; **the formats are documented in code plus the generated reference**:

- `MMExtension.htm` → "Structures" section (`structs.Lod`/`LodFile`/`LodRecord`/`LodBitmap`/`LodPcx`/`LodSprite`/
  `LodSpriteD3D`/`BitmapsLod`/`SpritesLod`/`LanguageLod`/`Fnt`, `BlvHeader`, `OdmHeader`, `MapFacet`, `MapDoor`,
  `MapModel`, `EventLine`, `SFT`, `DecListItem`, `ObjListItem`, `MonListItem`, `TileItem`, `SoundsItem`,
  `DChestItem`, `OverlayItem`, `PFTItem`, `IFTItem`, `TFTItem`), each listing field names with comments where the
  author knew the semantics.
- Layout source of truth: `Scripts/Structs/00 structs.lua` and `01 common structs.lua` (offsets, sizes, bit flags,
  `mmv(6,7,8)` per-game offsets); e.g. `ObjListItem` carries bit-flag comments (`Invisible`, `NoPickup`, `Bounce`, …).
  Parsing helpers: `Scripts/Structs/After/Text Tables.lua` (`ParseTextTable`, `ParseNamedColTable`,
  `ReadLodTextTable`, …) and `DataTablesSupport.lua` (`ReadWriteTable`, `DataTables.ToBin`).
- The map editor read path doubles as format documentation (`Scripts/Global/Editor Odm Read.lua`, `Editor Odm Data.lua`,
  `Convert Blv.lua`, `Editor BSP.lua`, `Editor Props.lua`, `Editor Read Data.lua`), and `Structs/After/00 Mem Functions.lua`
  (`mem.ExtendGameStructure`, `mem.ExtendAndSaveGameStructure`) documents how save games gain mod-persisted fields.

### 1.5 How rules are expressed; per-game branching

Rules live in **Lua tables + structs bound to live game memory**, not an external data format: MMExtension reads the
original `.txt`/`.bin` files into the engine's own arrays and exposes typed Lua views over them. Editing a rule means
editing the game's text table (or a `Data/Tables/*.txt` override) or mutating the in-memory struct from a script;
behavior hard-coded in the exe is changed by hooking (`events.*`) or byte patching (`mem.*`).

Per-game differences are pervasive and explicit: ~140 uses of the `mm78(a, b)` helper and dozens of
`mmver == 6 / == 7 / == 8 / > 6` branches across `Scripts/Structs/**` (e.g. `MapStatsItem` gains `AlertDays`,
`StealPerm`, `Per` only for MM7/8; `SpellInfo` has 3 masteries in MM6 and 4 + damage fields in MM7/8; separate
MM6/7/8 array bases and counts; MM8-only `Tile2`/`Tile3` tables and `InvisibleAsDead` kill-quest semantics;
`Structs/After/MM8 Blaster.lua`). The `Scripts/Structs/After/Remove*Limits.lua` family (quests, autonotes, awards,
shops, NPC professions, map stats, indoor/outdoor, party) shows which hard limits each game imposes.

### 1.6 Licensing / attribution constraints

- **No project-level LICENSE file and no SPDX headers** — only third-party notices (`Src/MMExtension/LuaJIT/COPYRIGHT`,
  `Src/MMExtension/luasocket/LICENSE`, `ExeMods/MMExtension/FASM License.TXT`). Per-file headers claim MIT for some
  modules (`Scripts/Modules/RSTemplates.lua:3`, `RSParse.lua:3`, `Scripts/Core/RSParseLua.lua:3`, `RSNoGlobals.lua:3`,
  `ShortFunctions.lua:3`, `RSPreprocessHook.lua:3`), others only "(c) Sergey Rozhenko" (`Scripts/Core/RSMem.lua:4`,
  `RSPersist.lua:4`). **The license of MMExtension as a whole is unstated [uncertain] — treat it as all-rights-reserved
  until the author clarifies; MIT-licensed files can be reused with attribution, the rest re-implemented or licensed.**
- **It does not redistribute original game data.** No game `.txt`/`.bin` tables, no `.lod`/`.odm`/`.blv`, no extracted
  art or audio are tracked (`git ls-files` shows only GrayFace's own `Data/editor.*.lod`, docs, sources, prebuilt
  DLLs); generated tables (`Data/Tables/*.txt`, `DataFiles/d*.bin`) come from the user's own installation at runtime
  (`Scripts/General/DataTables.lua`). A downstream kit must follow the same rule: ship loaders and structure
  definitions, never the tables. Attribution practice in-tree is to credit author-supplied knowledge inline
  (e.g. the disarm formulas at `01 common structs.lua:1624`) and to point at external tools (`MMArchive`,
  `mm8leveleditor`, `TxtEdit`) rather than bundling them.

---

## 2. OpenMM8

### 2.1 What it actually is

A **from-scratch Unity reimplementation of Might & Magic VIII**, not a patch or a mod (`README.md`: "This project is
a Unity reimplementation of original Might & Magic 8 RPG … Unity 2018.1.1f1 Personal is used"). C# — 213 files,
~32.7k lines — on Unity 2018.1.1f1 (`ProjectSettings/ProjectVersion.txt`), Windows-oriented
(`Assets/StreamingAssets/build_info.txt`). **No `LICENSE` file exists anywhere in the tree
[uncertain → default all-rights-reserved].** Maturity is early and the project is dormant: 152 commits, last
`59937a3` (2021-01-14) "Leftover changes"; README claims only map loading, partial GUI (minimap/compass), NPC
sprite rotation and villager AI as working.

### 2.2 Repo layout and subsystem decomposition

| Path | Owns |
|---|---|
| `Assets/OpenMM8/Scripts/Data/` | Content loading: `Databases/*.cs` (30 typed DBs), `DataHolders/*.cs` (row DTOs), `Databases/Util/CsvDataLoader.cs`, `Databases/DbMgr.cs`. |
| `Assets/OpenMM8/Scripts/Gameplay/` | `Game/` (`GameCore`, `GameMechanics`, `InitMgr`, `GlobalEvents`, `ConsoleCommands`), `Game/Player/` (Character, CharacterStats, Inventory, PlayerParty, Skill), `Game/Items/` (Item, ItemGenerator, ItemEnchant), `Game/Spells/`, `Game/GameEvents/`, `Game/Time/`, `DamageAndEffects/`, `Quest/`, `Loot/`, `UI/` (`UiMgr`, `UIDataHolders/*`, `UIState/*`), `Components/*`. |
| `Assets/OpenMM8/Scripts/AI/` | `NPC/Monster.cs`, `MonsterAI.cs`, `MonsterEnums.cs`, `NPC/Legacy/{BaseNpc,CombatNpc,VillagerNpc}.cs`. |
| `Assets/OpenMM8/Scripts/Sprites/`, `Triggers/`, `UI/`, `Util/`, `Unity/` | Billboard sprite system (`SpriteRegistry`, `SpriteBillboardAnimator`, `SpriteRotator`, `CameraFacingBillboard`), trigger dispatcher, minimap/compass/aspect UI, helpers, vendored Unity standard assets. |
| `Assets/OpenMM8/Resources/` | `Data/` (32 text tables), `Sprites/`, `Monsters/`, `Player/`, `Sounds/`, `Music/`, `Buildings/`, `UI/`, `Prefabs/`. |
| `Assets/OpenMM8/Maps/Outdoor/Dagger_Wound_Island/` | Pre-exported map: `.obj` + `.mtl` + `.textures/*.bmp` + materials. |
| `Assets/{DaggerWoundIsland.unity, DaggerWoundIsland/, Test/, Editor/}` | Scene (~19 MB), baked NavMesh, sandbox art, editor tools (`ObjExporter`, `NpcEditor`, `TriggerEditor`, `FaceIndexViewer`, `Tests/InventoryTests.cs`). |
| `Packages/`, `Assets/3dParty/`, `Misc/` | LINQtoCSV, MSTest, Unity-Logs-Viewer, codeandweb sprite packer, "Advanced INI Parser"; `MM8_CHAR_SOUNDS_EXPRESSIONS.xlsx`, `PlayerFrames_Deserialized.txt`. |

### 2.3 What it reuses from OpenEnroth / MMExtension

**No shared code and no citations were found.** Grepping the whole tree for `openenroth`, `mmextension` and
`grayface` returns nothing; `enroth` matches only inside bundled game text (`Resources/Data/{ITEMS,NPC_TOPIC,
NPC_TOPIC_TEXT,NPC_GREET}.txt`). There is no `.odm`/`.blv`/`.lod` reader at runtime — maps reach Unity through an
offline export pipeline (`Assets/Editor/ObjExporter.cs` + external M&M tooling) — so MMExtension's binary-format
knowledge is **not** reused here.

Where it *does* overlap with MMExtension is subject matter, and the overlap is instructive for a ruleset:
`Resources/Data/*` are text dumps of the same MM8 tables MMExtension exposes in memory (items, monsters, spells,
classes, NPCs, quests, sounds, frame tables, monster relations, item generation chances) — but serialization is
transposed/renamed in places (`RACE_STARTING_STATS.txt` puts stats in columns while MMExtension writes them as rows;
`CLASS_HP_SP.txt` lacks MMExtension's class header), so **these look like MM8LevelEditor-era dumps rather than
MMExtension `Data\Tables` output; provenance is not recorded in the repo [uncertain].** `GameMechanics.cs` restates
engine rules MMExtension also documents (resistance "1 − 30/(30+Res+Luck), halve per successful dice" at `:71-88`,
`GetAttributeEffect` thresholds at `:113+`, item generation at `ItemGenerator.cs:30-46` "taken from the original
M&M games") — **no attribution or license basis is stated for that transcribed knowledge.**

### 2.4 MM8-specific behavior it handles (paths)

- **GLOBAL.EVT / talk topics.** `Gameplay/Game/GameEvents/TalkEventMgr.cs:469-2413` (`ProcessTopicClickEvent`)
  reimplements MM8's `GLOBAL.EVT` (comment: "In MM8 this script had ~8000 lines") — quest-bit checks/sets, topic
  availability (`CanShowTopic`-style predicates around `:440-467`) and awards.
- **Per-map event scripts.** `Gameplay/Game/GameEvents/MapEventProcessors/EP_DaggerWoundIsland.cs` ports `out01.odm`
  events: house-enter events mapping to building ids (11→224, 13→225, …) and a timed trigger ("S'ton" at 10 AM on
  day 1 → event 500); base class `MapEventProcessor.cs`; `EventAPI.cs` mirrors EVT commands (`EnterHouse`,
  `TalkWithNPC`, `TalkNPCNews`, `AddAward`, `GetClass`/`SetClass`) — several still stubbed (`HasAward` → `false`).
- **Party size 5** (MM8): `Gameplay/Game/Player/PlayerParty.cs:182,430` ("Already 5 characters in party").
- **Monster kinds as triples** and the hostility matrix: `Data/Databases/MonsterRelationDb.cs` documents "Monster
  have 3 types, indexes for monster start from id 1", row 0 = player relations; `HostilityType` (0 Friendly …
  4 HostileLong) and `MonsterAggresivityType` (`Wimp/Normal/Agressive/Suicidal`) in `AI/NPC/MonsterEnums.cs:93-120`
  match the `Hst`/`AI Type` columns of MM8 `MONSTERS.txt`. Also MM8-shaped: 8-direction billboard NPCs (`README.md`,
  `Scripts/Sprites/*`), `MonsterBuffType`/`SpecialAbilityType`/`AttackPreferenceMask`, Blaster skill and spell
  (`GameMechanics.cs:255,465`, `AI/NPC/Monster.cs:971`), and the MM8 enumerations in `Game/Common.cs` (`SkillType`,
  `CharacterClass`, `CharacterRace`, `SpellType`, `ItemType`, `EquipSlot`, `ItemSkillGroup`, `SpecialEnchantType`).

### 2.5 Licensing / attribution constraints (OpenMM8)

- **No license file, no per-file headers [uncertain].** Assume all-rights-reserved: useful as *documentation of
  one person's understanding of MM8*, not as a source of copyable code or data.
- **It does redistribute converted original-game content**: 32 data tables under `Assets/OpenMM8/Resources/Data/`
  (e.g. `ITEMS.txt` retains the original MM8 header "1-199 equipable items, 200- potions, …"), 5,677 `.wav` files
  (mostly `Resources/Sounds`, plus `Player/…/Sounds`, `Buildings/Sounds`), map textures `.bmp` under
  `Assets/OpenMM8/Maps/Outdoor/Dagger_Wound_Island/Dagger_Wound_Island_Map.textures/`, `.ogv` videos and `.mp3`
  music. The README claim "No third-party assets are currently used" contradicts the tree; a downstream repo must
  **not** copy any of it. Minor: `Assets/InitializeOnLoad.cs` is entirely commented out (65 lines, including an
  unused registry-Run-key/temp-file payload) — dead code, not executed behavior.

---

## 3. What each donor is worth to a kit + MM6/7/8 ruleset

- **MMExtension**: the authoritative *game knowledge* donor. Take the table inventory and field semantics (§1.3), the
  binary formats (§1.4) and the per-game divergence list (§1.5) as the ruleset's requirements, plus the layering idea
  (structures/tables/constants vs. behavioural rules vs. content scripts) — re-implemented in the target language;
  cite the file, never copy game data.
- **OpenMM8**: the *reimplementation-shaped* donor. Take its subsystem decomposition (§2.2) as a sanity check for kit
  module boundaries, its formula transcriptions (`GameMechanics.cs`, `DamageCalculator.cs`, `ItemGenerator.cs`) as
  candidate rules to verify against MMExtension's documented in-engine formulas, its tab-separated loader shape
  (`CsvDataLoader` + typed DBs) as a content-loader precedent, and its `GLOBAL.EVT`/map-event port as a measure of
  how much per-map scripting a ruleset must absorb. Verify every formula independently; treat code and data as
  unlicensed.

**Open items before building:** MMExtension's overall license; the exact GrayFace patch builds each hook table
targets; whether the OpenMM8 tables may serve as a *comparison oracle* at all (only the formats are safe to study).
