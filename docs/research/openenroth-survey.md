# OpenEnroth survey — donor map for a new MM6/7/8-family engine

Source: `/home/research/old-games/OpenEnroth`, read-only inspection.
Snapshot: `master` @ `aee554e4aeb63d57ae73930f212eb44f2adb05a8` (2026-09-21), 7748 commits, first commit 2016-12-05.
Method: file listing, reads and greps only. No builds, no tests. All paths below are repo-relative.

## 1. What the project is

| Fact | Evidence |
| --- | --- |
| Open-source reimplementation of the Might & Magic VI–VIII engine, runs on original game data | `README.md:8-9` |
| **Only MM7 is playable**; "MM6 and MM8 support is planned" | `README.md:11-12` |
| Language: C++23 (`CMAKE_CXX_STANDARD 23`), no namespaces by policy | `CMakeLists.txt:7`; `HACKING.md:119` |
| Build: CMake ≥ 3.27, one `CMakeLists.txt` per folder, prebuilt deps or vcpkg | `CMakeLists.txt:1,29,34`; `HACKING.md:6-23,147` |
| Min compilers: VS 2022 17.10, GCC 15, AppleClang 16 | `HACKING.md:15-18` |
| License: **LGPL-3.0** | `LICENSE:1-2` |
| Platforms: Windows, Linux, macOS, Android (experimental) | `README.md:14,119-121` |
| Third-party deps vendored as git submodules (SDL, LuaJIT/sol2, imgui, fmt, spdlog, glm, googletest, …) | `.gitmodules` (26 entries) |

Rough size at this revision: `src/` = 389 `.cpp` + 496 `.h` = **137,541 lines**; `test/` = **8,580 lines**; working tree 328 MB including `thirdparty/` and `.git/`.
Test surface: **365 game tests** (`test/Bin/GameTest/GameTests_*.cpp`, 345 `Issues` + 20 `Prs`) and **424 unit tests** across `src/**/Tests` and `test/`.

Maturity: **plays MM7 end to end** — the shipped flow is FSM-driven from logo/intro videos through main menu, party
creation, world, combat, books, shops, save/load and a game-over/winner screen (`src/Application/GameStates/GameFsmBuilder.cpp`,
`src/Application/GameMenu.cpp`, `src/Application/GameOver.{h,cpp}`), with 365 recorded-trace game tests over it.
Nightly builds only, stable v0.1 still pending (`README.md:22-23`). CI runs game tests against MM7 data only —
`OPENENROTH_MM7_PATH: .../OpenEnroth_GameData/mm7` (`build_all.yml:263,271`); there is no MM6/MM8 data in CI.
Residue of the porting effort is visible throughout: `//----- (0x00465D0B) --------` decompilation banners (503 hits),
`// idb` markers, and 183 `@offset 0x…` doxygen tags pointing at the original binary.

## 2. Repository layout

Top level: `src/` (engine), `test/` (unit + game tests + tracedata), `thirdparty/` (submodules), `resources/`
(shaders, images, Lua scripts), `scripts/` (style checkers), `android/`, `CMakeModules/`, `distribution/linux/`
(flatpak), `.github/workflows/`, `HACKING.md`, `README.md`, `Doxyfile`, `.claude/`.

There is **no `docs/` directory and no format-spec Markdown**. All format and rule documentation lives in doxygen
comments next to the parsing code. Only `README.md` and `HACKING.md` exist at top level.

`src/` is layered by dependency (`HACKING.md:136-150`):

