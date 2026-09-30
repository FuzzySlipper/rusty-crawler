using System.Globalization;
using System.Text;
using System.Text.Json;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Conversation;
using PartyRpg.Kit.Input;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Progression;
using PartyRpg.Kit.Promotion;
using PartyRpg.Kit.Quests;
using PartyRpg.Kit.Services;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using PartyRpg.Rulesets.MightAndMagic7;
using Rusty.Engine;
using Xunit;

namespace PartyRpg.Host.Tests;

/// <summary>
/// What this game makes of a party's standing: the bands and their edges, what a deed does to the world's
/// opinion, what a person says and what a hall posts at each band, what a counter charges, and what the
/// party's accomplishments read as.
/// </summary>
/// <remarks>
/// <para>
/// This suite proves the readings the kit cannot: the donor's own band words at the donor's own edges, the
/// one mover this game states and the mover the donor states but this build has no owner for, the standing
/// line composed over the real conversation policy, the price the real service policy quotes, and the award
/// families read out of the ladder, the quest definitions, the counters, and the party's own records.
/// </para>
/// <para>
/// The shipped-data cases read the operator's own imported packs where they are staged and return when they
/// are not, in the style the quest policy's own shipped case uses: a machine without the operator's data
/// says so rather than failing, and what the packs really gate on a standing is the count those cases make.
/// </para>
/// </remarks>
public sealed class StandingPolicyTests
{
    private static readonly ContentLayout Layout = new("packs", "imports", "bundles");
    private static readonly PlaceId HallPlace = new("1");
    private static readonly UseIntentNames UseControls = new(
        ProductIdentity.UseIntent,
        ProductIdentity.UiActionContract);
    private static readonly ConversationIntentNames ConversationControls = new(
        ProductIdentity.ConversationLeaveIntent,
        ProductIdentity.UiActionContract);

    [Fact]
    public void The_bands_are_the_donor_s_own_words_at_the_donor_s_own_edges()
    {
        // The five words are the donor's own (OpenEnroth src/GUI/UI/UIGame.cpp:1645-1654,
        // GetReputationString), and its four edges are read back into this game's sign convention, in which a
        // higher reputation is a better one. The donor's own convention is the other way round — its
        // GetPartyReputation negates a location's reputation (src/Engine/LocationInfo.h:7) — so what is kept
        // is the word and the distance from the middle, not the sign.
        Assert.Equal(
            ["Liked", "Friendly", "Neutral", "Unfriendly", "Hated"],
            MightAndMagic7Standing.Bands.Select(band => band.Word));
        Assert.Equal(
            [25, 6, -5, -24, int.MinValue],
            MightAndMagic7Standing.Bands.Select(band => band.Floor));

        // Each edge is where the band begins, and a party at the bottom of the scale is hated rather than in
        // a band nothing names: reputation is not clamped, so the lowest band has no floor of its own.
        Assert.Equal("Liked", MightAndMagic7Standing.BandOf(25).Word);
        Assert.Equal("Friendly", MightAndMagic7Standing.BandOf(24).Word);
        Assert.Equal("Friendly", MightAndMagic7Standing.BandOf(6).Word);
        Assert.Equal("Neutral", MightAndMagic7Standing.BandOf(5).Word);
        Assert.Equal("Neutral", MightAndMagic7Standing.BandOf(-5).Word);
        Assert.Equal("Unfriendly", MightAndMagic7Standing.BandOf(-6).Word);
        Assert.Equal("Unfriendly", MightAndMagic7Standing.BandOf(-24).Word);
        Assert.Equal("Hated", MightAndMagic7Standing.BandOf(-25).Word);
        Assert.Equal("Hated", MightAndMagic7Standing.BandOf(int.MinValue).Word);

        // The standing a person will speak at is the "Friendly" band's own floor read out of that table
        // rather than a second number, so moving where the band begins moves what the band does.
        Assert.Equal(6, MightAndMagic7Standing.WellRegarded);
        Assert.Equal(
            MightAndMagic7Standing.Bands.Single(band => band.Word == "Friendly").Floor,
            MightAndMagic7Standing.WellRegarded);

        // The reading a panel shows is the party's own band in the game's words, and the line a person speaks
        // is composed around the same two facts rather than around a second table.
        using PartyEntity party = PartyOf(reputation: 6);
        MightAndMagic7Standing standing = new(promotions: null, quests: null, services: null);
        Assert.Equal("Friendly", standing.Read(party).Band);
        Assert.Contains("people speak well of the party", standing.Read(party).Reading, StringComparison.Ordinal);
        Assert.StartsWith("'Friendly is the word:", MightAndMagic7Standing.Words(party), StringComparison.Ordinal);
        Assert.Empty(standing.Awards(party));
    }

