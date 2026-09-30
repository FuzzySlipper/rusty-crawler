# Ranks in the live product: a character promoted twice, and the school its path closed

> Published copy of a point-in-time live-check record kept in the operator's ignored local verify tree.
> It is evidence of what was observed then, not a statement of current behaviour. Screenshots, scripts,
> logs and saves it names were not published; LAN addresses are replaced by `<lan-address>`.

`../promotions-check.mjs` drives the staged product — the
runtime pack's real dev runner, this box's own Chromium — and samples the panel's DOM, so every reading below
is what the product published rather than what a test asserted. `../promotions-stage.sh` puts the staging in
place and `../promotions-unstage.sh` puts the content root back; both are local scaffolding.

```bash
# the imported packs, regenerated from the operator's own data into the product's content root
dotnet run --project src/MightAndMagic7.Import.Tool --configuration Release -- write \
  --install /home/research/old-games/game-mm7 --output content/partyrpg/imports

# the staging below (one scenario pack, the bundle naming it, and four authored placements patched into the
# imported Harmondale), then the product on its own port as a managed background job
./local/verify/promotions-stage.sh
./.runtime/runtime-pack/bin/rusty dev --project ./src/PartyRpg.Host/PartyRpg.Host.csproj \
  --runtime ./.runtime/runtime-pack --bind-host 127.0.0.1 --port 4212

# drive the panel and read it
CRAWLER_URL=http://127.0.0.1:4212/ CRAWLER_EVIDENCE=local/verify/promotions \
  node local/verify/promotions-check.mjs
./local/verify/promotions-unstage.sh
```

`live-samples.json` holds every sample whole, and `host.log` is the product's own run.

## What the live run read

| Sample | Panel |
| --- | --- |
| `01-accepted-party` | the created party: `Roderick — Knight of rank 1`, `Aelina — Sorcerer of rank 1`, `Borin — Cleric of rank 1`, `Nyx — Thief of rank 1`; purse 200 |
| `02-chest-searched` | `A chest · search · searched · 90` — the shipped item table's own golem parts, lich jars, and Light book into the pack, and the purse to 5246 from three treasure-level references |
| `03-talking-to-the-first-promoter` | `Speaking with Thomas Grey`, whose own greeting the shipped NPC table carries; the topic `promote:sorcerer-wizard` labelled `Wizard` on offer |
| `04-first-promotion-granted` | **`Aelina rose from Sorcerer (rank 1) to Wizard (rank 2): met granted by Thomas Grey; carries Golem chest; carries Golem head; carries Golem left leg; carries Golem right leg; carries Golem right arm; carries Golem left arm`** |
| the ceiling across it | Aelina's `Fire · magic · basic · 1/9 (expert)` before → **`1/18 (master)`** after, and `Air` the same: the class's own row of the donor's mastery table |
| `05-talking-to-the-dark-promoter` | `Speaking with Halfgild Wynac`, the person the shipped tables name for the dark alternative, offering `promote:wizard-lich` |
| `06-second-promotion-the-dark-path` | **`Aelina rose from Wizard (rank 2) to Lich (rank 3), the dark path: met granted by Halfgild Wynac; carries Lich Jar; carries Case of Soul Jars`** |
| the ceiling across it | `1/18 (master)` → **`1/60 (grand master)`** for both element schools, and Aelina's row now reads `Aelina — Lich of rank 3` with `No rank leads on from here.` |
| `07-the-light-rank-withheld` | the person who gives the light rank still offers it, and the panel withholds it with the reason the party's own state produces: `Arch Mage — nobody in the party is a Wizard, and the rank of Arch Mage is given to one` |
| `08`–`09` | the operator's own Light Guild (`Guild of Illumination`, kept by Lews) open at its hours, and `The party pays 2000 coin(s) for Light Guild membership` (purse 5246 → 3246) |
| `10-the-opposed-school-refused` | **`Aelina took the dark path of the Lich: it takes Dark and leaves Light to the other alternative, so a Lich may hold no Light.`** — the guild's own lesson for the Light school, refused with the choice named |
| Panel errors | none |

Frames: `01-accepted-party`, `02-chest-searched`, `03-talking-to-the-first-promoter`,
`04-first-promotion-granted`, `05-talking-to-the-dark-promoter`, `06-second-promotion-the-dark-path`,
`07-the-light-rank-withheld`, `08-the-light-guild-counter-open`, `09-light-guild-membership-bought`,
`10-the-opposed-school-refused`.