| Dir | Owns |
| --- | --- |
| `src/Utility/` | Domain-independent helpers: `Memory/Blob.h`, `Streams`, `String`, `Math`, `IndexedArray.h`, `Flags.h`, `Segment.h`, `SmallVector.h`. Depends only on `thirdparty`. |
| `src/Library/` | Independent, non-cyclic libraries: `Binary/`, `Snapshots/`, `Serialization/`, `FileSystem/`, `Lod/`, `LodFormats/`, `Magic/`, `Vid/`, `Snd/`, `Image/`, `Compression/`, `Config/`, `Logger/`, `Platform/`, `Fsm/`, `Geometry/`, `Color/`, `Random/`, `StackTrace/`, `Environment/`, `Json/`, `Cli/`, `BuildInfo/`. |
| `src/Core/` | OpenEnroth-specific, domain-tied: `Time/` (`Time.h`, `Duration.h`), `Serialization/`, `Trace/`. |
| `src/Engine/` | The game engine proper (317 files) — see §4. |
| `src/GUI/` | `GUIWindow.cpp`, `GUIFont.cpp`, `GUIButton.cpp`, `GUIEnums.h`, `Overlay/`, `UI/` (all screens), `UI/Books/`, `UI/Houses/`. |
| `src/Application/` | Entry flow: `Game.cpp`, `GameMenu.cpp`, `GameOver.cpp`, `GameConfig.cpp`, `GameStates/` (FSM), `Startup/` (path resolution, filesystem, logging). |
| `src/Io/` | SDL-facing input: `Mouse.cpp`, `KeyboardController.cpp`, `KeyboardActionMapping.cpp`, `InputEnums.h`. |
| `src/Media/` | `MediaPlayer.cpp`, `Audio/` (OpenAL backend), `AudioDataSource`. |
| `src/Scripting/` | Lua (sol2) debug console and bindings; scripts in `resources/scripts/` (`HACKING.md:200-203`). |
| `src/Arcomage/` | The Arcomage card minigame (`Arcomage.cpp`, 2939 lines) plus card data (`Arcomage.h:195`). |
| `src/Bin/` | Executables: `OpenEnroth/`, `LodTool/` (LOD archive explorer), `CodeGen/` (enum/table code generator). |

`test/`: `Bin/UnitTest/`, `Bin/GameTest/`, `Bin/RetraceTest/`, `Data/` (455 recorded `.json` traces + `.mm7` saves),
`Testing/Game/` (`TestController`, tape recorders), `Testing/Unit/`, `Testing/Extensions/`.

## 3. Game-data and file-format knowledge

### 3.1 What the original games ship (as this repo sees it)

Required data files are validated at startup — `anims/magic7.vid`, `anims/might7.vid`, `data/bitmaps.lod`,
`data/events.lod`, `data/games.lod`, `data/icons.lod`, `data/sprites.lod`, `sounds/audio.snd`
(`src/Application/Startup/PathResolver.cpp:12-23`; the user-facing minimum is `ANIMS DATA MUSIC SOUNDS`,
`README.md:41`). Note MVII-specific names: `magic7.vid` / `might7.vid`.

| Container | Contents | Where |
| --- | --- | --- |
| `.lod` archive | Named entries; version enum knows `LOD_VERSION_MM6`, `MM6_GAME`, `MM7`, `MM8` | `src/Library/Lod/LodEnums.h:7-12`, `LodReader.h:16-21`, `LodInfo.h:7-11` |
| In-LOD codecs | compressed data / "compressed pseudo-image" / image+palette / sprite / font, sniffed by `lod::detect*` | `src/Library/LodFormats/LodFormats.h:10-14`, `LodFormats.cpp` |
| `.vid` | MM video container | `src/Library/Vid/VidReader.h:12-16` |
| `.snd` | MM sound bank; compression is part of the *container*, unlike LOD | `src/Library/Snd/SndReader.h:16-21` |
| `.pcx`, `.png`, palettes | images | `src/Library/Image/Pcx.cpp`, `Png.cpp`, `Palette.h` |
| `.evt` | per-map + `global.evt` event scripts (bytecode VM) | `src/Engine/Evt/EvtProgram.h:18`, `EvtInstruction.h` |
| `.odm` / `.ddm` | outdoor map and its respawn/visited delta | `src/Engine/Graphics/Outdoor.cpp:451-465`, `src/Engine/Data/MapData.h:12` |
| `.blv` / `.dlv` | indoor map and its delta | `src/Engine/Graphics/Indoor.cpp:266-284` |
| Saves | `saves/*.mm7`, header holds name/location/play time | `src/Engine/SaveLoad.h:14-21`, `SaveLoad.cpp:44` |
| Config/registry | per-game discovery via `OPENENROTH_MM6/7/8_PATH` and GOG/NWC registry keys | `src/Application/Startup/PathResolver.cpp:27-66`, `PathResolver.h:10-12` |

### 3.2 Where the format documentation lives

