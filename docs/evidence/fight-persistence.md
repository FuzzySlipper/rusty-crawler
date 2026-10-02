# A resident fight across an explicit save

Recorded on 2 October 2026 in two local CoreCLR hosts. Ordinary attack engaged an imported Manor
Fighter, ordinary Save produced stored bytes carrying its provocation and recovery, and a second host
resumed those bytes with the same hostile creature and health. The party died before Save; this is
bounded evidence for a resident fight and creature recovery, not a living-party recovery demonstration.

`CombatSave` names members by durable identity and creatures by content placement and ruleset kind.
It carries actual poses, health, recovery, provocation, effects, created origins and remaining lives,
explicit current-visit absences, bodies with held yields and death incarnations, attack cursor, pacing
and turn bookkeeping. The existing population, health, effect, corpse and turn owners restore it;
restoration advances no time and rerolls no death or loot. A missing resident placement, unknown kind,
excessive recovery, contradictory body pose, omitted resident record or impossible turn is named in
the aggregate load judgement before any owner is rebuilt. A legitimately absent resident is recorded
explicitly, rather than inferred from an omitted creature or historical world flags.

Staging retained imported Manor geometry, doors, targets, events and triggers, and the original
Fighter kind 61 encounter at `(-896,160,0)`, grade A, count one and zero spread. Other six combat/person
placements were omitted. Party Start was relocated to the previously observed grounded point
`(-893,156,2.13333)`, yaw 1842. The observer accepted the ordinary default creation party, with health
maxima 40, 20, 25 and 35. No creature hostility, combat policy, health table or saved bytes were edited.
The startup debugger assist had no attached debugger; Engine time was held for inspection and bounded
real-time windows admitted ordinary keys. The UI is a DOM panel over an empty frame.

The first successful save host was `d1601238-5335-459a-bb84-e3ac267cdb4d`. Physical B engaged the
Fighter: the live receipt showed it at 278/280 HP, waiting, while all four members were recovering and
alive. Enter changed pacing, but the admitted observation interval killed all four before a player
turn. Physical F and a DOM Save click followed. Original capture `12d73fb6-b04b-4b47-af90-f66a51b301a6`
visibly reported “Saved the session to slot 'session' at 1168-01-01 09:26.” A later query read 09:29;
that later clock is not the saved moment. Earlier attempts that returned browser transport success
without a Saved result or stored file are recorded as unadmitted, not as successful saves.

After release and a fresh empty-pool reading, the parent inspected the actual Engine storage container
read-only. The file was 6687 bytes, with a 6667-byte product JSON payload. The outer storage revision
is Engine metadata, not a product schema version. The saved values were:

| Fact | Stored value |
| --- | --- |
| Clock | 1595000 elapsed game milliseconds, 1168-01-01 09:26:35 |
| Place and party pose | Manor 17, `(-893,156,2.13333)`, yaw 1842 |
| Creature content identity and kind | `monster/monster-0-0`, kind `61` |
| Creature health, provocation and recovery | 278, true, 13750 milliseconds |
| Attack cursor | 88 resolved attacks |
| Pacing and turn | turn-based, no current turn or active round |
| Party | four Dead members, zero HP and zero member recovery |
| Visit absences and corpses | empty |

Only the isolated launch selection then changed from fresh to resume. The old deadline mission's
separate stored bytes were preserved. No save was patched or issued during resume.

Resume host `d19d3800-f164-47c8-955f-c5332ba64c1e` visibly named the party as resumed. It retained the
same grounded party pose and original Fighter pose, hostile engagement, 278/280 health, turn-based
pacing and all four Dead members. Startup advanced the clock to observed 10:15 before the observer
held it. This is not zero-time live load precision, and the creature's intervening attacks cannot be
inferred absent merely from matching health. Focused round-trip tests separately check exact saved
recovery and provocation before any admitted advance.

The held original `57f50b4c-efc5-4083-8a44-229b04797142`, frame step 5857, showed the Fighter recovering
11.3 seconds. A 250-millisecond Engine advance admitted 15 steps, or 7.5 game seconds at the existing
clock scale. Original `0236f4a3-8f33-430d-8fc6-785c82d57eb3`, step 5872, showed recovery 3.8 seconds.
The hostile identity, pose and 278/280 health stayed unchanged. These rounded panel values demonstrate
recovery continuing on the one clock, rather than being permanently ready or a DOM countdown.
The panel's “nothing is being fought” turn sentence described the absence of a round despite the
engagement shown immediately above it; no active player turn is claimed.

The originals, receipts, action journals, host logs and exact source identities are indexed in Den
reports 37292 (first save), 37297 (parent byte reconciliation) and 37302 (resume). First-save receipts
are `20261002T143217.115-observe-bfcf98f9.json` and `20261002T143315.145-observe-0dc8a84a.json`;
resume receipts are `20261002T144405.521-observe-034637d1.json`,
`20261002T144442.338-advance-38483e57.json` and `20261002T144446.798-observe-726da24c.json`.
Both leases were released; final resume cleanup closed the browser, stopped its host and left all ten
slots free. This historical cleanup does not authorize ending later or unrelated sessions.

The final full gate passed all builds, 68 UI tests, 21 architecture tests, 756 Kit tests, 85 Host tests,
335 ruleset tests and 189 importer tests with no skips, plus operator inventory, map decoding,
deterministic pack writes and CoreCLR staging. Three source review lanes accepted the implementation.
The focused cases include real-time and paced recovery, event-created creatures, summons and raised
bodies, held body yields, legitimate visit absences and named contradictory load refusals. No NativeAOT,
rendered world, broad imported traversal, living-party live save, or completion of all residue tasks is
claimed by this record.
