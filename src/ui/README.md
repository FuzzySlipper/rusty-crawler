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
| `menu.ts` | The Host-projected title, New Game, Continue, return confirmation, one-slot Save/Load screen and adventure launcher; it claims lifecycle actions and owns no session state. The saved party, place and calendar summary, dirty state, overwrite confirmation, load confirmation and failure sentence are rendered from the projection. |
| `reader.ts` | The one way a field is read: by name and type, with every missing or mistyped field named as a problem and its block read as unknown rather than half-read. |
| `snapshot.ts` | `readSnapshot`: every block of one projection, and every problem met reading it. |
| `dom.ts` | The small DOM vocabulary the sections share, and the one guard that decides when a section's controls are rebuilt. |
| `overview.ts`, `details.ts` | The composition, session, world, movement, clock, party, save, interaction, and controls blocks, and the panel's head, fact rows, pause/save/use controls, accomplishments, and keyboard hint. |
| `journal-book.ts` | The journal (`J`): a page per journal book — the quests (giver, note, each objective and how far it has come, the quest owner's last answer), notes, places with the way to the automap, the calendar on the one clock, and the history — each with an empty state in words. |
| `dialogue.ts`, `counter.ts` | The contextual screens: the person spoken with beside their words (face, greeting, what was said, topics and the withheld with why, the others present, a pocket to pick, the way out), and the counter with a page per thing it does — wares with pictures and each thief's hand, sell, identify, repair, lessons taught to the member picked, healing, training, food, a room, the bank with its quoted amount, travel, notices and debts — every price and refusal the mechanism's own before anything is settled. |
| `creation.ts`, `conversation.ts`, `service.ts`, `rest.ts`, `combat.ts` | Party creation (the four members as face cards, the faces, classes and skills on offer, the member's name and attributes, and reset, restore-default, confirm and accept), the conversation and counter kept in the diagnostic panel (the player's screens are `dialogue.ts` and `counter.ts`), the rest screen (each stop with the period and cost the rest mechanism judged, or why it would be refused, and whom a night would leave as they are), and the complete fight kept in the diagnostic panel. |
| `fight.ts` | The fight beside the world while something is fighting the party: the pacing and whose turn it is, the acting member with the fight's own readiness, what their attack would strike, attack, next member, the pacing switch, skip and wait, the nearest foes with the rest counted, and the fight's last blow. |
| `feedback.ts` | The answer to the party's latest act, read from the product's one numbered `feedback` block and headed by the act and what it concerned. |
| `progression.ts`, `promotion.ts`, `skills.ts`, `magic.ts` + `spellbook.ts`, `alchemy.ts` | The growth, magic and mixing blocks' readers and sections: growth and the whole-party spellbook are diagnostic sections, while `alchemy.ts`'s section is the spellbook's Mixing page; `magic.ts` holds the one rule for what a casting may be pointed at (`targetsOn`, `aimRows`). |
| `magic-book.ts` | The spellbook (`L`): the selected member's pages by school, every spell of a school learned or not with its price and the casting workflow's own reason it cannot be cast, the picked spell's aim (live reachable actors on the named side, or a typed downed body for a body-capable `either` spell, or the places it names), Cast and the quick slot; repeated names receive near-to-far words while durable target values stay in the action; the Mixing page holds the alchemy section. |
| `faces.ts` | The row of member faces a book opens with, selecting through the party's own selection. |
| `equipment.ts` | The equipment block's reader, and its whole-party diagnostic section: what each member wears, named working powers and permanent gifts, the pack's wearable and usable things, and Equip, Take off and Use rows. |
| `character.ts`, `inventory.ts` | The character book (`I`): the selected member's Stats (the game's sheet, growth with training at the hall the party stands at, the ranks the class leads to), Skills (rung, ceiling, the priced next point or the reason there is none), Inventory (the figure's every slot beside the party's one pack, item pictures, and an inspector offering Equip, Take off, Use, or a spell item's Use/Fire through the spellbook's casting) and Awards; a face at the top selects another member through the product. |
| `quests.ts`, `journal.ts`, `map.ts` | The quests and journal blocks' readers and their diagnostic sections, and the automap (the Map book's, with zoom that draws it larger and finds the party, and the adventure frame's small one). |
| `hud.ts` | The adventure frame's persistent parts: the four portraits with the face the product granted, pools at the percentage it published, conditions and the fight's own readiness, a click selecting the member; the purse, larder and clock; the adventure controls and the book buttons; the place, automap, running effects and companions down the right; and the Engine-backed reticle/context line naming the current target, ruleset disposition, verb, range and honest no-target or refusal reason beside the inline product-controlled next-target action and the answer to the latest act. |
| `frame.ts` | The screens over the world: the books a player opens (Character `I`, Spellbook `L`, Journal `J`, Map `V`, Rest), the contextual screens the product holds open, the keyboard handed to a book and back to the world, and the diagnostic panel (`` ` ``) holding every fact and session control. |
| `styles.ts` | The stylesheet, laid out for the supported desktop viewport of 1280×720 and wider. |
| `hud-styles.ts` | The adventure frame's own stylesheet, which `styles.ts` composes. |

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
- The adventure reticle's next-target button is an ordinary world control. It uses the `nextTarget` control the
  product publishes, labels the host key from that control, and claims its published action on click or keyboard
  activation. A disabled control is the product's bounded answer when no cycle can be taken; the companion does not
  count candidates, test reach, scan the world, or retain a target of its own.
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
- The lifecycle menu is also a projection surface. `menu.ts` sends `session.new-game`, `session.continue`, return,
  save/load and confirmation actions, while the Host and session keep the one admitted update and the existing
  persistence boundary authoritative. The Save/Load card renders the saved party, place and canonical calendar,
  disables an empty slot's load action, and leaves the live expedition visible through a failure sentence when a
  missing or corrupt document cannot be loaded. It owns no save bytes, cache, calendar or overwrite policy.
- Generated output is build product and stays ignored; never edit it by hand. Only `.js` files may be emitted
  there: the Engine refuses to stage a file type it does not serve.

The automap is the one drawing the panel makes, and it makes none of it: the product publishes the window the
game's zoom ladder shows, one rectangle per run of squares the party has seen, one point per mark with the kind the
game gave it, how large a mark is drawn, the party marker's corners and facing in the drawing's own space, the
ruleset's words for the drawing edges, and the words for what is seen or why nothing is. The panel writes those
values into SVG shapes and labels and computes no scale, no offset, no size, and no position of its own — a reload
of the same projection draws the same map.

It reports the last admitted movement step and the ground under the party — what it stands on, how often and how
soon that harms it, and what spares whom, each as the product read it — what the party faces and what using it did, the fight and its pacing,
each counter, conversation, and stop, and the party's records on the same terms: every value is the product's, a
refusal keeps its own code and sentence, and nothing on screen counts down or ticks on its own — a recovering
member shows the game time the product published, because a screen that timed recovery itself would show a
character ready before the fight agreed.

The central reticle in `hud.ts` is presentation of the product's current Engine selection. It names the target context,
ruleset disposition, verb, distance and selection reason, or says when no target is in sight; it owns no selection,
preview mutation, range check or action. Contextual screens hide it while the product owns input and show it again when
the world receives the next admitted update, so a stale facing or refusal cannot stand in for the current world.

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
