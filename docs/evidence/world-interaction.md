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

Everything shown is read from the fight's own record (`CombatState.RecentBlows`): which actor struck, which target,
and whether the blow landed. Nothing is decided by presentation, and a missed blow shows a grey burst and no flinch.
Unit tests bind both outcomes (`ScenePolicyTests`, `WorldViewTests`, `CombatStateTests`).

## Audit of event steps that change a place's look

The fixture runner passes over steps that only change what a player sees or hears
(`MightAndMagic7Fixtures.PresentationSteps`). Over the operator's current import (`mm7-tables/place-events.json`):

| Step | Steps | Places | Where it goes |
| --- | --- | --- | --- |
| `set-sprite` | 61 | 11 | #9254 (the importer writes no operands yet) |
| `set-texture` | 50 | 16 | #9254 (the importer writes no operands yet) |
| `character-animation` | 44 | 13 | portrait reactions, the HUD's work, not the world view |
| `set-faces-bit` | 36 | 9 | passable (0x20000000, 19 steps) is kept and collides; invisible (0x2000, 12 steps) is #9254; fluid (0x10, 5 steps) stays a decision |
| `play-sound` | 7 | 2 | no sound in the product |
| `toggle-indoor-light` | 7 | 2 | #9254 |
| `show-movie` | 2 | 1 | no video in the product |

## Limits

- A spell's blue burst is bound by tests, but no live spell cast was framed.
- Fidget groups are not driven.
- Door faces keep their texture coordinates as the door moves.
- No frame-rate claim follows from these captures.
