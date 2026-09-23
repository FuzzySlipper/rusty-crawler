# content

Loaded content for the product. Content is data: changing valid content does not
require a product rebuild, and content never carries code.

Planned layout:

| Path | Holds |
| --- | --- |
| `partyrpg/bundles/` | Game-bundle declarations: which ruleset, content packs, and tuning profiles a launchable product selects. |
| `partyrpg/content-packs/` | Authored definitions and scenario state interpreted by a ruleset: party archetypes, classes and skills, spells, monsters, items, services, quests, encounters, maps and placements. |
| `partyrpg/imports/<region>/` | Imported content produced offline from an operator-supplied game installation: geometry or terrain, spatial data, media, tables, resources, and the provenance that says where each artifact came from. Imports are generated; their sources stay outside the repository. |

Boundary rules:

- Original Might and Magic data is operator-supplied. Never commit it. Preserve
  attribution, licensing, and provenance for anything checked in.
- Runtime code consumes normalized packs, not source-shaped game data. Format
  knowledge belongs in `src/MightAndMagic.Import/`.
- Imported and authored content stay separate: regeneration must not overwrite
  authored files.

Only this README exists so far; no content is checked in.
