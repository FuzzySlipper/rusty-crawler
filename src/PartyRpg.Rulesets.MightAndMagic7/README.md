# PartyRpg.Rulesets.MightAndMagic7

The compiled Might and Magic VII ruleset: the concrete policy
that turns `PartyRpg.Kit` mechanisms into that game. The owner-level contract is
in [`../../docs/code-organization.md`](../../docs/code-organization.md).

Owns:

- Classes, races, ranks, and the two-stage promotion ladder whose second step
  splits each class into a light and a dark alternative (`MightAndMagic7Promotions`: the ladder itself, the
  people who give each rank, what each rank asks for, and the record it leaves).
- Skills with their class- and rank-specific mastery ceilings, and skill points.
- Alchemy (`MightAndMagic7Alchemy`, `MightAndMagic7Potions`): the mixtures the shipped `POTION.TXT` states,
  read from the pack the importer writes — which reagent makes which potion, which pairs make something,
  which go off and how hard, and the discovery each one records — with the rung each result asks for. The
  tier is content, authored by the importer from the donor's four id bands
  (`OpenEnroth/src/GUI/UI/UIPopup.cpp:2092-2112`); what a mixture comes out at and what a burst costs are
  this ruleset's readings over the donor's own
  arithmetic (`OpenEnroth/src/GUI/UI/UIPopup.cpp:2141-2162, 2267-2268`, `:2118-2131`). What each potion does when drunk
  is one row per shipped potion id, authored from the donor's drinking switch
  (`OpenEnroth/src/Engine/Objects/Character.cpp:3080-3300`) and expressed through the same effect path a spell uses, so
  `docs/magic-coverage.md` lists the potions beside the spells and cannot drift from them.
- The nine spell schools and their 99 spells (`MightAndMagic7Spells`): which skill gates each school, the
  tier each spell requires, what one casting costs at each rung of that school's mastery, how long it makes
  the caster recover, what it rolls, what it is aimed at, and which of the design's eight effect categories
  it is. The costs, recovery, damage, and required mastery are the donor's transcription of the executable
  (`pSpellDatas`); the targeting and the categories are ours. Also the spell-point pool each class and its
  casting score add up to, the learning rule a book is judged against, the guild rung that gates which of a
  school's books a counter sells, and the effect path every category is applied through
  (`MightAndMagic7SpellEffects`): harm as an attack of the spell kind through the fight's own gated entry,
  health through the member's own pool in the donor's four shapes, conditions through the member's own
  condition state, a ward or a buff the table aims at one character landed on that character with its own
  deadline and read where it applies for them (the six elemental and body protections, blessing, fate,
  heroism, hammerhands, a shield, pain reflection, regeneration, and a potion's boost of one score), a ward or
  a buff aimed at the band carried by the party (a day of the gods, an hour of power's five buffs with its
  blessing on every character), all of them read by the fight's own answers (resistance, armour class,
  recovery, the chance to land, what a blow is worth, the scores every sum reads, the luck a save reads, a
  missile turned aside, a blow turned back, whether a creature notices the party) and ended by the clock, by a
  dispelling, or by the character no longer carrying anything; a regeneration gives health back for every
  five-minute boundary the clock passes while it runs (`GameCalendar.Boundaries`, read in the effect path's own
  clock observation before the ledger ends what came due); light ended by the clock's daylight window, travel as a portal
  through the world's own transition path with a beacon in the party's carried state, and detection over
  the places and the population the world holds. A creature a spell creates (`MightAndMagic7Summons`) is
  created by the world's own population: a summoned light elemental of the grade the caster's mastery calls,
  found by the monster table's own internal name the importer carries (`internalName`), up to one, three, or
  five at once, for five or fifteen minutes a level (`CastSpellInfo.cpp:2410-2445`, `Actor.cpp:4145-4204`), and a
  body the fight laid stood back up in its place when its row's level is within the caster's reach, with at
  most ten hit points a level of that reach (`CastSpellInfo.cpp:2613-2662`). Both carry who made them, which
  the fight reads as the ally side; both are worth no experience and drop nothing; a raised body leaves the
  corpse ground with what it held; and a save taken while one stands is refused by name, because the schema
  carries no population (#8658). The same reanimation aimed at a dead member (the row's aim names an actor of
  either side) raises them as a zombie (`MightAndMagic7Undeath`, `OpenEnroth/src/Engine/Spells/CastSpellInfo.cpp:2632-2640`,
  `OpenEnroth/src/Engine/Objects/Character.cpp:485-505`): the member's own `Zombie` condition, which a save carries with the rest, every other
  condition ended, health filled and spell points emptied; the zombie acts, a heal stops at half its maximum
  (`Character.cpp:1283-1297`), every five minutes of game time its health falls by one toward half and its spell
  points by one toward none (`Engine.cpp:1425-1429`), a night leaves it no spell points and half its health
  (`Party.cpp:737-739`), an ordinary temple ends the state at the ordinary price, and the temples of the dark powers
  (buildings 78, 81 and 82) keep it and raise the dead as zombies (`Temple.cpp:33-81`, `:178-188`); what is faithful
  and what is ours is stated per effect on `MightAndMagic7Undeath`. What each spell does inside its category is its row in
  `MightAndMagic7SpellReadings`, and how far this build expresses each one is reported per spell in
  `docs/magic-coverage.md`, which a test generates and checks against those rows.
- Items that carry a spell (`MightAndMagic7Spells.Reading`): a scroll read once and used up, and a wand
  fired as the weapon it is, one charge per shot, at the donor's own fixed skill value rather than at its
  bearer's. Both are read from the shipped item table's own reference column through the same join a book's
  lesson uses, and a scenario's party may declare what it wears and what its pack holds
  (`MightAndMagic7Party`), which the live checks stage their starts with.
