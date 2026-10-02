# Item identity and item-bound magic

Focused source checks on 2026-10-02 cover actual composed casting, canonical inventory/equipment, combat,
counter operations, projection and the current Engine-backed save store. Ordinary staged product evidence
and the full verification gate are pending; this record makes no live acceptance claim.

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

No imported acquisition, normal creation, broad traversal, graphics or NativeAOT claim is made here.
