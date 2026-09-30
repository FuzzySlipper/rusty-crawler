# Quests and the journal, walked live (Den tasks #8505 and #8506, deferred steps)

> Published copy of a point-in-time live-check record kept in the operator's ignored local verify tree.
> It is evidence of what was observed then, not a statement of current behaviour. Screenshots, scripts,
> logs and saves it names were not published; LAN addresses are replaced by `<lan-address>`.

This records the live check of the two steps the earlier
lanes deferred: the quest lifecycle end to end, and the journal pass with a save, a wait across a day, and a
resume. Every sentence below is the product's own, read from the panel's projection or its DOM.

## Target and staging

Target **B** — the `rc-live-b` worktree, `http://<lan-address>:4178/`, unit `rc-live-b.service` — with its own
content root. Nothing was staged in the main checkout and its `git status --porcelain` stayed clean.

- `content/partyrpg/imports/rc8505-quest-scenario/` — the earlier lane's hand-written quest scenario, copied
  in with `cp -rL` (no symlink), its entry point corrected from `Party Start` (which place 3 does not have) to
  `West Start`, and its provenance line updated to say so.
- `content/partyrpg/imports/mm7-tables/` — the operator's imported tables (the `mm7-tables-quiet` variant and
  the two leftover scenario packs were moved to `imports-base/` so exactly one scenario pack stands in the
  import root).
- `content/partyrpg/bundles/partyrpg-default/bundle.json` — names `mm7-tables`, `mm7-world`,
  `rc8505-quest-scenario`.

Driving: `playtest start rusty-crawler-b`, `playtest browser SESSION --json '{"op":"inspect",...}'` for the
panel's rows, and — after browser keys stopped arriving (routed as #8704) — the host's own direct intent
channel, which admits the same claims a key press becomes (`act.py`, `walk-intents.py` here), with the
projection read off `/__rusty/product/runtime/outputs/fresh` (`show.py`).

## Check 1 — the quest lifecycle

| step | the panel |
| --- | --- |
| party accepted, place 3 | `Erathia · region`, `Position -22230, 3823, 791 @ 0`, `Pack 0`, `Coins 200`, `Food 10 portions`, `Start fresh` |
| walked to Org House | 15,251 units from `West Start` to `residence-304` `Org House` at `-7808, 8768, 480`; the reticle read `Frederick Org · talk · 311` from the door's east side only (`-7503, 8713, 460 @ 964`), and `use refused` — `"What the party faces is not in sight."` from the south |
| errand heard | `conversation.topic errand:35` → quests block `"The errand '35' was offered and is in the party's journal."`; quest row `state "offered"`, objective `reach-0 "Reach Castle Navan" met false` |
| errand taken | `accept:35` → `"The errand '35' was taken."`; quest row `state "accepted"`; Current Quests `"1 errand in the journal"`; History gained `Was offered The Elven Treasury at Castle Navan · 1168-01-01 14:01 · Erathia · quest` and `Took on The Elven Treasury at Castle Navan · 1168-01-01 14:06 · Erathia · quest` |
| completed at Castle Navan | quest row `state "completed"`, `canTurnIn true`, objective `met true`; History gained `Entered Castle Navan · 1168-01-01 15:46 · Castle Navan · world` |
| handed in | `"The errand '35' was finished."` with `experience 4000`, `coins 250`; purse `200 → 450`; quest row `state "turned-in"`; History gained `Finished The Elven Treasury at Castle Navan · 1168-01-01 16:26 · Erathia · quest` |
| promoted | ranks panel: `Roderick — Cavalier of rank 2`, and `"Roderick rose to Cavalier at rank 2, meeting granted by Frederick Org; holds the record of raid the Elven Treasury at Castle Navan."`; History gained `Was raised to Cavalier · 1168-01-01 16:37 · Erathia · progression` |

**What could not be reached, and why.** Castle Navan is not reachable in play from Erathia:

- `place-graph.json` holds 277 links: 84 `fare: true`, 193 walkable. Erathia's only links to The Tularean
  Forest (place 4) are `fare-55-4` and `fare-64-4` (2 and 3 days).
- `place-entrances.json` carries reaches for 155 of the 193 non-fare links and for none of the 84 fare links,
  and only a walked-into reach takes a transition.
- No code proposes `TransitionKind.PaidService` (it appears in `src/` only in `MightAndMagic7TravelCostRule`'s
  switch), so a passage bought at a counter can never be boarded. Routed as Den **#8703**.
- The walkable component of place 3 is six places; the only walkable region-to-region link in the graph is
  Harmondale ↔ The Land of the Giants; portals reach only places already marked visited.

