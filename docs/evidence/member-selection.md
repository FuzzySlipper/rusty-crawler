# One member acts at a time

Recorded on 2 October 2026 in a local CoreCLR host. Ordinary panel controls selected two different
members and charged each member's own attack recovery while leaving the other ready members untouched.
A repeated attack by the recovering selected member was refused by name. This bounded reading uses
empty-space attacks; it establishes no creature damage or engaged-fight outcome.

`PartyRoster.SelectedMember` is the durable choice on the existing party. `CombatState` asks its existing
capability and condition rules before accepting a selection. A recovering member stays selected and
an order reaches the existing recovery refusal. An incapable selection is refused; an invalidated
choice moves to the first capable member, or none when nobody can act. In paced combat each new
player turn selects its queue actor; a ready off-turn choice cannot spend that actor's turn. The current
save carries the member identity and names an unknown identity before rebuilding the party.

The Host declares N to cycle members. The panel sends a member's published durable identity through
the existing payload channel and renders the published selection and refusal. It computes no capability,
recovery or turn rule. The donor uses A for attack, Tab to cycle and B to pass (`OpenEnroth
src/Application/GameConfig.h:536,542,554`). This product retains B for attack and uses N because the
installed safe keyboard contract does not expose Tab. Keeping an explicitly selected recovering
member is an adaptation of the donor's automatic ready-member choice (`src/Engine/Party.cpp:217–264`).

The live host retained imported Harmondale terrain, geometry, events, targets and houses. Party Start
was relocated to `(768,7936,82)`, yaw 384, and 181 actor/encounter placements were omitted. An existing
authored four-member scenario used the normal party factory, with each member at 30/30 health. Two
prior attempts timed out in the browser lease after ordinary creation acceptance; they establish no
creation or member-control acceptance. The parent switched only the isolated scenario start after
both leases were released. No combat rule, recovery amount or saved byte was patched.

Session `08a788a6-9766-4dd9-baf1-88675a8298d0` observed grounded feet
`(767.9117,7936.0884,81.9724)`. Engine time was held for inspection. Each ordinary DOM payload was
admitted by a bounded 100 ms Engine advance; browser transport success alone was not counted as action.

| Ordinary action | Original capture | Observed result |
| --- | --- | --- |
| Attack with Roderick selected | `8bb72e99-c73c-4d48-9226-80e22db9c549` | Roderick recovering 20.9 seconds; Aelina, Borin and Nyx ready; Roderick's named empty-space attack message. |
| Select Aelina | `4eb8b53c-7111-4cd0-921c-3d20fc0815df` | “Aelina is selected to act,” while Roderick still recovers. |
| Attack with Aelina selected | `c87f5a9b-9d3c-451e-b726-40e156811181` | Roderick recovering 14.9 seconds, Aelina 20.9 seconds; Borin and Nyx ready; Aelina's named empty-space attack message. |
| Repeat Aelina's attack | `ca5c95f0-d201-4048-b951-986eb8726f3e` | “Aelina is still recovering: 20438ms of game time must pass before I can attack again.” Selection and the other ready members remain unchanged. |

The root inspected the original first-attack, second-attack and refusal captures. Den evidence 37326
indexes exact original PNGs, sidecars, admitted advances, live query receipts and host/event logs.
Cleanup released the owned lease, closed the browser and stopped the host; the final pool had no active
sessions. No Save, physical N-key exercise, ordinary creation, hostile fight, traversal, rendered world
or NativeAOT claim follows from this mission.

Meaningful focused checks cover selected-only ordinary key/payload orders, same-update selection and
attack, incapable/unknown selection, recovery refusal, off-turn refusal, current-turn choice, actual
JSON save restoration and malformed saved identities. The source reviews accepted the canonical
ownership and real callers. Full-gate identity and results remain in Den; this document does not replace
that live task authority.
