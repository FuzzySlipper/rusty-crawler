using System.Globalization;
using System.Text;
using PartyRpg.Kit.Alchemy;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Skills;
using PartyRpg.Kit.Time;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// The session's own glue, proven over the kit's fakes rather than through a game: a player's act reaches the
/// owner it belongs to through the one admitted update, and what the session refuses it reports by name.
/// </summary>
public sealed class SessionGlueTests
{
    private const string Contract = "test.actions";
    private static readonly SessionComposition Composition = new(new RulesetId("test.ruleset"), "Test");

    [Fact]
    public void A_mixture_asked_for_on_the_panel_is_made_by_the_one_mixing_workflow()
    {
        RecordingDiagnosticsService diagnostics = new();
        using RecordingUiProjectionChannel channel = new();
        PartyEntity party = AlchemyTests.Party(alchemyLevel: 3, alchemyTier: 1);
        ItemInstance berry = AlchemyTests.Take(party, AlchemyTests.Berry);
        ItemInstance bottle = AlchemyTests.Take(party, AlchemyTests.Bottle);
        AlchemyCatalog mixtures = new([
            new PotionMixture(AlchemyTests.Berry, AlchemyTests.Bottle, MixtureOutcome.Produces(AlchemyTests.Draught), new SkillTier(1), Power: 5, Note: 58),
        ]);
        using PartyRpgSession session = new(
            Composition,
            channel,
            new SessionOwners(diagnostics: diagnostics),
            new SessionParty.Playing(Party: party),
            new SessionRules { Alchemy = new AlchemyRules(new AlchemyTests.Rule(), mixtures) },
            new SessionControls { Mix = new MixIntentNames(Contract) });
        session.Start();

        // A pair the game states no mixture for is refused by the workflow in its own words, and both items stay.
        session.Update(Admitted.Update(1, 1, Payload($$"""{"action":"party.mix","member":0,"first":"{{berry.Id}}","second":"{{berry.Id}}"}""")));
        DiagnosticsPublishRequest refused = Assert.Single(diagnostics.Published, report => report.Source == "alchemy");
        Assert.Equal(DiagnosticsSeverity.Warning, refused.Severity);
        Assert.Equal(2, party.Inventory.Count);

        session.Update(Admitted.Update(2, 1, Payload($$"""{"action":"party.mix","member":0,"first":"{{berry.Id}}","second":"{{bottle.Id}}"}""")));
        Assert.Equal(AlchemyTests.Draught, Assert.Single(party.Items).Definition);
        Assert.Contains(diagnostics.Published, report => report.Code == "mixture-mixed");
        Assert.Equal("mixture-mixed", channel.Latest().Field("alchemy").Field("outcome").Field("code").AsString());
    }

    [Fact]
    public void A_quick_spell_for_nobody_or_for_a_spell_never_learned_is_refused_by_name()
    {
        RecordingDiagnosticsService diagnostics = new();
        using RecordingUiProjectionChannel channel = new();
        PartyEntity party = AlchemyTests.Party(alchemyLevel: 0, alchemyTier: 0);
        using PartyRpgSession session = new(
            Composition,
            channel,
            new SessionOwners(diagnostics: diagnostics),
            new SessionParty.Playing(Party: party),
            new SessionRules { Magic = Capabilities.Magic(new AlchemyTests.Spells(AlchemyTests.Draught)) },
            new SessionControls { Cast = new CastIntentNames(Contract) });
        session.Start();

        session.Update(Admitted.Update(1, 1, Payload("""{"action":"party.quick-spell","member":5,"spell":"potion:draught"}""")));
        session.Update(Admitted.Update(2, 1, Payload("""{"action":"party.quick-spell","member":0,"spell":"potion:draught"}""")));

        Assert.Equal(
            ["spell-member-unknown", "spell-quick-refused"],
            diagnostics.Published.Where(report => report.Source == "magic").Select(report => report.Code));
        Assert.Contains("member 6", diagnostics.Published[0].Message, StringComparison.Ordinal);
        Assert.Null(party.Members[0].Spells.QuickSpell);
    }

