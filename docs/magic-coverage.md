# Spell effect coverage

Every spell this game states numbers for, and how far this build expresses its effect. The file is
generated from the ruleset's own table by `MagicCoverageTests` and checked by that test on every
run, so it cannot drift from the code: change a row's category, its rung, what it is aimed at, or
how far it is expressed, and this document has to be regenerated with
`CRAWLER_WRITE_MAGIC_COVERAGE=1 dotnet test tests/PartyRpg.Host.Tests`.

Names live in the operator's own content — this repository commits none — so a row is identified by
content's own spell id, which is also the id the ruleset's table is keyed by and the id this
document's rows carry. A reader who wants the shipped name for a row will find it beside the same
id in `src/PartyRpg.Rulesets.MightAndMagic7/MightAndMagic7Spells.cs`.

## How a state is read

| state | what it means |
| --- | --- |
| implemented | the cast changes state through the owner that holds it, and this build reads that state where it applies |
| approximated | the cast changes real state through that owner, more coarsely than the game does; the difference is named |
| not yet | nothing this build reads changes, and the owner that would close the gap is named |

## Counts

| category | implemented | approximated | not yet | spells |
| --- | --- | --- | --- | --- |
| damage | 34 | 0 | 0 | 34 |
| healing | 5 | 1 | 1 | 7 |
| resistance | 2 | 7 | 1 | 10 |
| condition | 9 | 0 | 10 | 19 |
| light | 1 | 0 | 0 | 1 |
| travel | 2 | 0 | 4 | 6 |
| detection | 3 | 0 | 0 | 3 |
| utility | 2 | 5 | 12 | 19 |
| **all** | **58** | **13** | **28** | **99** |

## Every spell

A rung is the mastery of the spell's school the spell requires: one is basic, two expert, three
master, and four grand master.

