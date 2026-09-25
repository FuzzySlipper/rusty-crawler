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