    [Fact]
    public void A_finished_errand_moves_the_world_s_opinion_and_a_kill_moves_nothing()
    {
        using PartyEntity party = PartyOf(reputation: 0);
        PartyProgression progression = new(MightAndMagic7Progression.Instance, party);

        // The one mover this game states, through the one owner that writes either number: an errand the
        // town asked for and saw finished is worth one point per thousand experience it paid, which is the
        // donor's own figure for what word of a deed is worth (src/Engine/Party.cpp:371-379, Party::fame).
        ProgressionAwardResult errand = progression.Award(new PartyExperienceAward(PartyQuests.QuestSource, (long)MightAndMagic7Tuning.ErrandExperience.Default));
        Assert.True(errand.IsAwarded);
        Assert.Equal(4, errand.Standing.Reputation);
        Assert.Equal(4, party.Reputation.Reputation);

        // Fame moves on the same deed, and on everything else the party earns, because fame is what the
        // party's whole experience is worth at a thousand to the point — the donor's own figure.
        Assert.Equal(4, party.Reputation.Fame);

        // A creature brought down is an award and not news: the donor pays experience for it and changes no
        // reputation at all (src/Engine/Objects/Actor.cpp:3164-3167), and neither does this game.
        ProgressionAwardResult kill = progression.Award(new PartyExperienceAward("kill", 5000));
        Assert.True(kill.IsAwarded);
        Assert.Equal(0, kill.Standing.Reputation);
        Assert.Equal(4, party.Reputation.Reputation);
        Assert.Equal(5, kill.Standing.Fame);
        Assert.Equal(9, party.Reputation.Fame);

        // An errand worth less than a thousand still counts as one point, because a deed worth little is
        // still a deed — the unit the donor's own temple uses for a donation.
        ProgressionAwardResult small = progression.Award(new PartyExperienceAward(PartyQuests.QuestSource, 400));
        Assert.Equal(1, small.Standing.Reputation);
        Assert.Equal(5, party.Reputation.Reputation);

        // The whole chain, not only the arithmetic: two errands' worth of standing puts a party in the band
        // its own table names, which is what a person's line and a hall's notice are read from.
        Assert.Equal("Neutral", MightAndMagic7Standing.BandOf(party.Reputation.Reputation).Word);
        progression.Award(new PartyExperienceAward(PartyQuests.QuestSource, 4000));
        Assert.Equal(9, party.Reputation.Reputation);
        Assert.Equal("Friendly", MightAndMagic7Standing.BandOf(party.Reputation.Reputation).Word);
    }

    [Fact]
    public void A_person_says_what_the_town_thinks_once_the_party_is_worth_an_opinion()
    {
        using Fixture fixture = Fixture.Build();
        PlacementDefinition person = fixture.Placement("person-0");

        // A fresh party is offered the shipped topics and nothing about its own standing: the line waits for
        // the standing this game's own table names, and it is withheld with the number it wants rather than
        // hidden, so a player can read what would open it.
        Assert.Contains("topic-1", fixture.OnOffer(person));
        ConversationOffer withheld = fixture.Offer(person, MightAndMagic7Conversation.StandingTopicId);
        Assert.False(withheld.IsOnOffer);
        Assert.Equal(ConversationConditionKind.Reputation, Assert.Single(withheld.Topic.Conditions).Kind);
        Assert.Equal(MightAndMagic7Standing.WellRegarded, withheld.Topic.Conditions[0].Amount);
        Assert.Contains("standing is 0", withheld.Availability.Reason, StringComparison.Ordinal);
        Assert.Contains("needs 6", withheld.Availability.Reason, StringComparison.Ordinal);

        // The threshold is crossed the only way it may be — a deed through the progression owner — and the
        // line appears in the same conversation without anything having been invalidated: availability is
        // recomputed from the party on every read.
        PartyProgression progression = new(MightAndMagic7Progression.Instance, fixture.Party);
        progression.Award(new PartyExperienceAward(PartyQuests.QuestSource, (long)MightAndMagic7Tuning.ErrandExperience.Default));
        Assert.Equal(4, fixture.Party.Reputation.Reputation);
        Assert.False(fixture.Offer(person, MightAndMagic7Conversation.StandingTopicId).IsOnOffer);

        progression.Award(new PartyExperienceAward(PartyQuests.QuestSource, (long)MightAndMagic7Tuning.ErrandExperience.Default));
        Assert.Equal("Friendly", MightAndMagic7Standing.BandOf(fixture.Party.Reputation.Reputation).Word);
        ConversationOffer offered = fixture.Offer(person, MightAndMagic7Conversation.StandingTopicId);
        Assert.True(offered.IsOnOffer);
        Assert.Equal("What do people say about us?", offered.Label);

        // What the person says is the band's own reading in this game's words, and saying it is recorded on
        // the party like any other line.
        ConversationAnswer answer = fixture.Take(offered.Topic, person, "np-2");
        Assert.StartsWith("'Friendly is the word:", answer.Text, StringComparison.Ordinal);
        Assert.Contains("people speak well of the party", answer.Text, StringComparison.Ordinal);
        Assert.Contains("hall will put its notices its way", answer.Text, StringComparison.Ordinal);
        Assert.Equal(["heard:" + MightAndMagic7Conversation.StandingTopicId], answer.Records);

        // A party the town thinks better of hears a different word from the same line: the band is read from
        // the party every time, and no second line is kept for it.
        fixture.Party.Reputation.ChangeReputation(20);
        Assert.Equal("Liked", MightAndMagic7Standing.BandOf(fixture.Party.Reputation.Reputation).Word);
        Assert.Contains("Liked is the word", MightAndMagic7Standing.Words(fixture.Party), StringComparison.Ordinal);
    }

