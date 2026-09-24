# Might and Magic VII media formats — binary spec for a C# extractor

Read-only research. Sources: OpenEnroth `src/Library/{Lod,LodFormats,Image,Snd,Vid}` and its sprite users; MMExtension
`Src/RSPak/Extra/RSLod.pas` and `Scripts/Structs/01 common structs.lua`; byte checks against the operator's read-only
install `/home/research/old-games/game-mm7`. Fields **little-endian**, structs **packed**. Markers: **[donor]** =
OpenEnroth source; **[doc]** = MMExtension; **[data]** = measured here (offset/count given); **[uncertain]** = my
reading, unconfirmed. Nothing is [uncertain] unless marked.

**[data]** snapshot: `ICONS.LOD` 4241 entries/45,354,112 B · `SPRITES.LOD` 9343/76,300,007 · `BITMAPS.LOD`
1614/29,350,976 · `Events.lod` 198/432,573 · `GAMES.LOD` 152/19,608,695 · `Might7.vid` 162/112,600,244 ·
`Magic7.vid` 14/211,669,708 · `Audio.snd` 2519/29,982,482 · `Music/*.mp3` already plain · `d3d*.hwl` engine caches.
## 1. Containers and entry wrappers
### 1.1 LOD container

| off | size | type | field | notes |
| --- | --- | --- | --- | --- |
| 0x00 | 4 | char[4] | signature | `"LOD\0"`, else invalid **[donor: LodReader.cpp:24-25]** |
| 0x04 | 80 | char[80] | version | NUL-terminated `MMVI`/`GameMMVI`/`MMVII`/`MMVIII` **[donor: LodEnums.cpp:5-10]** |
| 0x54 | 80 | char[80] | description | free text, unused logically |
| 0xA4 | 4 | u32 | size | always 100 **[donor: LodSnapshots.h:40]** |
| 0xA8 | 4 | u32 | unk_0 | 0 |
| 0xAC | 4 | u32 | numDirectories | must be 1 **[donor: LodReader.cpp:32-33]** |
| 0xB0 | 80 | char[80] | unk_1 | uninitialised garbage, never read **[donor: LodSnapshots.h:43]** |
| 0x100 | 32 | `LodEntry` | root directory entry | `numItems` file entries follow at `root.dataOffset` |

`LodEntry_MM6` (32 B), used by the root entry and by every MM6/MM7 file entry **[donor: LodSnapshots.h:52-60]**:

| off | size | type | field | notes |
| --- | --- | --- | --- | --- |
| 0x00 | 16 | char[16] | name | NUL-padded, trailing bytes garbage — truncate at first NUL **[data: ICONS.LOD@0x10C = `7e 22 e8 77` after `icons\0`]** |
| 0x10 | 4 | u32 | dataOffset | root: from file start; file: from `root.dataOffset` **[donor: LodReader.cpp:110]** |
| 0x14 | 4 | u32 | dataSize | payload length (root: directory + contents) |
| 0x18 | 4 | u32 | unk_0 | 0 |
| 0x1C | 2 | u16 | numItems | non-zero ⇒ subdirectory; vanilla LODs have none **[donor: LodReader.cpp:60-62]** |
| 0x1E | 2 | u16 | priority | 0 |

Reader rules: payload is `data[root.dataOffset + e.dataOffset .. +e.dataSize]`; names are matched case-insensitively and
**the first** duplicate wins **[donor: LodReader.cpp:108-116]**; a Russian MM7 re-release ships a truncated root
`dataSize`, so clamp the directory region to `fileSize - root.dataOffset` **[donor: LodReader.cpp:101-102]**. MM8
(`MMVIII`) file entries are 76 B — `name[16]`, 12×u32 unknowns, `dataOffset`@+0x40, `dataSize`@+0x44, `unk_14`@+0x48 —
selected by `fileEntrySize(version)`; header and root entry unchanged **[donor: LodSnapshots.h:67-85, LodReader.cpp:52-58]**.
**The version string does not identify the game**: all five MM7 DATA archives say `MMVI`/`GameMMVI`
**[data: ICONS.LOD@0x04, GAMES.LOD@0x04]**.
### 1.2 The four wrappers, and how to choose

