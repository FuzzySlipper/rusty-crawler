# MightAndMagic7.Import.Tool

Planned operator-facing command line that drives `MightAndMagic7.Import`:
inspect a source file, import an operator-supplied game installation, and write
normalized packs under `content/`.

Boundary rules:

- It is a product of its own and calls the import library; it holds no format
  knowledge of its own and no runtime gameplay code.
- It is built by `scripts/verify.sh`, so a change to the import API cannot leave
  the tool silently broken.

Implemented: `info`, `list`, `report`, `verify`, `maps`, `encounters`, `media`, and `write` (see the root
README). `encounters` is the read-only half of the monster import: it decodes the maps, reads every actor
spawn through its map's encounter slots and the monster table, and prints what a write would emit — the
encounters per place, the records nothing was emitted for with their reason, and the notes the reading
makes (3,175 spawn records read, 1,800 encounters into 72 places, 43 refused over the operator's own
install). The importer chooses no grade and no count — the ruleset draws both when it populates a place —
so the creatures are stated as the range the slots allow: `fewestCreatures` (1,900, every random slot at
its fewest) to `mostCreatures` (5,458, every one at its most), with `drawnGrades` (1,775) the encounters
whose grade is drawn. `write` states the same counts in its summary so an operator does not have to run
two commands to see them, and `verify` checks each of them.
