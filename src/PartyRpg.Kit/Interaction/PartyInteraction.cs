using System.Numerics;
using PartyRpg.Kit.Movement;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.World;
using Rusty.Engine.Interaction;
using TargetIdentity = Rusty.Engine.Interaction.InteractionTarget;

namespace PartyRpg.Kit.Interaction;

/// <summary>
/// The one mechanism that walks up to something and uses it: what the party can reach and use is discovered
/// from the place's own content, which use applies and what it requires is the ruleset's answer, and the
/// outcome is applied against the party's owners and reported.
/// </summary>
/// <remarks>
/// <para>
/// <b>Targets are discovered, never listed.</b> Every refresh reads the place's placements from the live
/// world, asks the ruleset what each one offers, and lets the engine's own selection decide what the party's
/// pose and facing are aimed at. A kind of thing that did not exist — a chest, a lever, a person — becomes
/// usable by content and a ruleset answer, and nothing here changes.
/// </para>
/// <para>
/// <b>One workflow serves every verb.</b> Identify the target, judge each requirement in the order the
/// ruleset stated them, settle what the use costs through the party's one settlement path, ask the ruleset
/// what the use produces, apply it, record the state, and report it. Search, open, unlock, pull, talk, and
/// read differ only in the answers they are given.
/// </para>
/// <para>
/// <b>A failure is an outcome.</b> Every way a use can fail — nothing focused, out of reach, out of sight, a
/// requirement unmet, a charge the party cannot cover, an outcome the ruleset refused, an item the pack
/// cannot take — comes back as a refusal with a code and a sentence. Nothing throws out of the admitted
/// update, and nothing changes when a use is refused.
/// </para>
/// <para>
/// <b>The aim is the engine's.</b> Acquisition, hysteresis, cycling, availability, and the fresh re-check a
/// use makes before it reaches this mechanism are composed here from <see cref="WorldInteraction"/> rather
/// than re-derived: a reticle is spatial machinery, and a second cone implemented beside the engine's would
/// be the first thing to disagree with it about what the party is looking at.
/// </para>
/// </remarks>
public sealed class PartyInteraction : IWorldInteractionScene
{
    /// <summary>The word the engine's inspection reports as the action this scene offers.</summary>
    private const string ActionWord = "use";

    private readonly IInteractionWorld _world;
    private readonly IInteractionRule _rule;
    private readonly PlaceSpace _space;
    private readonly InteractionTuning _tuning;
    private readonly WorldInteraction _selection;
    private readonly List<InteractionCandidate> _candidates = [];
    private readonly List<InteractionTarget> _targets = [];
    private InteractionReadout _focus = new(null, InteractionReason.NoCandidate, []);
    private InteractionResult? _result;

