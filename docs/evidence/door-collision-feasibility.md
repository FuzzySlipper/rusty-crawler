# Door collision feasibility

This reading was taken on 2 October 2026 while preparing the moving-collision
residue. It establishes a supported construction path and an import constraint;
it does not establish that the product moves doors or that a doorway was walked
through.

The operator's install decoded as 76 maps. Across its 63 interiors, the survey
found 786 in-use doors. Every door's stored base offsets matched its decoded
vertices, no moved vertex belonged to two doors, and every face containing a
moved vertex was included in that door's face list.

However, 6,636 door/face associations included a face with some vertices outside
that door's moved set. These are associations, not a count of unique faces:
one face can contain vertices moved by different doors. Translating a whole face
or admitting one rigid mesh per door would move vertices the door does not own.
The import must preserve the moved-vertex membership and base offsets. The donor
updates only the door's named vertices from their base offsets and direction
times displacement (`OpenEnroth src/Engine/Graphics/Indoor.cpp:665-671`), then
updates the affected faces. It is behavior evidence, not code to translate.

The installed safe Engine API supports `ReplaceCollision` with explicitly
identified mesh assets and instances, `ApplyCollisionResidency` for later
upserts/removals, and `ReplaceCollisionNavigation` over the same session's current
collision. The Engine fixture in
`scripts/fixtures/CharacterMeshChecks.cs:320-358` demonstrates the safe residency
calls. Native ownership is in `rust/crates/csharp-engine-services/src/spatial.rs`
and `rust/crates/svc-collision/src/static_mesh.rs`; the product does not implement
collision, triangulation at runtime, or navigation derivation.

An exact-package Engine test-host check admitted a one-mesh content artifact,
added an independent mesh, derived two navigation cells, and removed the added
mesh. The counts were respectively one asset/instance, two assets/instances, and
one asset/instance. The retained base collision survived removal. Collision
changes require explicit navigation publication afterward.

There is a constraint on mixing that path with a retained content artifact. The
Engine privately derives the artifact's mesh asset and instance identity from
its digest (`spatial.rs:975-995`, `spatial_content_asset_id`). Its public artifact
readout supplies provenance, hashes and counts, without an identity allocator or
enumeration. A caller cannot guarantee an independent mesh ID by guessing a
range, and copying the Engine's private hash would duplicate an Engine mechanism.

The supported implementation path therefore starts by replacing the **complete**
collision partition with product-owned IDs: the static base and all mutable face
parts. Imported membership and per-vertex travel determine the geometry the
ruleset supplies. Engine validates and collides with that geometry, then derives
navigation in the party's existing scene. The content catalog remains the owner
of its import provenance. Collision residency and derived navigation clear the
Engine artifact identity; subsequent evidence must not describe the changed
scene as the original unchanged artifact.

The local survey report and package-check receipts are retained with the Den
task record. No operator game geometry is published here. Implementation,
per-place emitted partition counts, save/re-entry interoperability, focused
checks, and an imported doorway walk remain to be demonstrated.


## Implementation and native admission follow-up

The implementation now emits `collisionLayout`: a complete static base and mutable face fans with
per-corner door placement identity, rest and normalized full travel. Indoor face groups come from the
face extra's cog number; outdoor groups come from the face record's cog field at 0x122. All non-portal
addressable faces are retained, including ethereal faces that an event can make solid again. Bounds
include both door endpoints. Two operator writes were byte-identical.

Across the operator's 76 geometries the emitted partition identifies all 786 doors and 16,502 mutable
faces. The actual safe Engine admitted every partition at its initial state and again after every door
was marked open: 152 admissions, each with collision and nonzero derived navigation, no refusal or
omission. This does not certify every doorway's tuning or original event semantics.

Focused native checks also walked the actual ruleset body against an authored door: closed blocked,
open admitted passage, and closing blocked the return. A face group made passable removed its solidity,
clearing that bit restored it, and canonical ledger save/load and place restoration reproduced the
geometry. Partial-corner projection and unrelated-use caching have separate semantic checks.

Player words now call rest closed and full travel open; the source's state 2 starts open and other
positions settle closed without animation. The donor acknowledges its inverted enum naming at
OpenEnroth `src/Engine/Graphics/FaceEnums.h:62-66`. This is an explicit approximate endpoint policy.
The supported Engine calls remain whole `ReplaceCollision` and `ReplaceCollisionNavigation` in the
party's existing session; no artifact-ID mixing, runtime polygon triangulation or second collision
owner is introduced. Imported live doorway traversal and final source/review gates remain pending.
