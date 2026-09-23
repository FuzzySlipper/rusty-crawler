# data

Small, checked-in reference tables that the product or the importer reads but
that a person maintains: identity and name mappings, sprite and sound name
tables, authored-asset manifests, and similar lookup data.

Boundary rules:

- If a value is adjustable game tuning, it belongs in a typed tuning profile
  under `content/`; if it is authored content, it belongs in a content pack;
  if it is an algorithmic invariant, it belongs beside the algorithm. `data/` is
  for reference tables none of those owners should carry.
- Nothing here is generated build output, and nothing here is original game data.

Only this README exists so far.