Sniffing order is exactly `lod::decodeMaybeCompressed` then `magic()` **[donor: LodFormats.cpp:159-167, Magic.cpp:48-63]**:
(1) compressed data (16 B, `91969`+`"mvii"`), (2) image/palette/non-image wrapper (48 B), (3) sprite (32 B + lines +
pixels), (4) font (32 B + atlas + glyphs; in practice nested inside wrapper 2, see §4).

`LodCompressionHeader_MM6` **[donor: LodFormatSnapshots.h:14-20]**:

| off | size | type | field | meaning |
| --- | --- | --- | --- | --- |
| 0x00 | 4 | u32 | version | always `91969`; predicate `version == 91969 && signature == "mvii"` **[donor: LodFormats.cpp:48]** |
| 0x04 | 4 | char[4] | signature | `"mvii"` |
| 0x08 | 4 | u32 | dataSize | payload byte count. **Writer bug:** sometimes equals the whole record size, then the payload is all remaining bytes **[donor: LodFormats.cpp:133-139]** |
| 0x0C | 4 | u32 | decompressedSize | 0 ⇒ payload stored verbatim; >0 ⇒ zlib stream of exactly this many bytes |

`LodImageHeader_MM6` (48 B) **[donor: LodFormatSnapshots.h:23-46; field names cross-checked at [doc: RSLod.pas:554-568]]**:

| off | size | type | field | meaning |
| --- | --- | --- | --- | --- |
| 0x00 | 16 | char[16] | name | entry-ish name, often with a stray extension (`AFRAME1\0TGA\0`) **[data: ICONS.LOD@0x120]**; ignore |
| 0x10 | 4 | u32 | size | `width*height` of the base image; 0 for palette/non-image payloads |
| 0x14 | 4 | u32 | dataSize | stored bytes that follow (excludes the palette) |
| 0x18 / 0x1A | 2 + 2 | u16 ×2 | width, height | 0 for palette/non-image payloads |
| 0x1C / 0x1E | 2 + 2 | i16 ×2 | widthLn2, heightLn2 | `log2(...)` for power-of-two sizes; garbage tolerated |
| 0x20 / 0x22 | 2 + 2 | i16 ×2 | widthMinus1, heightMinus1 | `size-1` for power-of-two textures; **garbage in non-image payloads (observed 15/31)** **[data: ARRUS.FNT]** |
| 0x24 | 2 | i16 | paletteId | index of the matching `palNNN` in `BITMAPS.LOD` (§2) |
| 0x26 | 2 | i16 | anotherPaletteId | 0; runtime palette index **[doc: RSLod.pas:566]** |
| 0x28 | 4 | u32 | decompressedSize | 0 ⇒ pixel block stored; >0 ⇒ pixel block is zlib of exactly this size |
| 0x2C | 4 | u32 | flags | see below |

Flags **[donor: LodFormatSnapshots.h:40-44; doc: RSLod.pas:565-567]**: `0x0002` mipmaps; `0x0100` **not an image**
(decompress, then sniff the payload); `0x0200` palette entry 0 transparent; `0x0400` don't free buffers; `0x0010`
"something important" **[uncertain]**; `0x0001` also seen on mipmapped BITMAPS images **[data: 0x13 ×927, 0x12 ×311]**
**[uncertain]**.

Decision predicates, in effect verbatim **[donor: LodFormats.cpp:40-122]**:

