# content

Loaded content for the product. Content is data: changing valid content does not
require a product rebuild, and content never carries code. The four content kinds
and the rule that regeneration never overwrites authored work are fixed in
[`../docs/code-organization.md`](../docs/code-organization.md).

Planned layout:

| Path | Holds |
| --- | --- |
| `partyrpg/bundles/` | Game-bundle declarations: which ruleset, content packs, and tuning profiles a launchable product selects. |
| `partyrpg/content-packs/` | Authored **definitions** (classes, races, skills, spells, monsters, items, services, quests, conditions, places), **tuning** profiles, and **scenario** state. |
| `partyrpg/imports/<place>/` | Imported world content produced offline from an operator-supplied installation: geometry, spatial data, media, normalized tables, and the provenance that records game, build, source file, and transformation. Imports are generated; their sources stay outside the repository. |

Boundary rules:

- Original Might and Magic data is operator-supplied. Never commit it, and never
  copy converted game data out of a donor. Preserve attribution, licensing, and
  provenance for anything checked in.
- Runtime code consumes normalized packs, not source-shaped game data. Format
  knowledge belongs in `src/MightAndMagic7.Import/`.
- Imported and authored content stay separate: regeneration must not overwrite
  authored files.
- Definitions are data, not code: if content seems to need behavior, the
  behavior belongs in the ruleset and the content should carry the values.

Only this README exists so far; no content is checked in.
