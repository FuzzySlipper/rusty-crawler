# PartyRpg.Rulesets.MightAndMagic7

The compiled Might and Magic VII ruleset: the concrete policy
that turns `PartyRpg.Kit` mechanisms into that game. The owner-level contract is
in [`../../docs/code-organization.md`](../../docs/code-organization.md).

Owns:

- Classes, races, ranks, and the two-stage promotion ladder whose second step
  splits each class into a light and a dark alternative.
- Skills with their class- and rank-specific mastery ceilings, and skill points.
- The nine spell schools, spell tiers, costs, and effects.
- Monster, item, service, and condition definitions and their interpretation.
- Combat, damage, resistance, recovery, reward, and experience formulas.
- Time, calendar, rest, fatigue, and travel policy, including service hours.
- Quest, guild, reputation, and journal policy.
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

Implemented today: `MightAndMagic7Ruleset` (the compiled ruleset and its identity) and
`MightAndMagic7Session`, which composes the kit's session shell with this game's identity: this game's
world and movement policy (`MightAndMagic7World`, `MightAndMagic7Movement` — the party's body and walk
speed, cited from the donor, and the engine's own controller tuning scaled to that body), the one clock
(`MightAndMagic7Time` — the authored calendar, the donor's starting moment, its thirty-to-one rate, and
the hours it calls daylight), the party its content declares as scenario state (`MightAndMagic7Party`,
through the same factory creation hands a party to, never a party of its own invention, and what a host
that declared no creation screen plays), the larder's
policy (`MightAndMagic7Provisions` — one ration a day, and the weak condition a larder left short puts
on every member), and what a crossing costs (`MightAndMagic7TravelCostRule` — a day on the road and the
rations it eats, with paid and magical travel refused by name until the services that sell a fare and
the magic that opens a portal exist), and what using something means here (`MightAndMagic7Interaction` —
a door from the delta's own stored state with the donor's interaction range, a decoration that raises an
event as a fixture whose use names the event nothing executes yet, a `requires` array on a placement as
this game's locks, and a refusal that says what it needs), which places are clocked
(`MightAndMagic7Schedules` — the counters' own hours, or the hours a place states in its own entry, read
against the one clock so a door outside them is an unmet requirement rather than a menu entry that hides
itself), what fighting costs here (`MightAndMagic7Combat` — a monster's recovery is the monster table's own
`Recovery` column, its hostility band is the distance at which it notices the party, and a character is
paced by the donor's own attack-recovery sum as far as this build can read it: the base for a character
holding nothing, the armsmaster reduction, and the speed bonus, since a party cannot wear anything yet; a
creature's first recovery is a keyed draw over the actor so a group placed together does not strike in
lockstep, and a creature is recognized by a placement of kind `monster` naming the row it is — the
interface the monsters-and-AI task fills), and what stopping costs here (`MightAndMagic7Rest` — eight hours under a roof or in the open, the
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
with, which then travels into the party and into a save exactly as a chosen one does. Everything else
listed above — the remaining class, skill, spell, monster, item, service, and formula policy, and rest and
fatigue and service hours — attaches to the session as its stone lands. Fidelity per system — what matches
the original, what is approximate, and what is deliberately ours — is fixed in
[`../../docs/gameplay-design.md`](../../docs/gameplay-design.md).
