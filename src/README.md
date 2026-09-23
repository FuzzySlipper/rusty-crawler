# src

Planned product graph for Rusty Crawler. No C# project is checked in yet: this
pass created the repository shape and left the project graph to the planning
work, so the directories below state ownership rather than implemented
behavior.

| Directory | Planned project | Owns |
| --- | --- | --- |
| `PartyRpg.Kit/` | `PartyRpg.Kit` | Reusable party-centric, first-person RPG mechanisms: party roster and members, shared inventory and currency, skill and spell catalogs, casting workflows, combat execution (real-time and turn-based modes), monster/AI coordination, NPC interaction, quest/journal state, world and spatial session stepping, service/shop workflows, structured UI values, typed IDs, compiled ruleset/session contracts, bundle and tuning resolution. It is not a universal RPG framework and must not mention Might and Magic vocabulary. |
| `PartyRpg.Rulesets.MightAndMagic/` | `PartyRpg.Rulesets.MightAndMagic` | The compiled Might and Magic VI/VII/VIII ruleset: classes, races, skills and mastery, spell schools and formulas, monster and item definitions, combat and reward formulas, time and calendar policy, service and training policy, content interpretation, presentation meaning, save meaning, per-game (MM6/MM7/MM8) profiles, and session composition. |
| `PartyRpg.Host/` | `PartyRpg.Host` | Product lifecycle, the explicit built-in ruleset and bundle selection, default selection, and the one ordinary product entry. It may select Might and Magic; it never interprets Might and Magic rules or source files. |
| `MightAndMagic.Import/` | `MightAndMagic.Import` | Offline knowledge of the original games' data files and of the donor projects that document them: source formats, conversion quirks, provenance, and differential validation against the donor reimplementation. It is not a runtime dependency. |
| `MightAndMagic.Import.Tool/` | `MightAndMagic.Import.Tool` | The operator-facing command line that drives `MightAndMagic.Import` and writes import output. |
| `ui/` | product DOM companion (TypeScript) | Thin DOM presentation of Engine-delivered projections and semantic actions. It owns neither gameplay state nor game-world rendering. |

Runtime code consumes normalized content packs produced by the importer; it
does not read source-shaped game data.
