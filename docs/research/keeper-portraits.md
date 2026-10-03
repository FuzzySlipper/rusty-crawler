# House keeper portraits

## Decision

Service and residence placements carry an authored `portrait` face key in the
normalized `places.json` content. `PlaceServiceEmitter` assigns a stable key
from the repository's neutral member face palette for each building id. This
gives every emitted house a concrete face while keeping the choice explicitly
ours: it does not claim that the donor's compiled room table gave that face to
the proprietor. The existing media writer already emits those neutral `pc`
frames from the operator-supplied installation, so this decision adds no
source asset or imported count.

`MightAndMagic7Conversation` reads that placement key when a service or
residence has no person-table portrait. A person entry's own portrait still
wins. The resulting `ConversationPerson.Portrait` follows the existing
`ConversationSnapshot` projection to `portraitImage`, and the existing screen
resolves it through `ContentImages`.

## Donor evidence and boundary

The behavior decision comes from the local OpenEnroth source checkout at
`/home/research/old-games/OpenEnroth`. In
`src/GUI/UI/UIHouses.cpp:389`, `prepareHouse` reads
`pAnimatedRooms[houseTable[house].uAnimationID].house_npc_id` and draws the
keeper as `npc{:03}`. In
`src/Engine/Tables/HouseTable.cpp`, the `2devents.txt` parser stores the
building row's picture/animation index as `uAnimationID`; that source file does
not carry the animated-room `house_npc_id` value. The dialogue face naming is
also documented by `src/GUI/UI/UIDialogue.cpp:67`.

The importer can legitimately read `2DEvents.txt` and the operator's own media,
but it does not load or run the original executable and does not copy donor code
or converted donor data. Since the compiled room table is outside the current
import boundary, the normalized placement field is an authored product choice
with the donor behavior recorded as context rather than an asserted proprietor
identity.

## Focused evidence

`MightAndMagic7.Import.Tests.ServiceEmissionTests` checks that every emitted
service/residence placement has a deterministic face and that the writer carries
it as `portrait` in `places.json`. The same test's byte comparison covers repeat
imports. `PartyRpg.Rulesets.MightAndMagic7.Tests.ConversationPolicyTests` checks
that a house fallback receives its placement face while a named person keeps its
own portrait, and `PortraitPolicyTests` checks that a direct neutral `pc` key
resolves through the media projection.

On Emerald Isle (place `1`), the authored Party Start is `(12552, 800, 193)`
with yaw `512`. The imported `service-1` The Knight's Blade / Tor placement is
at `(12979.8247, 4896, 96)`, and `service-74` Healer's Tent / Lauren is at
`(6392.4369, 12860.7293, 32)`. Both have no person-table row, so ordinary use
of either entrance exercises the keeper fallback and should show a face in the
conversation screen. Final pack regeneration, `mm7import verify`, and live
capture are performed by the task owner over the operator's ignored content.
