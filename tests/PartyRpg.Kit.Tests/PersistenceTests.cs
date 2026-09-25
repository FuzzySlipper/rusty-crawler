using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Persistence;
using PartyRpg.Kit.Presentation;
using PartyRpg.Kit.Rulesets;
using PartyRpg.Kit.Sessions;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using PartyRpg.Rulesets.MightAndMagic7;
using Rusty.Engine;
using Rusty.Engine.Entities;
using Rusty.Engine.Persistence;
using Xunit;

namespace PartyRpg.Kit.Tests;

/// <summary>
/// The session's one current save schema: what a played session round-trips, what a malformed save names,
/// what is dropped on purpose, and where a save is allowed to happen.
/// </summary>
/// <remarks>
/// <para>
/// The party here is created through this game's real creation flow, so the round trip carries what a
/// player actually built rather than a hand-written seed: portraits that differ between neighbours, empty
/// starting equipment and empty starting spells, and the identities the party minted. The world, the clock,
/// and the larder policy are the test's own, because the kit holds none of them.
/// </para>
/// <para>
/// The bytes go through the engine's own JSON codec over the source-generated metadata — the same path a
/// product saves by — so a document that only survived because a test rebuilt it in memory would not pass
/// here.
/// </para>
/// </remarks>
public sealed class PersistenceTests
{
    private static readonly ContentLayout Layout = new("packs", "imports", "bundles");
    private static readonly PlaceId Home = new("1");
    private static readonly PlaceId Cave = new("2");
    private static readonly ConditionId Weakness = new("weak");
    private static readonly FacingRule Facing = new(unitsPerTurn: 2048, minimumPitch: -512, maximumPitch: 512);
    private static readonly SessionComposition Composition = new(new RulesetId("test.ruleset"), "Test");

    [Fact]
    public void A_played_session_round_trips_every_durable_fact_through_the_saved_bytes()
    {
        using Played played = new();
        played.Play();

        // The session is written at its explicit boundary, and the document that was written is the one the
        // save holds — not a second composition of it.
        SessionSave written = played.Session.Save();
        Assert.Same(written, Assert.Single(played.Store.Writes));

        // The bytes are the product's schema: they decode back into the same document and re-encode to the
        // same bytes, which is what "one current schema" means for a document that is written and read.
        byte[] bytes = Encode(written);
        SessionSave loaded = Decode(bytes);
        Assert.Equal(bytes, Encode(loaded));

        // Everything the save records is judged against the world it would be loaded into, and nothing is
        // wrong with it.
        Assert.Empty(loaded.Problems(Graph(), new PartyEntityFactory()));

        // A load composes a fresh session: a new clock moved to the saved position, a party rebuilt by the
        // factory, and a world rebuilt over the save's place and per-place state.
        GameClock clock = Clock();
        loaded.Clock.ApplyTo(clock);
        using PartyEntity restored = new PartyEntityFactory().Restore(loaded.Party);
        using SessionWorld world = RestoredWorld(clock, restored, loaded);

        // The place the party emptied comes back empty: the population is rebuilt from the place's state,
        // and the state says the party cleared it, so a load cannot quietly repopulate a dungeon.
        world.Populate();
        Assert.Empty(world.Population.Entities);

        AssertSameParty(played.Party, restored);
        Assert.Equal(played.World.Party.Capture(), world.Party.Capture());
        Assert.Equal(played.Clock.Elapsed, clock.Elapsed);
        Assert.Equal(played.Clock.Now, clock.Now);
        Assert.Equal(played.Clock.ElapsedGameDays, clock.ElapsedGameDays);
        Assert.Equal(played.World.Places.ElapsedGameDays, world.Places.ElapsedGameDays);
        foreach (PlaceDefinition place in Graph().Places)
        {
            Assert.Equal(played.World.Places.StateOf(place.Id), world.Places.StateOf(place.Id));
        }

        // The facts a person would check are the ones this played for: the party travelled, the place it
        // entered was cleared, time passed, and food was spent.
        Assert.Equal(Cave, world.Place);
        Assert.True(world.Places.StateOf(Cave).Visited);
        Assert.True(world.Places.StateOf(Cave).Cleared);
        Assert.Equal(GameDuration.FromHours(48), clock.Elapsed);
        Assert.Equal(8, restored.Food.Portions);
        Assert.Equal(3, restored.Purse.Coins);

        // The panel a resumed session publishes reads from the restored owners, not from the document.
        using RecordingUiProjectionChannel channel = new();
        using PartyRpgSession resumed = new(Composition, channel, world, clock: clock, party: restored);
        ProjectedNode published = channel.Latest();
        Assert.Equal("2", published.Field("world").Field("place").AsString());
        Assert.Equal("1168-01-03", published.Field("clock").Field("date").AsString());
        Assert.Equal(8d, published.Field("party").Field("provisions").AsNumber());
        Assert.Equal(3d, published.Field("party").Field("coins").AsNumber());
    }