**The `Snapshots` layer is the single most valuable donor artifact.** It is a set of structs that are byte-exact
images of the original on-disk layouts, verified by `static_assert(sizeof(...) == N)`, deliberately decoupled from
the engine's runtime types (`src/Engine/Snapshots/EntitySnapshots.h:14-20`). It documents ~70 original structs:

- MM7 game structs: `Character_MM7`, `Party_MM7`, `Actor_MM7`, `Item_MM7`, `SpellBuff_MM7`, `BLVFace_MM7`,
  `BLVSector_MM7`, `BLVDoor_MM7`, `BLVLight_MM7`, `BLVHeader_MM7`, `ODMFace_MM7`, `BSPNode_MM7`, `MapWeather_MM7`,
  `LocationInfo_MM7`, `SaveGameHeader_MM7`, `Timer_MM7`, `MonsterInfo_MM7`, `Chest_MM7`, `ActiveOverlay_MM7`,
  `PortraitFrameData_MM7`, `IconFrameData_MM7`, `TextureFrameData_MM7`, `LevelDecoration_MM7`, `SpawnPoint_MM7`,
  `ObjectDesc_MM7`, `BSPModelData_MM7`, `SoundInfo_MM7`, `ChestData_MM7`, `PersistentVariables_MM7`.
- MM6/MM8 variants for diverging structs: `SpriteFrame_MM6` vs `SpriteFrame_MM7`, `MonsterDesc_MM6` vs `_MM7`,
  `SpawnPoint_MM6`, `DecorationData_MM6` vs `_MM7`, `BLVLight_MM6`, `SoundInfo_MM6 : SoundInfo_MM7`, `UIAnimation_MM6`,
  `ObjectDesc_MM6`.
  Full list: `grep -n '^struct' src/Engine/Snapshots/EntitySnapshots.h`.
- Companion snapshot layers: `src/Engine/Snapshots/TableSerialization.{h,cpp}` (table file layouts),
  `CompositeSnapshots.{h,cpp}` (whole-map / whole-save images), `EnumSnapshots.{h,cpp}` (enum value round-trip),
  `src/Library/Lod/LodSnapshots.h`, `src/Library/LodFormats/LodFormatSnapshots.h`, `src/Library/Vid/VidSnapshots.h`,
  `src/Library/Snd/SndSnapshots.h`.
- Mechanism: `serialize` / `deserialize` / `snapshot` / `reconstruct` with tags `tags::via<T>` (read `T` then
  convert), `tags::cast<From,To>`, `tags::segment<First,Last>`, `tags::reverseBits` (MM7 stores bitsets in inverted
  bit order), `tags::unsized`/`presized`/`append`/`nullTerminated`/`context`
  (`src/Library/Snapshots/SnapshotTags.h:28-91`, `src/Library/Binary/BinaryTags.h:36-97`).
- Text encodings are explicit at call sites, e.g. `deserialize(headerBlob, &header, tags::via<SaveGameHeader_MM7>,
  tags::encoding(ENCODING_ASCII))` (`SnapshotTags.h:37`).

**Rule tables live in `data/events.lod` as tab-separated text**, loaded in one block in `Engine::SecondaryInitialization`
(`src/Engine/Engine.cpp:725-781`) via `ResourceManager::eventsData()` (`src/Engine/Resources/ResourceManager.h:9-21`).
Each loader documents its column layout inline:

