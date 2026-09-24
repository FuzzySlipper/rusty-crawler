# Might and Magic VII map payload formats — binary spec for a C# decoder

Read-only research. Sources, priority order: OpenEnroth `src/` (`OE:`), MMExtension `Scripts/` (`MMX:`), plus byte
checks against the operator's install `/home/research/old-games/game-mm7` (`GAMES.LOD`; read-only, entries extracted
to `/tmp` only). Little-endian; every struct is packed (`#pragma pack(1)` **[verified: OE:src/Engine/Snapshots/EntitySnapshots.h:84]**).

Markers: **[verified: p:l]** donor source line; **[verified: data E@0xN]** measured in `GAMES.LOD` entry `E`;
**[donor-doc]** donor documents it, not byte-verified here; **[uncertain]** my reading, unconfirmed.

Validated for this doc **[verified: data]**: all **13 `.odm`, 13 `.ddm`, 63 `.blv`, 63 `.dlv`** parse to exactly
`entryLength-16` bytes under the layouts below, no slack; the three BLV pools (`faceData`, `sectorData`,
`sectorLightData`) and the DLV door pool are consumed exactly, no remainder. (13 outdoor + 63 indoor = the 76 map
files of `local/research/mm7-data-inventory.md`.)

## 1. Container level

`.odm/.ddm/.blv/.dlv` are ordinary `DATA/GAMES.LOD` entries (19,608,695 B, version string `GameMMVI`, root
directory `maps`, 152 entries) **[verified: data]**; indoor loads `pGames_LOD->read("<name>.blv")`, outdoor
`read("<name>.odm")` **[verified: OE:src/Engine/Graphics/Indoor.cpp:276; src/Engine/Graphics/Outdoor.cpp:455]**.
Entry names match case-insensitively; the `MAPSTATS.TXT` file-name column is the link table.

| wrapper | applies to maps? | layout |
| --- | --- | --- |
| `LodCompressionHeader_MM6`, 16 B: `u32 version==91969`, `char[4] "mvii"`, `u32 dataSize`, `u32 decompressedSize` (0 ⇒ stored verbatim, else zlib) | **yes — the only one** | **[verified: OE:src/Library/LodFormats/LodFormats.cpp:41-49,129-143]** |
| `LodImageHeader_MM6`, 48 B, `flags&0x100` | no — `Events.lod` text tables only | detected by `size==0 && dataSize>0 && w==h==0 && flags&256 && blob==48+dataSize` **[verified: OE:.../LodFormats.cpp:51-63]** |

Rule: `payload = raw[16 : 16+dataSize]`, inflate to `decompressedSize`; if `dataSize == recordSize` the original
writer was buggy and the whole tail is the payload **[verified: OE:.../LodFormats.cpp:132-139]**. Measured
`d01.blv`: record 275,685 B, `dataSize` 275,669, `decompressedSize` 874,306 **[verified: data d01.blv@0x00]**; all
152 GAMES.LOD entries are `mvii` **[verified: data]**. **There is no second wrapper around map payloads** — byte 0 of
the inflated payload is structure field 0. Sanity stamps: `.odm` `description` == `"MM6 Outdoor v7.00"` (13/13),
`.blv` `u32@0` == 1 (63/63) **[verified: data]**.

## 2. ODM (outdoor) — `OutdoorLocation_MM7`

Field order *is* file order **[verified: OE:src/Engine/Snapshots/CompositeSnapshots.h:86-111;
.../CompositeSnapshots.cpp:581-612]**.

