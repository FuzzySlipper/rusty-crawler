# Imported door collision and traversal

Recorded on 2 October 2026 in a local CoreCLR product host. The party opened the actual imported
Manor fixture with ordinary controls and walked through its doorway. Before opening, the same
doorway stopped ordinary forward movement. Closing and the return obstruction were checked in
the real Engine over authored geometry, rather than claimed as a live Manor action.

The final live session was `27442188-4e21-4bfb-898b-ecf4bbdfc65a`. The operator's original Manor
geometry, door definitions, fixture program and people were retained. Staging relocated Party Start
to `(-832,-128,2)`, yaw zero, and omitted combat encounter placements. Normal creation supplied the
four members with 120 total health. The unused scenario-party document supplied no health to this
run. These are explicit staging assists, not an unmodified population or broad traversal claim.

The actual fixture stands at `(-640,-128,0)`. Its program first opens the Guard conversation and
records the first use; a subsequent use turns the local actor group hostile and opens doors 3, 4, 1
and 2. The observer used ordinary W and G, and the existing Leave button. No event, target pose or
party state was patched during play.

| Observation | Admitted steps | Simulation seconds | Party feet | Result |
| --- | ---: | ---: | --- | --- |
| Normal creation accepted | 11 | 0.183 | -832, -128, 2 | Door fixture ready, 192 units away |
| Forward against closed door | 48 | 0.800 | -695.133, -128, 2 | Stopped on the closed side, fixture 55.17 units away |
| First G | 57 | 0.950 | -695.133, -128, 2 | Guard conversation opened |
| Leave | 69 | 1.150 | -695.133, -128, 2 | Conversation closed |
| Second G | 528 | 8.800 | -695.133, -128, 2 | Applied outcome reports four doors opening |
| Forward through opened door | 962 | 16.033 | -496.735, -128, 2.120 | Past the door plane, grounded, movement unblocked |

All four member poses remained the shared party pose, all health stayed full, and no hostile creature
was present. The incidental outcome before deliberate G was “A fixture: nothing comes of it.” The
opening outcome then explicitly reported the actor-group change and four doors opening. The ordinary
input windows also admitted setup time; the table does not claim exact action durations.

Original observe receipt identifiers, in order, are `cb4e996d`, `6a7fff62`, `624cfed0`, `265a48e6`,
`ec8314a5` and `ac82db3f`, at 08:41:42.808, 08:41:54.634, 08:42:06.589, 08:42:16.195,
08:43:42.145 and 08:44:03.943 UTC. The canonical live queries report their frame correlation as
not measured. Browser captures and sidecars are retained separately with Den's task evidence:

| State | Original capture | Browser frame step / sequence |
| --- | --- | --- |
| Closed side | `d4c49e2a-ce40-43d9-9011-dd5134de514a` | 986 / 975 |
| After opening | `70fdcc7f-7073-42ef-b846-b985e0eb67eb` | 1466 / 1447 |
| Through doorway | `fafb3659-3f34-4b50-a6dc-866240ae6f25` | 1900 / 1883 |

The capture identifiers do not establish that a live query sampled that same frame. The panel sits
over an empty rendered frame; this is an ordinary-control traversal backed by canonical running-world
poses, not a graphical wall demonstration. The observer stopped its owned host and browser, confirmed
the PID was gone and the pool had no active leases, and made no source or staging changes.

The collision path uses one canonical interaction ledger. Per-corner door travel and passability
project the offline partition into complete Engine collision replacement, followed by navigation
derivation in the same scene. Source state zero is open at rest; state two is closed at full travel;
intermediate states settle closed without animation. The donor displacement reading is OpenEnroth
`src/Engine/Graphics/Indoor.cpp:591-614` and `src/Engine/Graphics/Indoor.cpp:665-671`.

Across the operator's install, deterministic writes identify all 786 doors in 76 geometries and
55,851 addressable faces. Raised events and passability cog groups remain distinct. The importer's
existing clicked-face cluster assignments identify each fixture's own surface, so neither another
cluster sharing its event nor a pressure plate can claim that surface. Engine supplies the nearest
collision hit and its instance identity; intervening geometry still obstructs interaction.

Focused native checks cover closed/open/closed traversal, passability restoration, canonical ledger
save and re-entry, corner membership, unrelated-use caching, actual imported fixture visibility and
separate same-event fixture obstruction. Endpoint settling and event interpretation are approximate;
this reading makes no original animation, full Manor traversal or NativeAOT claim.

The final full verification passed all builds, UI checks, inventory and map decoding, deterministic
operator writes, Architecture 21, Kit 746, Host 85, ruleset 306 and importer 186 tests, with no skips,
and CoreCLR staging. The actual Engine admitted all 76 partitions at their initial state and with all
doors open: 152 admissions, minimum 326 initial and 328 opened navigation cells, no omission or refusal.
All three review lanes approved the final source. The gate and exact source identities live in Den.
