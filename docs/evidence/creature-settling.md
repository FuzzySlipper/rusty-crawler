# A creature content stood under Harmondale's ground

Live reading for the creature-motion owner (`EngineCreatureMotion`, #9041): before the change, a creature step in
Harmondale faulted the runtime; after it, the same staged session ran thirteen minutes of simulated time with the
party walking among the place's creatures and no fault.

## What was run

```sh
dotnet src/MightAndMagic7.Import.Tool/bin/Release/net10.0/mm7import.dll write \
  --install <operator-install> --output content/partyrpg/imports --check-determinism
# a hand-written scenario pack (ignored, not committed): content/partyrpg/imports/live-9041-scenario —
#   place 2 (Harmondale), entry point "Party Start", "party": "scenario", two members with a deep health pool
#   so the place's guards could not end the run
# the tracked bundle temporarily named mm7-tables, mm7-world and live-9041-scenario; restored with
#   `git checkout -- content/partyrpg/bundles` afterwards, the scenario pack removed, the host stopped
rusty dev --project src/PartyRpg.Host/PartyRpg.Host.csproj --port 4181 --bind-host <lan-address> --live-debug
# input through the harness lane (control/claim, runtime/input, control/release); readings are playtest.observe
```

Staging, not play: the party was saved at the Party Start, the save's `world.pose` rewritten to (7300, -5248, 400)
beside Harmondale's shrine — where #8713's reading faulted — and served again with `RUSTY_CRAWLER_START=resume`.

## Before: the fault

Within the first seconds of the resumed session, the host log read:

```text
rusty: product update faulted: CSHARP_PRODUCT_CALL: Spatial.ProposeCharacterStep returned status 0:
EngineCallException: ... unresolved-character-controller-penetration: UnresolvedPenetration { depth: 45.22667 }
(at PartyRpg.Kit.Combat.EngineCreatureMotion.Move(CreatureMoveRequest request) ...)
```

## Why

The maps' spawn records state a nominal height: Harmondale's goblin spawns (`encounter-1` to `-3`, `-30`, `-40`)
are written at height 0 on ground that rises to 96–185 units above them under the record's point or its spread
circle. The donor keeps that height and lifts an outdoor actor below the floor onto it on its next update
(OpenEnroth `src/Engine/Graphics/Outdoor.cpp:1648-1649`). The engine's controller recovers a body that starts a
little inside collision, by a bounded distance per step, and refuses one it cannot recover with
`unresolved-character-controller-penetration`; its C# lifecycle guide leaves what then happens to that actor to the
product. The creature mover let that refusal escape the update, which stopped the session. The engine behaved as
its contract states, so nothing is routed to the engine.

An offline estimate over the imported placements (the collision surfaces over each creature record's point and
spread circle, places 1–15) found such buried records in most of those places — eighteen in Harmondale — some
more than a thousand units under the ground (place 4's Treant spawns, place 6's Golem spawns).

## After: settled, and a sustained run

With the change, the mover catches only that refusal, casts the engine's ray up the creature's column from its
feet, stands it on the first surface it meets (within the ruleset's reach of 8192 units, a region's whole ground
range), and steps it from there; a creature with no such ground, or still refused there, would be held as `stuck`
with a `creature-embedded` refusal. A temporary trace (removed before commit) printed every settle and hold:

```text
settled actor:58 from (2560,1032,0) to z 95.5
```

That is one of `encounter-2`'s goblins, buried 96 units on the offline pass. No creature was held. Readings of
`playtest.observe` over one continuous session (the party walked toward the goblin spawns, up a rise and down):

```text
16:05:48 steps   122 sim   2 pose (7300,-5248,66)  hp 59966 hostile  8 {waiting 3, backing away 2, down 1, closing 2}
16:09:22 steps 12978 sim 216 pose (7647,-5696,58)  hp 59966 hostile  9 {down 3, backing away 1, closing 1, waiting 4}
16:17:42 steps 42966 sim 716 pose (5935,-3637,66)  hp 59966 hostile 13 {down 5, closing 2, waiting 6}
16:18:15 steps 44964 sim 749 pose (5338,-2774,401) hp 59966 hostile 16 {down 6, backing away 2, closing 1, waiting 7}
16:18:44 steps 46711 sim 779 pose (4618,-1926,2)   hp 59966 hostile 18 {down 7, backing away 1, closing 2, waiting 8}
```

Seven hundred and seventy-nine simulated seconds, 46,711 admitted steps, the party moving throughout, creatures
closing, backing away, attacking and going down in the place's own fights between its factions, and no fault in
the host log. The cases are held by `EngineMovementTests` (settled, held by name, still refused once settled, and
any other engine failure still surfacing) and `MonsterAiTests` (a held creature is reported `stuck` and the fight
goes on).
