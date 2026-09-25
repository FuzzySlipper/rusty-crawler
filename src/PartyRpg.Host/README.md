# PartyRpg.Host

The product lifecycle, the one ordinary product entry, and the explicit built-in ruleset selection.

Implemented today:

- `CrawlerProduct` implements the generated `IEngineProduct` contract: it selects the built-in
  ruleset at its composition seam, builds that ruleset's session over an Engine UI projection
  channel, reads admitted input through the kit's session input router, and forwards the engine
  lifecycle to the session. The engine's pause and the player's hold are separate authorities: an
  engine resume does not release a hold the player asked for.
- `BuiltInRulesets` is the host's explicit ruleset selection, and the only place it chooses one.
- `ProductIdentity` declares the product id, title, projection stream and contract, and the two
  input names once; the project file declares the same values, and the architecture suite fails when
  the two drift.
- The project file declares the product metadata, the `session.pause-toggle` intent and its key
  mapping, the movement intents and their keys, the creation screen's two controls (`creation.advance`
  on Enter and `creation.accept` on Space) with the `crawler.ui` payload channel its choices arrive on,
  and the TypeScript build target.

Still to come: bundle selection, launcher and diagnostics surfaces, content bundles, and the gameplay
intents (movement, look, interaction, combat) that arrive with the stones that implement them.

Boundary rules:

- The host may select the Might and Magic ruleset and bundle; it may not
  interpret Might and Magic rules, read original game data, or contain
  Might and Magic formulas.
- Exactly one concrete product entry type is declared. The immutable SDK
  generates the CoreCLR and NativeAOT composition beneath ignored `obj` output;
  no generated bridge, binding, or export is checked in.
- No second loop, clock, timer, thread, renderer, or browser authority.
