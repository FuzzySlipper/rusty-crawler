# World interaction and combat changes in place

Reading of rusty-crawler#9219, 2026-10-02 (America/Los_Angeles), captured the same way as the [world rendering
reading](world-rendering.md). Each crew-services browser session started its own `rusty dev --live-debug` host, and every
image is the Engine runtime's own frame beside the DOM panel. Screenshots stay in `local/evidence-9211/`.

## A lever and its door part

This was a **staged live check**: the hand-written ignored `render-check-scenario` started the party at Barrow VI's
`Party Start`, served through the diagnostic `partyrpg-default` bundle. The bundle file was restored afterwards.

- `20-barrow-lever-before.png` — the party faced the wall niche with ordinary **Q/E**. `interaction.inspect` reported
  `Lever` at 207 units as visible, ready and selected; its handle is up in the niche.
- `21-barrow-lever-after.png` — after **G**, the same frame shows the handle moved down. The lever's door part was
  rebuilt where the interaction ledger's door state puts it. Nothing else in the frame changed, and no debug command
  was used.
- `22-lever-after-resume.png` — the party saved with **F**. A new host started with `RUSTY_CRAWLER_START=resume` (the
  panel reads `Party resumed`). Approached from the same pose, the handle is still down: the save carried the ledger
  state and the rebuilt scene read it.

The second door that `interaction.inspect` listed (`A door`, 270 units, 45° above the eye) could not be selected
past the nearer lever with the ordinary reticle. It was not forced with a target command.

## A blow and what it showed

**Ordinary new game** (default launch, default party accepted). On the road from the dock the party walked with
**W** to a townswoman and struck with **B**. Before the blow the fight panel reported `Nobody is hostile`.

- `23-townswoman-before-blow.png` — she stands in the meadow, drawn standing.
- `24-townswoman-struck-burst.png` — the first frame after Roderick's blow (the panel shows him `recovering 21.5s` and
  `Peasant — down at 332 — 0/6 hp`). She shows her hit group (arms thrown out), and red particles fly out from
  three-fifths of her look's height.
- `25-townswoman-falling.png` — the next frame shows her dying group as she falls.
- `26-townswoman-body.png` — her body lies where she stood.
- `27-strike-sequence-sheet.png` — the whole sixteen-frame sequence, including the walk on past her.

An earlier run, before the burst was given velocity, showed the same flinch with a single stationary red block. The
review found that the pinned Engine's particle descriptor does carry `VelocityMin`, `VelocityMax` and `Acceleration`,
so the burst now uses them.

**Staged Barrow IV** (`mm7-loot-scenario`, same diagnostic bundle, restored afterwards):
`28-barrow-skeleton-swings-sheet.png` shows a Skeleton Warrior coming through the doorway and alternating between its
melee group (sword raised and swung) and standing as its blows against the party resolve. The scenario's forty
creatures still overwhelm a level-one party within seconds, so no frame of a party blow landing there was framed.

**A learned spell** (read 2026-10-03, after the review found no live cast). The hand-written `rc-spell-scenario` started
a scenario party in Barrow III, through the same diagnostic bundle (restored afterwards): Aelina, a Sorcerer who knows
Fire Bolt at expert Fire, and Borin, a Knight. The world was held between steps (`assist time action-driven`), the
party turned with the ordinary turn, and every cast went through the spellbook (**L**, the Fire page, Fire Bolt, the
target picker, Cast).

- `36-spell-rat-before.png` — a Giant Rat on the floor ahead (`Giant Rat — … at 545 — 6/6 hp`).
- `37-spell-book-aimed.png` — the book aimed at it. The picker lists foes nearest first with the fight's own distance
  (`Giant Rat · 545 away` in this build); before this it listed every creature in the place unordered, so the nearest
  of twenty same-named rows could not be told apart. The review then found the exact distance went stale in the kept
  magic block, so the product now publishes the 250-unit step a target stands within (`Giant Rat · within 750`), which
  the block's key reads.
- `38-spell-struck-burst.png` — the frame after the cast: `Aelina hits Giant Rat (spell): 19 Fire damage landed; Giant
  Rat is at 0/6, and is down.` A pale blue burst marks the rat.
- `39-spell-burst-spreads.png` — the burst spreads; the panel shows Aelina recovering and the rat down at 545.
- `40-spell-rat-dying.png` — the rat in its dying group.
- `41-spell-missed.png` — an earlier cast at the same rat missed (`the hit roll was 4483 against a 44.12% chance`). That
  run showed the same blue burst a hit does, so a miss read as a hit. A missed spell now leaves the small grey puff a
  missed blow does (`ScenePolicyTests`). This frame, from the fixed build, shows no blue burst; the grey puff is too
  small to make out at this distance.
- `42-spell-sequence-sheet.png` — the cast's sequence.

Everything shown is read from the fight's own record (`CombatState.RecentBlows`): which actor struck, which target,
and whether the blow landed. Nothing is decided by presentation, and a missed blow shows a grey burst and no flinch.
Unit tests bind both outcomes (`ScenePolicyTests`, `WorldViewTests`, `CombatStateTests`).

## Audit of event steps that change a place's look

The fixture runner passes over steps that only change what a player sees or hears
(`MightAndMagic7Fixtures.PresentationSteps`). Over the operator's current import (`mm7-tables/place-events.json`):

| Step | Steps | Places | Where it goes |
| --- | --- | --- | --- |
| `set-sprite` | 61 | 11 | drawn since #9254 ([reading](world-events.md)) |
| `set-texture` | 50 | 16 | drawn since #9254 ([reading](world-events.md)) |
| `character-animation` | 44 | 13 | portrait reactions, the HUD's work, not the world view |
| `set-faces-bit` | 36 | 9 | passable (0x20000000, 19 steps) is kept and collides; invisible (0x2000, 12 steps) is drawn since #9254; fluid (0x10, 5 steps) stays a decision |
| `play-sound` | 7 | 2 | no sound in the product |
| `toggle-indoor-light` | 7 | 2 | passed over by decision: every shipped interior light has radius zero (#9254); its switch moves to #9267 |
| `show-movie` | 2 | 1 | no video in the product |

## Limits

- In these frames the top message line can show a creature's later blow instead of the cast's own result (`37`–`41`).
  Since #9226 the line is the answer to the party's latest act ([reading](combat-rest-feedback.md)).
- Fidget groups are not driven.
- Door faces keep their texture coordinates as the door moves.
- No frame-rate claim follows from these captures.
