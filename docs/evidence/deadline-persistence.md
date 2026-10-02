# Owned deadlines across an explicit save

Recorded on 2 October 2026 in two local CoreCLR product hosts. A save with a live fatigue debt,
party light and member wards succeeded. A second host resumed the same stored bytes: fatigue landed
after its original due time, and the light and wards ended after their original ends.

`ClockSave` carries elapsed game milliseconds and the existing owner roster's pending schedules.
Each schedule names its kind, durable subject, member carrier where applicable, original due elapsed
milliseconds, and repeat interval where it repeats. Transient handles are rebuilt in registration
order by the same fatigue, running-effect and service owners on the one clock. Capture suspends or
cancels nothing. Load checks unknown kinds, past due times, duplicates, impossible repeats, missing
fatigue, unknown services and effects absent from their named carrier before restoring state.

The first successful session was `dd112b45-f4d9-48b2-bee0-e8ad47d03ec6`; the resumed session was
`76f66c1d-ebe5-47dd-84bd-6bd490d6afec`. Both ran the same gameplay source. The final gate's later
supplement changed only tests and generated coverage wording. Source and gate identities live in Den.

The imported Manor geometry, doors, events and people were retained. Staging relocated Party Start
to `(-832,-128,2)`, yaw zero, and omitted combat encounters. The scenario party supplied four members
with 30 health each, 100 provisions, and Nyx with Basic Fire rank two and the imported Torch Light
and Fire Resistance spells. Nyx's actual starting spell pool was 18, rather than the seed's requested
100. These are staging assists, not normal creation or an unmodified population claim.

The observer used ordinary visible hourly waits and DOM casts. Waiting rested nobody. Torch Light
was cast once; three Fire Resistance attempts resolved to Roderick, Aelina, then Aelina because a
select rerender changed the targeting. The resulting save therefore held two member wards. Nyx had
eight spell points left. The Save button reported a successful save to slot `session` at
`1168-01-02 08:11`. The observer's initial label expecting the former deadline refusal was corrected
in Den: admission with these owned deadlines is the required behavior.

After the first host was released and a fresh pool read found no leases, the parent read the actual
Engine storage container without changing it. Its product payload held the following values:

| Schedule | Carrier | Magnitude | Due elapsed milliseconds | Repeat milliseconds |
| --- | --- | ---: | ---: | ---: |
| Fatigue | party | — | 86400000 | 86400000 |
| `spell.light` | party | 2 | 90680500 | — |
| `spell.resist.Fire` | Roderick, member 1 | 2 | 90686500 | — |
| `spell.resist.Fire` | Aelina, member 2 | 2 | 90698500 | — |

The saved elapsed time was 83504000 milliseconds. The file and payload were 5242 and 5222 bytes;
the outer storage revision is Engine metadata, not a product schema version. The parent backed up
the profile and stored file, changed only the isolated launch selection from fresh to resume, and
verified the saved-file hash had not changed. No effect, pose or saved-state patch was made.

The resumed projection named the party as resumed. Startup had admitted time before the observer
held the runtime, so the first reading was 08:18, rather than the saved 08:11:44. The original sleep
due remained 09:00; light and both member effects still named 10:11 and magnitude two. Held,
action-driven time was established before gameplay. Ordinary waits then produced these readings:

| Reading | Admitted steps | Party condition | Running light and member wards |
| --- | ---: | --- | --- |
| Resumed, 08:18 | 839 | none, all four 30/30 | original ends retained |
| Before fatigue, 08:59 | 935 | none, all four 30/30 | light still named 10:11 |
| Five-minute wait, 09:04 | 947 | all four Weak (1), panel Tired | light still named 10:11 |
| Hour and two five-minute waits, 10:14 | 983 | Weak (1), all four 30/30 | light and member-effect entries absent |

The debt landed once when the wait crossed 09:00. The existing fatigue policy then displayed its
next sleep due on the following day at 09:04, the delivery reading; this observation does not claim
that the next period stayed anchored to 09:00. The live readings bracket the original ends rather
than sample an exact millisecond boundary. Focused round-trip tests separately check one millisecond
before and at the original party/member ends and fatigue due time, plus repeated fatigue and shelf
restock intervals. The ruleset tests exercise the restored member ward's actual owner readings.

Original resumed observe receipts are `3f5b64d9`, `928f07ad`, `c5fe3138` and `64fb58de`, at
10:33:00.677, 10:33:37.786, 10:33:57.415 and 10:34:35.262 UTC. Original DOM inspections in the
session journal retain the effect entries and the later empty effect list. The first save's captures
are `46ca55cd-2ff5-4c08-b011-1b4a1b66c02b` and `1ab363ee-92b4-4404-8fa2-7acb19ab294f`.

| Resumed state | Original capture | Browser frame step / sequence |
| --- | --- | --- |
| Before 09:00 | `172bdb1c-78aa-4410-92f5-9a9ef7524389` | 935 / 864 |
| After fatigue | `10664f97-2247-4934-9f81-ca3431eb1b42` | 947 / 868 |
| After effect ends | `2b32804c-f139-4db3-ba8b-e153494f8be6` | 983 / 880 |

Canonical live queries explicitly report frame correlation as not measured; the capture sidecars
name their own browser frames. The panel is over an empty world frame. Both successful leases were
released by the observer, with host and browser stopped and no source or staging edits. An earlier
startup failure could not locate dotnet and yielded no gameplay evidence; the parent corrected only
the isolated profile PATH after its lease was released.

The complete corrected gate passed all builds, UI 68, inventory and map decoding, deterministic
operator writes, Architecture 21, Kit 749, Host 85, ruleset 307 and importer 186 tests, with no skips,
and CoreCLR staging. Three lanes approved the implementation and its test-only supplement. Pending
fights and creature population keep the separate named refusal routed to #8658. Shelf stock and
buy-back lots remain transient: retaining restock deadlines does not carry those contents. This
reading makes no NativeAOT, broad Manor traversal, graphical-world or original-save compatibility claim.
