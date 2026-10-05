# Joined follower profession benefits

Recorded on 5 October 2026 for Den task rusty-crawler#9151 (provenance only). The mechanism and its limits are
stated in the ruleset README under *Joined profession readings*; this page records what was checked.

## Sources

The terms were read from OpenEnroth: profession identities and declared benefits in
`OpenEnroth/src/Engine/Objects/NPCEnums.h:26-87`; presence, counted once per profession, in
`OpenEnroth/src/Engine/Objects/NPC.cpp:53-65`; Luck in `OpenEnroth/src/Engine/Objects/Character.cpp:754-760`;
the Enchanter's resistance term in `:1953`; experience learning in `:624-639`; skill terms in `:2398-2531` with
`getActualSkillValue` (a positive level reads at least Novice) at `:2534-2541`; foot-travel reductions in
`OpenEnroth/src/Engine/Party.cpp:1003-1013`; the reputation penalty in `:824-834`; the two places that suspend
every profession in `OpenEnroth/src/Engine/MapEnumFunctions.h:29-31`. The operator's own `npcprof.txt` (read
offline from `Events.lod`) gives the Fool a 100-gold fee and the Enchanter a 1000-gold fee, with the join and
dismissal words used below. No donor code or game data is copied.

Faithful in amount: Luck (Fool 5, Chimney Sweep 20, Psychic 10), resistance (Enchanter 20 on all seven magical
kinds), learning percentages, and the skill terms at the readers the ruleset README names. Ours: training,
learning a spell and spell mastery requirements read the purchased skill only, and the skills page shows
purchased levels.

## Focused checks

`tests/PartyRpg.Rulesets.MightAndMagic7.Tests/FollowerPolicyTests.cs`, through ordinary conversation hire and
dismissal in a composed session:

- Fool, Chimney Sweep and Psychic raise `ActualAttribute` Luck by 5/20/10; an Enchanter raises the actual damage
  plan's resistance by 20. The character page shows the same values and names the companion in the row's detail
  (`companions: Guide +5`). A current save resumed into a new session reads the same Luck, resistance and page row
  from the saved identity alone. Dismissal removes each reading at once.
- Sacrifice of a hired Fool removes its Luck immediately.
- Teacher and Instructor change real experience awards without touching purchased skills; dismissal restores the
  smaller award.
- Two Traders count once at the service quote; departure of both restores the quote.
- Scout and Locksmith let the party find and disarm a trapped container that springs after they leave.
- A Monk raises armour class through lent Dodging and an Apprentice the Fire school's level a spell is cast at;
  purchased skill entries never change and both readings return on dismissal.

The focused class passed 11 cases.

## Live reading

A private CoreCLR host served this branch with the operator's freshly written packs. A local variant of the
tables pack added two authored travelling people, "A travelling fool" (profession 27) and "A travelling
enchanter" (profession 37), 180 units in front of Emerald Island's Party Start, with the operator table's fees and
words; a scenario fixed a four-member party with 3000 gold. Imported people carry no hireable professions, so
the people are authored: this is not a generated town population claim.

Owned playtest session `ab5b64f2-82c3-4e17-883c-e19c7dde4082` (browser backend, slot 2). Ordinary controls:
New Game, Character (I), Use (G), the conversation's people and topics, Take your leave, and the companion rows.
The root opened every original capture listed.

| Ordinary action | Capture | Visible result |
| --- | --- | --- |
| Character book before hiring | `607e9be8-1a06-4ed5-a46b-27251895f578` | Ardin: Luck 10; Fire, Air, Water, Earth, Mind 0; Body 5. |
| Use (G) on the people | `d3b9e1ee-3c7a-4af9-a57c-815036c090a3` | Conversation with the fool; the enchanter selectable; "Join the party (100 gold)". |
| Hire the fool | `3870b201-d8f2-4fc2-ba7f-465638a1be64` | Join words; purse 2900; companion row added. |
| Hire the enchanter | `276d9f34-d0be-4b74-ae2b-36d28d381f08` | Join words; purse 1900; two companion rows. |
| Character book | `0c0786ed-e7a8-454c-a9c4-c17ae938da2f` | Luck 15, the five elements 20, Body 25, each changed row drawn in the page's changed colour. |
| Companion row, Leave the party (enchanter) | `9bc7065b-b804-41a7-a974-ae632d14e48e` | Dismissal words; one companion row left. |
| Character book | `d1308919-c3d6-4ac3-9667-7581a117deb1` | Resistances back to 0 and Body 5; Luck still 15. |
| Companion row, Leave the party (fool) | `fceb7d2b-b194-4664-af0f-9a026a933803` | Dismissal words; no companion rows. |
| Character book | `7b730535-12aa-47bf-80be-fc99ac11de22` | Luck 10; resistances as before hiring. |

Limits of this reading. The row detail naming the companion is a hover title; the headless captures do not draw
native tooltips, so the name is established by the focused page check, not by an image. A companion row opened
its conversation only with a short click: an 80 ms press did not register because the row is redrawn under the
pointer, which is a presentation defect outside this mechanism. No save, resume, fight, award, trade or trap
was exercised live; those are established by the focused checks above. Cleanup released the slot.
