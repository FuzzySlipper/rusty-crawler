# Live checks

How a lane serves the product, stages what a check needs, drives it through its own panel, reads what it
answers, and puts tracked content back. This is the durable procedure; which checks are in flight, and what
each must show, is Den's business. Readings a document cites as proof are published in
[`evidence/`](evidence/README.md).

## Serving a checkout of its own

A check runs from a checkout (or worktree) of its own, because the dev host watches its project's `src/`
and `content/` and replaces the running runtime on any write there: staging into a checkout another lane is
playing drops that lane's session.

```sh
scripts/developer-launch.sh doctor              # resolve tools and print pair status
scripts/developer-launch.sh install             # the pinned pair, once per machine
scripts/developer-launch.sh host \
  --port <port> --bind-host <address> --live-debug
```

`scripts/developer-launch.sh` is the supported checkout-local wrapper around
the Engine CLI. It resolves `rusty` from `PATH` (with the installer's normal
`$HOME/.local/bin` fallback) and resolves the .NET SDK from `PATH`,
`DOTNET_ROOT`, or the installer's normal `$HOME/.dotnet` location. It exports
the resolved SDK directory before invoking `rusty dev`, so a service manager or
Den broker with a sparse `PATH` reports the actual missing tool instead of
silently selecting a different SDK. No assessor or machine-specific home path
is part of the launch contract. `doctor` is read-only; it prints the checkout,
resolved tools, and the pinned pair before a host starts.

The same user-local discovery is applied by `scripts/verify.sh`, so a fresh
service or CI shell can run the repository gate without copying an assessor's
SDK path into its environment.

