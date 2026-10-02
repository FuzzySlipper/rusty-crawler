# MightAndMagic7.Import

Offline knowledge about the original Might and Magic VII data files and about the
donor projects that document them. The extraction source is the operator's own
installation (by default `/home/research/old-games/game-mm7`); what it extracts
stays outside the repository and is never committed.

Owns:

- Source formats: the original archive, rule-table, map, sprite, sound, and image
  formats, with the structure layouts this repository relies on.
- Conversion quirks: the special cases that make an otherwise regular file
  differ, recorded where the conversion happens rather than in runtime code. The
  potion table's own two are recorded in
  [`../../docs/research/mm7-map-formats.md`](../../docs/research/mm7-map-formats.md) §9 with the counts
  they produce: its last thirty-two rows appear twice, the second time with the number column blank, and a
  reagent's one recipe is written as text in its own effect cell.
- Provenance: where each imported artifact came from — game, release or build,
  source file, and what was transformed.
- Differential validation: comparisons against the donor reimplementation so an
  import bug is caught offline rather than in gameplay.
- Normalization: emitting the packs that runtime code consumes, in the shapes
  [`../../docs/code-organization.md`](../../docs/code-organization.md) fixes.
- Travel: a travel link is one move of a map event, and it is taken the way the donor raises that event. A clicked
  face group is a `fixture` like any other (below), and its event's move step names the link it takes (`link`,
  `toPlace`, and `travel`: `walking` between two regions, `entrance` otherwise) — or `withinPlace` with the
  position for a move that stays in the place. Every pressure plate is a `place-entrance` reach — the plate's own
  centroid and extent, with the model, face, event and attribute it came from — that `raises` its event's
  `floor-trigger` placement (`trigger-<event>`, at the plates' mean), whatever the event does: a move, a trap's spell
  or harm, an ambush, an alarm, a door shut behind the party. Only an event a counter or a container answers for, or
  one the program lacks, is left out, and both are counted (none and 9 over the operator's install). Nothing
  here chooses a move: the ruleset runs the event and its branches decide (`PlaceEntranceEmitter`). Every link
  of the place graph ends with a disposition written beside it in `place-graph.json` and in the write summary
  (`used` — a house's door whose event also moves the party among them —, `walked`, `used-or-walked`, `spoken` — a
  global program's move a person's topic raises —, `world-issued`, `unreachable`), with the condition a run must meet to reach its move read from the event's branches
  (`PlaceEventPaths`) and who sets each quest bit it compares, with what raises each of those writers: the faces,
  plates, decorations or house door of its map, its map's own trigger, or the person whose topic raises it;
  the table over the operator's install is [`../../docs/evidence/travel-links.md`](../../docs/evidence/travel-links.md).
- Fixtures and their events: every clicked face group and decoration whose event no other emitter answers for
  (a building, a container, a door) as a `fixture` placement over its faces at their lowest corner, and every such event
  — with the timers that refill what it gives — as a `place-event` entry of normalized steps: the donor's own
  instruction word, the operands the instruction carries, the variable named by family and slot, and the line a
  status step prints read from the map's own string table; a door step's door id and action, an item gift's
  level, its random kind read as the same two item tags a treasure cell is (`ItemVocabulary.FilterOfRandomItem`)
  and its item, a cast's spell, mastery and rank, a person call's or topic change's person, and a flag toggle's
  group and bit, a person move's person and house, a greeting change's person and row, and a house step's house are
  among those operands. A summoning's encounter (its slot
  plus three times its grade), count, point, group and unique name are too, and its slot is read from the place's
  map table row by the reading a spawn record's encounter gets (`PlaceEncounters.Slot`) and written beside it as
  `summons`, so the ruleset resolves its creatures as it resolves an encounter's. A house's own event that does more
  than open the house (`PlaceFixtureEmitter.DoesMoreThanOpen`: a door that also moves the party, a shop a quest bit
  shuts, the arbiter's door) is carried too, marked `house`, for the house's own use to run — its placement names it as
  `sourceEvent` — and is never a fixture. The greeting table (`npcgreet.txt`) is written as `person-greeting` entries by
  row, which a greeting change names. The discovery table is written beside them as
  `discovery` entries, and the history table (`history.txt`) as `history-line` entries keyed by the slot an
  event's `history` variable names (the table's row less one) with its `%30` and `%31`–`%34` codes written as
  `{date}` and `{member:1}`–`{member:4}` (`HistoryTable`). A person carries their six dialogue slots by position
  (`topicSlots`), and every topic-table row with an answer or a global event of its number is written as a `person-topic` entry whoever owns it,
  which is what a topic change can make a slot raise; a person placement carries its actor record's `group`.
  A step comparing or setting a character's `class` names the class in `which` — the class table's row its value
  numbers (`PlaceFixtureEmitter.Classed`) — so no reader needs the table's order. No step is interpreted here; the write summary counts the steps of each kind, which is
  what the ruleset's interpretation is measured against, and the shapes are recorded in
  [`../../docs/research/mm7-data-inventory.md`](../../docs/research/mm7-data-inventory.md) (*Fixtures, map
  events, and the discovery table*).
- The global program (`GlobalEventEmitter`): every event of `global.evt` as a `global-event` entry, in the steps a
  place's events are written in, with whether a person's topic raises it (`topic`: a slot `npcdata.txt` states, or one
  a `set-npc-topic` step of any program names). A message's line is the topic text table's row of its number, the
  program having no string table of its own; a topic's offer check (`can-show-dialog-item-compare`,
  `set-can-show-dialog-item` with `on`, `end-can-show-dialog-item`) is written like any step, as is a greeting change
  (`set-npc-greeting`, its person and `greeting` row); a move is a link the world issues, `scripted`. A person's topics
  are their slots — the topic table's row of each slot's number, with its first text and its `event` when the program
  holds one — and a row with neither, or a slot naming no row, is refused with its reason.