| family | predicate |
| --- | --- |
| compressed | `blob ≥ 16 && u32@0 == 91969 && bytes@4 == "mvii"` |
| palette | every field from 0x10 through 0x2C is 0 **and** `blob == 48 + 0x300` |
| image | `size>0 && dataSize>0 && width>0 && height>0 && size == width*height` and (`decompressedSize==0 && dataSize>=size` or `decompressedSize>0 && decompressedSize>=size`) **and** `blob == 48 + dataSize + 0x300` |
| non-image payload | `size==0 && dataSize>0 && width==0 && height==0 && widthLn2==0 && heightLn2==0 && paletteId==0 && anotherPaletteId==0 && (flags & 0x100) && blob == 48 + dataSize`. **Do not test `widthMinus1`/`heightMinus1`.** |
| sprite | `dataSize>0 && width>0 && height>0 && paletteId>0 && unk_0==0 && emptyBottomLines<=height && blob == 32 + height*8 + dataSize` |
| font | `firstChar < lastChar && field_3 == 8 && height ∈ [4,63] && all other header fields 0` |

**Correction to `local/research/mm7-data-inventory.md`.** Flag `0x100` does not mean "text": in `ICONS.LOD` its 76
`0x100` entries are 42 `.pcx`, 14 `.fnt`, 8 `.txt`, 6 `.str`, 5 `.evt`, 1 `.bin` (75 deflated, 1 stored) **[data]**.
Nor is the `"mvii"` header the only deflate path — sprites, bitmaps and icons carry zlib **inside** the 48-byte header via
`decompressedSize` (§2, §3). Also `local/tools/mm7lod.py:257-263` reads `paletteId`/`anotherPaletteId` from 0x20/0x22
instead of 0x24/0x26, seeing only 61 of those 76 and emitting 15 (12 fonts, 3 PCX) as raw wrapper bytes; the predicate
above finds all 76 **[data]**.

Census per archive **[data]**:

| archive | compressed | image | palette | non-image `0x100` | sprite | unclassified |
| --- | --- | --- | --- | --- | --- | --- |
| `ICONS.LOD` | 0 | 4165 | 0 | 76 | 0 | 0 |
| `SPRITES.LOD` | 0 | 0 | 0 | 0 | 9342 | 1 — `pending`, 160 B, fails every predicate; skip |
| `BITMAPS.LOD` | 0 | 1238 | 376 | 0 | 0 | 0 |
| `Events.lod` | 0 | 0 | 0 | 198 | 0 | 0 |
| `GAMES.LOD` | 152 | 0 | 0 | 0 | 0 | 0 |
### 1.3 SND container

`[u32 count][count × 52 B entry][per entry: u32 storedSize + storedSize bytes]` **[donor: SndSnapshots.h:29-35]**;
`entry.offset` points **past** that per-payload u32 **[data: all 2519 Audio.snd payloads are preceded by a u32 equal to
their size; first payload at headerEnd+4]**. `SndEntry_MM7`: `name[40]`@0x00, `u32 offset`@0x28 (absolute), `u32 size`@0x2C
(stored), `u32 decompressedSize`@0x30. Reject duplicates and `offset+size > fileSize`, and decompress when
`decompressedSize != 0 && decompressedSize != size` **[donor: SndReader.cpp:45-50, 81-94, 128-137]**.
### 1.4 VID container

`[u32 count][count × 44 B entry]`; `VidEntry_MM7`: `name[40]`@0x00, `u32 offset`@0x28 **[donor: VidSnapshots.h:27-31]**.
No size field: sort by offset, size = next offset − offset (last → EOF); offsets are monotonic and `>= headerSize`
**[donor: VidReader.cpp:41-61, 118-131]**.
## 2. Flat images (bitmaps, icons, palettes)

1. `stored = blob[48 .. 48+dataSize]`; `pixels = decompressedSize > 0 ? inflate(stored, decompressedSize) : stored`; require
   `len(pixels) >= width*height` — the header lies, so check the real length **[donor: LodFormats.cpp:206-217]**.
2. Base image = **first `width*height` bytes**, 8-bit palette indices, row-major, stride = width; the rest are mipmaps,
   largest first, halving to 16×16 **[donor: LodFormatSnapshots.h:28-30; data: `A1b` 128×256 inflates to
   43520 = 32768+8192+2048+512]**. All 1238 BITMAPS images carry mipmaps (inflated size > w*h); ICONS: 4131 exact,
   5 mipmapped, 29 stored uncompressed with `decompressedSize == 0` **[data]**.
