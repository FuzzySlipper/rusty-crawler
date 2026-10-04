using System.Text;
using System.Text.Json;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Movement;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.World;
using Rusty.Engine;
using Xunit;
using InteractionReason = Rusty.Engine.Interaction.InteractionReason;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// The one interaction mechanism: what the party can reach and use, what the use workflow does with it, and
/// what a refusal says.
/// </summary>
/// <remarks>
/// Every rule a game would recognize here belongs to the test's own rule — a reach, a verb, a requirement, a
/// price, what a use finds — which is the point of the seam: the kit holds no lock, no key, and no kind of
/// thing, so the same mechanism serves a test that invents them and a ruleset that owns them. The places are
/// written inline, so what a target is comes from content in these tests exactly as it does from an imported
/// pack in the product.
/// </remarks>
public sealed class InteractionTests
{
    [Theory]
    [InlineData(88, 59, true)]
    [InlineData(88, 195, true)]
    [InlineData(88, -195, true)]
    [InlineData(0, 384, true)]
    [InlineData(0, -384, true)]
    [InlineData(-1, 195, false)]
    public void A_forward_hemisphere_cone_can_acquire_elevated_targets_without_changing_reach(double ground, double height, bool acquired)
    {
        TestRule rule = new(only: "chest");
        rule.Outcomes["chest"] = (_, _) => InteractionOutcome.Applied("searched", "The chest is searched.");
        using Hall hall = Hall.Build(rule, new PlacePose(100 - ground, 0, -height, 1536, 0),
            tuning: new InteractionTuning(Math.PI / 2, (Math.PI / 2) + 0.11));
        hall.Interaction.Update();
        Assert.Equal(acquired, hall.Interaction.FocusedTarget is not null);
        if (acquired) Assert.True(hall.Interaction.Use().IsApplied);
    }

    [Fact]
    public void An_observed_target_beyond_its_reach_is_named_as_out_of_reach_instead_of_absent()
    {
        using Hall hall = Hall.Build(new TestRule(only: "chest"), new PlacePose(-285, 0, 0, 1536, 0));
        hall.Interaction.Update();
        Assert.Equal(InteractionReason.OutOfReach, hall.Interaction.FocusReason);
        Assert.Null(hall.Interaction.FocusedTarget);
        Assert.Equal("A chest", hall.Interaction.ContextTarget?.Definition.Name);
        Assert.Equal(385, hall.Interaction.ContextDistance, 3);
        InteractionResult refused = hall.Interaction.Use();
        Assert.Equal(InteractionCodes.InteractionOutOfReach, refused.Code);
        Assert.Equal("A chest", refused.Target?.Definition.Name);
        Assert.Contains("A chest", refused.Message, StringComparison.Ordinal);
        Assert.Contains("out of reach", refused.Message, StringComparison.Ordinal);

        InteractionSnapshot projection = InteractionSnapshot.From(hall.Interaction);
        Assert.Equal("A chest", projection.Label);
        Assert.Equal("search", projection.Verb);
        Assert.Equal("out-of-reach", projection.Reason);
        Assert.Equal(385, projection.Distance, 3);
    }

    [Fact]
    public void Interaction_projection_does_not_pair_a_previous_use_with_a_new_focus()
    {
        using Hall hall = Hall.Build(new TestRule(), Hall.Facing("door-0"));
        hall.Interaction.Update();
        Assert.True(hall.Interaction.Use().IsApplied);

        hall.Move(Hall.Facing("door-1"));
        hall.Interaction.Update();

        InteractionSnapshot projection = InteractionSnapshot.From(hall.Interaction);
        Assert.Equal("A door", projection.Label);
        Assert.Equal("open", projection.Verb);
        Assert.Equal("none", projection.Outcome);
        Assert.Empty(projection.Code);
        Assert.Empty(projection.Message);
    }

    [Fact]
    public void Interaction_projection_keeps_an_applied_result_for_the_current_incarnation_but_drops_it_after_place_restore()
    {
        using Hall hall = Hall.Build(new TestRule(), Hall.Facing("door-0"));
        hall.Interaction.Update();
        InteractionResult opened = hall.Interaction.Use();
        Assert.True(opened.IsApplied);
        Assert.Equal(1, opened.Target!.State.Revision);

        InteractionSnapshot applied = InteractionSnapshot.From(hall.Interaction);
        Assert.Equal("applied", applied.Outcome);
        Assert.Equal("open", applied.State);

        // Respawn forgets the place's interaction ledger, so the same placement identity begins a new
        // incarnation at revision zero. The retained result belongs to the old visit and must not appear
        // under the freshly rebuilt target.
        hall.World.Places.MarkCleared(HallPlace);
        hall.Time.ElapsedGameDays = 3;
        Assert.Single(hall.World.AdvanceTime());
        hall.Interaction.Update();

        InteractionSnapshot restored = InteractionSnapshot.From(hall.Interaction);
        Assert.Equal("A door", restored.Label);
        Assert.Equal("closed", restored.State);
        Assert.Equal("none", restored.Outcome);
        Assert.Empty(restored.Code);
        Assert.Empty(restored.Message);
    }