- What a character wears (`MightAndMagic7Figure`, `MightAndMagic7EquipmentUse`): the donor's sixteen slots
  (`ItemEnums.h:1054-1072`) under this game's names — `off hand`, `main hand`, `bow`, `armour`, `helm`,
  `belt`, `cloak`, `gauntlets`, `boots`, `amulet`, `ring 1` to `ring 6` — which kind of item goes where
  (`ItemEnumFunctions.h:240-266`), and what each worn row states: its dice, read as the donor reads the item
  table's damage cell (a bare number is armour class), and its modifier. The use rule judges the place, then the
  hands — a two-handed weapon needs the off hand empty, and a second weapon there needs a dagger at expert or a
  sword at master (`UICharacter.cpp:1908-1910`) — then the skill, each refused by name. Where the donor's doll
  hands a displaced shield back to the cursor, this game refuses and names what to take off: one change moves
  one item (ours). The player changes the figure through the kit's `PartyOutfitting` (`party.equip`,
  `party.unequip`), and the fight reads it into every character sum, term by term in the donor's order:
  - **recovery** (`Character.cpp:1636-1750`): the weapon's base from the donor's table (`mm7_data.cpp:355-378`)
    — a bow's for a shot, sixty for a trained unarmed fighter, a staff's hundred for empty hands — a slower
    off-hand weapon, a shield's and the armour's ticks at the share their own rung leaves, less the speed bonus,
    an expert sword's, axe's or bow's level, armsmaster (not for a shot or a blaster), and haste, floored at
    thirty for a blow and five for a shot. Faithful; a swift weapon's twenty ticks wait for item enchantments
    (#8513).
  - **armour class** (`Character.cpp:1875-1887`): the speed bonus, every working passive piece's dice and
    modifier (`:2299-2304`), the skill bonus of the shield, leather, chain, plate, staff, sword and spear worn,
    dodging while nothing heavier than leather is (`:2596-2648`), and the stone skin a spell adds. Faithful;
    the enchantment half of the items bonus waits for #8513.
  - **chance to land** (`Character.cpp:768-778`, `:911-922`): the accuracy bonus, the weapon skill with
    armsmaster (or unarmed and armsmaster for empty hands), the weapon's modifier in each hand, and for a shot
    the bow's modifier and level; a blessing is added where the chance is priced. Faithful.
  - **blow and shot** (`Character.cpp:814-856`, `:954-987`): the main hand's dice and modifier, a spear's extra
    die with the off hand empty, a second weapon's dice, the weapon skill's damage bonus (`:2693-2742`), might,
    heroism and hammerhands; a shot is the bow's dice and modifier and a grand master's bow level, without might.
    Faithful except that a second weapon whose die differs from the first is added as its average, because one
    roll of the kit's states one kind of die (ours). A master of the dagger triples each dagger's own dice and
    modifier — not the skill or might added after — at a chance of the dagger level in a hundred, drawn per hand
    (`:899-905`), stated as the kit's `DamageMultiplier` over that hand's run of the roll's dice; the chance is the
    donor's corrected reading (the original executable fixed it at ten in a hundred), and an averaged second
    dagger is tripled as its average. Not read: a slaying enchantment's double damage (#8513).
  - **resistances** (`Character.cpp:1900-1993`): a grand master of leather in working leather armour adds the
    leather level to fire, air, water and earth, beside the wards spells leave, and the base
    (`MightAndMagic7BaseResistance`, `:1900-1942`): the race's bonus — goblin five fire and air, dwarf five
    water and earth, elf ten mind, human five body and so five spirit, which reads body's base — a ruleset table
    cited to the donor because the shipped data has no race table, and a Lich's own floor (twenty in each element,
    two hundred mind and body, `:4025-4042`) read from the class, with a Lich's whole resistance held to two
    hundred (`:1988-1990`). Faithful. The donor's stored base starts at nothing and is raised by a map event's
    permanent resistance (`:4788-4817`), which `MightAndMagic7Fixtures` writes into the member's own stored
    resistances (the kit's `CharacterResistances`, saved with the member and capped at a byte), and by a genie
    lamp this build does not grant (#8513); a Lich's floor is read as a floor under the stored figure. Followers
    are #8514 and enchantments #8513. A special attack's saving throw reads the same sum.
  - **the scores every sum reads** (`Character.cpp:729-765`, `GetActualStat`): the score the character carries
    at the share their age leaves of it (`MightAndMagic7Ageing`, the donor's table at `:222-232`), plus a potion's
    boost of that score on them and the party's day of the gods, which adds to all seven (`:2360-2387`). Faithful
    for those terms; the conditions multiplier, item bonuses (#8513) and a follower's luck (#8514) are not
    invented. The pools are set by progression and are not re-read while a boost runs (ours).
  - **what a spell adds** (`Character.cpp:2322-2395`, `GetMagicalBonus`): every buff is read as the character's
    own plus the party's of the same name, so a potion on one member and a spell on the band are one reading —
    a haste takes the donor's flat twenty-five ticks whichever carries it (`:1723-1728`), and nothing the party
    carries hastens a person in the world.
  - **missiles and reflection**: a creature's missile is halved against a character carrying a shield, the
    spell's or the potion's (`Character.cpp:5987-6009`), stated as the fight's plan divisor; and a character
    carrying pain reflection turns the harm a creature's blow or missile did them back onto that creature through
    its own resistance (`:5875-5900`, `:6042-6062`), through the kit's `ICombatReflectionRule`. Faithful; the items
    that shield their wearer wait for #8513.
  - **what a spell leaves on a creature** (the kit's `CreatureEffects`, held on the creature and counted down by
    the fight's own clock advances): a paralysis is the fight's gate refusing every action (`Actor.cpp:169-176`), a
    slowing doubles the creature's recovery (`Actor.cpp:1296`) and divides its pace (`Indoor.cpp:814-816`), a
    shrinking divides its blow (`Character.cpp:5842-5846`), a fear makes it run in `MightAndMagic7MonsterAi`
    (`TurnEngine.cpp:889-892`), and a stun adds the donor's twenty ticks to its recovery through the fight's own
    `Delay` (`Actor.cpp:3179-3188`). A creature immune to the spell's kind of harm is untouched, and which kinds are
    undead is read from the shipped matrix's own column names (`MonsterEnumFunctions.cpp:278-288`). A creature a
    spell holds still is not down: it can be struck and the place still holds it. Allegiance is the same state read
    by `NatureOf` and by `MightAndMagic7MonsterAi`'s enmity in the donor's order (`Actor.cpp:2097-2104`,
    `:2122-2165`): a charmed or bound creature is the kit's `Hostility.Allied` and stands on the fight's `Ally` side
    — the party's act does not aim at it, it attacks nobody of the party, a charmed one keeps its quarrels with
    other kinds and a bound one is read as the party's own kind — and a berserk one is everybody's enemy at the
    longest band. Each of the three ends the other two.
  A member wearing a bow shoots it at whatever the party's pick finds, where the donor swings at a target in
  melee range and shoots otherwise (`Character.cpp:6367-6397`): the kit asks one kind of attack per actor rather
  than per target (ours). Each sum is a list of terms, so a later owner's term — a buff, an enchantment — is one
  more line in it. A save carries the figure as the party's item custody, and the ruleset suite resumes it and
  reads the same blow back.
- Monster, item, service, and condition definitions and their interpretation.
- Combat, damage, resistance, conditions, recovery, reward, and experience formulas.
- Progression policy: the experience curve, how a party's award divides, what a
  level gives each class and rank, the skill points a level grants, and what the
  world makes of a party's deeds. Age (`MightAndMagic7Ageing`): a character's natural age is twenty-one at the
  clock's first year and a year more for every year it runs (ours: the donor draws a birth year up to five years
  earlier, `Character.cpp:2891`), and the years something aged them beyond it are the character's own
  `CharacterProgression.AgeOffset`, which a save carries — a divine intervention adds ten to its caster, never past
  a hundred and twenty (`CastSpellInfo.cpp:2603-2607`), and a potion of rejuvenation gives them all back
  (`Character.cpp:3297-3299`). A pure potion raises its score by fifty for good, once in a character's life, kept
  as a record under the character and the score (`Character.cpp:3282-3295`). A creature's ageing touch lands and
  is saved against as a condition is, against endurance, and adds a year to the character's `AgeOffset` with no
  ceiling (`Character.cpp:1351-1360, 1604-1610`); breaking and stealing an item are not applied yet (see
  `ConditionOf`). A divine intervention may be cast three times a day by each caster
  (`MightAndMagic7DailyCasts`): the count is a record under the spell, the caster, and the clock's day, which
  turns at three in the morning as the donor's does (`CastSpellInfo.cpp:2592`, `Engine.cpp:1036-1081`), and a
  fourth is refused before anything is spent.
- Time, calendar, rest, fatigue, and travel policy, including service hours.
- Quest, guild, reputation, and journal policy: the errands the shipped quest table states, what each asks
  and pays, who gives it, and the board a town hall posts.
- Standing policy (`MightAndMagic7Standing`): the world's opinion of the party — the donor's five band words
  at the donor's four edges, what each band does to a person's line and to a hall's notice, what a finished
  errand is worth to it, and the reading that names what the party has accomplished out of the tables that
  wrote it. The services' price rule reads the same number, and the standing line a person speaks is composed
  in `MightAndMagic7Conversation` as an ordinary standing condition, so the band, the gate, and the words are
  one table.
- Discovery policy (`MightAndMagic7Knowledge`): how a note about each kind of discovery reads, and which
  discoveries this game keeps — a find when the shipped item table marks it an artifact or a relic, a recipe
  when the potion table's own cell records one, and a landmark's effect or a line read when the event that
  gave it runs — the shipped discovery table's own row, which a fixture's event names by number.
- Fixture policy (`MightAndMagic7Fixtures`, over `MightAndMagic7MapEvents`): what using a well, a fountain,
  an obelisk, a sign or a shrine does — the steps of the map event it raises, interpreted for the instructions
  this game's fixtures use as changes through the owners that keep what each step names, and every other
  instruction refused by name before anything changes. The split is stated below.
- Content interpretation and presentation meaning: what an imported region,
  map, sprite, or sound means to this ruleset.
- Session composition: assembling the kit's named services with Might and Magic
  policy, and Might and Magic save meaning.

Boundary rules:

- Might and Magic vocabulary is legal here, in Might and Magic content packs, in
  Might and Magic presentation, and in `MightAndMagic7.Import`. It is illegal in
  `PartyRpg.Kit`.
- This is a compiled ruleset. Adding code-bearing semantics requires a product
  rebuild; changing valid content or tuning does not. No runtime assembly
  loading, reflection discovery, or ambient service lookup.
- Might and Magic VI and VIII are donor context for formats and divergences, not
  targets: no code path may quietly depend on their data.
- Adjustable values are the handles `MightAndMagic7Tuning` declares — an errand's
  default pay, a bounty's rate, a lesson's base price, a shelf's lines, the two
  fares and how many days each network's journey takes (`fare.coach-days`, two, and
  `fare.boat-days`, three), a night's length, what a night under a roof eats, and how long a well's
  temporary bonus lasts (`fixture.bonus-hours`, a day — ours: the donor keeps it until the next rest), and how long a
  counter that caught a thief stays shut against the party (`theft.ban-hours`, the donor's day) — each with its
  default, range, and meaning. A rule reads them through the `TuningProfile` it
  composes from the selected catalog, so a bundle's tuning pack changes play
  without a rebuild and a value out of range stops composition by name.

Implemented today: `MightAndMagic7Ruleset` (the compiled ruleset and its identity) and
`MightAndMagic7Session`, which composes the kit's session shell with this game's identity: this game's
world and movement policy (`MightAndMagic7World`, `MightAndMagic7Movement` — the party's body and walk
speed, cited from the donor, and the engine's own controller tuning scaled to that body; the donor's fall harm,
which a feather fall the party carries spares (`Outdoor.cpp:1426`); and the jump spell's leap, the party's own jump
at the donor's ratio of a thousand to five times ninety-six (`CastSpellInfo.cpp:1111-1121`, `Outdoor.cpp:1193-1197`),
whose landing is not a fall; and flight, which its caster carries and pays a spell point for every five minutes in the
air below grand master (`Engine.cpp:1286-1296`), at four times the walk up to the donor's ceiling, never indoors;
and drowning, a tenth of each character's health every thirty game seconds the party stands on a region's water
(`Engine.cpp:1083-1099`), spared by a water walk its caster carries and pays for every twenty minutes on water, and
for one character by water breathing, with no stop allowed in water (`Game.cpp:1088-1089`); fluid faces are named
and drown nobody, as in the donor; the drowning rule names what spares whom — a water walk and a flight that holds
the party up spare everybody, water breathing its drinker — for the panel's footing row), the one clock
(`MightAndMagic7Time` — the authored calendar, the donor's starting moment, its thirty-to-one rate, and
the hours it calls daylight), the party its content declares as scenario state (`MightAndMagic7Party`,
through the same factory creation hands a party to, never a party of its own invention, and what a session
on the scenario's start plays — one party per selection, refused by name with every candidate
when the selection states two, exactly as the scenario's starting place is), the larder's
policy (`MightAndMagic7Provisions` — one ration a day, and the weak condition a larder left short puts
on every member, which a rest ends and a meal does not, as in the donor), and what a crossing costs (`MightAndMagic7TravelCostRule` — a day on the road and the
rations it eats; a fare is honoured by the passage the party bought, and a portal — a crossing the caster
issues rather than a place — is free of road time because the spell already paid for it), how long a journey a
counter sells takes (`MightAndMagic7FareDays` — a stable sells on the `coach` route and a dock on the `boat`
route, and the days are the tuned length of that network, the one answer the counter quotes and the road charges
the clock; the ticket the party holds and a save carries names the route and not the days, so a retune never
strands a passage), where those counters sell passages to (`MightAndMagic7FareNetwork`, an `IFareNetwork` — over
the counters content places, every stable reaches every other place that keeps a stable and every dock every other
port, one crossing per pair of stops landing at the destination's own start; the counter's offers and the world
graph's sold crossings are both read from it, through `MightAndMagic7World.Graph`, and content that authors a sold
crossing of its own, or stands one passage counter in two places, is refused by name; ours, simpler than the donor's thirty-five scheduled routes), which creatures a level's spawn records put on the field
(`MightAndMagic7Spawns` — the importer writes each actor spawn record as an `encounter` placement and chooses nothing;
this game draws a random slot's count from its range and each creature's grade from the donor's odds for the
slot's difficulty, through the engine's keyed random service under the place and the spawn index, when the
population reads the place's placements, so every visit and every load see the same creatures, and an errand
that counts every one of a kind in a place counts that same resolution; through the same seam, each of a map's
own `actor` records — a creature the level is built holding, written with its row, group, point, facing and its
index in the level's actor array — stands as exactly one `monster` placement, `monster-actor-<index>`, keeping the
`actors` field and index, at the record's point and facing, and drawing nothing; a record marked `hidden` (the
donor's `Disabled`, `OpenEnroth/src/Engine/Graphics/Outdoor.cpp:617-618`, revealed only by clearing its bit,
`OpenEnroth/src/Engine/Objects/Actor.cpp:124-136`, which no shipped event step does) stands as nothing, and a record naming a
row the monster table lacks is refused at composition, `actor-monster-unknown`; the donor loads that array before
its spawn records and reloads it on a respawn, `OpenEnroth/src/Engine/Graphics/Indoor.cpp:310-319,907-922`, so a first visit and
every restore hold it — the record's stored hit points are provenance, the fight reads the row's, as the donor's
load does, `OpenEnroth/src/Engine/Objects/Actor.cpp:2899-2925`), and what using
something means here (`MightAndMagic7Interaction` —
a door from the delta's own stored state with the donor's interaction range, a fixture or a decoration that
raises an event as a fixture whose use runs the event (`MightAndMagic7Fixtures`, below), a `requires` array on a placement as
this game's locks, and a refusal that says what it needs), which places are clocked
(`MightAndMagic7Schedules` — the counters' own hours, or the hours a place states in its own entry, read
against the one clock so a door outside them is an unmet requirement rather than a menu entry that hides
itself), what fighting costs here (`MightAndMagic7Combat` — a monster's recovery is the monster table's own
`Recovery` column, its hostility band is the distance at which it notices the party, and a character is
paced by the donor's own attack-recovery sum over what they wear (see the figure below); a creature's first recovery is a keyed draw over the actor so a group placed together does not strike in
lockstep, a creature is recognized by a placement of kind `monster` naming the row it is, and a person a
map's own record places reads the monster row that record names rather than one peasant row for everybody),
what a creature does with its moment (`MightAndMagic7MonsterAi` — the row's `AI Type` column decides
whether it runs and at how many hit points (`Wimp` always, `Normal` at twenty percent, `Aggress` at ten,
`Suicidal` never), its `Move` column whether it closes or holds its post, its speed column how fast it
walks, and its own chance columns which of its ways of attacking it uses in the donor's own order (the
first spell, the second, the second attack, then the first attack — `Actor.cpp:3644-3657`), with every
chance drawn from the engine's keyed service so the same fight replays identically; a creature that
chooses the party strikes a member drawn the same way from those not paralysed, unconscious, dead,
petrified or eradicated, and the first member only when nobody is left (`Actor.cpp:3259-3287`; the donor's
attack-preference narrowing by class, sex or race is not read, so every such member is equally likely);
which kinds of monster
are each other's enemies is the shipped `hostile.txt` matrix read as content
(`MightAndMagic7Hostility`), and a spell the imported spell table describes no harm for is one this build
cannot cast, so a creature keeps it on its row and never chooses it), and what one attack does here is the
same policy's other half,
`ICombatResolutionRule` and `ICombatAbilityResolutionRule`: a character's chance to land a blow is the
donor's own hit test
(`Character.cpp:6263-6300`) and a creature's is its other one (`Actor.cpp:3691-3707`), a character's blow is
the weapon in hand's own dice — or the unarmed three-sided die — plus its skill and might bonuses
(`Character.cpp:814-856`) while a
creature's is its row's own dice — its second attack's dice and kind of harm when the order names that
way of attacking, and a spell's own dice and kind from this game's per-spell table
— harm is of the row's own kind (`ItemEnums.h:10-23`, read from the monster
table's own attack-type column) and a monster's blow may leave the condition its special-attack column names
through the donor's chance and saving throw (`Character.cpp:1333-1600`), and resistance is the donor's four
checks over the resistance plus thirty (`Actor.cpp:3743-3758`, `Character.cpp:1097-1108`) with the table's
own `Imm` cell read as full immunity (`Monsters.cpp:327-330`) — the importer types every combat cell of a
monster row into named fields (`attack`, `secondAttack`, `firstSpell`, `secondSpell`, `resistances`,
`immunities`, `specialAttack`, `hostilityKind`) and the pack carries no raw row, so `MightAndMagic7Damage`
names the kinds, `MightAndMagic7SpecialAttacks` is the vocabulary of the table's own special-attack words
(matched whole, with the strength and count the importer read), and
`MightAndMagic7Health` is what a wound leaves on a character: unconscious while their health plus base
endurance is at least one, dead below that (`Character.cpp:1310-1316`)), what a kill leaves
(`MightAndMagic7Corpses` — a creature that dies on a region's water sinks and leaves no body, so what it carried
is lost, as the donor removes it (`Outdoor.cpp:1596-1619`) and loots only a dead body (`Viewport.cpp:239-241`),
read through the party's mover at the place's named ground under the body; otherwise the fight reports what it read
as down and this game generates each death's loot once, under a key that names the place, the creature, and which death it was, and holds it on the
body: the monster table's own treasure cell, read by the importer into a chance, coin dice, a treasure
level and the kind of thing asked for (`Monsters.cpp:440-490`), with the coin rolled and the item drawn
from the item table's own weights by level (`ItemTable.cpp:316-374`) and the donor's fallback when a level
offers nothing the cell asked for (`ItemTable.cpp:347`) — and `MightAndMagic7Loot`, which owns the same
tables for a container's random reference: the level is remapped through the place's own danger level
(`Item.cpp:760-783`) and yields one to five findings of nothing, coin or an item
(`Chest.cpp:323-365`), with the seventh level handing over one of the table's own artifacts
(`ItemEnums.h:977-978`) — a body and a chest are then searched through the one container mechanism, and a
corpse reads as the same kind of target a chest is), and what stopping costs here (`MightAndMagic7Rest` — eight hours under a roof or in the open, the
donor's ground table for what a camp eats, its own proximity rule for a party that will not lie down with
creatures near — a creature on the fight's ally side, whatever made it one (a charm, a binding, a control, or a spell
that created it), is not one of them (`Actor.cpp:3458-3481`) — an interrupted night that lasts only the hours it lasted, and the day-long debt of sleep
that weakens the party on the clock's own deadline). Party creation's game definitions are landed too:
`MightAndMagic7Creation.Options` offers the four races with their attribute ranges, the eight portraits,
and the nine base classes with the two skills each fixes, the nine it offers, and the hit and spell
points it starts with, over a pool of fifty points and two chosen skills per character;
`MightAndMagic7CreationTables` records which of those values the shipped data carries and
which are ours, with the manual citation and the donor transcription each authored value rests on; and
`MightAndMagic7Creation.Defaults` is the default party the flow applies through its own validation. Which
start a new session takes is the selected scenario's statement: the `scenario-start` entry's `party` word
(`MightAndMagic7World.StatedPartyStart`, answered in `MightAndMagic7Session.NewPartyStart`) is `creation` —
the session's creation mode, where the party a player accepts is built through the same factory and the world
is composed over it — or `scenario`, which plays the party the scenario's `scenario-party` document fixes
with no creation screen. It sits on the start rather than the bundle because how a game begins is the
scenario's decision and a bundle is only the kit's pack selection. A start that leaves it out takes creation
wherever the host declared a creation screen and the scenario's party where it declared none; a start asking
for creation from a host without the screen, a start asking for the scenario's party when the selection
states none, and a word that is neither are each refused by name. The composition block's `partyStart`
(`creation`, `scenario`, or `resumed`) says which start the session took. Whichever start it is, what the
party plays — its ledger and the world composed over it with every answer the world needs — is composed by
the session's one `Play` (also the resumed path's), and the kit composes every owner over it through one
sequence: both paths must go through that one composition, which `SessionCompositionParityTests` proves by
asking every owner to answer on each path and naming the owner and path that does not. The
class and skill definitions the choices name are checked against the loaded content, which refuses a
catalog that contradicts them. This game's save meaning is landed with them: `MightAndMagic7Persistence`
states the storage scope a session's saves live in under the host's persistence root, the engine-backed
store a session writes through, and the check that judges a document against the content it would be
resumed in before anything is built — naming every problem at once rather than the first.
`MightAndMagic7Ruleset.Save` writes the live session and `MightAndMagic7Ruleset.ResumeSession` composes the
same session a new game composes over the same content and hands it the save, so the party, its items,
equipment and portraits, the clock, the place and pose, and what each place remembers come from the save
while everything transient is composed fresh. A scenario member may state the `portrait` it was created
with, which then travels into the party and into a save exactly as a chosen one does. This game's progression is landed with them: `MightAndMagic7Progression` is
the ruleset's whole contribution to the kit's owner — the donor's cumulative experience curve, the donor's
division of a party award with the learning skill's bonus, the per-level class and rank growth tables with
the skill points a level grants, and the fame the party's deeds earn — and `MightAndMagic7Combat` hands each
death's own row, its experience column included, to the one award path the fight reports through, so a kill,
a quest, and any later deed grow a character through the same owner and a training hall only charges the fee
and quotes the step. This game's skills are landed beside it: `MightAndMagic7Skills` reads the shipped
table's 37 rows into the four blocks the manual states — 34 usable, with Blaster, Diplomacy, and Thievery
reported as the rows this game does not use — and answers the ceiling a class and rank impose from its own
transcription of the donor's per-class mastery matrix, its authored level bands over the donor's own rung
thresholds, and the donor's per-skill mastery fees, so a raise past the limit is refused with the limit and
the promotion that would raise it named. The keeper of a guild is that school's master teacher: the rung a
guild stands at in its own ladder is the deepest rung of its skill the counter offers, bought through the
conversation's counter handoff and the service mechanism's own lesson path, and `MightAndMagic7EquipmentUse`
refuses a weapon or armour whose row names a skill the member has not learned while the manual's five
exempt places need none. This game's ranks are landed with them: `MightAndMagic7Promotions` is the
ladder — the shipped class table's 36 rows as 9 families of a base class, a first promotion, and two
second-promotion alternatives — with the 27 ranks it states, the 18 people the shipped NPC and topic tables
name as their givers, each rank's errand from the shipped quest table, the proof items the shipped item table
carries where the errand's own words name one, the two counted deeds the original keeps as awards, the record
each rank leaves on the party (`promotion:<rank>`, the original's own award bit under this game's name), and
the eight classes whose pair of alternatives splits on the two schools. A rank asks for what this build can
judge — its giver, what the party carries, what deeds it holds on record — and states an errand whose words
name a deed as the record a finished quest leaves (`errand:<bit>`, the same identity a shipped topic's own
requirement column already gates a person's line on), so seventeen ranks' errands are judged by real quest
state rather than refused. What each errand asks is approximated: the deeds are the original's event programs,
which this build does not run, so all but one are judged by standing in the place the shipped words name and
one by a kill (`MightAndMagic7Quests`, each row stating why). `PartyProgression.Promote` is the one writer: it judges every
requirement before anything moves, refuses with what is missing named, and moves the class and the rank
together, so the ceiling, the growth table, and every class condition read one fact. A person the ladder names
as a giver offers the ranks they give through the conversation that already exists, and taking one hands the
party to that owner through the promotion handoff; the ceiling a class and rank impose now answers with the
path when a class's own choice closed a school, so a lesson, a book, and a casting are each refused with the
alternative named (`skill-closed-by-path`); and `PromotionSnapshot` publishes the ladder and what each rank
did, member by member, for the panel. What the shipped data carries, what this game authors, and the live
promotion through both stages are recorded in [`docs/evidence/promotions.md`](../../docs/evidence/promotions.md).
What the world makes of the party is landed beside them. `MightAndMagic7Standing` states the bands —
the donor's own five words at the donor's own four edges (`UIGame.cpp:1645-1654`, `GetReputationString`),
read into this game's convention in which a higher reputation is a better one, where the donor negates a
location's reputation to print it (`LocationInfo.h:7`) — what each band does, and what a deed is worth to it.
The mover is ours and stated as such: a finished errand moves the world's opinion by one point per thousand
experience it paid, at least one, which is the donor's own figure for what word of a deed is worth read from
the other side (`Party.cpp:371-379`, `Party::fame`); a creature killed where nobody was watching moves it not
at all, which is the donor's own reading (`Actor.cpp:3164-3167`). **Killing a townsperson lowers it one
point**, the donor's own step (`Actor.cpp:1083-1105`, `ApplyFineForKillingPeasant`, whose sign-flipped
`reputation++` is our point down): `MightAndMagic7Crimes` reads a death whose row is one of the shipped
peasant rows — the donor's `IsPeasant` (`MonsterEnumFunctions.h:48-54`), so every person whose record names
no row of their own, and not a guard or an adept — and credits its experience under `townsperson-kill`
rather than `kill`, so the deed reaches the world's opinion through `PartyProgression.Award`'s one entry as
every other deed does. **Any other peaceful person's death lowers it the same point** (ours: the donor moves
reputation only beside a peasant's fine) — a guard or an adept a place's own records stand there is credited
under `person-kill`, with no fine; a creature the fight reads as peaceful for another reason (the party
unseen, a band-zero creature) is not a person and is an ordinary kill; a death worth no experience reaches the
standing as a deed (`PartyProgression.Deed`) rather than being dropped. The fine beside a townsperson's death is
the donor's sum, `100 × (base + the row's level + the party's reputation in the donor's sign)`, read before the
deed moves the standing as the donor's order has it. The base is the map table's "Steal Perm" column
(`MapTable.cpp:73`), which the importer now carries onto every place as `stealFine` (Emerald Isle's own value is
zero). **The fine is a debt, as the donor's is** (`uFine`): it is added to what the party owes on the `fine`
account (`PartyDebts`), the whole kept between nothing and four million as the donor keeps it
(`Actor.cpp:1093-1099`), saved with the party, and paid at a town hall (`TownHall.cpp:30-45`, below); the purse is
never touched at the death. Ours: the donor's light-and-dark exemptions (`Actor.cpp:1087-1091`) are not read,
and its throne room's year in jail that clears a fine (`UIHouses.cpp:346-351`) has no building here, because the
imported building table carries no throne room.
**Theft is the donor's, through the one service mechanism** (`MightAndMagic7Theft`). Who may try is the donor's:
a member with the Stealing skill learned and able to act (`Character.cpp:360-362`), at the four shops whose
screen offers a thief the shelf — weapon, armour, magic, alchemist (`Shops.cpp:1100-1137`; a guild's books have
no theft) — or from a person standing in the world, reached from the conversation with them. A thief's reach is
one of the donor's five luck draws (−200…+200) plus the skill's level times its rung's figure (100/200/300/500,
`Character.cpp:110-118`). At a counter the cost of being seen is `100 × (standing in the donor's sign + base fine)`
plus the line's worth, three times over for a weapon (`Character.cpp:1166-1174`); one theft in twenty is seen
whatever the measures say, a reach that covers the cost goes unseen, one short by less than five hundred is seen
with the goods, and one shorter still is seen empty-handed (`Character.cpp:1180-1192`). Seen: the cost is added
to the fine, the world's opinion falls one point (two with the goods), and the counter will not serve the party
for `theft.ban-hours` (the donor's day, `Shops.cpp:1147-1166`); unseen, the opinion still falls two points
(`Shops.cpp:1165-1171`). What a shelf gives up carries the stolen mark (`Shops.cpp:1123`), and no counter will
buy, identify, or repair a stolen thing (`Item.cpp:684-686`). From a person the cost is their row's level plus
`100 × (base fine + standing)`, a thief who falls short is seen (`Character.cpp:1211-1214`), and every attempt
lowers the opinion one point whatever comes of it (`Actor.cpp:1236`); an unseen hand finds coin three times in
ten — the skill's level of dice whose sides the rung decides, never more than the person carries — something
else three times in ten, and nothing the rest (`Character.cpp:1220-1279`). Ours: a person who sees the hand fines
the party the cost the donor reckons and never charges, and the person robbed — not every peasant near, as the
donor's `AggroSurroundingPeasants` has it — stops talking and is put into the fight; what a person carries is what
their row would leave if they fell, drawn once and remembered for the session but not saved (the donor keeps it
on the actor, which this build does not save); a counter's line is worth the item table's value without the
donor's enchantment; and a theft charges no recovery time. Each theft's fall in the world's opinion is a deed
that pays no experience, told through `PartyProgression.Deed` under its own word (`theft-caught`,
`theft-caught-with-goods`, `theft-unseen`, `pickpocket`), which `MightAndMagic7Standing` reads. **A town hall
collects the fine** (`TownHall.cpp:30-45`, `:71-91`): while the party owes one, the hall offers the debt, and a
repayment takes what was asked, no more than is owed and no more than the purse holds. The donor's last mover the
wrong way, the dark sacrifice it charges fifteen points for (`CastSpellInfo.cpp:2800-2809`), needs a follower to
give up (#8514), and will be told through the same deed entry. A
person says what the town makes of the party
once the party is worth an opinion: `MightAndMagic7Conversation` composes one line for every person the NPC
table describes, gated on a standing condition at the "Friendly" band's own floor — the same vocabulary every
shipped topic's conditions are judged in, so the line appears with the standing and is withheld with the
number it wants — and the words it says are the band's own reading. A town hall's notice is gated the same
way, as an ordinary offer condition on the errand nobody authors, so the board a hall posts and the line a
person speaks open together. The shipped data gates nothing on a standing and the ruleset suite counts it: the
operator's people carry 416 topics over 300 people — one per slot whose row the topic table labels, every one of
them answered by the global program's event of its number — and state no standing condition on any of them (the
requirement column it does carry is a quest bit, and the six rows that name one are rows the original never
gives text or an owner to), and none of the 17 shipped errands states an offer condition at all — so the
standing gates this game ships are its own two, and they are stated in one table. What a counter does about
standing is the donor's own arithmetic and nothing more: `MightAndMagic7Services.MerchantValue` is
`playerMerchant` (`PriceCalculator.cpp:139-158`) read in this game's sign, so every point of standing moves
what a counter charges and a party the world dislikes pays above the shelf; and a counter never refuses a
party for its standing, which is the negative this game states rather than inventing a rule the donor does
not have — the donor's counters gate on a membership, an hour, and a rung. What the party has accomplished is
read from the records it already carries, in the three families the donor keeps as award bits
(`AwardEnums.h:3-91`): a rank the ladder gave (`promotion:<rank>`, named by the class it made the character),
an errand the quest definitions state (`errand:<bit>`, named by the errand's own words and its giver's own
name), a counted deed the ladder keeps (`award:*`, named by the ladder's own label for it), and a guild's
membership, named by the counter that sold it — "Fire Guild membership" rather than the effect a save spells.
A record that is none of those — a ward still running, a passage bought, a line heard — is state rather than
an accomplishment, and the panel shows only what this game can name. **Two counted deeds the ladder asks for
are written by nobody**: `award:arena-wins` (five victories, the Champion's light errand) and
`award:bounties` (ten thousand gold of town-hall bounties, the Hunter's dark one) are stated as records a
rank requires, and neither the arena nor a bounty turn-in credits them — a bounty pays coin and leaves no
record, and a party record is set rather than added to, so a count is not something
the record path can express yet. The reading above names either record once something writes one (#8689:
the arena, and a counting record path in `PartyRecords`). Fame carries no bands, because the donor
gives it none: it prints fame as a bare number (`UIQuickReference.cpp:134-143`) and reads it in exactly one
place, whether somebody will join the party, gated on the party's fame exceeding their own and disabled in the
donor with a note that it is an MM8 behaviour (`UIDialogue.cpp:70-95`). Nothing can join a party in this build
— there is no follower owner — so that gate has nothing to guard yet and is routed to #8514 rather than
faked. Standing and accomplishments ride the party's own save section: reputation, fame, and the
party's effects are all durable state, and the ruleset suite turns an errand in, saves through the engine's
store, resumes, and reads the same band and the same record back.

This game's quests are landed beside them: `MightAndMagic7Quests` reads the operator's shipped quest
table — 512 rows of a bit, the journal's own words, and an authoring column — and states the 17 promotion
errands over it, taking each errand's giver from the ladder rather than stating it twice and leaving the
shipped words as the note a player reads. What each errand asks is this game's own reading, resolved against
the places and creatures the packs carry: a place the errand names is a reach objective, a creature it names
is a kill objective, and where its words say *all* of a kind the count is every one the place's own
placements hold. What the original performs with an event program — a weight moved, a code cracked, an altar
defaced — is stated as the errand's residue rather than faked, so a player reads what this build judges beside
what the original asked for. A finished errand leaves `errand:<bit>` on the party, which is what the topic
table's own gate and a rank's requirement both read. A pack may state an errand of its own with a `reading`
beside its words — giver, objectives, offer and completion conditions, and what a turn-in pays — and a town
hall's board is the one errand nobody authors: the beast is the place's own encounter row by the month the
clock stands in, what it pays is the donor's hundred times its level, and the keeper who offers it is the one
the counter's placement names. **Both composition paths hold the errand owner**: a session that plays the party its
scenario fixes and one that creates its party hand the same quest owner to the shell, because the
conversation composes an errand's offer from this game's quests on either path — a created party handed no
owner would hear an errand and have nowhere to take it. The wait, the offer, and the turn-in all travel
through the conversation that already exists, as three topics whose stage is the party's own state; a turn-in pays experience through the
progression owner's one award entry, coin through the ledger, items through the acquisition path, and records
onto the party's effects, and a counter refuses to buy what an unfinished errand still needs, naming the
errand. What each of the 17 errands is read as is stated row by row in `MightAndMagic7Quests.Errands()`,
with the shipped words it is written over, the place and creature names it resolves against content, and
the residue that says what the original performs with an event program and this build does not judge; a
test over the operator's own packs checks that every objective resolved and that a count taken from a
place's placements is that place's own. What a party discovers is landed beside the journal: the knowledge
owner keeps the facts it can look up again — a mixture the potion table states a discovery for, a find the
shipped table marks an artifact or a relic, a fountain's effect, an obelisk's clue, and a sign's words — and
`MightAndMagic7Knowledge` states the words a note about each kind reads with and what this game counts as
worth keeping.

**Fixtures run their map events.** The importer writes every clicked face group and decoration whose event no
other emitter answers for as a `fixture` placement, the event and the timers that keep what it gives as
`place-event` entries (the donor's own instruction and variable words, the text a step prints resolved from the
map's own string table), and the discovery table as `discovery` entries — 1,268 fixtures in 76 places over 653
events, and 186 notes, over the operator's install (`mm7import verify` checks each figure). An event a pressure
plate raises that moves the party is a `floor-trigger` placement (59), which `Describe` answers as trodden on
(`InteractionVerb.Tread`): the reticle never offers it, and the plates' `place-entrance` reaches raise it.
`MightAndMagic7Fixtures` walks an event's steps against the party and the fixture, writing into an overlay so a
later comparison reads an earlier write, and applies what it collected only when the run ends without a
refusal. **Interpreted**: `exit`, `jump`, `compare`, `add`, `subtract` and `set` over the variables below,
`status-text` and `show-message` (the line the party reads, or a person's answer), `for-party-member`, `random-go-to` (the engine's keyed draw), `receive-damage`, `check-season`
(the donor's season boundaries on this game's calendar), and the timer triggers, which end a use's run and are
themselves run as the refill of what a fixture reads; `change-door-state` (open, close, or toggle a door at rest,
`OpenEnroth/src/Engine/Graphics/Indoor.cpp:721-770`: the place's door placement of that id is recorded in the
door owner's own word under its own identity through the outcome's target changes, so a lever-opened door reads
open; a door id the place does not hold moves nothing, as the donor's lookup, and a place holding two doors under
one id is refused while the world is built (`interaction-door-number-reused`) rather than moving the first as the
donor's lookup would; the collision does not move, #8594,
stated as residue); `give-item` (the named item, or one the loot owner draws at the step's treasure level from the
kind it admits, through the acquisition path, `OpenEnroth/src/Engine/Tables/ItemTable.cpp:316-360`); `cast-spell`
(the spell's own roll at the step's rank and mastery landed on the chosen characters — the donor flies a
projectile from a point at the party, this build flies nothing: approximate); `speak-npc` (the conversation with
that person opens through the outcome, as using a person does); `check-skill` (a chosen character at the rank and
exactly the mastery, `OpenEnroth/src/Engine/Evt/EvtInterpreter.cpp:517-524`); `move-to-map` to another place ends
the run with the journey in the outcome (`InteractionOutcome.Travels`: the transition the step's `link` names, which
the place must issue or the use is refused as `fixture-travel-unknown`, as `walking` or `entrance`), and the world takes
it through `SessionWorld.Travel` after the use is recorded — the donor shows an entry picture and waits for a
confirmation before a move naming a house or a picture and runs on after one that does not
(`OpenEnroth/src/Engine/Evt/EvtInterpreter.cpp:207-252`); this build confirms nothing and runs nothing after the move
(approximate); `move-to-map` within the place sets the party down at the step's position (`InteractionOutcome.Relocates`)
and runs on, and one naming no position moves nobody (`:124-134`, `:231-235`: faithful); `move-npc` runs on and states as
residue that the person's move to another house is not followed, because people stand where content placed them
(a receiver is still to be routed: Castle Harmondale's door is the one shipped event that reaches it). **Presentation, passed over**: the
product draws no world and plays no sound, so `set-texture`, `set-sprite`, `play-sound`, `character-animation`,
`toggle-indoor-light`, `show-movie` (the three effects the donor's player hangs on a movie's name — the arbiter's
alignment and the crossing's week of rest, `OpenEnroth/src/Engine/Evt/EvtInterpreter.cpp:288-300` — are the
executable's and are not kept: this build's path is the class a promotion chooses), and a `set-faces-bit` that only hides a face group or makes it fluid change nothing and the
event's gameplay steps still run — a decision, not a deferral (a fluid face is not water a party drowns in, and its
footsteps, splash and sinking corpses are not modelled); a `set-faces-bit` that makes a face group passable states
its residue (#8594) and runs on. The variables, each through its owner: `quest-bit` (the
party record `errand:<bit>` the conversation already reads), `member-bit` (a party record — the donor's is per
character), `autonote` (a knowledge report of the discovery row: `stat` and `misc` an effect, `obelisk`,
`teacher` and `seer` a clue, `potion` a recipe), `gold`, `item` (given through the acquisition path,
taken from the shared pack), `hit-points`, `spell-points`, `full-hit-points` and `full-spell-points` (a member's
resources), `attribute` (a member's base attribute, for good), `resistance-bonus` (the running effect a ward
leaves, `fixture.bonus-hours` long), `attribute-bonus` and `armour-class-bonus` (the running effects a spell
raising the attribute and a stone skin leave, which the attribute's and the armour class's own sums read, for
`fixture.bonus-hours` — the donor keeps them until a rest), `resistance` (the member's stored base, for good),
`item-equipped` (whether a chosen character wears the item, `OpenEnroth/src/Engine/Objects/Character.cpp:3981-3982`),
`skill-points` and `experience` (a gift to the chosen character through `PartyProgression.Gift`), `bank-gold`
(compared with the party's one bank holding), `counter` (set to now and compared by hours since,
`OpenEnroth/src/Engine/Objects/Character.cpp:3934-3951` — the donor's ten are the party's, this build keeps them
per place as `counter:<n>`, which only one place's events use: approximate), `age` (the character's age offset
through `Rejuvenate` and `Age`), `major-condition` (a `set` clears every condition), `gold` `set` (a find or a
payment of the difference), `condition` (a member's conditions), and `map-variable` (one of the
place's 75 byte-sized counters, kept by the interaction ledger for the place as `map-variable:<slot>` — every
fixture of the place reads and writes the same ones, as the donor keeps one array per map, 
`OpenEnroth/src/Engine/Engine.h:62-65`). When each timer of a place last ran is kept beside them as
`timer:<event>.<step>`; both travel in the save's world section, a load judges each against the place's slots
and events (`save-kept-value-unknown`), and a place the clock restores forgets them, as the donor re-reads a
respawned map's delta with its variables (`OpenEnroth/src/Engine/Graphics/Indoor.cpp:313-319`). **Steps that reach
past the fixture**: `set-npc-topic` changes which topic table row one of a person's six slots raises
(`OpenEnroth/src/Engine/Evt/EvtInterpreter.cpp:449-468`) as a party record `topic-slot:<person>.<slot>:<row>`
(`MightAndMagic7TopicSlots`; a later change of the slot replaces it, row zero raises nothing), saved with the
party's records and judged on load (`save-record-unknown` for somebody the content lacks or a seventh slot); the
conversation reads it, withdrawing the row the slot stated (`topicSlots` on the person, the importer's positional
`npcdata.txt` columns) and offering the row it raises when the topic table (`person-topic`, every `npctopic.txt`
row with an answer or a global event of its number) carries one.
The donor keeps the slot on the person (ours: on the party, the same for a one-party game); its one side effect of
a particular change, a guild screen opening, is not kept. `is-actor-killed` counts the place's population
(`MightAndMagic7Fixtures.ActorsOf`, read only): by `group` (the placement's own group, which person placements now
carry), `kind` (the monster row), `creature` (the map's own actor record by index — the donor also numbers the
creatures its spawn points add, at load, which this build does not, so the people and the creatures its actor
records stand answer — over the operator's install the Temple of Baa's own leaving event asks for actor 34, its
named priest, and finds it) or `any`; with a count, at least that many down, without one every match down
(`OpenEnroth/src/Engine/Objects/Actor.cpp:2811-2834`). Down is this visit's population (a creature's health spent)
or a place the party cleared; a creature killed on an earlier visit of a place still holding others stands again,
because the population is rebuilt on entry (ours). `toggle-actor-group-flag` with the aggressor bit
(`OpenEnroth/src/Engine/Objects/ActorEnums.h:109`, the only bit the shipped events toggle) keeps
`hostile-group:<n>` = 1 (or 0 to clear it) in the place's values, and the fight reads it in the creature's — or a
person's — own nature as hostile at the longest band (`OpenEnroth/src/Engine/Objects/Actor.cpp:2155-2156`); a
charm or a binding still outranks it and an invisible party is still not noticed; it is saved with the place and forgotten when the clock restores
it, as the donor's map delta is; any other bit is refused by name. `history` `add`/`set` writes the history table's
line for the slot (`history-line`, which the importer reads from `history.txt` under the slot an event names,
with its `%30` and `%31`–`%34` codes written as `{date}` and `{member:1}`–`{member:4}`) into the journal once, as a
`Chronicle` line with the day written as this build writes days (the donor spells the month,
`OpenEnroth/src/GUI/GUIWindow.cpp:953-965`: approximate); a slot the table lacks is `fixture-history-unknown`.
**Refused by name** (`fixture-step-not-interpreted`, `fixture-variable-not-interpreted`), each naming the task that
would interpret it: `hireling` (#8514); every other instruction and variable is refused by name without one. Over
the operator's install a fresh party using each of the 707 fixture and floor-trigger events once has 705 run — 145 of
them taking it along a travel link, 54 setting it down elsewhere in its place — and 2 refused, both at `hireling`; the ruleset suite counts it and holds every refusal to name its receiver. On the first region the town well gives fifty points of fire resistance and its note, the wells east
and west of the temple five hit and spell points from thirty charges a day, the western well two points of luck
for good from eight a month, and the town sign is read; the first obelisk is the second region's (the first
region has none). **What is ours**: the active character a run starts on is the first member able to act,
because this build selects none (#8659); a write to something the party holds once — coin, a note, a bit — is
made once, where the donor makes it once per chosen character; a timer runs when a fixture that reads what it
keeps is used, every one of them on the fixture's first use (the donor's reading of an unvisited map), and a
daily timer runs a day after it last ran rather than at its hour; a fixture's harm is the record's own figure,
not reduced by resistance; and a sign's words are kept as a clue, which the original does not keep. The 20 of
the 195 map variables two or more fixture events of one place share — mostly an interior's lever puzzles — are
shared, because the variables are the place's. A fixture's own state word (`used`, `read`) is live-only like a
door's (#8593), which grows the same ledger capture.

**A person's topic runs the global program** (`global-event`, the importer's `global.evt`, every event of it, the
ones a topic raises marked `topic`). A topic is the row of one of a person's slots, labelled by the topic table, and
the donor answers it by running the global program's event of that number (`OpenEnroth/src/GUI/UI/NPCTopics.cpp:662-666`):
`MightAndMagic7Conversation` offers it when the event's own offer check allows it and answers it with a
`HandoffOwner.Use` handoff naming the topic, which the session routes to `SessionWorld.Answer` — one use of the
speaker's placement, described from the word (`InteractionTargetRequest.Raised`) and run by the same
`MightAndMagic7Fixtures` run a fixture's event takes (`Speak`), so a topic's quest bits, purse, items, notes,
history, slot changes and moves are settled by the owners every use settles through, and what its messages show
(the topic text table's row of each message's number, `EvtInterpreter.cpp:403-423`) is what the person says
(`PartyConversations.Hear`). A move of the global program is the world's own, issued from no place, and is taken
as `Scripted` travel along the world-issued transition (links 68, 69 and 70); the party leaving ends the
conversation. The offer check is the donor's offer mode run by the same run (`Offers`, `EvtInterpreter.cpp:152-181`,
`:632-655`): each comparison asks every member, `set-can-show-dialog-item` states the answer, and an event that
states none is offered; a comparison of a variable this game does not interpret withholds the topic and names it.
A regular run passes over the offer steps, as the donor's does. A run that meets a step this game does not
interpret settles nothing and the person still says what the run had said (the topic table's own text when it had
said nothing), with the refusal as the residue. Over the operator's install, a fresh party choosing each of the 365
topic-raised events once has 321 run — 2 of them taking the party along a world-issued link, the temples' — and 44
stop at a named step: `award` 20, `hireling` 11 (#8514), `set-npc-greeting` 3, `class` 2, `bank-gold` 2,
`npc-set-item` 2, `food` 1, `bounties` 1, `arena-wins-knight` 1, and a creature flag `0x10000` 1; the ruleset suite
counts it. **What is ours**: the donor offers at most four scripted topics at once (`NPCTopics.cpp:603`), this build
every one its check allows; a topic said once is withheld for the rest of the conversation, as every topic is.
Fidelity per system — what matches
the original, what is approximate, and what is deliberately ours — is fixed in
[`../../docs/gameplay-design.md`](../../docs/gameplay-design.md).