    [Fact]
    public void Every_member_keeps_its_own_portrait_across_the_saved_bytes()
    {
        using Played played = new();

        // The portraits are what creation chose, and they differ between neighbours, so a default could not
        // pass for a carried value.
        PortraitId[] chosen = [.. played.Party.Members.Select(member => member.Profile.Portrait!.Value)];
        Assert.Equal(4, chosen.Length);
        Assert.Equal(4, chosen.Distinct().Count());

        SessionSave loaded = Decode(Encode(played.Session.Capture()));
        using PartyEntity restored = new PartyEntityFactory().Restore(loaded.Party);

        for (int index = 0; index < chosen.Length; index++)
        {
            Assert.Equal(chosen[index], restored.Members[index].Profile.Portrait);
            Assert.Equal(played.Party.Members[index].Profile.Race, restored.Members[index].Profile.Race);
        }
    }

    [Fact]
    public void Empty_equipment_and_an_empty_spellbook_round_trip_as_empty()
    {
        // A created party starts with nothing worn and no spells: the manual itemises no starting kit and
        // this game teaches magic from books. A save must round-trip that emptiness rather than fill it in.
        using PartyEntity party = CreatedParty();
        Assert.All(party.Members, member => Assert.Empty(member.Equipment.Items));
        Assert.All(party.Members, member => Assert.Empty(member.Spells.Known));

        SessionSave save = new(party.Capture(), new ClockSave(0), new WorldSave(new PartyPose(Home, PlacePose.Origin), new PlaceStateLedgerSnapshot(0, [])));
        using PartyEntity restored = new PartyEntityFactory().Restore(Decode(Encode(save)).Party);

        Assert.All(restored.Members, member => Assert.Empty(member.Equipment.Items));
        Assert.All(restored.Members, member => Assert.Empty(member.Spells.Known));
    }

    [Fact]
    public void Item_damage_and_enchantments_survive_the_saved_bytes_in_order()
    {
        using Played played = new();
        played.Play();

        SessionSave loaded = Decode(Encode(played.Session.Capture()));
        using PartyEntity restored = new PartyEntityFactory().Restore(loaded.Party);

        ItemInstance before = Assert.Single(played.Party.Items, item => item.Definition == new ItemDefinitionId("signet"));
        ItemInstance after = restored.FindItem(before.Id)!;

        Assert.Equal(4, after.State.Damage);
        Assert.True(after.State.IsIdentified);
        Assert.Equal(
            [new ItemEnchantment(new EnchantmentId("flame"), 7), new ItemEnchantment(new EnchantmentId("ward"), 2)],
            after.State.Enchantments);
    }

    [Fact]
    public void A_restore_keeps_what_was_saved_and_the_rules_supplied_at_load_still_gate_new_items()
    {
        using Played played = new();
        SessionSave save = played.Session.Capture();
        int held = save.Party.Items.Count;

        // The rules a party obeys are policy and are supplied when it is built, so a load supplies them
        // again — and does not re-judge what the save already recorded, because re-running a changed rule
        // could lose an item the product itself saved.
        using PartyEntity restored = new PartyEntityFactory(inventoryCapacity: new RefuseEverything()).Restore(save.Party);
        Assert.Equal(held, restored.Items.Count);
        ItemAcquisition refused = restored.AcquireItem(new ItemDefinitionId("sword"));
        Assert.Equal("test-no-room", refused.Refusal!.Code);
    }