| spell | category | rung | aim | state | what it does, or what is missing | receiver |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | light | 1 | party | implemented | a light carried by the party, read against the clock's own daylight and ended by its own deadline |  |
| 2 | damage | 1 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 3 | resistance | 1 | ally | approximated | the donor's own buff belongs to the character it is cast on; this build's wards are carried by the party, so every member gets it (receiver: a per-member effect owner, which the party model does not have) |  |
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
| 14 | resistance | 1 | ally | approximated | the donor's own buff belongs to the character it is cast on; this build's wards are carried by the party, so every member gets it (receiver: a per-member effect owner, which the party model does not have) |  |
| 15 | damage | 1 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 16 | travel | 2 | caster | not yet | a jump that carries the party over what it could not walk past | the party's mover, which walks and falls and does nothing else |
| 17 | resistance | 2 | caster | not yet | a shield that turns a missile aside | the fight's own ranged resolution |
| 18 | damage | 2 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 19 | utility | 3 | party | implemented | a party-carried effect read by the fight's own resolution |  |
| 20 | damage | 3 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 21 | travel | 3 | party | not yet | flight over what the party could not walk across | the party's mover, which walks and falls and does nothing else |
| 22 | damage | 4 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 23 | condition | 1 | ally | implemented | the named conditions lifted through the member's own condition state |  |
| 24 | damage | 1 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 25 | resistance | 1 | ally | approximated | the donor's own buff belongs to the character it is cast on; this build's wards are carried by the party, so every member gets it (receiver: a per-member effect owner, which the party model does not have) |  |
| 26 | damage | 1 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 27 | travel | 2 | party | not yet | water walked over rather than swum through | the party's mover, which walks and falls and does nothing else |
| 28 | utility | 2 | none | not yet | an item whose charges are given back | an item-aim owner: the pack holds the party's items and nothing aims a spell at one |
| 29 | damage | 2 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 30 | utility | 3 | none | not yet | an item to enchant | an item-aim owner: the pack holds the party's items and nothing aims a spell at one |
| 31 | travel | 3 | none | implemented | a portal taken through the world's own transition path, charged by the world's own cost rule |  |
| 32 | damage | 3 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 33 | travel | 4 | none | implemented | a beacon set in the party's own carried state and recalled through the world's own transition path |  |
| 34 | condition | 1 | foe | not yet | a condition on a world actor | the fight's own condition model, which is the party's |
| 35 | condition | 1 | foe | not yet | a creature slowed | the fight's own actor state, which paces an actor by its row |
| 36 | resistance | 1 | ally | approximated | the donor's own buff belongs to the character it is cast on; this build's wards are carried by the party, so every member gets it (receiver: a per-member effect owner, which the party model does not have) |  |
| 37 | damage | 1 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 38 | resistance | 2 | party | implemented | armour class carried by the party and read by the fight's own armour class |  |
| 39 | damage | 2 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 40 | condition | 2 | ally | implemented | the named conditions lifted through the member's own condition state |  |
| 41 | damage | 3 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 42 | utility | 3 | none | not yet | a door or a container across the room | an item-aim owner: the interaction mechanism reaches what stands in front of the party |
| 43 | damage | 3 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 44 | damage | 4 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 45 | detection | 1 | caster | implemented | a report read from the places and the population the world holds |  |
| 46 | utility | 1 | ally | approximated | the donor's own buff belongs to the character it is cast on; this build's wards are carried by the party, so every member gets it (receiver: a per-member effect owner, which the party model does not have) |  |
| 47 | utility | 1 | ally | approximated | the donor's own buff belongs to the character it is cast on; this build's wards are carried by the party, so every member gets it (receiver: a per-member effect owner, which the party model does not have) |  |
| 48 | condition | 1 | foe | not yet | a creature turned away from the party | the fight's allegiance state, which is a side rather than a fear |
| 49 | condition | 2 | ally | implemented | the named conditions lifted through the member's own condition state |  |
| 50 | utility | 2 | caster | not yet | the party's gear protected from harm | item state, which carries what a spell would protect |
| 51 | utility | 2 | ally | approximated | the donor's own buff belongs to the character it is cast on; this build's wards are carried by the party, so every member gets it (receiver: a per-member effect owner, which the party model does not have) |  |
| 52 | damage | 3 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 53 | healing | 3 | ally | implemented | a member stood back up at one hit point, with what laid them out lifted from their own conditions |  |
| 54 | healing | 3 | ally | implemented | the party's health pooled and shared through each member's own pool |  |
| 55 | healing | 4 | ally | implemented | a member stood back up at one hit point, with what laid them out lifted from their own conditions |  |
| 56 | condition | 1 | ally | implemented | the named conditions lifted through the member's own condition state |  |
| 57 | damage | 1 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 58 | resistance | 1 | ally | approximated | the donor's own buff belongs to the character it is cast on; this build's wards are carried by the party, so every member gets it (receiver: a per-member effect owner, which the party model does not have) |  |
| 59 | detection | 1 | caster | implemented | a report read from the places and the population the world holds |  |
| 60 | condition | 2 | foe | not yet | a charmed creature that fights for the party | the fight's allegiance state, which is a side rather than a loyalty |
| 61 | condition | 2 | ally | implemented | the named conditions lifted through the member's own condition state |  |
| 62 | condition | 2 | foe | not yet | a creature driven against its own | the fight's allegiance state, which is a side rather than a rage |
| 63 | condition | 3 | foe | not yet | creatures made afraid | the fight's own actor state, which is not the party's condition model |
| 64 | condition | 3 | ally | implemented | the named conditions lifted through the member's own condition state |  |
| 65 | damage | 3 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 66 | condition | 4 | foe | not yet | an enslaved creature that fights for the party | the fight's allegiance state, which is a side rather than a loyalty |
| 67 | condition | 1 | ally | implemented | the named conditions lifted through the member's own condition state |  |
| 68 | healing | 1 | ally | implemented | hit points restored through the member's own pool |  |
| 69 | resistance | 1 | ally | approximated | the donor's own buff belongs to the character it is cast on; this build's wards are carried by the party, so every member gets it (receiver: a per-member effect owner, which the party model does not have) |  |
| 70 | damage | 1 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 71 | healing | 2 | ally | not yet | health given back over a duration | this effect path's own clock observation: the running-effect ledger hears every advance |
| 72 | condition | 2 | ally | implemented | the named conditions lifted through the member's own condition state |  |
| 73 | utility | 2 | caster | approximated | the donor's own buff belongs to the character it is cast on; this build's wards are carried by the party, so every member gets it (receiver: a per-member effect owner, which the party model does not have) |  |
| 74 | condition | 3 | ally | implemented | the named conditions lifted through the member's own condition state |  |
| 75 | resistance | 3 | party | approximated | the donor reads this buff as a chance to resist a spell rather than as a resistance of one kind of harm; this build reads it as a ward against magic harm (receiver: the fight's spell resolution, which would make the check) |  |
| 76 | damage | 3 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 77 | healing | 4 | ally | implemented | hit points restored through the member's own pool |  |
| 78 | damage | 1 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 79 | damage | 1 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 80 | utility | 1 | none | approximated | the donor dispels the buffs of the creature it is cast on; this build's spell effects are the party's, so the casting ends what spells have left running on the party (receiver: an actor-buff owner for world actors) |  |
| 81 | condition | 1 | foe | not yet | a condition on a world actor | the fight's own condition model, which is the party's |
| 82 | utility | 2 | caster | not yet | a creature summoned to stand with the party | the world's population, which places what content declares |
| 83 | utility | 2 | party | not yet | six attributes raised for a day | the attribute readings a fight is priced by |
| 84 | damage | 2 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 85 | resistance | 3 | party | implemented | a ward carried by the party and read by the fight's own resistance |  |
| 86 | utility | 3 | party | not yet | every attribute and resistance raised for an hour | the attribute and resistance readings a fight is priced by |
| 87 | damage | 3 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 88 | healing | 4 | party | approximated | the donor allows three castings a day and ages the caster by ten; neither a daily count nor ageing exists in this build (receiver: a per-day cast count and progression's ageing) |  |
| 89 | utility | 1 | foe | not yet | a corpse raised to fight for the party | the world's bodies and the fight's allegiance state |
| 90 | damage | 1 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 91 | utility | 1 | ally | not yet | a weapon to bear | an item-aim owner: the pack holds the party's items and nothing aims a spell at one |
| 92 | condition | 1 | foe | not yet | a creature shrunk | the fight's own actor state, which is not the party's condition model |
| 93 | damage | 2 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 94 | condition | 2 | foe | not yet | an undead creature made to fight for the party | the fight's allegiance state, which is a side rather than a loyalty |
| 95 | utility | 2 | caster | not yet | harm reflected onto whoever struck the party | the fight's damage application |
| 96 | utility | 3 | none | not yet | a follower to give up | an item-aim owner: the party's followers exist and nothing aims a spell at one |
| 97 | damage | 3 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 98 | damage | 3 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
| 99 | damage | 4 | foe | implemented | harm resolved through the fight's own path: the spell's own dice, the target's resistance, and the condition a landed hit leaves |  |
