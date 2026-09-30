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
rusty install                                   # the pinned pair, once per machine
rusty dev --project src/PartyRpg.Host/PartyRpg.Host.csproj \
  --port <port> --bind-host <address> --live-debug
```

- **Bind to an address the browser can reach.** The Engine's development host admits a request only from
  the origin it is bound to, so a browser on another machine (the playtest service's) needs the host bound
  to an address that browser reaches rather than to loopback. `.den-serve.json` is the same command with
  Den choosing host and port.
- **The runtime needs a GPU adapter** (a software Vulkan driver counts); without one the load fails.
- The product draws no world: the frame is empty and the game is the DOM panel over it. `--live-debug`
  exposes the Engine's debug surface; the product registers no gameplay debug modules of its own yet, so
  the panel is the way to read gameplay state.
- Wait for the host to log that the runtime was replaced. A content refusal appears in the same log as the
  product's own sentence (`Rusty Crawler cannot start: <reason>`); a refused start has loaded nothing.

## Staging content

A check usually stages the operator's imported packs, a bundle that names them, and often a hand-written
**scenario** pack (which place the party starts in, where, and with what party) or a **variant** of an
imported pack with unwanted records dropped. All of it lives under `content/partyrpg/imports/<pack>/`
(ignored) and `content/partyrpg/bundles/partyrpg-default/bundle.json` (tracked). Generated game data is never
committed.

- **A pack's directory name must equal its `packId`.** The loader refuses a mismatch by name.
- **One root, one pack per document id.** Every pack directory under `content/partyrpg/imports` is read and
  validated whether or not the bundle names it, so a variant *replaces* the pack it varies in that root: two
  packs declaring the same document id stop the product with both named. Move the untouched pack one
  directory outside the root while a variant is staged, and back afterwards.
- **Copy contents, never a link.** A worktree's content root can hold symlinks to another checkout's packs;
  `cp -r` copies the link and a write through it edits the other checkout. Copy with
  `cp -rL <pack>/. <variant>/` and check `readlink -f` before writing.

## Staging a pose

When no scenario entry point stands near what a check needs, stage the pose through a save instead of
walking there:

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
playtest start <profile>                                          # prints the session id
playtest observe SESSION                                          # a PNG plus console[] and page_errors[]
playtest browser SESSION --json '{"op":"click","selector":"text=Accept party"}'
playtest browser SESSION --json '{"op":"inspect","selector":".crawler-session dt,.crawler-session dd"}'
playtest input   SESSION --json '[{"kind":"hold","keys":[87],"ms":800}]'   # hold W for 800 ms
playtest stop SESSION
```

A profile names the URL the service's browser opens; adding or changing one is `playtest reload` after the
service's game list is edited.

- **The panel is the reliable input surface**: creation is accepted by clicking its `Accept party` button.
  Click the play area once before expecting keys to arrive.
- **`input` holds take virtual-key codes**, not names: W 87, A 65, S 68, D 83, Q 81, E 69, G 71 (use),
  P 80 (pause), F 70 (save), B 66 (act), Enter 13 (pacing), Space 32. The declared keys are listed in
  [`../src/PartyRpg.Host/README.md`](../src/PartyRpg.Host/README.md).
- Keys are held, not tapped, and the panel's `Position` row is the feedback loop: turn until the bearing is
  right, walk, re-read. At 60 steps a second a held `W` walks about 382 units a second and a held `Q`/`E`
  turns about 512 facing units a second (2048 to a turn). The panel cannot tell a blocked direction from an
  input that never arrived.
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
