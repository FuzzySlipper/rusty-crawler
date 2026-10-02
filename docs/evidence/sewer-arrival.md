# Sewer arrival and callback diagnostics

Reconciled on 2 October 2026. This record distinguishes the earlier live retest from a later observed
callback fault; it does not claim a new sewer traversal.

## Signed arrival and continued play

The 30 September observer record in Den reports an ordinary held-W entry from Erathia into The Erathian
Sewers after imported packs were regenerated with the signed-coordinate reader. The start was a staged
resumed save at `(-2175,15050,26)`, facing 1536, about 194 units north of entrance `148.6025`.
The host used `rusty dev --live-debug` with the playtest service's browser and a working RADV adapter;
the corrected launch record says it did not use `--headless`.

The panel reported place 23 at `(6429,3511,-511)`, yaw 1024. History gained “Entered The Erathian Sewers”
on 1168-01-02. The session continued to 3,447 admitted steps / 57 simulated seconds, about 35 seconds
after arrival, with ordinary diagnostics and no runtime taint or exception in the host log. The retest
is an existing observer report, reused here without claiming newly inspected original screenshots or a
repeat on today's pair. The separate Engine-adoption task had already reviewed this report.

The owning importer reads every MoveToMap coordinate with signed little-endian Int32 in
`src/MightAndMagic7.Import/Events/EvtInstruction.cs`. The regression
`A_map_move_below_the_destination_s_zero_height_is_a_negative_height` in
`tests/MightAndMagic7.Import.Tests/TableAndEventTests.cs` checks -511 and a negative x. The latest full
Crawler gate passed all 189 importer cases, all other suites and CoreCLR staging, with no skips.
This supplies current source/check coverage without turning native admission into a live transition.

## An escaped callback names the defect

A later local CoreCLR observer session, `b087853c-0007-4c18-a2fb-ae38b192146f`, supplied a real fault
on the installed pair. Its initial neutral observation had zero admitted steps and an ungrounded Manor
arrival. The first bounded 200 ms advance faulted before gameplay. The host stderr reported:

> rusty: product update faulted: CSHARP_PRODUCT_CALL: Spatial.ProposeCharacterStep returned status 0: EngineCallException

The same line included the exception message “Rusty Engine Spatial.ProposeCharacterStep returned
status 0”, the concrete unresolved-controller-penetration result with depth `13.7290125`, and the
origin `PartyRpg.Kit.Movement.PartyMovement.Step` at its character-step call. The browser subsequently
reported a faulted runtime lifecycle rather than the old opaque runtime-tainted diagnostic. Thus the
report names the update operation, exception type, message and originating call. The initial zero-step
receipt and first-advance failure identify which admitted attempt faulted; the stderr line does not
claim a numeric update index or a complete stack trace.

This remains an infrastructure-error observation, not a gameplay acceptance or an intentional new
fault fixture. The observer released its host/browser. The installed Engine owns callback capture and
fault reporting; Crawler added no second exception bridge, loop or downstream workaround. A read-only
check of the paired Engine's runtime found its fault-line test preserving exception detail and the
`update` operation in the emitted diagnostic; no new upstream tests or build were run for this record.

The combined evidence covers signed sewer arrival/history and concrete callback diagnostics through
their existing owners. It makes no broad sewer traversal, rendered-world, original-population,
normal-creation, NativeAOT or original-save claim. Exact revisions, Den report identities and operator
artifact paths remain in Den.
