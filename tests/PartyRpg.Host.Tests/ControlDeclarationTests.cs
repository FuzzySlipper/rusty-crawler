using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Progression;
using PartyRpg.Kit.Sessions;
using Xunit;

namespace PartyRpg.Host.Tests;

/// <summary>
/// The product's identity and every control it declares, held to both halves of the declaration: the constant in
/// <see cref="ProductIdentity"/> and the project file the engine reads.
/// </summary>
/// <remarks>
/// The engine learns the admitted intent names from the project file and rejects a mapping whose intent the product
/// never declared, so a name that exists in code alone is a control that silently does nothing, and a key that is
/// mapped in the project alone presses nothing. Each key is checked against the intent its mapping names, so a key
/// moved to another control fails here rather than passing because the text is somewhere in the file.
/// </remarks>
public sealed class ControlDeclarationTests
{
    [Fact]
    public void The_product_identity_in_code_is_the_one_the_project_declares()
    {
        Assert.Equal(ProductIdentity.Id, ProductDeclarations.Property("RustyEngineProductId"));
        Assert.Equal(ProductIdentity.Title, ProductDeclarations.Property("RustyEngineProductTitle"));
        Assert.Equal(ProductIdentity.UiStream, ProductDeclarations.Property("RustyEngineProductUiProjectionStream"));
        Assert.Equal(ProductIdentity.UiContract, ProductDeclarations.Property("RustyEngineProductUiProjectionContract"));

        // The payload channel is the one intent that carries a value rather than a press: its value names the
        // contract the companion's actions arrive on, which is the contract every reader is composed over.
        Assert.Equal($"payload:{ProductIdentity.UiActionContract}", ProductDeclarations.ValueOf(ProductIdentity.UiActionIntent));
    }

    [Fact]
    public void Every_intent_the_host_names_in_code_is_declared_in_its_project_and_the_other_way_round()
    {
        // The movement, creation, save, use, service, conversation, stop, act, and pace intents — every one the
        // product reads — are named once in code and declared once in the project file.
        Assert.Equal(
            ProductDeclarations.IntentsInCode,
            ProductDeclarations.DeclaredIntents.Select(entry => entry.Intent).Order(StringComparer.Ordinal));

        // Every intent but the payload channel is a press, and every press is bound to one key, so the panel can
        // name it; the payload channel is claimed by a screen's own buttons instead.
        foreach ((string intent, string value) in ProductDeclarations.DeclaredIntents.Where(entry => entry.Intent != ProductIdentity.UiActionIntent))
        {
            Assert.Equal("digital", value);
            Assert.StartsWith("key:", ProductDeclarations.TriggerOf(intent), StringComparison.Ordinal);
        }

        ControlKeys keys = ProductControlKeys.Read(ProductTestContext.DeclaredInput());
        Assert.All(
            typeof(ControlKeys).GetProperties().Where(property => property.PropertyType == typeof(string)),
            property => Assert.False(string.IsNullOrEmpty((string?)property.GetValue(keys)), $"{property.Name} is bound to no key"));
    }

