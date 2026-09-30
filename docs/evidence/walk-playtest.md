# Walking Emerald Island from the running product

> Published copy of a point-in-time live-check record kept in the operator's ignored local verify tree.
> It is evidence of what was observed then, not a statement of current behaviour. Screenshots, scripts,
> logs and saves it names were not published; LAN addresses are replaced by `<lan-address>`.

This directory records the walk Den task #8566 asked
for: the product served by its real dev runner, driven by a keyboard through the agent playtest
service's remote GPU browser, with what the HUD showed at each step.

## What was run

```sh
# 1. stage the imported packs the collision lane generated, and point the tracked bundle at them
cp -r /tmp/mm7-live/content/partyrpg/imports/. content/partyrpg/imports/          # ignored path
#    bundle.json edited in place to name mm7-tables, mm7-world, mm7-live-scenario, then restored
#    (git status clean afterwards; a fresh checkout is unaffected)

# 2. serve the product (managed background job, never pkill)
./.runtime/runtime-pack/bin/rusty dev --project ./src/PartyRpg.Host/PartyRpg.Host.csproj \
  --runtime ./.runtime/runtime-pack --bind-host <lan-address> --port 4176

# 3. playtest against it (the binary is ~/.local/bin/playtest, not on PATH by default)
playtest start rusty-crawler
playtest run 8b1b7bc5-a57b-4866-9b63-3c47d549d609 --file local/verify/walk-playtest/walk-trial.js --budget-ms 90000
playtest run 8b1b7bc5-a57b-4866-9b63-3c47d549d609 --file local/verify/walk-playtest/walk-long.js  --budget-ms 110000
playtest run 8b1b7bc5-a57b-4866-9b63-3c47d549d609 --file local/verify/walk-playtest/walk-stuck.js --budget-ms 90000
playtest stop 8b1b7bc5-a57b-4866-9b63-3c47d549d609
```

`--bind-host 127.0.0.1` does not work for this browser path, and the reason is the product's own origin
rule: the engine's development host admits a request only when the `Host` header equals
`<bind-host>:<port>` while the bind is a loopback address (`rusty-engine`
`rust/crates/product-dev-host/src/host.rs`, `has_admitted_origin`). The browser reaches the game through
the service's container forwarder, which loads `http://localhost:4176/` whatever the profile URL was, so
a loopback-bound host answers `400 DEV_HOST_ORIGIN` (observed directly: `curl -H 'Host: localhost:4176'
http://<lan-address>:4176/` → 400 with the loopback bind, 200 with the LAN bind). Bound to
`<lan-address>`, the same request is admitted and the GPU box reaches it directly
(`ssh den-srv curl -H 'Host: localhost:4176' http://<lan-address>:4176/` → 200), so no port forwarder is
needed. The GPU box is on the same trusted LAN; the host was stopped again after the walk.

## What was changed outside this repository

The installed playtest game list `the playtest service's game list` gained one additive
entry (backup beside it: `games.json.bak-rustycrawler-20260924T230245Z`), and
`systemctl --user restart crew-playtest.service` was run while the pool was idle to load it:

```json
{"id":"rusty-crawler","description":"PartyRpg reference host (rusty-crawler): ...",
 "url":"http://<lan-address>:4176/","window_title":"Rusty Crawler",
 "controls":{"W/A/S/D":"...","Q/E":"...","Space":"Jump","P":"Pause and resume the session"},
 "reset":"No in-game reset control. Reload the page or restart the dev host for a fresh session."}
```

Delete that one object and restart the service to revert; nothing else in the file or the service changed.

## The session

* Session `8b1b7bc5-a57b-4866-9b63-3c47d549d609`, slot 1, created 23:02:59Z, stopped 23:08:20Z — about
  five minutes of attached browser with no `DEV_HOST_VIDEO_FEEDBACK_UNSUPPORTED` stop and no session
  drop. That limitation belongs to this box's own headless browser, not to the remote GPU browser.
* The page is the HUD panel on a dark canvas (the product renders no world yet); every value below is
  read from the screenshots in this directory, not from product state.

