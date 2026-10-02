# MightAndMagic7.Import.Tool

The operator-facing command line (`mm7import`) that drives `MightAndMagic7.Import`:
inspect a source file, check an operator-supplied game installation against the
recorded inventory, and write normalized packs under `content/partyrpg/imports`.

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
two commands to see them, and `verify` checks each of them. Its `creatures` block states the maps' own creature
records — the 703 actor records that name no person, all placed as `actor` placements in 36 places, 181 of them
held hidden, none refused, 35 in the Temple of Baa — which `verify` checks too; packs written before this block
existed lack the levels' own creatures and must be rewritten. The summary's `entrances` block states every travel
link's disposition — 193 links: 176 used (2 of them a house's door whose event also moves the party), 11 walked, 1
either, 3 spoken (the global program's moves a person's topic raises), 2 unreachable; 76 under a condition, each quest
bit a condition compares named with what raises its writers — and the 1,023 plates a party treads on in 40 places
(every plate whose event no counter or container answers for), each link with its trigger, condition and evidence
([`../../docs/evidence/travel-links.md`](../../docs/evidence/travel-links.md)); `verify` checks the counts. Packs
written before travel events became fixtures and floor triggers still turn every clicked travel face into a
walked-into reach and carry no event for it, and packs written before every plate became a floor trigger carry only
the 59 plates' events that move the party and no summoning's slot, so both must be rewritten. The summary's `fixtures`
block states the fixtures written (1,268 in 76 places over 653 events), the floor triggers (168, of which 59 move the
party), the plate events another emitter answers for (none) or the program lacks (9), the raised events another emitter answers for, the discovery
notes (186), the history lines (28), and how many steps of each kind the fixtures' and floor triggers' events hold, which is the split the ruleset's
interpretation is measured against. The `globalEvents` block states the global program carried (446 events), the ones a
person's topic raises (365, 68 of them checking whether their topic is offered), its moves (3), and the step kinds of the
raised ones; the `people` block states the people a map's own record holds hidden (`peopleHeldHidden`, 1), and its topics are now a person's slots (416 over 300 people), each answered by its event, and
the topic table carries 446 rows; the `fixtures` block's `houseEvents` (47) are the houses' own events a house's use
runs, and the greeting table carries 205 rows. `verify` checks these counts. Packs written before the global program was carried
offer only the topics the table gives text to and run none of them, and packs written before the houses' own events and
the greeting table were carried open every house without running its event, so they must be rewritten; packs
written before a `class` step named its class (`which`) cannot have a promoter's topic raise a rank, and packs written
before an `npc-set-item` step carried its person and item refuse it, so they must be rewritten too.
