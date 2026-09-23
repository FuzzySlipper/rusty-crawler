# Might and Magic VII: For Blood and Honor — design-experience outline

Derived read-only from the shipped manual. Purpose: let a designer rebuild the *shape* of the game —
what the player does, in what order, gated by what — without the manual.

## Sources, citations, markers

| Short form | Source |
| --- | --- |
| `p.N` | `MANUAL.PDF`, **printed** page N (the manual's own numbering, matching its contents page). PDF page = N + 1. |
| `RefCard p.1` / `p.2` | `Reference_card.PDF`, the two pages of the quick-reference card. |
| `poster` | `MAP.PDF`, one-page world-map image; text extraction yields only place labels. |

Extraction: `pdftotext -layout`. `MANUAL.PDF` = 60 pages (printed 2–59); `Reference_card.PDF` = 2 pages;
`MAP.PDF` = 1 page, image-only apart from 18 place labels.
**[inferred]** = the manual implies but never states it. **[not in manual]** = absent (checked by keyword sweep).

---

## 1. Character creation

- Party is **four characters**, fixed. *New* on the startup menu opens Create Party with a **default party already built**; accept it or customize each character one at a time. (p.10, p.11)
- **Races: 4 — human, elf, goblin, dwarf.** The *portrait* choice sets gender and race; voice is separate and cosmetic. Changing the portrait re-derives race statistics and skills and **cancels prior stat edits**. (p.12; RefCard p.1)
- **Attributes: 7** — Might, Intellect, Personality, Endurance, Accuracy, Speed, Luck. Effects: Might → melee damage; Intellect → spell points for elemental casters (Sorcerer, Archer, Ranger, Thief, Druid); Personality → spell points for "magic of the self" casters (Cleric, Paladin, Ranger, Monk, Druid); Endurance → hit points; Accuracy → chance to hit with melee and ranged; Speed → actions per unit time and recovery rate; Luck → magic resistance and reduced trap damage. (pp.12–13)
- **Attributes are assigned, not rolled.** Race supplies min/start/max per attribute; the player distributes a **pool of 50 bonus points** with +/- controls and must spend all of it before starting. Some attributes cost **2 points per +1**, others give **+2 per point spent**; a value may be lowered **2 below its start** to refund points; maxima run **15–30** by race and attribute. (p.12, p.17; RefCard p.1)
- Anchors from the race table: Human 9/11/25 for Might, Intellect, Personality, Accuracy and Speed; Elf Intellect 12/14/30; Goblin Might 12/14/30 and Intellect 5/7/15; Dwarf Endurance 12/14/30 and Accuracy 5/7/15. (p.17)
- **Race starting resistances:** Human +5 Spirit and Body; Elf +10 Mind; Goblin +5 Fire and Air; Dwarf +5 Earth and Water. (p.17)
- **Classes: 9 base classes** — Knight, Thief, Monk, Paladin, Archer, Ranger, Cleric, Druid, Sorcerer. Each has **three ranks**: base → first promotion → **one of two alternative second promotions**. (pp.14–16)

  | Base | 1st promotion | 2nd promotion (pair) | Base | 1st promotion | 2nd promotion (pair) |
  | --- | --- | --- | --- | --- | --- |
  | Knight | Cavalier | Champion / Black Knight | Archer | Warrior Mage | Master Archer / Sniper |
  | Thief | Rogue | Spy / Assassin | Ranger | Hunter | Ranger Lord / Bounty Hunter |
  | Monk | Initiate | Master / Ninja | Cleric | Priest | Priest of the Light / Priest of the Dark |
  | Paladin | Crusader | Hero / Villain | Druid | Great Druid | Arch Druid / Warlock |
  |  |  |  | Sorcerer | Wizard | Archmage / Lich |

  The tables give **36 rank rows** (9 classes × 4). The manual never calls the two branches "light" and "dark" **[inferred]** from the Light/Dark Magic columns and from names such as Priest of the Light and Lich. (pp.14–16)
- **Class fixes starting and per-level HP/SP, per rank.** Examples, starting HP/SP then HP per level for base/1st/2nd promotion: Knight 40/0, 5/7/9; Paladin 30/5, 4/5/6; Cleric 25/10, 2/3/4; Sorcerer 20/15, 2/3/3; Monk 35/0, 5/6/8 with SP 0/1/1. (p.17)
- **Starting skills: exactly 4 per character** — **2 fixed by class**, **2 chosen by the player** from the class-legal list; all four must be assigned to start. (p.12; RefCard p.1)
- Skill ceilings are class- and rank-specific, encoded **B / E / M / GM** (Basic, Expert, Master, Grand Master). (p.13)
- **Starting equipment is not itemized in the manual** [not in manual]. The card says the party begins on the **dock of Emerald Isle** with "a small amount of gold and a few items", must **equip manually**, magic users must **study their initial spells**, and a **docent** NPC is present. (RefCard p.1)

## 2. Skills

- **34 skills** in four blocks (the shipped table carries 37 rows — see the data inventory): **8 weapon, 5 armor, 9 magic, 12 miscellaneous**. (pp.38–41; ceilings pp.14–16) Weapon: Sword, Axe, Staff, Spear, Dagger, Bow, Mace, Unarmed. Armor: Leather, Chain, Plate, Shield, Dodging. Magic: Fire, Water, Air, Earth, Spirit, Mind, Body, Dark, Light. Misc: Alchemy, Armsmaster, Body Building, Disarm Trap, Identify Item, Identify Monster, Learning, Meditation, Merchant, Perception, Repair Item, Stealing.
- **How skills are learned:** 4 at creation; **basic level bought from shopkeepers** and taught at **Magic Guilds** for a fee (shopkeepers refuse skills the class cannot learn); **expertise tiers are raised only by "master teachers" found while exploring**, and by gaining class rank. (p.31, p.32, p.37, p.13)
- **Skill levels:** each skill has a numeric level starting at **1**; advancing to level N costs **N skill points** (Bow 3 → 4 costs 4). (p.37)
- **Skill points:** **5 per level at levels 1–9**, then **+1 for every further five levels** (6 at 10–19, 7 at 20–29); spent on the Skills screen by clicking a skill. (p.36, p.21)
- **Mastery tiers and what each unlocks** — every skill has its own ladder; representative rules (pp.38–41):
  - *Attack/damage scaling:* skill level adds to attack bonus at Normal; higher tiers add damage or multiply effects (Unarmed doubles bonus and damage at Master and gains a % chance to avoid attacks at GM; Axe GM can halve a target's armor class; Dagger Master has a % chance of triple damage).
  - *Recovery time:* many Expert tiers reduce recovery (Sword, Axe, Bow); Armsmaster reduces attack recovery even at Normal, adds attack at Expert and damage at Master, and **GM doubles all its bonuses**.
  - *Dual wielding:* Dagger (Expert) and Sword (Master) permit a second weapon in the right hand.
  - *Bow:* Master fires two shots per attack; GM adds skill to damage.
  - *Armor:* skill adds to armor class and weight slows the wearer. Leather Expert ignores weight on recovery, GM adds skill to elemental resistances. Chain GM takes 2/3 physical damage; Master removes the weight penalty. Plate Master takes half physical damage; GM has no recovery penalty. Shield GM grants the Shield spell's effect, Master adds twice skill to AC. Dodging applies only unarmored; GM extends it to leather.
  - *Utility caps:* Disarm Trap, Identify Item, Repair Item and Perception reach 100% at GM; Merchant GM buys and sells at true value; Stealing chance ×2/×3/×5 by tier; **Learning grants 9% + 1% per skill level**, ×2/×3/×5; Meditation adds 1 maximum spell point per skill level, ×2/×3/×5; Body Building adds skill level to maximum HP, doubled/tripled/quintupled.
- **Skills gate equipment:** a character cannot equip a weapon or armor without the matching skill, but **belts, boots, capes, helmets and gauntlets require no skill**. (p.39)

## 3. Magic

- **9 schools**, each with its own skill: **Fire, Air, Water, Earth, Spirit, Mind, Body, Light, Dark**. (p.39)
- **99 spells — exactly 11 per school**, tiered **4 Normal / 3 Expert / 3 Master / 1 Grand Master** (Light deviates: 4 / 2 / 4 / 1). Each school has exactly one Grand Master spell. (pp.44–58)
- **Learning:** spells come from **learning books**, bought at **Magic Guilds and magic shops** or found as treasure. Each book is used once, by clicking it onto the character, and the spell is then permanently in that character's spellbook. (p.42)
- **Mastery gating:** a spell's tier may only be learned and cast once the character holds that school's skill at the matching expertise; higher school ranks also strengthen lower-tier spells. (p.44)
- Also castable without the skill: **scrolls** (one spell, one use) and **wands** (multiple charges, equipped as a weapon, one charge per attack, gone when empty). (p.43)
- **Spell points:** the pool derives from class and Intellect/Personality; costs run **1 to 60** (Souldrinker 60, Divine Intervention 55; entry-tier spells cost 1–4). (p.44; entries pp.44–58)
- **Casting UI:** select the character (portrait or key 1–4) → open the spellbook (Cast Spell button or C) → double-click the icon. Each character keeps **one QuickSpell**, set with SetSpell and fired with S; right-click an icon for its description. (p.43)
- **Potions:** bought, found, or mixed by right-clicking a reagent onto an empty bottle. **Alchemy tiers** gate it — Normal basic→complex, Expert →compound, Master →white, GM →black. Mixing incompatible potions **explodes**. (p.43)

## 4. Combat

- Combat uses the same Adventure Screen as exploration; there is no separate battle scene. (p.33)
- **Two modes, one session.** Real time is the default: every action consumes time, shown by the character's **ready light darkening during recovery**, and a recovering character can start no new action. (pp.19, 34)
- **Switching: Enter toggles** real-time and turn-based at any moment. (p.33; RefCard p.2)
- **Turn-based:** combatants act **one at a time, ordered by speed and recovery status**, in **rounds of a few seconds**; fast characters and creatures can act **multiple times per round**; the game pauses before each character's turn and **B skips** a turn. At the end of a round the party gets a **movement phase** (arrow keys, a short distance), then creatures move, then the next round; any key skips the movement phase. A corner icon shows **Action Phase / Movement Phase / Computer Thinking**. (p.34)
- **Attacks:** click a creature, or hover and press **A**; **A** with no target auto-targets the nearest. **Bows and wands** strike at range, and **bows cannot be used while creatures are directly in front of the party**. **S** performs, in order, quick spell → bow/wand → hand-to-hand; **C** opens the spellbook. (p.33)
- **Recovery/recharge timing** is one quantity: driven by Speed plus skills (Armsmaster, weapon and armor expertise), penalized by armor weight, and used both to pace real time and to order turn-based initiative. (pp.13, 34, 38–40)
- **Monster behavior and aggro:** hostility is binary and telegraphed — **hostile creatures attack on sight** (p.29), and the **ready light doubles as an aggro meter**: green = none near, yellow = hostiles near, red = the party is under attack. (p.19)
- **Monster knowledge:** right-click a creature; **Identify Monster** adds exact HP (Normal), attack type and damage (Expert), spells (Master), resistances (GM); the **Detect Life** spell shows exact HP. (p.40, p.50)
- **Turning monsters aside:** Turn Undead and Mass Fear make them **flee**; Charm removes hostility; Berserk, Enslave, Control Undead and Reanimate turn them on each other. Invisibility and Town Portal **cannot be cast while hostiles are near** — Town Portal works near hostiles only at GM. (pp.46, 47, 52, 53, 57, 58)
- **Fleeing:** the manual defines no flee or retreat mechanic [not in manual]; the card advises running if the fight looks lost. (RefCard p.2)
- **Death and resurrection:** HP below zero → **unconscious**, out of action until HP is positive; HP far below zero → **death**, requiring **Raise Dead** or a **temple**. **Eradication** destroys the body and needs **Resurrection** or a temple. Reanimate on a corpse instead produces a **Zombie**, which does not sleep, gains nothing from resting, and is cured only at a temple. (p.35)
- **Conditions** (all removable at temples for a price): physical wounds; unconsciousness; death; **weakness** (fatigue or hunger, lowers maximum HP and effectiveness); **poison** and **disease** (lower attributes, combat efficiency and spell points over time); **insanity**; **afraid**; **cursed** (actions fail 50%); **asleep** (wakes when attacked, after rest, or via Awaken); **stoned**; **paralyzed**; **zombie**; **eradicated**. (p.35)
- Resting can be **interrupted by attacking creatures**. (p.24; RefCard p.2)

## 5. Towns and services — every type the manual names

| Service | What it does for the player |
| --- | --- |
| Blacksmith | Weapon shop: buy, sell, identify, repair; teaches weapon skills. (p.31) |
| Armory | Armor, shields, headpieces, gauntlets. (p.31) |
| Magic shop | Potions, ingredients, scrolls, rings, learning books, magic items. (p.31) |
| Alchemist | A magic shop specializing in potions and reagents. (p.31) |
| Magic Guild | Learn basic magic skills and buy learning books; **entry needs purchased membership**. (p.32) |
| Tavern / roadhouse | Buy food, or rent a room for a night of safe rest. (p.32) |
| Temple | Healing for donations and fees; removes every ill effect, including zombification and eradication. (p.32, p.35) |
| Stable | Overland **stage coach** routes that shorten long trips. (p.32) |
| Dock | Hire passage on a ship sailing to its next port. (p.32) |
| Bank | Safe keeping for gold. (p.32) |
| Government: town hall or castle | The local authority, and a usual source of tasks. (p.32) |
| Houses | Knocking on doors yields odd jobs, guild members and master teachers. (p.32) |
| Training hall | Pay a fee to convert earned experience into a level; most halls cap how far they train. (p.36) |

- Shops generally: **standard + special stock lists**, each shop specializing in one category, and they sell, identify, repair and teach basic skills. Sub-services appear when the player opens **Display Inventory** — **Sell**, **Identify** (unidentified items highlighted **green**, priced on mouse-over), **Repair** (broken items highlighted **red**). (pp.30–31)
- Shops keep **business hours**; doors are **locked at off times**. (p.28)
- **Stealing** from shop displays or from people is a Ctrl-click action available only with the Stealing skill, with unstated penalties on failure. (pp.28, 31)
- "Inn" is the reference card's word for the tavern room (RefCard p.2); the manual's service list has only **tavern/roadhouse** [not in manual as a separate building].

## 6. Travel and the world

- Movement is first-person: arrows walk and turn, **Shift+arrows run**, **Ctrl+arrows side-step**, **X jumps**. Falls deal heavy damage; gentler slopes can be descended; **running jumps clear small ledges and pits**. (p.26)
- **Outdoor structure:** each outdoor map is a **square section of Erathia**. Reaching a map edge lets the party travel to the **adjacent map**, which **takes several days** and consumes **1 food unit per day**; arriving with too little food means the party arrives **weakened**. (p.26)
- **Overland transport:** hired **boats** (docks) and **stage coaches** (stables). (p.26, p.32)
- **Entering places:** dungeon, cave and fortress entrances are clicked and confirmed; interior doors may need a **key, a switch, or an action**; **secret doors** are found with **Perception** and then blink red; town doors start a conversation with the occupant. (p.28)
- **Magical travel:** **Town Portal** (Water, Master) → the central fountain of any town already visited, success **10% per skill point**; **Lloyd's Beacon** (Water, GM) → up to **5 beacons**, each lasting **1 week per skill point**; **Fly** (Air, Master, PageUp/Insert/Home for ascent and descent, with a periodic spell-point drain); **Water Walk** (Water, Expert, also draining); **Jump** (Air, Expert, 60 feet); **Telekinesis** (Earth, Master) opens doors and chests at a distance. (pp.26, 46–49)
- **Automap:** drawn as territory comes into view, showing position and facing, with zoom (**+/-**) and a full **Maps** book that scrolls and zooms. **Wizard Eye** reveals creatures at Normal, treasure at Expert, other points of interest at Master. (p.18, p.45, p.22)
- **World map poster** labels 18 places (read from label positions; only **Erathia** and **Deyja** are named in the manual text): Spaward, Avlee, Tularean Forest, Emerald Isle, Pierpont, Moulder, Deyja, Steadwick, Land of the Giants, Tatalia, Erathia, Harmondale, Tidewater, Barrow Downs, Spyre, Nighon, Bracada, Evenmorn Island. Which labels are regions versus towns is **[inferred]** from the poster. Relative placement: Spaward, Avlee and Tularean Forest north; Emerald Isle northeast; Moulder, Pierpont, Deyja and Land of the Giants center; Steadwick, Erathia, Tatalia and Tidewater west; Harmondale and Barrow Downs center-south; Spyre, Bracada and Evenmorn Island south; Nighon southeast. (poster)
- **Obelisks** "dot the landscape" and the party records clues about them automatically. (p.22)
- **Fountains** are drunk from directly, and their effects are recorded automatically. (p.27, p.22)

## 7. Time

- The **Calendar** book displays the current time and date. (p.22)
- Journal dates use **day + month name + year**: "11 June 1165", "23 October 1166", "5 August 1167". (pp.4–9) **Weeks** exist as a unit ("1 week per point", p.48); **hours** and **minutes** drive spell durations throughout (pp.44–58). The full month list, days per month and week structure are **[not in manual]**.
- **Day/night:** light level varies — Torch Light's effect "is only visible when it is dark". (p.44)
- **Sleep:** the party should **sleep eight hours, once a day**; without it they become **weak from fatigue** after a day or so. Sleep restores lost hit and spell points. (p.24)
- **Rest menu: Rest & Heal 8 Hours** (restores HP/SP, consumes food) plus **wait** options that pass time **without healing** — the card names **Wait until dawn (to 5:00 AM)**, **Wait 1 hour**, **Wait 5 minutes**. (p.24; RefCard p.2)
- **Camping outdoors** consumes **1 food unit on grass** and **more on harsher terrain**; the party **refuses to camp with hostile creatures nearby**; creatures may attack a sleeping party. (p.24)
- **Renting a room** at a tavern is the safe alternative, but the manual gives **no price or duration** for it [not in manual]. (p.32; RefCard p.2)
- **Shop hours** exist as a rule but no specific hours are printed [not in manual]. (p.28)
- Spell durations are the real clock — buffs last *per point of casting skill* in minutes, hours or days — and some spells are limited per day (**Armageddon 3/day, 4 at GM; Divine Intervention 3/day**). (pp.44–58)
- **Aging** exists as a character effect: Divine Intervention **ages the caster ten years**. (p.56)

## 8. Progression and the meta-game

- **Experience** comes from killing monsters and completing quests. (p.36)
- **Level cost: current level × 1000 experience** to advance — 3000 XP to go from level 3 to 4. (p.36)
- **Training** happens at a **training hall** in a town for a fee, and most halls cap the levels they can grant. (p.36)
- **On level-up** maximum HP and SP rise by class, rank and attributes, and the character receives **5 skill points** (levels 1–9, rising by 1 every five levels). (p.36)
- **Promotions:** special **promotion quests** given by specific people empowered to grant rank for a given class. A new rank raises HP/SP per level and **raises skill ceilings and unlocks new skills**. (p.36, p.13)
- **Light/dark split:** the second promotion offers **two alternative ranks per class**, and the pairs differ in Light versus Dark Magic access — **Archmage** Light GM against **Lich** Dark GM; **Priest of the Light** / **Priest of the Dark**; **Hero** Light, **Villain** Dark; **Master Archer** Light, **Sniper** Dark. **Where and how the player makes that choice is [not in manual].** (pp.14–16)
- **Reputation** exists as a party value — the dark **Sacrifice** spell "greatly reduce[s] the party's reputation" — and **fame and reputation affect how NPCs treat the party** and whether they talk or join. Thresholds, display and decay are **[not in manual]**. (p.58; RefCard p.1)
- **Awards:** the character sheet has an **Awards** screen, but the manual never says what appears there [not in manual]. (p.21)
- **Quests and journal:** quests arrive in conversation and usually end by returning to the giver for money and/or experience, though not always; unfinished quests sit in **Current Quests** until completed; **quest items** exist and cannot be enchanted. (p.29, p.22, p.49)
- **Hirelings / NPCs:** up to **two hired followers** at a time, typically for an **up-front fee plus a small percentage of all gold found**; they appear as portraits and are dismissed through conversation. **Story characters may join without counting against that limit, and there is no limit on those.** (p.29, p.19)

## 9. Items and inventory

- Each character has a **loose inventory** and an **equipped figure**. Items are picked up by clicking (they stick to the cursor), dropped by clicking again, and inspected by right-clicking; identified items show detail. (p.23)
- **Equipping:** drop an item on the character figure. Items require the matching skill, and **only one of each item type may be worn at a time except rings**; an **Accessory Detail** toggle reveals equipped **rings, gauntlets and amulet**. (p.23)
- Slots the manual names: **weapon** (two-handed, or two one-handed once Dagger Expert or Sword Master permits an off-hand blade), **shield** (left hand, incompatible with two-handed weapons), **armor** (leather, chain, plate), plus **belts, boots, capes, helmets, gauntlets** (no skill needed), **rings** and an **amulet**. Exact grid dimensions and slot counts are **[not in manual]**. (pp.23, 38, 39)
- **Transferring:** drop an item on another character's portrait to give it; press Esc and click the main view to drop it on the ground. **Using** scrolls, learning books and potions means right-clicking them on a portrait or left-clicking them on the character figure. (p.23)
- **Identifying:** shopkeepers charge a fee (unidentified items show **green**); a character with **Identify Item** attempts it by right-clicking (GM = 100%). **Repairing:** shopkeepers charge a fee (broken items show **red**); **Repair Item** lets a character attempt it (GM = 100%). How items become broken is [not in manual]. (p.31, pp.40–41)
- **Enchanting:** **Enchant Item** (Water, Master) enchants a normal item at **10% per skill point**, stronger at GM, **never on quest items**; **Fire Aura** grants "of Fire/Flame/Infernos" weapon abilities; **Vampiric Weapon** drains life; **Recharge Item** (Water, Expert) refills charges while permanently reducing the maximum. (pp.44, 47, 57)
- **Artifacts and relics:** the welcome text promises "enchanted artifacts" as late-game acquisitions, but the manual defines **no artifact or relic item class, no list and no rules** [not in manual]. (p.3)
- **Gold:** one party purse shown on the adventure screen, spent on goods, training, temples, guild membership, followers and travel; **no starting amount or prices are given**; a **bank** stores it safely. (p.20, p.32)
- **Food:** a separate party resource beside gold, consumed by overland travel and camping; hunger causes weakness. (p.20, p.26, p.24, p.35)
- **Containers and corpses:** chests and drawers may be **trapped** (Disarm Trap, with Perception reducing the damage of a triggered trap); **bodies are searched** for gold and items and then disappear. (p.27)

## 10. Interface surfaces

- **Adventure Screen** carries the whole game: main first-person view; four **character portraits** with an HP bar (left), SP bar (right) and a **ready light** that doubles as an aggro indicator; **follower portraits** (arrows page past two); **active spell icons** for party-wide and per-character effects; **Torch Light** and **Wizard Eye** icons at the automap corners; the **books** row; **food and gold** readouts; and **Cast Spell / Rest / Quick Reference / Game Options** buttons. (pp.18–20)
- **Automap** sits in the corner of the main view, zooms with **+/-**, and shows position and facing. (p.18, p.59)
- **Character screens:** four tabs — **Stats, Skills, Inventory, Awards** — opened by double-clicking a portrait; statistics use a "current / normal" pair; right-click any statistic or skill for an explanation. (p.21)
- **Book screens:** five tabbed books — **Current Quests, Auto Notes, Maps, Calendar, History** — with page tabs. Auto Notes holds potion discoveries, fountain effects, obelisk clues and miscellaneous events; History is a chronological journal of the party's travels. (p.22)
- **Spellbook:** one page tab per magic school the character knows; double-click to cast, right-click for the description, **SetSpell** to assign the QuickSpell. (pp.42–43)
- **Rest & Camp screen** (p.24) and **Game Options** with New Game, Save Game, Load Game, Quit, and controls for graphics detail, **turn rate (16x … smooth)**, walksound, always run, flip on exit, show damage and three volume sliders (pp.24–25).
- **Startup menu:** New, Load, Credits, Exit; the Load dialog lists saves with a **thumbnail captured when the game was saved** plus a time and date stamp. (p.10)
- **Save/load:** named slots, chosen by clicking a slot and typing a name; loading shows the same thumbnail and stamp (p.25). The card adds an **autosave written each time the party exits a level**, listed first (RefCard p.1). Slot count is [not in manual].
- **Quick Reference** summarizes character and party information (also on **Z**). (p.20; RefCard p.2)
- **Input:** a full default keyboard map (p.59) — movement, flying, combat (A/S/B/C/Enter), Q/N/M/T/H for the five books, R to rest, 1–4 to select characters, spacebar to search or activate, Esc for options; the card adds **U** (always run), **Y** (yell, moving friendly creatures aside) and mouse shortcuts (RefCard p.2).
- **Arcomage**, the tavern card game, is **not mentioned anywhere in the manual or the reference card** [not in manual]. (keyword sweep)
- An **Erathian alphabet** is printed as an appendix (p.59) — a substitution cipher used by in-world signage and notes **[inferred]**. A separate booklet, *The Adventurers' Guide to Role Playing*, covers general RPG concepts and is not part of this manual. (p.3)

## 11. The broad main-quest arc, as far as the manual states it

- The manual does **not** narrate the main quest. It frames it: the player guides **four adventurers in Erathia** who begin with only basic skills and equipment and end as "true adventurers", accumulating abilities, artifacts, weapons and knowledge. (p.3)
- The only narrative supplied is **Archibald Ironfist's journal**, dated **11 June 1165 → 5 August 1167**: he escapes a decade of petrification, takes leadership of the **Necromancer Guild** in a Challenge of Dominance, sails to **Deyja** in Erathia, replaces the lich Gryphonheart, and closes by vowing to plant his "seed of discontent" in "those fertile disputed lands" against his brother and Catherine. (pp.4–9)
- The arc the tables and spell list imply: explore and level → complete **first promotion** quests for every class → complete **second promotion** quests, which split each class into a **light or dark** variant with exclusive Grand Master access to one of Light or Dark Magic (§1, §8) → late game with Town Portal, Lloyd's Beacon and Fly for movement and party-wide buffs, plus Armageddon, Souldrinker and Divine Intervention for large-scale conflict (pp.14–16, 36, 46–58).
- **Explicitly absent from the manual:** Harmondale, the Arbiter role, the succession or faction dispute, the scene where light or dark allegiance is chosen, any act structure, and any quest content beyond the statement that quests exist and are recorded in **Current Quests**. All of it is delivered in play, not in print [not in manual]. (pp.4–9, 22, 29)