    /// <summary>
    /// The key each control is bound to, named so a change to one is a decision rather than a silent edit.
    /// </summary>
    /// <param name="intent">The intent, read from the constant the product composes its readers with.</param>
    /// <param name="trigger">The trigger the project's mapping for that intent must state.</param>
    /// <param name="label">The key's name as the panel shows it.</param>
    [Theory]
    [InlineData(ProductIdentity.PauseToggleIntent, "key:key-p:pressed", "P")]
    // Movement holds: a key held walks, and letting go stops.
    [InlineData(ProductIdentity.MoveForwardIntent, "key:key-w:held", "W")]
    [InlineData(ProductIdentity.MoveBackIntent, "key:key-s:held", "S")]
    [InlineData(ProductIdentity.StrafeLeftIntent, "key:key-a:held", "A")]
    [InlineData(ProductIdentity.StrafeRightIntent, "key:key-d:held", "D")]
    [InlineData(ProductIdentity.TurnLeftIntent, "key:key-q:held", "Q")]
    [InlineData(ProductIdentity.TurnRightIntent, "key:key-e:held", "E")]
    // The engine names the space bar `space`; `key-space` is refused at load.
    [InlineData(ProductIdentity.JumpIntent, "key:space:pressed", "Space")]
    // The original rises on Page Up and sinks on Insert; the engine carries neither, so flight takes the arrows, held.
    [InlineData(ProductIdentity.AscendIntent, "key:arrow-up:held", "Arrow Up")]
    [InlineData(ProductIdentity.DescendIntent, "key:arrow-down:held", "Arrow Down")]
    [InlineData(ProductIdentity.CreationAdvanceIntent, "key:enter:pressed", "Enter")]
    [InlineData(ProductIdentity.CreationAcceptIntent, "key:space:pressed", "Space")]
    // The engine's keyboard controls carry no function keys, so the save control is a letter rather than F5.
    [InlineData(ProductIdentity.SaveIntent, "key:key-f:pressed", "F")]
    // The original's interaction key is Space and its jump key is X; Space already jumps here.
    [InlineData(ProductIdentity.UseIntent, "key:key-g:pressed", "G")]
    [InlineData(ProductIdentity.ServiceLeaveIntent, "key:key-x:pressed", "X")]
    // The original leaves a house's dialogue with Escape, and this build keeps it.
    [InlineData(ProductIdentity.ConversationLeaveIntent, "key:escape:pressed", "Escape")]
    // R is the original's own rest key.
    [InlineData(ProductIdentity.RestIntent, "key:key-r:pressed", "R")]
    [InlineData(ProductIdentity.CampIntent, "key:key-c:pressed", "C")]
    [InlineData(ProductIdentity.WaitUntilDawnIntent, "key:key-t:pressed", "T")]
    [InlineData(ProductIdentity.WaitAnHourIntent, "key:key-h:pressed", "H")]
    [InlineData(ProductIdentity.WaitFiveMinutesIntent, "key:key-m:pressed", "M")]
    // The donor's own act control is B, held so a held key keeps attacking.
    [InlineData(ProductIdentity.AttackIntent, "key:key-b:held", "B")]
    // Enter is the original's own turn-based toggle; the two turn actions take letters no other control claims.
    [InlineData(ProductIdentity.TurnBasedToggleIntent, "key:enter:pressed", "Enter")]
    [InlineData(ProductIdentity.TurnSkipIntent, "key:key-k:pressed", "K")]
    [InlineData(ProductIdentity.TurnWaitIntent, "key:key-y:pressed", "Y")]
    public void Each_control_is_bound_to_the_key_the_project_states_for_its_own_intent(string intent, string trigger, string label)
    {
        Assert.Equal("digital", ProductDeclarations.ValueOf(intent));
        Assert.Equal(trigger, ProductDeclarations.TriggerOf(intent));

        // And the panel names that key: what the product reads from the mappings the engine hands it is the key the
        // project bound to this intent, not another control's.
        ControlKeys keys = ProductControlKeys.Read(ProductTestContext.DeclaredInput());
        string? shown = intent switch
        {
            ProductIdentity.PauseToggleIntent => keys.Pause,
            ProductIdentity.SaveIntent => keys.Save,
            ProductIdentity.UseIntent => keys.Use,
            ProductIdentity.AttackIntent => keys.Attack,
            ProductIdentity.TurnBasedToggleIntent => keys.TurnBased,
            ProductIdentity.TurnSkipIntent => keys.TurnSkip,
            ProductIdentity.TurnWaitIntent => keys.TurnWait,
            ProductIdentity.RestIntent => keys.Rest,
            ProductIdentity.CampIntent => keys.Camp,
            ProductIdentity.WaitUntilDawnIntent => keys.WaitDawn,
            ProductIdentity.WaitAnHourIntent => keys.WaitHour,
            ProductIdentity.WaitFiveMinutesIntent => keys.WaitFiveMinutes,
            ProductIdentity.ServiceLeaveIntent => keys.ServiceLeave,
            ProductIdentity.ConversationLeaveIntent => keys.ConversationLeave,
            ProductIdentity.CreationAdvanceIntent => keys.CreationAdvance,
            ProductIdentity.CreationAcceptIntent => keys.CreationAccept,
            _ => null,
        };
        if (shown is not null) Assert.Equal(label, shown);
    }