    [Fact]
    public void Only_the_explicit_boundary_writes_a_save()
    {
        using Played played = new();
        Assert.Empty(played.Store.Writes);

        // Everything a session does by itself writes nothing: admitted updates, a hold, a republish, and
        // releasing the session are all silent.
        played.Session.Start();
        for (ulong step = 1; step <= 5; step++) played.Session.Update(Update(step, admitted: 6));
        played.Session.Hold();
        played.Session.Update(Update(6, admitted: 6));
        played.Session.ReleaseHold();
        played.Session.PublishWorld();
        Assert.Empty(played.Store.Writes);

        played.Session.Save();
        Assert.Single(played.Store.Writes);

        // A second save is a second explicit act, not a side effect of the first.
        played.Session.Save();
        Assert.Equal(2, played.Store.Writes.Count);

        // Ending the session is not a save either.
        played.Session.Dispose();
        Assert.Equal(2, played.Store.Writes.Count);
    }

    [Fact]
    public void A_session_with_nowhere_to_write_refuses_to_save_by_name()
    {
        using RecordingUiProjectionChannel channel = new();
        using PartyEntity party = CreatedParty();
        using SessionWorld world = World(Clock(), party);
        using PartyRpgSession session = new(Composition, channel, world, clock: Clock(), party: party);

        Assert.Null(session.Saves);
        InvalidOperationException refused = Assert.Throws<InvalidOperationException>(() => session.Save());
        Assert.Contains("nowhere to write a save", refused.Message, StringComparison.Ordinal);

        // Capturing still works: what a session holds is separate from where a save would go.
        Assert.Empty(session.Capture().Problems(Graph(), new PartyEntityFactory()));
    }

    [Fact]
    public void A_session_holding_nothing_to_save_is_refused_by_name()
    {
        using RecordingUiProjectionChannel channel = new();
        using PartyRpgSession empty = new(Composition, channel);

        SessionSaveException refused = Assert.Throws<SessionSaveException>(() => empty.Capture());

        Assert.Contains("holds no party", refused.Message, StringComparison.Ordinal);
        Assert.Contains("holds no clock", refused.Message, StringComparison.Ordinal);
        Assert.Contains("holds no world", refused.Message, StringComparison.Ordinal);
        Assert.Equal(3, refused.Problems.Count);
    }

    [Fact]
    public void A_clock_holding_scheduled_work_cannot_be_saved()
    {
        using Played played = new();
        DeadlineId deadline = played.Clock.ScheduleAfter(GameDuration.FromHours(2));

        // A deadline's number means nothing without the owner that scheduled it, and no owner exists, so a
        // save refuses rather than dropping the schedule or restoring one nobody can act on.
        SessionSaveException refused = Assert.Throws<SessionSaveException>(() => played.Session.Capture());
        Assert.Contains("pending deadline", Assert.Single(refused.Problems), StringComparison.Ordinal);

        // Once the clock holds nothing scheduled, the same session saves.
        Assert.True(played.Clock.Cancel(deadline));
        Assert.NotNull(played.Session.Capture());
    }