3. Palette = the **768 bytes at `blob[48+dataSize .. +0x300]`** — after the *stored* block, not the inflated one — 256
   RGB triplets, `color[i] = (b[3i], b[3i+1], b[3i+2])` **[donor: LodFormats.cpp:32-38, 219-220]**. A palette entry is
   the same 48-byte header all-zero plus 768 bytes; 376 in `BITMAPS.LOD`, named `PAL001`…`pal999` **[data]**.
4. `paletteId` is redundant: `A1b` has `paletteId == 132` and an embedded palette byte-identical to
   `BITMAPS.LOD:pal132` **[data]**; the donor always uses the embedded one **[donor: ImageLoader.cpp:63,
   TileGenerator.cpp:93-94]** — keep `paletteId` as a cross-check field only. `zeroIsTransparent = flags & 0x200`
   **[donor: LodFormats.cpp:221]**; the donor maps palette entry 0 to alpha when set, else applies a per-texture colour
   key **[donor: ImageLoader.cpp:66-97]**. The flag is unreliable in the data **[donor: LodImage.h:9-12]**, so carry
   both flag and indices.

API: `DecodedBitmap { int Width, Height; byte[] Indices /* w*h, stride w */; Rgb24[256] Palette; bool ZeroIsTransparent;
int MipLevelCount; uint Flags; }`. The donor's extractor writes one PNG per image and a 256×1 PNG per palette
**[donor: LodTool.cpp:77-84]**. **Nested PCX:** 42 `ICONS.LOD` entries are PCX files inside wrapper 2 (`0x0A`
manufacturer byte at payload offset 0, 8-bit, 1 plane, RLE) **[data: Border2.pcx → 469×109, Drag5.PCX → 640×480]**; after
unwrapping, sniff and hand off to a PCX decoder, exactly as the donor recurses **[donor: LodTool.cpp:70-71, 107-109]**.
## 3. Sprite frames

`LodSpriteHeader_MM6` (32 B) **[donor: LodFormatSnapshots.h:52-65; same as [doc: RSLod.pas:577-585] `TSprite`]**:

| off | size | type | field | meaning |
| --- | --- | --- | --- | --- |
| 0x00 | 12 | char[12] | name | entry name, NUL-padded; ignore |
| 0x0C | 4 | u32 | dataSize | stored pixel bytes (after the line table) |
| 0x10 / 0x12 | 2 + 2 | u16 ×2 | width, height | canvas width; height = number of line records |
| 0x14 / 0x16 | 2 + 2 | u16 ×2 | paletteId, unk_0 | `paletteId` → `BITMAPS.LOD` entry `pal%03d`, must be >0 for the predicate; `unk_0` = 0 |
| 0x18 | 2 | u16 | emptyBottomLines | "yskip": clear lines at the bottom (redundant with the line table) |
| 0x1A | 2 | u16 | flags | runtime only **[doc: RSLod.pas:583]** |
| 0x1C | 4 | u32 | decompressedSize | 0 ⇒ stored; >0 ⇒ pixel block is zlib of this size |

Then `height` × `LodSpriteLine_MM6` (8 B): `i16 begin`@0, `i16 end`@2, `u32 offset`@4 **[doc: RSLod.pas:572-576]
names them L, R, Pos]**. Row `y` gets `pixels[offset .. offset+(end-begin))` copied to column `begin`; `begin == end` is
an empty row; all other canvas pixels are 0 **[donor: LodFormats.cpp:248-271]**. Validate `0 <= begin <= end <= width`
and `offset+(end-begin) <= len(pixels)`. Result: `{ Width, Height, byte[] Indices /* stride width, 0 = transparent */,
ushort PaletteId, ushort EmptyBottomLines, uint Flags }`, coloured from `BITMAPS.LOD:pal%03d`
**[donor: LodTool.cpp:96; doc: RSLod.pas:3775-3812]**; index 0 is transparent **[donor: ImageLoader.cpp:283-291]**.
`paletteId` is only the *default* — recoloured monsters use the frame table's `PaletteId` **[donor: LodSprite.h:7-9]** — so
never bake colours in; emit PNG with the header palette and record the id. 9325 of 9342 sprites deflate their pixel block
**[data]**.

