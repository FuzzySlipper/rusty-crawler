# content

Loaded content for the product. Content is data: changing valid content does not
require a product rebuild, and content never carries code. The four content kinds
and the rule that regeneration never overwrites authored work are fixed in
[`../docs/code-organization.md`](../docs/code-organization.md).

Layout:

| Path | Holds |
| --- | --- |
| `partyrpg/bundles/` | Game-bundle declarations: which ruleset, content packs, and tuning profiles a launchable product selects. The shipped `partyrpg-default` selects none until the operator names imported packs. |
| `partyrpg/content-packs/` | The authored root: **definitions**, **tuning** profiles, and **scenario** state that are ours rather than imported. None is committed today. |
| `partyrpg/imports/<pack>/` | Packs `mm7import write` produces offline from an operator-supplied installation: tables, places and their collision artifacts, placements, encounters, maps, and the provenance that records game, build, source file, and transformation. Generated and never committed. |

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

What is checked in is this README, the two directory READMEs, and the default bundle; every pack the
product plays is generated from the operator's own data.
