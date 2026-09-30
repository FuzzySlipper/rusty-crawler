# Reaching save and resume from the running product

> Published copy of a point-in-time live-check record kept in the operator's ignored local verify tree.
> It is evidence of what was observed then, not a statement of current behaviour. Screenshots, scripts,
> logs and saves it names were not published; LAN addresses are replaced by `<lan-address>`.

This directory records the live check Den task #8581
asked for: the product served by its real dev runner on `<lan-address>:4176`, driven by a keyboard through
the agent playtest service's remote browser, with the HUD read from the screenshots here.

## What was run

```sh
# staging (both restored afterwards; a fresh checkout is unaffected):
#   content/partyrpg/bundles/partyrpg-default/bundle.json  edited in place to name mm7-tables,
#     mm7-world and mm7-live-scenario, then restored with `git checkout --`
#   content/partyrpg/imports/mm7-live-scenario/scenario.json (ignored) pointed at place 52,
#     The Dragon's Lair, and restored to place 1 afterwards

# 1. serve the product fresh (managed background job, never pkill)
./.runtime/runtime-pack/bin/rusty dev --project ./src/PartyRpg.Host/PartyRpg.Host.csproj \
  --runtime ./.runtime/runtime-pack --live-debug --bind-host <lan-address> --port 4176

# 2. playtest against it (slot-2; slot-1 belongs to another lane)
playtest start rusty-crawler
playtest run <session> --file local/verify/save-resume/save-and-cross.js --budget-ms 90000

# 3. restart the *same* host told to resume, then a fresh browser session on the same URL
#    (the switch is the environment variable the product reads where it is created)
RUSTY_CRAWLER_START=resume ./.runtime/runtime-pack/bin/rusty dev ... --port 4176
playtest recover <session>
playtest run <new session> --file local/verify/save-resume/read-resumed-rows.js --budget-ms 30000

# 4. a third fresh run, to watch the save control be refused at the creation screen
playtest recover <session>
playtest run <new session> --file local/verify/save-resume/save-during-creation.js --budget-ms 40000
playtest run <new session> --file local/verify/save-resume/scroll-to-bottom.js --budget-ms 40000
```

The keyboard only reaches the page while the browser holds focus, so the first holds were `P` (pause) and
`P` again: `03-paused.png` reads `Session paused`, which is what makes the walk below trustworthy.

## The frames, and what each one reads

| frame | what was held before it | Session | Place | Date | Days | Food | Position | Explored |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `01-creation-screen.png` | nothing (start) | creating | no world loaded | 1168-01-01 | 0 | — | — | 0 / 0 |
| `02-party-accepted.png` | Space (accept the default party) | running | 52 The Dragon's Lair | 1168-01-01 | 0 | 10 portions | `924, 2248, 2 @ 0` | 1 / 76 |
| `03-paused.png` | P 300 ms | paused | 52 | 1168-01-01 | 0 | 10 portions | `924, 2248, 2 @ 0` | 1 / 76 |
| `04-out-of-reach.png` | S 1.0 s (west, out of the lair's exit reach) | running | 52 | 1168-01-01 | 0 | 10 portions | `540, 2248, 2 @ 0` | 1 / 76 |
| `05-crossed-to-emerald-island.png` | W 1.5 s (east, back into the reach) | running | **1 Emerald Island** | **1168-01-02** | **1** | **9 portions** | `14229, 16368, 169 @ 1` | **2 / 76** |
| `06-save-frame.png` | F 200 ms (the declared save key) | running | 1 | 1168-01-02 | 1 | 9 portions | `14229, 16368, 169 @ 1` | 2 / 76 |
| `07-save-rows.png` (scrolled) | W 1.2 s after the save, then Tab + Down ×4 | running | 1 | 1168-01-02 | 1 | 9 portions | `14690, 16370, 98 @ 1` | 2 / 76 |
| `08-resumed-session.png` | nothing: a fresh browser on the host started with `RUSTY_CRAWLER_START=resume` | running | 1 Emerald Island | 1168-01-02 | 1 | 9 portions | `14229, 16368, 169 @ 1` | 2 / 76 |
| `09-resumed-rows.png` (scrolled) | Tab + Down ×5 | running | 1 | 1168-01-02 | 1 | 9 portions | `14229, 16368, 169 @ 1` | 2 / 76 |
| `10-save-refused-at-creation.png` (scrolled) | F at the creation screen of a third, fresh run | **creating** | no world loaded | 1168-01-01 | 0 | — | — | 0 / 0 |

Read off the panel:

* **The save is an explicit act with a visible outcome.** `07-save-rows.png` reads `Save
  1168-01-02 09:03 · session`, `Start fresh`, and the product's own line `Saved the session to slot
  'session' at 1168-01-02 09:03.` The file landed at `.runtime/persistence/sessions/session` (4,827 bytes,
  written at the second the panel named), and nothing else on the panel moved for it.
* **The slot holds the moment of the save, not the end of play.** After saving, the walk continued to
  `14690, 16370, 98 @ 1`; the resumed session stands back at `14229, 16368, 169 @ 1`, the pose the save
  was taken at, with the same party (Roderick · Human · Knight · human-man, Aelina · Elf · Sorcerer ·
  elf-woman, Borin · Dwarf · Cleric · dwarf-man, Nyx · Goblin · Thief · goblin-woman), the same place, the
  same clock reading and the same per-place state (`2 / 76`).
* **The resumed session is a host decision, and says so.** `08-resumed-session.png` heads the roster
  `Party resumed`; `09-resumed-rows.png` reads `Start resumed` and `Save —`: this session plays the save
  and has saved nothing of its own. Its clock is the saved one advanced by its own admitted play — 23.4 s
  of admitted time at the first frame and 38.7 s at the scrolled one, at this game's 30 game seconds per
  admitted second, which is 09:03 + 11.7 and 09:03 + 19.4 minutes: the `09:14` and `09:22` the frames
  read. A fresh session could not be standing on `1168-01-02` in either frame.
* **A save that cannot land names what went wrong.** At the creation screen of a fresh run there is
  nothing to save yet, so the declared key was answered: `10-save-refused-at-creation.png` reads `Save
  failed` with `The session cannot be saved: the session holds no party, so a save would load as an
  expedition nobody leads; the session holds no world, so a save would name no place to resume in.` No
  file was written. (The clock is missing from that list because creation's session does hold one: the
  frame reads `Date 1168-01-01` beside it.)

Not exercised live: a *failed write* (a store that cannot open, or refuses bytes). It needs a host started
without a persistence root, and the kit and host suites prove both refusals by name —
`SaveReachTests.A_write_the_store_refuses_surfaces_by_name_and_leaves_the_session_playable` and
`SaveReachTests.A_session_with_nowhere_to_write_says_so_rather_than_saving_nowhere`.

## Files left behind

None: the bundle and the scenario were restored, the browser session was stopped, both dev hosts were
stopped (port 4176 free), and the save the check wrote under `.runtime/persistence/sessions/` was
removed with it.
