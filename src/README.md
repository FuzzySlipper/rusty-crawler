# src

The product graph for Rusty Crawler. What each project implements is in its own README; the owner-level
contract is in [`../docs/code-organization.md`](../docs/code-organization.md), and the open residue is in
[`../AGENTS.md`](../AGENTS.md).

| Directory | Project | Owns |
| --- | --- | --- |
| `PartyRpg.Kit/` | `PartyRpg.Kit` | Reusable party-centric, first-person RPG mechanisms: the party entity and the components attached to it (roster and members, one shared inventory of item instances, per-member equipment, purse, food, reputation and fame, followers, party-wide effects), skill and spell catalogs, casting workflows, combat execution (real-time and turn-based pacing over one combat state), monster/AI coordination, NPC interaction, quest/journal state, world and spatial session stepping, the one game clock and calendar every duration and schedule is stated against, the character-creation flow over the choices and budgets a ruleset supplies, service workflows, structured UI values, typed IDs, compiled ruleset/session contracts, bundle and tuning resolution. It is not a universal RPG framework and must not mention Might and Magic vocabulary. |
| `PartyRpg.Rulesets.MightAndMagic7/` | `PartyRpg.Rulesets.MightAndMagic7` | The compiled Might and Magic VII ruleset: classes and ranks, races, skills and mastery ceilings, the nine spell schools and their formulas, monster and item definitions, item enchantment/coating and follower hiring/salary policy, combat and reward formulas, time and calendar policy, service and training policy, bound arena bouts through existing quest/population/fight owners, quest and guild policy, promotion and light/dark path policy, content interpretation, presentation meaning, save meaning, and session composition. |
| `PartyRpg.Host/` | `PartyRpg.Host` | Product lifecycle, the explicit built-in ruleset and bundle selection, default selection, and the one ordinary product entry. It may select Might and Magic; it never interprets Might and Magic rules or source files. |
| `MightAndMagic7.Import/` | `MightAndMagic7.Import` | Offline knowledge of the original game's data files and of the donor projects that document them: source formats, conversion quirks, provenance, normalization into packs, and differential validation against the donor reimplementation. It is not a runtime dependency. **Implemented:** the container reader (every entry of all five archives decodes), the rule-table readers with a text inventory and the rule families the data does not carry, the event-program reader and the place graph, the map decoders (all 76 maps, byte-exact walks), and the media extractor (images, palettes, nested PCX, sprites, sound, with a provenance manifest). |
| `MightAndMagic7.Import.Tool/` | `MightAndMagic7.Import.Tool` | The operator-facing command line that drives `MightAndMagic7.Import` and writes import output. |
| `ui/` | product DOM companion (TypeScript) | Thin DOM presentation of Engine-delivered projections and semantic actions. It owns neither gameplay state nor game-world rendering. |

Runtime code consumes normalized content packs produced by the importer; it
does not read source-shaped game data.

Item properties, hardening and charge capacity live on Kit item instances; the ruleset supplies their
meaning through `MightAndMagic7ItemMagic`. Combat plans carry independently resisted damage parts and
explicit hit observers through the existing resolution; the UI prints its resulting sentence.
