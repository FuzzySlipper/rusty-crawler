# The adventure frame

Reading of rusty-crawler#9220, 2026-10-03 (America/Los_Angeles). Each crew-services browser session started its own
`rusty dev --live-debug` host, and every image is the Engine runtime's own frame with the DOM companion over it, at
the supported desktop viewport of 1280×720. Screenshots stay in `local/evidence-9212/`. The default launch is the
ordinary new game; the party is the default one, accepted with **Accept party**.

## What a player sees

- `01-adventure-frame.png` — the world fills the view. Along the bottom are the four members with their actual faces
  (the installed face sets, granted to the panel through the Engine), hit-point and spell-point bars at the
  percentages the product publishes, the selected member outlined, the purse, larder, date and time, the adventure
  controls with their keys (Use, Attack, Next, Turn-based, Save), and the books. Down the right are the place's name
  and the automap.
- `03-world-before-book.png` → `04-character-book-I.png`, `05-spellbook-L.png`, `06-journal-J.png`, `07-map-V.png` —
  **I**, **L**, **J** and **V** open the Character, Spellbook, Journal and Map books over the world, with the bar and
  the automap still in view; the open book's button is marked. The books hold the existing sections until their own
  screens land (#9221–#9226).
- `08-escape-to-world.png` — **Escape** closes the open book and returns to the world. Every change of screen hands
  the keyboard back to the game view, and the host's own keys work over any screen: held **W** walked the party on
  straight after **Accept party** and after a book was opened and closed, with no click on the world.
- `02-fight-beside-world.png` — after **B** struck a townswoman, the town turned hostile: the fight's panel stands
  beside the world while something fights the party, and only then; the portraits stay in view. The line along the top
  names what the party faces (`The body of Peasant — search · 263`).
- A click on a portrait selected that member (Borin, outlined), through the product's own `party.select-member`.

## What the product publishes for it

The party block gains a `roster`: each member's identity, name, class, portrait and its granted image URL, pools,
the percentage each bar is drawn at, conditions and whether they are selected. A member's readiness is the fight's
own reading of that member. Nothing on the frame is worked out by the panel: the companion suite still fails any
module that combines a published quantity.

## What is kept, and what is not

Which book is open is presentation only: it is not saved, and opening one asks the product for nothing. A contextual
screen — party creation, a conversation, a counter — is the product's and shows exactly while the product holds it
open; its own controls leave it, and opening one closes any book behind it. Every fact and session control the product publishes is still drawn, in a
diagnostic panel shown by its button or the backquote key and hidden by default (shown at once when a product without
content gives setup guidance).

## Limits

- The books' contents are the existing sections, unchanged, until #9221 to #9226 give each its own screen.
- A conversation was not reached live in this reading: the town's bard had wandered from the well. The swap to the
  conversation screen, and books waiting until it is left, are bound by the companion suite.
- The line along the top shows the last thing a use said even when it is old; telling current feedback from stale
  feedback is #9226's.
- The game clock keeps running while a book is open, and the party can be walked under one.
- The bars are one colour: how a game shades a bar by its share is a reading the product does not publish yet.