**Frame indexing.** Each entry is one frame image; animation structure is not in the media. The original frame table
(`SFT`; `dsft.bin` events data in the donor **[donor: Engine.cpp:681-682]**) holds per frame `GroupName[12]`,
`SpriteName[12]`, `SpriteIndex[8]`, flags, `LightRadius`, `PaletteId`, `Time` (1/32 s), `TotalTime`
**[doc: 01 common structs.lua:2405-2467]**. LOD entry names are `<textureName><frame-letter><octant-digit>`; the engine
appends the digit `0..7` whenever `textureName` is shorter than 7 chars **[donor: Sprites.cpp:138-151]**. Not all octants
are stored: 2223 `(base, letter)` groups exist, 1518 with 5 frames and 701 with 1, digits 0–4 dominating **[data]**; 5/6/7
are mirrored from 3/2/1 via `SPRITE_FRAME_MIRROR_*` **[donor: Sprites.cpp:107-137, SpriteEnums.h:31-41]**.

**Anchor/pivot.** The sprite binary has none. `SPRITE_FRAME_CENTER` (0x20) z-centres the sprite
(`pos.z -= scale*height/2`); otherwise the **bottom** sits at the object's z, with `emptyBottomLines` as the blank tail
**[donor: SpriteEnums.h:27-28, BaseRenderer.cpp:115-118]** — emit both numbers.

**MM6/MM7/MM8.** The sprite wrapper is one format for all three (single `decodeSprite`) **[donor: LodFormats.cpp:240-274]**.
Frame-table differences: `Images3` (0x10000, only views 0/2/4 stored), `Glow` (0x20000) and `Transparent` (0x40000) exist
only in MM7+ **[doc: 01 common structs.lua:2431-2437; donor: SpriteEnums.h:39-41]**; MM7+ `SFTItem` is 2 bytes wider
**[doc: 01 common structs.lua:2447-2449]**; MM6 ignores the palette-number argument **[doc: 01 common structs.lua:2883]**;
MM8 file-entry records are 76 B vs 32 B (§1.1). `d3dsprite.hwl`/`d3dbitmap.hwl` are pre-converted D3D texture caches the
donor deliberately ignores **[donor: PathResolver.cpp:16-17]** — skip them, the LODs are authoritative.
## 4. Fonts

All 14 `ICONS.LOD` `.fnt` entries are `0x100`-wrapped and zlib-deflated **[data]**; the donor decompresses before
`decodeFont` **[donor: GUIFont.cpp:37, LodFormats.cpp:287-289]**. Decompressed layout: 32-byte header + atlas + glyphs
**[donor: LodFormatSnapshots.h:79-124; doc: 01 common structs.lua:3034-3046]**:

| off | size | type | field | observed |
| --- | --- | --- | --- | --- |
| 0x00 | 1 | u8 | firstChar | 31 for 13 fonts, 30 for `COMIC.FNT` |
| 0x01 | 1 | u8 | lastChar | 255 |
| 0x02 | 1 | u8 | field_3 | always 8 (depth) |
| 0x03 / 0x06 | 2 each | u8[2] | field_4/5, field_7/8 | always 0 |
| 0x05 | 1 | u8 | height | 14…30 across the 14 fonts |
| 0x08 | 4 | u32 | paletteCount | always 0 |
| 0x0C | 20 | u32[5] | palettes | always 0 (were pointers) |
| 0x20 | 4096 | — | atlas (MM7) | 256 × (`i32 leftSpacing`, `i32 width`, `i32 rightSpacing`) then 256 × `u32 glyphOffset` |
| 0x20 | 1280 | — | atlas (MMX) | 256 × `u8 width` then 256 × `u32 glyphOffset`, no bearings; only `calig.fnt` |

