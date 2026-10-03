# Rusty Crawler: current product and gameplay assessment

Assessment date: October 2, 2026 (America/Los_Angeles). This is a point-in-time assessment of the checkout and content tested, not a replacement for live Den status. The exact source identity, launch manifests, importer output, session identities and original capture provenance are retained in the accompanying local evidence manifest.

## What the current product offers

The default product can launch its ordinary creation flow and accept a four-member party. It selects no content packs and visibly reports **No world loaded**. A player can therefore finish creation but cannot begin an expedition from the unconfigured default launch.

The current presentation is a working, narrow DOM panel over an empty Engine frame. This agrees with the current README and live-check instructions; the absence of a rendered world is not a failed GPU launch. It nevertheless prevents the first-person experience described by the design: the player cannot see roads, houses, doors, people, creatures, loot, terrain or obstacles in the world view.

The distinction matters. This project has substantial party-RPG mechanisms, content import and a useful mechanism-inspection surface. That is different from a player being able to discover and complete the ordinary explore → meet/fight → loot → return to town → improve → travel loop.

## Method and evidence boundaries

Three independent playtest missions, followed by a focused town check and two short operator-resume inspections, used the maintained crew-services browser backend. The default mission used the unmodified tracked bundle. The gameplay mission used an isolated checkout of the same source, current operator-imported tables/world, and one authored launch-only entry selecting the original Emerald Island `Party Start` with `party: creation`. A third mission accepted the untouched default party in that imported start and concentrated on rest, books and panel usability. No scenario party, statistics, starting items, monster values, geometry or services were changed. The original game installation was read only.

The staged start is configuration needed to enter the current product, not proof that an ordinary launcher selects a new game. Gameplay actions are distinguished from semantic DOM locator assistance, read-only runtime facts and any held-time inspection. No save bytes were rewritten, no party was teleported, and no outcomes were forced to complete a demonstration.

Original screenshots are preserved with paths and metadata. Real-time screenshots may have uncertain simulation-frame correlation; they show the visible UI at capture time, but do not establish a precise admitted step or deterministic timing. These runs do not measure frame rate, GPU completion or performance isolation.

Online comparison used twelve original screenshots from the GOG listing plus the original reference card. All twelve source images were inspected through browser captures. Source images are 640×480; the test browser is 1280×720. The comparisons concern information hierarchy, recognition, navigation and action feedback. There is no matched camera pose or pixel-accuracy claim. The separate reference appendix links every source.

Windows was not used for this assessment: the local browser path successfully exposed the product DOM UI, and configured native Windows lanes were for other products. No Windows, NativeAOT, gamepad, audio or cross-platform acceptance claim follows from these runs.

## Launch and content preparation findings

1. **Missing tool environment.** The first owned host could not find `dotnet`, although a local SDK was installed. Chromium stayed on `about:blank`; that white screenshot is infrastructure evidence, not the game's appearance. An assessment-only manifest added the installed SDK directory to PATH. Product source and the repository's ordinary manifest were left alone.
2. **Concurrent startup contention.** Fresh-game starts initially hit the broker's ten-second lease-lock timeout while another host was starting. This constrained test orchestration; it says nothing about gameplay mechanics. Subsequent startup was sequenced.
3. **Stale generated content.** Existing operator packs lacked the current Arena challenger-feet data. The runtime refused startup with `Arena content cannot place its challengers`. Old pack copies were retained outside the content root. The current importer was built and run against the operator install with determinism checking; the two writes were identical. Regenerated Arena content carries twenty challenger positions. No manual data workaround was introduced.
4. **No selected default world.** After environment recovery the default product connected, rendered creation, and accepted the party, while still showing `partyrpg-default · 0 packs` and `No world loaded`. This is a genuine default-experience limit, rather than another harness fault.
5. **No player launcher.** Entering an imported expedition required selecting content and an explicit start in an operator-configured bundle. The host README explicitly says a launcher is not declared. New/load selection is therefore not yet an ordinary player flow.

The fresh import contains 76 place definitions and geometries, 276 monster rows, 800 item rows, 99 spells, 37 skills, 36 class ranks, 136 services, 1,000 people rows, 512 quest rows, 193 travel-link entries and 1,023 plate entries. These are counts of emitted records, not claims that every row is playable, every route is reachable, or every quest forms a complete authored progression. The importer write and runtime selection provide different evidence from a full playthrough.

