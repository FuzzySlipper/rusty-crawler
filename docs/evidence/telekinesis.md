# Telekinesis through world interaction

Recorded for rusty-crawler#9145 on 5 October 2026 (America/Los_Angeles).

## Mechanism

Spell 42 aims from the spellbook (`L`) at the door or container the party faces, read with
`PartyInteraction.AimAtReach`: the same interaction scene builds its candidates at the spell's tuned reach
(`spell.telekinesis-reach`, default 5120) with only doors and containers offered, and the Engine's read-only
`WorldInteraction.Preview` answers by ordinary acquisition, retention and ranking without moving the sticky
focus. A cast is judged before payment: an absent, occluded, out-of-reach or changed aim (place, placement,
runtime number or revision) is refused with `spell-world-target-unavailable`. A judged aim becomes the Engine
focus only for its own `UseFocused` admission, through the one use workflow, and the focus is then refreshed
from ordinary facts. Target-ID use stays off. The use is settled as the use control's own is — world report and
journey, knowledge, and any conversation.

## Focused checks

- Kit `InteractionTests`: the preview leaves an out-of-reach ordinary focus and a cycled sticky choice where
  they were; a distant chest is used through focused admission and ordinary Use stays out of reach afterwards;
  a revised or turned-away aim is refused as `interaction-target-changed` with no state change; an
  eligibility filter is not shadowed by a nearer target; a blind mover offers nothing.
- Ruleset `TelekinesisPolicyTests`: projected distant door and container casts spend 20 points, open/search
  through the use workflow and survive the current save; stale revision, pose, place and spell reach refuse
  before payment; a door requirement and a container trap answer as ordinary uses; discovered knowledge from a
  distant use is carried through the save; the projected aim refreshes as the focus changes.
- The full ruleset suite, including the Arena ordinary-interaction checks that a focus-moving preview broke,
  passes.

## Bounded live cast

A private host from this branch with a hand-written scenario (an Earth Master sorcerer who knows the spell)
starting at The Arena's own arrival point. Staging was the scenario pack and `playtest.look` turns; nothing was
walked.

1. Facing a gate 2600 units away, the reticle read `A door · out of reach · 2600 away`; `G` answered
   `A door is out of reach.`
2. `L` opened the spellbook; Telekinesis offered the aim `A door (door)`. Interaction inspection before the cast:
   focus `OutOfReach`, nothing selected, the gate at revision 0.
3. `Cast Telekinesis`: `Aelina casts Telekinesis for 20 spell point(s): Telekinesis: A door swings open.` Spell
   points 102 → 82; the gate read state `open`, revision 1; the ordinary focus was still `OutOfReach` with
   nothing selected, and the last use read `applied`.
4. Turned away until no gate stood in the forward cone, with the ordinary reticle on a nearby fixture (`Door`,
   ready, 320): the spellbook offered no aim, and `Cast Telekinesis` answered `Telekinesis finds no door or
   container the party faces within its reach and in sight.` Spell points stayed 82, and the reticle stayed on
   the fixture.

## Bounds

- Doors and containers only (a body lying where it fell is a container); no item pickup and no event
  decorations, which the donor's Telekinesis also reaches (OpenEnroth `src/Engine/Spells/CastSpellInfo.cpp:2161-2207`).
- A sprung trap harms the party as an ordinary use's does; the donor's blast reaches only a party within 768
  units of the chest (OpenEnroth `src/Engine/Objects/SpriteObject.cpp:512-517`).
- The live run cast at a door only; container casts, traps, requirements, knowledge and the save are the
  focused checks' evidence, not live readings. No save/resume, broad traversal, other place or NativeAOT claim
  is made. Visibility is the interaction scene's sight answer, which accepted a gate partly behind a pillar.
