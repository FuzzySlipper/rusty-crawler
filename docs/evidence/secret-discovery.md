# Imported secret surfaces and Perception

Recorded on 2 October 2026. The importer reads the secret face bit and the map's Perception difficulty,
then writes `secret`, `perceptionDifficulty` and source `secretFaces` only on affected door and fixture
placements. Door faces come from the door's own face list; a fixture uses only its own clustered faces.
The operator's imported data produced 95 secret doors and 12 secret fixtures. The Manor's door 0 carries
secret face 914 and difficulty 10; these are imported facts, not a synthetic skill requirement.

Donor references: OpenEnroth `src/Engine/Graphics/FaceEnums.h:9` identifies `FACE_IsSecret` as bit 2;
`src/Engine/Tables/MapTable.cpp:70` reads difficulty from column 5;
`src/Engine/Party.cpp:168-177` takes the best acting member against twice that difficulty;
`src/Engine/Objects/Character.cpp:599-610` reads level times 1, 2 or 3, with grand master at 10,000.
`src/Engine/Engine.cpp:135` and `src/Engine/Graphics/Renderer/OpenGLRenderer.cpp:2514,2978` use that
result to highlight secret faces. This product has no world renderer: its deliberate approximation is
an explicit first ordinary Use that discovers the surface, followed by a second Use through the unchanged
door or map-event workflow. The failure names the required Perception and best acting reading.

Discovery neither opens the door nor runs its event. It records one named value in the existing world
interaction ledger and one clue in party knowledge. Save/load carries both through their existing owners;
the current save validator rejects a discovery of an absent/non-secret target or a value other than one.
The world memory follows the place's ordinary reset policy; the historical knowledge note is kept by
its own owner. Locks, hours, traps, Engine focus, visibility, reach and collision continue to govern use.

Focused composed checks exercise below-threshold, expert, master, grand-master and incapacitated readings,
the actual imported Manor definition, discovery without opening, current save/load and subsequent opening,
and a secret fixture whose door event waits until a later ordinary use after resume. Malformed thresholds
and forged saved discoveries refuse by name. Importer checks cover secret-source metadata, deterministic
secret and container emission and unchanged non-secret placements.

Bounded ordinary live discovery and final integrated verification are pending. Den owns their acceptance.
These checks make no rendered-world, broad-traversal, original-save or NativeAOT claim.