    [Fact]
    public void The_pacing_switches_only_a_fight_there_is_and_a_turn_outside_one_is_refused()
    {
        RecordingDiagnosticsService diagnostics = new();
        using RecordingUiProjectionChannel channel = new();
        TurnIntentNames turns = new("test.turn-based", "test.skip", "test.wait", Contract);

        // A session whose game answered no combat has nothing to pace.
        using (PartyRpgSession fightless = new(
            Composition,
            channel,
            new SessionOwners(diagnostics: diagnostics),
            new SessionParty.Playing(Party: AlchemyTests.Party(alchemyLevel: 0, alchemyTier: 0)),
            controls: new SessionControls { Combat = new CombatIntentNames("test.attack", Contract, turns) }))
        {
            fightless.Start();
            fightless.Update(Admitted.Update(1, 1, Admitted.Digital("test.turn-based")));
            Assert.Equal("combat-pacing-unavailable", Assert.Single(diagnostics.Published).Code);
        }

        // With a fight, the switch is reported beside the fight's own account of it; a turn passed while nothing is
        // being fought has nowhere to go.
        diagnostics = new();
        using PartyRpgSession session = new(
            Composition,
            channel,
            new SessionOwners(TestClock.Create(), diagnostics),
            new SessionParty.Playing(Party: AlchemyTests.Party(alchemyLevel: 0, alchemyTier: 0)),
            new SessionRules { Combat = Capabilities.Combat(new CombatStateTests.TestCombatRule(null)) },
            new SessionControls { Combat = new CombatIntentNames("test.attack", Contract, turns) });
        session.Start();
        session.Update(Admitted.Update(1, 1, Admitted.Digital("test.turn-based")));
        session.Update(Admitted.Update(2, 1, Admitted.Digital("test.skip")));

        Assert.Equal(CombatPacing.TurnBased, session.Combat!.Pacing);
        Assert.Equal(
            ["combat-pacing-toggle", "no-turn"],
            diagnostics.Published
                .Where(report => report.Code is "combat-pacing-toggle" or "no-turn")
                .Select(report => report.Code));
        Assert.Equal(DiagnosticsSeverity.Warning, diagnostics.Published.Single(report => report.Code == "no-turn").Severity);
    }

    [Fact]
    public void Menu_owned_update_cannot_switch_hidden_combat_pacing_or_advance_time()
    {
        RecordingDiagnosticsService diagnostics = new();
        using RecordingUiProjectionChannel channel = new();
        SessionMenuState menu = new();
        TurnIntentNames turns = new("test.turn-based", "test.skip", "test.wait", Contract);
        using PartyRpgSession session = new(
            Composition,
            channel,
            new SessionOwners(TestClock.Create(), diagnostics),
            new SessionParty.Playing(Party: AlchemyTests.Party(alchemyLevel: 0, alchemyTier: 0)),
            new SessionRules { Combat = Capabilities.Combat(new CombatStateTests.TestCombatRule(null)) },
            new SessionControls { Combat = new CombatIntentNames("test.attack", Contract, turns) },
            menu: menu);
        session.Start();

        menu.SetControlsOwnedForUpdate(true);
        session.Update(Admitted.Update(1, 60, Admitted.Digital("test.turn-based")));
        menu.SetControlsOwnedForUpdate(false);

        Assert.Equal(CombatPacing.RealTime, session.Combat!.Pacing);
        Assert.Equal(0, session.SimulationSeconds);
        Assert.Empty(diagnostics.Published);
    }

    [Fact]
    public void Lifecycle_menu_ownership_preserves_member_selection_until_the_menu_closes()
    {
        RecordingDiagnosticsService diagnostics = new();
        using RecordingUiProjectionChannel channel = new();
        SessionMenuState menu = new();
        TurnIntentNames turns = new("test.turn-based", "test.skip", "test.wait", Contract);
        PartyEntity party = AlchemyTests.Party(alchemyLevel: 0, alchemyTier: 0);
        using PartyRpgSession session = new(
            Composition,
            channel,
            new SessionOwners(TestClock.Create(), diagnostics),
            new SessionParty.Playing(Party: party),
            new SessionRules { Combat = Capabilities.Combat(new CombatStateTests.TestCombatRule(null)) },
            new SessionControls
            {
                Combat = new CombatIntentNames("test.attack", Contract, turns, nextMember: "test.next-member"),
            },
            menu: menu);
        session.Start();
        // The first ordinary update populates the party combatants; selection input in later updates then has a
        // real candidate to choose, just as the product's world update does after creation is accepted.
        session.Update(Admitted.Update(1, 1));
        PartyMemberId initial = party.Roster.SelectedMember!.Value;

        menu.ShowSaveLoad(unsaved: false);
        menu.SetControlsOwnedForUpdate(true);
        session.Update(Admitted.Update(2, 1, Admitted.Digital("test.next-member")));
        menu.SetControlsOwnedForUpdate(false);
        Assert.Equal(initial, party.Roster.SelectedMember);

        menu.ShowReturnConfirmation(unsaved: false);
        menu.SetControlsOwnedForUpdate(true);
        session.Update(Admitted.Update(3, 1, Admitted.Digital("test.next-member")));
        menu.SetControlsOwnedForUpdate(false);
        Assert.Equal(initial, party.Roster.SelectedMember);

        // Closing the menu and the selection key in one admitted update are still owned by the menu.
        menu.ShowAdventure();
        menu.SetControlsOwnedForUpdate(true);
        session.Update(Admitted.Update(4, 1, Admitted.Digital("test.next-member")));
        menu.SetControlsOwnedForUpdate(false);
        Assert.Equal(initial, party.Roster.SelectedMember);

        session.Update(Admitted.Update(5, 1, Admitted.Digital("test.next-member")));
        Assert.NotEqual(initial, party.Roster.SelectedMember);
    }

    /// <summary>One payload action on this suite's contract, as the companion sends it.</summary>
    private static ProductInputEvent Payload(string json) => Admitted.Payload(Contract, json);
}