| screenshot | what was held before it | Session | Simulation | Admitted steps | Position |
| --- | --- | --- | --- | --- | --- |
| `02-before.png` | nothing | running | 52.0 s | 3118 | `12552, 800, 193 @ 512` |
| `03-after-hold-1.png` | W 2.0 s | running | 54.4 s | 3267 | `12552, 1561, 2 @ 512` |
| `04-released-1.png` | released, +2.0 s | running | 56.7 s | 3405 | `12552, 1561, 2 @ 512` |
| `05-after-hold-2.png` | W 2.0 s | running | 59.2 s | 3555 | `12552, 2328, 20 @ 512` |
| `06-released-2.png` | released, +2.3 s | running | 61.5 s | 3693 | `12552, 2328, 20 @ 512` |
| `07-after-hold-3.png` | W 4.0 s | running | 66.1 s | 3965 | `12552, 3859, 98 @ 512` |
| `08-released-3.png` | released, +2.3 s | running | 68.4 s | 4103 | `12552, 3859, 98 @ 512` |
| `09-walk0.png` | still nothing, +70 s | running | 138.4 s | 8305 | `12552, 3859, 98 @ 512` |
| `10-walk1.png` | W 8.0 s | running | 146.9 s | 8815 | `12552, 6931, 98 @ 512` |
| `11-walk2.png` | W 8.0 s | running | 155.5 s | 9327 | `12552, 7094, 96 @ 512` |
| `12-walk3.png` | W 8.0 s | running | 163.9 s | 9835 | `12552, 7094, 96 @ 512` |
| `13-turn-held.png` | Q 1.5 s | running | 166.0 s | 9958 | `12552, 7094, 96 @ 1280` |
| `14-turn-released.png` | released, +1.8 s | running | 167.8 s | 10065 | `12552, 7094, 96 @ 1280` |
| `15-jump.png` | Space 0.3 s | running | 168.6 s | 10113 | `12552, 7063, 98 @ 1280` |
| `16-jump-settled.png` | +1.8 s | running | 170.4 s | 10221 | `12552, 7063, 98 @ 1280` |
| `18-forward-again.png` | W 4.0 s | running | 245.0 s | 14698 | `11559, 5991, 96 @ 1280` |
| `19-backward.png` | S 4.0 s | running | 249.5 s | 14967 | `12645, 7077, 98 @ 1280` |
| `20-strafe-right.png` | D 4.0 s | running | 253.9 s | 15236 | `12580, 7171, 98 @ 1280` |
| `21-strafe-left.png` | A 4.0 s | running | 258.5 s | 15507 | `13633, 6085, 98 @ 1280` |
| `22-settled.png` | released, +2.3 s | running | 260.8 s | 15645 | `13633, 6085, 98 @ 1280` |

The panel read `Rusty Crawler`, `Might and Magic VII: For Blood and Honor`,
`partyrpg-default · 3 packs`, `Emerald Island · region`, `Place 1`, `Explored 1 / 76` throughout, with
the `Pause session` button and `Pause or resume with the button, or with the P key.` `crops/` holds the
same frames cropped to the panel.

## What the walk showed

* **Movement works from a browser.** Eight seconds of held W moved the party 3,059 units north
  (y 800 → 3859; 761, 767 and 1,531 units per hold), at about 382 units/s, matching the collision
  lane's engine-side measurement (384 units/s). The whole walk covered roughly 10,900 units of path
  (6,294 north, then 1,461, 1,536, 114 and 1,400 in the four directions probed after the turn).
* **A released key stops the party, and nothing drifts afterwards.** Every post-release sample repeats
  the pose of the sample taken while the key was still down; the 70 s between `08-released-3` and
  `09-walk0` changed the admitted-step count from 4,103 to 8,305 and the pose not at all.
* **Held turn and jump behave.** Q held for 1.5 s changed the yaw `512 → 1280` and froze there on
  release; Space moved the party from `12552, 7094, 96` to `12552, 7063, 98` and it stayed there.
* **The HUD is self-consistent.** `Simulation` is the admitted-step count over 60 (15,645 / 60 =
  260.75 s), and `Place 1` / `Explored 1 / 76` agreed with the scenario start in the staged pack.
* **The party stayed on the ground.** The pose's z followed the surface it walked over: 193 on the
  starting platform, 2 on the terrain below it, then 20, 98, 96, 98 as it crossed. The panel has no
  fall, blocked, or grounded readout, so this is what the pose shows and not a session fact.
* **A held key can move the pose zero units.** The third 8 s hold of W produced no pose change at all
  (`12552, 7094, 96` before and after) while the admitted steps advanced 9,327 → 9,835, and the jump
  nudged the party to `12552, 7063, 98`. Walking in other directions from that same spot moved freely
  (1,461 units on W after the turn, 1,536 on S, 1,400 on A), so the mover was not dead — that direction
  is blocked by geometry, and the block slides a little before it stops dead (163 units over the second
  8 s hold, none over the third). The HUD cannot show that difference: a blocked direction and an input
  that never arrived look identical on the panel.
* **No transition can be taken by walking.** Two exclusions, both checkable:
  * Nothing consults the travel links as the party moves. `PartyRpgSession.Update` calls only
    `world.Step(intent, seconds)` (`src/PartyRpg.Kit/Sessions/PartyRpgSession.cs:190`), and
    `SessionWorld.Travel` (`SessionWorld.cs:446`) has no product caller — the only callers are tests.
    The scenario start reaches the world through `ArriveAt`, which is a placement, not a transition
    (`src/PartyRpg.Rulesets.MightAndMagic7/MightAndMagic7World.cs:54`).
  * The imported data carries no source-side trigger to walk into. The two links out of place 1
    (`place-graph.json` ids 137 and 138) carry the **destination** pose inside The Temple of the Moon
    and The Dragon's Lair, and place 1's placements are 11 spawns and 147 decorations with **no door
    placement**, so the outdoor entrances the original game triggers on do not exist here as geometry
    the mover could meet.
  So roughly 10,900 units of walking, a turn, and strafing in three directions left `Place 1` and
  `Explored 1 / 76` unchanged: the destination-with-its-own-geometry half of the task could not be
  observed from the running product and is reported as a gap rather than a walk that happened.
* Read-only cross-checks: `/__rusty/product/runtime/debug/catalog`, `.../diagnostics/read`, and
  `.../outputs/fresh` are not admitted (the dev host was started without `--live-debug`), and an
  EventSource GET on `.../outputs` returned only `{"code":"DEV_HOST_OUTPUT_LAG"}` — so the panel
  screenshots are the only HUD record, and no independent product readout contradicts them.
