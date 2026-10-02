# Training time and recovery at an imported hall

Observed on 2026-10-02 in owned local session
`76f02fbc-2932-4701-af0b-5d57dcf9ad22`, using ordinary panel controls at Basic
Principles in Harmondale. Parent staging relocated its imported service placement
90 beside Party Start and removed creatures and other speakers for a quiet
check. Original structural placements, geometry, service definitions and rules
remained. The scenario supplied four healthy level-one members with sufficient
experience, Weak and Fear, 50,000 coins and ten portions. This does not certify
reaching the original town door or playing its encounters.

| Ordinary action | Visible result |
| --- | --- |
| First step, Roderick | Level 2, five skill points, 20 coins paid, purse 49,980. Rest for 191 hours 19 minutes to 1168-01-09 09:00; all four members restored. |
| Second step at the same counter | Level 3, five more points, 40 coins paid, purse 49,940. No additional rest sentence; the next sleep deadline remained 1168-01-10 09:00. |
| Leave, use the speaker again, ask to train, third step | Level 4, five more points, 60 coins paid, purse 49,880. Rest for 191 hours 5 minutes to 1168-01-17 09:00; all four restored. |

The final DOM reading retained 49,880 coins and ten portions, no conditions,
415/415 combined vitality and 31/31 magic. All four members were ready. The final
calendar read 1168-01-17 10:18 after further inspection; it is not the immediate
post-transaction time. Ordinary game time during inspection explains why the
reported rest periods were shorter than 192 hours. The service result displays
its actual rest duration and destination on the one session clock.

Original artifacts, with same-basename JSON sidecars and the inline action and
DOM receipts in this session's `events.jsonl`:

| Reading | Browser artifact | Broker capture |
| --- | --- | --- |
| Before first step | `c57c750a-511a-41a8-b7a7-61f257774d12` | `30a11dce-9635-4c09-8a4d-118242e6b4e8` |
| After first step | `c96858e2-7ec2-4674-937d-ff24241fe06b` | `bd5a3010-e85c-4dbc-acc0-a11afdf4adc4` |
| After second step | `2007935d-5fd0-4fc6-9901-e981e65c236b` | `eafd4a66-9cb9-47fb-b147-7b459e578091` |
| Before new-visit step | `32e5648b-be1a-4baa-ac91-931c65dce7b9` | `683d7488-130d-4b96-bb57-5cd102ec7514` |
| After new-visit step | `83780567-8ace-482c-9d92-675f253d353a` | `9489df01-fde5-4f98-952a-e603558e421b` |

This run had no separate per-step DOM calendar readings before and after the
first two transactions. Its result messages and captures establish the reported
rest and recovery; focused semantic tests establish the exact first-step-only
clock charge.

## Panel calendar supplement

A second owned session, `6db0f218-5ce0-4091-b498-60fd73c966ca`, used the same
source and staging and read the panel calendar around each ordinary transaction.

| Step | DOM calendar before | DOM calendar after | Result |
| --- | --- | --- | --- |
| First step | 1168-01-01 09:30 | 1168-01-09 09:04 | Level 2; rest to Jan 9 09:00; 20 coins paid. |
| Same visit | 1168-01-09 09:08 | 1168-01-09 09:16 | Level 3; no rest sentence or date jump; 40 coins paid. |
| New visit after leaving and returning | 1168-01-09 09:30 | 1168-01-17 09:04 | Level 4; rest to Jan 17 09:00; 60 coins paid. |

The short intervals include ordinary admitted game time while input and
inspection ran; they do not imply zero frame time. The result messages and
multi-day jumps agree with the first-step-only service charge checked in the
semantic suite. Weak and Fear cleared; final vitality and magic were full,
food remained ten, and the panel read four of four ready.

The original browser artifacts are `30d48ff9-eabe-45e5-aa60-e13370346f7f`
(first result), `82faff8e-d9b2-4bfa-a481-c4bf7acdec5c` (same-visit result),
`ac33ffea-7011-4c92-adc4-0276ea614c31` (new-visit result), and
`b6605a7f-5ae4-479e-b752-6f4eb3d234cb` (final observation), with their original
JSON sidecars. DOM clock receipt pairs in this session's `events.jsonl` are
`d46d3e23-c533-4483-9555-92b274b20e20` / `849ba04f-39c8-4258-a610-52837db8c8e9`,
`bf643f9c-f853-45e7-a21f-755758c03640` / `c0e4e97d-1167-4f39-9f0f-07980f3a225b`,
and `7d2c037d-9abf-4fba-9069-8185239c6228` / `3660da06-f656-4ded-b37b-9a04d198c38a`.
The final artifact identity above corrects an earlier observer transcription.

An unsupported PageDown diagnostic after the gameplay sequence delivered no
product action. The observer therefore did not separately scroll and read
every member row in this supplement; the four-member restoration result,
combined full pools, condition reading and readiness are the visible evidence.
The owned host and browser stopped and released successfully, with zero active
sessions afterwards.

The first attempted session, `98fe72d9-f322-418f-89d2-88950029e01b`, stopped at
startup because parent staging omitted an entrance-referenced floor trigger.
Its original fault capture and stderr were retained, the staging was corrected,
and it performed no training actions. Both that failed start and the successful
run released their owned host and browser; the pool returned to zero active
sessions.

The duration is adapted from `OpenEnroth/src/GUI/UI/Houses/Training.cpp:75-88`
and the next-dawn calculation in `OpenEnroth/src/Engine/Engine.cpp:1447-1450`.
The task deliberately charges only the first successful step of a visit; the
donor charges each increase in the maximum per-member count of levels gained.
The two deep halls' extra twelve hours and subsecond precision are semantic
checks, not observations in this Harmondale run. No NativeAOT or broader
traversal claim follows from this reading.