    [Fact]
    public void What_the_town_thinks_changes_what_a_counter_charges_and_never_whether_it_serves()
    {
        using Fixture fixture = Fixture.Build();
        ServiceDefinition shop = fixture.Shop();
        // A room rather than a meal, because a full larder is the tavern's own refusal and this case is
        // about a refusal standing is supposed to cause and does not.
        ServiceSubject subject = ServiceSubject.OfOffer(new ServiceOffer(ServiceOfferKind.Stay, "A room", Value: 100));
        PartyMemberId member = fixture.Party.Members[0].Id;

        // The donor's own merchant rule, read in this game's sign convention: what the party's standing earns
        // it is a discount percent, so a party the town likes pays less and one it dislikes pays above the
        // shelf (src/Engine/PriceCalculator.cpp:139-158, playerMerchant and applyMerchantDiscount).
        Assert.Equal(0, MightAndMagic7Services.MerchantValue(fixture.Party));
        ServiceQuote plain = fixture.Services.Quote(new ServiceQuoteRequest(shop, ServiceOperationKind.Stay, subject, member, fixture.Party, fixture.Clock));
        Assert.Equal(100, plain.Charge.Coins);

        fixture.Party.Reputation.ChangeReputation(30);
        Assert.Equal(30, MightAndMagic7Services.MerchantValue(fixture.Party));
        ServiceQuote liked = fixture.Services.Quote(new ServiceQuoteRequest(shop, ServiceOperationKind.Stay, subject, member, fixture.Party, fixture.Clock));
        Assert.Equal(70, liked.Charge.Coins);

        fixture.Party.Reputation.ChangeReputation(-60);
        Assert.Equal(-30, fixture.Party.Reputation.Reputation);
        Assert.Equal("Hated", MightAndMagic7Standing.BandOf(fixture.Party.Reputation.Reputation).Word);
        Assert.Equal(-30, MightAndMagic7Services.MerchantValue(fixture.Party));
        ServiceQuote hated = fixture.Services.Quote(new ServiceQuoteRequest(shop, ServiceOperationKind.Stay, subject, member, fixture.Party, fixture.Clock));
        Assert.Equal(130, hated.Charge.Coins);

        // The negative this game states rather than inventing: a counter does not refuse a party for its
        // standing. The donor's own counters gate on a membership, an hour, and a rung, and the only
        // reputation it reads at a counter is the price (src/GUI/UI/Houses/Shops.cpp:1105-1107, where it is
        // read for a theft's fine); this game keeps that and adds nothing, so the worst-regarded party in the
        // world is still served and pays the number its standing earns.
        ServiceEligibility verdict = fixture.Services.Judge(
            new ServiceEligibilityRequest(shop, ServiceOperationKind.Stay, subject, member, fixture.Party, fixture.Clock));
        Assert.True(verdict.IsAllowed, verdict.Refusal?.Message);

        // The counter's own answer says what it recognises the party by, and for this one that is nothing at
        // all: the fixture's guild sells a membership rather than requiring one, so the party is served on
        // the shelf's terms however the town regards it.
        Assert.Empty(fixture.Services.Access(new ServiceAccessRequest(shop, fixture.Party)));
    }

    [Fact]
    public void A_membership_the_party_bought_is_an_accomplishment_in_the_counters_own_words()
    {
        using Fixture fixture = Fixture.Build();
        MightAndMagic7Standing standing = new(promotions: null, quests: null, services: fixture.Services);

        // A membership is the third family the donor keeps as an award (src/Engine/Data/AwardEnums.h:50-60,
        // AWARD_MEMBERSHIP_*) and it is party-carried state here, so what the party bought is what the
        // reading names — in the words of the lesson that sold it, not in a second spelling of the effect.
        // The guild is the counter that sells it: the tavern beside it sells meals to anybody.
        Assert.Contains(("guild.fire", "Fire Guild membership"), fixture.Services.Memberships);
        Assert.Equal("guild.fire", fixture.Guild().Membership);
        Assert.Empty(standing.Awards(fixture.Party));

        fixture.Party.Effects.Apply(new PartyEffect(new EffectId("guild.fire"), 1));
        AwardReading award = Assert.Single(standing.Awards(fixture.Party));
        Assert.Equal("guild.fire", award.Id);
        Assert.Equal(MightAndMagic7Standing.MembershipKind, award.Kind);
        Assert.Equal("Fire Guild membership", award.Label);

        // A record that is none of this game's award families is not an accomplishment: a ward still running
        // and a passage bought are state, not something the party did.
        fixture.Party.Effects.Apply(new PartyEffect(new EffectId("ward:magic"), 1));
        fixture.Party.Effects.Apply(new PartyEffect(new EffectId("passage:somewhere"), 1));
        Assert.Single(standing.Awards(fixture.Party));
    }

