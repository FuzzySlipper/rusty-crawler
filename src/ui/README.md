# ui

The product DOM companion, authored in TypeScript and compiled into generated output by the host
project's build.

Implemented today: `main.ts` exports `mountProductUi(root, context)`, renders the session projection
(composition title, session mode, admitted simulation), and claims the pause/resume action when the
button is used. `tests/PartyRpg.Ui.Tests` exercises it under jsdom.

Still to come: the party sheet, inventory and equipment, spellbook, journal and quests, dialogue,
service screens, and menus — each arriving with the stone that gives it something real to show.

Boundary rules:

- No gameplay state, no rules evaluation, no game-world rendering, no transport,
  and no game loop. It renders what the product publishes and reports intents
  back.
- Generated output is build product and stays ignored; never edit it by hand.

The panel also reports the last admitted movement step: whether the party is grounded or airborne, what
blocked it when the engine refused the displacement, a step the engine accepted, and the cost of a fall
the tuning priced. Those values come from the movement owner through the projection; the DOM computes
nothing about movement quality.

It reports what the party faces and what using it did on the same terms: the focused target's kind, name,
verb, state and distance, the reason the reticle holds or refuses it, what it requires, and the last use's
outcome with its code, sentence, and the residue a use could not deliver. The use button claims the
product's `party.use` action and is offered disabled while the session holds no interaction or faces
nothing, so a control that cannot work never looks like one that can.

It reports the fight on the same terms: whether anything is hostile, each of the party's members with the
readiness the product published, the actors fighting them and how far off they stand, and what the last
order did with its code and sentence. The attack button claims the product's `party.attack` action and is
offered exactly when the product would take the order: outside a paced round it is disabled while no member
may act, and inside one it follows the party's own turn, where a press is the turn and the product's refusal
is what the panel shows. It reports the pacing on the same terms too:
which pacing the fight is in, the round and the phase, whose turn it is and what that actor still owes, and
the order the fight will act in with each actor's remaining recovery — and the pace controls claim the
product's `combat.turn-based`, `combat.turn-skip`, and `combat.turn-wait` actions, offered while a round is
under way because outside one there is no turn of the player's to pass. The panel runs no
countdown of its own — a recovering member shows the game time the product published and nothing ticks on
screen — because a screen that timed recovery itself would show a character ready before the fight agreed.
An actor the fight has laid out reads as down rather than as recovering, because being out of the fight
outranks the recovery it still owes.