Glyphs are **1 byte per pixel**, packed in character order at `glyphOffset[c]`, `height*width` bytes, values 0
background / 1 shadow / 255 text **[donor: LodFont.h:53-60]**. Verified on `ARRUS.FNT`: 44598 = 32 + 4096 + 40470 and
`Σ height*width` over chars 31…255 = 40470, offsets monotone **[data]**. **Skip fonts in v1** — the engine renders its own
UI text; if emitted later, a 16-column glyph-atlas PNG plus metrics JSON **[donor: LodTool.cpp:24-50]**.
## 5. Sound banks (`SOUNDS/Audio.snd`)

Container §1.3; 2519 entries, all single-stream zlib **[data]**. Every payload is a complete RIFF/WAVE:

| property | value |
| --- | --- |
| `fmt ` tag | **17 (0x11 = IMA/DVI ADPCM)**, `cbSize = 2` — all 2518 RIFF payloads **[data]** |
| rate | 22050 Hz everywhere |
| bits / block align | 4 bits; 512 B blocks mono (2506 entries), 1024 B stereo (12) |
| samples per block | 1017 mono (`(512-4)*2+1`); read `fmt` bytes 36..37 instead of assuming |
| chunks / storage | `fact` (exact sample count, e.g. 13611 → 0.617 s) then `data`; container zlib only (1 raw payload, 2518 deflated) |

Playable PCM requires decoding IMA ADPCM (each block: `i16` predictor, `u8` step index, `u8` reserved, then 4-bit nibbles,
2 samples/byte) or delegating: `ffprobe` reports `adpcm_ima_wav, 22050 Hz, 1 ch, 4 bits` and `ffmpeg -i in.wav -f s16le`
decodes it **[data, ffmpeg n9.0.1]**. Emit the decompressed RIFF as `.wav` (fidelity) and transcode to 16-bit PCM/OGG only
if the product needs it. Corruption is real: entry 3 `02Flame01` fails zlib inflate ("invalid distance too far back")
**[data]**; the donor falls back to a raw-deflate best-effort inflate without the Adler check and warns
**[donor: SndReader.cpp:81-93, Compression.cpp:60-90]** — skip and record unreadable samples, never abort the archive.
## 6. Video (`Anims/*.vid`)

Container §1.4. Payloads are self-contained movies: `Might7.vid` = 162 × `SMK2` (Smacker); `Magic7.vid` = 13 × `BIKf`
(Bink 1) + 1 × `SMK2` **[data]**. Both codecs are proprietary and **no donor decodes them**: `VidReader` only lists and
slices entries, playback goes through libav/ffmpeg over the sliced bytes **[donor: MediaPlayer.cpp:757-758,
VidReader.cpp:80-89]**; `ffprobe` reads a sliced `SMK2` as `smackvideo` 460×344 + `smackaudio` and a sliced `BIKf` as
`binkvideo` 320×240 + `binkaudio_rdft` **[data]**. **Verdict: out of scope for the C# extractor** — viewable output needs
ffmpeg (entry → `.smk`/`.bik` → mp4/webm), and the product excludes reproducing the original's videos
**[docs/gameplay-design.md:432]**. The 26 `Music/*.mp3` files need no decoding.
## 7. Repository constraints and recommended artifacts

Already fixed: `local/` is git-ignored (`/.gitignore:1`), so artifacts stay there; original game data is operator-supplied
and never committed, with provenance kept for anything checked in **[src/MightAndMagic7.Import/README.md:24-27]**; imported
media is "a local development convenience with recorded provenance, never a shipped artifact"
**[docs/gameplay-design.md:432]**, owned by the importer/pack layer **[docs/code-organization.md:118]**. Re-implement from
this spec; do not port donor C++.

