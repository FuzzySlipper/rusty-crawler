# PartyRpg.Rulesets.MightAndMagic

Planned home of the compiled Might and Magic VI/VII/VIII ruleset: the concrete
policy that turns `PartyRpg.Kit` mechanisms into those three games.

Owns, once implemented:

- Classes, races, skills, skill mastery tiers, promotions, and trainers.
- Spell schools, spell formulas, casting costs and effects.
- Monster, item, artifact, and shop definitions and their interpretation.
- Combat, damage, resistance, reward, and experience formulas.
- Time, calendar, rest, and travel policy, including service opening hours.
- Quest, guild, reputation, and journal policy.
- Content interpretation and presentation meaning: what an imported region,
  map, sprite, or sound means to this ruleset.
- Per-game profiles and the differences MM6, MM7 and MM8 explicitly carry
  (party composition, skill and spell availability, promotion paths, world
  layout, add-on content).
- Session composition: assembling the kit's named services with Might and Magic
  policy, and Might and Magic save meaning.

Boundary rules:

- Might and Magic vocabulary is legal here, in Might and Magic content packs,
  in Might and Magic presentation, and in `MightAndMagic.Import`. It is illegal
  in `PartyRpg.Kit`.
- This is a compiled ruleset. Adding code-bearing semantics requires a product
  rebuild; changing valid content or tuning does not. No runtime assembly
  loading, reflection discovery, or ambient service lookup.

Nothing is implemented yet. The per-game split and the exact ownership seam
between kit and ruleset are planning decisions recorded in `AGENTS.md` and the
coverage plan when that plan exists.
