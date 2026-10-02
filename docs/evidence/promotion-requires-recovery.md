# A dead Knight refused by the shipped promoter event

Observed on 2026-10-02 through an owned local crew-playtest browser session,
`5c062f0c-6e29-4a9a-8bae-b1e2b4dc4f74`.

The party saved in the preceding Manor check was resumed with Roderick still a
Dead Knight. Parent staging placed Frederick Org (`npc-43`) 100 units east of
the saved arrival and set the party's prerequisite `errand:140` and changed-topic
record `topic-slot:npc-43.0:74`. This selected the operator's imported global
event 74, rather than replacing its program or bypassing its promotion caller.
Those prerequisite records and the arrival are staging, not proof of completing
the errand or walking to the promoter.

Using the person opened the normal conversation. Choosing its Cavalier topic
produced the panel's sentence:

> Cavalier runs global event 74, whose step 10 makes Roderick a Cavalier:
> Roderick is missing recovery from Dead through a temple cure or a raising
> spell before promotion, so it was not given. Nothing was changed.

Roderick remained a Dead Knight; no promotion or reward occurred. The refusal
is the shared progression owner's answer reached by the existing global-event
interpreter. This live reading covers Dead; focused real-ruleset checks cover
Unconscious, Dead, Petrified and Eradicated through Promote, Grant and JudgeGrant.
The imported dialogue's announcement of a promotion remained visible above the
explicit refusal residue; that announcement is program text, not evidence that
a rank moved.

The policy is the owner's explicit choice: require recovery before promotion.
It deliberately differs from the donor's unconditional class assignment at
`OpenEnroth/src/Engine/Objects/Character.cpp:4028-4029`. Party-scoped errands
remain available to the band; promotion is judged for the individual member.

Original capture identities are `9320bd0a-2020-4160-9b98-2d2a4663f915` (initial),
`027bb947-cd4a-48a2-b119-5eb82226c734` (conversation), and
`36cfd172-0d20-4eb2-9fcf-633ff474f704` (full refusal), retained with the browser
journal in the operator's local evidence store. The owned session stopped
successfully and the service reported no occupied slots. The optional resumed
chest recheck was not attempted.