    [Fact]
    public void A_stale_use_names_a_current_target_that_changed_before_use()
    {
        using Hall hall = Hall.Build(new TestRule(), Hall.Facing("door-0"));
        hall.Interaction.Update();

        // Change the existing placement's state after focus was acquired. Engine revalidation rejects the
        // old incarnation, while the same placement remains present for a useful retry message.
        hall.World.Interactions.Record(HallPlace, new PlacementContentId("door", "door-0"), "open");
        InteractionResult refused = hall.Interaction.Use();

        Assert.False(refused.IsApplied);
        Assert.Equal(InteractionCodes.InteractionTargetChanged, refused.Code);
        Assert.Contains("A door changed before this use; try again.", refused.Message, StringComparison.Ordinal);
    }

    private static readonly ContentLayout Layout = new("packs", "imports", "bundles");
    private static readonly PlaceId HallPlace = new("1");
    private static readonly UseIntentNames UseControls = new("test.use", "test.ui.action.v1");
    private const double StepSeconds = 1.0 / 60.0;

    [Fact]
    public void Targets_are_discovered_from_the_places_content_and_what_the_party_faces()
    {
        using Hall hall = Hall.Build(new TestRule(), new PlacePose(0, 0, 0, 0, 0));

        // The party faces the place's second ground axis at a facing of zero, and the door standing that way
        // is what the reticle holds: its content identity, the ruleset's own word for it, and the distance the
        // party would have to close.
        hall.Interaction.Update();
        InteractionTarget? focused = hall.Interaction.FocusedTarget;

        Assert.NotNull(focused);
        Assert.Equal(new PlacementContentId("door", "door-0"), focused.Content);
        Assert.Equal(HallPlace, focused.Id.Place);
        Assert.Equal("door", focused.Definition.Kind.Value);
        Assert.Equal("A door", focused.Definition.Name);
        Assert.Equal(InteractionVerb.Open, focused.Verb);
        Assert.Equal(InteractionReason.Ready, hall.Interaction.FocusReason);
        Assert.Equal(100, hall.Interaction.FocusedDistance, 3);

        // The door behind the party and the door across the hall are in the place, but they are not what the
        // party is facing: a target is chosen by where the party stands and looks, never by a list.
        Assert.NotEqual(new PlacementContentId("door", "door-1"), focused.Content);
        Assert.NotEqual(new PlacementContentId("door", "door-2"), focused.Content);

        // Turning to face the other way changes what is held, with no code between the two: the same content,
        // read against a different pose.
        hall.Move(new PlacePose(0, 0, 0, 1024, 0));
        hall.Interaction.Update();
        Assert.Equal(new PlacementContentId("door", "door-1"), hall.Interaction.FocusedTarget?.Content);

        // A thing nothing uses is not a target at all: the spawn point and the light the place declares are
        // never offered, so standing among them leaves the reticle holding nothing.
        hall.Move(new PlacePose(-400, 0, 0, 512, 0));
        hall.Interaction.Update();
        Assert.Null(hall.Interaction.FocusedTarget);
        Assert.Equal(InteractionReason.NoCandidate, hall.Interaction.FocusReason);
    }

