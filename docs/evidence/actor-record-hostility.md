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