    [ImportedFact("people.json")]
    public void The_shipped_data_gates_nothing_on_a_standing_and_the_standing_lines_are_this_game_s_own()
    {
        ContentCatalog catalog = ImportedContent.Load();
        MightAndMagic7Promotions promotions = MightAndMagic7Promotions.Read(catalog);
        MightAndMagic7Quests quests = MightAndMagic7Quests.Read(catalog, promotions)!;
        MightAndMagic7Services services = MightAndMagic7Services.Read(catalog, quests: quests)!;
        MightAndMagic7Conversation conversation = MightAndMagic7Conversation.Read(catalog, services, promotions, quests)!;

        // What the operator's own topic table gates on, counted rather than asserted: how many people carry
        // topics at all, how many topics there are, and how many conditions of each kind the shipped rows
        // state. The answer this case exists to make is that no shipped line waits for a standing: the
        // requirement column the table does carry is a quest bit, read as an errand, and the rows that name
        // one the import can neither place nor give text (so they are not in the pack at all).
        int peopleWithTopics = 0;
        int topics = 0;
        Dictionary<string, int> conditions = [];
        foreach ((_, _, ContentEntry entry) in catalog.Entries(MightAndMagic7Conversation.PersonDefinitionKind))
        {
            JsonElement[] stated = [.. entry.GetArray("topics")];
            if (stated.Length > 0) peopleWithTopics++;
            foreach (JsonElement topic in stated)
            {
                topics++;
                if (topic.ValueKind != JsonValueKind.Object || !topic.TryGetProperty("conditions", out JsonElement list)) continue;
                foreach (JsonElement condition in list.EnumerateArray())
                {
                    string kind = ContentEntry.ReadString(condition, "kind");
                    conditions[kind] = conditions.GetValueOrDefault(kind) + 1;
                }
            }
        }

        Assert.True(peopleWithTopics > 0, "the operator's people carry topics, so the shipped topic table is readable here");
        Assert.True(topics > 0);
        Assert.DoesNotContain(ConversationConditionKind.Reputation.ToString().ToLowerInvariant(), conditions.Keys);
        Assert.DoesNotContain(
            "reputation",
            conditions.Keys,
            StringComparer.OrdinalIgnoreCase);

        // The shipped quest content is the ladder's own errands, and none of them states an offer condition:
        // who gives them and what they ask for is the shipped table's business, and a standing is not part of
        // it. The one errand this game does gate on a standing is the town hall's notice, which nobody
        // authors — and it is gated in the vocabulary the conversation and the quests share.
        Assert.Equal(17, quests.ErrandCount);
        foreach (QuestDefinition definition in quests.Definitions)
        {
            if (definition.Id.Value.StartsWith(MightAndMagic7Identities.BountyPrefix, StringComparison.Ordinal)) continue;
            Assert.Empty(definition.OfferConditions);
        }

        string hall = string.Empty;
        foreach ((_, _, ContentEntry place) in catalog.Entries(PlaceGraphLoader.PlaceDefinitionKind))
        {
            foreach (JsonElement placement in place.GetArray("placements"))
            {
                if (!string.Equals(ContentEntry.ReadString(placement, "fixture"), "Town Hall", StringComparison.Ordinal)) continue;
                hall = ContentEntry.ReadId(placement, "id");
            }
        }

        Assert.NotEqual(string.Empty, hall);
        string posted = quests.BountyQuest(hall, new GameDate(1168, 3, 1));
        Assert.StartsWith(MightAndMagic7Identities.BountyPrefix, posted, StringComparison.Ordinal);
        QuestDefinition bounty = quests.Definition(new QuestId(posted))!;
        ConversationCondition gate = Assert.Single(bounty.OfferConditions);
        Assert.Equal(ConversationConditionKind.Reputation, gate.Kind);
        Assert.Equal(MightAndMagic7Standing.WellRegarded, gate.Amount);

        // A shipped person, named by the table itself, is where the standing line really bites: the topics
        // the shipped rows state are offered whatever the standing is, and the line this game composes for
        // every person is the one that appears and disappears with it.
        (PlaceId where, PlacementDefinition person) = ShippedPerson(catalog, conversation, "npc-1");
        ConversationSubject subject = conversation.Describe(new ConversationTargetRequest(where, person))!;
        using PartyEntity party = PartyOf(reputation: 0);
        GameClock clock = Clock();
        ConversationOffer At(int reputation)
        {
            party.Reputation.ChangeReputation(reputation - party.Reputation.Reputation);
            return conversation.Offers(new ConversationContext(where, person, subject, subject.First.Id, [], party, clock))
                .Single(offer => offer.Id == MightAndMagic7Conversation.StandingTopicId);
        }

        Assert.False(At(0).IsOnOffer);
        Assert.Contains("needs 6", At(0).Availability.Reason, StringComparison.Ordinal);
        Assert.False(At(5).IsOnOffer);
        Assert.True(At(6).IsOnOffer);
        Assert.True(At(25).IsOnOffer);
    }

    [ImportedFact("quests.json")]
    public void The_awards_of_the_shipped_game_read_in_the_ladder_s_and_the_errands_own_words()
    {
        ContentCatalog catalog = ImportedContent.Load();
        MightAndMagic7Promotions promotions = MightAndMagic7Promotions.Read(catalog);
        MightAndMagic7Quests read = MightAndMagic7Quests.Read(catalog, promotions)!;
        MightAndMagic7Services services = MightAndMagic7Services.Read(catalog, quests: read)!;
        MightAndMagic7Standing standing = new(promotions, read, services);
        using PartyEntity party = PartyOf(reputation: 0);

        // A rank the party holds reads as the class the rank names, with the rung it was taken from beside
        // it: the ladder wrote both, so the name cannot drift from the promotion that gave it.
        PromotionRank rank = promotions.Ladder.Ranks.First(candidate => candidate.Rank == 2);
        party.Effects.Apply(new PartyEffect(new EffectId(rank.Award), 1));
        AwardReading promoted = Assert.Single(standing.Awards(party));
        Assert.Equal(rank.Award, promoted.Id);
        Assert.Equal(MightAndMagic7Standing.PromotionKind, promoted.Kind);
        Assert.Equal(rank.To.Value, promoted.Label);
        Assert.Contains(rank.From.Value, promoted.Detail, StringComparison.Ordinal);

        // An errand the shipped table states reads as the words this game gives it, and its giver's own name
        // comes from the ladder's requirement rather than from a second table.
        QuestDefinition errand = read.Definitions.First(definition => definition.Record.StartsWith("errand:", StringComparison.Ordinal));
        party.Effects.Apply(new PartyEffect(new EffectId(errand.Record), 1));
        AwardReading finished = standing.Awards(party).Single(award => award.Id == errand.Record);
        Assert.Equal(MightAndMagic7Standing.ErrandKind, finished.Kind);
        Assert.Equal(errand.Name, finished.Label);
        Assert.StartsWith("given by ", finished.Detail, StringComparison.Ordinal);

        // A counted deed the ladder keeps as a record reads in the ladder's own words for it, with how much
        // of it the party holds: five arena victories are five, not a second number this game would keep.
        PromotionRequirement deed = promotions.Ladder.Ranks
            .SelectMany(candidate => candidate.Requirements)
            .First(requirement => requirement.Kind == PromotionRequirementKind.Award
                && requirement.Name.StartsWith(MightAndMagic7Identities.DeedPrefix, StringComparison.Ordinal));
        party.Effects.Apply(new PartyEffect(new EffectId(deed.Name), deed.Amount));
        AwardReading counted = standing.Awards(party).Single(award => award.Id == deed.Name);
        Assert.Equal(MightAndMagic7Standing.DeedKind, counted.Kind);
        Assert.Equal(deed.Label, counted.Label);
        Assert.Contains(
            deed.Amount.ToString(CultureInfo.InvariantCulture),
            counted.Detail,
            StringComparison.Ordinal);

        // The operator's own guilds sell memberships too, and every one of them reads in words rather than
        // as the effect a save spells: the imported table writes no membership column at all, so the effect
        // and its name are this game's answers about the kind — "Fire Guild membership" for the school the
        // guild keeps — and the label is never the identity the party carries it under.
        Assert.NotEmpty(services.Memberships);
        Assert.All(services.Memberships, membership => Assert.NotEqual(membership.Effect, membership.Label));
        Assert.Contains(("guild.fire", "Fire Guild membership"), services.Memberships);
    }

