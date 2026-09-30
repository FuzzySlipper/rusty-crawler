# PartyRpg.Host

The product lifecycle, the one ordinary product entry, and the explicit built-in ruleset selection.

Implemented today:

- `CrawlerProduct` implements the generated `IEngineProduct` contract: it selects the built-in
  ruleset at its composition seam, builds that ruleset's session over an Engine UI projection
  channel, reads admitted input through the kit's session input router, and forwards the engine
  lifecycle to the session. The engine's pause and the player's hold are separate authorities: an
  engine resume does not release a hold the player asked for.
- `BuiltInRulesets` is the host's explicit ruleset selection, and the only place it chooses one.
- `ProductControlKeys` reads, from the keyboard mappings the engine hands the product at creation, which key
  the project file bound each stand-alone control to, and hands those labels to the session through the
  ruleset context; the panel names those keys and no others, so a hint cannot drift from the declaration.
- `ProductIdentity` declares the product id, title, projection stream and contract, and the two
  input names once; the project file declares the same values, and the host suite (`ControlDeclarationTests`,
  which reads the constants by reference and the project file as XML) fails when the two drift.
- `BuiltInBundles` is the compiled list of bundles the product will start from, and `ProductStart` reads
  whether a start is fresh or resumed from `RUSTY_CRAWLER_START` (a resume with nothing saved is refused by
  name rather than starting fresh).
- The project file declares the product metadata and 23 input intents, each digital with its key: pause
  (`session.pause-toggle`, P) and save (`session.save`, F); the movement intents (W/S/A/D, Q/E, Space); use
  (`party.use`, G); the ways out of a counter and a conversation (`service.leave`, X; `conversation.leave`,
  Escape); the stops (`rest.rest` R, `rest.camp` C, `rest.wait-dawn` T, `rest.wait-hour` H,
  `rest.wait-five-minutes` M); the act control (`party.attack`, B, held, so a held key keeps attacking as each
  member's recovery elapses — the donor's own key and trigger); the pace controls (`combat.turn-based` on
  Enter, the original's own key, and `combat.turn-skip` K and `combat.turn-wait` Y); and creation's two
  (`creation.advance` Enter, `creation.accept` Space). Everything that names a row — a choice at creation, a
  counter's offer, a topic, a spell and its target, a mix — arrives as a payload action on the `crawler.ui`
  channel, declared beside them. The TypeScript build target is declared there too.

Not declared: a look or aim control and a launcher; `docs/live-checks.md` covers how a check selects content
instead.

Boundary rules:

- The host may select the Might and Magic ruleset and bundle; it may not
  interpret Might and Magic rules, read original game data, or contain
  Might and Magic formulas.
- Exactly one concrete product entry type is declared. The immutable SDK
  generates the CoreCLR and NativeAOT composition beneath ignored `obj` output;
  no generated bridge, binding, or export is checked in.
- No second loop, clock, timer, thread, renderer, or browser authority.
