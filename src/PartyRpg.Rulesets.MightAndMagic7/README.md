# PartyRpg.Rulesets.MightAndMagic7

Planned home of the compiled Might and Magic VII ruleset: the concrete policy
that turns `PartyRpg.Kit` mechanisms into that game. The owner-level contract is
in [`../../docs/code-organization.md`](../../docs/code-organization.md).

Owns, once implemented:

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
`MightAndMagic7Session`, which composes the kit's session shell with this game's identity. Party creation's
game definitions are landed too: `MightAndMagic7Creation.Options` offers the four races with their
attribute ranges, the eight portraits, and the nine base classes with the two skills each fixes, the nine
it offers, and the hit and spell points it starts with, over a pool of fifty points and two chosen skills
per character; `MightAndMagic7CreationTables` records which of those values the shipped data carries and
which are ours, with the manual citation and the donor transcription each authored value rests on; and
`MightAndMagic7Creation.Defaults` is the default party the flow applies through its own validation. The
class and skill definitions the choices name are checked against the loaded content, which refuses a
catalog that contradicts them. Everything else listed above attaches to the session as its stone lands.
Fidelity per system — what matches the original, what is approximate, and what is deliberately ours — is
fixed in [`../../docs/gameplay-design.md`](../../docs/gameplay-design.md).
