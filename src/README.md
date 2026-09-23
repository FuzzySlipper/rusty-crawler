# src

Planned product graph for Rusty Crawler. No C# project is checked in yet: this
pass created the repository shape and fixed the design, so the directories below
state ownership rather than implemented behavior. The owner-level contract is in
[`../docs/code-organization.md`](../docs/code-organization.md).

| Directory | Planned project | Owns |
| --- | --- | --- |
| `PartyRpg.Kit/` | `PartyRpg.Kit` | Reusable party-centric, first-person RPG mechanisms: party roster and members, shared inventory and currency, skill and spell catalogs, casting workflows, combat execution (real-time and turn-based pacing over one combat state), monster/AI coordination, NPC interaction, quest/journal state, world and spatial session stepping, service workflows, structured UI values, typed IDs, compiled ruleset/session contracts, bundle and tuning resolution. It is not a universal RPG framework and must not mention Might and Magic vocabulary. |
| `PartyRpg.Rulesets.MightAndMagic7/` | `PartyRpg.Rulesets.MightAndMagic7` | The compiled Might and Magic VII ruleset: classes and ranks, races, skills and mastery ceilings, the nine spell schools and their formulas, monster and item definitions, combat and reward formulas, time and calendar policy, service and training policy, quest and guild policy, promotion and light/dark path policy, content interpretation, presentation meaning, save meaning, and session composition. |
| `PartyRpg.Host/` | `PartyRpg.Host` | Product lifecycle, the explicit built-in ruleset and bundle selection, default selection, and the one ordinary product entry. It may select Might and Magic; it never interprets Might and Magic rules or source files. |
| `MightAndMagic7.Import/` | `MightAndMagic7.Import` | Offline knowledge of the original game's data files and of the donor projects that document them: source formats, conversion quirks, provenance, normalization into packs, and differential validation against the donor reimplementation. It is not a runtime dependency. |
| `MightAndMagic7.Import.Tool/` | `MightAndMagic7.Import.Tool` | The operator-facing command line that drives `MightAndMagic7.Import` and writes import output. |
| `ui/` | product DOM companion (TypeScript) | Thin DOM presentation of Engine-delivered projections and semantic actions. It owns neither gameplay state nor game-world rendering. |

Runtime code consumes normalized content packs produced by the importer; it
does not read source-shaped game data.
