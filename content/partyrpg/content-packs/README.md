# Authored content packs

The loader's authored root (`ContentLayout` names it beside `imports/` and `bundles/`). No authored pack is
committed today: everything the product plays is imported from the operator's own data into
[`../imports`](../imports), and a live check's hand-written scenario is staged there too
([`../../../docs/live-checks.md`](../../../docs/live-checks.md)). A pack authored for the repository — a
tuning profile, a scenario, definitions that are ours rather than the original's — belongs here and is
committed.

Each pack is a directory with a `pack.json` manifest and the documents the manifest declares. A pack is one
of four kinds — definitions, tuning, scenario, or world — and the loader validates the whole catalog when the
product starts: pack ids must match their directories, entry ids must be unique across all packs of the same
definition kind, and every declared reference must resolve.

A new pack must be named by a bundle in [`../bundles`](../bundles), or nothing loads it.