    [Fact]
    public void A_party_that_creates_its_characters_can_hear_take_and_finish_an_errand()
    {
        // The creation path is the one a shipped host composes: the player makes a party and the world is
        // built over it. It must hold the errand owner exactly as the scenario path does, because the
        // conversation composes an errand's offer from this game's quests on both paths — a session that
        // offered an errand it held no owner for would leave a player with a line nothing could act on.
        InMemoryPersistenceService persistence = new();
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(
            persistence,
            // The bundle names the creation tables because this case creates its party: the selection is what
            // loads, so the pack that declares the classes and skills creation offers has to be in it.
            [ProductTestContext.Bundle(BuiltInBundles.Default, "world", "creation-tables"), .. ErrandContent()[1..], .. ProductTestContext.CreationTables()]);
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            ProductTestContext.RulesetContext(context, ui, creation: true) with
            {
                Use = UseControls,
                Conversation = ConversationControls,
            });
        session.Start();

        // The accept control finishes the new game, and the created party stands in the place the pack starts
        // it in with the errand's owner in front of it.
        session.Update(ProductTestContext.Update(1, 1, ProductTestContext.Digital(ProductIdentity.CreationAcceptIntent)));
        Assert.Equal(SessionMode.Running, session.Mode);
        Assert.True(ProjectedNode.Of(ui.Latest().Value).Field("quests").Field("available").AsBoolean());

        session.Update(ProductTestContext.Update(2, 1, ProductTestContext.Digital(ProductIdentity.UseIntent)));
        session.Update(ProductTestContext.Update(3, 1, ProductTestContext.ChooseTopic("quest:relic")));
        ProjectedNode offered = ProjectedNode.Of(ui.Latest().Value).Field("quests");
        Assert.Equal("offer", offered.Field("action").AsString());
        Assert.Equal("applied", offered.Field("outcome").AsString());
        Assert.Equal("offered", offered.Field("journal").Item(0).Field("state").AsString());