| off | size | type | field / notes |
| --- | --- | --- | --- |
| 0x000 / 0x020 / 0x040 | 32 each | char[32] | name `"blank"`, fileName `"default.odm"`, description `"MM6 Outdoor v7.00"` — all 13 alike **[verified: data]** |
| 0x060 / 0x080 | 32 each | char[32] | skyTexture (empty in 12/13, `"sky13"` in one; BITMAPS.LOD name) / groundTilesetUnused (`"grastyl"`, unread by OE) **[verified: data]** |
| 0x000A0 | 16 | `OutdoorTileType_MM7[4]` | `u16 tileset` + `u16 tileOffset` **[verified: OE:...EntitySnapshots.h:1350-1354]**; tileset values `EnumSnapshots.h:43-70`; the offset is recomputed by the engine, may be ignored **[verified: OE:...CompositeSnapshots.cpp:532-537]** |
| 0x0B0 / 0x40B0 / 0x80B0 | 16384 each | u8[128*128] | heightMap (flat index `gridY*128+gridX`; world `z = 32*h` **[verified: OE:src/Engine/Graphics/OutdoorTerrain.cpp:47-51]**) / tileMap (only the top-left 127×127 cells are used **[verified: OE:...OutdoorTerrain.cpp:12-16]**) / attributeMap (all zero in all 13 **[verified: data]**, skip) |
| 0x0C0B0 | 4 | u32 | normalCount (out01 1387, out02 25516) **[verified: data]** |
| 0x0C0B4 | 131072 | f32[128*128*2] | per-cell 2 normal distances; `-0.0`/0 filler in out01, real values in out02/out06 **[verified: data]** |
| 0x2C0B4 | 65536 | i16[128*128*2] | normal indices into `normals`; max == `normalCount-1` in 3/3 **[verified: data]**; then `0x3C0B4 + 12·N` Vec3f[N] normals **[verified: OE:...CompositeSnapshots.h:99]** |
| … | 4 | u32 | modelCount (out01 98, out02 114, out06 94) **[verified: data]** |
| … | 188·M | `BSPModelData_MM7[M]` | all headers contiguous, then extras — see below |
| … | per model i | extras | `12·nv` Vec3i vertices, `308·nf` `ODMFace_MM7`, `2·nf` u16 facesOrdering, `8·nn` `BSPNode_MM7`, `10·nf` char[10] faceTextures, in model order **[verified: OE:...CompositeSnapshots.cpp:597-605]** |
| … | 4 | u32 | decorationCount D **[verified: OE:...CompositeSnapshots.cpp:607]** |
| … | 32·D | `LevelDecoration_MM7[D]` | **[verified: OE:...EntitySnapshots.h:1098-1110]** |
| … | 32·D | char[32][D] | decorationNames — `"Party Start"`, `"Snd_Boat"`, `"tree04"` **[verified: data]** |
| … | 4+2·P | u32 P + u16[P] | pidList of `ObjectRef` (`kind=v%8`, `index=v/8` **[donor-doc MMX:Scripts/Structs/01 common structs.lua:634-706]**); out01 P=18,207, values ≤1173 ⇒ index < D **[verified: data]** |
| … | 65536 | u32[128*128] | decorationMap ("OMAP"): per-cell offset into pidList; out01 all 16384 distinct, max 18206 < P **[verified: data]** |
| … | 4+24·S | u32 S + `SpawnPoint_MM7[S]` | spawns (out01 S=11, out02/out06 S=49) **[verified: data]** |

`BSPModelData_MM7` (188 B) **[verified: OE:...EntitySnapshots.h:1208-1242]**: `+0x00 char[32] modelName`, `+0x20
char[32] modelName2`, `+0x40 i32 wasSeenUnused`, `+0x44 u32 numVertices`, `+0x48 ptr vertices`, `+0x4C u32 numFaces`,
`+0x50 u32 numConvexFaces`, `+0x54 ptr faces`, `+0x58 ptr facesOrdering`, `+0x5C u32 numNodes`, `+0x60 ptr nodes`,
`+0x64 u32 numDecorations`, `+0x68 i32 centerX`, `+0x6C i32 centerY`, `+0x70 Vec3i position`, `+0x7C..0x93 i32
minX..maxZ`, `+0x94..0xAB otherMin..otherMax`, `+0xAC Vec3i boundingCenter`, `+0xB8 i32 boundingRadius`. The pointer
fields hold stale runtime addresses — **never dereference them** (out01 model 0 `vertices` = 0x01EFF4F0 while the
array sits at a fixed file offset) **[verified: data]**. `numNodes == numConvexFaces == 0` for **every** model of
**every** shipped `.odm`, so the node term is 0 bytes, but read `numNodes` anyway **[verified: data, all 13]**.
Model vertices are **`Vec3i`, 12 B, absolute world coordinates** — not 6-byte `Vec3s`, not model-relative: model
`Tavern_E` face 0 lists ids 13,9,20,19 whose coordinates reproduce its bbox exactly **[verified: data
out01.odm@0x449B4]** (the `.blv` uses 6-byte `Vec3s` **[verified: OE:...CompositeSnapshots.h:32]**).

