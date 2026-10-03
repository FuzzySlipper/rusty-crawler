# Map events that change how a place looks

Reading of rusty-crawler#9254, 2026-10-03 (America/Los_Angeles), captured the same way as the [world rendering
reading](world-rendering.md). Each crew-services browser session started its own `rusty dev --live-debug` host, and every
image is the Engine runtime's own frame beside the DOM panel. Screenshots stay in `local/evidence-9211/`. Both checks are
**staged**: the hand-written ignored `render-check-scenario` starts the party at the place's `Party Start`, served through
the diagnostic `partyrpg-default` bundle (restored afterwards). Each resume used the ordinary default bundle
`mm7-new-game` with `RUSTY_CRAWLER_START=resume`. Routes were walked with held keys, steered from observed poses along
waypoints read from the place's collision layout (coordinate-assisted navigation). Every use was the ordinary **G**.

## What the import writes

Over the operator's current import:

- Every `set-texture` (50 steps) and `set-sprite` (61 steps) carries its cog and name, and every name resolves: each
  texture to a material of its place, each decoration name to a look. Decorations an event gives were added to the looks.
- 433 faces of event-switched cogs that were dropped as invisible are now kept, in parts of their own.
- Every map decoration's row is resolved by name, as the donor does on load. Before this, every interior decoration
  carried row 0 and none was drawn; 3,309 now draw (torches, fires, urns).

## A decoration taken: Lord Markham's Manor

- `30-manor-urn-before.png` — the fireplace niche: the log fire and, on the mantel, the urn (`dec97`, cog 2), both
  interior decorations drawn for the first time. `interaction.inspect` reported `A fixture (dec97)` visible, ready and
  selected. Before decoration targets were aimed at their own middle and judged with their look's radius as the
  Engine's endpoint tolerance, the reticle aimed at the urn's base on the mantel and reported it occluded from every
  distance.
- `31-manor-urn-taken.png` — after **G**: the urn is gone from the mantel. Its event gave its item, hid cog 2's
  decorations (`set-sprite` off) and cast its trap: Roderick fell from 40 to 18 hit points. The urn is no longer offered.
- `32-manor-urn-after-resume.png` — after **F** and a resume in a new host (`Party resumed`, Roderick still 18/40):
  the mantel stays empty.

## A face retextured: White Cliff Cave

- `33-cave-vein-before.png` — the ore vein's face on the left wall, gold-veined rock (`fixture-198`, visible, ready).
- `34-cave-vein-mined.png` — after **G**: the same face is plain rock (`set-texture` cog 4 → `cwb1`) in place. The
  switched cog's part was rebuilt with the place's `cwb1` material.
- `35-cave-vein-after-resume.png` — after **F** and a resume in a new host: the face is still plain rock.

## What the resume found

The first resume was refused: `place '17' keeps 'decoration-shown:2' = 0, and no fixture of this game keeps a value of
that name`. A save carrying the new names could not be loaded. Every switch name is now judged on load, and the resumes
above load it.

## Decisions and limits

- `toggle-indoor-light` is passed over. All 3,154 interior lights the import reads have radius zero, which the donor's
  sector lighting reads as reaching nothing (OpenEnroth `src/Engine/Graphics/Lighting.cpp:133-151`). What lights a
  dungeon in the donor are its decorations' stationary lights, which this product does not draw.
- A retextured face keeps its texture coordinates divided by its original bitmap's size. A replacement of another size
  is stretched; the donor divides by the current bitmap's size.
- A door keeps its own faces whatever their cog, so a `set-texture` or invisible-bit step on a cog a door moves is
  kept but not drawn; an invisible face a door moves stays undrawn. A global or topic event that switches a cog its
  place's own program never names finds that cog's faces not split out, and changes nothing drawn.
- Keeping a retextured face and a decoration's new row in the save is ours. The donor keeps face attributes and
  decoration visibility in its map delta, and re-runs a vein's on-reload step.
- **No live capture of a face group hidden or shown.** The nearest such event is 8,500 units into the Erathian Sewers,
  among eighteen thieves, and the sewers have no start point a staged check can use. The hide path is the same switch
  part rebuilt by the retexture above, published not-visible (`WorldViewTests`), and its ledger value is kept and judged
  (`FixturePolicyTests`).
