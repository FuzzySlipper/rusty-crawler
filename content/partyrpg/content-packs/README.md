# Authored content packs

Each pack is a directory with a `pack.json` manifest and the documents the manifest declares. A pack
is one of four kinds — definitions, tuning, scenario, or world — and the loader validates the whole
catalog when the product starts: pack ids must match their directories, entry ids must be unique
across all packs of the same definition kind, and every declared reference must resolve.

Authored packs are committed. Packs produced by the importer are not: they land in
[`../imports`](../imports) and are regenerated from the operator's own game data.

A new pack must be added to a bundle in [`../bundles`](../bundles), or nothing loads it.
