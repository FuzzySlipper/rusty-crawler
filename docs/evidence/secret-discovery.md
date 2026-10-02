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

The bounded ordinary live check passed on the same imported Manor door. The low party's best acting
Perception was 1: ordinary DOM Use visibly refused with `secret-not-discovered`, kept the door closed,
and left Auto Notes empty. With Borin at Perception 10 / Expert 2, the first ordinary Use visibly
reported discovery with Perception 20 while the door stayed closed; the second visibly opened it.
The rendered journal's DOM query showed one `Read a secret surface` note. Both parties were grounded
at the corrected start (1100, -450, 2.13333), facing 1134, and every member stayed at 30/30.

The initial start was beyond the imported floor boundary and both profiles fell below it. That attempt
established no interaction acceptance. The parent corrected only the new arrival over an existing floor;
the imported door, event, secret metadata and collision geometry were unchanged. No save, resume,
debug positioning or targeted-use assistance was used in the successful observer sessions.

Den's terminal observer reading preserves original captures and receipts: low refusal
`e18a48d3-2058-4817-b15b-6288e3b62142`, high discovery `1f3313a0-4cc9-47c5-9c20-70cf218ca557`,
and high opening `f52e25f9-ef5b-44db-91f9-2bcddfac2493`. The parent inspected those originals.
Both sessions stopped with host, browser and lease released; the final pool was empty. Current-save
reconstruction is covered by the composed checks, not a live save/resume claim.

The full integrated verification passed every suite, import inventory/map decoding/deterministic
pack write and CoreCLR staging. Den holds its revision and terminal receipt.
These checks make no rendered-world, broad-traversal, original-save or NativeAOT claim.
