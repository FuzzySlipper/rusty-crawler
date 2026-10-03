# Selected fixed item powers and Genie Lamp

Approximate repertoire: working equipped Puck (500) adds Speed 40; Iron Feather (501) Might 40; Splitter (506) Fire resistance 50; Twilight (525) Speed/Luck 50 and Fire/Air/Water/Earth/Mind/Body resistance -15; Elfbane (531) halves hostile missile damage. Shielding and spell Shield share one non-cumulative division. Broken or unequipped items contribute nothing immediately. Unknown fixed powers, including other powers of Elfbane, are named uncompiled. This is not an exhaustive artifact/relic emulation.

The identities, kinds, material and names were checked against the operator's normalized item table. Behaviour references are `OpenEnroth/src/Engine/Objects/Item.cpp:450-574` (`PopulateArtifactBonusMap`), `ItemEnums.h:655-692`, and `Character.cpp:5970-6010` for non-cumulative missile shielding. Values above are selected donor terms; the selection is ours. Actual combat attribute, resistance, special-attack saving and damage-plan readers consume them, rather than a copied modifier store. The figure names powers and each member's actual worn contributions.

Genie Lamp (616) has an ordinary inventory action targeting a real member and instance. It consumes that instance through party custody, respecting quest retention, and adds the calendar's week-of-month (1–4 in this game's calendar) permanently to one keyed choice of Fire/Air/Water/Earth/Mind/Body resistance. The gift uses `CharacterResistances`, current-save member state, canonical resistance readers and the figure's permanent-gift row. Recovery from a laid-out condition and repair of a broken lamp are required. A consumed identity refuses a repeat. Keyed chance uses the Engine random service and durable item identity.

This is an explicit adaptation of `OpenEnroth/src/Engine/Objects/Character.cpp:3425-3536`: the donor changes gifts by month and grants a random six-kind resistance in December, with dangerous calendar-day curses. Here the resistance branch applies in every month, with no curse; other calendar gifts are not claimed. No invented spell definition is used to represent an item action, and no extra clock, effect ledger or inventory is introduced.

Composed checks use ordinary equipment and item payloads, actual combat readers, break/unequip changes, current-save identity/gift restoration, repeat and laid-out refusal. Creation/scenario parity probes the composed item-use owner. C# projection fixtures and the DOM suite bind usable item identities, member selection, result sentences, disappearance after consumption and permanent-gift rows. The complete verification gate passed, including importer determinism and CoreCLR staging. Three source lanes approved the implementation; a narrow comments/documentation correction was separately approved. NativeAOT was not run.


## Bounded ordinary live reading

On imported Harmondale terrain, a parent-staged healthy scenario carried the six actual imported item definitions. Ordinary Equipment selection, Equip and Take off showed Puck Speed +40, Iron Feather Might +40, Splitter Fire resistance +50, Twilight Speed/Luck +50 with the six resistance penalties, and Elfbane hostile-missile shielding. Removing each item removed its worn contribution and returned the same instance to the shared pack. Elfbane explicitly named its other fixed powers uncompiled.

Selecting Aelina and the ordinary Genie Lamp Use button consumed the lamp. The product sentence was: “Aelina uses the Genie Lamp: permanent Water resistance +1, now 1; the lamp is consumed.” The final panel showed that permanent gift and no usable lamp row. Four members remained healthy and ready.

The observer's session was `83010ab3-957c-4cd8-94e4-514ccf74221a`; indexed original captures, sidecars, action receipts and cleanup are Den evidence 37778, with launch-fingerprint correction 37779. The parent inspected the original final panel. The owned browser and host stopped cleanly, the lease released, and all ten slots were free.

This live reading covers ordinary equipment and item use. Hostile damage and current-save restoration are covered by composed semantic checks; no live damage, Save/resume, broad traversal, rendered world or NativeAOT claim is made. The operator's game data remains uncommitted.
