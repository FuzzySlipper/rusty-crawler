using System.Text.Json;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Conversation;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Progression;
using PartyRpg.Kit.Quests;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// Quests: the definitions a game states, the instances a party holds, the objective kinds and the owner
/// each one reads, the one turn-in that pays, and what a save carries.
/// </summary>
/// <remarks>
/// <para>
/// The quests are the test's own, which is the point of the seam: the kit holds no quest, no creature, and
/// no place of any game's, so a test states its own and demands the same mechanism serve it. What only this
/// suite can prove is that every objective kind reads the owner that already reports it — the fight's
/// deaths, the party's inventory, the world's places, the conversation's records, and the party's own flags
/// — that each reward reaches its owner rather than a copy of it, that a refusal names the objective which
/// is unmet, and that an instance survives a save with its progress intact.
/// </para>
/// <para>
/// The save round trip goes through the document a product really writes: the party's and the quests'
/// sections are serialized and read back with the one current schema's own metadata, so a section that
/// could not be written is a failure here rather than in a session.
/// </para>
/// </remarks>
public sealed class QuestTests
{
    private static readonly PlaceId Keep = new("1");
    private static readonly PlaceId Vault = new("2");
    private static readonly ItemDefinitionId Seal = new("seal");
    private static readonly ItemDefinitionId Parcel = new("parcel");
    private static readonly ItemDefinitionId Medal = new("medal");
    private static readonly EffectId Signal = new("signal");
    private static readonly EffectId ClerkMet = new("met:clerk");
    private static readonly string Monster = "7";