- **Bind to an address the browser can reach.** The Engine's development host admits a request only from
  the origin it is bound to, so a browser on another machine (the playtest service's) needs the host bound
  to an address that browser reaches rather than to loopback. `.den-serve.json` is the same command with
  Den choosing host and port.
- **The runtime needs a GPU adapter** (a software Vulkan driver counts); without one the load fails.
- The product draws the party's place from the imported render meshes and media, beside the DOM panel. `--live-debug`
  exposes the Engine's debug surface, where the product registers the playtest and interaction commands
  ([Reading gameplay state](#reading-gameplay-state)): read gameplay state there, not from the panel.
- Wait for the host to log that the runtime was replaced. A content refusal appears in the same log as the
  product's own sentence (`Rusty Crawler cannot start: <reason>`); a refused start has loaded nothing. A
  `developer-launch` tool or pair error happens before product startup and should be diagnosed with `doctor`.

## Staging content

A check usually stages the operator's imported packs, a bundle that names them, and often a hand-written
**scenario** pack (which place the party starts in, where, and with what party) or a **variant** of an
imported pack with unwanted records dropped. All of it lives under `content/partyrpg/imports/<pack>/`
(ignored) and a bundle (tracked). Generated game data is never committed. An ordinary run plays the default
`mm7-new-game` bundle — the imported tables and world from the authored opening, through creation — and needs
nothing staged beyond the current importer preparation command:

```sh
scripts/developer-launch.sh prepare-content --install /path/to/your/mm7
```

The command builds the checkout's importer, runs `verify` and `maps`, then runs the actual `write` command
with `--check-determinism`. It stops on an invalid or obsolete source/content refusal; do not patch generated
JSON or weaken validation to make an old pack load. Set `CRAWLER_MM7_INSTALL` instead of `--install` when a
shell or service already owns the operator-install path. A check with its own scenario names it in
`partyrpg-default` and serves with `RUSTY_CRAWLER_BUNDLE=partyrpg-default`, so the new game's start and the
check's never collide.

- **A pack's directory name must equal its `packId`.** The loader refuses a mismatch by name.
- **A scenario says which start it takes.** Its `scenario-start` entry's `"party": "scenario"` plays the party
  its `scenario-party` document fixes, with no creation screen; `"creation"`, or leaving the word out, opens
  creation. `playtest.observe`'s `composition.partyStart` and the panel's `Start` row name the start taken.
- **Pick a start nothing will kill at once.** Places now hold the maps' own pre-placed creatures, so a start in
  a lair puts a fresh party beside its keeper: a Red Dragon in Dragon's Lair killed a default party with one
  ranged blow within two seconds of acceptance. Older readings that started there predate those creatures.
- **Only the bundle's packs play.** A scenario pack the bundle does not name contributes nothing, but it is
  still validated: a broken one stops the start with a refusal that says it is not selected and the directory
  it was read from. A reading of both is in
  [`evidence/bundle-selection-and-party-start.md`](evidence/bundle-selection-and-party-start.md).
- **One root, one pack per document id.** Every pack directory under `content/partyrpg/imports` is read and
  validated whether or not the bundle names it, so a variant *replaces* the pack it varies in that root: two
  packs declaring the same document id stop the product with both named. Move the untouched pack one
  directory outside the root while a variant is staged, and back afterwards.
- **Copy contents, never a link.** A worktree's content root can hold symlinks to another checkout's packs;
  `cp -r` copies the link and a write through it edits the other checkout. Copy with
  `cp -rL <pack>/. <variant>/` and check `readlink -f` before writing.

## Reading gameplay state

With `--live-debug` the product answers the Engine's playtest commands from the session it holds at that
moment. Each is a plain-text `POST` to the host's debug route, with the bound origin; a refused command is
`422` with the refusal's code and sentence:

```sh
curl -s -X POST -H 'Content-Type: text/plain; charset=utf-8' -H "Origin: http://<address>:<port>" \
  --data-binary 'playtest.observe' http://<address>:<port>/__rusty/product/runtime/debug/execute
```

- **`playtest.observe`** — the session's mode and admitted steps; `composition` (the bundle, how many packs it
  selected, and `partyStart`: `creation`, `scenario` or `resumed`); `steering` (whether a held movement key would
  step the party now, and why not); `place` and `pose` (the party's feet in the place's own coordinates, yaw and
  pitch in facing units, 2048 to a turn here, growing to the left); the last movement step (`grounded`,
  `blocked`); `facing` (what the reticle holds, its verb, state, distance and reason, and the last use's
  outcome and refusal code); `combat.hostile` (every actor fighting the party with its distance, health and
  activity) and `combat.members`; and which screen is open. It is the snapshot the panel is built from, read
  without stepping anything.
- **`playtest.action <intent>`** — one declared control: its physical key (`KeyW`, `Space`, `Enter`), hold or
  tap, a nominal input window (not a game timing), and whether the session would take it now with the reason
  when not. `playtest.help` lists every intent.
- **`playtest.look <yaw> 0`** — turns the party by degrees (positive right) through its facing rule, between
  updates and without advancing time; refused while the party could not turn with its keys, and for any pitch.
- **`interaction.inspect`** — every candidate the reticle considers, which one it holds, and each one's
  reach, visibility and signed yaw to it: `playtest.look` by that yaw faces it. Targeted `interaction.use` is
  off; use what is faced with its key (`party.use`).

Input without a page is the Engine's harness claim (`control/claim` with the runtime binding that
`engine.renderer.presentation` reports, then `input` batches of `{"kind":"key","code":"key-w",...}` facts in the
`gameplay.default` context, then `control/release`; see the Engine's playtest-inspection guide). A check reads
`playtest.observe` before and after a press rather than a screenshot or the panel's rows. A reading taken this way is in
[`evidence/playtest-observe.md`](evidence/playtest-observe.md).

## Staging a pose

Walk and turn to a nearby spot with the harness input and `playtest.observe` as the feedback loop. When no
scenario entry point stands near what a check needs, stage the pose through a save instead of walking there:

1. Play to any point, and save (`F`, or the panel's save control). The save is the checkout's persistence
   store, slot `session` (`.runtime/persistence/sessions/session`): the header `RSP2`, a little-endian u64
   version, a u64 length, then the JSON document.
2. Stop the host, rewrite only `world.pose` (the place id and the `x`, `y`, `z`, `yaw`, `pitch` of the pose),
   and fix the length field.
3. Serve again with `RUSTY_CRAWLER_START=resume`. The same party, clock, errands and journal now stand at the
   staged pose; a resume with nothing saved is refused by name.

Record a staged pose as staging, not as play, in the evidence.

## Driving and reading

```sh
playtest games                                                    # the profiles the service knows
playtest start <profile>                                          # one owned session; prints its id
playtest observe SESSION                                          # a PNG plus console[] and page_errors[]
playtest browser SESSION --json '{"op":"click","selector":"text=Accept party"}'
playtest browser SESSION --json '{"op":"inspect","selector":".crawler-session dt,.crawler-session dd"}'
playtest input   SESSION --json '[{"kind":"hold","keys":[87],"ms":800}]'   # hold W for 800 ms
playtest stop SESSION                                             # stop only the id this check owns
```

Start checks sequentially when the host is being replaced or a profile is
being reused. A broker lease timeout while another start is in progress is an
orchestration failure, not a product startup refusal: wait for the prior
owned start to finish or clean up its owned session, then retry one start.
Never stop a session belonging to another check to clear a lease, and do not
claim a product failure until the host's own readiness text and product log
have been read.

A profile names the URL the service's browser opens; adding or changing one is `playtest reload` after the
service's game list is edited.

- **The panel is the reliable input surface**: creation is accepted by clicking its `Accept party` button.
  Click the play area once before expecting keys to arrive.
- **`input` holds take virtual-key codes**, not names: W 87, A 65, S 68, D 83, Q 81, E 69, G 71 (use),
  P 80 (pause), F 70 (save), B 66 (act), Enter 13 (pacing), Space 32. The declared keys are listed in
  [`../src/PartyRpg.Host/README.md`](../src/PartyRpg.Host/README.md).
- Keys are held, not tapped, and `playtest.observe` is the feedback loop: turn until the bearing is right
  (or `playtest.look`), walk, re-read. At 60 steps a second a held `W` walks about 382 units a second and a
  held `Q`/`E` turns about 512 facing units a second (2048 to a turn). `movement.blocked` tells a blocked
  direction from an input that never arrived, which the panel cannot.
- **After a host restart, read the new binding.** The [restart reading](evidence/restart-input.md)
  records held W working on a fresh page after restarting `rusty dev`; it does not replay the original
  systemd-unit restart or certify an old page. Replaced runtime bindings and their input sequences cannot
  be reused. First attach a fresh session, focus the play area and compare `playtest.observe` before/after.
- **Direct-intent diagnostic fallback.** The original restart failure was worked around by posting the
  product's declared intent to the Engine input lane, one claim per admitted update. `sequence` is a
  canonical decimal **string**, obtained from the current binding/claim's `nextInputSequence`; it increases
  within that binding. An active digital intent claim is one update, not a held key. For example, the
  claim within a `batch` names `context: "gameplay.default"`, `intent: "party.move-forward"`, and
  `value: {"kind":"digital","active":true}`. Use the current Engine `control/claim` and `control/release`
  procedure described above, retaining the binding returned by the claim. Never reuse the old runtime or
  steal another session's claim. Record queued/admitted/product-observed results and the before/after pose.
  This is a diagnostic fallback, not proof that browser keys work; prefer ordinary keys for visible
  acceptance. The historical direct claims succeeded; no new current-pair fallback run is claimed here.
- **Read the panel's rows, not a screenshot, for exact words**: `inspect` returns each matched element's
  `innerText`; `.crawler-session dt` / `dd` give the facts in order, `.crawler-use-result` and
  `.crawler-use-residue` the last use's sentence and residue, and the panel's `data-*` attributes and its
  `data-problems` list are the product's own words for the same facts.
- A session whose `Admitted steps` and `Simulation` stop moving is not stepping (a replaced runtime under an
  old page, for one): stop and start the playtest session, then re-accept the party.

## Cleanup

`playtest stop SESSION` releases the browser slot. When the check is done, put tracked content back — the
bundle (`git checkout -- content/partyrpg/bundles`) and any tracked file a variant touched — move any pack
set aside back into the import root, and stop the host. Publish the readings a document will cite as a small
text record under [`evidence/`](evidence/README.md): no screenshots, saves, game data, or LAN addresses.
