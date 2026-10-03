# People, creatures, decorations and bodies in the drawn world

Reading of rusty-crawler#9218, 2026-10-02 (America/Los_Angeles), captured as the [world rendering
reading](world-rendering.md) was: each crew-services browser session started its own `rusty dev --live-debug` host and
every image is the Engine runtime's own frame beside the DOM panel. Screenshots stay in `local/evidence-9211/`.

## What the import wrote

From the operator's install the importer read the sprite frame table (9,221 frames, 2,057 named groups) and the monster,
decoration and object lists (276 monster rows, 228 decorations, 254 object kinds), and packed 1,914 sprite groups into
atlases with 640 looks. Two sprite entries the frame table names are absent from the archive (`sp97b70`, `sp97b80`); the
summary names them and their cells stay empty. The atlases total about 500 MB as RGBA PNG, a size the operator's
regenerated packs carry locally and nothing commits.

## Ordinary new game

- `10-new-game-sprites.png` — the first view from the dock now holds trees, the campfire, rocks, flower beds and two
  townspeople in the plaza, each a billboard cut from its look's atlas, crisp and without colour fringes.
- `11a-ailyssa-faced.png` → `11-ailyssa-speaks.png` — walked with **W** to the well; the observation's facing named
  `Ailyssa the Bard` at 293 units while she stood drawn at the centre of the view, and **G** opened her conversation
  with her greeting and topics. The reticle's target and the drawn person are one placement.
- `15-peasant-body.png` — a peasant beside the road was attacked with held **B**; the fight reported her down, the rest
  of the town backing away, and after a step back the frame shows her body lying where she stood, which
  `interaction.inspect` names `The body of Peasant` at that spot.
- `16-body-after-resume.png` — after the panel's **Save session** (`Saved the session to slot 'session' …`) and a new host
  started with `RUSTY_CRAWLER_START=resume` (`partyStart: resumed`), the same body lies still at the same spot.

## Moving creatures (staged)

The existing hand-written `mm7-loot-scenario` (Barrow IV, served through the diagnostic bundle and restored afterwards)
puts skeletons and giant bats beside the arrival. A five-frame burst taken through the playtest script right after
**Accept party** shows a giant bat in flight (`13-barrow-burst-0s.png`) and, three seconds later, a Skeleton Warrior
come into the doorway and a bat swooping low (`14-barrow-burst-3s.png`); `12-barrow-skeleton-bats.png` is a still from
the first run. The scenario's forty creatures overwhelm a level-one party within seconds, which is that check's own
balance; the fight panel reports the party down.

## Limits

Creatures choose between their standing and walking groups from their own movement; attack and hit groups and the
blow bursts are #9219's ([world interaction reading](world-interaction.md)); fidget groups are not driven. Ordinary-play motion was harder to frame than the staged burst: the
attacked town's people flee, and chasing one walked the party off the dock into water, where the water rule drowned
two members — movement's rule, recorded rather than worked around. Decorations that events hide or change, and
sprite-swap event steps, are routed by #9219's audit to #9254.
