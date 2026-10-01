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
| `creation.ts`, `conversation.ts`, `service.ts`, `rest.ts`, `combat.ts` | Party creation, a conversation, a counter, the stops, and the fight. |
| `progression.ts`, `promotion.ts`, `skills.ts`, `magic.ts` + `spellbook.ts`, `alchemy.ts` | Levels, ranks, skills, the spellbook and the magic the pack carries, and mixing. |
| `equipment.ts` | What each member wears, the pack's wearable things, and the Equip and Take off controls; whether a change is allowed is the product's refusal. |
| `quests.ts`, `journal.ts`, `map.ts` | The quests book, the other four books, and the automap. |
| `styles.ts` | The stylesheet. |

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
- The only thing kept between projections is what a section with controls last drew (`redrawGuard` in `dom.ts`).
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