## What the operator's data carries and what this game authors

| Thing | Where it came from |
| --- | --- |
| The 36 class rows (9 families × base, first promotion, light second, dark second) and their names | the operator's `CLASS.TXT`, through the importer (`mm7-tables/classes.json`); the ladder is written in those names and checked against those rows by a test |
| Who gives each of the 27 ranks | the operator's `npc.txt` notes column ("Good Sorcerer promoter", "Evil Cleric promoter", …) and the `npctopic` owner columns, which name the rank each promoter gives; the ladder carries the row numbers as identities (`npc-48` Thomas Grey) |
| Each rank's errand | the operator's quest table: bit 18 the vase for William Lasker, bit 30 the Perfect Bow for Lawrence Mark, bit 45 the six golem parts for Thomas Grey, bit 48 the lich jars for Halfgild Wynac, one row per rank in between |
| The proof items the party brings | the operator's item table's own rows: `624 Vase`, `620 Big Tapestry`, `542 The Perfect Bow`, `647 Dragon Egg`, `639/641-645` the golem parts, `601 Lich Jar`, `602 Case of Soul Jars`, `487 Divine Intervention` |
| The mastery ceilings each promotion changes | the donor's transcription of the executable (`OpenEnroth/src/Engine/mm7_data.cpp:763`, `skillMaxMasteryPerClass`), which the skills stone already shipped |
| The guild, its rung, its prices, its books | the operator's building table (`2DEvents.txt`) through the importer |
| **Authored by this game** | which requirement kind each errand is stated as (item / award / quest), the two counted deeds stated as records with a magnitude, the light/dark word for each alternative, the eight classes whose paths split on the two schools, the award each rank leaves (`promotion:<rank>`), and every giver's own line |
| **Staging, not imported** | where the party starts, and the four authored placements: the two promoters, the Light Guild counter, and one chest (see the table below) |

The row numbers, the topic owners, and the quest texts were read from the operator's install with a local
probe over the importer's own readers; the ladder cites each of them beside the row it states.

## Real-data counts

| Count | Value |
| --- | --- |
| Shipped class rows / families | 36 rows, 9 families, four per family in the shipped order base → first → light → dark |
| Ranks this game states | 27: 9 first promotions (rank 2) and 18 second-promotion alternatives (rank 3) |
| People named as givers | 18, one good and one evil per family |
| Ranks whose errand is stated as a quest (no owner judges it yet) | 17 |
| Ranks whose proof is an item the shipped table carries | 8 rows, 14 item requirements (`624`, `620`, `542` ×2, `647`, `639/641-645`, `487`, `601`, `602`) |
| Ranks whose proof is a counted record | 2: five arena victories (Champion), ten thousand gold of bounties (Bounty Hunter) |
| Classes whose pair splits on magic | 8 (Hero/Villain, Master Archer/Sniper, Priest of the Light/Priest of the Dark, Arch Mage/Lich) |
| Records a rank leaves on the party | 27, one per rank, `promotion:<rank>` |

## Staging: what is the operator's data and what this check added

| Thing | Where it came from |
| --- | --- |
| Harmondale, its entry points, Thomas Grey and Halfgild Wynac as people, the Light Guild row (kind, rung, keeper, multipliers, books), the 36 class rows, the items, the skills | the operator's install, through the importer (`mm7-tables`, `mm7-world`) |
| Where the party starts, and where the two people, the guild counter, and the chest stand | authored staging: one scenario pack (`rc8504-scenario`, named in the bundle) and four placements patched into `mm7-tables/places.json`, because the imported map puts the promoters thousands of units away and this box's browser cannot steer that far |
| What the chest holds | authored staging: the shipped item rows the two ranks ask for, plus three treasure-level references, because the guild's membership costs 2000 and a created party starts with 200 |
| The party | the product's own creation screen, accepted as its default party; Aelina is its elf sorcerer |

One defect was found and fixed while making this reachable: a session that creates its party composed its
conversation **before** that party existed, so the conversation could read none of it — an offer that asks
whether the party holds the class a rank promotes from was withheld with "this world holds nobody to give it
to". `PartyRpgSession` now composes the conversation over the party again when creation is accepted, and a
host test covers the created-party route.
