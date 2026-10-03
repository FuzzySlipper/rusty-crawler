# The journal and the automap

Reading of rusty-crawler#9225, 2026-10-03 (America/Los_Angeles), in a crew-services browser session over its own
`rusty dev --live-debug` host at 1280×720. Screenshots stay in `local/evidence-9212/`. The party is the default one,
accepted on the ordinary new game and walked a short way north.

## What a player sees

- `38-journal-quests-empty.png` — **J** opens the Journal on its first book. Its tabs are the product's books in the
  original's order — Current Quests, Auto Notes, Maps, Calendar, History — and a new party's quest page says the
  product's own standing (`The party has been offered nothing.`) and what a player does next.
- `39-journal-history.png`, `40-journal-calendar.png`, `41-journal-maps.png` — History lists `Entered Emerald Island`
  at 1168-01-01 09:00 (`1 entry`); the Calendar is the calendar book, the product's reading of the one clock (today and
  the time, day or night); Maps lists the places known, the visited one marked, with **Open the automap**.
- `42-automap.png` → `43-automap-zoomed.png` — **V** opens the automap: the place, the squares walked (`127 of 16384`),
  the walked area and the party's facing marker. **+** draws it larger in steps (×2, ×4) inside a scrolling frame and
  scrolls the party's marker into view; **Find the party** does so again; **Escape** returns to the world.

The companion suite proves the quest page on the product's fixture: a quest with its giver by name
(`Given by Lord Godwinson`), its note, each objective with how far it has come (`Slay the guards — 1 of 3`), a later
projection marking it met without the page keeping any count of its own, and the quest owner's refused turn-in at the
head of the page; and every book's empty state.

## What changed underneath

The quests block names each giver through `IGameNames.PersonName` (this game reads the people table), and a journal
book of one entry says `1 entry`.

## Limits

- No quest was accepted live: the opening offers no reachable errand yet, which #9231 owns (its acceptance requires
  accepting a real initial objective through conversation and a journal entry that updates from actual state).
- The calendar shows the calendar book's rows; deadlines appear there as the product publishes them.