    [Fact]
    public void A_control_a_screen_also_offers_is_the_kits_own_action_on_the_products_contract()
    {
        // A key and a screen's button ask for exactly the same thing: the intent a key arrives on is the kit's own
        // action name, so the two can never become two different words.
        Assert.Equal(RestActions.Rest, ProductIdentity.RestIntent);
        Assert.Equal(RestActions.Camp, ProductIdentity.CampIntent);
        Assert.Equal(RestActions.WaitUntilDawn, ProductIdentity.WaitUntilDawnIntent);
        Assert.Equal(RestActions.WaitAnHour, ProductIdentity.WaitAnHourIntent);
        Assert.Equal(RestActions.WaitFiveMinutes, ProductIdentity.WaitFiveMinutesIntent);
        Assert.Equal(ServiceActions.Leave, ProductIdentity.ServiceLeaveIntent);
        Assert.Equal(TurnActions.Toggle, ProductIdentity.TurnBasedToggleIntent);
        Assert.Equal(TurnActions.Skip, ProductIdentity.TurnSkipIntent);
        Assert.Equal(TurnActions.Wait, ProductIdentity.TurnWaitIntent);
        Assert.Equal(UseActions.Use, ProductIdentity.UseAction);
        Assert.Equal(SaveActions.Save, ProductIdentity.SaveAction);
        Assert.Equal(CombatActions.Attack, ProductIdentity.AttackAction);
        Assert.Equal(CastActions.Cast, ProductIdentity.CastAction);
        Assert.Equal(CastActions.QuickSpell, ProductIdentity.QuickSpellAction);
        Assert.Equal(AlchemyActions.Mix, ProductIdentity.MixAction);
        Assert.Equal(EquipActions.Equip, ProductIdentity.EquipAction);
        Assert.Equal(EquipActions.Unequip, ProductIdentity.UnequipAction);
        Assert.Equal(SkillRaiseActions.Raise, ProductIdentity.SkillRaiseAction);

        // And every one of those actions is one the companion is held to sending.
        foreach (string action in new[]
        {
            ProductIdentity.UseAction, ProductIdentity.SaveAction, ProductIdentity.AttackAction, ProductIdentity.CastAction,
            ProductIdentity.QuickSpellAction, ProductIdentity.MixAction, ProductIdentity.SkillRaiseAction,
            ProductIdentity.EquipAction, ProductIdentity.UnequipAction,
            ProductIdentity.RestIntent, ProductIdentity.CampIntent, ProductIdentity.WaitUntilDawnIntent,
            ProductIdentity.WaitAnHourIntent, ProductIdentity.WaitFiveMinutesIntent, ProductIdentity.TurnBasedToggleIntent,
            ProductIdentity.TurnSkipIntent, ProductIdentity.TurnWaitIntent, ServiceActions.Buy, ServiceActions.Sell,
            ServiceActions.Identify, ServiceActions.Repair, ServiceActions.Teach, ServiceActions.Leave,
            ServiceActions.Train, ServiceActions.Fare, ServiceActions.Steal, ServiceActions.Repay,
            ServiceActions.Cure, ServiceActions.Provision, ServiceActions.Stay, ServiceActions.Deposit,
            ServiceActions.Withdraw, ServiceActions.Amount,
            ConversationActions.Topic, ConversationActions.Person, ConversationActions.Leave,
        })
        {
            ProjectionContractTests.AssertPanelMayClaim(action);
        }
    }
}