## What happened in normal play

### Creation and entering the island

The unmodified default bundle allowed the complete default party to be accepted but loaded no world. In the imported setup, accepting the same defaults entered Emerald Island. A separate new party changed the first member to a Human Paladin named Valeria through the ordinary portrait/class/skill/attribute/name workflow. Shield and Bodybuilding were selected alongside the class's fixed skills. The other three members kept their defaults. Ordered-step refusals and an attempted attribute increase with zero points gave concrete explanations rather than silently changing the party.

The customized party started with 200 coins, 10 food portions, an empty shared pack, nobody wearing equipment, 110/110 total vitality, and 71/71 total spell points. The unchanged default party had different pools, as expected from its Knight rather than Paladin. Both starts showed no learned spells or quick spell. Creation source explicitly chooses an empty learned-spell list and expects learning books during play; this is a deliberate current behavior, not an inference that the casting mechanism is missing.

The first movement hold after using the panel did not move the party. Clicking the world frame and repeating a movement hold did. This is a focus/input-discovery concern: input transport acceptance alone did not establish movement. Once focused, W/A/D and turning changed live feet positions and grounding. No corresponding road, slope, obstruction, or camera change was visible because the frame remained empty.

### Exploration, signs, and finding a person

The broad mission used read-only interaction facts to approach a target named `Shops` with ordinary movement, then pressed G. The panel reported a read/use result, but opened no shop. A nearby `Lord Markham` target turned out to be a sign: its message read “Lord Markham.” This is useful evidence of a discovery problem. A sign labeled with a person's name is not the person, and a sign labeled Shops is not a counter. It is not evidence that shop mechanisms are broken.

A later focused fresh-party check used the known coordinates of Tor as read-only navigation help. It walked from Party Start, adjusted around an approach where forward holds made no progress, and reached a position roughly 430 units from Tor. Normal G opened his conversation with a greeting, his name, `Ask to see the wares`, and `Take your leave`. Selecting the ordinary wares topic handed off to `The Knight's Blade · Tor`, a weapon shop open 06:00–18:00. This completes a real approach → talk → counter chain without target-ID activation or teleporting. It remains assisted discovery because nothing in the world view revealed the route or the shop.

### A purchase, equipment, and the shared purse

The weapon counter displayed stock, prices, theft actions for Nyx, and skill lessons with a chosen member. It offered a Crude Longsword for 75 coins, a Cutlass for 60, a Crude Axe for 45, and more expensive weapons. An unaffordable spear was selected before the cheap purchase; no spear was acquired. The capture did not settle on its refusal caption, so this report makes no claim about that refusal's wording.

Buying the Crude Longsword changed its stock button to `sold out` and exposed the newly carried item in the counter. After leaving the counter, the existing equipment action put it in Roderick's main hand. The screenshot explicitly says `Roderick now wears Crude Longsword in 'main hand'`. The shared purse showed 125 coins, consistent with charging 75 once from 200. The shared pack then showed nothing to wear because its one item was equipped. That empty-pack sentence is not evidence that the purchase lost the item.

This is a meaningful positive result: a fresh created party can acquire and equip a useful item through current conversation, service, inventory, equipment and ledger owners. It does not establish theft, lessons, selling, identification, repair, armor, spellbook purchases, or the full economy. Those controls were observed but not exercised downstream.

### Camp, wait, pause, and the calendar

At the default party's starting outdoor location, `Make camp` advanced eight game hours, restored four members, and charged two food portions. The moving customized party's later camp charged one portion. These are two observed locations/states, not a controlled terrain-cost comparison. They must not be treated as inconsistent pricing without checking the terrain policy and ground under each party.

`Rest & heal 8 hours` outdoors refused with guidance to make camp or find a roof. `Wait 5 minutes` advanced the clock and explicitly said that nobody was restored. `Wait until dawn` advanced into the next day without healing. The calendar and history reflected the resulting day, time, and entry into Emerald Island. Pause changed to Resume; in the broad mission a movement hold while paused left the pose unchanged and the observer named the held steering state.

