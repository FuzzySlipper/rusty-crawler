# ui

The product DOM companion, authored in TypeScript and compiled into generated output by the host
project's build. The compiler options are in `tsconfig.json`, which both the host build and
`npm run build:ui` read, so the companion suite (`npm run test:ui`) exercises the same `generated/` modules
the product ships. There is no bundler: the Engine stages the whole generated directory and serves each module
beside `main.js`, so the modules import one another by their `./name.js` specifiers.

`main.ts` exports `mountProductUi(root, context)`: it composes the panel, subscribes to the projection, reads
each projection through `readSnapshot`, and hands each block to the section that draws it. Every block has a
module of its own, and every reader and every section in it is a named export the companion suite can call on
its own:

| Module | Holds |
| --- | --- |
| `context.ts` | The mount context as the Engine delivers it: the interface port, the projection view (`current()` and a `subscribe` that delivers the current envelope, possibly `null`, at once), the intent port, and the input port. |
| `actions.ts` | The projection and action contracts, and the actions a row of a screen claims. |
| `reader.ts` | The one way a field is read: by name and type, with every missing or mistyped field named as a problem and its block read as unknown rather than half-read. |
| `snapshot.ts` | `readSnapshot`: every block of one projection, and every problem met reading it. |
| `dom.ts` | The small DOM vocabulary the sections share, and the one guard that decides when a section's controls are rebuilt. |
| `overview.ts`, `details.ts` | The composition, session, world, movement, clock, party, save, interaction, and controls blocks, and the panel's head, fact rows, pause/save/use controls, accomplishments, and keyboard hint. |
| `creation.ts`, `conversation.ts`, `service.ts`, `rest.ts`, `combat.ts` | Party creation (the four members as face cards, the faces, classes and skills on offer, the member's name and attributes, and reset, restore-default, confirm and accept), a conversation, a counter, the stops, and the fight. |
| `progression.ts`, `promotion.ts`, `skills.ts`, `magic.ts` + `spellbook.ts`, `alchemy.ts` | Levels, ranks, skills, the spellbook and the magic the pack carries, and mixing. |
| `equipment.ts` | What each member wears, named working powers and permanent gifts, the pack's wearable and usable things, and ordinary Equip, Take off and Use controls; the product judges every action. |
| `quests.ts`, `journal.ts`, `map.ts` | The quests book, the other four books, and the automap. |
| `hud.ts` | The adventure frame's persistent parts: the four portraits with the face the product granted, pools at the percentage it published, conditions and the fight's own readiness, a click selecting the member; the purse, larder and clock; the adventure controls and the book buttons; the place, automap, running effects and companions down the right; and the line saying what the party faces and what was last said. |
| `frame.ts` | The screens over the world: the books a player opens (Character `I`, Spellbook `L`, Journal `J`, Map `V`, Rest), the contextual screens the product holds open, the keyboard handed to a book and back to the world, and the diagnostic panel (`` ` ``) holding every fact and session control. |
| `styles.ts` | The stylesheet, laid out for the supported desktop viewport of 1280×720 and wider. |

Boundary rules:

- No gameplay state, no rules evaluation, no game-world rendering, no transport,
  and no game loop. It renders what the product publishes and reports intents
  back.
- Every verdict is the product's. Whether a stand-alone control would be taken, the key it is bound to, and the
  action it sends arrive in the projection's `controls` block; whether a row's command would be taken arrives on
  the row (`canBuy`, `canCast`, `canUse`, `canTrain`, `canMix`); and every derived number — the level a training
  step reaches, the side a spell names, the items a counter would identify or mend, an automap mark's size and
  the party marker's corners — is published rather than worked out. The companion suite fails on any module that
  combines a published quantity with anything or disables a control from anything but a published verdict.
- A projection field this companion reads and does not find is a broken contract, not a quiet session: it is named
  in the panel's problem list (`data-problems` on the panel), and the block it belongs to is read as unknown. The
  fixtures the companion suite mounts are the product's own output, so a renamed C# field fails a test.
- What is kept between projections is presentation, and nothing else. The frame's screens (`frame.ts`): which book a
  player has open — a button or its key opens it, the same again or Escape closes it, a contextual screen the product
  opens closes it — whether the diagnostic panel shows, and whether a setup guidance has already opened it; none is
  saved and none asks the product for anything. A contextual screen — creation, a conversation, a counter — is the
  product's and shows exactly while the product holds it open. And what a section with controls last drew
  (`redrawGuard` in `dom.ts`).
  A running session's projection changes every admitted update, because its simulation time and step count move,
  so an unchanged block still arrives many times a second; a section rebuilt on each would replace the button under
  the pointer between its press and release and reset a picker a player has open. The guard compares the whole
  published block, so what is drawn is always the last value the product sent.
- Generated output is build product and stays ignored; never edit it by hand. Only `.js` files may be emitted
  there: the Engine refuses to stage a file type it does not serve.

The automap is the one drawing the panel makes, and it makes none of it: the product publishes the window the
game's zoom ladder shows, one rectangle per run of squares the party has seen, one point per mark with the kind the
game gave it, how large a mark is drawn, the party marker's corners and facing in the drawing's own space, and the
words for what is seen or why nothing is. The panel writes those numbers into SVG shapes and computes no scale, no
offset, no size, and no position of its own — a reload of the same projection draws the same map.

It reports the last admitted movement step and the ground under the party — what it stands on, how often and how
soon that harms it, and what spares whom, each as the product read it — what the party faces and what using it did, the fight and its pacing,
each counter, conversation, and stop, and the party's records on the same terms: every value is the product's, a
refusal keeps its own code and sentence, and nothing on screen counts down or ticks on its own — a recovering
member shows the game time the product published, because a screen that timed recovery itself would show a
character ready before the fight agreed.

The service offer rows carry each patient or party choice, the quoted quantity, charge, payment and refusal.
Cures and training therefore choose the member they were priced for. The bank amount field asks the
product for a fresh quote through `service.amount`; deposit and withdrawal buttons send that published
quantity until the next projection replaces them. This is one transient counter selection, with no purse,
holding, price arithmetic or eligibility in the companion. Counters without an offer show no action for it.

## Player-facing acceptance

The adventure frame (`hud.ts`, `frame.ts`) is the player's surface: the world with the party's portraits, purse and
controls, and the books and contextual screens over it. Until their own screens land (#9221–#9226), the books hold the
existing sections unchanged; those sections and the diagnostic panel are a mechanism-inspection surface that does not
satisfy the game-feature landing rule in the repository's [AGENTS.md](../../AGENTS.md). Adding another row, command,
button or collapsible section to them is not an ordinary feature implementation.

The product must instead present a persistent adventure HUD and recognizable contextual screens
or visible world controls, following [the interface design](../../docs/gameplay-design.md#311-interface-surfaces--match-information-architecture--ours-presentation).
A screen may scroll a bounded list; it must organize the gameplay decision, show its actual
result/refusal and preserve relevant party/world context. The companion continues to render the
one product projection and report semantic actions; it acquires no gameplay or screen authority,
clock or world renderer. Console/debug commands may call the existing canonical owners for agents.