| File | Holds | Structure doc |
| --- | --- | --- |
| `MapStats.txt` | per-map name, file name, respawn interval, alert days, stealing fine, perception/disarm difficulty, trap dice, treasure level, encounter tables, music, EAX env | `src/Engine/Tables/MapTable.cpp:19`, `src/Engine/Data/MapData.h` |
| `monsters.txt` | monster combat stats, attacks, spells, resistances, AI type, movement, treasure | `src/Engine/Objects/Monsters.cpp:255`, `src/Engine/Objects/Monsters.h:25-90` |
| `placemon.txt` | placement/map monster names | `src/Engine/Objects/Monsters.cpp:244` |
| `spells.txt` | spell school, name, per-mastery descriptions | `src/Engine/Spells/Spells.cpp:499`, `Spells.h:24-33` |
| `items.txt` | item icon/name/value/type/skill/damage/material per enchant level | `src/Engine/Tables/ItemTable.cpp:89` |
| `rnditems.txt`, `stditems.txt`, `spcitems.txt` | random-item and standard/special enchantment generation tables | `ItemTable.cpp:58,33` |
| `potion.txt`, `potnotes.txt` | potions and mixing matrix | `ItemTable.cpp:253` |
| `2dEvents.txt` | 24 columns of building/house data (type, name, proprietor, prices, hours, exit map, quest bit) | `src/Engine/Tables/HouseTable.cpp:20-45` |
| `hostile.txt` | monster-group hostility matrix | `src/Engine/Tables/HostilityTable.cpp:15` |
| `quests.txt` | quest bit → text + notes | `src/Engine/Tables/QuestTable.cpp:15` |
| `autonote.txt`, `awards.txt`, `history.txt`, `scroll.txt`, `trans.txt`, `merchant.txt` | autonotes, awards, journal history, message scrolls, map transitions, shop phrases | `AutonoteTable.cpp:19`, `AwardTable.cpp:15`, `HistoryTable.cpp:16`, `MessageScrollTable.cpp:15`, `TransitionTable.cpp:16`, `MerchantTable.cpp:18` |
| `npcdata/npctopic/npcgreet/npcnews/npcnames/npcprof/npcdist/npcgroup/npctext.txt` | NPC roster, dialogue trees, greetings, news, names, professions, distribution | `src/Engine/Tables/NPCTable.cpp:28-143` |
| `global.txt`, `class.txt`, `skilldes.txt`, `stats.txt`, `credits.txt` | localization strings and class/skill/attribute text | `src/Engine/Localization.cpp:47-49,328-329,386-387,462-463`; `src/GUI/UI/UICredits.cpp:23` |
| `global.evt` | global event script | `src/Engine/Engine.cpp:780` |

**Rule enums are generated from that data by `src/Bin/CodeGen`**, which is why the headers say "DON'T EDIT, THIS IS
AUTOGENERATED CODE": `runItemIdCodeGen`, `runMapIdCodeGen`, `runHouseIdCodeGen`, `runMonsterIdCodeGen`,
`runMonsterTypeCodeGen`, `runBountyHuntCodeGen`, `runMusicCodeGen`, `runDecorationCodeGen`,
`runSpeechPortraitsCodeGen`, `runLstrIdCodeGen` (`src/Bin/CodeGen/CodeGenOptions.h:6-18`; markers in
`src/Engine/MapEnums.h:35-37`, `src/Engine/Data/HouseEnums.h:8-10`, `src/Engine/Objects/ItemEnums.h:150-152`,
`src/Engine/Objects/MonsterEnums.h:10-12`, `src/Engine/Data/DecorationEnums.h:10`).
Generated sizes at this revision: 92 `MapId`, 592 `HouseId`, 985 `ItemId`, 423 `MonsterId`, 113 `SpellId`,
47 `Skill`, 66 `EvtOpcode`.

Hardcoded (not data-driven) rule constants worth knowing: spell points per mastery (`spellCountForMastery`,
`src/Engine/Objects/CharacterEnumFunctions.h:33-45`), the 36-class table (`src/Engine/Objects/CharacterEnums.h:260-300`),
23 buffs (`CharacterEnums.h:43-72`), 19 conditions (`CharacterEnums.h:5-25`), monster AI types
(`src/Engine/Objects/MonsterEnums.h:505-509`), and the calendar constants (28-day months, 4-week months, 12 months,
epoch year 1168 "since the Silence", 1 game second = N real seconds) in `src/Core/Time/Time.h:5-50` and
`Duration.h`.

## 4. Engine subsystem decomposition (the names this codebase uses)

