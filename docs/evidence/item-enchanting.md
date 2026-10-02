# Item identity and item-bound magic

Focused source checks on 2026-10-02 cover actual composed casting, canonical inventory/equipment, combat,
counter operations, projection and the current Engine-backed save store. Both ordinary staged phases and the literal saved bytes are reconciled below. The initial full gate
passed; the final load-validation supplement requires its closeout gate.

## What changed

An artifact remains the imported definition on its unique durable instance, with custody and item state
carried across the current save. Special Artifact, Relic and Special material rows traverse the ordinary
shop flow: buy two independent instances, identify, repair, sell and leave through one purse. They are not
enchanted or coated into a different identity. No global one-of-each artifact rule is claimed.

The normal casting panel's item offers identify actual party-held instances. Enchant Item produces a
permanent property with mastery-dependent strength; fixed special items and quest items refuse before
payment. Cheap gear or a failed keyed chance breaks unless hardened. The durable attempt count resumes
with the party records. Fire Aura and Vampiric Weapon, recharge, hardening and six weapon-coating potions
use the same utility effect path. Coatings expire against the original elapsed-clock deadline across a
resume; grand-master spell coating is permanent. Recharge reduces the instance's real maximum capacity
and every charged-item consumer reads it.

Elemental and dragon contributions use the current attack's landed hit and their own resistance through
the existing resolver. Vampiric restores the actual wielder after settled harm; working passive properties
change actual combat attribute/armour readings, and swift takes twenty recovery ticks once. The DOM prints
the resolver's sentence rather than calculating an additional damage total. Permanent ordinary properties
change the ordinary counter quote and settlement. Contradictory special-item properties, hardening and
charge capacities, default-capacity overuse and already-expired property deadlines are all named before a load rebuilds anything.

## Approximation and donor references

Primary behavior references are `OpenEnroth/src/Engine/Spells/CastSpellInfo.cpp:687-741` (Fire Aura and
Vampiric Weapon), `:1334-1510` (recharge and enchanting),
`OpenEnroth/src/Engine/Objects/Character.cpp:1727-1733` (swift), and
`OpenEnroth/src/GUI/UI/UIPopup.cpp:2182-2254` (item property presentation). No donor source was copied.

Common eligibility, quest refusal, value floors, rank chance, mastery strength and rank-hour spell
durations follow the stated policy. Permanent property selection is uniform over this game's compact
weapon/passive repertoire rather than original weighted tables; elemental and dragon damage magnitudes
and trade premiums are ours. All original special-item powers and original enchantment numerical
equivalence are not claimed. Further powers and Genie Lamp use have receiver #9148. World-targeted
Telekinesis is receiver #9145; character Preservation is receiver #9146, correcting the old gear-protection
reading against `Character.cpp:1310-1321`.

## Focused evidence

`ItemEnchantmentPolicyTests` exercises actual spell and potion requests, quest/special refusal, mastery,
failure/hardening, recharge capacity, real composed fight damage and vampiric healing, working equipment
readings, permanent and temporary resume, fixed artifact identity and aggregate invalid-save refusal.
`ServicePolicyTests` covers the four material rows through the same counter and actual permanent-property
quote/settlement. `CombatResolutionTests` proves separate resistances, immunity and a single canonical
health settlement/hit notification. `ItemMagicTests` and `PersistenceTests` check actual charged casting,
last-charge custody and source-generated current saved bytes. The projection fixture and UI suites check
the C#/TypeScript contract and the actual resolver sentence.

No normal creation, mastery-ceiling, broad traversal, graphics or NativeAOT claim is made here.

## First ordinary staged phase

The bounded imported Manor mission used its original geometry, doors, map events and container placement,
with a relocated grounded north-room arrival, five combat placements omitted, a four-member scenario and
pre-authorized Master Water caster. Container 3 had explicitly authored Puck, Great Sword and Lich Jar
contents and its trap flag omitted. Those are mission staging, not an unstaged loot or creation claim.
The portrait/member surface reports the caster's staged school tier separately from the imported class
ceiling; this check does not certify learning or class/mastery progression.

Ordinary DOM Use searched the actual chest and acquired one of each. A held physical G did not admit use
in this run; it is not keyboard evidence. The captured product message named all three definitions.
Ordinary target selection and Enchant Item produced Great Sword instance 2 with permanent sparks 8 and
spent Nyx's fifteen points (18 to 3). A first special-item attempt lacked points, which was a resource
refusal rather than a special-item check. Ordinary Rest & heal advanced eight hours and charged two
portions, restoring the party; subsequent ordinary casts on Puck and Lich Jar named the special and quest
refusals respectively with no point payment. Ordinary DOM Save was admitted at 1168-01-01 17:11.

After the first owned lease released, the parent read the actual Engine store bytes without changing
them: the 6,797-byte store held a 6,777-byte current JSON document. Item 1 was definition 500 Puck, item 2
was definition 7 Great Sword with sparks 8 and no end deadline, and item 3 was definition 601 Lich Jar;
all three were in the party pack and the two refused items held no property. The enchant-attempt record
was one and selected member was Nyx. This is literal saved state, not an inference from the Save button.

Original captures: chest `07c47f26-e0c9-4160-9d0b-8369bfafe8f6`, enchant
`74d6183f-6b27-4d2c-ae20-9d078d063016`, special refusal `ed5034e5-981e-4560-ac54-9566d40afb23`,
quest refusal `2b535897-3d8e-4877-af6d-bbd7519b6a08`, Save `65175fcb-c2a2-4a57-ad10-128f612807f9`.
The parent opened these originals. Den 37402 owns the complete receipt and cleanup record, with the chest
capture identity corrected by 37405. The lease released and the local pool was empty; a later session's
ownership must be refreshed independently. The parent then authorized a separate resume phase against
those same bytes. Its terminal evidence follows.

## Resume through the current save

The parent changed only the new item profile from fresh to resume after the first lease released and
the literal bytes were reconciled. The resumed product displayed Party resumed, the same four ready
30/30 members, Nyx selected with 18/18 points, and the original next-sleep deadline. Its pack showed
Puck and Great Sword with sparks 8; ordinary item target options retained the three saved instance
identities. The photographed equipment row makes the persisted property visible without a second cast.

Ordinary DOM Use on the same searched chest was refused with “A chest has already been emptied.”
No new yield, cast or Save was admitted in this phase. After the second owned lease released, the parent
reread the original store and confirmed its bytes unchanged. The separately protected earlier fight and
deadline stores were unchanged through both phases.

Original resumed captures: neutral `ea719fa8-f6c7-45d8-8c7d-7bc7762a6ccf`, pack/property
`c223d7b0-d7ef-4550-95e5-e28d9d7e21df`, already-empty refusal
`e5ee9ce8-7411-4bed-b849-41141a05e2be`. The parent opened these originals. Den 37408 owns the complete
second-session, receipt and cleanup record. Both owned item leases released; the final observed local
pool was empty. No further resume is authorized.

The initial complete verification ran Architecture 21, Kit 770, Host 86, ruleset 367, importer 189 and UI
70 checks with no skips, plus all builds, operator inventory/map decoding/deterministic writes and CoreCLR
staging. A source review then resolved the no-override charge-overuse and expired-property load findings
with a named aggregate regression; 29 focused item/load checks pass and all three source lanes approve
that supplement. A separate final full gate remains required for the supplemented source. NativeAOT
was not run. This record certifies the bounded item mission, not normal creation, class/mastery learning,
unstaged loot populations, combat, services beyond the focused counter tests, broad traversal or graphics.