    [Fact]
    public void Every_public_removal_entry_is_exercised_by_the_retention_cases()
    {
        string[] entries = typeof(PartyEntity).GetMethods()
            .Where(method => method.ReturnType == typeof(ItemRemoval) || method.ReturnType == typeof(ItemChargeSpend))
            .Select(method => method.Name).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { nameof(PartyEntity.ConsumeItem), nameof(PartyEntity.ReleaseItem), nameof(PartyEntity.SpendItemCharge) }, entries);
    }

    [Theory]
    [InlineData("consume", 1)]
    [InlineData("release", 1)]
    [InlineData("charge", 1)]
    [InlineData("charge", 5)]
    public void Every_removal_refuses_an_accepted_errands_item_but_allows_offered_and_turned_in_items(string entry, int capacity)
    {
        using PartyEntity party = PartyOf(Member("Tester"));
        QuestDefinition definition = new(new QuestId("retention"), "Keep the seal", "marshal",
            [new QuestObjective("retrieve", QuestObjectiveKind.Retrieve, Seal.Value, label: "Carry the seal")]);
        PartyQuests quests = new(new TestQuests(definition), party);
        quests.Offer(definition.Id, "marshal");
        ItemInstance offered = party.AcquireItem(Seal).Item!;
        Assert.Null(Remove(entry, party, offered.Id, capacity));
        if (entry == "charge" && capacity > 1) Assert.True(party.ConsumeItem(offered.Id).Removed);
        ItemInstance held = party.AcquireItem(Seal).Item!;
        quests.Accept(definition.Id);
        Assert.True(quests.Read(definition.Id)!.IsComplete);
        // Met objectives still require their carried item until turn-in; the canonical need is asked live.
        long stamp = party.Stamp;
        Refusal refused = Assert.IsType<Refusal>(Remove(entry, party, held.Id, capacity));
        Assert.Equal(QuestCodes.QuestItemNeeded, refused.Code);
        Assert.Contains(definition.Name, refused.Message, StringComparison.Ordinal);
        Assert.Contains("Carry the seal", refused.Message, StringComparison.Ordinal);
        Assert.Same(held, party.FindItem(held.Id));
        Assert.Equal(0, held.State.ChargesSpent);
        Assert.Equal(stamp, party.Stamp);
        Assert.True(quests.TurnIn(definition.Id, "marshal").IsApplied);
        Assert.Null(Remove(entry, party, held.Id, capacity));
    }

    [Fact]
    public void A_delivery_hands_over_the_needed_item_through_the_quest_owners_explicit_transfer()
    {
        using PartyEntity party = PartyOf(Member("Tester"));
        QuestDefinition definition = new(new QuestId("delivery"), "Deliver the parcel", "marshal",
            [new QuestObjective("deliver", QuestObjectiveKind.Deliver, Parcel.Value, label: "Hand over the parcel", person: "met:marshal")]);
        PartyQuests quests = new(new TestQuests(definition), party);
        quests.Offer(definition.Id, "marshal");
        quests.Accept(definition.Id);
        party.Records.Set("met:marshal", 1);
        ItemInstance item = party.AcquireItem(Parcel).Item!;
        Assert.NotNull(party.ConsumeItem(item.Id).Refusal);
        QuestResult result = quests.TurnIn(definition.Id, "marshal");
        Assert.True(result.IsApplied, result.Refusal?.Message);
        Assert.Null(party.FindItem(item.Id));
        Assert.False(item.IsHeld);
        Assert.Single(result.Payment.Delivered);
        ItemInstance another = party.AcquireItem(Parcel).Item!;
        Assert.True(party.ConsumeItem(another.Id).Removed);
    }

    private static Refusal? Remove(string entry, PartyEntity party, ItemInstanceId item, int capacity) => entry switch
    {
        "consume" => party.ConsumeItem(item).Refusal,
        "release" => party.ReleaseItem(item).Refusal,
        "charge" => party.SpendItemCharge(item, capacity).Refusal,
        _ => throw new ArgumentOutOfRangeException(nameof(entry)),
    };

    [Fact]
    public void A_quest_is_offered_taken_progressed_and_turned_in_with_every_reward_reaching_its_owner()
    {
        using PartyEntity party = PartyOf(Member("Roderick"), Member("Ysolde"));
        PartyResourceLedger accounts = new(party);
        PartyProgression progression = new(new TestProgression(), party);
        PartyQuests quests = new(new TestQuests(Errand()), party, accounts, progression);
        PartyMember first = party.Members[0];

        // Nothing is known before somebody states it: an errand nobody offered cannot be taken, and one that
        // was offered cannot be finished before it is taken.
        Assert.Null(quests.Read(Errand().Id));
        Assert.Equal("quest-not-offered", quests.Accept(Errand().Id).Refusal!.Code);

        // A person the quest does not name as its giver cannot offer it, which is what keeps a journal from
        // claiming an errand came from somebody who never gave it.
        QuestResult stranger = quests.Offer(Errand().Id, "somebody-else", Keep);
        Assert.False(stranger.IsApplied);
        Assert.Equal("quest-not-the-giver", stranger.Refusal!.Code);

        QuestResult offered = quests.Offer(Errand().Id, "marshal", Keep);
        Assert.True(offered.IsApplied);
        Assert.Equal(QuestStage.Offered, offered.Stage);
        Assert.Equal("offered", quests.Read(Errand().Id)!.State);
        Assert.Equal(Keep.Value, quests.Instance(Errand().Id)!.OfferedIn);

        Assert.Equal(QuestStage.Accepted, quests.Accept(Errand().Id).Stage);

        // The two deeds that are recorded: the fight's deaths and the world's own report of the place. A
        // reach is met by standing in the place the quest names, and a kill counts what the fight read as
        // down where the quest says the deed happens.
        quests.Observe(Vault);
        quests.ObserveDeath(Death(Keep, Monster));
        quests.ObserveDeath(Death(Keep, Monster));

        // Everything the party already carries is read from its own owners: the seal in the one inventory,
        // the flag with its magnitude, and the record the conversation leaves of having met somebody.
        party.AcquireItem(Seal);
        party.AcquireItem(Parcel);
        party.Records.Set(Signal.Value, 2);
        party.Records.Set(ClerkMet.Value, 1);

        // The completion condition is the party's own standing, which is a threshold rather than a count.
        party.Reputation.ChangeReputation(4);

        QuestReading ready = quests.Read(Errand().Id)!;
        Assert.True(ready.IsComplete, string.Join("; ", ready.Unmet));
        Assert.Equal("completed", ready.State);
        Assert.True(ready.CanTurnIn);

        QuestResult paid = quests.TurnIn(Errand().Id, "marshal");
        Assert.True(paid.IsApplied, paid.Refusal?.Message);
        Assert.Equal(QuestStage.TurnedIn, paid.Stage);

        // Experience arrived at the one award entry and divided: two members, twelve hundred each.
        Assert.Equal(2400, paid.Payment.Experience!.Amount);
        Assert.Equal(1200, first.Progression.Experience);
        Assert.Equal(1200, party.Members[1].Progression.Experience);
        Assert.Equal(QuestSource(), paid.Payment.Experience.Source);

        // Coin went through the party's one ledger, and the item through its one acquisition path.
        Assert.Equal(120, paid.Payment.Coins);
        Assert.Equal(120, party.Purse.Coins);
        Assert.Equal(1, party.Inventory.TotalOf(Medal));

        // Records landed on the party's own effects: the quest's own record says it is finished, and the
        // reward's record is the deed a later rank or topic reads.
        Assert.True(party.Records.Has("errand:seal-of-office"));
        Assert.Equal(3, party.Records.CountOf("standing:marshal"));

        // What a delivery asked for left the party: the parcel was handed over, and the seal was not.
        Assert.Equal(0, party.Inventory.TotalOf(Parcel));
        Assert.Equal(1, party.Inventory.TotalOf(Seal));
        Assert.Equal([new QuestRewardItem(Parcel.Value, 1)], paid.Payment.Delivered);

        // An errand is finished once: the same turn-in is refused by name and pays nothing again.
        QuestResult twice = quests.TurnIn(Errand().Id, "marshal");
        Assert.Equal("quest-already-finished", twice.Refusal!.Code);
        Assert.Equal(120, party.Purse.Coins);
        Assert.Equal("turned-in", quests.Read(Errand().Id)!.State);
    }

    [Fact]
    public void Every_objective_kind_reads_the_owner_that_already_reports_it()
    {
        using PartyEntity party = PartyOf(Member("Roderick"));
        PartyResourceLedger accounts = new(party);
        PartyProgression progression = new(new TestProgression(), party);
        QuestDefinition everything = Everything();
        PartyQuests quests = new(new TestQuests(everything), party, accounts, progression);

        quests.Offer(everything.Id, "marshal");
        quests.Accept(everything.Id);

        // A kill objective counts the fight's own report of each death: a death of the row it names counts, and
        // a death of another row or in another place counts for nothing.
        quests.ObserveDeath(Death(Keep, Monster));
        quests.ObserveDeath(Death(Keep, "9"));
        quests.ObserveDeath(Death(Vault, Monster));
        Assert.Equal(1, Progress(quests, "kill"));

        // A reach objective reads the world's own place: standing in the place it names meets it, and the
        // world reporting somewhere else does not.
        quests.Observe(Vault);
        Assert.Equal(1, Progress(quests, "reach"));

        // A retrieve objective reads the party's one inventory, and stays unmet while the thing is not held.
        Assert.Equal(0, Progress(quests, "retrieve"));
        party.AcquireItem(Seal);
        Assert.Equal(1, Progress(quests, "retrieve"));
        Assert.Equal(QuestCodes.QuestItemNeeded, party.ConsumeItem(party.Inventory.Find(Seal)!.Id).Refusal!.Code);
        Assert.Equal(1, Progress(quests, "retrieve"));

        // A talk objective reads the record the conversation leaves of having met somebody, which is the
        // party's own carried state rather than a second list of people this mechanism keeps.
        Assert.Equal(0, Progress(quests, "talk"));
        party.Records.Set(ClerkMet.Value, 1);
        Assert.Equal(1, Progress(quests, "talk"));

        // A delivery needs both halves: the thing carried and the person it goes to.
        Assert.Equal(0, Progress(quests, "deliver"));
        party.AcquireItem(Parcel);
        Assert.Equal(1, Progress(quests, "deliver"));
        party.Records.Remove(ClerkMet.Value);
        Assert.Equal(0, Progress(quests, "deliver"));
        party.Records.Set(ClerkMet.Value, 1);

        // A flag objective reads the party's own record with its magnitude: one is one step and two is the
        // objective met, which is what makes a record a count rather than a yes or no.
        Assert.Equal(0, Progress(quests, "flag"));
        party.Records.Set(Signal.Value, 1);
        Assert.Equal(1, Progress(quests, "flag"));
        Assert.False(quests.Read(everything.Id)!.Objectives.Single(reading => reading.Objective.Id == "flag").IsMet);
        party.Records.Set(Signal.Value, 2);
        Assert.Equal(2, Progress(quests, "flag"));

        QuestReading reading = quests.Read(everything.Id)!;
        Assert.All(reading.Objectives, objective => Assert.True(objective.IsMet, objective.Statement));
        Assert.True(reading.IsComplete);
    }

    [Fact]
    public void A_turn_in_refused_because_an_objective_is_unmet_names_it()
    {
        using PartyEntity party = PartyOf(Member("Roderick"));
        PartyResourceLedger accounts = new(party);
        PartyProgression progression = new(new TestProgression(), party);
        QuestDefinition everything = Everything();
        PartyQuests quests = new(new TestQuests(Errand(), everything), party, accounts, progression);
        quests.Offer(everything.Id, "marshal");
        quests.Accept(everything.Id);

        QuestResult refused = quests.TurnIn(everything.Id, "marshal");
        Assert.False(refused.IsApplied);
        Assert.Equal("quest-objectives-unmet", refused.Refusal!.Code);
        Assert.Null(refused.Stage);

        // Every unmet objective is named, in the quest's own words, and the party is exactly where it stood:
        // nothing was paid, no record was left, and the errand is still the party's to finish.
        foreach (string unmet in new[] { "kill", "reach", "retrieve" })
        {
            string label = everything.Objectives.Single(objective => objective.Id == unmet).Label;
            Assert.Contains(label, refused.Refusal.Message, StringComparison.Ordinal);
        }

        Assert.Equal(0, party.Purse.Coins);
        Assert.Equal(0, party.Members[0].Progression.Experience);
        Assert.False(party.Records.Has("errand:seal-of-office"));
        Assert.Equal(QuestStage.Accepted, quests.Instance(everything.Id)!.Stage);

        // The completion conditions are judged too, and a quest that states one says which is unmet: every
        // objective is met here — including the delivery, which needs the person as well as the thing — and
        // the standing the quest asks for is what is left.
        party.AcquireItem(Seal);
        party.AcquireItem(Parcel);
        party.Records.Set(Signal.Value, 2);
        party.Records.Set(ClerkMet.Value, 1);
        quests.Observe(Vault);
        quests.ObserveDeath(Death(Keep, Monster));
        Assert.True(quests.TurnIn(everything.Id, "marshal").IsApplied);

        // A quest that states a completion condition beyond its objectives is judged on it as well, and says
        // which one is unmet: the errand's every objective is met here and its standing is not.
        quests.Offer(Errand().Id, "marshal");
        quests.Accept(Errand().Id);
        party.AcquireItem(Seal);
        party.AcquireItem(Parcel);
        party.Records.Set(ClerkMet.Value, 1);
        quests.Observe(Vault);
        quests.ObserveDeath(Death(Keep, Monster));
        quests.ObserveDeath(Death(Keep, Monster));
        QuestResult gated = quests.TurnIn(Errand().Id, "marshal");
        Assert.Equal(QuestCodes.QuestObjectivesUnmet, gated.Refusal!.Code);
        Assert.Contains(Errand().CompletionConditions[0].Label, gated.Refusal.Message, StringComparison.Ordinal);

        party.Reputation.ChangeReputation(4);
        Assert.True(quests.TurnIn(Errand().Id, "marshal").IsApplied);

        // A turn-in by somebody who is not the giver is refused by name, and one for an errand the party
        // never took is refused as such rather than quietly inventing an instance.
        QuestResult wrong = quests.TurnIn(new QuestId("no-such-errand"), "marshal");
        Assert.Equal("quest-not-taken", wrong.Refusal!.Code);
        Assert.Equal("quest-unknown", quests.Offer(new QuestId("no-such-errand"), "marshal").Refusal!.Code);
    }

    [Fact]
    public void Quest_state_survives_a_save_round_trip_including_a_partially_completed_instance()
    {
        using PartyEntity party = PartyOf(Member("Roderick"));
        PartyResourceLedger accounts = new(party);
        PartyProgression progression = new(new TestProgression(), party);
        TestQuests rule = new TestQuests(Errand(), Everything());
        PartyQuests quests = new(rule, party, accounts, progression);

        quests.Offer(Errand().Id, "marshal", Keep);
        quests.Accept(Errand().Id);
        quests.Offer(Everything().Id, "marshal", Vault);
        quests.Accept(Everything().Id);

        // One instance is part-way through both of the deeds this owner records, and the other has not begun.
        quests.ObserveDeath(Death(Keep, Monster));
        quests.Observe(Vault);

        QuestSave save = quests.Capture();

        // The document really written and read back: the section is serializable under the one current
        // schema's own metadata, so a quest state that could not be written fails here.
        GameClock clock = new(
            GameCalendar.TwelveMonthsOfFourWeeks,
            new GameDate(1168, 1, 1, 9, 0, 0),
            new GameTimeScale(30),
            new DaylightWindow(new TimeOfDay(5, 0), new TimeOfDay(21, 0)));
        PlaceStateLedger places = new(PlaceGraph.From([], []), PlaceRespawnRule.FromContent());
        SessionSave document = new(
            party.Capture(),
            ClockSave.Capture(clock),
            new WorldSave(new PartyPose(Keep, PlacePose.Origin), places.Capture()),
            save);
        string json = JsonSerializer.Serialize(document, SessionSaveJson.TypeInfo);
        SessionSave read = JsonSerializer.Deserialize(json, SessionSaveJson.TypeInfo)!;
        Assert.Equal(2, read.Quests.Instances.Count);

        // A restored party is a new party carrying the same durable identities and the same carried state,
        // which is what a load really builds — and the quest section is read back beside it.
        using PartyEntity restoredParty = new PartyEntityFactory().Restore(read.Party);
        PartyQuests resumed = new(rule, restoredParty, accounts, progression, save: read.Quests);
        Assert.Equal(2, resumed.Instances.Count);

        // The partial instance is exactly where it was: accepted, in the place the offer was taken in, with
        // the death and the place it recorded still recorded.
        QuestReading partial = resumed.Read(Everything().Id)!;
        Assert.Equal(QuestStage.Accepted, partial.Instance.Stage);
        Assert.Equal(Vault.Value, partial.Instance.OfferedIn);
        Assert.Equal(1, Progress(resumed, "kill"));
        Assert.Equal(1, Progress(resumed, "reach"));
        Assert.Equal(1, resumed.Read(Everything().Id)!.Objectives.Single(reading => reading.Objective.Id == "kill").Count);
        Assert.False(partial.IsComplete);

        // The other instance is untouched by any of it, which is what one instance per quest means: the same
        // death was read against both quests, and only the one that names the row in that place moved.
        QuestReading other = resumed.Read(Errand().Id)!;
        Assert.Equal(QuestStage.Accepted, other.Instance.Stage);
        Assert.Equal(1, other.Objectives.Single(reading => reading.Objective.Id == "kill").Count);

        // Both quests ask for the same vault, so the one place report meets both of their objectives: what a
        // reach records is the place, and which quests name it is each quest's own business.
        Assert.Equal(1, other.Objectives.Single(reading => reading.Objective.Id == "reach").Count);
    }

    [Fact]
    public void Two_instances_of_different_quests_progress_independently()
    {
        using PartyEntity party = PartyOf(Member("Roderick"));
        PartyResourceLedger accounts = new(party);
        PartyProgression progression = new(new TestProgression(), party);
        TestQuests rule = new TestQuests(Errand(), Patrol());
        PartyQuests quests = new(rule, party, accounts, progression);

        quests.Offer(Errand().Id, "marshal");
        quests.Accept(Errand().Id);
        quests.Offer(Patrol().Id, "marshal");
        quests.Accept(Patrol().Id);

        // One death in the place the errand names moves the errand and nothing else: the patrol asks for a
        // place rather than for a kill, so what one instance records is its own.
        quests.ObserveDeath(Death(Keep, Monster));
        Assert.Equal(1, Progress(quests, Errand(), "kill"));
        Assert.Equal(0, Progress(quests, Errand(), "reach"));

        // The place the errand names is the vault the patrol does not; the patrol's own place is the keep,
        // which the errand only kills in. Each report moves the instance that names it and leaves the other.
        quests.Observe(Vault);
        Assert.Equal(1, Progress(quests, Errand(), "reach"));
        Assert.Equal(0, quests.Read(Patrol().Id)!.Objectives.Single(reading => reading.Objective.Id == "beat").Count);

        quests.Observe(Keep);
        Assert.Equal(1, quests.Read(Patrol().Id)!.Objectives.Single(reading => reading.Objective.Id == "beat").Count);
        Assert.Equal(1, Progress(quests, Errand(), "kill"));

        // What the party carries is read by the quest that asks for it, and only that one.
        party.AcquireItem(Seal);
        Assert.Equal(1, Progress(quests, Errand(), "retrieve"));
        Assert.DoesNotContain(quests.Read(Patrol().Id)!.Objectives, reading => reading.Objective.Kind == QuestObjectiveKind.Retrieve);

        // Each instance finishes on its own terms: the patrol is complete once its round is walked, and the
        // errand is not, so a turn-in of one pays and a turn-in of the other refuses.
        QuestResult patrol = quests.TurnIn(Patrol().Id, "marshal");
        Assert.True(patrol.IsApplied, patrol.Refusal?.Message);
        QuestResult errand = quests.TurnIn(Errand().Id, "marshal");
        Assert.Equal("quest-objectives-unmet", errand.Refusal!.Code);
        Assert.Equal(QuestStage.TurnedIn, quests.Instance(Patrol().Id)!.Stage);
        Assert.Equal(QuestStage.Accepted, quests.Instance(Errand().Id)!.Stage);
    }

    [Fact]
    public void A_needed_quest_item_is_reported_while_the_instance_needs_it()
    {
        using PartyEntity party = PartyOf(Member("Roderick"));
        PartyResourceLedger accounts = new(party);
        PartyProgression progression = new(new TestProgression(), party);
        PartyQuests quests = new(new TestQuests(Errand()), party, accounts, progression);

        // An errand nobody has taken needs nothing: what is needed is what an unmet objective of a taken
        // errand asks for, which is why the reading is asked rather than a list of names kept anywhere.
        Assert.Null(quests.Needs(Seal));
        quests.Offer(Errand().Id, "marshal");
        Assert.Null(quests.Needs(Seal));
        quests.Accept(Errand().Id);

        QuestNeed needed = quests.Needs(Seal)!.Value;
        Assert.Equal(Errand().Id, needed.Quest);
        Assert.Equal("The seal of office", needed.Name);
        Assert.Contains("Carry the seal", needed.Statement, StringComparison.Ordinal);

        // What the errand asks to be delivered is needed too, and something no objective names is not.
        Assert.Contains("Deliver the parcel", quests.Needs(Parcel)!.Value.Statement, StringComparison.Ordinal);
        Assert.Null(quests.Needs(new ItemDefinitionId("something-else")));

        // An errand that is finished needs nothing more, so the refusal a sale reads stops applying.
        party.AcquireItem(Seal);
        party.AcquireItem(Parcel);
        party.Records.Set(Signal.Value, 2);
        party.Records.Set(ClerkMet.Value, 1);
        quests.Observe(Vault);
        quests.ObserveDeath(Death(Keep, Monster));
        quests.ObserveDeath(Death(Keep, Monster));
        party.Reputation.ChangeReputation(4);
        QuestResult finished = quests.TurnIn(Errand().Id, "marshal");
        Assert.True(finished.IsApplied, finished.Refusal?.Message);
        Assert.Null(quests.Needs(Seal));
    }

    [Fact]
    public void The_kit_s_world_and_conversation_code_own_no_quest_definition_or_objective()
    {
        // A quest is content plus this owner: the world, the conversation, and the interaction mechanism name an
        // errand's identity and never what it asks, which is what keeps a second reading of an objective out of the
        // code that reports the deeds. The law binds every name in those three owners' sources, so a use of anything
        // the quest owner declares — a definition, an objective, an instance, a reading, the owner itself — fails
        // here however it is spelled.
        SourceCode kit = ProductSource.Kit;
        string[] directories = ["src/PartyRpg.Kit/World/", "src/PartyRpg.Kit/Conversation/", "src/PartyRpg.Kit/Interaction/"];
        foreach (string directory in directories) Assert.NotEmpty(kit.TreesUnder(directory));

        SourceSite[] offenders =
        [
            .. kit.UsesOfNamespace(typeof(PartyQuests).Namespace!)
                .Where(site => directories.Any(directory => site.File.StartsWith(directory, StringComparison.Ordinal))),
        ];
        Assert.True(offenders.Length == 0, "The world and the conversation own no quest:\n" + string.Join('\n', offenders.Select(site => site.ToString())));

        // The law is not vacuous: the owner's own sources are exactly what it looks for.
        Assert.Contains(kit.UsesOfNamespace(typeof(PartyQuests).Namespace!), site => site.File.StartsWith("src/PartyRpg.Kit/Sessions/", StringComparison.Ordinal));
    }

    /// <summary>How much of one objective the party has recorded or is reported to have done.</summary>
    private static int Progress(PartyQuests quests, string objective) =>
        Progress(quests, Everything(), objective);

    /// <summary>How much of one quest's objective the party has recorded or is reported to have done.</summary>
    private static int Progress(PartyQuests quests, QuestDefinition quest, string objective) =>
        quests.Read(quest.Id)!.Objectives.Single(reading => reading.Objective.Id == objective).Count;

    /// <summary>How much of the errand's own objective the party has recorded.</summary>
    private static int ResumedProgress(PartyQuests quests, QuestId quest) =>
        quests.Read(quest)!.Objectives[0].Count;

    /// <summary>The source of an award a finished quest earned, as the owner names it.</summary>
    private static string QuestSource() => PartyQuests.QuestSource;

    /// <summary>A creature's death, as the fight reports it, read from a placement as the ruleset would.</summary>
    private static CreatureDeath Death(PlaceId place, string row)
    {
        using JsonDocument payload = JsonDocument.Parse($$"""{ "kind": "monster", "id": "monster-0", "monster": {{row}} }""");
        PlacementDefinition placement = new(
            new PlacementContentId("monster", "monster-0"),
            "spawnPoints",
            0,
            PlacePose.Origin,
            new ContentEntry("monster-0", payload.RootElement.Clone()));
        return new CreatureDeath(place, placement, $"a creature of row {row}");
    }

    /// <summary>The errand the first test drives: a short quest with a payment of every kind.</summary>
    private static QuestDefinition Errand() => new(
        new QuestId("seal-of-office"),
        "The seal of office",
        "marshal",
        [
            new QuestObjective("kill", QuestObjectiveKind.Kill, Monster, 2, "Bring down the thing", place: Keep.Value),
            new QuestObjective("reach", QuestObjectiveKind.Reach, Vault.Value, label: "Reach the vault"),
            new QuestObjective("retrieve", QuestObjectiveKind.Retrieve, Seal.Value, label: "Carry the seal"),
            new QuestObjective("deliver", QuestObjectiveKind.Deliver, Parcel.Value, label: "Deliver the parcel", person: ClerkMet.Value),
        ],
        new QuestRewards(
            experience: 2400,
            coins: 120,
            items: [new QuestRewardItem(Medal.Value)],
            records: [new QuestRewardRecord("standing:marshal", 3)]),
        completionConditions: [new ConversationCondition(ConversationConditionKind.Reputation, "standing", 4, "the party's standing")],
        record: "errand:seal-of-office",
        note: "Carry the seal to the vault and hand the parcel over.");

    /// <summary>A second errand that shares nothing with the first but its giver.</summary>
    private static QuestDefinition Patrol() => new(
        new QuestId("patrol"),
        "The long patrol",
        "marshal",
        [new QuestObjective("beat", QuestObjectiveKind.Reach, Keep.Value, label: "Walk the keep")]);

    /// <summary>One quest that states every objective kind, for the kinds' own proof.</summary>
    private static QuestDefinition Everything() => new(
        new QuestId("everything"),
        "Everything",
        "marshal",
        [
            new QuestObjective("kill", QuestObjectiveKind.Kill, Monster, 1, "Bring down the thing", place: Keep.Value),
            new QuestObjective("reach", QuestObjectiveKind.Reach, Vault.Value, label: "Reach the vault"),
            new QuestObjective("retrieve", QuestObjectiveKind.Retrieve, Seal.Value, label: "Carry the seal"),
            new QuestObjective("talk", QuestObjectiveKind.Talk, ClerkMet.Value, label: "Speak with the clerk"),
            new QuestObjective("deliver", QuestObjectiveKind.Deliver, Parcel.Value, label: "Deliver the parcel", person: ClerkMet.Value),
            new QuestObjective("flag", QuestObjectiveKind.Flag, Signal.Value, 2, "Raise the signal"),
        ]);

    private static PartyEntity PartyOf(params MemberCreation[] members) =>
        new PartyEntityFactory().Create(new PartyCreation(members, coins: 0, foodPortions: 0, reputation: 0, fame: 0));

    private static MemberCreation Member(string name) => new(new PartyMemberSeed(
        name,
        new RaceId("testfolk"),
        new ClassId("recruit"),
        [new AttributeScore(new AttributeId("vigour"), 12)],
        skills: [],
        spells: [],
        experience: 0,
        level: 1,
        skillPoints: 0,
        classRank: 1,
        conditions: [],
        hitPoints: ResourcePool.Full(80),
        spellPoints: ResourcePool.Full(20)));

    /// <summary>The quests a test states, with the one ruleset answer this mechanism cannot make itself.</summary>
    private sealed class TestQuests(params QuestDefinition[] definitions) : IQuestRule
    {
        public IReadOnlyList<QuestDefinition> Definitions { get; } = definitions;

        public QuestDefinition? Definition(QuestId quest)
        {
            foreach (QuestDefinition definition in Definitions)
            {
                if (definition.Id == quest) return definition;
            }

            return null;
        }

        /// <summary>A death counts for the objective that names the row the placement stands for.</summary>
        public int Counts(QuestKillRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            string row = request.Body.Source.GetId("monster");
            return string.Equals(row, request.Objective.Target, StringComparison.Ordinal) ? 1 : 0;
        }

        /// <summary>A stated condition holds exactly when the party carries what it names.</summary>
        public bool Holds(QuestConditionRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            return request.Condition.Kind switch
            {
                ConversationConditionKind.Flag or ConversationConditionKind.Errand =>
                    request.Party.Records.Has(request.Condition.Name),
                ConversationConditionKind.Reputation =>
                    request.Party.Reputation.Reputation >= request.Condition.Amount,
                _ => false,
            };
        }
    }

    /// <summary>A division an award needs, so experience really lands on the members.</summary>
    private sealed class TestProgression : IProgressionRule
    {
        public long ExperienceForLevel(int level) => 1000L * Math.Max(1, level);

        public IReadOnlyList<ProgressionShare> Divide(ProgressionDivision division)
        {
            ArgumentNullException.ThrowIfNull(division);
            List<ProgressionShare> shares = [];
            long each = division.Amount / Math.Max(1, division.Party.Members.Count);
            foreach (PartyMember member in division.Party.Members)
            {
                shares.Add(new ProgressionShare(member.Id, member.Profile.Name, each));
            }

            return shares;
        }

        public ProgressionGrowth Growth(ProgressionGrowthRequest request) => ProgressionGrowth.None;

        public ProgressionStanding Standing(ProgressionStandingRequest request) => ProgressionStanding.None;
    }
}
