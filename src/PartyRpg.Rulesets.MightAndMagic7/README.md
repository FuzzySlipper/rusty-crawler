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
  the places and the population the world holds. What each spell does inside its category is its row in
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
    roll of the kit's states one kind of die (ours). Not read: a slaying enchantment's double damage (#8513) and
    a master dagger's chance of triple damage, which one roll of the kit's cannot state.
  - **resistances** (`Character.cpp:1900-1993`): a grand master of leather in working leather armour adds the
    leather level to fire, air, water and earth, beside the wards spells leave. Faithful for those terms; the
    character's own base and racial terms are not stated by this game's members, followers are #8514, and
    enchantments #8513.
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
    spell holds still is not down: it can be struck and the place still holds it.
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
  as a record under the character and the score (`Character.cpp:3282-3295`). A creature's ageing touch is a special
  attack this build does not apply yet, with breaking and stealing an item (see `ConditionOf`).
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
  temporary bonus lasts (`fixture.bonus-hours`, a day — ours: the donor keeps it until the next rest) — each with its
  default, range, and meaning. A rule reads them through the `TuningProfile` it
  composes from the selected catalog, so a bundle's tuning pack changes play
  without a rebuild and a value out of range stops composition by name.

Implemented today: `MightAndMagic7Ruleset` (the compiled ruleset and its identity) and
`MightAndMagic7Session`, which composes the kit's session shell with this game's identity: this game's
world and movement policy (`MightAndMagic7World`, `MightAndMagic7Movement` — the party's body and walk
speed, cited from the donor, and the engine's own controller tuning scaled to that body), the one clock
(`MightAndMagic7Time` — the authored calendar, the donor's starting moment, its thirty-to-one rate, and
the hours it calls daylight), the party its content declares as scenario state (`MightAndMagic7Party`,
through the same factory creation hands a party to, never a party of its own invention, and what a host
that declared no creation screen plays — one party per selection, refused by name with every candidate
when the selection states two, exactly as the scenario's starting place is), the larder's
policy (`MightAndMagic7Provisions` — one ration a day, and the weak condition a larder left short puts
on every member), and what a crossing costs (`MightAndMagic7TravelCostRule` — a day on the road and the
rations it eats; a fare is honoured by the passage the party bought, and a portal — a crossing the caster
issues rather than a place — is free of road time because the spell already paid for it), how long a journey a
counter sells takes (`MightAndMagic7FareDays` — a stable sells on the `coach` route and a dock on the `boat`
route, and the days are the tuned length of that network, the one answer the counter quotes and the road charges
the clock; the ticket the party holds and a save carries names the route and not the days, so a retune never
strands a passage), where those counters sell passages to (`MightAndMagic7FareNetwork`, an `IFareNetwork` — over
the counters content places, every stable reaches every other place that keeps a stable and every dock every other
port, one crossing per pair of stops landing at the destination's own start; the counter's offers and the world
graph's sold crossings are both read from it, through `MightAndMagic7World.Graph`, and content that authors a sold
crossing of its own is refused by name; ours, simpler than the donor's thirty-five scheduled routes), which creatures a level's spawn records put on the field
(`MightAndMagic7Spawns` — the importer writes each actor record as an `encounter` placement and chooses nothing;
this game draws a random slot's count from its range and each creature's grade from the donor's odds for the
slot's difficulty, through the engine's keyed random service under the place and the spawn index, when the
population reads the place's placements, so every visit and every load see the same creatures, and an errand
that counts every one of a kind in a place counts that same resolution), and what using
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
(`MightAndMagic7Corpses` — the fight reports what it read as down and this game generates each death's
loot once, under a key that names the place, the creature, and which death it was, and holds it on the
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
creatures near, an interrupted night that lasts only the hours it lasted, and the day-long debt of sleep
that weakens the party on the clock's own deadline). Party creation's game definitions are landed too:
`MightAndMagic7Creation.Options` offers the four races with their attribute ranges, the eight portraits,
and the nine base classes with the two skills each fixes, the nine it offers, and the hit and spell
points it starts with, over a pool of fifty points and two chosen skills per character;
`MightAndMagic7CreationTables` records which of those values the shipped data carries and
which are ours, with the manual citation and the donor transcription each authored value rests on; and
`MightAndMagic7Creation.Defaults` is the default party the flow applies through its own validation, and a
host that declares a creation screen gets that flow as the session's creation mode — the party a player
accepts is built through the same factory and the world is composed over it — so a new game is created
while a host without a creation screen plays the party its scenario fixes. The
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
every other deed does. The fine beside it is the donor's sum, `100 × (base + the row's level + the party's
reputation in the donor's sign)` clamped to `0..4,000,000`, read before the deed moves the standing as the
donor's order has it, and approximated three ways that are ours: the map table's per-place base fine is read
as zero (the importer does not carry that column; zero is Emerald Isle's own value), the fine is taken from
the purse through the party's ledger at the moment of the death — as much as the purse holds — where the donor
carries it as a debt a town hall collects (`TownHall.cpp:30-45`), and the donor's light-and-dark exemptions
(`Actor.cpp:1087-1091`) are not read. The donor's other two movers have no owner here: being caught stealing
(`Shops.cpp:1147-1174`) needs a stealing act, which nothing offers although the Stealing skill can be
learned (#9025, with the carried fine and the town hall that collects it), and the dark sacrifice the
donor charges fifteen points for (`CastSpellInfo.cpp:2800-2809`) needs a follower to give up (#8514). A
person says what the town makes of the party
once the party is worth an opinion: `MightAndMagic7Conversation` composes one line for every person the NPC
table describes, gated on a standing condition at the "Friendly" band's own floor — the same vocabulary every
shipped topic's conditions are judged in, so the line appears with the standing and is withheld with the
number it wants — and the words it says are the band's own reading. A town hall's notice is gated the same
way, as an ordinary offer condition on the errand nobody authors, so the board a hall posts and the line a
person speaks open together. The shipped data gates nothing on a standing and the ruleset suite counts it: the
operator's topic table carries 54 topics over 15 people and states no standing condition on any of them (the
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
map's own string table), and the discovery table as `discovery` entries — 1,097 fixtures in 66 places over 495
events, and 186 notes, over the operator's install (`mm7import verify` checks each figure).
`MightAndMagic7Fixtures` walks an event's steps against the party and the fixture, writing into an overlay so a
later comparison reads an earlier write, and applies what it collected only when the run ends without a
refusal. **Interpreted**: `exit`, `jump`, `compare`, `add`, `subtract` and `set` over the variables below,
`status-text`, `for-party-member`, `random-go-to` (the engine's keyed draw), `receive-damage`, `check-season`
(the donor's season boundaries on this game's calendar), and the timer triggers, which end a use's run and are
themselves run as the refill of what a fixture reads. The variables, each through its owner: `quest-bit` (the
party record `errand:<bit>` the conversation already reads), `member-bit` (a party record — the donor's is per
character), `autonote` (a knowledge report of the discovery row: `stat` and `misc` an effect, `obelisk`,
`teacher` and `seer` a clue, `potion` a recipe), `gold`, `item` (given through the acquisition path,
taken from the shared pack), `hit-points`, `spell-points`, `full-hit-points` and `full-spell-points` (a member's
resources), `attribute` (a member's base attribute, for good), `resistance-bonus` (the running effect a ward
leaves, `fixture.bonus-hours` long), `condition` (a member's conditions), and `map-variable` (the fixture's
own counter). **Refused by name** (`fixture-step-not-interpreted`, `fixture-variable-not-interpreted`): every
other instruction and variable. Over the operator's install a fresh party using each of the 495 fixture events
once has 327 run and 168 refused, by the first step each run reaches that this game does not interpret:
`change-door-state` 75 (a lever moving a door, #8594), `set-texture` 21, `give-item` 14, `set-sprite` 7,
`bank-gold` 6, `cast-spell` 6, `play-sound` 5, `speak-npc` 5, `set-faces-bit` 4, `attribute-bonus` 9 (might 3,
personality 4, accuracy 1, endurance 1), `toggle-actor-group-flag` 3, `resistance` 3 (fire, mind, water — a
permanent resistance, which no owner keeps), `character-animation` 2, `hireling` 2, and `armour-class-bonus`,
`counter`, `gold` (a `set`), `set-npc-topic`, `skill-points` and `toggle-indoor-light` 1 each; the ruleset suite
counts it. On the first region the town well gives fifty points of fire resistance and its note, the wells east
and west of the temple five hit and spell points from thirty charges a day, the western well two points of luck
for good from eight a month, and the town sign is read; the first obelisk is the second region's (the first
region has none). **What is ours**: the active character a run starts on is the first member able to act,
because this build selects none (#8659); a write to something the party holds once — coin, a note, a bit — is
made once, where the donor makes it once per chosen character; a timer runs when a fixture that reads what it
keeps is used, every one of them on the fixture's first use (the donor's reading of an unvisited map), and a
daily timer runs a day after it last ran rather than at its hour; a fixture's harm is the record's own figure,
not reduced by resistance; a sign's words are kept as a clue, which the original does not keep; and a
fixture's map variables and timer times are its own state word in the interaction ledger, so the 20 of the 195
map variables two or more fixture events of one place share — mostly an interior's lever puzzles — are kept once
per fixture rather than once per place, and like a door's state they are not saved yet (#8593). Fidelity per system — what matches
the original, what is approximate, and what is deliberately ours — is fixed in
[`../../docs/gameplay-design.md`](../../docs/gameplay-design.md).