    /// <summary>Creates the mechanism over the live world it resolves uses against.</summary>
    /// <param name="world">The live state a use reads and settles against.</param>
    /// <param name="rule">This game's answer about targets, requirements, and outcomes.</param>
    /// <param name="space">
    /// How the place's own coordinates and facing become the engine's world axes. It is the same rule the
    /// party's movement walks by, so what the party aims at and what it can walk to are one space.
    /// </param>
    /// <param name="tuning">The aim the reticle acquires and releases targets within.</param>
    /// <exception cref="ArgumentNullException">The world or the rule is missing.</exception>
    public PartyInteraction(IInteractionWorld world, IInteractionRule rule, PlaceSpace space, InteractionTuning tuning)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _rule = rule ?? throw new ArgumentNullException(nameof(rule));
        _space = space;
        _tuning = tuning;
        _selection = new WorldInteraction(this);
    }

    /// <summary>The target the reticle holds, or null when nothing usable is in reach and in sight.</summary>
    public InteractionTarget? FocusedTarget { get; private set; }

    /// <summary>How far the focused target stands from the party, or zero when nothing is focused.</summary>
    public double FocusedDistance { get; private set; }

    /// <summary>Why the reticle holds what it holds: the engine's own reason for the selection.</summary>
    public InteractionReason FocusReason { get; private set; } = InteractionReason.NoCandidate;

    /// <summary>The last use this mechanism resolved, or null before the party has used anything.</summary>
    public InteractionResult? LastResult => _result;

    /// <summary>
    /// Refreshes the reticle from where the party now stands and what the place now holds. This is what a
    /// session steps inside its one admitted update, so what the panel shows as focused is what the last
    /// step actually faced.
    /// </summary>
    /// <param name="cycleDirection">A requested move to the next candidate: -1, 0 to keep the selection, or +1.</param>
    /// <exception cref="ArgumentOutOfRangeException">The cycle direction is not -1, 0, or +1.</exception>
    public void Update(int cycleDirection = 0)
    {
        _focus = _selection.Update(cycleDirection);
        AdoptFocus();
    }

    /// <summary>
    /// Uses what the reticle holds, re-checking the target's identity, reach, and sight from fresh facts,
    /// and reports what came of it. A use that reaches nothing is a refusal like any other.
    /// </summary>
    /// <returns>The use's result.</returns>
    public InteractionResult Use()
    {
        _result = null;
        InteractionUseReceipt receipt = _selection.UseFocused();
        if (_result is not null) return _result;

        // A use the engine's own selection refused — nothing focused, out of reach, out of sight, a target
        // that is gone — never reaches this scene, so its refusal is built here instead of being lost
        // between the two halves of the mechanism. When nothing was selected at all, the reticle's own
        // reason is the better answer: "the thing you are looking at is not in sight" is something a player
        // acts on, where "no focused target" would hide the reason the reticle gave a moment ago.
        InteractionReason reason = receipt.Reason == InteractionReason.NoCandidate && _focus.Reason != InteractionReason.NoCandidate
            ? _focus.Reason
            : receipt.Reason;

        // A use changes what the party faces — a door's state, and with it the use the door offers — so the
        // reticle is refreshed from the facts the use just produced. Without this the panel would show what
        // was faced before the use for the rest of the update, which is the one frame a player looks at.
        _focus = _selection.Update();
        AdoptFocus();
        _result = InteractionResult.Refused(FocusedTarget, CodeFor(reason), MessageFor(reason, receipt.Message));
        return _result;
    }

    /// <inheritdoc />
    InteractionSceneSnapshot IWorldInteractionScene.ReadInteraction() => Build();

    /// <inheritdoc />
    InteractionActionResult IWorldInteractionScene.UseInteraction(TargetIdentity identity)
    {
        if (_targets.Find(target => target.Number == identity.Id) is not { } target)
        {
            // The engine re-checks identity before it calls this, so a target that cannot be found here is
            // one the scene stopped offering between the two calls: a refusal, not a defect.
            return new InteractionActionResult(false, "What the party was using is no longer here.");
        }

        InteractionResult result = Resolve(target);
        _result = result;

        // The reticle is refreshed after the use so what the mechanism reports as faced is the target as the
        // use left it, rather than the incarnation the use was aimed at.
        _focus = _selection.Update();
        AdoptFocus();
        return new InteractionActionResult(result.IsApplied, result.Message);
    }

    /// <summary>
    /// Builds the candidates and the query one refresh resolves against, in the engine's world axes.
    /// </summary>
    /// <remarks>
    /// The party's eye and every target's point are placed through the movement's own space rule, so the
    /// distance the reticle measures is the distance the party walks, and a target at the party's own height
    /// is a target it can look at rather than one below its feet. Sight is cast only for what could be used
    /// at all: a place holds hundreds of placements and a ray per placement per update would be work spent
    /// on things the party is nowhere near.
    /// </remarks>
    private InteractionSceneSnapshot Build()
    {
        PlaceId place = _world.Place;
        IReadOnlyList<PlacementDefinition> placements = _world.Placements;
        Vector3 eye = _space.Position(_world.Pose);
        _candidates.Clear();
        _targets.Clear();
        double furthest = 0;

        for (int index = 0; index < placements.Count; index++)
        {
            PlacementDefinition placement = placements[index];
            InteractionTargetState state = _world.States.StateOf(place, placement.Content);
            if (_rule.Describe(new InteractionTargetRequest(place, placement, state.State)) is not { } definition) continue;

            Vector3 point = _space.Position(placement.Pose);
            double distance = Vector3.Distance(point, eye);

            // Only what the party can reach becomes a candidate. A place holds hundreds of placements, and a
            // sight cast for each of them per update would be work spent on things the party is nowhere near;
            // a target nobody can use is also not something a reticle should hold. A distant target is
            // therefore not refused — it is simply not faced, and the panel says that nothing is.
            if (distance > definition.Reach) continue;

            _targets.Add(new InteractionTarget(
                new InteractionTargetId(place, placement.Content),
                (ulong)index,
                placement,
                definition,
                state));
            _candidates.Add(new InteractionCandidate(
                new TargetIdentity((ulong)index, (ulong)state.Revision),
                definition.Name,
                point,
                (float)definition.Reach,
                _world.InSight(eye, point) ? InteractionVisibility.Visible : InteractionVisibility.Occluded,
                // Availability is left available whatever the requirements are: a lock is a requirement this
                // game states, and the answer to it is a sentence naming what the door needs. Handing the
                // engine its own Locked gate instead would replace that sentence with a word.
                InteractionAvailability.Available));
            furthest = Math.Max(furthest, definition.Reach);
        }

        InteractionQuery query = new(
            eye,
            Look(_world.Pose.Yaw),
            (float)_tuning.AcquisitionAngleRadians,
            (float)_tuning.ReleaseAngleRadians,
            (float)furthest,
            (float)furthest);
        return new InteractionSceneSnapshot(query, _candidates.ToArray(), Stamp(), ActionWord);
    }

    /// <summary>Runs the one use workflow over a target, and answers with what came of it.</summary>
    private InteractionResult Resolve(InteractionTarget target)
    {
        InteractionContext context = new(_world.Place, target.Placement, target.Definition, _world.Party, _world.Clock);

        // Requirements first, in the order the ruleset stated them: the first one the party does not meet is
        // the answer, and nothing at all is applied.
        foreach (InteractionRequirement requirement in target.Definition.Requires)
        {
            InteractionRequirementVerdict verdict = _rule.Judge(requirement, context);
            if (verdict.IsMet) continue;
            return InteractionResult.Refused(
                target,
                "interaction-requirement-unmet",
                $"{target.Definition.Name} requires {requirement.Describe()}: {verdict.Explanation}");
        }

        // What the use costs settles through the party's one settlement path, which judges both accounts
        // before either moves, so a charge the party cannot cover refuses the use whole.
        if (!target.Definition.Price.IsFree)
        {
            if (_world.Accounts is not { } accounts)
            {
                return InteractionResult.Refused(
                    target,
                    "interaction-no-accounts",
                    $"{target.Definition.Name} asks a price and this world holds no party whose accounts could pay it.");
            }

            ResourceSettlement settlement = accounts.Settle(target.Definition.Price);
            if (!settlement.Admitted) return InteractionResult.Refused(target, settlement.Refusal!.Code, settlement.Refusal.Message);
        }

        // What the target guards itself with comes next, and before anything it holds: a trap the party
        // cannot get past is answered here, never by handing over what it was guarding. The ruleset stated
        // every word and number of it, and the workflow — notice it before defeating it, spend it once, let
        // a failed attempt cost the party — is what this mechanism owns.
        if (_rule.Trap(target.Definition, context) is { } trap)
        {
            return Spring(target, trap, context);
        }

        InteractionOutcome outcome = _rule.Apply(target.Definition, context);
        if (!outcome.IsApplied) return InteractionResult.Refused(target, outcome.Refusal!.Code, outcome.Refusal.Message);

        // What the use gives is judged before anything moves: a pack with no room for what was found refuses
        // the whole use rather than leaving a container half emptied into it.
        if (outcome.Items.Count > 0)
        {
            if (_world.Party is not { } keeper)
            {
                return InteractionResult.Refused(
                    target,
                    "interaction-no-party",
                    $"{target.Definition.Name} gives what it holds and this world holds no party to take it.");
            }

            foreach (InteractionItemYield yield in outcome.Items)
            {
                if (keeper.Inventory.Judge(yield.Definition, yield.Count) is { } refusal)
                {
                    return InteractionResult.Refused(target, refusal.Code, refusal.Message);
                }
            }
        }

        string taken = HandOver(target, outcome);
        InteractionTargetState state = _world.States.Record(_world.Place, target.Id.Content, outcome.State);
        InteractionTarget used = target with { State = state };
        return InteractionResult.Applied(used, outcome, taken.Length == 0 ? outcome.Message : $"{outcome.Message} {taken}");
    }

    /// <summary>
    /// Judges the trap a target holds, and produces what the party's use of it made: a trap noticed, a trap
    /// defeated, or a trap that went off.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The order is the whole workflow and it is why this is the kit's: a trap the party has not noticed is
    /// judged by what noticing it takes, one it has noticed by what defeating it takes, and the check's
    /// result decides whether the party learns what they face or pays for it. Nothing here knows what a
    /// trap is called, what a skill is, or how hard anything is; those are the ruleset's, and they arrive in
    /// the trap itself.
    /// </para>
    /// <para>
    /// <b>A trapped target is not searched on the same use.</b> A trap that goes off spends itself, and the
    /// party gets no further in that act — which is the donor's own behaviour, where an explosion leaves the
    /// chest standing for a second attempt rather than handing over its contents in the same breath. The
    /// residue states it, so the contents are not silently lost: they are still in the container, and the
    /// next use finds what is left of it.
    /// </para>
    /// <para>
    /// <b>The harm lands on the members.</b> Every member loses what the ruleset said, through the party's
    /// own resources, so a consequence is a change in the world rather than a sentence. When there is no
    /// party to harm, the use is refused by name: a trap with nowhere to land is a fact about the world and
    /// not something to record as if it had happened.
    /// </para>
    /// </remarks>
    private InteractionResult Spring(InteractionTarget target, InteractionTrap trap, InteractionContext context)
    {
        InteractionChallenge challenge = trap.Next;
        if (challenge.Succeeds)
        {
            string learned = trap.IsKnown
                ? $"{target.Definition.Name}'s {trap.Name} is defeated ({challenge.Describe()})."
                : $"{target.Definition.Name} is guarded by {trap.Name}, and the party notices it ({challenge.Describe()}).";
            InteractionOutcome passed = InteractionOutcome.Applied(trap.PassedState, learned);
            InteractionTargetState passedState = _world.States.Record(_world.Place, target.Id.Content, passed.State);
            return InteractionResult.Applied(target with { State = passedState }, passed, passed.Message);
        }

        if (_world.Party is not { } party)
        {
            return InteractionResult.Refused(
                target,
                "interaction-no-party",
                $"{target.Definition.Name}'s {trap.Name} goes off and this world holds no party for it to catch.");
        }

        // The harm is taken before the state is recorded, so what the ledger says happened and what the
        // members' resources show cannot disagree: a sprung trap is one the party has already paid for.
        int members = 0;
        foreach (PartyMember member in party.Members)
        {
            member.Resources.TakeDamage(trap.Harm.PerMember);
            members++;
        }

        string message = members == 0
            ? $"{target.Definition.Name}'s {trap.Name} goes off ({challenge.Describe()}), and the party has nobody in it to catch it."
            : $"{target.Definition.Name}'s {trap.Name} goes off ({challenge.Describe()}): {members} member(s) take {trap.Harm.PerMember} {trap.Harm.Label} each.";
        InteractionOutcome sprung = InteractionOutcome.Applied(
            trap.SprungState,
            message,
            "Nothing was taken from it in the same act: the trap spent itself, and what the target holds is still there.");
        InteractionTargetState state = _world.States.Record(_world.Place, target.Id.Content, sprung.State);
        return InteractionResult.Applied(target with { State = state }, sprung, sprung.Message);
    }

    /// <summary>Moves what an outcome gives into the party's own owners, and reports it in one clause.</summary>
    private string HandOver(InteractionTarget target, InteractionOutcome outcome)
    {
        List<string> taken = [];
        if (outcome.Items.Count > 0 && _world.Party is { } keeper)
        {
            foreach (InteractionItemYield yield in outcome.Items)
            {
                ItemAcquisition acquisition = keeper.AcquireItem(yield.Definition, yield.Count);
                if (!acquisition.Admitted) continue;
                taken.Add($"{yield.Count} × {yield.Definition}");
            }
        }

        if (!outcome.Gain.IsFree && _world.Accounts is { } accounts) accounts.Credit(outcome.Gain);
        if (taken.Count == 0) return string.Empty;
        return $"The party takes {string.Join(" and ", taken)}.";
    }

    /// <summary>Takes the selection's answer onto this mechanism's own view of what is focused.</summary>
    private void AdoptFocus()
    {
        FocusReason = _focus.Reason;
        FocusedTarget = null;
        FocusedDistance = 0;
        if (_focus.Selected is not { } selected) return;

        foreach (InteractionObservation observation in _focus.Candidates)
        {
            if (observation.Candidate.Target != selected) continue;
            FocusedDistance = observation.Distance;
            FocusedTarget = _targets.Find(target => target.Number == selected.Id);
            return;
        }
    }

    /// <summary>
    /// The engine heading the party looks along, in the engine's world axes.
    /// </summary>
    /// <remarks>
    /// The heading comes from the movement's own space rule, and the direction it names is the one the
    /// engine's controller walks a heading along (<c>forward = (sin yaw, 0, -cos yaw)</c>), so the reticle
    /// points where the party walks. Pitch is deliberately not part of it: nothing in this product looks up
    /// or down yet, and a pitch convention invented here would be a second answer to a question the place
    /// data already answers.
    /// </remarks>
    private Vector3 Look(double yaw)
    {
        double heading = _space.FacingRadians(yaw);
        return new Vector3((float)Math.Sin(heading), 0, (float)-Math.Cos(heading));
    }

    /// <summary>A word for where the party stands and what it has done, so two receipts can be told apart.</summary>
    private string Stamp()
    {
        PlacePose pose = _world.Pose;
        return string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{_world.Place}@{pose.X:0.#},{pose.Y:0.#},{pose.Z:0.#}");
    }

    /// <summary>What a use that reached the mechanism but not a target reports.</summary>
    /// <remarks>
    /// The engine states a reason for a selected target that stopped being usable, and that message is kept
    /// as it is. When nothing was selected there is no such message to keep, and the reason word alone is
    /// not what a player reads, so each reason gets the sentence it means.
    /// </remarks>
    private static string MessageFor(InteractionReason reason, string selected) =>
        reason == InteractionReason.NoCandidate && selected.Length > 0
            ? selected
            : reason switch
            {
                InteractionReason.NoCandidate => "Nothing the party can use is in front of it.",
                InteractionReason.OutsideQuery => "What the party faces is outside where it is looking.",
                InteractionReason.OutOfReach => "What the party faces is out of reach.",
                InteractionReason.Occluded => "What the party faces is not in sight.",
                InteractionReason.VisibilityUnknown => "Whether the party can see what it faces is not known.",
                InteractionReason.Unavailable => "What the party faces cannot be used.",
                InteractionReason.Locked => "What the party faces is locked.",
                InteractionReason.InvalidTarget or InteractionReason.StaleTarget => "What the party was using is no longer there.",
                _ => selected,
            };

    /// <summary>The refusal code for a use the engine's own selection refused before it reached the scene.</summary>
    /// <remarks>
    /// Each reason keeps its own code: "nothing is there to use" and "the thing is out of reach" are
    /// different answers to a player, and a single "refused" would be exactly the silence this mechanism
    /// exists to prevent.
    /// </remarks>
    private static string CodeFor(InteractionReason reason) => reason switch
    {
        InteractionReason.NoCandidate => "interaction-no-target",
        InteractionReason.OutsideQuery => "interaction-outside-view",
        InteractionReason.OutOfReach => "interaction-out-of-reach",
        InteractionReason.Occluded => "interaction-occluded",
        InteractionReason.VisibilityUnknown => "interaction-visibility-unknown",
        InteractionReason.Unavailable => "interaction-unavailable",
        InteractionReason.Locked => "interaction-locked",
        InteractionReason.InvalidTarget => "interaction-target-gone",
        InteractionReason.StaleTarget => "interaction-target-changed",
        _ => "interaction-refused",
    };
}