| Subsystem | Where it lives / what it does |
| --- | --- |
| Party | `src/Engine/Party.{h,cpp}`, `PartyEnums.h`, `PartyPlacement.{h,cpp}` — 4 characters, gold/food, reputation, fame, hirelings, action queue, rest/heal, per-house and per-event timers (`PartyTimeStruct`, `Party.h:38-46`). |
| Characters | `src/Engine/Objects/Character.{h,cpp}` (6834 lines — the largest file), `CharacterEnums.h`, `CharacterEnumFunctions.{h,cpp}`, `CharacterConditions.h`, `Conditions.{h,cpp}` — stats, HP/SP, races, classes, skills, mastery, conditions, level-ups, recovery times, attack rolls. |
| Skills | `CharacterEnums.h` (`enum class Skill`, 47 values), `CharacterEnumFunctions.h` (`allSkills`, skill<->mastery rules), `src/Engine/Objects/CombinedSkillValue.{h,cpp}` (skill + mastery as one value). |
| Spells & buffs | `src/Engine/Spells/Spells.{h,cpp}` (spell table + book/school logic), `SpellEnums.h`, `SpellBuff.h`, `CastSpellInfo.{h,cpp}` (3217 lines of per-spell effects), `src/Engine/SpellFxRenderer.cpp` (VFX). |
| Combat — real-time | Attack timers expressed as `Duration` recovery times: `Character::GetAttackRecoveryTime(bool attackUsesBow)` (`Character.h:169`), `MonsterInfo::recoveryTime` (`Monsters.h:80`), AOE impact registry `src/Engine/AttackList.{h,cpp}`. |
| Combat — turn-based | `src/Engine/TurnEngine/TurnEngine.{h,cpp}` + `TurnEngineEnums.h`: initiative-sorted `TurnBased_QueueElem` queue, `TurnEngineStep` (`TE_WAIT` monsters / `TE_ATTACK` party / `TE_MOVEMENT`), AI action kinds; `src/Engine/Graphics/TurnBasedOverlay.cpp` draws the turn UI; toggled by `pParty->bTurnBasedModeOn` (`src/Application/Game.cpp:1542-1581`). |
| AI / monsters | `src/Engine/Objects/Actor.{h,cpp}` (4557 lines: `AIDirection`, `ActorJob`, per-actor state machine, movement, targeting), `Monsters.{h,cpp}` + `MonsterEnums.h` (stats table, `MonsterAiType` suicide/wimp/normal/aggressive, special attacks/abilities, projectiles), `AttackList.{h,cpp}`, `SpawnPoint.h`. |
| Inventory / items | `src/Engine/Objects/Inventory.{h,cpp}`, `CharacterInventory`, `Item.{h,cpp}` (783 lines), `ItemEnums.h` (1131 lines), `ItemEnchantment.h`, `CombinedSkillValue.h`; generation tables in `src/Engine/Tables/ItemTable.{h,cpp}`; UI grid `src/GUI/UI/ItemGrid.{h,cpp}`, `UIInventory.{h,cpp}`, `UICharacter.{h,cpp}`. |
| Chests / decorations / sprites | `Objects/Chest.{h,cpp}`, `Objects/Decoration.{h,cpp}`, `Objects/SpriteObject.{h,cpp}`, `Objects/ObjectList.{h,cpp}`, `Objects/TalkAnimation.{h,cpp}`, `src/Engine/Data/{ChestData,DecorationData,OverlayData,TileData,TextureFrameData,IconFrameData,PortraitFrameData}.h`. |
| Maps — outdoor | `src/Engine/Graphics/Outdoor.{h,cpp}` (1863 lines) + `OutdoorTerrain.{h,cpp}` (128×128 grid, 512 world units per cell, `gridToWorld`/`worldToGrid` at `OutdoorTerrain.h:17-45`), `Weather.{h,cpp}`, `MapWeather.h`. |
| Maps — indoor | `src/Engine/Graphics/Indoor.{h,cpp}` (1930 lines) — `BLVFace`, `BLVSector`, `BLVDoor`, `BLVLight`, `BLVMapOutline`, `BSPNode`, `IndoorLocation` (`Indoor.h:24-260`); portal/BSP rendering in `BspRenderer.cpp`, `PortalFunctions.cpp`, `BSPModel.h`. |
| Outdoor↔indoor transition | Both locations load from `data/games.lod` by swapping the extension of the MapTable file name (`.odm`/`.ddm`, `.blv`/`.dlv`) — `Outdoor.cpp:451-465`, `Indoor.cpp:266-284`; per-map transient state in `src/Engine/LocationInfo.h`; travel links in `src/Engine/Tables/TransitionTable.{h,cpp}`; UI in `src/GUI/UI/UITransition.{h,cpp}`. |
| Dialogue / NPC | `src/Engine/Objects/NPC.{h,cpp}` + `NPCEnums.h`, `src/Engine/Tables/NPCTable.{h,cpp}` (9 npc*.txt tables), `src/GUI/UI/NPCTopics.{h,cpp}` (813 lines), `UIDialogue.{h,cpp}`, `UIBranchlessDialogue.{h,cpp}`, `UIMessageScroll.{h,cpp}`. |
| Quests / journal | `src/Engine/Tables/QuestTable.{h,cpp}` + `AwardTable`, `AutonoteTable`, `HistoryTable`; UI in `src/GUI/UI/Books/` — `QuestBook`, `JournalBook`, `AutonotesBook`, `MapBook`, `CalendarBook`, `LloydsBook`, `TownPortalBook`. |
| Shops / training / services | `src/GUI/UI/Houses/` — `Shops`, `Temple`, `Tavern`, `Bank`, `Training`, `TownHall`, `MagicGuild`, `MercenaryGuild`, `Transport`, `Jail`; house types and schedule in `src/Engine/Data/HouseEnums.h`, `HouseData.h`, `src/Engine/Tables/HouseTable.cpp`, `MerchantTable.{h,cpp}`. |
| Time / calendar | `src/Core/Time/{Time.h,Duration.h}` (epoch "since the Silence", `CivilTime` year/month/week/day/hour), `src/Engine/Timer.{h,cpp}` (frame `dt`, pause, turn-based mode); per-map/shop/guild refresh times in `PartyTimeStruct` (`Party.h:38-46`). |
| Save / load | `src/Engine/SaveLoad.{h,cpp}` (`SaveGame` = header + `Party` + timers + overlays + 501 NPC records + 51 NPC groups + map deltas + thumbnail), snapshotting via `Engine/Snapshots/CompositeSnapshots` and `Library/Snapshots/SnapshotSerialization.h`; UI `src/GUI/UI/UISaveLoad.{h,cpp}`. |
| Audio | `src/Media/Audio/` — `AudioPlayer`, `SoundList`, `AudioSamplePool`, OpenAL backend (`OpenALSoundProvider`, `OpenALSample16`, `OpenALTrack16`); `src/Library/Snd/SndReader` for the `.snd` bank; sound ids in `src/Engine/Data/SoundEnums.h`. |
| Video | `src/Library/Vid/VidReader.{h,cpp}` + `VidSnapshots.h`, playback via `src/Media/MediaPlayer.{h,cpp}` / `Movie.h` (FFmpeg sources in `src/Media/FFmpeg*.cpp`), driven by `src/Application/GameStates/VideoState.{h,cpp}`. |
| Assets / LOD runtime | `src/Engine/Resources/{LOD,ResourceManager,LodSpriteCache,LodTextureCache,EngineFileSystem}` and `src/Engine/AssetsManager.{h,cpp}`. Five LODs are held open: `pGames_LOD` (`LOD.cpp:10`), `pIcons_LOD`/`pBitmaps_LOD`/`pSprites_LOD` (`Engine.cpp:639-645`), plus `events.lod` behind `ResourceManager` (`ResourceManager.cpp:12`). |
| Rendering | `src/Engine/Graphics/Renderer/` — `Renderer`, `BaseRenderer`, `OpenGLRenderer`, `NullRenderer` (headless tests), `OpenGLShader*`, `RendererFactory`; shaders in `resources/shaders/`. |
| UI framework | `src/GUI/GUIWindow.cpp`, `GUIButton`, `GUIFont`, `GUIEnums.h`, `GUIMessageQueue`; screens under `src/GUI/UI/` (game, main menu, party creation, rest, spellbook, popups, status bar); scriptable overlay system `src/GUI/Overlay/`. |
| Debug console / modding | Lua entry point `resources/scripts/init.lua`, console `resources/scripts/dev/console_overlay.lua`; bindings `src/Scripting/*Bindings.{h,cpp}`; dev cheats in `resources/scripts/dev/cheat_commands_list.lua`. |
| Determinism / replay | `src/Engine/Components/Deterministic/EngineDeterministicComponent`, `Components/Random/TracingRandomEngine`, `Components/Trace/EngineTrace{Recorder,Player}`, `Components/Control/EngineController`; game tests are recorded input traces in `test/Data/`. |