`ODMFace_MM7` (308 B) **[verified: OE:...EntitySnapshots.h:896-929]**, cross-checked field-by-field against MMX
`ModelFacet` (size 0x134) **[donor-doc MMX:...01 common structs.lua:2161-2198]**:

| off | size | type | field |
| --- | --- | --- | --- |
| 0x00 | 16 | `Planei_MM7` | fixed-point plane `i32 nx,ny,nz` (65536 = 1.0) + `i32 dist` — out01 face0 `(65536,0,0), -763363328` **[verified: data]** |
| 0x10 | 12 | i32[3] | zCalc1..3; `0x1C u32 attributes` (indoor flag values, `FaceEnums.h:7-46`) |
| 0x20 | 40 | i16[20] | vertexIds (indices into the model's vertex array) |
| 0x48 / 0x70 | 40 / 40 | i16[20] | textureUs / textureVs |
| 0x98 / 0xC0 / 0xE8 | 40 each | i16[20] | x / y / zInterceptDisplacements |
| 0x110 | 2 | i16 | textureId — **0 in 56,345 of 56,347 faces across all 13 `.odm`** (2 exceptions); dead, the name comes from `faceTextures` **[verified: data]** |
| 0x112 / 0x114 | 2 / 2 | i16 | textureDeltaU / V (= `-min(U)` / `-min(V)`) **[verified: data]** |
| 0x116 | 12 | `BBoxs_MM7` | i16 minX,maxX,minY,maxY,minZ,maxZ (order is OpenEnroth's, `EntitySnapshots.h:93-100`) |
| 0x122 / 0x124 / 0x126 | 2 each | i16 | cogNumber / eventId / eventTriggerType (always 0); `0x128 char[2]` reserved, `0x12A u8[4] gradientVertexes` |
| 0x12E | 1 | u8 | numVertices (**≤ 20**); `0x12F u8 polygonType` (0..6, `FaceEnums.h:77-85`); `0x130 u8 shadeType` (0..24), `0x131 u8 visible`, `0x132 char[2]` |

Arrays are fixed 20 slots: `vertexIds[numVertices]` repeats vertex 0; the U/V/intercept slots at `[numVertices]` are
0 in shipped data and OE reads only `numVertices` values **[verified: data out01 face0 vertexIds=[13,9,20,19,13,0],
us=[4740,4868,4868,4740,0,0]; OE:...CompositeSnapshots.cpp:491-494]**. **The ODM texture reference is the 10-byte
name in the model's `faceTextures`** (`"Hhp1d"`), fed to `SetTexture`; 26 of the 56,347 shipped faces have an empty
name (untextured) **[verified: data]**. `facesOrdering` is all-zero and unusable **[verified: data; donor-doc
OE:...CompositeSnapshots.h:78]**.

World model **[verified: OE:src/Engine/Graphics/OutdoorTerrain.h:21-41]**: X east, Y north, origin at centre, 128×128
cells of 512 units; `gridX=(x>>9)+64`, `gridY=63-(y>>9)` (arithmetic shift, negative X rounds down),
`x=(gridX-64)<<9`, `y=(64-gridY)<<9`, `z=32·heightMap[gridY*128+gridX]`. Independent confirmation: the
`"Party Start"`/`"… start"` decoration z equals `32·h` exactly in 10/13 ODMs (the 3 misses are slopes the engine
recomputes) **[verified: data, all 13]**.

## 3. BLV (indoor) — `IndoorLocation_MM7` (+ its `.dlv` for doors)

Order **[verified: OE:...CompositeSnapshots.h:30-48; ...CompositeSnapshots.cpp:294-311]**; each array is preceded by
a `u32` count unless its size comes from the header (`presized`) **[verified: OE:src/Library/Binary/CommonSerialization.h:137-162]**.

| off | size | type | field |
| --- | --- | --- | --- |
| 0x000 / 0x004 | 4 / 100 | u32 / char[100] | version == 1 and name `"No Name Level"` in all 63 **[verified: data]**; MMX reads the name as 60 B + 40 B skip **[donor-doc MMX:...01 common structs.lua:376-385]** |
| 0x068 / 0x06C / 0x070 / 0x074 | 4 each | i32 | faceDataSizeBytes (bytes = 2×i16 count) / sectorDataSizeBytes / sectorLightDataSizeBytes / doorsDataSizeBytes (door pool lives in the `.dlv`) |
| 0x078 | 16 | char[16] | zero in all 63 **[verified: data]** |
| 0x088 | 4+6·V | u32 V + `Vec3s[V]` | vertices (`Vec3s` == 6 B **[verified: OE:...EntitySnapshots.h:64]**) |
| … | 4+96·F | u32 F + `BLVFace_MM7[F]` | faces |
| … | header | i16[] | faceData pool |
| … | 10·F | char[10][F] | faceTextures (BITMAPS.LOD names) |
| … | 4+36·E | u32 E + `BLVFaceExtra_MM7[E]` | face extras; then `10·E` char[10] faceExtraTextures (empty in d01 **[verified: data]**) |
| … | 4+116·S | u32 S + `BLVSector_MM7[S]` | sectors |
| … | header | i16[] | sectorData pool; then sectorLightData pool (indices into `lights`) |
| … | 4 | u32 | doorCount — **200 in all 63 `.blv`** (fixed-capacity array) **[verified: data]**; the door *records* are in the `.dlv` |
| … | 4+32·D | u32 D + `LevelDecoration_MM7[D]` | decorations; then `32·D` char[32] decorationNames |
| … | 4+16·L, 4+8·B, 4+24·P, 4+12·O | count + array | lights, BSP nodes, spawn points, map outlines (in that order) |

`BLVFace_MM7` (96 B) **[verified: OE:...EntitySnapshots.h:161-183]** — offsets confirmed on data:

| off | size | type | field |
| --- | --- | --- | --- |
| 0x00 | 16 | `Planef_MM7` | float plane `f32 nx,ny,nz,dist` (d01 face0 `(0,0,-1) d=-64`) |
| 0x10 | 16 | `Planei_MM7` | same plane in 16.16 fixed point (the one the engine uses) |
| 0x20 | 12 | i32[3] | zCalc1..3; `0x2C u32 attributes` (`FaceEnums.h:7-46`: 0x1 portal, 0x10 fluid/water, 0x2000 invisible, 0x4000 animated, 0x02000000 clickable, 0x04000000 pressure plate **[donor-doc MMX:Scripts/Global/Editor Read Data.lua:56-93]**) |
| 0x30 | 24 | 6×ptr | vertexIDs, x/y/zInterceptDisplacements, vertexUIds, vertexVIds — stale addresses, ignore |
| 0x48 | 2 | u16 | faceExtraId → index into `BLVFaceExtra_MM7[]` |
| 0x4A | 2 | u16 | bitmapId — **0xFFFF in all 4277 faces of d01; do not use** **[verified: data]** |
| 0x4C / 0x4E | 2 / 2 | u16 / i16 | sectorId (0xFFFF/0 ⇒ none) / backSectorId (portal's far side) |
| 0x50 | 12 | `BBoxs_MM7` | i16 minX,maxX,minY,maxY,minZ,maxZ |
| 0x5C / 0x5D / 0x5E | 1 / 1 / 2 | u8 / u8 / i16 | polygonType (1 wall, 3 floor, 4 slope floor, 5 ceiling, 6 slope ceiling) / numVertices / pad |

`faceData` pool: **6 arrays of `(numVertices+1)` i16 per face, in face order** — vertexIds, xIntercept, yIntercept,
zIntercept, U, V **[verified: OE:...CompositeSnapshots.cpp:163-186; donor-doc MMX:Scripts/Global/Convert Blv.lua:250-259]**.
`Σ 6·(nv+1) == faceDataSizeBytes/2` exactly on all 63 `.blv` **[verified: data]**. The trailing `+1` slot duplicates
vertex 0 in the id array and is 0 in U/V **[verified: data d01 face0]** — read `numVertices` entries, discard it.
`faceTextures[i]` (10 B) is face `i`'s texture; empty = untextured (47/4277 in d01) **[verified: data;
OE:...CompositeSnapshots.cpp:191-197]**. `faceExtras[face.faceExtraId]` gives `face_id` (+0x0C),
`uAdditionalBitmapID` (+0x0E, always -1 in OE), `sTextureDeltaU/V` (+0x14/+0x16), `sCogNumber` (+0x18), `uEventID`
(+0x1A) **[verified: OE:...EntitySnapshots.h:1116-1136; ...CompositeSnapshots.cpp:213-229]**.

`BLVSector_MM7` (116 B) **[verified: OE:...EntitySnapshots.h:845-889]**, corroborated by MMX `MapRoom` (0x74)
**[donor-doc MMX:...01 common structs.lua:2249-2296]**: `+0x00 i32 flags` (0x08 non-vertical portals, 0x10 has BSP
**[donor-doc:2251-2253]**; d01 sectors = 0x18), then alternating `u16 count`+`i16 pad`+`ptr` for floors(0x04/0x08),
walls(0x0C/0x10), ceilings(0x14/0x18), fluids(0x1C/0x20), portals(0x24/0x28), faces(0x2C) + nonBspFaces(0x2E) +
faceIds(0x30), cylinderFaces(0x34/0x38), cogs(0x3C/0x40), decorations(0x44/0x48), markers(0x4C/0x50),
lights(0x54/0x58); then `i16` waterLevel(0x5C), mistLevel(0x5E), lightDistanceMultiplier(0x60),
minAmbientLightLevel(0x62), firstBspNode(0x64), exitTag(0x66), `BBoxs_MM7 boundingBox(0x68)`. Fluids, cylinderFaces,
cogs, markers are 0 in shipped MM7 data, and waterLevel/mistLevel/lightDistanceMultiplier/exitTag are 0 or -30000
**[donor-doc; verified: OE:...EntitySnapshots.h:856-886]**.

Pools — pointers are stale, data is concatenated **per sector in sector order** **[verified:
OE:...CompositeSnapshots.cpp:234-278]**: `sectorData` = `floors, walls, ceilings, fluids, portals, faces, cogs,
decorations (MMX "Sprites"), markers` — the cylinder count sits between faces and cogs in the sector record but is 0 in
all MM7 data and OE does not advance the pool by it; `sectorLightData` = `lights` per sector. Both match their
declared sizes **exactly** in d01 (8682/8682, 337/337 i16) and all 63 **[verified: data]**. `faceIds[0..numNonBspFaces)`
are the non-BSP faces; portal faces live in `portals[]`, usually repeat at the head of `faceIds[]`, and name the far
side in `backSectorId` (d01 sector1 portals [72,126], sector2 [72,3700]) **[verified: data]**.

### 3.1 DLV (indoor delta) — where the doors are

`.dlv` is a delta parsed **with the loaded `.blv` as context** (its counts come from the `.blv`), read from
`GAMES.LOD` or a save **[verified: OE:src/Engine/Graphics/Indoor.cpp:284-320]**. Order **[verified:
OE:...CompositeSnapshots.h:54-67; ...CompositeSnapshots.cpp:455-467]**; all 63 walk to EOF exactly **[verified: data]**:

| off | size | type | field |
| --- | --- | --- | --- |
| 0x000 | 40 | `LocationHeader_MM7` | `i32 respawnCount, lastRespawnDay, reputation, alertStatus`, `u32 totalFacesCount, decorationCount, bmodelCount`, `i32 f1C, f20, f24` **[verified: OE:...EntitySnapshots.h:1310-1319]**; all zero in shipped deltas **[verified: data]** (`lastRespawnDay==0` ⇒ first visit **[verified: OE:src/Engine/Graphics/Indoor.cpp:288-300]**) |
| 0x028 | 875 | char[875] | visibleOutlines bitfield (7000 bits) |
| … | 4·F_blv | u32[F] | faceAttributes, one per **BLV** face (DLV-only; all non-zero in d01) |
| … | 2·D_blv | u16[D] | decorationFlags, one per **BLV** decoration |
| … | 4+0x344·A | u32 A + `Actor_MM7[A]` | actors; then spriteObjects (`4+0x70·Q`), chests (`4+5324·C`) |
| … | 80·200 | `BLVDoor_MM7[200]` | **always 200 records, no count prefix**; then `doorsDataSizeBytes` of i16 door pools |
| … | 200 / 8 / 48 | vars / i64 / `MapWeather_MM7` | 75 map vars + 125 decoration vars / lastVisitTime / 12-byte sky name + `i32 flags` (1 = fog) + 2 fog distances + 24 unused |

`BLVDoor_MM7` (80 B) **[verified: OE:...EntitySnapshots.h:815-838; donor-doc MMX:...01 common structs.lua:2368-2401]**:
`+0x00 u32 attributes` (1 triggered, 4 no-sound), `+0x04 u32 doorId`, `+0x08 u32 timeSinceTriggered`, `+0x0C Vec3i
direction`, `+0x18 u32 moveLength`, `+0x1C u32 openSpeed`, `+0x20 u32 closeSpeed`, `+0x24 8×ptr` (vertexIds, faceIds,
sectorIds, deltaUs, deltaVs, x/y/zOffsets), `+0x44 u16 numVertices`, `+0x46 numFaces`, `+0x48 numSectors`, `+0x4A
numOffsets`, `+0x4C u16 state`, `+0x4E pad`. Door pool order per door: `numVertices, numFaces, numSectors,
numFaces(deltaU), numFaces(deltaV), 3×numOffsets` i16 **[verified: OE:...CompositeSnapshots.cpp:384-409]**; the sum
equals `doorsDataSizeBytes/2` exactly (d01: 3052) for all 63 pairs **[verified: data]**. **Doors do not name their
sectors in MM7** — `numSectors == 0` in **all 12,600 door slots of all 63 `.dlv`**; only 786 slots are in use
(`numVertices != 0`), the rest are all-zero records, and only states 0 (12,028) and 2 (572) occur
**[verified: data]**: a door connects through its faces' `sectorId`/`backSectorId`, and its faces (d01 door0 = faces
1592..1596, each twice, one per side) are the geometry moved by `state` (0 initial, 1 moving to 1, 2 moved, 3 moving
back **[donor-doc MMX:...01 common structs.lua:2396-2400]**) by `x/y/zOffsets`, which are indexed by
`moveLength·progress` **[verified: OE:src/Engine/Graphics/Indoor.cpp:584-615]**. Unused slots are all-zero records.
`.ddm` mirrors this outdoors **[verified: OE:...CompositeSnapshots.h:113-125]**: header 40, `u8[88][11]
fullyRevealedCells`, `u8[88][11] partiallyRevealedCells` (968 B each), `u32[totalFaces] faceAttributes`,
`u16[decorations] decorationFlags`, actors, spriteObjects, chests, 200 B event vars, i64 lastVisit, weather 48 —
walks to EOF for all 13 **[verified: data]**.

## 4. Entry/exit points and spawn points

* **Arrival is a decoration, not a header field.** The party arrives at the level decoration named `"Party Start"`,
  `"North Start"`, `"South Start"`, `"East Start"` or `"West Start"` **[verified: OE:src/Engine/MapEnums.cpp:5-11]**,
  matched against `decorationNames[]`/the decoration table **case-insensitively** (`ascii::noCaseEquals`
  **[verified: OE:src/Engine/Tables/DecorationTable.cpp:13-22]**) — shipped maps use `"east start"`/`"north start"`
  lowercase as often as `"Party Start"` **[verified: data, all 13 ODMs]**. Position = decoration `vPosition` (+0x04),
  but z is **replaced** by the floor level (`ODM_GetFloorLevel`/`BLV_GetFloorLevel`) and yaw comes from `_yawAngle`
  (+0x10, 2048 units/turn: 0 west, 512 south, 1024 east, 1536 north) **[verified: OE:src/Engine/PartyPlacement.cpp:17-45]**.
  No matching decoration ⇒ the party does not move.
* **Which start point**: a `MoveToMap` with an all-zero x/y/z means "arrive at Party Start" **[verified:
  OE:src/Engine/Evt/EvtInterpreter.cpp:117-135]**; walking off an outdoor edge uses a hardcoded per-map direction
  table **[verified: OE:src/Engine/Graphics/Outdoor.cpp:106-121,286-326]** (e.g. entering Harmondale from the south
  uses `South Start`).
* **`MoveToMap` operands**: `u32 x, y, z, yaw, pitch, zspeed`, `u8 house_id`, `u8 exit_pic_id`, NUL-terminated
  destination map name — the widely-quoted "x/y/z + house_id + exit_pic + name" **omits `yaw`, `pitch`, `zspeed`**
  **[verified: OE:src/Engine/Evt/EvtInstruction.cpp:930-940; src/Engine/Evt/EvtInstruction.h:135]**. Non-zero x/y/z is
  an absolute placement in the destination map; `yaw == -1` keeps the current facing **[verified:
  OE:src/Engine/PartyPlacement.h:14-27]**. A destination name starting with `'0'` (or empty) means "stay on this map",
  so **intra-map teleports are `MoveToMap` with coordinates and no real destination** **[verified:
  OE:src/Engine/Evt/EvtInterpreter.cpp:120-131; src/GUI/UI/UITransition.cpp:153-156]**. `house_id`/`exit_pic_id` drive
  the transition screen only (house movie/sound, exit-picture index) — **no geometry** **[verified:
  OE:src/GUI/UI/UITransition.cpp:137-176]**.
* **Coverage** **[verified: data]**: 58/63 indoor maps declare a `"Party Start"` decoration (only `d01`, `mdt01`,
  `mdt09`, `mdt10`, `mdt14` do not), yaw 0 everywhere except `t02.blv` (512); 12/13 outdoor maps declare `Party Start`
  plus 1–3 direction starts, with yaw 0/512/1024/1536 (west/south/east/north). Indoor arrival therefore needs both
  mechanisms: the decoration when present, otherwise the `MoveToMap` coordinates.
* **Spawn points are monsters/treasure, not the party** (`SpawnEncounter`/`SpawnRandomTreasure` on first visit)
  **[verified: OE:src/Engine/Graphics/Outdoor.cpp:1768-1776]**: `SpawnPoint_MM7` = `Vec3i position` (absolute),
  `u16 radius`, `u16 type` (ObjectRef kind: 2 object/treasure, 3 actor), `u16 treasureLevelOrMonsterIndex`,
  `u16 attributes`, `u32 group` **[verified: OE:...EntitySnapshots.h:945-953; donor-doc MMX:...01 common structs.lua:2346-2366]**.
  Outdoor spawns are map-global in the `.odm`; indoor spawns are in the `.blv`, and levels also spawn actors from the
  `.dlv` `Actor_MM7` array **[verified: OE:...CompositeSnapshots.cpp:363-366]**.

## 5. Minimal viable decode

Order matters: **every field must be seeked over even when unused**, since later sizes derive from earlier fields.

1. **Container**: LOD root directory + 32-byte entries (`LodSnapshots.h:52-61`, `LodReader.cpp:42-70`), accept the
   16-byte `mvii` wrapper, zlib-inflate.
2. **ODM** `(a,b)`: read the 176-byte prefix, 16-byte tileTypes, 16 KB heightMap; **seek** past tileMap (or keep it
   to draw ground), 16 KB attributeMap, `u32 normalCount`, 131072-byte normal-distance block, 65536-byte
   normal-index block, `12·normalCount` normals. Read `u32 modelCount`, then **read all M 188-byte headers as one
   block** (only `numVertices/numFaces/numNodes/position/min-max` are needed), then walk per-model extras **in model
   order**: keep `Vec3i` vertices (12 B each) and `ODMFace_MM7` faces, **seek** past `2·numFaces` ordering words and
   `8·numNodes` nodes, keep `10·numFaces` texture names. Then decorations + names (keep `*Start`), seek past pidList
   and the 64 KB decorationMap, read spawns. `(c)`: face vertices = `vertices[vertexIds[i]]` (absolute world), texture
   = `faceTextures[faceIndex]`, UV = inline arrays. `(b)`: ±32768 in x/y, z from heightMap, per-model min/max for
   surface bounds.
3. **BLV** `(a,b,c,d)`: 136-byte header, vertices, faces, the faceData pool (needed for vertex ids and UV — cannot be
   skipped), faceTextures, faceExtras + extra textures, sectors, the sectorData pool (needed for sector →
   floors/walls/ceilings/portals/faces), the sectorLightData pool, `doorCount`, decorations + names (Party Start),
   lights, bspNodes, spawns, outlines.
4. **DLV** `(d)`: header, seek 875 + `4·F_blv` + `2·D_blv`, read actors / sprite objects / chests (count-prefixed
   each), read the 200 door records + pool, seek 200 + 8 + 48 tail bytes. Door faces come from
   `doorsData` using the per-door split; door sector linkage comes from each face's `sectorId`/`backSectorId`
   (`numSectors` is 0).

**Safe to skip**: ODM tileTypes offsets (recomputable), attributeMap, all three normal arrays (recompute from
heights), facesOrdering, model BSP nodes (always empty), pidList, decorationMap, sky/ground strings (geometry);
BLV `face.bitmapId` and the six face pointers, fluids/cylinder/cogs/markers pool entries (all zero),
waterLevel/mistLevel/lightDistanceMultiplier/exitTag, mapOutlines (minimap only), bspNodes (acceleration only — walk
all faces instead); DLV visibleOutlines, faceAttributes, decorationFlags, PersistentVariables, lastVisitTime,
weather (presentation only). **Cannot skip**: `u32` count prefixes, the fixed 188-byte ODM header array, `10·numFaces`
ODM texture names, the fixed `131072+65536+12N` normal block, the 64 KB decorationMap, the three BLV pools, the 200
DLV door records, and the extras ordering (all model headers first, then per-model extras).

## 6. Risks and unknowns

* **ODM 128 KB float block**: matches MMX's runtime `TerNormDist` (`f32[128][128][2]`) followed by `TerNormId`
  (`i16[128][128][2]`) and `TerNorm` **[donor-doc MMX:...01 common structs.lua:211-217]** and the index-range check
  passes, but no OpenEnroth code reads it (OE recomputes normals). The *size* is **[verified: data]**; the *meaning*
  of both blocks is **[uncertain]**. `attributeMap` is all zero in all 13 ODMs — also **[uncertain]**; do not build
  gameplay on it.
* **Absolute ODM model vertices** conflict with MMX's editor path, which treats model `Pos` as a separate placement;
  the geometry is absolute in the file **[verified: data]**, so never add `position` twice.
* `.blv` `exitTag` (i16 @0x66): MMX documents **two `u8`s** — `levelRoomExitsTo` + `exitTag`
  **[donor-doc MMX:...01 common structs.lua:2281-2282]** — disagreeing with OE's single `i16`; both are 0 in shipped
  data, so **[uncertain]**.
* `BSPNode_MM7.uBSPFaceIDOffset` is an offset into the level face array **[verified: OE:...EntitySnapshots.h:1142-1148]**;
  indoor `firstBspNode` and node ordering are documented only by donor code and remain **[uncertain]** without
  tracing the renderer (a decoder can ignore BSP and still enumerate all faces).
* **Door state naming**: OE's own comment says most closed doors are `DOOR_OPEN` **[verified:
  OE:src/Engine/Graphics/FaceEnums.h:61-67]**; MMX's naming (0 = state(0), 2 = state(1)) matches shipped data (198/200
  d01 doors are 0) **[verified: data]** — use MMX's reading.
