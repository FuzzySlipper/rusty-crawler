using PartyRpg.Kit.Party;
using PartyRpg.Kit.Persistence;
using Xunit;

namespace PartyRpg.Kit.Tests;

public sealed class FollowersTests
{
    [Fact]
    public void Hired_places_and_story_presence_share_one_party_and_dismissal_frees_only_the_occupied_place()
    {
        using PartyEntity original = TestParty.OfFour();
        PartyEntityFactory factory = new(hiredLimit: 2);
        using PartyEntity party = factory.Restore(original.Capture());
        Assert.True(party.Actor.Has<PartyFollowers>());
        Assert.Null(party.Followers.Join(new("first"), FollowerKind.Hired));
        Assert.Null(party.Followers.Join(new("second"), FollowerKind.Hired));
        Assert.Null(party.Followers.Join(new("story"), FollowerKind.Story));
        PartySave saved = party.Capture();
        long before = party.Stamp;
        Assert.Equal(FollowerCodes.HiredLimit, party.Followers.Join(new("third"), FollowerKind.Hired)!.Code);
        Assert.Equal(FollowerCodes.AlreadyJoined, party.Followers.Join(new("story"), FollowerKind.Hired)!.Code);
        Assert.Equal(before, party.Stamp);
        Assert.Equal(2, party.Followers.HiredCount);
        Assert.Null(party.Followers.Dismiss(new("first")));
        Assert.True(party.Stamp > before);
        Assert.Null(party.Followers.Join(new("third"), FollowerKind.Hired));
        Assert.Equal(new[] { "second", "story", "third" }, party.Followers.All.Select(follower => follower.Definition.Value));
        Assert.Equal(new[] { "first", "second", "story" }, saved.Followers.Select(follower => follower.Definition.Value));
        Assert.Equal(original.Purse.Coins, party.Purse.Coins);
        Assert.Equal(original.Items.Count, party.Items.Count);
    }

    [Fact]
    public void Capture_and_restore_preserve_presence_order_and_joining_kind_and_report_contradictions_together()
    {
        using PartyEntity party = TestParty.OfFour();
        Assert.Null(party.Followers.Join(new("guide"), FollowerKind.Hired));
        Assert.Null(party.Followers.Join(new("guest"), FollowerKind.Story));
        PartyEntityFactory factory = new(hiredLimit: 2);
        PartySave saved = party.Capture();
        using PartyEntity restored = factory.Restore(saved);
        Assert.Equal(saved.Followers, restored.Followers.All);
        Assert.Null(restored.Followers.Dismiss(new("guide")));
        Assert.Single(restored.Followers.All);
        Assert.Equal(2, party.Followers.All.Count);
        PartySave broken = saved with
        {
            Followers = [new(new("guide"), FollowerKind.Hired), new(new("guide"), FollowerKind.Story), new(default, (FollowerKind)99)],
        };
        IReadOnlyList<SaveProblem> problems = factory.Problems(broken);
        Assert.Contains(problems, problem => problem.Code == SaveCodes.SaveFollowerTwice);
        Assert.Contains(problems, problem => problem.Code == SaveCodes.SaveFollowerInvalid);
        Assert.Throws<ArgumentException>(() => factory.Restore(broken));
        Assert.Equal(2, party.Followers.All.Count);
    }
}
