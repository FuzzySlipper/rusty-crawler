# MightAndMagic7.Import

Planned home of offline knowledge about the original Might and Magic VII data
files and about the donor projects that document them. The extraction source is
the operator's own installation at
`/home/research/old-games/game-mm7`; extracted data stays in ignored
`local/` paths and is never committed.

Owns, once implemented:

- Source formats: the original archive, rule-table, map, sprite, sound, and image
  formats, with the structure layouts this repository relies on.
- Conversion quirks: the special cases that make an otherwise regular file
  differ, recorded where the conversion happens rather than in runtime code.
- Provenance: where each imported artifact came from — game, release or build,
  source file, and what was transformed.
- Differential validation: comparisons against the donor reimplementation so an
  import bug is caught offline rather than in gameplay.
- Normalization: emitting the packs that runtime code consumes, in the shapes
  [`../../docs/code-organization.md`](../../docs/code-organization.md) fixes.
- Walk-in entrances: for every travel link whose event the source map raises on a face, the reach that
  face gives the party to walk into — the face's own centroid and extent, with the model, face, event and
  attribute it came from — and, per link, the reason when there is none (an event no face raises, a later
  instruction of an event whose first move is another link, or a move the world itself issues).
- Collision geometry: one artifact per place in the engine's own spatial document,
  built from the solid faces the map decoders already resolve — portals, ethereal
  faces and degenerate corners excluded, outdoor terrain tiled from its height
  field — and refused whole for a place that cannot be closed enough for a party
  to stand on. The document's shape and what it must contain are in
  [`../../docs/research/mm7-map-formats.md`](../../docs/research/mm7-map-formats.md) §8.

Boundary rules:

- This project is not a runtime dependency. No other project references it, and
  it must not reference the runtime projects.
- Original game data stays operator-supplied. Never commit it, never copy
  converted game data out of a donor, and keep attribution and provenance with
  anything checked in.
- Donor projects are behavior and format references, not an architecture
  template. Do not port their C++ topology, build system, or globals, and do not
  translate donor code (see the licensing posture in `AGENTS.md`).

Implemented: every container decodes, the rule tables, event programs, place graph,
and all 76 map payloads reproduce the recorded inventory, media extraction writes its
manifest, and `write` emits the content packs — each place's collision artifact and
the reaches a walking party can take its transitions through included. The source-format shapes are recorded in
[`../../docs/research/mm7-data-inventory.md`](../../docs/research/mm7-data-inventory.md),
[`mm7-map-formats.md`](../../docs/research/mm7-map-formats.md), and
[`mm7-media-formats.md`](../../docs/research/mm7-media-formats.md). The Python
extractors under ignored `local/tools/` are research tools, not this project.
