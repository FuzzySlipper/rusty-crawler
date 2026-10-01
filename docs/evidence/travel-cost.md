# What a crossing costs, from the running product

> Published copy of a point-in-time live-check record kept in the operator's ignored local verify tree.
> It is evidence of what was observed then, not a statement of current behaviour. Screenshots, scripts,
> logs and saves it names were not published; LAN addresses are replaced by `<lan-address>`.

This directory records the live check Den task #8563
asked for: the product served by its real dev runner, driven by a keyboard through the agent playtest
service's remote browser, with the HUD read from screenshots.

The two receivers the task wires are now visible on the panel: the **Date/Time/Days** rows are the
session's one clock, and the **Party/Coins/Food/Standing/Condition** rows are the party content declares.
A walked crossing charges the clock the day the cost rule quoted and the larder the rations that day eats.

## What was run

```sh
# the local live-check scenario (ignored, hand-written) was staged for this check:
#   content/partyrpg/imports/mm7-live-scenario/party.json   -> a scenario-party: 2 members, 200 coins, 2 portions
#   content/partyrpg/imports/mm7-live-scenario/scenario.json -> place 52 (The Dragon's Lair), entry "Party Start"
#   both declared in that pack's pack.json; the start place was restored to place 1 afterwards
#   (the party document is left in place: it is what makes a live check show the party at all)

# 1. serve the product (managed background job, never pkill)
./.runtime/runtime-pack/bin/rusty dev --project ./src/PartyRpg.Host/PartyRpg.Host.csproj \
  --runtime ./.runtime/runtime-pack --bind-host <lan-address> --port 4176

# 2. playtest against it (the binary is ~/.local/bin/playtest)
playtest start rusty-crawler
playtest run <session> --file local/verify/travel-cost/walk-1.js --budget-ms 60000
playtest run <session> --file local/verify/travel-cost/walk-2.js --budget-ms 45000
playtest stop <session>
```

The keyboard only reaches the page while the browser holds focus, so `walk-1.js` holds `P` first and the
`02-paused.png` frame shows the session paused (`Session paused`, `Resume session`): the keys arrived, and
the walk that follows is trustworthy.

## The crossings

The party starts at The Dragon's Lair's own start point `924, 2248, 36`, which is *inside* the reach of
the lair's exit entrance, so the walk first leaves that reach backwards (`S`, west at facing 0) and then
walks forwards (`W`, east) back into it. On the island the party arrives inside the cave-mouth reach, so
the second crossing is made the same way: west (`S`) back into it.

| screenshot | what was held before it | Place | Date | Days | Food | Condition | Position | Explored | Admitted steps |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `01-before-crossing.png` | nothing (start) | 52 The Dragon's Lair | 1168-01-01 | 0 | 2 portions | — | `924, 2248, 2 @ 0` | 1 / 76 | 2385 |
| `02-paused.png` | `P` 300 ms | 52 | 1168-01-01 | 0 | 2 portions | — | `924, 2248, 2 @ 0` | 1 / 76 | 3282 |
| `04-out-of-reach.png` | `S` 1.0 s (out of the exit reach) | 52 | 1168-01-01 | 0 | 2 portions | — | `534, 2248, 2 @ 0` | 1 / 76 | 3402 |
| `05-after-crossing-one.png` | `W` 1.5 s (into the reach) | **1 Emerald Island** | **1168-01-02** | **1** | **1 portions** | — | `14229, 16368, 169 @ 1` | **2 / 76** | 3543 |
| `06-settled-one.png` | released, +0.6 s | 1 | 1168-01-02 | 1 | 1 portions | — | `14229, 16368, 169 @ 1` | 2 / 76 | 3625 |
| `07-island-before-second-crossing.png` | nothing | 1 | 1168-01-02 | 1 | 1 portions | — | `14229, 16368, 169 @ 1` | 2 / 76 | 6215 |
| `08-entering-the-reach.png` | `S` 0.7 s (west into the cave mouth) | **52 The Dragon's Lair** | **1168-01-03** | **2** | **0 portions** | **weak (1)** | `784, 2228, 1 @ 1012` | 2 / 76 | 6265 |
| `09-settled-second-crossing.png` | released, +0.4 s | 52 | 1168-01-03 | 2 | 0 portions | weak (1) | `784, 2228, 1 @ 1012` | 2 / 76 | 6286 |
| `10-west-again.png` | `S` 0.4 s (away from the exit reach) | 52 | 1168-01-03 | 2 | 0 portions | weak (1) | `488, 2228, 1 @ 1012` | 2 / 76 | 6325 |

Read off the panel:

* **The clock is the session's one clock, and a crossing charges it once.** The date stands at
  `1168-01-01` before the first crossing and at `1168-01-02` after it, and the second crossing takes it to
  `1168-01-03`; `Days` follows at 0 → 1 → 2. Between crossings the time of day moves only with the admitted
  simulation (`09:27` at 54.7 s, `09:29` at 59.0 s, `09:51` at 104.0 s — thirty game seconds per real
  second), which is the clock being stepped by the one admitted update rather than by the journey.
* **The larder is the party's one store, and it is charged once per crossing.** `2 portions` before,
  `1 portions` after the first crossing, `0 portions` after the second.
* **Arriving short weakens the party.** The second crossing spends the last portion the larder holds, so
  the day is covered only in part, and the ruleset's hunger condition lands on every member: the
  `Condition` row reads `weak (1)` from that crossing on. Nothing else on the panel changed for it.
* **The place, the arrival pose, and the explored count are the transition's own**, unchanged by any of
  this: place 1 is marked visited by the same path as any other transition (`2 / 76`), and the pose shown
  is the link's arrival point plus however far the same key hold walked on.

Not exercised live: the recovery half of the hunger rule. It needs a day the larder covers, and the
shipped product has no shop, no provision find, and no rest owner yet, so the only way to feed this party
would be content that starts it with food it did not spend. Since this reading the rule changed: a fed
day no longer clears the condition, and a completed rest does, as the donor's full rest does
(`TravelPolicyTests.A_day_eats_one_ration_and_an_empty_larder_weakens_the_party_until_it_rests`).
