# Spell effect coverage

Every spell this game states numbers for, and how far this build expresses its effect. The file is
generated from the ruleset's own table by `MagicCoverageTests` and checked by that test on every
run, so it cannot drift from the code: change a row's category, its rung, what it is aimed at, or
how far it is expressed, and this document has to be regenerated with
`CRAWLER_WRITE_MAGIC_COVERAGE=1 dotnet test tests/PartyRpg.Rulesets.MightAndMagic7.Tests`.

A row is identified by content's own spell id, which is also the id the ruleset's table is keyed by and
the id this document's rows carry. The shipped name for each row stands beside the same id, as a
comment, in `src/PartyRpg.Rulesets.MightAndMagic7/MightAndMagic7Spells.cs`.

## How a state is read

| state | what it means |
| --- | --- |
| implemented | the cast changes state through the owner that holds it, and this build reads that state where it applies |
| approximated | the cast changes real state through that owner, more coarsely than the game does; the difference is named |
| not yet | nothing this build reads changes, and the owner that would close the gap is named |

## What a duration does across a save

A duration is a deadline registered with the session's one clock, and the effect it ends is carried
state: an effect a spell aimed at one character is written under that character's own entry, and a
spell aimed at the party is carried by the party. Neither the deadline nor the effect's end moment
is a number the effect carries, which is what makes the save boundary's own answer the honest one:

| what | across a save today |
| --- | --- |
| an item's spent charges | carried: a charge is item state, written with the instance's damage and enchantments, so a half-spent wand resumes half spent |
| an effect's existence and magnitude | not carried: an effect's end is a moment the save cannot carry yet, so a save taken while a ward, light, or haste runs is refused naming the effect |
| when an effect ends | not carried, for the same reason: the save records elapsed game time and no deadlines |

The refusal is by name rather than silent — `PartyRpg.Kit.Persistence.ClockSave.Capture` throws a
`SessionSaveException` listing the deadlines the clock holds — so a save taken while magic runs is
refused where a player can read it instead of dropping the schedule. Carrying deadlines (which
owner registered one, when it is due, and how it repeats) is Den task #8617's own requirement, and
it names the spell-effect deadlines among the owners that task must carry.

## Casting from an item

A scroll and a wand carry one spell each, read from the shipped item table's own reference column,
and both are cast through the session's one casting workflow with the item as the spell's source:
no school skill and no spell point is asked for, and the item is what pays.

| what is used | how |
| --- | --- |
| a scroll | the one spell it carries, once, and the scroll is used up; the donor's own scroll cast carries no mana cost at all (OpenEnroth `src/Engine/Spells/CastSpellInfo.cpp:207`, the `overrideSkillValue` branch that sets `uRequiredMana = 0`) |
| a wand | the spell it carries, fired as the weapon it is wielded as, one charge spent per use, and the item leaves the party through the inventory when its last charge goes; the donor fires it at a fixed eighth level of novice mastery (OpenEnroth `src/Engine/Spells/CastSpellInfo.h:61`, `WANDS_SKILL_VALUE`) |
| a potion | the effect this game states for its own row, once, and the potion is used up; what it is read at is the potion's own strength rather than any character's school level (OpenEnroth `src/Engine/Objects/Character.cpp:3081-3085`, `potionStrength`), which is what makes a potion the way a character with no school at all gets a spell's effect |