## 5. MM6 vs MM7 vs MM8 differences the code explicitly handles

- **LOD container versions** are a first-class enum: `LOD_VERSION_MM6`, `LOD_VERSION_MM6_GAME`, `LOD_VERSION_MM7`,
  `LOD_VERSION_MM8` (`src/Library/Lod/LodEnums.h:7-12`).
- **Struct-level divergence** is modelled with `_MM6` / `_MM7` snapshot variants rather than runtime branches —
  `SpriteFrame_MM6` (56 bytes) vs `SpriteFrame_MM7` (60 bytes) with the comment "TODO(captainurist): 2 bytes in MM6?"
  (`src/Engine/Snapshots/EntitySnapshots.h:135-158`); `MonsterDesc_MM6` vs `_MM7` (715, 731); `SpawnPoint_MM6` vs
  `_MM7` (935, 945); `DecorationData_MM6` vs `_MM7` (1005, 1020); `BLVLight_MM6` (1048); `SoundInfo_MM6` (1260);
  `ObjectDesc_MM6` (1168); `UIAnimation_MM6` (638).
- **MM6-only data**: `EVENT_LocationName` ("Only in MM6 data", `src/Engine/Evt/EvtInstruction.cpp:925`),
  `EVENT_SetSnow` ("likely present in MM6", `EvtInstruction.cpp:957`), `HouseType` entries `Element Guild` and
  `Mercenary Guild` marked "This is MM6 only" (`src/Engine/Tables/HouseTable.cpp:67,70`), `mapstats.txt` designer
  column "set only in mm6" (`MapTable.cpp:20-21`), and `MapData::alertDays` "Unused, always 7" only in the MM7 data
  read (`src/Engine/Data/MapData.h:19`).
