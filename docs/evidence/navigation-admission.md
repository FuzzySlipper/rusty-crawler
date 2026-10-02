# Collision-derived navigation admission

Point-in-time source and real Engine check on 2026-10-02. The importer wrote the
operator's own installation twice with identical bytes. Every emitted geometry
carried its collision vertex bounds as `navigationRegion`, beside the unchanged
Engine artifact, retaining the map file and geometry source counts.

| Imported geometry | With request metadata | With Engine-derived cells |
| --- | --- | --- |
| Interiors, sampled at 128 units | 63 | 63 |
| Regions, sampled at 512 units | 13 | 13 |
| Total | 76 | 76 |

No geometry was missing or refused. The real Engine published 699,840 walkable
cells in total. A prior 65,536-column budget excluded The Pit, The Walls of Mist
and The Titans' Stronghold. Their measured footprints were 83,600, 109,140 and
72,588 columns. The ruleset's separate derivation budget is now 131,072; the
final run had no navigation omission or refusal. The per-query pursuit budget
remains 1,024 cells. A future derivation budget refusal retains collision and
reports why pursuit holds; focused checks exercise that recoverable path.

`EnginePartyMover.Enter` admitted content, replaced the place's collision and
called `ReplaceCollisionNavigation` in that same spatial session. It supplied
the canonical ruleset controller: its body, slope and exact step, with maximum
drop from that step and support snaps scaled independently from the Engine
defaults. The final whole-install check took about 56.1 seconds of aggregate
admission time; the slowest entry, Harmondale, took about 6.1 seconds. These
include content and collision admission and are not isolated navigation timings
or a benchmark. Barrow X published 1,450 cells in about 128 milliseconds.

The headless check used the package's safe `EngineTestHost` through ordinary
test-project support. An authored floor with an impassable wall additionally
exercised the canonical party and creature movers with the actual ruleset body:
the creature went past the wall end and reached the target, and the party still
stepped in the same collision scene. This is a mechanism check, not an imported
interior live pursuit reading. Both real Engine checks passed.

Queries take feet, while character steps retain body centres. A refused route
holds by name and can be retried after the target moves. Initial placement still
uses a stationary canonical character step and the existing ray-and-settle
path, including when navigation is absent. Backing away retains its existing
character-step behavior. No product rasterizer, pathfinder or second spatial
scene was introduced.

Sampling is ours and approximate, especially the 512-unit outdoor grid. These
counts do not certify every narrow passage, every pursuit, ordinary traversal,
NativeAOT or the pending imported-interior browser reading. The operator's local
gate evidence retains the per-place admission report and original logs; no game
data is published here.
