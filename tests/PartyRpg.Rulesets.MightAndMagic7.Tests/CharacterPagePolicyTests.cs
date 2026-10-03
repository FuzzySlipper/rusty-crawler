using PartyRpg.Kit.Party;
using PartyRpg.Kit.Sessions;
using PartyRpg.Testing;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Rulesets.MightAndMagic7.Tests;

/// <summary>
/// This game's character page and inventory reading: the sheet reads the fight's own sums, so wearing armour moves
/// the armour class it shows, and every carried item is read with its kind, its damage or armour, and its worth.
/// </summary>
public sealed class CharacterPagePolicyTests
{
    [Fact]
    public void The_sheet_reads_the_fights_own_sums_and_the_pack_reads_each_items_kind_and_numbers()
    {
        (ProductCreateContext context, RecordingUiService ui) = RulesetTestContext.Create(EquipmentPolicyTests.Content());
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            RulesetTestContext.RulesetContext(context, ui, combat: true) with { Equip = new(Declared.UiActionContract) });
        session.Start();
        session.Update(RulesetTestContext.Update(1, 1));

        ProjectedNode member = Character(ui).Field("members").Item(0);
        Assert.Equal("Roderick", member.Field("name").AsString());
        Assert.Equal(["Scores", "Vitals", "Resistances"], Titles(member));
        Assert.Equal(("Might", "17"), Row(member, 0, "Might"));
        Assert.Equal(["Fire", "Air", "Water", "Earth", "Mind", "Body"], Labels(member, 2));
        string before = Row(member, 1, "Armour class").Value;

        // The pack is every item, each read in this game's words.
        ProjectedNode pack = ProjectedNode.Of(ui.Latest().Value).Field("equipment").Field("pack");
        ProjectedNode leather = Find(pack, "66");
        Assert.Equal("Armour", leather.Field("kind").AsString());
        Assert.Equal(["Armour +4", "Uses the leather skill", "Worth 150 gold"], Facts(leather));
        Assert.Equal(["Damage 3d3", "Uses the sword skill", "Worth 50 gold"], Facts(Find(pack, "1")));
        Assert.Equal("Potion", Find(pack, "222").Field("kind").AsString());
        Assert.Equal(0, Find(pack, "222").Field("slots").Length());

        // Wearing the leather moves the armour class the sheet shows, by the fight's own sum.
        PartyEntity party = ((MightAndMagic7Session)session).Party!;
        ItemInstance armour = party.Inventory.Items.First(item => item.Definition.Value == "66");
        session.Update(RulesetTestContext.Update(2, 1,
            RulesetTestContext.Payload($$"""{"action":"party.equip","member":0,"item":"{{armour.Id}}"}""")));
        var combat = (MightAndMagic7Combat)((MightAndMagic7Session)session).Owners.Rules.Combat!.Rule;
        string after = Row(Character(ui).Field("members").Item(0), 1, "Armour class").Value;
        Assert.NotEqual(before, after);
        Assert.Equal(combat.CharacterArmorClass(party.Members[0]).ToString(System.Globalization.CultureInfo.InvariantCulture), after);

        // A broken item says so.
        armour.TakeDamage(1);
        session.Update(RulesetTestContext.Update(3, 1));
        ProjectedNode worn = ProjectedNode.Of(ui.Latest().Value).Field("equipment").Field("members").Item(0).Field("worn").Item(0);
        Assert.Contains("Broken — a smith or a repair service mends it", Facts(worn));
    }

    private static ProjectedNode Character(RecordingUiService ui) => ProjectedNode.Of(ui.Latest().Value).Field("character");

    private static string[] Titles(ProjectedNode member)
    {
        ProjectedNode sections = member.Field("sections");
        return [.. Enumerable.Range(0, sections.Length()).Select(i => sections.Item(i).Field("title").AsString())];
    }

    private static string[] Labels(ProjectedNode member, int section)
    {
        ProjectedNode rows = member.Field("sections").Item(section).Field("rows");
        return [.. Enumerable.Range(0, rows.Length()).Select(i => rows.Item(i).Field("label").AsString())];
    }

    private static (string Label, string Value) Row(ProjectedNode member, int section, string label)
    {
        ProjectedNode rows = member.Field("sections").Item(section).Field("rows");
        ProjectedNode row = Enumerable.Range(0, rows.Length()).Select(rows.Item).First(entry => entry.Field("label").AsString() == label);
        return (label, row.Field("value").AsString());
    }

    private static ProjectedNode Find(ProjectedNode pack, string definition) =>
        Enumerable.Range(0, pack.Length()).Select(pack.Item).First(row => row.Field("definition").AsString() == definition);

    private static string[] Facts(ProjectedNode row)
    {
        ProjectedNode facts = row.Field("facts");
        return [.. Enumerable.Range(0, facts.Length()).Select(i => facts.Item(i).AsString())];
    }
}