- **MM7-only data**: many `TileEnums.h` tile variants say "Doesn't exist in MM7 data"
  (`src/Engine/Data/TileEnums.h:101-176`), i.e. they belong to MM6/MM8 tilesets; `OutdoorLocation` and house exit
  picture/map fields are annotated "not used in MM7" (`HouseTable.cpp:20-43`).
- **MM8 divergence**: event-state↔event-id remapping differs — the comment at `src/Engine/Evt/EvtInterpreter.cpp:502-509`
  records that MM6/MM7 `EVENT_ChangeEvent` operands fit `[383,443]` while MM8 `global.evt` uses ids 268-289 and
  531-570 with a piecewise map "read out of the PS2 SLPS_250.31 binary at 0xa33e0 and 0xa3418, the PC MM8.exe is
  presumed to match", with a TODO to make the mapping per-game. Weather flags: "MM7 has only one weather flag, but
  apparently we have more in MM8" (`src/Engine/MapEnums.h:9-13`), citing the MMExtension Lua structs as the source.
- **Version-specific dead code is flagged, not deleted**: `#mm6` TODO tags (5 occurrences) mark MM6 legacy remnants,
  e.g. `src/GUI/UI/Houses/MercenaryGuild.cpp:58` ("this is MM6 legacy, and this decompiled code doesn't look sane")
  and `src/GUI/GUIWindow.cpp:806,818` ("#mm6 remnant, this in mm7 would be a dialogue text for Thieves to Rogues promotion").
- **Lookup tuples diverge per game but the code path is MM7**: path resolution has `resolveMm6Paths` /
  `resolveMm7Paths` / `resolveMm8Paths` and three override env vars, but only `resolveMm7Paths` is called
  (`src/Application/Startup/PathResolver.cpp:112-121`; `src/Application/Startup/GameStarter.cpp:233`).

## 6. Reliance on decompiled binaries and data tables

This is a **decompilation-derived reimplementation, not a clean-room one**, and it says so:

- Almost every function carries its original address, either as a doxygen `@offset 0x…` tag (183 occurrences across
  `src/Engine`, `src/GUI`, `src/Library`) or as a legacy `//----- (0x00465D0B) --------` banner (503 occurrences).
  `HACKING.md:79` instructs contributors to *preserve* those offsets and to convert them into `@offset` tags.
- Decompiler artifacts are visible in identifiers and comments: `// idb` markers (e.g. `src/Engine/mm7_data.h:70`,
  `src/Engine/Party.h:392`), names like `stru262_TurnBased` (`TurnEngine.h:22`), `stru319` (`Actor.h:32`),
  `dword_6BE364_game_settings_1` (`Engine.cpp`), `field_2C`/`field_3D` struct members (`MapData.h:22,39-41`).
