# Creating a party and paying for a crossing, from the running product

> Point-in-time live-check record. It is evidence of what was observed then, not a statement of current
> behaviour. Screenshots, logs and saves it names were not published; LAN addresses are replaced by
> `<lan-address>`.

This records one fresh run of the product for Den tasks #8481 (creation as a real product flow), #8563
(travel cost charged to the clock and the larder) and #8580 (creation reachable through the session): a party
made through the creation screen, with illegal choices refused by name, accepted into the world, and walked
across two transitions that each charged one day and one ration. State was read with `playtest.observe` and
the panel's rows (`docs/live-checks.md`), and the party was then saved and the save document read back.

## What was run

```sh
# packs written from the operator's own install into the checkout's ignored import root
dotnet src/MightAndMagic7.Import.Tool/bin/Release/net10.0/mm7import.dll write \
  --install <operator-install> --output content/partyrpg/imports
# a hand-written scenario pack (ignored, not committed): content/partyrpg/imports/live-creation-scenario,
#   one scenario-start entry, place 20 (The Temple of the Moon), entry point "Party Start"; no party,
#   so a host that declares a creation screen creates one
# the tracked bundle temporarily named mm7-tables, mm7-world and live-creation-scenario, and was restored
#   with `git checkout -- content/partyrpg/bundles` afterwards

rusty dev --project src/PartyRpg.Host/PartyRpg.Host.csproj --port 4180 --bind-host <lan-address> --live-debug
playtest start <profile on <lan-address>:4180>      # the panel was driven with `browser` clicks and fills
```

A first attempt started at The Dragon's Lair (place 52), as earlier records did, and is not the run below: the
lair's own actor records now stand a Red Dragon there, and it killed the default party with one ranged attack
("79 Fire damage landed") from 3372 units within two seconds of acceptance. Place 52 no longer serves as a
benign start for a live check.

## Creation, before any party exists

`playtest.observe` on the fresh session: `mode: creating`, `admittedSteps: 0`, `simulationSeconds: 0`,
`place: null`, `pose: null`, `party.present: false`, `screens.creation: true`, clock `1168-01-01 09:00`.
Holding `W` for 1.5 s changed nothing (`movement.moved: false`, pose still `null`, clock still `09:00`), and
`playtest.action party.move-forward` answered `available: false` — "A party is still being made: the party
walks once creation is accepted." — while `playtest.action creation.advance` answered `available: true`
(`Enter`). Every reading below was taken minutes of real time later, with the clock still at `09:00` until the
party was accepted.

The screen opened on the ruleset's default party, offered as choices and already validated: Roderick (Knight),
Aelina (Sorcerer), Borin (Cleric), Nyx (Thief), each `complete`.

| Panel action | Step head after it | Refusal shown (the flow's own sentence) |
| --- | --- | --- |
| reopen `4. Nyx · Thief · complete` | Creating member 4 of 4 · portrait | — |
| `Monk` (a class, at the portrait step) | Creating member 4 of 4 · portrait | Choosing a class happens at the Class step, and member 4 is at the Portrait step; creation's steps are taken in order. |
| `Goblin man`, `Confirm step` | … · class (attributes re-derived: **50 points left**) | — |
| `Monk`, `Confirm step` | … · name | — |
| `Set name` with the field blank | … · name | A character's name cannot be blank; every member of the party is named before the game starts. |
| name `Grub`, `Set name`, `Confirm step` | … · attributes | — |
| `Confirm step` with the pool untouched | … · attributes | The pool of 50 attribute points must be spent exactly and 50 remain unspent. |
| `+` on Might ×16, Speed ×16, Endurance ×14, Accuracy ×14, Luck ×6 | … · attributes, **0 points left** | — |
| `+` on Luck once more | … · attributes | Raising Luck by 1 costs 1 of the 0 attribute points left; the pool of 50 is spent exactly, never overdrawn. |
| `Confirm step`, `Staff`, `Confirm step` | … · skills | A character starts with 2 chosen skills and 1 remains unchosen. |
| `Accept party` (member 4 unfinished) | … · skills | The party cannot be accepted while creation is unfinished: member 4 is at the Skills step. |
| `Armsmaster`, `Confirm step` | Every member is finished; accept the party, or reopen one to change it. | — |

The panel offered Grub's skills as `Dodging (fixed)`, `Unarmed (fixed)` and the Monk's choosable list; the
attributes it showed after spending were Might 30, Intellect 7, Personality 7, Endurance 25, Accuracy 25,
Speed 30, Luck 15 against the goblin's ranges.

## Accepted, and walked across twice

`Accept party` turned the step head to `Party accepted` and listed the party the factory built:

```
1. Roderick · Human · Knight · human-man
2. Aelina · Elf · Sorcerer · elf-woman
3. Borin · Dwarf · Cleric · dwarf-man
4. Grub · Goblin · Monk · goblin-man
```

| Reading | Mode | Place | Date / time | Days | Food | Coins | Condition | Position |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| just accepted | running | 20 The Temple of the Moon | 1168-01-01 09:00 | 0 | 10 portions | 200 | — | `-1152, -3968, 320 @ 0` |
| `playtest.look 90 0`, held `W` 1.8 s, `W`+`Space` 2.5 s up the stair to the exit | running | **1 Emerald Island** | **1168-01-02** 09:07 | **1** | **9 portions** | 200 | — | `15487, 12161, 1134 @ 1024` |
| `playtest.look 180 0`, held `W` 1.2 s back into the temple door's reach | running | **20 The Temple of the Moon** | **1168-01-03** 09:12 | **2** | **8 portions** | 200 | — | `-1105, -4069, 322 @ 320` |

Each crossing moved the date one day and the larder one ration, once; between crossings the time of day moved
only with the admitted simulation. `Explored` read `1 / 76`, then `2 / 76`. The party arrived with food to
spare, so the hunger case was not reached live here; it is shown live in [`travel-cost.md`](travel-cost.md)
and proved by `TravelCostWiringTests.A_party_that_arrives_short_is_weakened_and_a_day_it_covers_ends_it` and
`TravelPolicyTests.A_day_eats_one_ration_and_an_empty_larder_weakens_the_party_until_it_is_fed`.

## The save carries the created party

`F` (the declared `session.save`) at the last row wrote the checkout's persistence slot (`Save` row:
`1168-01-03 09:15 · session`). The document read back (`RSP2` header, then JSON with sections `party`, `clock`,
`world`, `quests`, `journal`, `knowledge`, `maps`):

- `clock.elapsedMilliseconds: 173759000` — two days and sixteen minutes past `1168-01-01 09:00`;
- `party.foodPortions: 8`, `party.coins: 200`;
- `world.pose.place: "20"`, `world.places.elapsedGameDays: 2`;
- the four members with their creation choices, portrait included: `Roderick / Human / Knight / human-man`,
  `Aelina / Elf / Sorcerer / elf-woman`, `Borin / Dwarf / Cleric / dwarf-man`,
  `Grub / Goblin / Monk / goblin-man`, Grub with attributes 30/7/7/25/25/30/15 and skills Dodging, Unarmed,
  Staff, Armsmaster.

The host was stopped, the playtest session released, and the bundle restored afterwards.
