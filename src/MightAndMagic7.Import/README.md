# MightAndMagic7.Import

Planned home of offline knowledge about the original Might and Magic VII data
files and about the donor projects that document them. The extraction source is
the operator's own installation at
`/home/research/old-games/Might and Magic 7`; extracted data stays in ignored
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

Boundary rules:

- This project is not a runtime dependency. No other project references it, and
  it must not reference the runtime projects.
- Original game data stays operator-supplied. Never commit it, never copy
  converted game data out of a donor, and keep attribution and provenance with
  anything checked in.
- Donor projects are behavior and format references, not an architecture
  template. Do not port their C++ topology, build system, or globals, and do not
  translate donor code (see the licensing posture in `AGENTS.md`).

Nothing is implemented yet. A working Python LOD/table extractor exists only as a
local research tool under ignored `local/tools/`; it is not this project.
