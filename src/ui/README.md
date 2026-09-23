# ui

Planned product DOM companion, authored in TypeScript and compiled into
generated output by the host project's build.

Owns, once implemented: thin DOM presentation of Engine-delivered projections
and the semantic actions a player takes on them (party sheet, inventory and
equipment, spellbook, journal and quests, NPC dialogue, shops and services,
selection and menu surfaces).

Boundary rules:

- No gameplay state, no rules evaluation, no game-world rendering, no transport,
  and no game loop. It renders what the product publishes and reports intents
  back.
- Generated output is build product and stays ignored; never edit it by hand.

Nothing is implemented yet.