- `src/Engine/mm7_data.{h,cpp}` (2300 lines) is a straight port of the original binary's global data: named offsets
  (`// 004E5E50`), tables like `pNPCPortraits_x/y`, `pHouse_ExitPictures`, `byte_4ECF08`, `speechVariants`.
- `src/Engine/Objects/Party.h:43-45` cites "Silvo's binary" for `CounterEventValues`/`HistoryEventTimes`/`_s_times`
  offsets (`0xACD314h`, `0xACD364h`, `0xACD44Ch`).
- `src/Engine/Evt/EvtInterpreter.cpp:504-508` cites the PS2 executable `SLPS_250.31` for MM8 semantics.
- Accuracy guidance for contributors:
  - `HACKING.md:221-225` ("Additional Resources"): points to old event-decompiler and IDB files hosted externally,
    and to the `OpenEnroth_PreHistory` repo which "carries the unsorted `mm7_*.cpp` decompilation dumps, so it's mostly
    useful for tracing where a piece of code originally came from".
  - `.claude/CLAUDE.md:13`: "*NEVER* claim anything about game data or runtime behaviour that you haven't checked.
    OpenEnroth targets MM6, MM7 and MM8, so a claim about the data means all three were scanned."
  - `HACKING.md:127-131`: use `assert` for invariants, exceptions for non-recoverable errors, `Logger` for warnings.
  - Determinism is enforced: game tests compare the RNG state each frame and fail with
    `Random state desynchronized when playing back trace` on unintended logic changes (`HACKING.md:182-187`).
  - Residual decompilation uncertainty is admitted inline, e.g. `src/Engine/Graphics/Outdoor.cpp:1824`
    (`"decompilation can be inaccurate, please send savegame to Nomad"`), `src/Engine/Graphics/Vis.cpp:598`
    ("but it may be decompilation error thou"), and dozens of `TODO(yoctozepto): … check MM6&8` notes in
    `src/Engine/Evt/EvtInstruction.cpp`.

## 7. Explicitly not implemented / incomplete

There is **no `TODO.md`, roadmap file, or in-repo feature matrix** — status has to be reconstructed. What is stated
or directly observable:

- **MM6 and MM8 are not playable.** Only MM7 is (`README.md:11-12`); CI runs game tests against MM7 data only
  (`build_all.yml:263,271`); only `resolveMm7Paths` is wired in (`GameStarter.cpp:233`); MM8 event remapping and MM8
  weather flags carry TODOs (§5). MM6/MM8 support is tracked only in GitHub milestones, not in the tree.
- **A stable release does not exist yet** — nightly builds only, v0.1 milestone still open (`README.md:22-23`).
- **No user-facing error reporting**: "We don't yet have a mechanism for displaying errors to the user through the
  UI" (`HACKING.md:131`).
- **Modding is explicitly not planned for the near future**; Lua scripting exists for debugging only
  (`HACKING.md:206-208`), console reachable only in-game via `~` (`HACKING.md:206-218`).
- **No HWL hardware textures** — `d3dbitmap.hwl` / `d3dsprite.hwl` are commented out of the required-data list
  (`src/Application/Startup/PathResolver.cpp:17-18`).
- **MM6-only UI animations are not resurrected** — food/gold "glow" animations are commented out with a TODO to try
  MM6 resource files (`src/Engine/Engine.cpp:754-770`).
- **Known stubs / errors in gameplay code**: thieving/pickpocketing paths log `MM_ERROR("Thieving unsupported")`
  (`src/Engine/Objects/Character.cpp:4521,5105,5512`); a spell effect is marked "spell not implemented in the game"
  (`src/Engine/SpellFxRenderer.cpp:1307`); Arcomage animations "advance once per frame now" rather than by time
  (`src/Arcomage/Arcomage.h:168`).
- **627 TODO markers** in `src/` (312 `TODO(captainurist)`, 145 `TODO(pskelton)`, 34 `TODO(yoctozepto)`, 9
  `TODO(Gerark)`). Most are refactor/accuracy notes rather than missing features — do not read the count as a
  feature gap list.
- Uncertainty note: because maturity claims are mostly external (milestones, Discord), anything beyond "MM7 plays
  end to end, MM6/MM8 do not" is **not verifiable from this checkout** and is not asserted here.
