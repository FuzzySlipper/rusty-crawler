# The creation screen

Reading of rusty-crawler#9221, 2026-10-03 (America/Los_Angeles). Each crew-services browser session started its own
`rusty dev --live-debug` host from the ordinary new game, and every image is the Engine runtime's own frame with the
DOM companion over it, at the supported desktop viewport of 1280×720. Screenshots stay in `local/evidence-9212/`.
Every choice was an ordinary click or key on the screen; no scenario party stood in for creation.

## What a player sees

- `09-creation-default.png` — the new game opens on **Make your party**. The four members stand across the top as
  cards with their installed faces (granted through the Engine, as on the adventure bar), name, race and class, step
  and points left; the member being made is outlined. Below, on the left, the eight faces (each deciding the race),
  the nine classes and the member's skills (fixed, chosen and on offer); on the right the name box and the
  attributes with their race's range and − / +; beneath, **Reset member**, **Restore default party**, **Confirm
  step** and **Accept party**.
- `10-creation-custom.png` — the second member, reset and remade through every step: Dwarf woman, Paladin, the name
  Hilde typed into the box and sent with **Enter**, all 50 points spent (Intellect and Personality stopped at the
  race's 25 with the flow's own sentence, `Personality is already 25 and creation raises it at most to 25 for this
  race.`), and Sword and Shield chosen beside the class's fixed Mace and Spirit. A choice made out of step is refused
  in the same line (`Spending attribute points happens at the Attributes step, and member 2 is at the Name step`).
- `11-custom-party-hud.png` — **Accept party** put Roderick, Hilde, Borin and Nyx on the adventure bar in Emerald
  Island, Hilde with her face and her Paladin's spell bar.
- `12-creation-reset-refused.png` — in a fresh new game, **Reset member** cleared Roderick to an unnamed member with
  no face and 50 points, and **Confirm step** was refused by name: `A character is created with a portrait; choosing
  one is what decides its race.`
- `13-creation-restored.png` → `14-default-party-hud.png` — **Restore default party** put every member back as the
  ruleset offers them, ready to accept, and **Enter** on the focused **Accept party** button accepted it into the
  world with the default four on the bar.

## What the product publishes for it

The creation block's members and offered portraits each carry the face image URL (`portraitImage`, `image`) the
session's portrait images granted, empty where content gives none (the card then shows the member's initial). Two
creation actions were added: `creation.reset-member` begins the member being made again, and
`creation.apply-default` restores the ruleset's default party through the same validated steps; the latter's
button is shown only when the ruleset offers a default. The name box keeps what the player is typing until the
member or that member's published name changes.

## Limits

- Only the 1280×720 viewport was read live. Below 760 px wide the choices and the sheet stack in one column; that
  arrangement is checked by its style rule, not by a live reading.
