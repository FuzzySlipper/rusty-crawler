# Actor group news

Recorded for Den task #9150 (provenance only). The source was the operator's
Might and Magic VII English Update 1.1, GOG build 1207658916, container fingerprint
`8633b4033a5a7353`. Original text and converted assets are not included here.

The fresh importer output contains 51 group assignments, 51 news rows and 48
`set-npc-group-news` instructions. The numeric operands, event/step identities and
normalized document digests are in [group-news-inventory.json](group-news-inventory.json).
An independent read of the actual archive instructions verifies both operands against
the actual source tables; global event 3 step 12 assigns group 1 to news 5.

Donor semantics were checked in OpenEnroth:

- `src/Engine/Evt/EvtInstruction.cpp:1149-1152`: a 13-byte record with two unsigned
  32-bit operands.
- `src/Engine/Evt/EvtInterpreter.cpp:525-526`: assignment of the news row to the group.
- `src/Engine/Tables/NPCTable.cpp:96-112`: zero-based group and localized news tables.
- `src/Engine/Graphics/Viewport.cpp:186-200`: friendly unnamed actors speak their
  group's news; named NPCs use their individual dialogue instead.

The runtime uses imported definitions, the existing conversation/interaction owners,
and party records. It retains no second NPC registry. The canonical fight and presence
owners decide whether an unnamed actor can speak. Zero news silences a group. A later
refused event step settles neither earlier news nor an earlier follower join.

Validation:

- Five focused importer cases passed, including unsigned high-bit values, truncated
  operands, normalized event emission, actual JSON pack fields and the source-table check.
- All 470 ruleset cases passed against the fresh import, without skips. The topic sweep
  applied all 365 topic-raised events for a fresh party; three travelled. This is the
  fresh-party branch of each event, not exhaustive coverage of every campaign branch.
- The composed conversation regression checks before/after text, zero news, hostility
  withholding, invalid save identities, current source-generated party JSON round-trip,
  and later-step refusal preserving news and follower state.
- All 108 host and 21 architecture cases passed. The preceding full importer run passed 213 cases;
  the added focused cases were then run separately against the final changes.

Reproduction: set `CRAWLER_OPERATOR_INSTALL` for the source-archive importer test and
`CRAWLER_IMPORTED_CONTENT` to the parent of `partyrpg/imports` for the ruleset tests.
The importer command remains `mm7import write --install <install> --output <root>/partyrpg/imports`.
This record establishes imported/runtime/save behavior through the real owners; it does
not claim an ordinary browser walkthrough of the affected story events.
