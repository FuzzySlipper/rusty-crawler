# The character book

Reading of rusty-crawler#9222, 2026-10-03 (America/Los_Angeles). Each crew-services browser session started its own
`rusty dev --live-debug` host, and every image is the Engine runtime's own frame with the DOM companion over it, at
the supported desktop viewport of 1280×720. Screenshots stay in `local/evidence-9212/`. The party is the default one,
accepted on the ordinary new game; its items were bought at a counter in play, not seeded.

## What a player sees

- `15-character-stats.png` — **I** opens the Character book on the selected member, Roderick. The four faces at the
  top choose whose page it is (through the party's own selection, so the adventure bar's outline follows). **Stats**
  shows the game's sheet in three boxes: the seven scores as the fight reads them, the vitals (hit and spell points,
  armour class, condition, age, level, experience, skill points) and the six resistances; beneath, growth (level 2 at
  1000 experience, gained by training at a hall) and the Knight's ranks with what each needs.
- `16-inventory.png` — **Inventory**: every slot of Roderick's figure, empty, beside the party's one pack, empty; the
  opening gives the default party nothing.
- `17-bought-in-pack.png` — after walking to Tor, talking, asking to see the wares and buying the Crude Longsword
  (75) and the Cutlass (60) at The Knight's Blade (200 → 65 gold), the pack holds both with their installed pictures.
- `18-inspect-sword.png` → `19-equipped.png` — picking the longsword inspects it (Sword; Damage 3d3; Uses the Sword
  skill; Worth 50 gold); **Equip (main hand or off hand)** put it in Roderick's main hand and the pack fell to one.
- `20-equip-refused.png` — on Aelina's page, equipping the Cutlass was refused by the equipment owner, shown at the top
  of the page: `Aelina has not learned Sword, which is what a Cutlass needs before it can be worn or wielded.`
- `21-saved.png` → `22-resumed-inventory.png` — after Save, a session resumed from the save slot shows Roderick still
  wearing the longsword, the Cutlass in the pack and 65 gold.
- `23-taken-off.png` — picking the worn sword and **Take off** returned it to the pack: `Roderick took Crude Longsword
  off 'main hand', and it is in the pack.`
- `24-skills.png` — **Skills**: each skill's rung and ceiling (basic 1, up to master 12), and with no points to spend
  each row says why there is no next point, in the product's words.

## What the product publishes for it

The equipment block's worn rows and its new `pack` carry each item's picture URL (the item table's icon, granted
through the Engine like the faces), the game's kind word and facts, its slots, its ordinary use and, for a thing an
errand needs, the quest owner's reason it cannot leave. A new `character` block carries each member's sheet as titled
label–value sections the ruleset reads from the fight's own sums. A scroll or wand picked in the pack is read or fired
through the spellbook's casting with the selected member as caster (exercised by the companion suite on the
product's fixture; the opening party carries none).

## Limits

- No looted, quest-bound, usable or spell-carrying item was reached live; those rows are exercised by the companion
  and ruleset suites on the product's own fixtures. No member was incapable in this run.
- Identification hides nothing yet (every item is named by its real name), so the inspector states no unidentified
  line. Attack, shoot and the quick spell are not on the stats page.
- Only the 1280×720 viewport was read live; the figure column scrolls inside the book at that size.