* `MapWeather_MM7.field_2F4` (24 B) and `.ddm/.dlv` `field_1C/20/24` are unread by every donor; sizes
  **[verified: data]**, meaning **[uncertain]**.
* **MM6/MM8 divergence** (all **[donor-doc]**, no MM6/MM8 data at hand): MM6 `.dlv` header is 8 B with no
  face/decoration attribute arrays **[MMX:Convert Blv.lua:287-293]**; MM6 `MapFacet` is 0x50 with no float plane,
  MM7/8 0x60 **[MMX:...01 common structs.lua:2086,2118]**; lights 0xC / 0x10 / 0x14 (MM8 adds `Id`)
  **[MMX:2314-2325]**; MM8 sectors add `EaxEnvironment` (+4 B) and MM8 `.odm` adds `TilesetsFile`, a `Bits` dword,
  weather flags + `Ceiling`, and a 30,604-byte notes block **[MMX:2249-2258,387-404,407-435;
  Convert Blv.lua:312-314]**. Outdoor facets are 0x134 in all three games **[MMX:2161-2198]**. MM7's own outdoor
  payload still self-identifies as `"MM6 Outdoor v7.00"` **[verified: data]** — do not read that as MM6.
* Everything here is **shipped `GAMES.LOD` data**; runtime `.ddm/.dlv` written by the engine into `SAVES/` are only
  guaranteed to match the same reader and are out of scope.
* Name matching (LOD entries, decoration/start-point names) must be case-insensitive, and a re-saving level editor
  can reorder extras — validate `Σ 6·(nv+1) == faceDataSizeBytes/2` and `Σ pools == sectorDataSizeBytes/2` and fail
  loudly instead of drifting silently **[verified: data — both hold exactly on all 63 `.blv`]**.
