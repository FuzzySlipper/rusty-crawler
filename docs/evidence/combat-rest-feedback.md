# The fight beside the world, the rest screen, and the answer line

Reading of rusty-crawler#9226, 2026-10-03 (America/Los_Angeles). Every capture is a crew-services browser session that
started its own `rusty dev --live-debug` host from the checkout, at 1280×720, with the world held between steps
(`assist time action-driven`) so each frame shows one moment. Screenshots stay in `local/evidence-9212/` (`9226-*`).
Controls were the ordinary ones: the bar's book buttons, a portrait click, the fight panel's buttons, and the host's
keys sent as declared controls (**B** attack, **F** save, and the walk keys).

## The ordinary new game

The default bundle and the default party.

**Rest screen** (the bar's **Rest** button):

- `9226-01-rest-options.png`: five options, each with the rest mechanism's own judgment before it is pressed.
  - **Rest & heal 8 hours** is refused and disabled: `The party stands in the open in Emerald Island: it makes camp
    here, or finds a roof.`
  - **Make camp**: `8 hour(s) · costs 2 portions`.
  - **Wait until dawn**: `19 hour(s) 58 minute(s) · costs nothing`. The wait for an hour and the wait for 5 minutes
    show their own lengths.
- `9226-02-camped.png`: **Make camp** applied. The clock moved from 09:01 to 17:01, the larder from 10 to 8 portions,
  and the screen's result says so.
- `9226-03-camp-refused-larder.png`: four camps later the larder is empty. The camp is disabled with `A sleep here
  costs 2 portions and the party's larder holds 0 portions.` The party woke Weak, from going without food.

**Answer line**, along the top:

- `9226-04-save-confirmed.png`: after **F**, `Save: Saved the session to slot 'session' at 1168-01-03 01:01.` Before
  this task a save showed nothing.
- `9226-05-use-answer.png`: walking from the start over a floor plate gives `Use: A fixture: nothing comes of it.`

**Fight panel**, beside the world:

- `9226-06-attack-nothing-in-reach.png`: **B** with nobody in reach gives `Attack · Roderick: Roderick attacks nothing in
  reach (melee) and recovers for 23.0s.` Roderick's portrait reads `recovering 20.0s`.
- `9226-07-press-while-recovering.png`: **B** pressed again too early gives `Attack · Roderick: Roderick is still
  recovering: 19.5s before they can act again.`
- `9226-08-fight-panel-struck.png`: closer, **B** on the townswoman. The top line reads `Attack · Roderick → Peasant:
  Roderick attacks Peasant (melee) … Peasant is at 0/6, and is down.` The fight panel opens beside the world:
  - `Real time`;
  - `Acting: Roderick — recovering 20.0s`;
  - `Target: nothing within Roderick's reach`;
  - Attack (B), Next member (N) and Turn-based (Enter);
  - the nearest five foes with distance, health and what each is doing, the struck townswoman crossed out as down,
    and `and 5 more`;
  - the last order.

  This frame is from the build before the control buttons were laid in one row.
- `9226-09-turn-based-from-panel.png`: the panel's **Turn-based** button switches the pacing: `Turn-based · round 1 ·
  Aelina's turn (yours)`. Aelina is selected to act, **Skip turn (K)** and **Wait (Y)** appear, and the switch now
  reads **Real-time (Enter)**.
- `9226-10-portrait-selects-recovering.png`: clicking Roderick's portrait makes him the actor, `Acting: Roderick —
  recovering 18.0s`.
- `9226-11-recovering-refused.png`: **B** is refused, `Roderick is still recovering: 18.0s before they can act again.`,
  in the panel and on the top line.
- `9226-12-off-turn-refused.png`: Borin selected by his portrait, ready but not on his turn. **B** gives `Borin is
  selected, but this turn belongs to Aelina; nothing was spent.`
- `9226-13-camp-refused-hostiles.png`: the Rest screen mid-fight. Camping is refused with `There are 8 hostile
  creature(s) within 5120 of the party, and it will not make camp with them near.`

## A member a night cannot restore

This check was staged. A hand-written `rc-dead-scenario` started a scenario party whose Knight is dead at Emerald
Island's Party Start, through the diagnostic `partyrpg-default` bundle; the bundle was restored afterwards.

- `9226-14-camp-names-dead-member.png`: **Make camp** is offered as `8 hour(s) · costs 2 portions` with `Not restored —
  Borin: Dead; sleep cannot restore this condition, so seek a temple cure`.
- `9226-15-camp-left-dead-member.png`: after the camp, `1 member(s) restored. Left as they were: Borin: Dead; …`.

## What changed to get here

- **One answer line.** The product publishes a `feedback` block: the answer to the party's latest act from whichever
  owner gave it, numbered, and naming the act, the actor and the subject. The line along the top shows that block
  and nothing else, so an older answer cannot stand in for a newer one. Before this, the line chose among the
  interaction's last use, the fight's last order while engaged, and a selection refusal. A sign read before a fight
  stayed on the line afterwards, and a save showed nothing.
- **A held attack waits.** **B** held in real time ordered an attack every update. While the member recovered, each
  update was refused, so the line showed `still recovering` instead of what the blow did. A held control now waits
  through the recovery. A fresh press, or a panel click, is still ordered and answered.
- **Readable sentences.** Combat sentences now read `recovers for 23.0s` and `misses, with a 44.12% chance to hit`.
  They used to read `must recover 23438ms of game time`, `the hit roll was 4483`, and `(spell: 2)`.
- **Rest judged in advance.** The rest mechanism judges each stop by the checks the stop itself runs, without moving
  anything (`PartyRest.Judge`). The stop controls are offered by that judgment, and only while the session would hand a
  stop to the rest mechanism at all: not while it is held, or while a counter or a conversation holds the controls.
- **What an attack would strike** is the fight's own answer (`CombatState.AimOf`), and the fight lists its foes nearest
  first.
- **Portrait labels.** A member's state line now sits under the bars whether or not the member has a spell bar.

## Limits

- **Not captured live.** The bar's pacing switch naming `Real-time` while paced, and a neutral selection line drawn in
  the plain colour, both landed after the run. They are bound by the UI suite (`fight-rest-feedback.test.mjs`).
- **The Rest screen covers the fight panel** while it is open, as every book covers the world. Its options use the
  screen's left half.
- **Mechanics left to the combat campaign.** Targeting is the fight's nearest-within-reach rule, and the panel shows no
  aiming of its own. A complete fight with loot belongs to the interaction and combat campaign.
- **Focus.** Keyboard focus returns to the game view after every panel claim and every change of screen, as before
  (`main.ts`, `frame.ts`). These captures drove keys as declared controls, so they do not test DOM focus by themselves.