    [Fact]
    public void A_save_that_does_not_fit_the_world_names_every_problem_at_once()
    {
        using Played played = new();
        SessionSave sound = played.Session.Capture();

        // A save wrong in four separate ways: a member with no identity, an item worn by nobody the save
        // records, a place the world does not have, and a pose the place will not admit.
        PartyMemberSave[] members = [.. sound.Party.Members];
        members[0] = members[0] with { Id = default };
        ItemSave[] items =
        [
            .. sound.Party.Items,
            new ItemSave(
                new ItemInstanceId(sound.Party.NextItemValue + 5),
                new ItemDefinitionId("orphan"),
                1,
                ItemState.Unidentified,
                ItemCustody.EquippedBy(new PartyMemberId(97), new EquipmentSlot("main-hand"))),
        ];
        PartySave party = new(
            sound.Party.NextMemberValue,
            sound.Party.NextItemValue + 6,
            members,
            items,
            sound.Party.Coins,
            sound.Party.FoodPortions,
            sound.Party.FoodUnit,
            sound.Party.Reputation,
            sound.Party.Fame,
            sound.Party.Followers,
            sound.Party.Effects);
        PlaceState[] states =
        [
            .. sound.World.Places.States,
            PlaceState.Untouched(new PlaceId("nowhere")),
            // The current place recorded twice, and restored on a day the save has not reached: two more
            // contradictions of the same section, so the list is not one problem per section.
            sound.World.Places.States[0],
            sound.World.Places.States[0] with { LastResetDay = sound.World.Places.ElapsedGameDays + 1 },
        ];
        SessionSave broken = new(
            party,
            sound.Clock,
            new WorldSave(
                new PartyPose(Cave, new PlacePose(-40, 0, 0, 0, 0)),
                new PlaceStateLedgerSnapshot(sound.World.Places.ElapsedGameDays, states)));

        // Nothing is built while the document is judged, so the whole list arrives at once.
        IReadOnlyList<string> problems = broken.Problems(Graph(), new PartyEntityFactory(), RefusesNegativeX());

        Assert.Contains(problems, problem => problem.Contains("member 1 is recorded without an identity", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.Contains("whom the save does not record", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.Contains("the world has no such place", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.Contains("does not admit the party's recorded pose", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.Contains("is recorded twice", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.Contains("after the day", StringComparison.Ordinal));
    }

    [Fact]
    public void A_member_with_no_identity_in_the_saved_bytes_is_refused_by_name()
    {
        using Played played = new();
        byte[] sound = Encode(played.Session.Capture());

        // A document written by an older writer, or by an editor, with the first member's identity gone: the
        // field nobody wrote is read back as the unset identity, and the load says which member it cannot
        // address rather than resuming a party with a nameless member in it.
        JsonNode document = JsonNode.Parse(Encoding.UTF8.GetString(sound))!;
        Assert.True(document["party"]!["members"]![0]!.AsObject().Remove("id"));

        SessionSave loaded = Decode(Encoding.UTF8.GetBytes(document.ToJsonString()));
        Assert.Contains(
            loaded.Problems(Graph(), new PartyEntityFactory()),
            problem => problem.Contains("member 1 is recorded without an identity", StringComparison.Ordinal));
        ArgumentException refused = Assert.Throws<ArgumentException>(() => new PartyEntityFactory().Restore(loaded.Party));
        Assert.Contains("member 1 is recorded without an identity", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_record_held_by_nobody_is_refused_by_name_and_never_lands_in_the_pack()
    {
        using PartyEntity party = CreatedParty();
        ItemInstance loose = party.CreateItem(new ItemDefinitionId("loose-loot"));
        PartySave save = party.Capture();

        // The record a container or a loose world item would produce if it were written as an inventory
        // record: an instance the party has never picked up.
        PartySave withDetached = Copy(
            save,
            [.. save.Items, new ItemSave(loose.Id, loose.Definition, 1, loose.State, ItemCustody.Detached)]);

        IReadOnlyList<string> problems = new PartyEntityFactory().Problems(withDetached);
        string problem = Assert.Single(problems, entry => entry.Contains("held by nobody", StringComparison.Ordinal));
        Assert.Contains("an item it never took", problem, StringComparison.Ordinal);

        ArgumentException refused = Assert.Throws<ArgumentException>(() => new PartyEntityFactory().Restore(withDetached));
        Assert.Contains("held by nobody", refused.Message, StringComparison.Ordinal);

        // The control: the same party without that record restores, so the refusal is the loose record.
        using PartyEntity restored = new PartyEntityFactory().Restore(save);
        Assert.DoesNotContain(restored.Items, item => item.Id == loose.Id);
    }

    [Fact]
    public void A_load_leaves_no_stale_entity_behind_and_rebuilds_the_places_population()
    {
        using Played played = new();
        SessionSave save = played.Session.Capture();

        // The pre-save session's runtime entities: the party's own, and the current place's population.
        EntityId partyRuntime = played.Party.RuntimeId;
        List<PlacePopulationEntity> population = [.. played.World.Population.Entities];
        Assert.NotEmpty(population);

        // Ending the session ends the store its entities lived in, so a reference kept from before the save
        // cannot even answer whether it is alive: there is no stale entity to read, only a dead store.
        played.Session.Dispose();
        Assert.Throws<ObjectDisposedException>(() => played.Party.IsAlive);
        Assert.Throws<ObjectDisposedException>(() => population[0].IsAlive);

        GameClock clock = Clock();
        save.Clock.ApplyTo(clock);
        using PartyEntity restored = new PartyEntityFactory().Restore(save.Party);
        using SessionWorld world = RestoredWorld(clock, restored, save);

        // The restored party is a new party in a new store, and it is found by durable identity only. The
        // store mints runtime identity from its own counter, so a fresh store hands out the same numbers
        // again — which is exactly why the schema records none of them and why an entity is identified here
        // by the store that holds it and by the durable identity of what it stands for.
        Assert.NotSame(played.Party.Actor.Store, restored.Actor.Store);
        Assert.Equal(partyRuntime.Value, restored.RuntimeId.Value);
        Assert.True(restored.IsAlive);
        Assert.Equal(save.Party.Members[0].Seed.Name, restored.Member(save.Party.Members[0].Id).Profile.Name);
        Assert.All(restored.Members, member => Assert.True(member.IsAlive));

        // The population is rebuilt from the place's content placements: same content identity, new
        // entities, and the place the party resumes in is the one that has them.
        world.Populate();
        Assert.NotEmpty(world.Population.Entities);
        Assert.All(world.Population.Entities, entity => Assert.True(entity.IsAlive));
        Assert.Equal(
            population.Select(entity => entity.Content),
            world.Population.Entities.Select(entity => entity.Content));
        Assert.NotSame(population[0].Actor, world.Population.Entities[0].Actor);

        // Nothing the document carries is a runtime identity: its types are scanned for one elsewhere, and
        // no field of its bytes is named for one.
        Assert.DoesNotContain(
            "runtime",
            Encoding.UTF8.GetString(Encode(save)),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_saved_bytes_are_one_current_schema_with_no_version_field()
    {
        using Played played = new();
        byte[] bytes = Encode(played.Session.Capture());
        string json = Encoding.UTF8.GetString(bytes);

        // The document is exactly its three sections: no version marker, no schema number, and nothing a
        // compatibility reader could key on. A save is this shape or it is not read at all.
        using JsonDocument document = JsonDocument.Parse(bytes);
        Assert.Equal(
            ["party", "clock", "world"],
            document.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.DoesNotContain("version", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("schema", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("migrat", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_save_path_holds_no_runtime_identity_and_no_native_handle()
    {
        string directory = Path.Combine(RepositoryRoot(), "src", "PartyRpg.Kit", "Persistence");
        string[] sources = [.. Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)];
        Assert.NotEmpty(sources);

        // A save names people and things by the durable identities the party minted. An engine entity
        // identity, a store-local id, or a native handle in these types would be a reference a load cannot
        // honour, so the scan fails the kit if one appears rather than leaving it to a reviewer to notice.
        string[] forbidden = ["EntityId", "RuntimeId", "Actor", "unsafe", "Native", "GCHandle"];
        foreach (string source in sources)
        {
            // Comments are read past on purpose: a rule that forbids explaining itself is not a boundary, it
            // is a trap, and the reason this schema carries no runtime identity is worth writing down.
            string text = string.Join(
                '\n',
                File.ReadAllLines(source).Where(line =>
                    !line.TrimStart().StartsWith("///", StringComparison.Ordinal) &&
                    !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));
            foreach (string token in forbidden)
            {
                Assert.False(
                    text.Contains(token, StringComparison.Ordinal),
                    $"{Path.GetFileName(source)} names '{token}': a save carries durable identity only, and a runtime identity or native handle in the schema is a reference a load cannot honour.");
            }
        }
    }

    [Fact]
    public void The_document_is_read_and_written_through_the_engines_own_codec()
    {
        // The metadata is source-generated, which is what makes the codec AOT-safe: the engine's codec is
        // handed a JsonTypeInfo the build produced, so nothing here discovers a type at runtime.
        JsonProductStateCodec<SessionSave> codec = new(SessionSaveJson.TypeInfo);
        using Played played = new();
        SessionSave save = played.Session.Capture();

        ArrayBufferWriter<byte> buffer = new();
        codec.Encode(save, buffer);
        SessionSave decoded = codec.Decode(buffer.WrittenSpan);

        Assert.Equal(buffer.WrittenSpan.ToArray(), Encode(decoded));

        // The metadata is the generated context's own type for the document, so nothing on this path
        // resolves a type by reflection; the codec is handed exactly that.
        Assert.Same(SessionSaveJsonContext.Default.SessionSave.Type, SessionSaveJson.TypeInfo.Type);
    }

    private static byte[] Encode(SessionSave save) => Encode(new JsonProductStateCodec<SessionSave>(SessionSaveJson.TypeInfo), save);

    private static byte[] Encode(IProductStateCodec<SessionSave> codec, SessionSave save)
    {
        ArrayBufferWriter<byte> buffer = new();
        codec.Encode(save, buffer);
        return [.. buffer.WrittenSpan];
    }

    private static SessionSave Decode(byte[] bytes) =>
        new JsonProductStateCodec<SessionSave>(SessionSaveJson.TypeInfo).Decode(bytes);

    /// <summary>Compares every durable fact of two parties, member by member and instance by instance.</summary>
    private static void AssertSameParty(PartyEntity before, PartyEntity after)
    {
        Assert.Equal(before.Identity.NextMemberValue, after.Identity.NextMemberValue);
        Assert.Equal(before.Identity.NextItemValue, after.Identity.NextItemValue);
        Assert.Equal(before.Members.Count, after.Members.Count);
        for (int index = 0; index < before.Members.Count; index++)
        {
            PartyMember left = before.Members[index];
            PartyMember right = after.Members[index];
            Assert.Equal(left.Id, right.Id);
            Assert.Equal(left.Profile.Name, right.Profile.Name);
            Assert.Equal(left.Profile.Race, right.Profile.Race);
            Assert.Equal(left.Profile.Class, right.Profile.Class);
            Assert.Equal(left.Profile.Portrait, right.Profile.Portrait);
            Assert.Equal(left.Attributes.Scores, right.Attributes.Scores);
            Assert.Equal(left.Skills.Entries, right.Skills.Entries);
            Assert.Equal(left.Spells.Known, right.Spells.Known);
            Assert.Equal(left.Progression.Experience, right.Progression.Experience);
            Assert.Equal(left.Progression.Level, right.Progression.Level);
            Assert.Equal(left.Progression.SkillPoints, right.Progression.SkillPoints);
            Assert.Equal(left.Progression.ClassRank, right.Progression.ClassRank);
            Assert.Equal(left.Conditions.Active, right.Conditions.Active);
            Assert.Equal(left.Resources.HitPoints, right.Resources.HitPoints);
            Assert.Equal(left.Resources.SpellPoints, right.Resources.SpellPoints);
            Assert.Equal(left.Equipment.Count, right.Equipment.Count);
            foreach (EquippedItem equipped in left.Equipment.Items)
            {
                ItemInstance? same = right.Equipment.ItemIn(equipped.Slot);
                Assert.NotNull(same);
                AssertSameItem(equipped.Item, same);
            }
        }

        Assert.Equal(before.Items.Count, after.Items.Count);
        foreach (ItemInstance item in before.Items)
        {
            ItemInstance? same = after.FindItem(item.Id);
            Assert.NotNull(same);
            Assert.Equal(item.Custody, same.Custody);
            AssertSameItem(item, same);
        }

        Assert.Equal(before.Purse.Coins, after.Purse.Coins);
        Assert.Equal(before.Food.Portions, after.Food.Portions);
        Assert.Equal(before.Food.Unit, after.Food.Unit);
        Assert.Equal(before.Reputation.Reputation, after.Reputation.Reputation);
        Assert.Equal(before.Reputation.Fame, after.Reputation.Fame);
        Assert.Equal(before.Followers.Followers, after.Followers.Followers);
        Assert.Equal(before.Effects.Active, after.Effects.Active);
    }

    private static void AssertSameItem(ItemInstance before, ItemInstance after)
    {
        Assert.Equal(before.Id, after.Id);
        Assert.Equal(before.Definition, after.Definition);
        Assert.Equal(before.StackCount, after.StackCount);
        Assert.Equal(before.State.IsIdentified, after.State.IsIdentified);
        Assert.Equal(before.State.Damage, after.State.Damage);
        Assert.Equal(before.State.Enchantments, after.State.Enchantments);
    }

    /// <summary>A session that has been played, and everything a load rebuilds it from.</summary>
    private sealed class Played : IDisposable
    {
        internal Played()
        {
            Clock = PersistenceTests.Clock();
            Party = CreatedParty();
            World = PersistenceTests.World(Clock, Party);
            Channel = new RecordingUiProjectionChannel();
            Store = new RecordingSaveStore();
            Session = new PartyRpgSession(
                Composition,
                Channel,
                World,
                clock: Clock,
                party: Party,
                saveStore: Store);
        }

        internal GameClock Clock { get; }

        internal PartyEntity Party { get; }

        internal SessionWorld World { get; }

        internal RecordingUiProjectionChannel Channel { get; }

        internal RecordingSaveStore Store { get; }

        internal PartyRpgSession Session { get; }

        /// <summary>Plays the session: items picked up and worn, a place entered and cleared, travel, and food spent.</summary>
        internal void Play()
        {
            Session.Start();

            // A blade the party takes and a member wears, and a ring that is identified, damaged, and
            // enchanted — the item state a save has to keep.
            ItemInstance blade = Party.AcquireItem(new ItemDefinitionId("sword")).Item!;
            Assert.Null(Party.Equip(Party.Members[0].Id, new EquipmentSlot("main-hand"), blade.Id).Refusal);

            ItemInstance signet = Party.AcquireItem(new ItemDefinitionId("signet")).Item!;
            signet.Identify();
            signet.TakeDamage(4);
            signet.Enchant(new ItemEnchantment(new EnchantmentId("flame"), 7));
            signet.Enchant(new ItemEnchantment(new EnchantmentId("ward"), 2));

            // Coins spent the way a shop would spend them, so the purse is not its starting value.
            Assert.True(Party.Purse.TryDebit(197));

            // A journey: two days of road and two portions, charged on arrival by the world.
            PlaceTransition outbound = Assert.Single(Graph().TransitionsFrom(Home));
            Assert.True(World.Travel(outbound, TransitionKind.Walking).Arrived);

            // The party emptied the place it entered, and walked on inside it.
            World.Places.MarkCleared(Cave);
            World.Party.Move(12, -3, 0);
            World.Party.Turn(256, 0);
        }

        public void Dispose() => Session.Dispose();
    }

    private static PartyEntity CreatedParty()
    {
        PartyCreationFlow flow = MightAndMagic7Creation.Start();
        Assert.True(flow.IsComplete);
        return new PartyEntityFactory().Create(flow.ToCreation());
    }

    private static SessionWorld World(GameClock clock, PartyEntity party) =>
        new(
            Graph(),
            new PartyPoseOwner(new PartyPose(Home, new PlacePose(1, 2, 3, 512, 0)), Facing),
            new PlaceStateLedger(Graph(), PlaceRespawnRule.FromContent()),
            new TestCostRule(),
            clock,
            mover: null,
            diagnostics: null,
            entrances: null,
            clock: clock,
            resources: new PartyResourceLedger(party, provisioning: new Rations(party, Weakness)));

    /// <summary>A world rebuilt from a save, over a party the factory restored.</summary>
    private static SessionWorld RestoredWorld(GameClock clock, PartyEntity party, SessionSave save)
    {
        PlaceGraph graph = Graph();
        return new SessionWorld(
            graph,
            new PartyPoseOwner(save.World.Pose, Facing),
            PlaceStateLedger.Restore(graph, PlaceRespawnRule.FromContent(), save.World.Places),
            new TestCostRule(),
            clock,
            mover: null,
            diagnostics: null,
            entrances: null,
            clock: clock,
            resources: new PartyResourceLedger(party, provisioning: new Rations(party, Weakness)));
    }

    /// <summary>Copies a party save with a different item list, which is how a defective document is stated.</summary>
    private static PartySave Copy(PartySave save, IReadOnlyList<ItemSave> items) =>
        new(
            save.NextMemberValue,
            save.NextItemValue,
            save.Members,
            items,
            save.Coins,
            save.FoodPortions,
            save.FoodUnit,
            save.Reputation,
            save.Fame,
            save.Followers,
            save.Effects);

    /// <summary>The place rule a pose outside the place is refused by, as a world would supply one.</summary>
    private static PlacePoseAdmission RefusesNegativeX() => (PlaceId _, PlacePose pose, out PlacePose admitted) =>
    {
        admitted = pose;
        return pose.X >= 0;
    };

    /// <summary>The clock these tests run on: a stated calendar, start, rate, and daylight window.</summary>
    private static GameClock Clock() => new(
        GameCalendar.TwelveMonthsOfFourWeeks,
        new GameDate(1168, 1, 1, 9, 0, 0),
        new GameTimeScale(30),
        new DaylightWindow(new TimeOfDay(5, 0), new TimeOfDay(21, 0)));

    private static ProductUpdate Update(ulong step, uint admitted, double fixedDelta = 1.0 / 60.0)
    {
        ProductUpdateFacts facts = new(
            ProductUpdateMode.Realtime,
            ProductLifecycleState.Running,
            1,
            1,
            0,
            step,
            60,
            admitted,
            0,
            fixedDelta);
        return new ProductUpdate(facts, ReadOnlySpan<ProductInputEvent>.Empty);
    }

    private static PlaceGraph Graph() => PlaceGraphLoader.Load(
        ContentCatalogLoader.Load(
            new InMemoryContentSource()
                .Add("packs/world/pack.json", Manifest())
                .Add("packs/world/places.json", Document("places", "place",
                    """{ "id": "1", "kind": "region", "name": "Home", "respawnDays": 1, "entryPoints": [ { "id": "Party Start", "x": 1, "y": 2, "z": 3, "yaw": 512 } ], "placements": [ { "kind": "monster", "id": "wanderer", "x": 4, "y": 5, "z": 0 } ] }""",
                    """{ "id": "2", "kind": "interior", "name": "Cave", "respawnDays": 1, "entryPoints": [ { "id": "Party Start", "x": 5, "y": 6, "z": 7, "yaw": 0 } ], "placements": [ { "kind": "monster", "id": "lurker", "x": 8, "y": 9, "z": 0 } ] }"""))
                .Add("packs/world/links.json", Document("links", "travel-link",
                    """{ "id": "edge", "fromPlace": "1", "toPlace": "2", "entryPoint": "Party Start" }""",
                    """{ "id": "back", "fromPlace": "2", "toPlace": "1", "x": 1, "y": 2, "z": 3, "yaw": 512 }""")),
            Layout).RequireValid());

    private static string Manifest() =>
        """
        {
          "schemaVersion": 1,
          "packId": "world",
          "kind": "definitions",
          "provenance": { "description": "test content" },
          "documents": [
            { "path": "places.json", "documentId": "places", "definitionKind": "place" },
            { "path": "links.json", "documentId": "links", "definitionKind": "travel-link" }
          ]
        }
        """;

    private static string Document(string documentId, string definitionKind, params string[] entries) =>
        $$"""
        { "documentId": "{{documentId}}", "definitionKind": "{{definitionKind}}", "entries": [ {{string.Join(",", entries)}} ] }
        """;

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")) &&
                Directory.Exists(Path.Combine(directory.FullName, "src")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find the repository root above the test assembly.");
    }

    /// <summary>A store that records what was written, which is how "nothing else writes" is observable.</summary>
    private sealed class RecordingSaveStore : ISessionSaveStore
    {
        internal List<SessionSave> Writes { get; } = [];

        public void Write(string slot, SessionSave save) => Writes.Add(save);

        public SessionSave? Read(string slot) => Writes.Count > 0 ? Writes[^1] : null;

        public void Dispose()
        {
        }
    }

    /// <summary>The cost contract as this suite states it: every transition costs two days and two portions.</summary>
    private sealed class TestCostRule : ITravelCostRule
    {
        public TravelCostQuote Quote(TransitionRequest request) =>
            TravelCostQuote.Payable(new TravelCost(
                new TravelTime(2, TravelTimeUnit.Days),
                new Provisions(2, ProvisionUnit.Portions)));
    }

    /// <summary>The larder policy this suite states: one portion a day, hunger when the larder is empty.</summary>
    private sealed class Rations(PartyEntity party, ConditionId weakness) : IProvisionDayRule
    {
        public Provisions DailyCharge(int members, int followers) => new(1, ProvisionUnit.Portions);

        public ActiveCondition? Consequence(int portionsAfter, int members, int followers)
        {
            if (portionsAfter >= 1)
            {
                foreach (PartyMember member in party.Members) member.Conditions.Clear(weakness);
                return null;
            }

            return new ActiveCondition(weakness, 1);
        }
    }

    /// <summary>A capacity rule that refuses everything, so a rule supplied at load is observable.</summary>
    private sealed class RefuseEverything : IInventoryCapacityRule
    {
        public PartyRefusal? Judge(IReadOnlyList<ItemInstance> items, ItemDefinitionId definition, int count) =>
            new("test-no-room", "the test's pack holds nothing more");
    }
}
