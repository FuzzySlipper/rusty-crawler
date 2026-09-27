# Live checks

How a lane takes a live-check target, drives the running product through its own panel, and reads what
it answers. This is the durable procedure; which checks are in flight, and what each one must show, is
Den's business and is not mirrored here.

## Targets

A **live-check target** is one running dev host with its own port, its own playtest profile, and its own
content root. On `den-agents` two stand up as user units, both self-restarting, both holding stdin open,
both bound to the host's LAN address so a browser on this box can reach them:

| Target | Checkout | URL | Unit | Content root |
| --- | --- | --- | --- | --- |
| A | this repository | `http://192.168.1.10:4176/` | `rc-live.service` | the repository's own `content/` |
| B | a worktree, e.g. `/home/agent/dev/rc-live-b` | `http://192.168.1.10:4178/` | `rc-live-b.service` | the worktree's own `content/` |

Each writes its build and runtime output to its own `.runtime/`, and its stdout to `/tmp/rc-live.log` and
`/tmp/rc-live-b.log`. The playtest profiles `rusty-crawler` and `rusty-crawler-b` name those URLs; adding
a third target means adding a unit, a port, and a profile entry, in that order — and restarting the
playtest service, whose registry it reads once at startup, before `playtest start` knows the new id.

**Stage on B, never on A.** A target's dev host watches its own `src/` and `content/` and replaces the
running runtime on any write. On A those paths are the repository's, which is the collision that made two
lanes drop each other's sessions: one lane's scenario write restarted the product under the other lane's
browser. B exists so that a check can stage freely; a change to A's content is a change to the repository.

## Staging on B

A live check usually stages two things: a **scenario** (which place the party starts in, and where) and,
when the place under test is defended or otherwise unsuitable, a **variant of an imported pack** with the
unwanted records dropped. Both are hand-written JSON under `content/partyrpg/imports/<pack>/`, named by
`content/partyrpg/bundles/<bundle>/bundle.json`, and generated game data is never committed.

Two loader rules decide whether the product starts, and one staging rule keeps a write out of the other
checkout:

- **A pack's directory name must equal its `packId`.** The loader refuses a mismatch by name:
  `the manifest says 'x' but the pack directory is 'y'`.
- **One root, one pack per document id.** Every pack directory under `content/partyrpg/imports` is read
  and validated whether or not the bundle names it, so a variant *replaces* the pack it varies in that
  root rather than sitting beside it: two packs declaring `places` stop the product with
  `document id 'places' is already declared by pack '...'`. Keep the untouched packs one directory
  outside the root while a variant is staged — the `rc-live-b` worktree keeps them in
  `content/partyrpg/imports-base/` — and move them back when the variant is done with.
- **Copy contents, never a link.** A worktree's content root can hold *symlinks* to the main checkout's
  packs; `cp -r <pack> <variant>` copies the symlink, and a write through it edits the main checkout's
  pack. Copy with `cp -rL <pack>/. <variant>/`, and check `[ -L <variant> ]` (or `readlink -f`) before
  writing into it.

Then wait for the runtime to come back: the dev host logs `runtime-replaced`, and a content refusal
appears as `Error: "CSHARP_PRODUCT_CALL: ... status 99: Rusty Crawler cannot start: <reason>"` in the same
log. A refusal is a started-nothing, not a half-loaded world.

## Attaching and driving

```sh
playtest start rusty-crawler-b                                   # session id; observe returns console[], page_errors[], path (PNG)
playtest observe SESSION                                        # a PNG and the console/page-error stream
playtest browser SESSION --json '{"op":"click","selector":"text=Accept party"}'
playtest browser SESSION --json '{"op":"inspect","selector":".crawler-session dt,.crawler-session dd"}'
playtest input   SESSION --json '[{"kind":"hold","keys":[87],"ms":800}]'   # hold W for 800 ms
playtest stop SESSION
```

- **The panel is the reliable input surface**: creation is accepted by clicking the panel's own
  `Accept party` button, not by a key. Click the play area once before expecting keys to arrive.
- **`input` holds take virtual-key codes**, not key names: W 87, A 65, S 68, D 83, Q 81, E 69, G 71
  (the use key), P 80, Space 32. A batch that names a key instead is refused by the service's validation;
  that refusal currently reaches the caller as an `EOF` from the service rather than as the sentence,
  which is a playtest-service defect rather than a product one — send VK codes and it does not arise.
- Keys are held, not tapped: one `hold` of `W` walks, one `hold` of `Q`/`E` turns, and the panel's
  `Position` row is the feedback loop — turn until the bearing is right, walk, re-read, repeat. At 60 fps
  a held `W` moves the party about 382 units a second and a held `Q`/`E` turns about 512 facing units a
  second (2048 to a turn).
- **Read the panel's rows, not a screenshot, for exact words**: `inspect` with a selector returns each
  matched element's `innerText`, so `.crawler-session dt` and `.crawler-session dd` give the panel's facts
  in order (`Pack`, `Coins`, `Position`, `Facing`, `Use`, …), and
  `.crawler-use-result` / `.crawler-use-residue` give the last use's sentence and its residue. The same
  panel's `data-*` attributes (`data-place`, `data-interaction`, `data-use`) are the product's own words
  for the same facts. The panel is `position: fixed` with its own scrollbar, so a fact below the fold is
  read from the DOM or photographed after scrolling the panel, not the page.

## Reading failures

- `observe` and `browser` return the page's `console[]` and `page_errors[]`. WebGL warnings such as
  `GPU stall due to ReadPixels` are the browser's, not the product's; a product-side JavaScript failure
  arrives as a `pageerror`.
- The product's own refusals are sentences in the panel and in the host log, and the host log is where a
  refused start states why (`status 99` plus the reason).
- A product that registers live-debug commands publishes them at
  `GET /__rusty/product/runtime/debug/catalog`, executed with `POST .../debug/execute` and a
  `text/plain; charset=utf-8` body. This product registers the engine's entity and renderer reads; it
  registers none of its own, so the panel remains the way to read gameplay state.
- A browser session can outlive the product's runtime replacement with a stale projection on screen: if
  the panel's `Admitted steps` and `Simulation` stop moving, the session is not stepping. Reload by
  stopping and starting the session (`playtest stop` then `playtest start`), then re-accept the party.

## Cleanup

`playtest stop SESSION` releases the browser slot for another lane; the pool is not single-session. Leave
the staged packs in place while the check is being read — a lane that stages over another lane's scenario
is the same collision in a quieter form — and stage A back to what the repository says it is.
