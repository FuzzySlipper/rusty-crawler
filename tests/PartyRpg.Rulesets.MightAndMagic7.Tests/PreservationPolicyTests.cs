using PartyRpg.Kit.Content;
using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using Rusty.Engine;
using Xunit;
using static PartyRpg.Rulesets.MightAndMagic7.Tests.SpellEffectPolicyTests;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

public sealed class PreservationPolicyTests
{
    [Theory]
    [InlineData(false, 2, 80)]
    [InlineData(false, 3, 80)]
    [InlineData(false, 4, 120)]
    [InlineData(true, 2, 90)]
    public void Canonical_damage_and_saved_expiry_read_the_original_member_protection(bool potion, int mastery, int minutes)
    {
        InMemoryPersistenceService persistence = new();
        var content = Content(extra: [
            ($"{RulesetTestContext.ContentDirectory}/content-packs/world/items.json", """
                {"documentId":"items","definitionKind":"item","entries":[
                  {"id":"231","name":"Preservation","value":300,"equipStat":"Bottle","type":"potion","skillGroup":"Misc","skill":"misc","damageDice":"0","damageModifier":"1"}]}
                """, """{"path":"items.json","documentId":"items","definitionKind":"item"}"""),
            ($"{RulesetTestContext.ContentDirectory}/content-packs/world/potions.json", """
                {"documentId":"potions","definitionKind":"potion","entries":[
                  {"id":"231","name":"Preservation","description":"White Potion","effect":"Preservation","kind":"potion","units":[1,1,0],"tier":2,"mixtures":{"231":"none"}}]}
                """, """{"path":"potions.json","documentId":"potions","definitionKind":"potion"}""")]);
        var (context, ui) = RulesetTestContext.Create(persistence, content);
        using IGameSession session = Casting(context, ui);
        var live = (MightAndMagic7Session)session;
        PartyMember protectedMember = live.Party!.Members[0];
        protectedMember.Skills.SetTier(new SkillId("Spirit"), new SkillTier(mastery));
        long before = live.Owners.Clock!.Elapsed.Milliseconds;
        if (potion)
        {
            ItemInstance bottle = live.Party.CreateItem(new("231"), state: new ItemState(potency: 3));
            Assert.True(live.Party.AcquireItem(bottle).Admitted);
            int points = protectedMember.Resources.SpellPoints.Current;
            session.Update(RulesetTestContext.Update(1, 1, RulesetTestContext.Payload(
                $$"""{"action":"party.cast","member":0,"spell":"{{MightAndMagic7Potions.EffectId(231)}}","target":"","item":{{bottle.Id}}}""")));
            Assert.Null(live.Party.FindItem(bottle.Id));
            Assert.Equal(points, protectedMember.Resources.SpellPoints.Current);
        }
        else Cast(session, ui, 1, "50", string.Empty);
        Assert.Equal("cast", Magic(ui).Field("outcome").AsString());
        Assert.True(protectedMember.Effects.Has(SpellEffectIds.Preservation));
        protectedMember.TakeDamage(1000);
        protectedMember.TakeDamage(1000);
        Assert.True(protectedMember.Conditions.Has(MightAndMagic7Conditions.Unconscious));
        Assert.False(protectedMember.Conditions.Has(MightAndMagic7Conditions.Dead));
        Assert.Equal(1960, protectedMember.Resources.Deficit);
        live.Party.Members[1].TakeDamage(1000);
        Assert.True(live.Party.Members[1].Conditions.Has(MightAndMagic7Conditions.Dead));

        SessionSave saved = MightAndMagic7Ruleset.Instance.Save(session);
        DeadlineSave original = Assert.Single(saved.Clock.Deadlines, d => d.Subject == SpellEffectIds.Preservation.Value);
        Assert.Equal(protectedMember.Id, original.Member);
        Assert.InRange(original.DueElapsedMilliseconds - before, minutes * 60000L, minutes * 60000L + 1000);
        Assert.NotNull(persistence.Payload("sessions", "session"));
        var (againContext, againUi) = RulesetTestContext.Create(persistence, content);
        using IGameSession resumed = MightAndMagic7Ruleset.Instance.CreateSession(
            RulesetTestContext.RulesetContext(againContext, againUi) with
            { Start = SessionStart.Resume, Cast = new CastIntentNames(Declared.UiActionContract) });
        resumed.Start();
        var again = (MightAndMagic7Session)resumed;
        PartyMember held = again.Party!.Members[0];
        Assert.Equal(1960, held.Resources.Deficit);
        SessionSave restored = MightAndMagic7Ruleset.Instance.Save(resumed);
        Assert.Equal(original, Assert.Single(restored.Clock.Deadlines, d => d.Subject == SpellEffectIds.Preservation.Value));
        GameClock clock = again.Owners.Clock!;
        clock.Advance(GameDuration.FromMilliseconds(original.DueElapsedMilliseconds - clock.Elapsed.Milliseconds - 1));
        held.TakeDamage(1);
        Assert.False(held.Conditions.Has(MightAndMagic7Conditions.Dead));
        clock.Advance(GameDuration.FromMilliseconds(1));
        Assert.False(held.Effects.Has(SpellEffectIds.Preservation));
        // Expiry removes protection; it neither heals nor invents a new wound. The next wound applies the ladder.
        Assert.True(held.Conditions.Has(MightAndMagic7Conditions.Unconscious));
        held.TakeDamage(1);
        Assert.True(held.Conditions.Has(MightAndMagic7Conditions.Dead));
        Assert.False(held.Conditions.Has(MightAndMagic7Conditions.Unconscious));
    }

    [Theory]
    [InlineData("Dead")]
    [InlineData("Petrified")]
    [InlineData("Eradicated")]
    public void A_carried_protection_does_not_reverse_an_already_laid_out_condition(string condition)
    {
        var (context, ui) = RulesetTestContext.Create(Content());
        using IGameSession session = Casting(context, ui);
        PartyMember member = ((MightAndMagic7Session)session).Party!.Members[0];
        Cast(session, ui, 1, "50", string.Empty);
        member.Conditions.Apply(new ActiveCondition(new(condition)));
        member.TakeDamage(1000);
        Assert.True(member.Conditions.Has(new(condition)));
        Assert.False(member.Conditions.Has(MightAndMagic7Conditions.Unconscious));
        Assert.Equal(0, member.Resources.HitPoints.Current);
        ((MightAndMagic7Session)session).Owners.Clock!.Advance(GameDuration.FromHours(2));
        Assert.True(member.Conditions.Has(new(condition)));
        Assert.False(member.Effects.Has(SpellEffectIds.Preservation));
    }
}
