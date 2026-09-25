using PartyRpg.Kit.Movement;
using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Interaction;

/// <summary>What the world needs to compose its interaction mechanism: this game's answers and its aim.</summary>
/// <remarks>
/// The three travel together because they are one decision — how a use is resolved in this game — and because
/// a world composed with one of them and not the others would be a mechanism that cannot answer. A world
/// composed with no policy at all has no interaction, which is what content that places nothing usable and a
/// ruleset that offers no answers both get.
/// </remarks>
/// <param name="Rule">The ruleset's answers about targets, requirements, and outcomes.</param>
/// <param name="Space">
/// How the place's own coordinates and facing become the engine's world axes. It is the movement's own rule,
/// so what the party aims at and what it can walk to are one space.
/// </param>
/// <param name="Tuning">The aim the reticle acquires and releases targets within.</param>
public sealed record InteractionPolicy(IInteractionRule Rule, PlaceSpace Space, InteractionTuning Tuning);
