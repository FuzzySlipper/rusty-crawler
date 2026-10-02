# Preservation through character health

Recorded on 2 October 2026. Focused composed checks cast spell 50 and drink potion 231 through the existing
casting path. The protected member takes two otherwise lethal wounds through `PartyMember.TakeDamage`
and remains Unconscious, while an unprotected companion becomes Dead. The save written through Engine
storage carries the accumulated deficit and original member deadline. A real resumed session remains
protected one millisecond before that deadline; at the deadline the effect expires. The next wound applies
the ordinary death ladder. An already Dead, Petrified or Eradicated member remains laid out.

The donor model is adapted from OpenEnroth `src/Engine/Objects/Character.cpp:1310-1321`,
OpenEnroth `src/Engine/Spells/CastSpellInfo.cpp:1703-1731` and
OpenEnroth `src/Engine/Objects/Character.cpp:3081-3085,3140-3142`.
Spell duration is one hour plus five minutes per school level, or fifteen at grand master. Potion duration
is thirty minutes per strength. This game's spell keeps its caster-only carrier; the donor targets an ally
at expert and the whole party at master and grand master. Protection is a presence flag to the health rule;
it neither heals nor resurrects. The existing effect owner removes the actual state of laid-out carriers,
and the existing party save/factory retains harm past zero. No additional effect ledger or clock exists.

Full verification and bounded ordinary live protection have not yet been reconciled for this reading.
No played protection, ordinary creation, broad traversal, rendered-world or NativeAOT claim follows from
these composed checks. Den owns task acceptance.
