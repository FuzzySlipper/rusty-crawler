# Walking into a transition, from the running product

> Published copy of a point-in-time live-check record kept in the operator's ignored local verify tree.
> It is evidence of what was observed then, not a statement of current behaviour. Screenshots, scripts,
> logs and saves it names were not published; LAN addresses are replaced by `<lan-address>`.

This directory records the live walk Den task #8567
asked for: the product served by its real dev runner, driven by a keyboard through the agent playtest
service's remote GPU browser, with the HUD read from screenshots and the product's own travel reports
read from its live-debug diagnostics.

It follows [the earlier walk](walk-playtest.md) (#8566), which proved movement worked and that no walk could
change the place. This walk changes the place — twice, in both directions, through the entrance the
imported pack declares for each place.

## What the trigger is, and where it comes from

A place's exit is a **reach** content declares: a transition, a centre in the place, and a radius. The
world takes the transition on the step that carries the party's pose from outside that reach to inside
it, through `SessionWorld.Travel` — the one transition path, cost rule and all. Standing in a reach is
not entering it, so a party a transition put down inside a cave mouth does not bounce straight back.

The importer derives each reach from the map's own **event face**: a face whose event is the event the
travel link's move belongs to. The donor raises those events when the party steps on the face
(`FACE_PRESSURE_PLATE`, OpenEnroth `src/Engine/Graphics/Outdoor.cpp:966-980`,
`Indoor.cpp:1491`) or clicks it (`FACE_CLICKABLE`, `Indoor.cpp:1397-1416`); the product's walk-in is the
adaptation the task asked for, and the reach is the smallest ball holding the face — its centroid and
its own extent — so both numbers come from the face rather than from a position this work chose.

`write` emitted **532 reaches for 155 of the 193 travel links across all 76 places** (203 of them faces
the donor trips by stepping on them, 328 by clicking), and named every link that has none:

| links | count | why nothing can be walked into it |
| --- | --- | --- |
| 155 | one or more reaches | the source map carries a face raising the link's event |
| 33 | `conditional-step` | a later instruction of an event whose first move is another link; only an event interpreter (not built) can select it |
| 2 | `no-event-face` | links 5 (Castle Gloaming) and 51 (The Pit): no face in the source map raises events 502 and 505 |
| 3 | `world-issued` | links 68, 69, 70 come from the global program: arrivals with no place to walk into them from |

Place 1's two links carry no source-side geometry of their own — their `x, y, z` is the *destination*
pose inside the Temple of the Moon and The Dragon's Lair — so nothing was derived from them. Their
reaches come from the two event faces the region does carry:

* link 137 → the Temple of the Moon: two faces of model `CaveFall1a` (event 101), centres
  `15888, 12166, 1174` (r 135) and `15960, 12168, 1344` (r 165);
* link 138 → The Dragon's Lair: face 1730 of model `DragonCave1_E` (event 102), centre
  `13760, 16394.9, 352.6`, radius 305.3.

## What was run

```sh
# 1. import the packs (the importer was rebuilt with the entrance emission)
dotnet run --project src/MightAndMagic7.Import.Tool -- write /home/research/old-games/game-mm7 --output /tmp/mm7-entrance-check
cp -r /tmp/mm7-entrance-check/mm7-tables /tmp/mm7-entrance-check/mm7-world content/partyrpg/imports/   # ignored path
#    bundle.json edited in place to name mm7-tables, mm7-world, mm7-live-scenario, then restored
#    (git status clean afterwards; a fresh checkout is unaffected)
#    the live scenario (ignored, hand-written) was pointed at place 52, The Dragon's Lair, and restored
#    afterwards; that is staging, not product data

# 2. serve the product with the product's own diagnostics readable (managed background job, never pkill)
./.runtime/runtime-pack/bin/rusty dev --project ./src/PartyRpg.Host/PartyRpg.Host.csproj \
  --runtime ./.runtime/runtime-pack --bind-host <lan-address> --port 4176 --live-debug

# 3. playtest against it (the binary is ~/.local/bin/playtest)
playtest start rusty-crawler
playtest run <session> --file local/verify/walk-transition/walk-9.js  --budget-ms 60000
playtest run <session> --file local/verify/walk-transition/walk-10.js --budget-ms 60000
playtest run <session> --file local/verify/walk-transition/walk-11.js --budget-ms 40000
playtest stop <session>
```

`--bind-host 127.0.0.1` does not work for this browser path (the engine's dev host admits a request only
when the `Host` header equals `<bind-host>:<port>` while the bind is loopback; `rusty-engine`
`rust/crates/product-dev-host/src/host.rs`, `has_admitted_origin`). The GPU box reaches
`http://<lan-address>:4176/` directly on the trusted LAN.

The keyboard only reaches the page while the browser holds focus. A content change reloads the page and
loses it: after the scenario change every key was ignored (a held `P` did not pause the session, and four
800 ms holds in four directions moved the pose nowhere), and a fresh `playtest start` restored it. Any
walk run against a page that was reloaded under it is worthless for this reason — check that a held `P`
pauses the session before trusting a walk.

## The crossings

The scenario starts the party at The Dragon's Lair's own start point `924, 2248, 36`, which is inside the
reach of the lair's exit entrance (`53.381`/`53.382`), so the party first walks *out* of that reach
(backwards, west at facing 0) and then forwards back into it.

| screenshot | what was held before it | Place | HUD name | Position | Explored | Admitted steps |
| --- | --- | --- | --- | --- | --- | --- |
| `a0-before.png` | nothing | 52 | The Dragon's Lair · interior | `924, 2248, 2 @ 0` | 1 / 76 | 3254 |
| `a1-out-of-reach.png` | S 1.0 s | 52 | The Dragon's Lair · interior | `540, 2248, 2 @ 0` | 1 / 76 | 3345 |
| `a2-after-crossing.png` | W 1.5 s | **1** | **Emerald Island · region** | `14229, 16368, 169 @ 1` | **2 / 76** | 3463 |
| `a3-settled.png` | released, +0.7 s | 1 | Emerald Island · region | `14229, 16368, 169 @ 1` | 2 / 76 | 3523 |
| `a4-island-out.png` | W 1.2 s (east, out of the cave-mouth reach) | 1 | Emerald Island · region | `14690, 16370, 98 @ 1` | 2 / 76 | 3625 |
| `a5-island-back.png` | S 2.0 s (west, back into the cave-mouth reach) | **52** | **The Dragon's Lair · interior** | `790, 2228, 1 @ 1012` | 2 / 76 | 3776 |
| `a6-settled.png` | released, +0.7 s | 52 | The Dragon's Lair · interior | `790, 2228, 1 @ 1012` | 2 / 76 | 3835 |
| `b2-crossing-into-island.png` | the same walk, run again | 1 | Emerald Island · region | `14375, 16367, 105 @ 1` | 2 / 76 | 6983 |
| `d0-before.png` | nothing (island, out of the cave-mouth reach) | 1 | Emerald Island · region | `14037, 16359, 170 @ 1` | 2 / 76 | 11065 |
| `d1-west-1.png` | S 0.5 s west into the cave-mouth reach | 1 | Emerald Island · region | `13799, 16367, 169 @ 1` | 2 / 76 | 11125 |
| `d5-settled.png` | released, +0.7 s | 1 | Emerald Island · region | `13799, 16365, 169 @ 1` | 2 / 76 | 11365 |

The arrival pose is the *link's*: `53` carries `13839, 16367, 169` and `138` carries `752, 2229, 1`, and
the HUD shows the party there plus however far the rest of the same key hold walked it (`a2` is 390 units
past the island arrival, `a5` is 38 past the lair's). The explored count follows: the lair is known at the
start (1 / 76), and arriving on the island makes it 2 / 76 — the destination is marked visited by the same
path as any other transition. The pose's `z` follows the destination's ground (169 on the island, 1 in the
lair), which is the destination's geometry being admitted rather than the old place's left behind.

`d1` is the clearest single frame of the whole walk: one 500 ms hold west crossed island → lair → island,
because the lair's arrival pose sits about 50 units outside its own exit reach and the rest of the hold
walked straight back into it. The pose the HUD ends on is the island's arrival point again, 40 units west
of it, after two `entrance-entered` reports in the same second.

## The product's own account of the crossings

`POST /__rusty/product/runtime/diagnostics/read` on the live-debug host returns the world's own reports;
the `travel` source carries one per crossing, in order:

```
entrance-entered | The party walked into the entrance '53.382' in place '52' and arrived in place '1'
                   at PlacePose { X = 13839, Y = 16367, Z = 169, Yaw = 1, Pitch = 0 }.
entrance-entered | The party walked into the entrance '138.1730' in place '1' and arrived in place '52'
                   at PlacePose { X = 752, Y = 2229, Z = 1, Yaw = 1012, Pitch = 0 }.
entrance-entered | The party walked into the entrance '53.381' in place '52' and arrived in place '1' ...
entrance-entered | The party walked into the entrance '138.1730' in place '1' and arrived in place '52' ...
entrance-entered | The party walked into the entrance '53.381' in place '52' and arrived in place '1' ...
```

`138.1730` is the reach derived from Emerald Island's own cave-mouth face, so the region's entrance into
the lair was walked into twice; `53.381`/`53.382` are two faces of the lair's own exit, which is why the
two directions alternate. The lair's arrival from the island (`752, 2229, 1`) sits about 50 units outside
its own exit reach, so a party that keeps walking east there crosses back almost immediately: in
`walk-11` one 500 ms hold crossed island → lair → island, which is what `d1` shows.

## What could not be walked, and why

* **The Temple of the Moon's cave mouth** (link 137's reach at `15888, 12166`) was not reached by walking.
  Its reach is up the island's north-west mountains; from the lair's cave mouth the route is ~10,000 units
  across the shelf, and from the game's own start point (`12552, 800`, the earlier walk's scenario) about
  21,000 units around the ridge. Four open-loop attempts walked it with legs planned against the region's
  own height field and collision artifact (see `walk-1.js` … `walk-5.js`) and each drifted into the
  island's fences and cliffs: the poses at the end of those runs were `13936, 3756`, `11559, 5881`,
  `18282, 8885` and `17939, 9194` against planned waypoints thousands of units away. A keyboard hold is
  real-time, so the party's position cannot be read between two holds of the same program; the drift is
  found only after the program ends. The Temple's entrance is the same mechanism as the lair's, which two
  crossings do prove, so this is a navigation limit of the open-loop walk and not an untested trigger.
* **Links 5 and 51** (`no-event-face`), the 33 conditional continuations and the 3 world-issued links
  cannot be walked into at all in the shipped data; the reasons above are the importer's, per link.
* Nothing on Emerald Island was walked into *from* the game's own start point in this session: the
  scenario starts in the lair. The region's own entrance is still the one that was walked into (`138.1730`)
  — from the island side, exactly where that entrance stands.

## Read-only cross-checks

`GET /__rusty/product/runtime/debug/catalog` is admitted with `--live-debug` and lists the engine's debug
commands; the product registers no debug command of its own, so the HUD screenshots and the travel
diagnostics above are the whole record. The diagnostics also carry the engine's own `browser-host` and
`renderer` events, which were not read for this walk.
