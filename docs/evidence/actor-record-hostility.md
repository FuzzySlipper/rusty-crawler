# A level's own guards stand peaceful; its own hobgoblins still fight

Live reading for #9055: Harmondale's guards, stood by the map's own actor records, attacked a fresh party at
Party Start (#9041 found a 1,800-hit-point party dead within about a minute). With the change, the ruleset reads
an actor record's standing toward the party the donor's way — the record's aggressor bit, the kind it counts as,
and otherwise what that kind thinks of the party in the shipped matrix — and a fresh party stood at Party Start
for fourteen simulated minutes untouched, while the same place's hobgoblin records still engaged it.

## What was run

```sh
dotnet src/MightAndMagic7.Import.Tool/bin/Release/net10.0/mm7import.dll write \
  --install <operator-install> --output content/partyrpg/imports --check-determinism
# a hand-written scenario pack (ignored, not committed): content/partyrpg/imports/live-9055-scenario —
#   place 2 (Harmondale), entry point "Party Start", "party": "scenario", two level-1 members of 40 hit points
# the tracked bundle temporarily named mm7-tables, mm7-world and live-9055-scenario; restored with
#   `git checkout -- content/partyrpg/bundles` afterwards, the scenario pack and the save removed, the host stopped
rusty dev --project src/PartyRpg.Host/PartyRpg.Host.csproj --port 4182 --bind-host <lan-address> --live-debug
# readings are playtest.observe; the save was taken with F through the harness lane (control/claim, input, release)
```

## At Party Start

Two guard records (`actor-39`, `actor-40`) stand about 1,440 units from Party Start and a third about 4,400; their
row's band (3, 5,120 units) is what made them engage before. Readings of `playtest.observe`, never moving:

```text
16:46:12 steps 37890 sim 632  pose (-16832,12512,373) engaged false hostile 0  Aelina 40/40 Borin 40/40
16:46:48 steps 40263 sim 671  pose (-16832,12512,373) engaged false hostile 0  Aelina 40/40 Borin 40/40
16:49:35 steps 50097 sim 835  pose (-16832,12512,373) engaged false hostile 0  Aelina 40/40 Borin 40/40
```

## Beside the hobgoblins (staging, not play)

The save's `world.pose` was rewritten to (6600, 4100, 600), about 1,100 units from Harmondale's two nearest
hobgoblin records (`actor-52`, `actor-53`; their kind is in the party's row at band 2, 2,560 units), and served
again with `RUSTY_CRAWLER_START=resume`. The staged height dropped the party some 470 units, which cost a member
six hit points on landing:

```text
16:51:16 sim  0 engaged true hostile 4 [Hobgoblin 1074 waiting, Hobgoblin 1125 waiting, Hobgoblin 2400 waiting, Hobgoblin 2402 waiting]  Aelina 34 Borin 40
16:51:21 sim  5 engaged true hostile 4 [Hobgoblin 1070 closing, Hobgoblin 1065 closing, Hobgoblin 2255 attacking, Hobgoblin 2242 waiting]  Aelina 0 Borin 0
```

All four hobgoblin records are in the fight; the encounter goblins 1,151 and 1,313 units off are not, because an
encounter's creature still notices at its row's band (1, 1,024 units).

## Over the operator's install

Every actor record that names no person, read through the same rule (aggressor bit, else the party's row of the
matrix at the record's kind). No record carries the aggressor bit or names another kind. Before the change every
standing record was hostile, because every monster row states a band of one to four.

| | standing | hidden |
| --- | ---: | ---: |
| peaceful | 503 | 49 |
| hostile | 19 | 132 |

Towns, castles and temples: Harmondale 43 peaceful and 4 hostile standing (the hobgoblins); Erathia 37, Tularean
Forest 33, Bracada Desert 29, Celeste 52, The Pit 19, Mount Nighon 30, Tatalia 37, Avlee 39, Stone City 49, Emerald
Island 28 all peaceful; Deyja 14 peaceful and 8 hostile (hobgoblins); Castle Gryphonheart 6, Castle Navan 26, Castle
Lambent 5, Castle Gloaming 6, The Temple of Baa 35, The Temple of the Moon 3, the two Grand Temples 1 each, The
Mercenary Guild 7 all peaceful; Castle Harmondale's 62 records are all hidden. The standing hostile ones are the
two places' hobgoblins and seven lone lair creatures (a dragon in each of The Dragon Caves and The Dragon's Lair, and
one each in Clanker's Laboratory, The Red Dwarf Mines, The Maze, Colony Zod and The Hall under the Hill).

## Spawned creatures read the same matrix (#9059)

The paragraph above records how an encounter's creature noticed the party when #9055 was read live: at its row's
band. #9059 matched the donor for those too. An encounter's creature takes its monster type as its faction and has
its row's hostility overwritten with friendly (OpenEnroth `src/Engine/Objects/Actor.cpp:4331-4334`), and a map
event's summoning is the same spawn with the event's point and group and no aggressor
(`src/Engine/Evt/EvtInterpreter.cpp:77-99`, calling `SpawnEncounter` with `aggro` 0), so `_SelectTarget` picks the
party only when the kind's relation to the party in the matrix is not friendly, and notices it at that band's
distance (`Actor.cpp:2097-2116`, `:2122-2166`). The rows' own bands decide nothing toward the party. Only the
rest-encounter ambush stands its creatures as aggressors (`src/Engine/Graphics/Indoor.cpp:1815`); this build's broken
night stands no creature.

Counted over the operator's install by the imported case
`SpawnedHostilityPolicyTests.Over_the_operators_install_the_matrix_decides_which_spawned_kinds_start_fights`
(`CRAWLER_IMPORTED_CONTENT` pointed at a root `mm7import write` filled): every kind an encounter placement or a
map event's summoning names, by the party's row of the matrix at that kind. Before the change every one of them
attacked on sight, because every monster row states a band of one to four.

| | kinds | encounter placements | event summonings |
| --- | ---: | ---: | ---: |
| peaceful until attacked | 25 | 708 | 4 |
| hostile | 30 | 1,092 | 7 |

Peaceful (band 0): Angel, Archer, Cleric Moon, Cleric Sun, Dwarf, Elf Archer, Elf Spearman, Fighter Chain, Fighter
Leather, Fighter Plate, Gargoyle, Ghoul, Golem, Griffin, Harpy, Lich, Mage, Monk, Necromancer, Roc, Swordsman,
Treant, Vampire, Warlock, Zombie (the four zombie summonings).

Hostile, with the band toward the party and so the distance it notices the party at: band 4 (10,240) Bat,
Beholder, Dragon, Genie, Ghost, Gog, Robot, Sea Monster, Titan, Wyvern; band 3 (5,120) Behemoth, Devil, the four
Elementals, Hydra, Medusa, Spider, Thief, Troglodyte; band 2 (2,560) Dragonfly, Goblin, Minotaur, Rat, Skeleton
Warrior, Troll, Wight, zBlasterGuy; band 1 (1,024) Ooze. The seven hostile summonings are three dragonfly and four
goblin ambushes.

A creature the party attacks is its enemy from then on whatever its kind (the donor's aggressor bit, set on the
creature struck, `Actor.cpp:706-708` from `:3153-3154`), and a camp is refused only beside a creature that is the
party's enemy, as the donor's proximity check reads the same relation (`Actor.cpp:3458-3481`).
