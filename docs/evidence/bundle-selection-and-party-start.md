# Bundle selection and the party start, live

Live check for rusty-crawler#8657 (a bundle's selection decides what loads) and #8589 (a scenario says which
start a new session takes), on 2026-10-01. Procedure: [`../live-checks.md`](../live-checks.md). Readings are
`playtest.observe` answers and the host's own refusal sentences, trimmed to the facts that matter.

## Staging

- Host: `rusty dev --project src/PartyRpg.Host/PartyRpg.Host.csproj --port 4178 --bind-host <lan-address> --live-debug`
  from a worktree of its own.
- `mm7import write` from the operator's install into the worktree's ignored `content/partyrpg/imports`
  (`mm7-tables`, `mm7-world`).
- Two hand-written scenario packs beside them, never committed:
  - `live-start-a` — start in place 57 (Barrow III), `"party": "scenario"`, one member "Unselected", 111 coins.
  - `live-start-b` — start in place 3 (Erathia, entry `North Start`), two members "Selected One" and
    "Selected Two", 222 coins; no `party` word at first.
- The tracked bundle `partyrpg-default` named `mm7-tables`, `mm7-world`, `live-start-b` — not `live-start-a`.
  It was restored with `git checkout -- content/partyrpg/bundles` afterwards, the staged packs were removed, and
  the host was stopped.

## 1. Two scenario packs on disk, one selected, its start says nothing → creation

```json
{"mode":"creating","composition":{"bundle":"partyrpg-default","contentPacks":3,"partyStart":"creation"},
 "place":null,"party":{"present":false,"members":0},"screens":{"creation":true}}
```

Three packs loaded (the bundle's), the default start is creation, and nothing of `live-start-a` (its
`"party": "scenario"` and its party) reached the session.

## 2. The selected scenario says `"party": "scenario"` → its own party, its own place

`live-start-b/start.json` gained `"party": "scenario"`; the host replaced the runtime on the write.

```json
{"mode":"running","composition":{"bundle":"partyrpg-default","contentPacks":3,"partyStart":"scenario"},
 "place":{"id":"3","name":"Erathia","kind":"region","open":true},
 "combat":{"members":[{"name":"Selected One","hitPoints":40},{"name":"Selected Two","hitPoints":30}]},
 "party":{"present":true,"members":2,"hitPoints":70,"hitPointsMax":70},"screens":{"creation":false}}
```

No creation screen; the selected scenario's party in the selected scenario's place. The unselected pack's
place 57 and its member "Unselected" appear nowhere.

## 3. The unselected pack broken → refused, saying it is not selected

`live-start-a/start.json` replaced by `{ not json`. The start was refused (the debug route answered 503), and
the host logged:

```text
Rusty Crawler cannot start: live-start-a: 'start.json' is not valid JSON: ... [pack 'live-start-a' is not
selected: bundle 'partyrpg-default' does not name it; it was read from 'partyrpg/imports/live-start-a'
because every pack under the content root is judged at start]
```

The chosen behaviour is to keep validating the whole root (identity is the root's, so a broken pack beside the
selection is a contradiction the product holds) and to say, in the refusal, that the pack is not selected and
where it was read from.

## 4. Both scenario packs selected → refused by name, not first-wins

`live-start-a` restored and added to the bundle:

```text
The content this product selected states 2 scenario starts, and which place the party begins in would be the
order the packs happened to load in. The candidates are 'live-start-b' in live-start-b/live-start-b-start,
which begins the party in place '3'; 'live-start-a' in live-start-a/live-start-a-start, which begins the party
in place '57'.
```