The one-clock presentation is useful: these actions have named time/resource effects. The current panel still requires scrolling to compare their consequences with food, date, party pools and world context.

### Combat and attempted body search

The broad mission first switched to turn-based mode while nobody was hostile; the panel correctly said no round was under way. Pressing Attack then provoked local inhabitants. The subsequent projection showed an engagement with Peasants, one downed Peasant, and member recovery; read-only observations also named Ailyssa the Bard. This was an initiated attack on inhabitants, not a naturally encountered hostile monster or proof of the intended monster combat loop.

Aelina's turn-based attack selected Borin next; Borin's attack selected Nyx. Screenshots show the acted members recovering and the ready count decreasing. Returning to real-time changed the pacing description to per-actor recovery; Nyx's attack also entered recovery. These are actual pacing/selection/recovery responses. The run did not reach a completed fight, a victory reward, experience gain, or a cleared-place result.

A downed Peasant body became the current search target and G was attempted near it. The lower panel displayed that body and an `applied` use status, but retained the earlier sign message, while the pack remained empty. That capture does not establish a newly completed body-search outcome or loot transfer. Treat corpse searching and reward collection as **attempted, outcome uncertain**, rather than a passed loot step.

The practical combat issue is target awareness. A player sees `Nobody is hostile` and an enabled Attack button, then a large provoked fight appears as text. Without a visible target, scene or disposition cue, it is difficult to understand whom the action will attack and what consequences it will have. Attacking peaceful inhabitants may be valid ruleset behavior; the missing actionable context is the assessed gap.

### Save and operator resume

The broad mission activated Save through F and the panel. The focused town mission separately saved its newly created, sword-equipped party, with an explicit slot/time confirmation. These were different parties and their outcomes are not combined into one expedition.

The town save was then resumed by an assessment-only host selection using the existing `RUSTY_CRAWLER_START=resume` seam. It showed `Party resumed`, the same four members, the same place and nearby Tor position, 125 coins, 10 food, the `Met Tor` history entry, 183 explored cells, and Roderick's main-hand Crude Longsword. Ordinary live time and grounding continued after startup, so exact clock/pose equality at the later screenshot is not claimed. The original save's checksum stayed identical through both short resume inspections; no save edits or subsequent saves were made.

Persistence therefore has bounded positive new-party → purchase/equip → Save → operator-resume evidence. There is still no ordinary title/options Load selection in this tested UI, and no claim that the customized party's active fight was resumed in this run.

## Coverage ledger

| Part of the loop | What was reached | Assessment |
| --- | --- | --- |
| Default start | Creation and acceptance, zero packs, no world | Launch works; default expedition unavailable |
| Imported new game | Default and customized parties accepted into Emerald Island | Passed for supplied operator configuration |
| Creation gates | Ordered steps, skills/name/class change, zero-point refusal | Concrete successful choices and named refusals |
| Walking/turning | Ordinary movement changed position; focus acquisition was needed | Mechanism responds; unaided visual navigation unavailable |
| Interactions | Sign reading; guided ordinary G opened Tor | Positive sign/talk evidence; discovery assisted |
| Shop and equipment | Wares topic, 75-coin purchase, sold-out stock, main-hand equip | Passed bounded acquisition chain |
| Rest/camp/wait | Outdoor-rest refusal, camp costs/restoration, waits without healing | Passed bounded behaviors |
| Party control | Next member and paced actor selection | Positive selected-member behavior |
| Combat | Provoked inhabitants, attacks, recovery, two pacing modes | Partial; no natural monster encounter or resolved reward loop |
| Corpse/loot | Body selected and G attempted, no new loot/result confirmed | Uncertain; not accepted as a completed loot step |
| Books/map | SVG automap, quests/notes/calendar/history and progression/skills visible | Readable state; most fresh-party content empty |
| Magic/alchemy | Spell points, empty learned/quick spells, no mixable items | Surfaces reached; no cast/learning/mixing outcome tested |
| Progression | Level 1, zero experience/points, skill costs and promotions displayed | Inspection only; no earning, training or promotion |
| Save/resume | Ordinary town Save; unchanged bytes resumed through operator seam | Passed bounded persistence, no in-game Load UX proof |
| Quests/travel | No quest accepted/turned in; no place transition completed | Unassessed in this run |
| Full expedition | No single party completed fight → loot → town → improve → travel | Not demonstrated; presentation/discovery remain central barriers |