So the completion and the hand-in were reached from **staged poses**: the party's place and pose in the
session save were moved to Castle Navan's own `Party Start (0, -1472, 224)` and back to Erathia's Org House
stand-off (`-7503, 8713, 460 @ 964`), each time resuming with `RUSTY_CRAWLER_START=resume`. The errand, the
clock, the journal, and the party are the same session throughout; only the pose is staged. `pose.py` here
does the staging and `save-errand-completed.bin` / `save-day2.bin` are the documents used.

Arriving at Castle Navan cost the party a member: the resumed level-1 party met the place's own creatures and
the panel read `Condition Dead`, `Vitality 80 / 120` within about twenty seconds. The turn-in, the promotion,
and the record all completed anyway, and the promotion was granted to the Knight whose 40 maximum hit points
are the 40 the party had lost — worth a design call rather than a defect report: the ranks panel's own
sentence does not mention the condition.

## Check 2 — the journal pass

| step | the panel |
| --- | --- |
| 1. the five books | `Current Quests` — `"The party has been offered nothing."` (then `1 errand in the journal`); `Auto Notes` — `"The party has learned nothing worth noting yet."`; `Maps` — `1 place mapped`, row `Erathia · region · 64 of 16384 squares walked (0%)`; `Calendar` — `1168-01-01`, rows `Today`, `Time 09:00 · day`, `Days since the expedition began 0`; `History` — `1 entries`, `Entered Erathia · 1168-01-01 09:00 · Erathia · world` |
| 2. walk into a new place | walked from 41 units north into the sewers reach (`148.6025`, centre `-2176, 14856, 128`, radius 176): the projection published `place 'The Erathian Sewers'` at `6647, 3511, 4294966785`, and the runtime then answered `CSHARP_RUNTIME_TAINTED` on every route, so the arrival's History line could not be read. The dated place line itself is confirmed twice: `Entered Erathia · 1168-01-01 09:00` above, and `Entered Castle Navan · 1168-01-01 15:46 · Castle Navan · world`. Routed as Den **#8702** |
| 3. meet somebody, take an errand | `Met Frederick Org · 1168-01-01 13:35 · Erathia · conversation`, `Was offered … · 14:01 · quest`, `Took on … · 14:06 · quest` |
| 4. save, then wait | `save` → `"Saved the session to slot 'session' at 1168-01-01 16:48."`; `rest.wait-dawn` → `"The party waits for 12 hour(s) 5 minute(s), to 1168-01-02 05:00. Waiting rests nobody: the clock moved and nothing was restored."`, Calendar `1168-01-02 05:03`; saved again at `1168-01-02 05:07` |
| 5. resume, read the dates | restarted the unit with `RUSTY_CRAWLER_START=resume`: Calendar `1168-01-02 05:17` while every line still reads its own day — `Entered Erathia · 1168-01-01 09:00`, `Finished The Elven Treasury at Castle Navan · 1168-01-01 16:26`, `Was raised to Cavalier · 1168-01-01 16:37`. The save itself carries the lines as `elapsedMilliseconds`, never as dates |

## Commands that worked

```sh
playtest start rusty-crawler-b                       # session id; observe/inspect read the panel
python3 show.py --seconds 6 --out projection.json    # the newest crawler.hud envelope, in the panel's words
python3 act.py session.save                          # a semantic action over the host's direct intent channel
python3 act.py conversation.topic '{"target":"turn-in:35"}'
python3 act.py party.move-forward --digital          # one claim per POST = one admitted update
python3 walk-intents.py -2176 14960 --stop 60        # walks by claiming the party's own declared intents
systemctl --user set-environment RUSTY_CRAWLER_START=resume && systemctl --user restart rc-live-b.service
```

Two traps worth knowing: a payload claim with a stale `"sequence"` is queued and dropped in silence (the
sequence must be read from the binding just before the post, and it must be a **string**); and while a
conversation is open the session reads no movement input at all, which looks exactly like keys not arriving.

## Defects routed

- **#8702** — arriving in The Erathian Sewers taints the runtime (`CSHARP_RUNTIME_TAINTED`) after publishing
  the arrival pose with a wrapped z (`4294966785`); the arrival's History line could not be read.
- **#8703** — a passage bought at a counter can never be boarded, so paid travel is unreachable in play and
  content whose only route is a fare cannot be played.
- **#8704** — after a dev-host restart, held keys from a freshly attached browser page never reach the
  product while direct intent claims do; the session reports `connected`.