What this build does not take from the donor is the fixed skill reading of a scroll cast: the donor
casts one at the fifth level of master mastery (OpenEnroth `src/Engine/Spells/CastSpellInfo.h:60`,
`SCROLL_OR_NPC_SPELL_SKILL_VALUE`), while this build casts it at the caster's own school, because a
damaging spell's numbers are resolved by the fight's own ability answer and that answer is asked
with the caster rather than with the item that carried the spell (receiver: the fight's ability
resolution, which would have to be handed the casting's own skill reading). A wand's own value *is*
taken, because a wand is the weapon the fight resolves the attack with.

## Potions

A potion is an item that carries one effect, and drinking it is a casting whose source is the item:
the same workflow that reads a scroll, the same effect path that applies a spell, and a per-character
deadline on the one clock wherever the donor's potion lasts. What is different is where the strength
comes from — a potion's own, not a caster's school — and that this game authors the effect's numbers
from the donor's drinking switch (OpenEnroth `src/Engine/Objects/Character.cpp:3080-3300`), because
the shipped `POTION.TXT` states what each potion is for in words and no numbers at all.

| potion | category | aim | state | what it does, or what is missing | receiver |
| --- | --- | --- | --- | --- | --- |

| 221 | condition | caster | implemented | a condition left on the character drinking it through their own condition state (Poison Weak) |  |
| 222 | healing | caster | implemented | hit points restored through the member's own pool, at the potion's own strength |  |
| 223 | utility | caster | implemented | spell points given back through the member's own pool, at the potion's own strength |  |
| 224 | condition | caster | implemented | the conditions the potion names lifted from the drinker's own condition state |  |
| 225 | condition | caster | implemented | the conditions the potion names lifted from the drinker's own condition state |  |
| 226 | condition | caster | implemented | the conditions the potion names lifted from the drinker's own condition state |  |
| 227 | condition | caster | implemented | the conditions the potion names lifted from the drinker's own condition state |  |
| 228 | utility | caster | implemented | a carried effect of its own identity (spell.haste) on that character, read where it applies and ended by a deadline on the one clock |  |
| 229 | utility | caster | implemented | a carried effect of its own identity (spell.heroism) on that character, read where it applies and ended by a deadline on the one clock |  |
| 230 | utility | caster | implemented | a carried effect of its own identity (spell.bless) on that character, read where it applies and ended by a deadline on the one clock |  |
| 231 | utility | caster | not yet | the party's gear protected from harm | item state, which carries what a spell would protect |
| 232 | resistance | caster | implemented | a shield on the character drinking it, read by the fight's own ranged resolution — a creature's missile does half to them — with a deadline on the one clock |  |
| 233 | utility | none | not yet | an item whose charges are given back | an item-aim owner: the pack holds the party's items and nothing aims a potion at one |
| 234 | resistance | caster | implemented | armour class carried by the character and read by the fight's own armour class, with a deadline on the one clock |  |
| 235 | utility | caster | not yet | water breathed under rather than walked over | the party's mover, which walks and falls and does nothing else |
| 236 | utility | none | not yet | an item made harder to break | an item-aim owner: the pack holds the party's items and nothing aims a potion at one |
| 237 | condition | caster | implemented | the conditions the potion names lifted from the drinker's own condition state |  |
| 238 | condition | caster | implemented | the conditions the potion names lifted from the drinker's own condition state |  |
| 239 | condition | caster | implemented | the conditions the potion names lifted from the drinker's own condition state |  |
| 240 | utility | caster | implemented | Might raised on the character drinking it, read wherever a fight reads the score, with a deadline on the one clock |  |
| 241 | utility | caster | approximated | Intellect is raised wherever a fight reads it; the pool a raised intellect would deepen is set by progression and is not re-read while it runs (ours) |  |
| 242 | utility | caster | approximated | Personality is raised wherever a fight reads it; the pool a raised personality would deepen is set by progression and is not re-read while it runs (ours) |  |
| 243 | utility | caster | approximated | Endurance is raised wherever a fight reads it; the pool a raised endurance would deepen is set by progression and is not re-read while it runs (ours) |  |
| 244 | utility | caster | implemented | Speed raised on the character drinking it, read wherever a fight reads the score, with a deadline on the one clock |  |
| 245 | utility | caster | implemented | Accuracy raised on the character drinking it, read wherever a fight reads the score, with a deadline on the one clock |  |
| 246 | utility | none | not yet | a weapon given the property of flame | an item-aim owner: the pack holds the party's items and nothing aims a potion at one |
| 247 | utility | none | not yet | a weapon given the property of frost | an item-aim owner: the pack holds the party's items and nothing aims a potion at one |
| 248 | utility | none | not yet | a weapon given the property of poison | an item-aim owner: the pack holds the party's items and nothing aims a potion at one |
| 249 | utility | none | not yet | a weapon given the property of sparks | an item-aim owner: the pack holds the party's items and nothing aims a potion at one |
| 250 | utility | none | not yet | a weapon given the property of swiftness | an item-aim owner: the pack holds the party's items and nothing aims a potion at one |
| 251 | condition | caster | implemented | the conditions the potion names lifted from the drinker's own condition state |  |
| 252 | condition | caster | implemented | the conditions the potion names lifted from the drinker's own condition state |  |
| 253 | healing | caster | implemented | hit points restored through the member's own pool, at the potion's own strength |  |
| 254 | utility | caster | implemented | spell points given back through the member's own pool, at the potion's own strength |  |
| 255 | utility | caster | implemented | a carried effect of its own identity (spell.fate) on that character, read where it applies and ended by a deadline on the one clock |  |
| 256 | resistance | caster | implemented | a resistance carried by the character and read by the fight's own resistance sum, with a deadline on the one clock |  |
| 257 | resistance | caster | implemented | a resistance carried by the character and read by the fight's own resistance sum, with a deadline on the one clock |  |
| 258 | resistance | caster | implemented | a resistance carried by the character and read by the fight's own resistance sum, with a deadline on the one clock |  |
| 259 | resistance | caster | implemented | a resistance carried by the character and read by the fight's own resistance sum, with a deadline on the one clock |  |
| 260 | resistance | caster | implemented | a resistance carried by the character and read by the fight's own resistance sum, with a deadline on the one clock |  |
| 261 | resistance | caster | implemented | a resistance carried by the character and read by the fight's own resistance sum, with a deadline on the one clock |  |
| 262 | condition | caster | implemented | the conditions the potion names lifted from the drinker's own condition state |  |
| 263 | utility | none | not yet | a weapon made deadly to dragons | an item-aim owner: the pack holds the party's items and nothing aims a potion at one |
| 264 | utility | caster | implemented | Luck raised by fifty for good through the character's own scores, once in their life: a second bottle is drunk and changes nothing |  |
| 265 | utility | caster | implemented | Speed raised by fifty for good through the character's own scores, once in their life: a second bottle is drunk and changes nothing |  |
| 266 | utility | caster | implemented | Intellect raised by fifty for good through the character's own scores, once in their life: a second bottle is drunk and changes nothing |  |
| 267 | utility | caster | implemented | Endurance raised by fifty for good through the character's own scores, once in their life: a second bottle is drunk and changes nothing |  |
| 268 | utility | caster | implemented | Personality raised by fifty for good through the character's own scores, once in their life: a second bottle is drunk and changes nothing |  |
| 269 | utility | caster | implemented | Accuracy raised by fifty for good through the character's own scores, once in their life: a second bottle is drunk and changes nothing |  |
| 270 | utility | caster | implemented | Might raised by fifty for good through the character's own scores, once in their life: a second bottle is drunk and changes nothing |  |
| 271 | utility | caster | implemented | every year the character was aged beyond their natural age given back, through the age progression keeps for them |  |

## Counts

| category | implemented | approximated | not yet | spells |
| --- | --- | --- | --- | --- |
| damage | 33 | 1 | 0 | 34 |
| healing | 6 | 1 | 0 | 7 |
| resistance | 9 | 1 | 0 | 10 |
| condition | 17 | 2 | 0 | 19 |
| light | 1 | 0 | 0 | 1 |
| travel | 2 | 0 | 4 | 6 |
| detection | 3 | 0 | 0 | 3 |
| utility | 8 | 2 | 9 | 19 |
| **all** | **79** | **7** | **13** | **99** |

## Every spell

A rung is the mastery of the spell's school the spell requires: one is basic, two expert, three
master, and four grand master.

| spell | category | rung | aim | state | what it does, or what is missing | receiver |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | light | 1 | party | implemented | a light carried by the party, read against the clock's own daylight and ended by its own deadline |  |
| 2 | damage | 1 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 3 | resistance | 1 | ally | implemented | a ward on the character the casting named, read by the fight's own resistance for that character and ended by its own deadline; the donor gives several of these to the whole party at once (OpenEnroth src/Engine/Spells/CastSpellInfo.cpp:767-801, pPartyBuffs[PARTY_BUFF_RESIST_*]), and this game's own table aims each one at a single character |  |
| 4 | utility | 1 | ally | not yet | a weapon in hand | an item-aim owner: the pack holds the party's items and nothing aims a spell at one |
| 5 | utility | 2 | party | implemented | a party-carried effect read by the fight's own resolution |  |
| 6 | damage | 2 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 7 | damage | 2 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 8 | damage | 3 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 9 | damage | 3 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 10 | damage | 3 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 11 | damage | 4 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 12 | detection | 1 | party | implemented | a report read from the places and the population the world holds |  |
| 13 | travel | 1 | party | not yet | a fall slowed until it cannot hurt | the party's mover, which walks and falls and does nothing else |
| 14 | resistance | 1 | ally | implemented | a ward on the character the casting named, read by the fight's own resistance for that character and ended by its own deadline; the donor gives several of these to the whole party at once (OpenEnroth src/Engine/Spells/CastSpellInfo.cpp:767-801, pPartyBuffs[PARTY_BUFF_RESIST_*]), and this game's own table aims each one at a single character |  |
| 15 | damage | 1 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 16 | travel | 2 | caster | not yet | a jump that carries the party over what it could not walk past | the party's mover, which walks and falls and does nothing else |
| 17 | resistance | 2 | caster | implemented | a shield on the caster, read by the fight's own ranged resolution — a creature's missile does half to them — and ended by its own deadline; the donor shields the whole party (OpenEnroth src/Engine/Spells/CastSpellInfo.cpp:904-945, PARTY_BUFF_SHIELD), and this game's own table aims it at the caster |  |
| 18 | damage | 2 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 19 | utility | 3 | party | implemented | a party-carried effect read by the fight's own resolution |  |
| 20 | damage | 3 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 21 | travel | 3 | party | not yet | flight over what the party could not walk across | the party's mover, which walks and falls and does nothing else |
| 22 | damage | 4 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 23 | condition | 1 | ally | implemented | the named conditions lifted through the member's own condition state |  |
| 24 | damage | 1 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 25 | resistance | 1 | ally | implemented | a ward on the character the casting named, read by the fight's own resistance for that character and ended by its own deadline; the donor gives several of these to the whole party at once (OpenEnroth src/Engine/Spells/CastSpellInfo.cpp:767-801, pPartyBuffs[PARTY_BUFF_RESIST_*]), and this game's own table aims each one at a single character |  |
| 26 | damage | 1 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 27 | travel | 2 | party | not yet | water walked over rather than swum through | the party's mover, which walks and falls and does nothing else |
| 28 | utility | 2 | none | not yet | an item whose charges are given back | an item-aim owner: the pack holds the party's items and nothing aims a spell at one |
| 29 | damage | 2 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 30 | utility | 3 | none | not yet | an item to enchant | an item-aim owner: the pack holds the party's items and nothing aims a spell at one |
| 31 | travel | 3 | none | implemented | a portal taken through the world's own transition path, charged by the world's own cost rule |  |
| 32 | damage | 3 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 33 | travel | 4 | none | implemented | a beacon set in the party's own carried state and recalled through the world's own transition path |  |
| 34 | condition | 1 | foe | approximated | a creature the casting named, not immune to earth, has twenty of the donor's ticks added to what it must recover before it acts again, through the fight's own recovery (OpenEnroth src/Engine/Objects/Actor.cpp:3073-3080, 3179-3188); the donor rolls the spell's hit as a blow's and knocks the creature back, and this build always lands it and moves nobody (ours) |  |
| 35 | condition | 1 | foe | implemented | a creature the casting named, not immune to earth, is slowed on its own state: its recovery is doubled and its pace divided by the spell's power until the clock ends it (OpenEnroth src/Engine/Spells/CastSpellInfo.cpp:572-610, src/Engine/Objects/Actor.cpp:1296, src/Engine/Graphics/Indoor.cpp:814-816) |  |
| 36 | resistance | 1 | ally | implemented | a ward on the character the casting named, read by the fight's own resistance for that character and ended by its own deadline; the donor gives several of these to the whole party at once (OpenEnroth src/Engine/Spells/CastSpellInfo.cpp:767-801, pPartyBuffs[PARTY_BUFF_RESIST_*]), and this game's own table aims each one at a single character |  |
| 37 | damage | 1 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 38 | resistance | 2 | party | implemented | armour class carried by the party and read by the fight's own armour class |  |
| 39 | damage | 2 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 40 | condition | 2 | ally | implemented | the named conditions lifted through the member's own condition state |  |
| 41 | damage | 3 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 42 | utility | 3 | none | not yet | a door or a container across the room | an item-aim owner: the interaction mechanism reaches what stands in front of the party |
| 43 | damage | 3 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 44 | damage | 4 | foe | approximated | the donor takes a share of the target's current health; this rolls the row's base and dice |  |
| 45 | detection | 1 | caster | implemented | a report read from the places and the population the world holds |  |
| 46 | utility | 1 | ally | implemented | an effect on the character the casting named, read by the fight's own resolution for that character and ended by its own deadline; the donor rewards a blessing, a fate, and hammerhands to one character below the rungs where it widens them to the party (OpenEnroth src/Engine/Spells/CastSpellInfo.cpp:846-880, :1631-1656, :2364-2384), and this game's own table aims each one at a single character |  |
| 47 | utility | 1 | ally | implemented | an effect on the character the casting named, read by the fight's own resolution for that character and ended by its own deadline; the donor rewards a blessing, a fate, and hammerhands to one character below the rungs where it widens them to the party (OpenEnroth src/Engine/Spells/CastSpellInfo.cpp:846-880, :1631-1656, :2364-2384), and this game's own table aims each one at a single character |  |
| 48 | condition | 1 | foe | implemented | every undead creature in view is made afraid on its own state and runs from what it fights until the clock ends it (OpenEnroth src/Engine/Spells/CastSpellInfo.cpp:1734-1762); the table aims the casting at a creature, and the spell takes hold of every one in view as the donor's does |  |
| 49 | condition | 2 | ally | implemented | the named conditions lifted through the member's own condition state |  |
| 50 | utility | 2 | caster | not yet | the party's gear protected from harm | item state, which carries what a spell would protect |
| 51 | utility | 2 | ally | implemented | an effect on the character the casting named, read by the fight's own resolution for that character and ended by its own deadline; the donor rewards a blessing, a fate, and hammerhands to one character below the rungs where it widens them to the party (OpenEnroth src/Engine/Spells/CastSpellInfo.cpp:846-880, :1631-1656, :2364-2384), and this game's own table aims each one at a single character |  |
| 52 | damage | 3 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 53 | healing | 3 | ally | implemented | a member stood back up at one hit point, with what laid them out lifted from their own conditions |  |
| 54 | healing | 3 | ally | implemented | the party's health pooled and shared through each member's own pool |  |
| 55 | healing | 4 | ally | implemented | a member stood back up at one hit point, with what laid them out lifted from their own conditions |  |
| 56 | condition | 1 | ally | implemented | the named conditions lifted through the member's own condition state |  |
| 57 | damage | 1 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 58 | resistance | 1 | ally | implemented | a ward on the character the casting named, read by the fight's own resistance for that character and ended by its own deadline; the donor gives several of these to the whole party at once (OpenEnroth src/Engine/Spells/CastSpellInfo.cpp:767-801, pPartyBuffs[PARTY_BUFF_RESIST_*]), and this game's own table aims each one at a single character |  |
| 59 | detection | 1 | caster | implemented | a report read from the places and the population the world holds |  |
| 60 | condition | 2 | foe | implemented | a creature the casting named, not immune to mind, stands with the party on its own state: the fight puts it on the party's side, it attacks nobody of the party and keeps its own quarrels with other kinds, until the clock ends it (OpenEnroth src/Engine/Spells/CastSpellInfo.cpp:615-660, src/Engine/Objects/Actor.cpp:2152-2154) |  |
| 61 | condition | 2 | ally | implemented | the named conditions lifted through the member's own condition state |  |
| 62 | condition | 2 | foe | implemented | a creature the casting named, not immune to mind, is driven berserk on its own state: it is the enemy of every creature and of the party, until the clock ends it (OpenEnroth src/Engine/Spells/CastSpellInfo.cpp:2005-2050, src/Engine/Objects/Actor.cpp:2134, 2142) |  |
| 63 | condition | 3 | foe | implemented | every living creature in view, not immune to mind, is made afraid on its own state and runs from what it fights until the clock ends it (OpenEnroth src/Engine/Spells/CastSpellInfo.cpp:2089-2119); the table aims the casting at a creature, and the spell takes hold of every one in view as the donor's does |  |
| 64 | condition | 3 | ally | implemented | the named conditions lifted through the member's own condition state |  |
| 65 | damage | 3 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 66 | condition | 4 | foe | implemented | a living creature the casting named, not immune to mind, is bound to serve on its own state: the fight puts it on the party's side and it fights what fights the party, until the clock ends it (OpenEnroth src/Engine/Spells/CastSpellInfo.cpp:2053-2087, src/Engine/Objects/Actor.cpp:2135-2160); an undead creature is not bound and the casting is spent, as the donor's is |  |
| 67 | condition | 1 | ally | implemented | the named conditions lifted through the member's own condition state |  |
| 68 | healing | 1 | ally | implemented | hit points restored through the member's own pool |  |
| 69 | resistance | 1 | ally | implemented | a ward on the character the casting named, read by the fight's own resistance for that character and ended by its own deadline; the donor gives several of these to the whole party at once (OpenEnroth src/Engine/Spells/CastSpellInfo.cpp:767-801, pPartyBuffs[PARTY_BUFF_RESIST_*]), and this game's own table aims each one at a single character |  |
| 70 | damage | 1 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 71 | healing | 2 | ally | implemented | health given back to the character the casting named every five minutes of game time the clock passes, through their own pool, until its own deadline (OpenEnroth src/Engine/Spells/CastSpellInfo.cpp:744-766, src/Engine/Engine.cpp:1236, 1398-1401) |  |
| 72 | condition | 2 | ally | implemented | the named conditions lifted through the member's own condition state |  |
| 73 | utility | 2 | caster | implemented | an effect on the character the casting named, read by the fight's own resolution for that character and ended by its own deadline; the donor rewards a blessing, a fate, and hammerhands to one character below the rungs where it widens them to the party (OpenEnroth src/Engine/Spells/CastSpellInfo.cpp:846-880, :1631-1656, :2364-2384), and this game's own table aims each one at a single character |  |
| 74 | condition | 3 | ally | implemented | the named conditions lifted through the member's own condition state |  |
| 75 | resistance | 3 | party | approximated | the donor reads this buff as a chance to resist a spell rather than as a resistance of one kind of harm; this build reads it as a ward against magic harm (receiver: the fight's spell resolution, which would make the check) |  |
| 76 | damage | 3 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 77 | healing | 4 | ally | implemented | hit points restored through the member's own pool |  |
| 78 | damage | 1 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 79 | damage | 1 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 80 | utility | 1 | none | approximated | the donor dispels the buffs of the creature it is cast on; this build's spell effects are the party's, so the casting ends what spells have left running on the party (receiver: an actor-buff owner for world actors) |  |
| 81 | condition | 1 | foe | implemented | a creature the casting named, not immune to light, is paralysed on its own state: the fight's gate refuses it every action until the clock ends it (OpenEnroth src/Engine/Spells/CastSpellInfo.cpp:549-570) |  |
| 82 | utility | 2 | caster | not yet | a creature summoned to stand with the party | the world's population, which places what content declares |
| 83 | utility | 2 | party | approximated | every score a fight reads is raised for every member; the pools a raised endurance, intellect, or personality would deepen are set by progression and are not re-read while it runs (ours) |  |
| 84 | damage | 2 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 85 | resistance | 3 | party | implemented | a ward carried by the party and read by the fight's own resistance |  |
| 86 | utility | 3 | party | implemented | a blessing on every character, and heroism, a shield, stone skin, and a haste carried by the party, each read where the fight reads that effect and ended by its own deadline; the haste is withheld while a character is weak (OpenEnroth src/Engine/Spells/CastSpellInfo.cpp:2530-2592) |  |
| 87 | damage | 3 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 88 | healing | 4 | party | approximated | the donor allows three castings a day; no daily count of a spell's castings exists in this build (receiver: a per-day cast count). The caster is aged ten years, never past a modifier of a hundred and twenty, as the donor ages them (OpenEnroth src/Engine/Spells/CastSpellInfo.cpp:2603-2607) |  |
| 89 | utility | 1 | foe | not yet | a corpse raised to fight for the party | the world's bodies and the fight's allegiance state |
| 90 | damage | 1 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 91 | utility | 1 | ally | not yet | a weapon to bear | an item-aim owner: the pack holds the party's items and nothing aims a spell at one |
| 92 | condition | 1 | foe | approximated | a creature the casting named, not immune to dark, is shrunk on its own state and the harm it does is divided by the spell's power until the clock ends it (OpenEnroth src/Engine/Objects/SpriteObject.cpp:951-1040, src/Engine/Objects/Character.cpp:5842-5846); the donor's grand master ray shrinks every creature near where it lands, and this build shrinks the one named (ours) |  |
| 93 | damage | 2 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 94 | condition | 2 | foe | implemented | an undead creature the casting named, not immune to dark, is bound to serve on its own state: the fight puts it on the party's side and it fights what fights the party, until the clock ends it (OpenEnroth src/Engine/Spells/CastSpellInfo.cpp:2719-2762); a living creature is not bound and the casting is spent, as the donor's is |  |
| 95 | utility | 2 | caster | implemented | pain reflection on the caster: the harm a creature's blow or missile does them is turned back onto that creature through its own resistance by the fight's damage application, until its own deadline; the donor gives it to every character at master and above (OpenEnroth src/Engine/Spells/CastSpellInfo.cpp:2813-2840), and this game's own table aims it at the caster |  |
| 96 | utility | 3 | none | not yet | a follower to give up | a follower owner: the party keeps no followers until hirelings and story companions land |
| 97 | damage | 3 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 98 | damage | 3 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 99 | damage | 4 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
