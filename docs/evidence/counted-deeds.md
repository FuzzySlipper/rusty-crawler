# A deed can accumulate

`PartyRecords` already holds durable counts apart from timed effects. `Increment` now adds a positive
earned amount through that existing owner. A quest reward explicitly states `accumulate: true` to add,
while ordinary record rewards still replace their stated magnitude. `PartyQuests.TurnIn` is the writer:
a refused or already finished quest pays nothing. The whole declared count payment is judged before
experience, coin or custody changes, so exceeding the durable integer range gives a named refusal
without partial settlement. An ambiguous authored counting word is a named content defect.

The town-hall path is real: a monthly quest identity names its counter, year and month, its kill
objective reads the existing death report, and its once-only turn-in adds the bounty's gold payment to
`award:bounties`. This counts gold, not visits or the number of hunts. The donor's town hall increments
`uNumBountiesCollected` by its paid gold and clears the month's killed target after payment
(`OpenEnroth src/GUI/UI/Houses/TownHall.cpp:147–173`). This product retains its existing approximation:
the place's encounter list chooses the monthly beast rather than the donor's random huntable set,
and its existing reputation gate still applies.

No actual arena exists. The honest current source for `award:arena-wins` is an explicitly authored
accumulating quest reward, settled by that same quest owner. Task #9144 holds ordinary Knight-tier
bout entry, win and once-only reward settlement, current-save interoperability and live acceptance.
A staged quest is not described as a played arena victory.

The Champion requirement reads five arena wins and Bounty Hunter reads ten thousand gold of bounties.
The event variables `arena-wins-knight` and `bounties` compare the same durable records, as donor event
comparisons read the corresponding party counters (`OpenEnroth src/Engine/Objects/Character.cpp:3966–3976`).
There is no duplicate effect, award ledger or fixture counter. Existing standing/accomplishment
projection names those records. The current party save carries the count; the quest section carries
its completed earning instance. Restoration neither rerolls nor awards anything.

Focused semantic evidence recorded on 2 October 2026 covers:

- Two distinct monthly bounty turn-ins each pay 5000 gold and accumulate to 10000; repeated turn-in,
  including through a reconstructed quest owner, cannot credit the purse or deed again.
- Two independently completed authored rewards add instead of replace, while ordinary marks retain
  replacement behavior and running effects stay empty.
- The actual source-generated session JSON restores both counts and completed quests; repeat
  settlement stays refused after restoration.
- Both actual promotion paths refuse below their count requirement and grant once the earned counts
  reach it, with their existing errand/giver and recovery requirements intact.
- Fixture comparisons branch below and at each threshold through the canonical records, and invalid
  authored counting values name their content defect.
- A count payment that cannot fit refuses before experience, coin, record or completion changes.

The imported topic survey runs 353 of 365 topic events for a fresh party; its twelve remaining named
refusals are hireling steps routed to #8514. A zero count now takes the event's unmet branch rather than
refusing the variable as unknown. The corresponding ruleset case checks this survey over the operator's
own packs.

These are source and focused-test evidence, not a live bounty, arena, traversal or NativeAOT claim.
Exact submitted source, review lanes and full-gate status remain in Den.