## Visible UI comparison

### Adventure view and persistent party context

[Original adventure screenshot](https://images.gog-statics.com/8b63937249daa2f9f2cca82f200eadea9a193b09cd46f82b54f1386b78564601.jpg) shows a walkable road, buildings, vegetation and a person, with four portraits and pool bars along the bottom. The right column keeps map, followers, effects, resources and books visible. The current product's empty frame supplies none of that spatial or character context. This is the highest-impact presentation gap: moving the party may change authoritative state, but there is no scene from which a player can choose a destination or read a threat.

The design deliberately permits original presentation, not necessarily the original art. A modern layout is compatible with the target. The needed property is simultaneous awareness of world, party condition, direction, interaction and urgent actions.

### Creation

[Original creation screenshot](https://images.gog-statics.com/9df5d0a2f963be605aa46627eb3afc82e77a5a7789336dec2f7b517d8083c6a3.jpg) presents four members side by side, with portraits, races, classes, attributes and skills. The current flow expresses the same kinds of decisions and provides a ready default party. It presents one selected member's controls as text buttons and sequential steps, so comparing the party takes switching members rather than glancing across four columns.

The current "Portrait" controls are labels such as `Human man` and `Elf woman`, not portrait images. The selected member carries a string identity such as `human-man`. Race/class/skill decisions are readable, but visual party identity is absent. The name input is visibly reduced to a narrow sliver by the adjacent full-width `Set name` button; a successful programmatic fill would not prove that a player can comfortably read and edit the field.

### Combat

[Original dungeon combat](https://images.gog-statics.com/3ab9c7f378612b02a4fec0ea73144cced81d8e96b5551211372f74759367d36a.jpg) makes enemy position, proximity, corridor geometry and target identity visible in the same scene as the party. [Original combat feedback](https://images.gog-statics.com/99fc8e08afec125d3f38aacae9052d9283f13e1f4342c5b3448b2f90db34e244.jpg) supplies a concise attacker/damage/kill line while party faces and bars remain present. The current panel has actor health/readiness, selection and pacing controls, which can communicate rule outcomes, but the empty world prevents visual aiming, distance judgment and threat interpretation. A working attack command is not a complete combat experience without those cues.

### Inventory and equipment

[Original inventory screenshot](https://images.gog-statics.com/f6b4fcca8f68adfbe476e851515c8e16c20fdc1253f9fa00ed1337041fb65aaa.jpg) uses recognizable item art, an inventory grid and an equipped character. The project intentionally replaces four packs with one shared party inventory. That is a deliberate improvement, not a defect. The useful comparison is whether items, equipment ownership, properties and use choices are easy to recognize and manage; reproducing the original grid or separate packs is unnecessary.

### Magic, map and conversation

[Original spellbook](https://images.gog-statics.com/4da6803833e28c33dd71ca4fe8cd8cf8c86aeab2e720f63f736942a498fca012.jpg) groups spells on a school page with distinguishable icons, selected-spell text and a quick-spell action. [Original map book](https://images.gog-statics.com/6f51e71a4a90f0bf289335ae3f7ebca5c673bf911ee1839aebc8c3bbcf897ae8.jpg) emphasizes explored geography while retaining party context. [Original residence](https://images.gog-statics.com/fdeb79bc0b2d70e0cf0c227df09f527cdabbd705ecfdd40181f70c0b6505fc0e.jpg) gives a room and named portrait choices. Current text/SVG mechanisms can serve these roles, but they need contextual presentation and deliberate screen hierarchy to be an ordinary game interface.

Arcomage appears in one reference image. Its presence in the original does not by itself make it a defect or a new requirement for this repository; current authorized design and task scope decide that.

### Panel hierarchy and controls

The default accepted-party panel is about 410 pixels wide at a 1280×720 viewport. It shows roster, rest and combat in one long column. DOM geometry places pause, save and use controls roughly 2,300 pixels below the initial top. CSS provides an internal scrollbar, so this is **offscreen placement**, not proof that controls are impossible to reach. In the imported default-party session those same controls were around 3,800 pixels down; an open counter added still more height. It makes persistent resources, books and urgent actions compete for scroll position with mechanism details.

The source composes creation, conversation, services, rest, combat, progression, promotion, skills, spellbook, alchemy, equipment, awards, automap, journal and general facts into the same panel. The information is valuable for diagnosis. A player needs a smaller persistent HUD and clearly entered contextual screens so that opening a shop or examining equipment does not displace awareness of the expedition.

A raw unsupported PageDown virtual-key probe degraded the default test adapter. That is a harness problem and is not attributed to the product's scrolling behavior. Supported named browser-key actions and ordinary locator activation are separate paths.


The imported SVG automap is present: its observed 192-pixel drawing showed a small explored trace, and the journal separately listed explored-cell counts. It is not an absent mechanism. The comparison gap is scale and context: it sits deep in the general panel and cannot serve as a persistent navigation corner while other actions are in view.

Semantic heading clicks sometimes scrolled the intended heading into view but timed out when the canvas intercepted the click. These headings are non-interactive text; this is not evidence that ordinary action buttons are obstructed. Supported key navigation did scroll the panel. Pausing made the inspection stable while live projection updates had moved or replaced elements. No mouse-wheel, touch, accessibility, or responsive-layout acceptance follows from this run.
## Findings and recommended order

The order below reflects impact on an ordinary new player, rather than the amount of mechanism code already present. These recommendations are assessment outcomes; they do not create or change Den tasks.

| Priority | Finding | Evidence and consequence | Owning work |
| --- | --- | --- | --- |
| First | An unconfigured default accepts creation but has no expedition | Default screenshot shows an accepted party with zero packs and no world. A person cannot enter the loop without operator configuration. | Host/bundle startup: an explicit new-game/load flow, selected valid content and a useful missing-content explanation. Do not redistribute operator data. |
| First | No visible world | Every accepted-party capture has an empty frame, including after movement and combat state changes. Navigation and encounter discovery require facts unavailable on screen. | Product presentation over the existing Engine renderer/resources/spatial scene. Imported geometry is not itself a presented world. |
| First | The interface functions as a mechanism console | One scrolling column exposes roster, actions, rule details, books and diagnostics; basic expedition context does not stay visible together. | Ruleset projection and thin DOM UI: persistent party/resources/interaction context, explicit contextual screens, and discoverable actions. Preserve one authoritative projection and semantic actions. |
| Next | Creation has a visible name-field sizing defect and no character art | The field is squeezed beside a full-width button. Portrait choices are strings; party identity is textual. | Existing creation view and styles. Fix sizing; provide readable identity and party-wide comparison before accepting. |
| Next | Fresh-start direction and magic onboarding remain incomplete in this evidence | Created members begin with an empty shared pack, wear nothing, and show no learned spells/quick spell. A later assisted approach bought and equipped a sword, so equipment acquisition is possible; spell acquisition was not demonstrated. | Ruleset and bundle content: a deliberate initial direction and magic acquisition path that a new player can discover and complete; test it without a preloaded party. |
| Next | Rule feedback lacks world context | Named refusals and recovery values help diagnose actions, but no visible target/body/entrance associates them with space. | Interaction/combat presentation over the canonical current owners; keep target, reach, availability, action result and recovery visible where the player acts. |
| Supporting | Local startup depends on a usable SDK environment and current packs | Missing SDK PATH and old Arena metadata each prevented startup, with different errors. Recovery worked without product code changes. | Operator setup and content preparation. Document/recheck supported launch paths and regeneration; preserve concrete validation failures. |

The biggest gap is not more numerical fidelity. It is turning an already broad set of mechanisms into an observable expedition. Adding more imported places or monsters before a player can read one road, one person and one encounter would increase the amount of data without resolving that gap.

A sensible next acceptance target is one ordinary, newly created band completing the loop described in the design: identify a destination visually, approach and talk, obtain a useful item or spell, equip or learn it, fight a visible hostile, collect its loot, return to a service and spend the proceeds, then leave/re-enter or travel and resume the same expedition. This is a way to evaluate the general mechanisms once presented, not authorization to build a proof-only slice or to limit capability coverage to Emerald Island. No seeded equipment, edited saves, teleports or invisible target-ID activation should carry that acceptance claim.

## Existing gaps and coverage still needed

Den was reachable and its project/task guidance was read before the assessment. Existing receiver tasks include world-targeted Telekinesis (#9145), NPC group news (#9150), and follower profession benefits (#9151). The Telekinesis task identifies a safe Engine interaction-preview dependency. These are known receiving requirements, not failures reproduced by this session. Their status and dependency completion remain live Den facts.

The breadth campaign and its region/content/promotion/endgame children exist in Den. Imported row counts do not establish that those tasks are complete. This run also does not establish complete quests, promotion paths, the light/dark choice, all services, every spell category, follower hiring, every travel link, respawn schedules, underwater/flying movement, secret discovery, the condition/death ladder, or late-game play. Existing repository evidence can support bounded mechanisms separately; it cannot be substituted for what a new player actually reached here.

## Interpretation and limits

A successful button response is evidence for that action and resulting state, not for the whole system. A refusal can be correct rule behavior. Conversely, a working owner behind a blank world does not prove usable first-person play. The report keeps those conclusions separate.

There was no matched original-game playthrough, numeric differential test, performance benchmark, or broad automated verification run for this assessment. The current importer build and deterministic write were exercised to make the genuine fresh start runnable. Existing checked-in test suites were not rerun merely to attach a green label to an observational report.

The screenshot comparison is structural. Original bitmap art, four private inventories and original keyboard shortcuts are not mandatory fidelity goals. The reference establishes recognizable information and interaction patterns; the repository design decides how to express them with shared inventory, current owners and original presentation.

## Evidence package and cleanup

The operator-local package is `local/assessment-20261002/`. It contains the illustrated report and comparison gallery, original-image copies with checksums and sidecars, observer reports, browser journals, current importer receipts, launch profiles, save provenance, and cleanup receipts. The manifest distinguishes original artifact IDs and paths from capture IDs; it is the evidence index to use instead of inferring identity from a filename.

The package currently indexes **105 product images and 12 online-reference browser screenshots**. Copies are byte-identical to the originals; no product UI was hidden or cropped. One action-returned PNG lacks a capture sidecar and is marked in the manifest/gallery; its frame identity is unknown. The gallery selects the important transitions and also exposes the complete product capture collection. The original-reference appendix includes all twelve GOG images and direct source links. Raw images, saves and operator-imported content stay outside Git according to the repository's evidence policy.

Six successful product browser sessions were used: default launch, customized fresh party, default-party UI/time pass, focused fresh town acquisition, and two short inspections of the town save resumed by the operator seam. Failed owned starts and online-reference sessions were also stopped. The final pool read showed no assessment session running; another product's active session was preserved. Only assessment profiles were removed, through reload rather than a shared service restart. The isolated checkout, current generated packs and ordinary town save were retained locally for reproducibility. Main gameplay source, default bundle and normal launch manifest were not changed.

This published record is text only so a clone can read the findings. The original images and exact revision/launch identities live in the local manifest; they are not pins in durable prose.

## Source anchors

- [README](../../README.md) and [live-check procedure](../live-checks.md): current no-world rendering and content-selection procedure.
- [Gameplay design](../gameplay-design.md) §§1, 3.1 and 3.11: expedition loop, shared inventory divergence and interface shape.
- [UI composition](../../src/ui/main.ts), [styles](../../src/ui/styles.ts), and [creation view](../../src/ui/creation.ts): panel order, sizing, internal scrolling, portrait text and name row.
- [Host guidance](../../src/PartyRpg.Host/README.md) and [product entry](../../src/PartyRpg.Host/CrawlerProduct.cs): declared entry, controls and operator fresh/resume selection.
- [Arena composition](../../src/PartyRpg.Rulesets.MightAndMagic7/MightAndMagic7Arena.cs): current challenger-placement requirement that refused stale packs.
- [Original reference card](https://www.mocagh.org/nwc/mm7-refcard.pdf): original creation, starter equipment/spells, HUD and options flow. The operator-supplied copy and repository manual outline provide the local reference.
- [Creation owner](../../src/PartyRpg.Kit/Party/PartyCreationFlow.cs): initial learned spells and created-party seed.
- [UI guidance](../../src/ui/README.md) and [automap view](../../src/ui/map.ts): thin projection-only presentation and the actual SVG map.
