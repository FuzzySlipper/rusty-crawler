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
- `CrawlerProduct` registers the Engine's `PlaytestDebugModule` and `InteractionDebugModule` in the generated
  debug catalog (`IDebugCommandModuleSource`). `ProductPlaytest` answers `playtest.observe` with the session's
  own snapshot including canonical live combat actor positions, `playtest.action <intent>` with each declared keyboard control's physical key (`KeyW`, `Space`),
  hold or tap, a nominal input window, and whether the session would take it now (the controls block's answer,
  or the steering rule for movement, and that rule with the party's flight for rising and sinking), and `playtest.look` by turning the party through its facing rule
  (yaw only). Every query resolves the session held when it is asked, so a restart or an accepted creation
  never leaves a stale module; `interaction.inspect` reads the host's one `InteractionSelection`, which every
  session's world aims through. `PlaytestRegistrationTests` drives the generated catalog itself.
- `ProductIdentity` declares the product id, title, projection stream and contract, and the two
  input names once; the project file declares the same values, and the host suite (`ControlDeclarationTests`,
  which reads the constants by reference and the project file as XML) fails when the two drift.
- `BuiltInBundles` is the compiled list of bundles the product will start from: the default `mm7-new-game`
  and the empty shell `partyrpg-default`, chosen by `RUSTY_CRAWLER_BUNDLE` (unset is the default; any other
  value is refused by name). `ProductStart` reads whether a start is fresh or resumed from `RUSTY_CRAWLER_START`
  (a resume with nothing saved is refused by name rather than starting fresh). When the selected bundle's only
  defect is that packs it names are absent, the selection carries them and the bundle's `setup` text; the
  session offers no creation and publishes them as `composition.setup`, which the panel shows in place of a
  world ([new-game reading](../../docs/evidence/new-game-bundle.md)). Any other content defect still stops the
  product with every problem named.
- The ordinary product entry opens a fresh launch on the projected title menu. `SessionMenuState` is shared by the
  Host and its one session: New Game reveals the existing creation or scenario path, Continue composes a resumed
  session and swaps it only after a successful load, and a visible return control pauses for an unsaved confirmation
  before disposing and rebuilding the session. A refused Continue keeps a usable title menu with its persistence or
  content sentence; no menu action creates a second update loop or a parallel session owner. The Host marks the
  visible menu as the owner of the whole admitted update, including the update that opens or closes it, so the
  activating key cannot also advance hidden creation or gameplay input.
- `CrawlerProduct` owns one `EngineUiProjectionChannel` for its whole lifetime. Every replacement session receives
  that same channel and sets `OwnProjection` false, so session disposal releases its world, party, images and save
  store without closing the stream or publishing a stale stopped projection; product shutdown closes the channel once.
  The shared stream keeps projection sequences increasing for the browser binding across New Game, Continue, return
  to title and restart.
- The adventure menu's explicit Save/Load screen uses the existing one-slot persistence boundary. It shows the saved
  party, member count, coins, provisions, place and ruleset calendar, marks unsaved live work, asks before overwriting
  a slot or discarding changes for a load, and keeps the current held session usable when a slot is empty, missing or
  corrupt. Save and load errors remain visible in the menu; they do not create a parallel store or silently start a
  fresh expedition. The ordinary Save action and F key use the same overwrite confirmation while this screen is open;
  an empty slot remains directly writable.
- The project file declares the product metadata and 25 input intents, each digital with its key: pause
  (`session.pause-toggle`, P) and save (`session.save`, F); the movement intents (W/S/A/D, Q/E, Space, and
  flight's `party.ascend` and `party.descend` on the up and down arrows, held — the original's Page Up and Insert
  are not keys the engine carries); use
  (`party.use`, G); the ways out of a counter and a conversation (`service.leave`, X; `conversation.leave`,
  Escape); the stops (`rest.rest` R, `rest.camp` C, `rest.wait-dawn` T, `rest.wait-hour` H,
  `rest.wait-five-minutes` M); the act control (`party.attack`, B, held, so a held key keeps attacking as each
  member's recovery elapses — the donor's own key and trigger); the pace controls (`combat.turn-based` on
  Enter, the original's own key, and `combat.turn-skip` K and `combat.turn-wait` Y); and creation's two
  (`creation.advance` Enter, `creation.accept` Space). Everything that names a row — a choice at creation, a
  counter's offer, a topic, a spell and its target, a mix — arrives as a payload action on the `crawler.ui`
  channel, declared beside them. `ProductIdentity.ServicePayloadActions` declares the counter vocabulary
  (including cure, train, provision, stay, deposit, withdraw and the amount quote), and the host-written
  contract fixture binds it to the companion. The SDK declares the payload channel, not individual JSON
  action names. The TypeScript build target is declared there too.

Not declared: a look or aim control and a launcher (`playtest.look` turns the party for a harness, not a player);
`docs/live-checks.md` covers how a check selects content instead.

Boundary rules:

- The host may select the Might and Magic ruleset and bundle; it may not
  interpret Might and Magic rules, read original game data, or contain
  Might and Magic formulas.
- Exactly one concrete product entry type is declared. The immutable SDK
  generates the CoreCLR and NativeAOT composition beneath ignored `obj` output;
  no generated bridge, binding, or export is checked in.
- No second loop, clock, timer, thread, renderer, or browser authority.


`party.next-member` is a declared digital N press, and the same next-member action is published for
the panel. `party.select-member` names a real durable member on the existing UI payload channel.
Both reach the kit's roster selection before the ordinary selected-only attack. Lifecycle menus own
selection input until their closing update finishes, so N cannot change the party behind a menu. The companion prints
the product's selected flag and selection/refusal message; it owns no choice, capability rule or timer.
N adapts the donor's Tab cycling to the installed Engine keyboard vocabulary. B is this product's
attack binding; the donor uses A for attack and B for passing a turn (`OpenEnroth/src/Application/GameConfig.h:536,542,554`).
