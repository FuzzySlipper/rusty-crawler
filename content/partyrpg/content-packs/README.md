# Authored content packs

The loader's authored root (`ContentLayout` names it beside `imports/` and `bundles/`). The one committed
authored pack is `mm7-new-game`: the ordinary new game's `scenario-start`, which opens creation and puts the
created party at the first region's `Party Start`, the selected creation purse, a `service-stock` table for the
existing imported Tor service (`service` 1), and the opening `opening-island-guide` errand. The stock table is
an explicit adaptation for this bundle: it keeps basic melee, ranged, and wearable gear beside first books for
the seven base magic schools, including Fire Bolt, four Potion Bottles, and four Widowsweep Berries. The stock
counts and labels are authored, while item values remain those of the imported item table for the same buy and
sell quote. The ruleset applies it to the imported service definition, so the imported Tor placement remains the
only counter placement and no generated operator pack is changed. The selected purse makes a created custom
party able to buy four Bow/Leather Armor/Fire Bolt sets for 2,700 coin at Tor's 1.5 multiplier, study a
carried introductory book, and retain the four-coin bottle/reagent purchase; this is authored opening tuning
rather than a claim about original values.
That errand is given by the imported Ailyssa placement (`npc-4`), asks the party to speak with the imported Sally
placement (`npc-5`) and deliver a `Potion Bottle` to Ailyssa, and pays through the existing quest owners. The
imported Emerald Island signs, people, counter, and entrances supply the visible route.
Everything else the product plays is
imported from the operator's own data into [`../imports`](../imports), and a live check's hand-written scenario
is staged there too ([`../../../docs/live-checks.md`](../../../docs/live-checks.md)). A pack authored for the repository — a
tuning profile, a scenario, definitions that are ours rather than the original's — belongs here and is
committed.

Each pack is a directory with a `pack.json` manifest and the documents the manifest declares. A pack is one
of four kinds — definitions, tuning, scenario, or world — and the loader validates the whole catalog when the
product starts: pack ids must match their directories, entry ids must be unique across all packs of the same
definition kind, and every declared reference must resolve.

A new pack must be named by a bundle in [`../bundles`](../bundles), or nothing loads it.
