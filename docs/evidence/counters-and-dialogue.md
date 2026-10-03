# Counters and dialogue

Reading of rusty-crawler#9224, 2026-10-03 (America/Los_Angeles), in a crew-services browser session over its own
`rusty dev --live-debug` host at 1280×720. Screenshots stay in `local/evidence-9212/`. The party is the default one,
accepted on the ordinary new game, walked by ordinary movement and spoken to with **G**.

## What a player sees

- `27-dialogue-tor.png` — **G** on Tor opens the dialogue: the speaker beside their words, the greeting, what was
  said, the offered topic (`Ask to see the wares`) and **Take your leave**. A keeper has no imported face yet, so Tor is
  drawn with his initial (#9292); a person from the people table is drawn with their own portrait.
- `28-counter-wares.png`, `29-counter-unaffordable.png` — the topic hands off to **The Knight's Blade · Tor**: Weapon
  Shop, open 06:00–18:00, the purse (200 gold) and a page per thing the counter does (Wares, Sell, Learn). The wares are
  tiles with the installed item pictures and prices; picking the Cruel Spear and **Buy for 675** is refused by the
  service mechanism before anything moves: `The price is 675 coin(s) and the party holds 200 coin(s) and 10 portions:
  475 coin(s) short.` Each thief's hand is offered beside Buy (`Nyx: steal it`).
- `30-counter-bought.png` — **Buy for 75** on the Crude Longsword: `The party buys 1 × Crude Longsword for 75
  coin(s), and 0 are left.`, the tile reads sold out, its Buy is no longer offered, and the purse reads 125 on the
  counter and on the bar.
- `31-counter-learn.png`, `33-counter-lesson-refused.png` — **Learn**: a card per lesson (skill and level, price) taught
  to the member picked; a 500-gold lesson is refused by name for the purse.
- `32-counter-sell.png` — **Sell**: the party's own items with the price the counter would pay (Crude Longsword,
  14 gold).
- `34-bought-sword-equipped.png` — leaving the counter and equipping the bought sword in the character book:
  `Roderick now wears Crude Longsword in 'main hand'.`
- `35-dialogue-lauren.png`, `36-counter-healing.png`, `37-counter-raised.png` — after dragonflies laid the party low, the
  Healer's Tent offers **Healing** and **Raise the dead**, each a choice per patient with its price and, where it does not
  apply, the mechanism's reason (`Borin suffers nothing Raise the dead treats, so there is nothing to heal.`); raising
  Roderick for 50 leaves the purse at 75 and puts him back on the bar.

The companion suite exercises the remaining pages on the product's own fixture: identify, repair, travel (a passage
priced for the party, paid through the fare command), notices, debts (repaid by what the product says the purse would
hand over), the bank's quoted amount, a refused cure with its reason, and a closed counter.

## What changed underneath

A person on the conversation block carries the portrait image the Engine granted; a counter's lots and the party's
items it would buy, identify or mend carry their pictures; and the service owner names the party's items in the
game's words, so a sale reads `Crude Longsword` rather than its definition id. Identifying and repairing an item are
quoted on its row before anything is settled, as a sale is.

## Limits

- Training and travel counters were not reached live (the trainer stood past water, the boat keeper was not among the
  placed people); their pages are proven on the fixture.
- NPC group news stays with #9150.
