# Turn-based pacing in the live product

> Published copy of a point-in-time live-check record kept in the operator's ignored local verify tree.
> It is evidence of what was observed then, not a statement of current behaviour. Screenshots, scripts,
> logs and saves it names were not published; LAN addresses are replaced by `<lan-address>`.

`turn-based-check.mjs` (one level up) drives the staged
product through the runtime pack and samples the panel's own DOM, so every reading below is what the
product published rather than what a test asserted.

```bash
# the imported packs, regenerated from the operator's own data, plus one hand-written scenario, and a
# product staged from the same content root
dotnet run --project src/MightAndMagic7.Import.Tool -- write \
  --install /home/research/old-games/game-mm7 --output content/partyrpg/imports
# content/partyrpg/imports/mm7-turn-scenario is the scenario this check starts from, and the default bundle
# names exactly mm7-tables, mm7-world, and it. The imports root must hold only ONE scenario-start pack for
# the run: `MightAndMagic7World.ReadStart` takes the first one the whole catalog holds
# (src/PartyRpg.Rulesets.MightAndMagic7/MightAndMagic7World.cs:257-268) and does not consult the bundle's
# selection, so a leftover scenario pack in the same root hijacks the start (defect #8657). Both
# mm7-loot-scenario and mm7-turn-scenario were in that root while this ran, and the loot one won until it was
# moved aside; it was moved back afterwards, so a re-run has to move it aside again (or name only one).
dotnet msbuild src/PartyRpg.Host/PartyRpg.Host.csproj -t:StageRustyEngineCoreClrProduct -p:Configuration=Release
./.runtime/runtime-pack/bin/rusty dev --project ./src/PartyRpg.Host/PartyRpg.Host.csproj \
  --runtime ./.runtime/runtime-pack --bind-host 127.0.0.1 --port 4198
CRAWLER_URL=http://127.0.0.1:4198/ CRAWLER_EVIDENCE=local/verify/turn-based \
  node local/verify/turn-based-check.mjs
```

The scenario starts the party in Barrow III (place 57) — an imported interior whose own spawn records put
fourteen creatures on the floor, ghasts and giant rats — and the default party is accepted through the
creation flow.

What `live-samples.json` recorded on 1168-01-01, in one run:

| Sample | Panel |
| --- | --- |
| `02-real-time-the-partys-hurt` | `running`, `realtime`, 12 hostiles. A giant rat struck first: `Roderick — 39/40 hp — Disease Weak (1)`, the other three unhurt. Pose `-140, -1056, 0 @ 0` |
| `03-switched-to-turn-based` | `turnbased`, round 1, action phase, `Nyx`'s turn (yours), `25.8s round`, due in 6.0s. **The fight is the same fight**: `Roderick — 39/40 hp — Disease Weak (1)` and the other three pools are exactly what real time left, the twelve hostiles are the same twelve, and the pose has not moved |
| `04-fought-in-rounds` | `turnbased`, round 1, **movement phase** — `Move: round 1, 18.9s of movement left`. Opposition 12 → 11: `Giant Rat — 0/6 hp — attacking — down`, and the order reads that rat `out of the fight`. Members still `39/40 — Disease Weak (1) / 20/20 / 25/25 / 35/35` |
| `05-switched-back-to-real-time` | `running`, `realtime`, phase `none`. The same values again — the wound, the condition, the down rat (`0/6 hp — down`), the eleven hostiles, the same pose |
| `06-continued-in-real-time` | the fight goes on: the clock moved `09:01` → `09:03`, eleven hostiles, and every value above unchanged |
| `07-switched-again` | `turnbased`, round 1, `Roderick`'s turn (yours) — the state survived a second switch, still `39/40 hp — Disease Weak (1)` with the rat still down |
| `08-acted-again` | committing Roderick's turn spent his recovery (`recovering 23.0s`) and handed the turn to `Aelina` |

The turns inside the paced round, from the same run's trail:

* `turn 0` — Nyx acted: `Nyx hits Giant Rat (melee): 6 Phys damage landed`, which is the rat that ends up
  `0/6 hp — down`; the turn passed to Aelina.
* `turn 1` — Aelina acted (`attacks nothing in reach`), turn to Roderick.
* `turn 2` — Roderick **skipped**: the panel read `last=skip`, the turn passed to Borin, and the skipped
  member was charged the recovery of the action he did not take.
* `turn 3` — Borin **waited**: `last=wait`, and the round ran on to its movement phase.
* `turn 4` — the action phase ended into the party's movement phase with 18.9s of movement left.

Two things this shows and one it does not.

* It shows one state in two pacings: the same health, the same condition, the same body, the same
  opposition count, the same pose throughout, and time moving only when the pacing says it does — a whole
  paced round passed in which nobody's recovery was released between turns, and real time resumed from the
  recovery the paced round left.
* It shows the round structure in play: initiative (the ready party members first, then the creatures as
  their own recoveries elapse), the action phase taking a turn at a time, skip and wait with their stated
  consequences, and the movement phase closing the round.
* It does **not** show the party bringing a fight to its end in rounds, or a creature reaching the party
  from across the room: these places still carry no navigation projection, so a creature that cannot see
  its target walks straight at it and can be walled off (#8665). The live check therefore walks the party
  into reach before the switch, exactly as the panel's own distances say.
