# MightAndMagic.Import

Planned home of offline knowledge about the original Might and Magic VI/VII/VIII
data files and about the donor projects that document them.

Owns, once implemented:

- Source formats: the original archive, map, sprite, sound, text, and table
  formats, with the structure layouts this repository relies on.
- Conversion quirks: the special cases that make an otherwise regular file
  differ, recorded where the conversion happens rather than in runtime code.
- Provenance: where each imported artifact came from, which game and version,
  and what was transformed.
- Differential validation: comparisons against the donor reimplementation so an
  import bug is caught offline rather than in gameplay.
- Normalization: emitting the packs that runtime code consumes.

Boundary rules:

- This project is not a runtime dependency. No other project references it, and
  it must not reference the runtime projects.
- Original game data stays operator-supplied. Do not commit it, and keep
  attribution and provenance with anything checked in.
- Donor projects are behavior and format references, not an architecture
  template. Do not port their C++ topology, build system, or globals.

Nothing is implemented yet.