Emit (1) **one PNG per decoded unit**, RGBA8, index 0 → alpha 0 only where the data says transparent (sprites always,
images per `flags & 0x200`), colours from the embedded palette (images) or `pal%03d` (sprites); (2) **one JSON manifest**
(`local/extracted/media/media-manifest.json`) whose run header carries the source install identity (release/build, e.g.
GOG build id + `MM7.exe` sha256) and the decoder version; (3) never copy raw entry bytes into the repository.

Identity scheme, extending the importer's declared form `ArchiveName:EntryName`
**[src/MightAndMagic7.Import/Lod/LodSource.cs:15-24]**: `id = mm7:<archive>/<entryName>` with the name verbatim-case (plus
`#<octant-digit>` for a sprite frame). Manifest fields per artifact: `id`, `sourceRelease`, `sourceBuild`, `archive`,
`entryName`, `entryOffset`, `entrySize`, `entrySha256`, `wrapper` (`compressed|image|palette|nonImage|sprite|font|pcx|raw`),
`payloadDeflated`, `pixelDeflated`, `kind` (`bitmap|palette|sprite|font|pcx|other`), `width`, `height`, `mipLevels`,
`paletteId`, `paletteSource` (`embedded|palNamed`), `zeroIsTransparent`, `emptyBottomLines`, `flags`, `outputPath`,
`outputSha256`. Store names as on disk (`ArrowA0`, `pal132`, `Border2.pcx`); archive lookup is case-insensitive
**[donor: LodReader.cpp:113]**, so downstream references key on the manifest `id`, never a re-normalized name.

Acceptance tests, all reproducible here **[data]**: the census in §1.2; `A1b` → 128×256 with a palette byte-identical to
`pal132`; `AFRAME1` → 69×91, `flags == 0x200`; `ARRUS.FNT` unwraps to 44598 bytes with `Σ height*width == 40470`;
`Audio.snd` → 2519 entries, exactly one payload undecodable; `Might7.vid` → 162 entries, `Magic7.vid` → 14.

## 8. Measured corrections from the extractor

The extractor in `src/MightAndMagic7.Import/Media/` decodes all five hundred thousand-odd media
entries twice and produces byte-identical output. Five claims in the body of this document did not
survive that contact with the data, plus one thing the data does that the document never mentions.

1. **§2.2, mipmaps.** The chain does not "halve down to 16×16". All 1238 `BITMAPS.LOD` images and the
   5 mipmapped `ICONS.LOD` images inflate to a fixed four-level chain: base plus three halvings.
   `SKY01` at 256×256 inflates to 87,040 = 65,536 + 16,384 + 4,096 + 1,024 and ends at 32×32;
   `solid01` at 16×16 inflates to 340 and ends at 2×2. The decoder measures the chain from the block
   instead of assuming a floor.
2. **§2, nested PCX.** These are not all 8-bit single-plane. 39 of the 42 are three-plane 24-bit:
   `Border2.pcx` declares 8 bits per pixel with 3 planes and 470 bytes per line for a width of 469,
   and RLE-decodes to 153,690 = 109 · 3 · 470. Only `GryLite2.pcx`, `GryLite3.pcx`, and `maketop.pcx`
   are single-plane, and those are the ones carrying a `0x0C` palette tail. Both shapes are decoded.
3. **§6, music.** The installation has 19 `Music/*.mp3` files, not 26.
4. **§5, wave format.** The samples-per-block field is at `fmt` + 18 (file offsets 38–39, measured
   1017); offsets 36–37 are `cbSize`.
5. **§3, sprite frame groups.** The census in the body does not reproduce under either grouping of a
   name without its last character. Measured: 2542 groups as 1555 five-frame and 862 one-frame groups
   case-sensitively, 2505 groups case-insensitively. The grouping key is under-specified, which is why
   sprite animation semantics stay out of scope rather than being guessed at.
6. **Unresolved palette references.** Four sprites — `Swptree1` to `Swptree4` — name palette 940, which
   exists in no archive. They are emitted with an identity grey ramp and `paletteSource: unresolved`,
   so the gap is visible in the manifest rather than hidden behind a plausible-looking palette.
