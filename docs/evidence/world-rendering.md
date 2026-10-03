# The drawn world, from the running product

Reading of rusty-crawler#9216 and #9217, 2026-10-02 (America/Los_Angeles). Every image was captured by the
crew-services browser playtest backend from a session that started its own `rusty dev --live-debug` host: the Engine
runtime rendered on the local GPU and streamed its frames into the product page at 1280×720, so each capture is the
Engine's own frame with the DOM panel beside it — no synthetic or offline render. Screenshots stay in the operator's
`local/evidence-9211/` (named below); the packs were written by the current importer from the operator's install.

## What the import wrote

`mm7import write` over the operator's install wrote render geometry for all 76 places — 1,698,677 vertices and 873,201
triangles, 784 door parts, 5,663 faces left undrawn (portals and invisible faces no event shows), none untextured — and
a media pack of 857 world bitmaps, the first-visit sky panorama and 617 interface images. No world bitmap a place names was absent;
five interface names the tables use were (`null`, `axe4`, `lshield3`, `npc116`, `pc2301`), and the summary names them.

## Ordinary new game, outdoors

Default launch (bundle `mm7-new-game · 4 packs`), default party accepted with the panel's **Accept party**:

- `01-new-game-dock-day.png` — the party's first view from the Emerald Island dock at 09:00: the town gate with its
  sign, the cobbled road, houses, the hills and a clouded sky panorama.
- `02-turned-ship-shore.png` — after holding **Q**: the moored ship beside the dock, the sea and the shoreline (shore
  tiles show the water through their keyed texels), the temple on the far hill.
- `03-gate-approach.png` — after holding **W** up the road: the gate's posts and its painted sign close up.
- `04-signpost-obstacle.png` — walking on, movement reported the party stopped at y≈7091; the frame shows the
  crossroads signpost's post filling the view at exactly that spot. The drawn obstacle and the collision agree.
- `09-new-game-dock-night.png` — two **C** camps later (01:03): the sky panorama gives way to a dark clear sky, the
  ground dims, and only what is near the party is lit by the light it carries.

The walk used ordinary held keys. Its heading was steered from `playtest.observe` poses, and a longer route around the
hill was planned offline from the place's terrain heights: coordinate-assisted navigation, not unaided visual
discovery. Falls on that route cost hit points (movement's own rule).

## Entrances and interiors

- `05-dragon-cave-mouth.png` — the Dragon's Lair mouth's rock faces. Its use face is 344 units ahead but above eye
  level behind rock, so `interaction.inspect` reports it occluded and the ordinary **G** cannot reach it; the product
  has no look-up control. That is reachability, not drawing, and it is routed to the opening-loop work rather than
  worked around here.
- `06-temple-interior.png` — a **staged live check** (a hand-written ignored scenario starting at the Temple of the
  Moon's own `Party Start`, played through the diagnostic `partyrpg-default` bundle with `RUSTY_CRAWLER_BUNDLE`; the
  bundle file was restored afterwards): the cave passage's stone faces lit near the party and falling into darkness.
- `07-temple-exit.png` — walked with **W** to the dark exit face; `interaction.inspect` reported it visible, ready and
  selected.
- `08-returned-hilltop.png` — **G** took the exit: `Leave the Temple of the Moon leads the party on. The party arrives in
  Emerald Island.` The next frame is the region from the cave mouth on the hilltop — rock framing, the waterfall pool,
  the town below — at the arrival pose the transition gave (15816, 12161, 1133). The place swap drew the new scene and
  released the old one.

## Limits

People, creatures, objects and bodies are drawn by #9218 ([reading](world-objects.md)); interaction and combat
changes are #9219's ([reading](world-interaction.md)), whose audit routed the event steps that change a place's look
to #9254. Since the Engine pair moved forward, distance fades linearly into the background colour through
`CameraView.SetFog`. This is ours: the donor fades only in foggy weather. The fade runs from `view.fog-start` to
`view.fog-end` outdoors and to black indoors (`29-distance-fade-sea.png`: the sea's far edge fades into a pale horizon).
Tone mapping stays off, so the frames read as before. The door faces keep their texture coordinates as the door moves (the donor slides them). No GPU
timing or frame-rate claim follows from these captures.