        // And the rest of the errand's own three moments route to the same owner on this path: agreed to,
        // then handed back.
        session.Update(ProductTestContext.Update(4, 1, ProductTestContext.ChooseTopic("accept:relic")));
        Assert.Equal("accepted", ProjectedNode.Of(ui.Latest().Value).Field("quests").Field("journal").Item(0).Field("state").AsString());
        ((MightAndMagic7Session)session).World!.ArriveAt(new PlaceId("1"), PlacePose.Origin);
        session.Update(ProductTestContext.Update(5, 1));
        ((MightAndMagic7Session)session).World!.ArriveAt(new PlaceId("2"), PlacePose.Origin);
        session.Update(ProductTestContext.Update(6, 1, ProductTestContext.ChooseTopic("turn-in:relic")));
        ProjectedNode finished = ProjectedNode.Of(ui.Latest().Value).Field("quests");
        Assert.Equal("applied", finished.Field("outcome").AsString());
        Assert.Equal("turned-in", finished.Field("journal").Item(0).Field("state").AsString());
    }

    [Fact]
    public void An_award_a_turn_in_sets_rides_the_save_with_the_standing_it_moved()
    {
        // The whole chain through the product: an errand a pack states, offered by the person it names,
        // walked and handed back — and then saved and resumed, so what survives is the state a real turn-in
        // left rather than state a test set.
        InMemoryPersistenceService persistence = new();
        (ProductCreateContext context, RecordingUiService ui) = ProductTestContext.Create(persistence, ErrandContent());
        using IGameSession session = MightAndMagic7Ruleset.Instance.CreateSession(
            ProductTestContext.RulesetContext(context, ui) with
            {
                Use = UseControls,
                Conversation = ConversationControls,
            });
        session.Start();

        session.Update(ProductTestContext.Update(1, 1));
        Assert.Equal("Erathia", ProjectedNode.Of(ui.Latest().Value).Field("world").Field("name").AsString());

        // A fresh party is worth no opinion yet, so the line about its standing is withheld and the number it
        // wants is the band this game's table opens at.
        ProjectedNode fresh = ProjectedNode.Of(ui.Latest().Value).Field("party");
        Assert.True(fresh.Field("standingRead").AsBoolean());
        Assert.Equal("Neutral", fresh.Field("standing").AsString());
        Assert.Equal(0, fresh.Field("reputation").AsNumber());
        Assert.Equal(0, fresh.Field("awards").Length());

        // The giver is spoken with and the errand taken, and the pack's own words are what the offer says.
        session.Update(ProductTestContext.Update(2, 1, ProductTestContext.Digital(ProductIdentity.UseIntent)));
        ProjectedNode talking = ProjectedNode.Of(ui.Latest().Value).Field("conversation");
        Assert.True(talking.Field("open").AsBoolean());
        Assert.Equal("Frederick Org", talking.Field("speaker").AsString());
        Assert.Contains(
            "quest:relic",
            Enumerable.Range(0, talking.Field("topics").Length()).Select(position => talking.Field("topics").Item(position).Field("id").AsString()));
        session.Update(ProductTestContext.Update(3, 1, ProductTestContext.ChooseTopic("quest:relic")));
        ProjectedNode heard = ProjectedNode.Of(ui.Latest().Value).Field("quests");
        Assert.Equal("offered", heard.Field("journal").Item(0).Field("state").AsString());
        session.Update(ProductTestContext.Update(4, 1, ProductTestContext.ChooseTopic("accept:relic")));
        ProjectedNode taken = ProjectedNode.Of(ui.Latest().Value).Field("quests");
        Assert.Equal("accepted", taken.Field("journal").Item(0).Field("state").AsString());

        // The errand asks for a place, so the party is put where it asks and the world's own report meets it.
        ((MightAndMagic7Session)session).World!.ArriveAt(new PlaceId("1"), PlacePose.Origin);
        session.Update(ProductTestContext.Update(5, 1));
        Assert.Equal("completed", ProjectedNode.Of(ui.Latest().Value).Field("quests").Field("journal").Item(0).Field("state").AsString());

        // Handing it back to the giver pays it: the record the errand leaves is on the party, the world's
        // opinion moved by what the errand was worth, and fame moved by the same deed.
        ((MightAndMagic7Session)session).World!.ArriveAt(new PlaceId("2"), PlacePose.Origin);
        session.Update(ProductTestContext.Update(6, 1, ProductTestContext.ChooseTopic("turn-in:relic")));
        ProjectedNode paid = ProjectedNode.Of(ui.Latest().Value).Field("party");
        Assert.Equal("turned-in", ProjectedNode.Of(ui.Latest().Value).Field("quests").Field("journal").Item(0).Field("state").AsString());
        Assert.Equal(6, paid.Field("reputation").AsNumber());
        Assert.Equal(6, paid.Field("fame").AsNumber());
        Assert.Equal("Friendly", paid.Field("standing").AsString());
        Assert.Contains("people speak well of the party", paid.Field("standingDetail").AsString(), StringComparison.Ordinal);
        Assert.Equal(1, paid.Field("awards").Length());
        Assert.Equal("errand:relic", paid.Field("awards").Item(0).Field("id").AsString());
        Assert.Equal(MightAndMagic7Standing.ErrandKind, paid.Field("awards").Item(0).Field("kind").AsString());
        Assert.Equal("The Elven relic", paid.Field("awards").Item(0).Field("label").AsString());

        // The product saves, and the bytes really are in the engine's store: the standing and the record are
        // in the document rather than only in the running session.
        SessionSave written = MightAndMagic7Ruleset.Instance.Save(session);
        byte[]? payload = persistence.Payload(MightAndMagic7Persistence.StoreScope, MightAndMagic7Persistence.SaveSlot);
        Assert.NotNull(payload);
        Assert.Contains("errand:relic", Encoding.UTF8.GetString(payload), StringComparison.Ordinal);
        Assert.Equal(6, written.Party.Reputation);
        Assert.Equal(6, written.Party.Fame);
        Assert.Contains(written.Party.Effects, effect => effect.Effect.Value == "errand:relic");

        // A resumed product reads the same band, the same numbers, and the same accomplishment: a load is
        // not a demotion and the deed is not forgotten.
        (ProductCreateContext resumedContext, RecordingUiService resumedUi) = ProductTestContext.Create(persistence, ErrandContent());
        using IGameSession resumed = MightAndMagic7Ruleset.Instance.ResumeSession(ProductTestContext.RulesetContext(resumedContext, resumedUi));
        ProjectedNode after = ProjectedNode.Of(resumedUi.Latest().Value).Field("party");
        Assert.Equal("Friendly", after.Field("standing").AsString());
        Assert.Equal(6, after.Field("reputation").AsNumber());
        Assert.Equal(6, after.Field("fame").AsNumber());
        Assert.Equal(1, after.Field("awards").Length());
        Assert.Equal("errand:relic", after.Field("awards").Item(0).Field("id").AsString());
        Assert.Equal("The Elven relic", after.Field("awards").Item(0).Field("label").AsString());
    }

    /// <summary>The content the save case plays: an errand a pack states, its giver, and where it is walked.</summary>
    private static (string Path, string Text)[] ErrandContent() =>
    [
        ProductTestContext.Bundle(BuiltInBundles.Default, "world"),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/pack.json",
            """
            {
              "schemaVersion": 1,
              "packId": "world",
              "kind": "definitions",
              "provenance": { "description": "authored for a test" },
              "documents": [
                { "path": "places.json", "documentId": "places", "definitionKind": "place" },
                { "path": "people.json", "documentId": "people", "definitionKind": "person",
                  "references": [ "person:npc-43" ] },
                { "path": "quests.json", "documentId": "quests", "definitionKind": "quest" },
                { "path": "start.json", "documentId": "start", "definitionKind": "scenario-start" },
                { "path": "party.json", "documentId": "party", "definitionKind": "scenario-party" }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/places.json",
            """
            {
              "documentId": "places",
              "definitionKind": "place",
              "entries": [
                { "id": "2", "kind": "region", "name": "Erathia", "respawnDays": 7,
                  "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
                  "placements": [
                    { "id": "residence-304", "kind": "residence", "name": "Org House", "proprietor": "Placeholder",
                      "fixture": "House", "x": 100, "y": 0, "z": 0, "people": [ "npc-43" ] } ] },
                { "id": "1", "kind": "interior", "name": "Castle Navan", "respawnDays": 7,
                  "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ] }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/people.json",
            """
            {
              "documentId": "people",
              "definitionKind": "person",
              "entries": [
                { "id": "npc-43", "npcId": 43, "name": "Frederick Org", "portrait": "700",
                  "greeting": "'Well met, travellers.'", "greetingAgain": "'You again.'", "dialogueEvents": 1,
                  "topics": [ { "id": "topic-1", "label": "The castle", "text": "'This is my castle.'", "textCount": 1 } ] }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/quests.json",
            """
            {
              "documentId": "quests",
              "definitionKind": "quest",
              "entries": [
                { "id": "relic", "name": "The Elven relic", "note": "Walk to Castle Navan and hand the word back.",
                  "reading": {
                    "giver": "npc-43",
                    "objectives": [ { "id": "reach", "kind": "reach", "target": "Castle Navan", "label": "Reach Castle Navan" } ],
                    "experience": 6000, "coins": 100,
                    "record": "errand:relic",
                    "residue": "the relic itself is the castle's own event program, which this build does not run" } }
              ]
            }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/start.json",
            """
            { "documentId": "start", "definitionKind": "scenario-start", "entries": [ { "id": "start", "place": "2", "entryPoint": "Party Start" } ] }
            """),
        ($"{ProductTestContext.ContentDirectory}/content-packs/world/party.json",
            """
            {
              "documentId": "party",
              "definitionKind": "scenario-party",
              "entries": [
                { "id": "party", "coins": 0, "food": 6, "reputation": 0, "fame": 0,
                  "members": [
                    { "name": "Roderick", "race": "Human", "class": "Knight", "level": 1, "classRank": 1,
                      "hitPoints": 40, "spellPoints": 0,
                      "attributes": [ { "id": "Might", "value": 13 } ],
                      "skills": [], "spells": [], "conditions": [] } ] }
              ]
            }
            """),
    ];

    /// <summary>The first placement the packs give a person, so a shipped person can be spoken with.</summary>
    private static (PlaceId Place, PlacementDefinition Person) ShippedPerson(
        ContentCatalog catalog,
        MightAndMagic7Conversation conversation,
        string who)
    {
        PlacePopulationContent population = PlacePopulationContent.Read(PlaceGraphLoader.Load(catalog));
        foreach ((_, _, ContentEntry place) in catalog.Entries(PlaceGraphLoader.PlaceDefinitionKind))
        {
            PlaceId id = new(place.Id);
            foreach (PlacementDefinition placement in population.PlacementsOf(id))
            {
                if (conversation.Describe(new ConversationTargetRequest(id, placement)) is not { } subject) continue;
                if (subject.People.Any(person => string.Equals(person.Id, who, StringComparison.Ordinal))) return (id, placement);
            }
        }

        throw new InvalidOperationException($"The packs place nobody the person '{who}' is at, so the shipped line cannot be read against them.");
    }

    private static GameClock Clock() => new(
        GameCalendar.TwelveMonthsOfFourWeeks,
        new GameDate(1168, 1, 1, 9, 0, 0),
        new GameTimeScale(1),
        new DaylightWindow(new TimeOfDay(5, 0), new TimeOfDay(21, 0)));

    /// <summary>A party of one whose standing a case moves through the owner that owns it.</summary>
    private static PartyEntity PartyOf(int reputation)
    {
        PartyEntityFactory factory = new();
        return factory.Create(new PartyCreation(
            [
                new MemberCreation(new PartyMemberSeed(
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
                    hitPoints: ResourcePool.Full(20),
                    spellPoints: ResourcePool.Full(5))),
            ],
            coins: 500,
            foodPortions: 4,
            ProvisionUnit.Portions,
            reputation,
            fame: 0));
    }

    /// <summary>A person, a counter, and a guild's membership, over a catalog a case composes the real policies with.</summary>
    private sealed class Fixture : IDisposable
    {
        private Fixture(
            ContentCatalog catalog,
            MightAndMagic7Conversation conversation,
            MightAndMagic7Services services,
            PartyEntity party)
        {
            Catalog = catalog;
            Conversation = conversation;
            Services = services;
            Party = party;
            Clock = StandingPolicyTests.Clock();
        }

        internal ContentCatalog Catalog { get; }

        internal MightAndMagic7Conversation Conversation { get; }

        internal MightAndMagic7Services Services { get; }

        internal PartyEntity Party { get; }

        internal GameClock Clock { get; }

        internal static Fixture Build()
        {
            ContentCatalog catalog = ContentCatalogLoader.Load(
                new PolicyContentSource()
                    .Add("packs/world/pack.json", Manifest())
                    .Add("packs/world/places.json", Places())
                    .Add("packs/world/people.json", People())
                    .Add("packs/world/services.json", ServicesJson()),
                Layout).RequireValid();
            MightAndMagic7Services services = MightAndMagic7Services.Read(catalog)
                ?? throw new InvalidOperationException("The content declares services, so the policy must be read.");
            MightAndMagic7Conversation conversation = MightAndMagic7Conversation.Read(catalog, services)
                ?? throw new InvalidOperationException("The content declares people, so the dialogue policy must be read.");
            return new Fixture(catalog, conversation, services, PartyOf(reputation: 0));
        }

        /// <summary>One placement of the fixture's place, so a case speaks with exactly what it names.</summary>
        internal PlacementDefinition Placement(string id)
        {
            PlacePopulationContent population = PlacePopulationContent.Read(PlaceGraphLoader.Load(Catalog));
            return population.PlacementsOf(HallPlace).First(placement => placement.Content.Id == id);
        }

        /// <summary>The counter that serves anybody: a tavern, whose own service requires no membership.</summary>
        internal ServiceDefinition Shop()
        {
            ServiceDefinition? service = Services.Describe(new ServiceTargetRequest(HallPlace, Placement("service-7")));
            return service ?? throw new InvalidOperationException("The fixture's tavern placement is not a counter.");
        }

        /// <summary>The counter whose membership is what it sells.</summary>
        internal ServiceDefinition Guild()
        {
            ServiceDefinition? service = Services.Describe(new ServiceTargetRequest(HallPlace, Placement("service-8")));
            return service ?? throw new InvalidOperationException("The fixture's guild placement is not a counter.");
        }

        /// <summary>What the person at a placement has to say, with each topic's verdict.</summary>
        internal IReadOnlyList<ConversationOffer> Offers(PlacementDefinition placement)
        {
            ConversationSubject subject = Conversation.Describe(new ConversationTargetRequest(HallPlace, placement))!;
            return Conversation.Offers(new ConversationContext(HallPlace, placement, subject, subject.First.Id, [], Party, Clock));
        }

        /// <summary>The topics on offer at a placement.</summary>
        internal IReadOnlyList<string> OnOffer(PlacementDefinition placement) =>
            [.. Offers(placement).Where(offer => offer.IsOnOffer).Select(offer => offer.Id)];

        /// <summary>One topic of a placement's list, whatever its verdict.</summary>
        internal ConversationOffer Offer(PlacementDefinition placement, string topic) =>
            Offers(placement).First(offer => string.Equals(offer.Id, topic, StringComparison.Ordinal));

        /// <summary>One thing said, resolved against the party and the clock as they stand.</summary>
        internal ConversationAnswer Take(ConversationTopic topic, PlacementDefinition placement, string speaker)
        {
            ConversationSubject subject = Conversation.Describe(new ConversationTargetRequest(HallPlace, placement))!;
            return Conversation.Take(topic, new ConversationContext(HallPlace, placement, subject, speaker, [], Party, Clock));
        }

        public void Dispose() => Party.Dispose();

        private static string Manifest() =>
            """
            {
              "schemaVersion": 1,
              "packId": "world",
              "kind": "definitions",
              "provenance": { "description": "authored for a test" },
              "documents": [
                { "path": "places.json", "documentId": "places", "definitionKind": "place" },
                { "path": "people.json", "documentId": "people", "definitionKind": "person" },
                { "path": "services.json", "documentId": "services", "definitionKind": "service" }
              ]
            }
            """;

        private static string Places() =>
            """
            { "documentId": "places", "definitionKind": "place", "entries": [
              { "id": "1", "kind": "interior", "name": "Somewhere", "respawnDays": 7,
                "entryPoints": [ { "id": "Party Start", "x": 0, "y": 0, "z": 0, "yaw": 0 } ],
                "placements": [
                  { "id": "person-0", "kind": "person", "x": 0, "y": 100, "z": 0, "people": [ "np-2" ] },
                  { "id": "service-7", "kind": "service", "houseId": 7, "x": 100, "y": 0, "z": 0, "people": [ "np-1" ] },
                  { "id": "service-8", "kind": "service", "houseId": 8, "x": 200, "y": 0, "z": 0, "people": [ "np-1" ] } ] } ] }
            """;

        private static string People() =>
            """
            { "documentId": "people", "definitionKind": "person", "entries": [
              { "id": "np-1", "npcId": 1, "name": "Tester One", "portrait": "709",
                "greeting": "'Well met, travellers.'", "greetingAgain": "'Back again, are you?'", "house": 7,
                "dialogueEvents": 0, "topics": [] },
              { "id": "np-2", "npcId": 2, "name": "Tester Two", "portrait": "707",
                "greeting": "'A fine day for it.'", "greetingAgain": "'Still at it, then?'", "dialogueEvents": 0,
                "topics": [
                  { "id": "topic-1", "label": "The contest", "text": "'The first to bring the items wins.'", "textCount": 1 } ] } ] }
            """;

        private static string ServicesJson() =>
            """
            { "documentId": "services", "definitionKind": "service", "entries": [
              { "id": "7", "kind": "Tavern", "name": "The Broken Wheel", "proprietor": "Master Ash",
                "operations": [ "provision", "stay" ], "openHour": 6, "closedHour": 22,
                "priceMultiplier": 1.5, "skillPriceMultiplier": 1.5 },
              { "id": "8", "kind": "Fire Guild", "name": "The Fire Guild", "proprietor": "Master Ash",
                "operations": [ "buy", "teach" ], "openHour": 6, "closedHour": 18,
                "priceMultiplier": 1.5, "skillPriceMultiplier": 1.5, "membership": "guild.fire",
                "lessons": [
                  { "kind": "effect", "subject": "guild.fire", "amount": 1, "value": 50, "name": "Fire Guild membership" } ] } ] }
            """;
    }
}
