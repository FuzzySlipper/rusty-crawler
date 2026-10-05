# Endgame interpretation and content gaps

Recorded for task 8515. The original imported events remain the source of the
quest chain, power puzzle, device reward and final hand-in. The product adds the
meaning of the final throne-room instruction and its visible epilogue.

| Source event | Product behavior |
| --- | --- |
| Global 149/159, then 150/160 | The Light/Dark prior quest chain leads to the shared completed-arc record 120. |
| Global 169/171 | The respective final request records errand 130/131 and changes the quest giver's topic to 170/172. |
| Lincoln 475 | Restores the ship's power through the map's existing state and changes its lights and panel texture. |
| Lincoln 376 | Requires that power, awards the Oscillation Overthruster (item 605), and records the one-time acquisition. |
| Global 170/172 | Consumes that item, updates the speaker's topic and world news, then reaches ending house 600/601. The product requires the matching path, prior arc and final request before settling these effects. |

These facts are checked against the operator-supplied imported event tables.
OpenEnroth `src/GUI/UI/UIHouses.cpp:297-300` handles houses 600/601 as game-over
triggers, and `OpenEnroth/src/Engine/Data/HouseEnums.h` names them as the good/evil
final-task completion houses. Donor code is behavioral evidence, not copied code.

The Dark throne-room regression runs the real Pit household event that moves Kastore, then the real Castle Gloaming door event that selects house 184. An event-selected household resolves its current residents through the same conversation owner, even when it shares another household’s physical door.

## Deliberate adaptation

The original movies are not reproduced. Each path receives its own authored
prose epilogue. This is an approximate ending, not cinematic or script fidelity.
The completion is a canonical party record, saved by the existing session save.
The final errand is removed from the active quest book on success. The party
continues exploring the same world after dismissing the epilogue; the Journal
can recall it, and resuming a completed save shows it again. There is no
additional completion reward or automatic world reset.

## Semantic evidence

`EndgamePolicyTests` uses the imported final quests and Lincoln placements. For
each path it checks a missing item, the unpowered device, power restoration,
one-time item acquisition, wrong-path refusal without item loss, successful
hand-in, distinct completion, removal of the final errand, and session save and
resume. Prerequisites are supplied directly in this regression; it does not
claim a whole-campaign playthrough. The DOM test checks epilogue dismissal,
Journal recall, menu hiding and a fresh session showing the earned ending again.

## Visible evidence boundary

The isolated browser evaluation did not reach a product scene. Its first host
build lacked the checkout's Node dependencies; after those were installed,
two later starts failed before allocation with `broker lease lock timed out
after 10s`. The failed owned session was stopped and its slot released; the
later failures created no owned host. There are no screenshots or gameplay
inputs from those attempts. Consequently the semantic evidence above does not
establish an ordinary end-to-end progression or visible ending acceptance.
The prepared scenario supplied a qualified Light party and prior quest records,
but supplied neither ship power, the Overthruster nor earned completion.