    [Fact]
    public void A_kind_of_thing_the_kit_has_never_heard_of_is_content_and_needs_no_new_class()
    {
        // The rule answers about a kind no kit type names, and the one mechanism discovers it, uses it, and
        // records what it left behind — which is what "a new kind means new content" has to mean in practice.
        TestRule rule = new();
        rule.Add("obelisk", new InteractionTargetDefinition(new InteractionTargetKind("obelisk"), "An obelisk", InteractionVerb.Read, reach: 400));
        rule.Outcomes["obelisk"] = (_, _) => InteractionOutcome.Applied("read", "The obelisk's letters are worn but readable.");

        using Hall hall = Hall.Build(rule, Hall.Facing("obelisk-0"));
        hall.Interaction.Update();
        Assert.Equal("obelisk", hall.Interaction.FocusedTarget?.Definition.Kind.Value);

        InteractionResult result = hall.Interaction.Use();
        Assert.True(result.IsApplied);
        Assert.Equal(InteractionVerb.Read, result.Verb);
        Assert.Equal("read", result.State);
        Assert.Contains("obelisk's letters", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void One_workflow_serves_search_open_pull_read_and_talk()
    {
        TestRule rule = new();
        rule.Outcomes["chest"] = (_, _) => InteractionOutcome.Applied("searched", "The chest holds nothing but dust.");
        rule.Outcomes["lever"] = (_, _) => InteractionOutcome.Applied("pulled", "The lever clanks down.");
        rule.Outcomes["sign"] = (_, _) => InteractionOutcome.Applied("read", "The sign reads: mind the drop.");
        rule.Outcomes["person"] = (_, _) => InteractionOutcome.Applied("spoken", "The stranger nods and says nothing.");

        // Every verb goes through the same three steps — identify, judge, apply — and the verb is the
        // ruleset's answer about the content rather than a branch in the mechanism.
        Assert.Equal(
            [
                (InteractionVerb.Search, "searched", "A chest"),
                (InteractionVerb.Pull, "pulled", "A lever"),
                (InteractionVerb.Read, "read", "A sign"),
                (InteractionVerb.Talk, "spoken", "A person"),
                (InteractionVerb.Open, "open", "A door"),
            ],
            [
                Use(rule, "chest-0"),
                Use(rule, "lever-0"),
                Use(rule, "sign-0"),
                Use(rule, "person-0"),
                Use(rule, "door-0"),
            ]);

        static (InteractionVerb, string, string) Use(TestRule rule, string placement)
        {
            using Hall hall = Hall.Build(rule, Hall.Facing(placement));
            hall.Interaction.Update();
            InteractionResult result = hall.Interaction.Use();
            Assert.True(result.IsApplied, $"{placement}: {result.Message}");
            return (result.Verb!.Value, result.State, result.TargetName);
        }
    }

    [Fact]
    public void A_requirement_the_party_does_not_meet_refuses_the_use_and_names_what_it_needs()
    {
        TestRule rule = new();
        rule.Requires["door"] = [new InteractionRequirement(InteractionRequirementKind.Item, "iron-key", label: "the Iron Key")];
        Verdict keyless = Verdict.Unmet("the party carries none of it");
        rule.Judgements["iron-key"] = keyless;

        using PartyEntity party = Party();
        using Hall hall = Hall.Build(rule, Hall.Facing("door-0"), party);

        // The door announces what it needs before anybody tries it, and the use is refused with the party's
        // lack named: a locked door that said nothing would be the silent no-op this mechanism exists to
        // prevent.
        hall.Interaction.Update();
        Assert.Equal(["the Iron Key"], hall.Interaction.FocusedTarget!.Definition.Requires.Select(requirement => requirement.Describe()));
        Assert.Equal(InteractionVerb.Unlock, hall.Interaction.FocusedTarget.Verb);

        InteractionResult refused = hall.Interaction.Use();
        Assert.False(refused.IsApplied);
        Assert.Equal(InteractionCodes.InteractionRequirementUnmet, refused.Code);
        Assert.Contains("the Iron Key", refused.Message, StringComparison.Ordinal);
        Assert.Contains(keyless.Explanation, refused.Message, StringComparison.Ordinal);

        // Nothing moved: the door is still what it was, and nothing was recorded against it.
        Assert.Equal(InteractionTargetState.None, hall.Interaction.FocusedTarget!.State);
        Assert.Equal("closed", hall.Interaction.FocusedTarget.Definition.State);

        // With the key in the party's own pack the same use turns the lock, and the use after that opens the
        // door: the requirement was the whole difference, and it was judged by the ruleset rather than by the
        // mechanism.
        rule.Judgements["iron-key"] = Verdict.Met;
        party.AcquireItem(new ItemDefinitionId("iron-key"), 1);
        InteractionResult unlocked = hall.Interaction.Use();
        Assert.True(unlocked.IsApplied);
        Assert.Equal(InteractionVerb.Unlock, unlocked.Verb);
        Assert.Equal("unlocked", unlocked.State);

        hall.Interaction.Update();
        Assert.Equal(1, hall.Interaction.FocusedTarget!.State.Revision);
        Assert.Equal("unlocked", hall.Interaction.FocusedTarget.Definition.State);
        Assert.Equal(InteractionVerb.Open, hall.Interaction.FocusedTarget.Verb);
        InteractionResult opened = hall.Interaction.Use();
        Assert.Equal("open", opened.State);

        hall.Interaction.Update();
        Assert.Equal(2, hall.Interaction.FocusedTarget!.State.Revision);
        Assert.Equal("open", hall.Interaction.FocusedTarget.Definition.State);
    }

    [Fact]
    public void What_a_use_gives_lands_in_the_shared_pack()
    {
        TestRule rule = new();
        rule.Outcomes["chest"] = (_, _) => InteractionOutcome.Applied(
            "searched",
            "The chest holds a purse.",
            items: [new InteractionItemYield(new ItemDefinitionId("coin-pouch"), 2)]);

        using PartyEntity party = Party();
        using Hall hall = Hall.Build(rule, Hall.Facing("chest-0"), party);
        hall.Interaction.Update();
        InteractionResult found = hall.Interaction.Use();

        // What a search finds enters the party through its one acquisition path, and the report says what was
        // taken rather than only that something happened.
        Assert.True(found.IsApplied);
        Assert.Equal(2, party.Inventory.TotalOf(new ItemDefinitionId("coin-pouch")));
        Assert.Contains("2 × coin-pouch", found.Message, StringComparison.Ordinal);
        hall.Interaction.Update();
        Assert.Equal(1, hall.Interaction.FocusedTarget!.State.Revision);
    }

    [Fact]
    public void A_use_that_reaches_nothing_is_an_outcome_with_a_reason_rather_than_silence()
    {
        using Hall hall = Hall.Build(new TestRule(), new PlacePose(4000, 4000, 0, 0, 0));
        hall.Interaction.Update();

        // Nothing is faced, and the use says exactly that rather than doing nothing quietly.
        InteractionResult nothing = hall.Interaction.Use();
        Assert.False(nothing.IsApplied);
        Assert.Null(nothing.Target);
        Assert.Equal("interaction-no-target", nothing.Code);
        Assert.NotEmpty(nothing.Message);
        InteractionSnapshot noTargetProjection = InteractionSnapshot.From(hall.Interaction);
        Assert.Empty(noTargetProjection.Target);
        Assert.Equal("refused", noTargetProjection.Outcome);
        Assert.Equal("interaction-no-target", noTargetProjection.Code);
        Assert.Equal(nothing.Message, noTargetProjection.Message);

        // A target the party faces but cannot see is refused with that reason, and the sight line comes from
        // the world rather than from the mechanism: the mover is what holds the collision.
        using Hall blind = Hall.Build(new TestRule(), Hall.Facing("door-0"), mover: new BlindMover());
        blind.Interaction.Update();
        Assert.Equal(InteractionReason.Occluded, blind.Interaction.FocusReason);
        Assert.Null(blind.Interaction.FocusedTarget);

        InteractionResult hidden = blind.Interaction.Use();
        Assert.False(hidden.IsApplied);
        Assert.Equal("interaction-occluded", hidden.Code);
    }

    [Fact]
    public void A_target_is_aimed_at_its_stated_height_and_seen_through_its_own_body()
    {
        // The lever stands on its point; the rule aims at 10 above its feet and lets 30 around that point not hide it.
        TestRule rule = new();
        rule.Add("lever", new InteractionTargetDefinition(new InteractionTargetKind("lever"), "A lever", InteractionVerb.Pull, reach: 256, state: "ready")
        {
            AimHeight = 10,
            Radius = 30,
        });
        SightMover sight = new();
        using Hall hall = Hall.Build(rule, Hall.Facing("lever-0"), mover: sight);
        hall.Interaction.Update();

        Assert.Equal("A lever", hall.Interaction.FocusedTarget!.Definition.Name);
        (_, System.Numerics.Vector3 to, float tolerance) = sight.Asked.Last(asked => asked.Target.Kind == "lever");
        Assert.Equal((10f, 30f), (to.Y, tolerance));
    }

    [Fact]
    public void A_refusal_the_ruleset_states_is_reported_with_its_own_code()
    {
        TestRule rule = new();
        rule.Outcomes["lever"] = (_, _) => InteractionOutcome.Refused(
            new Refusal("fixture-step-not-interpreted", "A lever raises event 42, whose step this game does not interpret."));

        using Hall hall = Hall.Build(rule, Hall.Facing("lever-0"));
        hall.Interaction.Update();
        InteractionResult refused = hall.Interaction.Use();

        // The ruleset's refusal is an outcome like any other: it carries a code, a sentence, and no change.
        Assert.False(refused.IsApplied);
        Assert.Equal("fixture-step-not-interpreted", refused.Code);
        Assert.Contains("42", refused.Message, StringComparison.Ordinal);
        Assert.Equal(InteractionVerb.Pull, refused.Verb);
        Assert.Equal(InteractionTargetState.None, hall.Interaction.FocusedTarget!.State);
    }

    [Fact]
    public void A_lever_that_moves_a_door_records_the_door_and_the_place_value_it_read()
    {
        // The lever reads the door's word and the place's count through its context, and states what it makes
        // of the door and the count in its outcome: the mechanism records both only because the use applied.
        TestRule rule = new();
        rule.Outcomes["lever"] = (_, context) =>
        {
            PlacementDefinition door = context.PlaceTargets.First(placement => placement.Content.Kind == "door");
            long pulls = context.PlaceValues.GetValueOrDefault("pulls");
            return InteractionOutcome.Applied(
                "pulled",
                $"The lever clanks down; the door was '{context.TargetState(door.Content)}'.",
                kept: new Dictionary<string, long> { ["pulls"] = pulls + 1 },
                changes: [new InteractionTargetChange(door.Content, "open")]);
        };

        using Hall hall = Hall.Build(rule, Hall.Facing("lever-0"));
        hall.Interaction.Update();
        InteractionResult pulled = hall.Interaction.Use();
        Assert.True(pulled.IsApplied, pulled.Message);
        Assert.Equal("The lever clanks down; the door was ''.", pulled.Message);

        WorldSave saved = hall.World.Capture();
        Assert.Equal(new PlaceValue("pulls", 1), Assert.Single(Assert.Single(saved.Interaction.Places).Values));

        // The door now reads as the lever left it, under its own identity and incarnation.
        IInteractionWorld world = hall.World;
        PlacementDefinition doorPlacement = world.Placements.First(placement => placement.Content.Kind == "door");
        Assert.Equal(new InteractionTargetState("open", 1), world.States.StateOf(world.Place, doorPlacement.Content));
    }

    [Fact]
    public void A_place_restored_forgets_what_the_party_did_to_its_targets()
    {
        using Hall hall = Hall.Build(new TestRule(), Hall.Facing("door-0"));
        hall.Interaction.Update();
        Assert.Equal("open", hall.Interaction.Use().State);

        hall.Interaction.Update();
        Assert.Equal("open", hall.Interaction.FocusedTarget!.Definition.State);
        Assert.Equal(1, hall.Interaction.FocusedTarget.State.Revision);

        // Opening a door that is already open is an outcome of its own rather than a second opening.
        hall.Interaction.Update();
        Assert.Equal("door-already-open", hall.Interaction.Use().Code);

        // Restoring the place restores what it holds: a door the party forced belongs to the visit that did
        // it, so the place comes back as content says it was.
        hall.Time.ElapsedGameDays = 7;
        IReadOnlyList<PlaceState> restored = hall.World.AdvanceTime();
        Assert.Equal(HallPlace, Assert.Single(restored).Place);

        hall.Interaction.Update();
        Assert.Equal(InteractionTargetState.None, hall.Interaction.FocusedTarget!.State);
        Assert.Equal("closed", hall.Interaction.FocusedTarget.Definition.State);
    }

    [Fact]
    public void A_session_steps_the_mechanism_inside_its_one_admitted_update()
    {
        TestRule rule = new();
        rule.Outcomes["lever"] = (_, _) => InteractionOutcome.Applied("pulled", "The lever clanks down.");

        using PartyEntity party = Party();
        using Hall hall = Hall.Build(rule, Hall.Facing("door-0"), party);
        using RecordingUiProjectionChannel channel = new();
        using PartyRpgSession session = new(
            new SessionComposition(new RulesetId("test.ruleset"), "Test"),
            channel,
            new SessionOwners(null),
            new SessionParty.Playing(World: hall.World, Party: party),
            controls: new SessionControls
            {
                Use = UseControls,
            });

        // The projection that exists before any update says the session can interact and that nothing is
        // faced yet: what the party faces is read from where its last admitted step put it, which is a fact
        // the first update produces.
        ProjectedNode before = channel.Latest().Field(SessionProjection.InteractionField);
        Assert.True(before.Field("available").AsBoolean());
        Assert.Equal(string.Empty, before.Field("label").AsString());
        Assert.Equal("none", before.Field("outcome").AsString());

        // The first update faces the door the party stands in front of, and an update that carries no
        // request uses nothing.
        session.Update(Admitted.Update(1, 1));
        ProjectedNode faced = channel.Latest().Field(SessionProjection.InteractionField);
        Assert.Equal("A door", faced.Field("label").AsString());
        Assert.Equal("open", faced.Field("verb").AsString());
        Assert.Equal("closed", faced.Field("state").AsString());
        Assert.Equal("ready", faced.Field("reason").AsString());
        session.Update(Admitted.Update(2, 1));
        Assert.Equal(InteractionTargetState.None, hall.Interaction.FocusedTarget!.State);

        // The update that carries the press uses what the step before it put in front of the party, and the
        // outcome is part of the same projection the input arrived in.
        session.Update(Admitted.Update(3, 1, Admitted.Digital("test.use", InputEdge.Pressed)));
        Assert.Equal("open", hall.Interaction.FocusedTarget!.State.State);
        ProjectedNode after = channel.Latest().Field(SessionProjection.InteractionField);
        Assert.Equal("applied", after.Field("outcome").AsString());
        Assert.Equal("open", after.Field("state").AsString());
        Assert.Contains("swings open", after.Field("message").AsString(), StringComparison.Ordinal);
        Assert.Contains("cannot be walked through yet", after.Field("residue").AsString(), StringComparison.Ordinal);

        // A use is an instant, not a state: a key reported as held — which the engine sends every update it
        // stays down — uses nothing, and neither does a payload that names some other action.
        session.Update(Admitted.Update(4, 1, Admitted.Digital("test.use", InputEdge.Held)));
        Assert.Equal(1, hall.Interaction.FocusedTarget!.State.Revision);
        session.Update(Admitted.Update(5, 1, Admitted.Payload(UseControls.ActionContract, """{"action":"something.else"}""")));
        session.Update(Admitted.Update(6, 1, Admitted.Payload(UseControls.ActionContract, "not json at all")));
        Assert.Equal(1, hall.Interaction.FocusedTarget!.State.Revision);

        // The panel's own control asks on the payload contract, and it uses exactly what the key uses.
        hall.Move(Hall.Facing("lever-0"));
        session.Update(Admitted.Update(7, 1, Admitted.Payload(UseControls.ActionContract, """{"action":"party.use"}""")));
        Assert.Equal("pulled", hall.Interaction.FocusedTarget!.State.State);

        // A held session still uses: a use is an instant rather than an interval, so a lever pulled while
        // the world is held is an act rather than a passage of time.
        hall.Move(Hall.Facing("door-0"));
        session.Hold();
        session.Update(Admitted.Update(8, 1, Admitted.Digital("test.use", InputEdge.Pressed)));
        Assert.Equal("door-already-open", session.LiveWorld!.LastInteraction!.Code);
        Assert.Equal("refused", channel.Latest().Field(SessionProjection.InteractionField).Field("outcome").AsString());
    }

    [Fact]
    public void A_session_whose_ruleset_answered_no_interaction_publishes_that_it_holds_none()
    {
        using Hall hall = Hall.Build(new TestRule(), Hall.Facing("door-0"), interactive: false);
        using RecordingUiProjectionChannel channel = new();
        using PartyRpgSession session = new(
            new SessionComposition(new RulesetId("test.ruleset"), "Test"),
            channel,
            new SessionOwners(),
            new SessionParty.Playing(World: hall.World));

        Assert.Null(hall.World.Interaction);
        Assert.Null(hall.World.Interact(use: true));

        ProjectedNode interaction = channel.Latest().Field(SessionProjection.InteractionField);
        Assert.False(interaction.Field("available").AsBoolean());
        Assert.Equal("none", interaction.Field("outcome").AsString());
        Assert.Equal(string.Empty, interaction.Field("label").AsString());
    }

    [Fact]
    public void Every_requirement_kind_the_kit_states_is_answered_by_the_ruleset()
    {
        // The kit's vocabulary is judged by the ruleset and by nothing else: the same mechanism carries an
        // item, a skill, a flag, and a part of the day to the rule, and what each one means is the rule's
        // answer. This test's rule refuses the flag and accepts the rest, and the mechanism stops at the one
        // the party does not meet.
        TestRule rule = new();
        rule.Requires["door"] =
        [
            new InteractionRequirement(InteractionRequirementKind.Item, "iron-key", label: "the Iron Key"),
            new InteractionRequirement(InteractionRequirementKind.Skill, "perception", 4, "Perception"),
            new InteractionRequirement(InteractionRequirementKind.TimeOfDay, "day", label: "daylight"),
            new InteractionRequirement(InteractionRequirementKind.Flag, "cellar-opened", label: "the cellar opened"),
        ];
        Verdict untrained = Verdict.Unmet("no member has trained it");
        rule.Judgements["perception"] = untrained;

        using PartyEntity party = Party();
        using Hall hall = Hall.Build(rule, Hall.Facing("door-0"), party);
        hall.Interaction.Update();

        Assert.Equal(
            ["the Iron Key", "Perception 4", "daylight", "the cellar opened"],
            hall.Interaction.FocusedTarget!.Definition.Requires.Select(requirement => requirement.Describe()));

        InteractionResult refused = hall.Interaction.Use();
        Assert.Equal(InteractionCodes.InteractionRequirementUnmet, refused.Code);
        Assert.Contains("Perception 4", refused.Message, StringComparison.Ordinal);
        Assert.Contains(untrained.Explanation, refused.Message, StringComparison.Ordinal);

        // The rule is asked about each requirement in order until one is unmet, so a use that needs four
        // things answers with the one the party is missing rather than with all four: the flag behind the
        // skill is never judged, because nothing after the first refusal could change the answer.
        Assert.Equal(["iron-key", "perception"], rule.Judged);
    }

    /// <summary>A party that carries whatever a test gives it, with the rules a test states.</summary>
    private static PartyEntity Party(int coins = 0) =>
        new PartyEntityFactory().Create(
            new PartyCreation(
                [new MemberCreation(new PartyMemberSeed(
                    "Tester",
                    new RaceId("testfolk"),
                    new ClassId("fighter"),
                    [new AttributeScore(new AttributeId("vigour"), 12)],
                    skills: [],
                    spells: [],
                    experience: 0,
                    level: 1,
                    skillPoints: 0,
                    classRank: 1,
                    conditions: [],
                    hitPoints: ResourcePool.Full(10),
                    spellPoints: ResourcePool.Full(5)))],
                coins,
                foodPortions: 4,
                ProvisionUnit.Portions,
                reputation: 0,
                fame: 0));

    /// <summary>A mover that holds no collision, sees everything, and records what each target was looked at with.</summary>
    private sealed class SightMover : IPartyMover
    {
        internal List<(PlacementContentId Target, System.Numerics.Vector3 To, float Tolerance)> Asked { get; } = [];

        public PlaceGeometryAdmission Enter(PlaceId place) => PlaceGeometryAdmission.Empty(place);

        public MovementOutcome Step(MovementIntent intent, double elapsedSeconds) =>
            new(PlacePose.Origin, System.Numerics.Vector3.Zero, Grounded: true, default, CharacterBlockFlags.None, default, SurfaceEffect.Ordinary, FallOutcome.None);

        public bool InSight(System.Numerics.Vector3 from, System.Numerics.Vector3 to) => true;

        public bool InSight(System.Numerics.Vector3 from, System.Numerics.Vector3 to, PlacementContentId target, float tolerance)
        {
            Asked.Add((target, to, tolerance));
            return true;
        }

        public void Dispose()
        {
        }
    }

    /// <summary>A mover that holds no collision but reports everything it is asked about as unseen.</summary>
    private sealed class BlindMover : IPartyMover
    {
        public PlaceGeometryAdmission Enter(PlaceId place) => PlaceGeometryAdmission.Empty(place);

        public MovementOutcome Step(MovementIntent intent, double elapsedSeconds) =>
            new(
                PlacePose.Origin,
                System.Numerics.Vector3.Zero,
                Grounded: true,
                default,
                CharacterBlockFlags.None,
                default,
                SurfaceEffect.Ordinary,
                FallOutcome.None);

        public bool InSight(System.Numerics.Vector3 from, System.Numerics.Vector3 to) => false;

        public void Dispose()
        {
        }
    }

    /// <summary>
    /// The rules a test states: what a kind of placement offers, what a requirement means, and what a use
    /// produces.
    /// </summary>
    private sealed class TestRule : IInteractionRule
    {
        private readonly Dictionary<string, InteractionTargetDefinition> _definitions = new(StringComparer.Ordinal)
        {
            ["door"] = new(new InteractionTargetKind("door"), "A door", InteractionVerb.Open, reach: 512, state: "closed"),
            ["chest"] = new(new InteractionTargetKind("chest"), "A chest", InteractionVerb.Search, reach: 384, state: "sealed"),
            ["lever"] = new(new InteractionTargetKind("lever"), "A lever", InteractionVerb.Pull, reach: 256, state: "ready"),
            ["sign"] = new(new InteractionTargetKind("sign"), "A sign", InteractionVerb.Read, reach: 256, state: "legible"),
            ["person"] = new(new InteractionTargetKind("person"), "A person", InteractionVerb.Talk, reach: 512, state: "waiting"),
        };

        internal TestRule(string? only = null)
        {
            if (only is null) return;
            foreach (string kind in _definitions.Keys.Where(kind => kind != only).ToArray()) _definitions.Remove(kind);
        }

        /// <summary>What each placement kind requires, in the order the checks happen.</summary>
        internal Dictionary<string, IReadOnlyList<InteractionRequirement>> Requires { get; } = new(StringComparer.Ordinal);


        /// <summary>What a use of each kind produces.</summary>
        internal Dictionary<string, Func<InteractionTargetDefinition, InteractionContext, InteractionOutcome>> Outcomes { get; } = new(StringComparer.Ordinal);

        /// <summary>What each requirement is answered with, met when a test says nothing.</summary>
        internal Dictionary<string, Verdict> Judgements { get; } = new(StringComparer.Ordinal);

        /// <summary>Every requirement the rule was asked about, in order, so the workflow's order is visible.</summary>
        internal List<string> Judged { get; } = [];

        internal void Add(string placementKind, InteractionTargetDefinition definition) => _definitions[placementKind] = definition;

        public InteractionTargetDefinition? Describe(InteractionTargetRequest request)
        {
            if (!_definitions.TryGetValue(request.Placement.Content.Kind, out InteractionTargetDefinition? definition)) return null;

            // A door's own state is the word the mechanism carries back to the rule; every other kind in this
            // test is the same however often it is used.
            string state = request.State.Length > 0
                ? request.State
                : string.Equals(request.Placement.Content.Kind, "door", StringComparison.Ordinal) &&
                  request.Placement.Source.GetInt32("state") == 0
                    ? "open"
                    : definition.State;

            IReadOnlyList<InteractionRequirement> requires = Requires.GetValueOrDefault(request.Placement.Content.Kind, []);
            return definition with
            {
                State = state,
                // A door whose lock is stated and not yet turned offers the use that turns it, which is the
                // ruleset's answer about the verb and never the mechanism's.
                Verb = requires.Count > 0 && state != "unlocked" ? InteractionVerb.Unlock : definition.Verb,
                Requires = requires,
            };
        }

        /// <summary>
        /// Nothing in this hall guards itself: a trap is what a container test states, and the workflow that
        /// applies one is exercised there rather than here.
        /// </summary>
        public InteractionTrap? Trap(InteractionTargetDefinition target, InteractionContext context)
        {
            _ = target;
            _ = context;
            return null;
        }

        public Verdict Judge(InteractionRequirement requirement, InteractionContext context)
        {
            Judged.Add(requirement.Name);
            return Judgements.GetValueOrDefault(requirement.Name, Verdict.Met);
        }

        public InteractionOutcome Apply(InteractionTargetDefinition target, InteractionContext context) =>
            Outcomes.TryGetValue(target.Kind.Value, out Func<InteractionTargetDefinition, InteractionContext, InteractionOutcome>? outcome)
                ? outcome(target, context)
                : target.Kind.Value switch
                {
                    "door" when target.State == "open" => InteractionOutcome.Refused(new Refusal("door-already-open", "A door already stands open.")),
                    "door" when target.Requires.Count > 0 && target.State != "unlocked" =>
                        InteractionOutcome.Applied("unlocked", "What the door was locked with is to hand."),
                    "door" => InteractionOutcome.Applied(
                        "open",
                        "A door swings open.",
                        "Doors do not move in this build, so the doorway cannot be walked through yet."),
                    _ => InteractionOutcome.Refused(new Refusal("test-no-outcome", $"Nothing states what using {target.Name} does.")),
                };
    }

    /// <summary>
    /// The world these tests interact with: one interior whose content declares every kind of thing, and a
    /// time source a test moves by hand.
    /// </summary>
    private sealed class Hall : IDisposable
    {
        private readonly PartyEntity? _party;

        private Hall(SessionWorld world, PartyEntity? party, TestRule rule, FakeTimeSource time)
        {
            World = world;
            _party = party;
            Rule = rule;
            Time = time;
        }

        internal SessionWorld World { get; }

        internal TestRule Rule { get; }

        internal FakeTimeSource Time { get; }

        internal PartyInteraction Interaction => World.Interaction ?? throw new InvalidOperationException("This hall was built without an interaction policy.");

        /// <summary>Where a party stands to face one named placement from the place's arrival point.</summary>
        internal static PlacePose Facing(string placement) => placement switch
        {
            "door-0" => new PlacePose(0, 0, 0, 0, 0),
            "door-1" => new PlacePose(0, 0, 0, 1024, 0),
            "chest-0" => new PlacePose(0, 0, 0, 1536, 0),
            "lever-0" => new PlacePose(0, 0, 0, 512, 0),
            "sign-0" => new PlacePose(0, 0, 0, 1792, 0),
            "person-0" => new PlacePose(0, 0, 0, 768, 0),
            "obelisk-0" => new PlacePose(0, 0, 0, 256, 0),
            _ => throw new ArgumentOutOfRangeException(nameof(placement), placement, "No pose in this hall faces that placement."),
        };

        internal void Move(PlacePose pose) => World.ArriveAt(HallPlace, pose);

        internal static Hall Build(
            TestRule rule,
            PlacePose pose,
            PartyEntity? party = null,
            IPartyMover? mover = null,
            bool interactive = true,
            InteractionTuning? tuning = null)
        {
            ContentCatalog catalog = ContentCatalogLoader.Load(
                new InMemoryContentSource()
                    .Add("packs/world/pack.json", TestPacks.PlacesOnly)
                    .Add("packs/world/places.json", TestPacks.Document("places", "place", Place())),
                Layout).RequireValid();

            PlaceGraph graph = PlaceGraphLoader.Load(catalog);
            FakeTimeSource time = new();
            PartyPoseOwner owner = new(new PartyPose(HallPlace, pose), new FacingRule(unitsPerTurn: 2048, minimumPitch: -512, maximumPitch: 512));
            // The same axis rule the party's movement walks by: a place facing of zero points along the
            // place's second ground axis, and a facing of 512, 1024, and 1536 turns to its first axis, its
            // second axis negated, and its first axis negated.
            InteractionPolicy policy = new(
                rule,
                PlaceSpace.HeightIsThird(new FacingRule(unitsPerTurn: 2048, minimumPitch: -512, maximumPitch: 512), radiansAtZeroFacing: 0),
                tuning ?? new InteractionTuning(acquisitionAngleRadians: 0.20, releaseAngleRadians: 0.31));

            SessionWorld world = new(
                graph,
                owner,
                new PlaceStateLedger(graph, PlaceRespawnRule.FromContent()),
                new FaresRefusedTravel(),
                time: time,
                mover: mover,
                resources: party is null ? null : new PartyResourceLedger(party),
                partyEntity: party,
                interaction: interactive ? policy : null);
            return new Hall(world, party, rule, time);
        }

        public void Dispose()
        {
            World.Dispose();
            _party?.Dispose();
        }

        /// <summary>The place, with everything the mechanism has to discover and everything it must ignore.</summary>
        private static string Place() =>
            """
            { "id": "1", "kind": "interior", "name": "Hall", "respawnDays": 3,
              "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
              "placements": [
                { "id": "door-0", "kind": "door", "x": 0, "y": 100, "z": 0, "state": 2 },
                { "id": "door-1", "kind": "door", "x": 0, "y": -100, "z": 0, "state": 2 },
                { "id": "door-2", "kind": "door", "x": 0, "y": 4000, "z": 0, "state": 2 },
                { "id": "chest-0", "kind": "chest", "x": 100, "y": 0, "z": 0 },
                { "id": "lever-0", "kind": "lever", "x": -100, "y": 0, "z": 0 },
                { "id": "sign-0", "kind": "sign", "x": 100, "y": 100, "z": 0 },
                { "id": "person-0", "kind": "person", "x": -100, "y": -100, "z": 0 },
                { "id": "obelisk-0", "kind": "obelisk", "x": -200, "y": 200, "z": 0 },
                { "id": "spawn-0", "kind": "spawn", "x": -50, "y": 0, "z": 0 },
                { "id": "light-0", "kind": "light", "x": -60, "y": 0, "z": 64 } ] }
            """;
    }

    /// <summary>A day source the test moves by hand, standing in for the clock a later stone wires.</summary>
    private sealed class FakeTimeSource : IWorldTimeSource
    {
        public int ElapsedGameDays { get; set; }
    }
}
