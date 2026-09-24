# MightAndMagic7.Import.Tool

Planned operator-facing command line that drives `MightAndMagic7.Import`:
inspect a source file, import an operator-supplied game installation, and write
normalized packs under `content/`.

Boundary rules:

- It is a product of its own and calls the import library; it holds no format
  knowledge of its own and no runtime gameplay code.
- It is built by `scripts/verify.sh`, so a change to the import API cannot leave
  the tool silently broken.

Implemented: `info`, `list`, `report`, `verify`, `maps`, `media`, and `write` (see the root README).