- The automap raster: one `place-map` entry per place — a region on its own terrain grid at its own
  512-unit pitch with one height band per square, an interior on this importer's own 128-unit grid with a
  square marked wherever one of the level's own minimap outlines passes through it — which is what the
  product's automap is drawn from and what its map owner remembers a party walking. The layout, and the one
  flip that puts a region's grid in the product's axes, are in
  [`../../docs/research/mm7-map-formats.md`](../../docs/research/mm7-map-formats.md) §9. What the original
  draws instead is coarser here and is marked ours.
- Collision geometry: one artifact per place in the engine's own spatial document,
  built from the solid faces the map decoders already resolve — portals, ethereal
  faces and degenerate corners excluded, outdoor terrain tiled from its height
  field — and refused whole for a place that cannot be closed enough for a party
  to stand on. Beside each artifact the place's named ground: `water` for a region's terrain squares whose tile
  the game's tile table (`dtile.bin`, `TerrainTileTable`) flags as water, and `fluid` for solid faces the level
  marks fluid, each over the same triangles the collision carries. The document's shape and what it must contain
  are in [`../../docs/research/mm7-map-formats.md`](../../docs/research/mm7-map-formats.md) §8.

Boundary rules:

- This project is not a runtime dependency. No other project references it, and
  it must not reference the runtime projects.
- Original game data stays operator-supplied. Never commit it, never copy
  converted game data out of a donor, and keep attribution and provenance with
  anything checked in.
- Donor projects are behavior and format references, not an architecture
  template. Do not port their C++ topology, build system, or globals, and do not
  translate donor code (see the licensing posture in `AGENTS.md`).
- No gameplay choice is made here. A spawn record is written as the `encounter` it asks for — its slot,
  the grade only when the record fixes one, the slot's kind, difficulty and count range, and the variant
  rows — and the ruleset draws the grade and the count when a place is populated; a delta's actor record that
  names no person is written as the `actor` it is (`PlaceCreatures`) — its monster row, group, attributes, AI
  state, point and facing, under the `actors` field and its index in the level's array, with `hidden` set when
  the level holds it hidden (state nineteen or the bit `0x10000`) — and the ruleset stands it, or not; a stable or a dock is
  written as the counter it is — its kind and the placement it stands at — and no passage, destination,
  route or days: which places it sells passages to is the ruleset's fare network and how long they take is
  its tuned fare rule.

Implemented: every container decodes, the rule tables, the discovery table, event programs and each map's
string table, the fixtures and their normalized events, place graph,
the treasure rules the tables carry (a monster row's own cell read into a drop chance, coin dice, a
treasure level and the kind of thing asked for, and the random-item table's 618 weighed rows carried onto
the items they weigh, so the packs state numbers rather than a string every reader would spell again),
and all 76 map payloads reproduce the recorded inventory, media extraction writes its
manifest, and `write` emits the content packs — each place's collision artifact and
the plates a party treads on to travel and every travel link's disposition included. The source-format shapes are recorded in
[`../../docs/research/mm7-data-inventory.md`](../../docs/research/mm7-data-inventory.md),
[`mm7-map-formats.md`](../../docs/research/mm7-map-formats.md), and
[`mm7-media-formats.md`](../../docs/research/mm7-media-formats.md). The Python
extractors in the operator's ignored local tree are research tools, not this project.
