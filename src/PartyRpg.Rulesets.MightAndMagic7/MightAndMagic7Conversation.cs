using System.Globalization;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Conversation;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Promotion;
using PartyRpg.Kit.Quests;
using PartyRpg.Kit.Services;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// This game's answers about talking to somebody: who is here, what they say, what they can be asked
/// about, and what an answer does.
/// </summary>
/// <remarks>
/// <para>
/// <b>People come from the game's own tables.</b> A person is an NPC table row, carried into the packs as a
/// person entry with the name, portrait, two greetings, and topics the game's tables give them. A placement
/// names the people standing there, so a building holds the people the table places in it and a record in a
/// map holds whoever the map stands in the open. This class reads both and answers with the people present,
/// which is what makes a shopkeeper, a household, and a stranger on a road one kind of thing.
/// </para>
/// <para>
/// <b>The greeting is read, not remembered.</b> The game's greeting table carries two lines per person —
/// the first meeting and later ones — and this answers with the first while the party carries no record of
/// having met them and with the second afterwards. Meeting somebody is a party-wide effect rather than
/// state this class keeps, so it is saved with the party and read back through the same identity, exactly
/// as a guild membership is.
/// </para>
/// <para>
/// <b>A topic's availability is content's conditions judged against real state.</b> A topic the packs
/// carry states what must hold for it: a party-carried flag, the party's standing, a member's class or
/// race, the hour, or an errand the party has finished — each written by name, so a topic that waits for
/// something is read the same way whoever wrote it. The topic table's own requirement column is read as an
/// errand, because that is what the original gates those rows on. Every one of those is read from the owner that
/// holds it — the party's effects, its standing, its members, the one clock — and a topic whose condition
/// does not hold is withheld with the reason that condition states. Nothing here is remembered between
/// reads, so a topic appears and disappears with the state it depends on rather than with an invalidation.
/// </para>
/// <para>
/// <b>What has been said is part of the conversation.</b> A line the person has already answered in the
/// conversation the party is in is withheld for the rest of it — the words are in the transcript above —
/// and comes back the next time the party speaks with them, because a conversation's own memory is not
/// durable state. Taking a line also records on the party that it has been heard, which is the
/// party-carried half of an offer a later owner can read.
/// </para>
/// <para>
/// <b>A keeper offers the counter, and never the wares.</b> A person standing where a service placement
/// stands offers to step up to the counter as their own topic, and the offer hands off to the service
/// mechanism rather than describing what is behind it. What a counter sells, whether it is open, and what
/// it charges stay the service mechanism's business, so this adds no second copy of a shop.
/// </para>
/// <para>
/// <b>What this build does not do is run scripts.</b> The original picks a reply's text and its
/// consequences by running the map's event programs; nothing here executes them, so a person's replies are
/// the lines the topic table records and an answer says so rather than pretending the errand behind it
/// happened. That is the residue the conversation publishes beside what was said.
/// </para>
/// </remarks>
internal sealed class MightAndMagic7Conversation : IConversationRule
{
    /// <summary>The definition kind a person's own entry is declared under.</summary>
    internal const string PersonDefinitionKind = "person";

    /// <summary>The placement kind somebody standing at a position a map states stands under.</summary>
    internal const string PersonPlacementKind = "person";

    /// <summary>The target kind the interaction mechanism reaches a person as.</summary>
    internal const string PersonTargetKind = "person";

    /// <summary>The placement kind a counter stands in a place as.</summary>
    internal const string ServicePlacementKind = "service";

    /// <summary>The placement kind a household stands in a place as.</summary>
    internal const string ResidencePlacementKind = "residence";

    /// <summary>The field a placement names the people standing there under.</summary>
    internal const string PeopleField = "people";

    /// <summary>The identity a counter's own offer carries, which is one of the two topics this game adds.</summary>
    internal const string CounterTopicId = "counter";

    /// <summary>
    /// The identity this game's standing line carries.
    /// </summary>
    /// <remarks>
    /// The line is this game's own rather than a row of the shipped topic table: the original writes what a
    /// person says about the party's standing into the text of its NPC strings with a code that stands for
    /// the reputation category (MMExtension, <c>MMExtension.htm</c>, "Special Codes in Texts"), and no row
    /// of the shipped topic table carries it. Composing it here keeps the mechanism the same — a topic with
    /// conditions, judged on every read — because the table has nothing to gate.
    /// </remarks>
    internal const string StandingTopicId = "standing";

    /// <summary>What the standing line reads as in a list of things to bring up.</summary>
    internal const string StandingLabel = "What do people say about us?";









    /// <summary>The word a part of the day reads as when the clock says it is light.</summary>
    internal const string DayWord = "day";

    /// <summary>The word a part of the day reads as when the clock says it is dark.</summary>
    internal const string NightWord = "night";

    private readonly Dictionary<string, PersonFacts> _people;
    private readonly Dictionary<(string Place, string Placement), IReadOnlyList<string>> _present;
    private readonly MightAndMagic7Services? _services;
    private readonly MightAndMagic7Promotions? _promotions;
    private readonly MightAndMagic7Quests? _quests;
    private readonly Func<PartyQuests?>? _journal;
    private readonly IReadOnlyList<string> _notes;

    private MightAndMagic7Conversation(
        Dictionary<string, PersonFacts> people,
        Dictionary<(string, string), IReadOnlyList<string>> present,
        MightAndMagic7Services? services,
        MightAndMagic7Promotions? promotions,
        MightAndMagic7Quests? quests,
        Func<PartyQuests?>? journal,
        IReadOnlyList<string> notes)
    {
        _people = people;
        _present = present;
        _services = services;
        _promotions = promotions;
        _quests = quests;
        _journal = journal;
        _notes = notes;
    }

    /// <summary>How many ranks the people of this world can hand out, or zero when it states no ladder.</summary>
    internal int RankCount => _promotions?.RankCount ?? 0;

    /// <summary>What reading the people tables noticed, for the composition to report.</summary>
    internal IReadOnlyList<string> Notes => _notes;

    /// <summary>How many people the content carries.</summary>
    internal int PersonCount => _people.Count;

    /// <summary>How many placements name somebody.</summary>
    internal int PlacementCount => _present.Count;

    /// <summary>
    /// Reads every person the content declares and every placement that names one.
    /// </summary>
    /// <remarks>
    /// Everything is read and judged before anybody is spoken with, so a pack that names a person no entry
    /// describes, a person with no name, or a placement that holds nobody fails with every problem at once
    /// while the world is being built rather than at the moment a player walks up to somebody.
    /// </remarks>
    /// <param name="catalog">The validated content the product loaded, when it loaded any.</param>
    /// <param name="services">This game's service answers, which say whether a placement keeps a counter.</param>
    /// <param name="promotions">
    /// This game's ranks, when they were read: a person the ladder names as a giver offers the ranks they
    /// give, which is what makes a promotion something taken from somebody in the world rather than from a
    /// screen. Without a ladder nobody offers a rank, and the count says so.
    /// </param>
    /// <param name="quests">
    /// This game's errands, when they were read: a person the reading names as a giver offers the errands
    /// they give, which is what makes a quest something taken from somebody in the world rather than from a
    /// journal that fills itself. Without one nobody offers an errand, and the count says so.
    /// </param>
    /// <param name="journal">
    /// The quest owner of the party being played, or null while there is none: what the party has already
    /// been offered, taken, and finished is the owner's state, and a person offers an errand only while the
    /// party stands at the stage before it. It is read through a call for the same reason an award reads its
    /// owner that way — the mechanism is composed before the party that creates one exists.
    /// </param>
    /// <returns>This game's dialogue policy over that content, or null when no content was loaded.</returns>
    /// <exception cref="ContentValidationException">Content declares people that cannot be spoken with; every problem is named.</exception>
    internal static MightAndMagic7Conversation? Read(
        ContentCatalog? catalog,
        MightAndMagic7Services? services,
        MightAndMagic7Promotions? promotions = null,
        MightAndMagic7Quests? quests = null,
        Func<PartyQuests?>? journal = null)
    {
        if (catalog is null) return null;
        List<ContentValidationIssue> issues = [];
        List<string> notes = [];
        Dictionary<string, PersonFacts> people = [];

        foreach ((LoadedPack pack, ContentDocument document, ContentEntry entry) in catalog.Entries(PersonDefinitionKind))
        {
            void Defect(string code, string message) =>
                issues.Add(new ContentValidationIssue(code, message, pack.PackId, document.DocumentId));

            if (entry.Id.Length == 0)
            {
                Defect("person-identity-missing", "a person entry declares no id, so no placement could name them.");
                continue;
            }

            if (people.ContainsKey(entry.Id))
            {
                Defect("person-identity-reused", $"person '{entry.Id}' is declared more than once, so which person a placement names would be ambiguous.");
                continue;
            }

            string name = entry.GetString("name");
            if (name.Length == 0)
            {
                Defect("person-name-missing", $"person '{entry.Id}' declares no name, so nobody could be shown who is speaking.");
                continue;
            }

            List<TopicFacts> topics = [];
            foreach (System.Text.Json.JsonElement element in entry.GetArray("topics"))
            {
                string id = ContentEntry.ReadId(element, "id");
                string label = ContentEntry.ReadString(element, "label");
                string text = ContentEntry.ReadString(element, "text");
                if (id.Length == 0 || label.Length == 0 || text.Length == 0)
                {
                    Defect(
                        "person-topic-incomplete",
                        $"person '{entry.Id}' carries a topic without an identity, a label, or an answer, so choosing it would say nothing.");
                    continue;
                }

                int requires = ReadInt(element, "requires");
                List<ConversationCondition> conditions = [];
                foreach (System.Text.Json.JsonElement stated in Conditions(element))
                {
                    if (ReadKind(ContentEntry.ReadString(stated, "kind")) is not { } kind)
                    {
                        Defect(
                            "person-topic-condition-unknown",
                            $"topic '{id}' of person '{entry.Id}' waits for '{ContentEntry.ReadString(stated, "kind")}', which is not a kind of state this game reads.");
                        continue;
                    }

                    string named = ContentEntry.ReadId(stated, "name");
                    if (named.Length == 0)
                    {
                        Defect(
                            "person-topic-condition-unnamed",
                            $"topic '{id}' of person '{entry.Id}' states a condition that names nothing, so what it waits for cannot be read.");
                        continue;
                    }

                    conditions.Add(new ConversationCondition(
                        kind,
                        named,
                        Math.Max(1, ReadInt(stated, "amount", 1)),
                        ContentEntry.ReadString(stated, "label")));
                }

                // The topic table's own requirement column is an errand: the original gates those rows on a
                // quest bit, so it is read as the flag the quest owner will set rather than as a second
                // vocabulary. A pack that states its own conditions keeps them beside it.
                if (requires != 0)
                {
                    conditions.Add(new ConversationCondition(
                        ConversationConditionKind.Errand,
                        $"{MightAndMagic7Identities.ErrandFlagPrefix}{requires.ToString(CultureInfo.InvariantCulture)}",
                        1,
                        $"the errand the table calls {requires.ToString(CultureInfo.InvariantCulture)}"));
                }

                topics.Add(new TopicFacts(
                    id,
                    label,
                    text,
                    ReadInt(element, "textCount", 1),
                    conditions));
            }

            people[entry.Id] = new PersonFacts(
                entry.Id,
                name,
                entry.GetString("portrait"),
                entry.GetString("greeting"),
                entry.GetString("greetingAgain"),
                ReadInt(entry, "house"),
                ReadInt(entry, "dialogueEvents"),
                topics);
        }

        Dictionary<(string, string), IReadOnlyList<string>> present = ReadPlacements(catalog, people, issues);

        if (issues.Count > 0)
        {
            throw new ContentValidationException(
                $"This game's people cannot be spoken with: {issues[0].Message}",
                issues);
        }

        if (people.Count > 0)
        {
            notes.Add($"{people.Count} people are carried, {people.Count(person => person.Value.Topics.Count > 0)} of them with something to say.");
        }

        if (promotions is { } ladder)
        {
            int given = ladder.Ladder.Ranks.Count(rank => people.ContainsKey(rank.Giver));
            notes.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"{ladder.RankCount} ranks are carried, {given} of them given by somebody this world holds."));
        }

        if (quests is not null)
        {
            // What the party has taken is live state the session owns, so the errands a person offers are
            // counted from the rule and what the party stands with each of them is read through the owner
            // the session composes when its party exists — the same shape the award path reads its owner by.
            int errands = quests.ErrandCount;
            notes.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"{errands} errands are carried, over {quests.ErrandGiverCount} people who give them, at {quests.Definitions.Count} definitions in all."));
        }

        return new MightAndMagic7Conversation(people, present, services, promotions, quests, journal, notes);
    }

    /// <inheritdoc />
    public ConversationSubject? Describe(ConversationTargetRequest request)
    {
        string kind = request.Placement.Content.Kind;
        bool placed = string.Equals(kind, PersonPlacementKind, StringComparison.Ordinal);
        bool house = string.Equals(kind, ServicePlacementKind, StringComparison.Ordinal)
            || string.Equals(kind, ResidencePlacementKind, StringComparison.Ordinal);
        if (!placed && !house) return null;

        List<ConversationPerson> people = [];
        if (_present.TryGetValue((request.Place.Value, request.Placement.Content.Id), out IReadOnlyList<string>? named))
        {
            foreach (string id in named)
            {
                if (_people.TryGetValue(id, out PersonFacts? person)) people.Add(person.Who);
            }
        }

        // A building whose table names nobody still has somebody behind the door: the proprietor the
        // counter's own definition names, then the one its placement carries, and failing both the building
        // itself. A shop nobody could speak with would be a shop the party cannot enter, which is worse than
        // an unnamed keeper — and the name is read from the same answers the counter's own screen shows, so
        // the two cannot disagree about who keeps it.
        if (people.Count == 0 && house)
        {
            string building = request.Placement.Source.GetString("name");
            string proprietor = _services?.Describe(new ServiceTargetRequest(request.Place, request.Placement))?.Proprietor
                ?? request.Placement.Source.GetString("proprietor");
            string named2 = proprietor.Length > 0 && !MightAndMagic7Services.IsPlaceholderName(proprietor)
                ? proprietor
                : building;
            people.Add(new ConversationPerson(
                $"{MightAndMagic7Identities.KeeperIdPrefix}{request.Placement.Content.Id}",
                named2.Length > 0 ? named2 : "the keeper"));
        }

        // A placed person nobody stands for is a defect the pack cannot state: the placement is where
        // somebody is, so a placement with nobody would be a target that answers nothing.
        if (people.Count == 0) return null;
        return new ConversationSubject(request.Placement.Content.Id, people);
    }

    /// <inheritdoc />
    public ConversationAnswer Greeting(ConversationContext context)
    {
        PersonFacts? person = Facts(context.Speaker);
        if (person is null)
        {
            // Somebody the building table names rather than the NPC table: this game has no line of theirs
            // to read, so the keeper greets the party in this game's own words for what they keep.
            return new ConversationAnswer(
                KeeperGreeting(context),
                records: []);
        }

        bool met = Carries(context, $"{MightAndMagic7Identities.MetFlagPrefix}{person.Id}");
        string text = met && person.GreetingAgain.Length > 0
            ? person.GreetingAgain
            : person.Greeting.Length > 0
                ? person.Greeting
                : KeeperGreeting(context);

        return new ConversationAnswer(
            text,
            // The original scripts what follows a person's replies. Nothing here runs those programs, so a
            // reply that would hand over an item or set a task does not, and the party is told rather than
            // left to wonder why nothing changed.
            person.DialogueEvents > 0
                ? "Everything this person says from here is a line the game's own table records: the event programs the original runs behind a reply are not executed in this build, so a line that would hand something over or set a task does not."
                : string.Empty,
            records: [$"{MightAndMagic7Identities.MetFlagPrefix}{person.Id}"]);
    }

    /// <inheritdoc />
    public IReadOnlyList<ConversationOffer> Offers(ConversationContext context)
    {
        List<ConversationOffer> offers = [];
        if (Facts(context.Speaker) is { } person)
        {
            foreach (TopicFacts topic in person.Topics)
            {
                ConversationAvailability availability = ConversationAvailability.OnOffer;
                foreach (ConversationCondition condition in topic.Conditions)
                {
                    availability = Judge(condition, context);
                    if (!availability.IsOnOffer) break;
                }

                if (availability.IsOnOffer && Said(context, topic.Id))
                {
                    availability = ConversationAvailability.Withheld("they have already said this in this conversation");
                }

                offers.Add(new ConversationOffer(topic.Topic, availability));
            }

            // What the town makes of the party is one line every person the table describes can be asked
            // for, and it waits for the party to be worth an opinion: the condition is an ordinary standing
            // condition, judged by the same answer every other topic's conditions are judged by, so the line
            // appears when the standing is there and is withheld with the number it wants when it is not.
            offers.Add(StandingOffer(context));
        }

        if (Counter(context) is { } counter)
        {
            offers.Add(new ConversationOffer(
                new ConversationTopic(CounterTopicId, CounterLabel(counter.Kind.Value)),
                CounterOffer(counter, context)));
        }

        // A person the ladder names as a giver offers the ranks they give, in the ladder's own order. The
        // offer is composed here rather than read from the shipped topic table because the original answers
        // those rows with event programs this build does not run: what the table states is who gives which
        // rank, and this is that fact turned into something a party can take.
        if (_promotions is { } ladder)
        {
            foreach (PromotionRank rank in ladder.Ladder.GivenBy(context.Speaker))
            {
                string id = $"{MightAndMagic7Identities.PromotionTopicPrefix}{rank.Id}";
                ConversationAvailability availability = RankOffer(rank, context);
                if (availability.IsOnOffer && Said(context, id))
                {
                    availability = ConversationAvailability.Withheld("they have already said this in this conversation");
                }

                offers.Add(new ConversationOffer(new ConversationTopic(id, rank.To.Value), availability));
            }
        }

        AddErrands(offers, context);

        return offers;
    }

    /// <summary>
    /// What a person has to say about errands: the one they give, and the board a town hall keeps.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>An errand is offered in the stage the party stands at.</b> A person who gives an errand the party
    /// has not heard offers it; once it has been heard they offer to take it on; once taken, they ask after
    /// it. Every stage is a topic rather than a single line that changes its meaning, because what a player
    /// does with each is different and a topic is what a choice is made of.
    /// </para>
    /// <para>
    /// <b>A town hall's board is the one errand nobody authors.</b> A hall's own encounter row names the
    /// beasts that live around it and this game's clock says which month it is, so the keeper of a hall whose
    /// place posts a bounty offers the hunt that month's row states. The errand is named by its own identity
    /// rather than kept anywhere, so a party that leaves and returns — or saves and resumes — meets the same
    /// contract the notice advertises.
    /// </para>
    /// <para>
    /// The offer is withheld rather than absent when the party does not meet what the errand asks: a player
    /// who can see the errand and read why not is better served than one who cannot see it at all, which is
    /// the same rule a rank's offer follows.
    /// </para>
    /// </remarks>
    private void AddErrands(List<ConversationOffer> offers, ConversationContext context)
    {
        if (_quests is not { } quests) return;
        PartyQuests? journal = _journal?.Invoke();

        foreach (QuestDefinition definition in quests.GivenBy(context.Speaker))
        {
            QuestInstance? instance = journal?.Instance(definition.Id);
            if (instance is null)
            {
                offers.Add(Errand(definition, $"{MightAndMagic7Identities.QuestTopicPrefix}{definition.Id}", definition.Name, context));
                continue;
            }

            if (instance.Stage == QuestStage.Offered)
            {
                offers.Add(new ConversationOffer(
                    new ConversationTopic($"{MightAndMagic7Identities.AcceptTopicPrefix}{definition.Id}", definition.Name),
                    Said(context, $"{MightAndMagic7Identities.AcceptTopicPrefix}{definition.Id}")
                        ? ConversationAvailability.Withheld("they have already said this in this conversation")
                        : ConversationAvailability.OnOffer));
                continue;
            }

            if (instance.Stage == QuestStage.Accepted)
            {
                offers.Add(new ConversationOffer(
                    new ConversationTopic($"{MightAndMagic7Identities.TurnInTopicPrefix}{definition.Id}", definition.Name),
                    ConversationAvailability.OnOffer));
            }
        }

        if (Counter(context) is not { } hall || context.Clock is not { } now) return;
        if (!string.Equals(hall.Kind.Value, MightAndMagic7ServiceKinds.TownHall, StringComparison.Ordinal)) return;

        string bounty = quests.BountyQuest(context.Placement!.Content.Id, now.Now);
        if (bounty.Length == 0) return;
        if (journal?.Instance(new QuestId(bounty)) is not null) return;
        if (quests.Definition(new QuestId(bounty)) is not { } hunt) return;
        offers.Add(Errand(hunt, $"{MightAndMagic7Identities.QuestTopicPrefix}{hunt.Id}", hunt.Name, context));
    }

    /// <summary>
    /// What a person says about the party's standing, as the one condition it waits for leaves it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The condition is a standing condition in the vocabulary the whole mechanism judges, so nothing here
    /// compares anything: <see cref="Holds"/> answers it and <see cref="Reason"/> says what it wanted, which
    /// is the same pair of answers a shipped topic's own conditions get. Where the band begins is this game's
    /// table, so a threshold moved there moves the line with it.
    /// </para>
    /// <para>
    /// A person who has already answered it in this conversation withholds it for the same reason every
    /// other topic does, so a party that asks twice is told so rather than hearing the same line again.
    /// </para>
    /// </remarks>
    private static ConversationOffer StandingOffer(ConversationContext context)
    {
        ConversationCondition condition = new(
            ConversationConditionKind.Reputation,
            "reputation",
            MightAndMagic7Standing.WellRegarded,
            "the party's standing");
        ConversationTopic topic = new(StandingTopicId, StandingLabel, [condition]);
        foreach (ConversationCondition stated in topic.Conditions)
        {
            if (Holds(stated, context.Party, context.Clock)) continue;
            return new ConversationOffer(topic, ConversationAvailability.Withheld(Reason(stated, context)));
        }

        return new ConversationOffer(
            topic,
            Said(context, StandingTopicId)
                ? ConversationAvailability.Withheld("they have already said this in this conversation")
                : ConversationAvailability.OnOffer);
    }

    /// <summary>One errand's own offer, as its stated conditions leave it.</summary>
    private static ConversationOffer Errand(
        QuestDefinition definition,
        string id,
        string label,
        ConversationContext context)
    {
        foreach (ConversationCondition condition in definition.OfferConditions)
        {
            if (Holds(condition, context.Party, context.Clock)) continue;
            return new ConversationOffer(
                new ConversationTopic(id, label),
                ConversationAvailability.Withheld(Reason(condition, context)));
        }

        return new ConversationOffer(new ConversationTopic(id, label), ConversationAvailability.OnOffer);
    }

    /// <summary>The errand a topic names and which act it asks for, or null when the topic names neither.</summary>
    /// <remarks>
    /// The three prefixes each name one operation of the quest owner: hearing an errand, agreeing to one,
    /// and handing a finished one back. A topic whose identity names no errand this game states answers
    /// nothing, which is the same shape a rank's offer takes when its identity names no rank.
    /// </remarks>
    private static (QuestDefinition Definition, HandoffOwner Handoff)? ErrandTopic(string topic, MightAndMagic7Quests quests)
    {
        (string Prefix, HandoffOwner Handoff)[] acts =
        [
            (MightAndMagic7Identities.TurnInTopicPrefix, HandoffOwner.ErrandTurnIn),
            (MightAndMagic7Identities.AcceptTopicPrefix, HandoffOwner.ErrandAccept),
            (MightAndMagic7Identities.QuestTopicPrefix, HandoffOwner.ErrandOffer),
        ];

        foreach ((string prefix, HandoffOwner handoff) in acts)
        {
            if (!topic.StartsWith(prefix, StringComparison.Ordinal)) continue;
            string id = topic[prefix.Length..];
            return quests.Definition(new QuestId(id)) is { } definition ? (definition, handoff) : null;
        }

        return null;
    }

    /// <summary>What a person says when an errand is offered, agreed to, or asked after.</summary>
    /// <remarks>
    /// The offer says the shipped errand's own words, which is what a player hears; the agreement and the
    /// asking are this game's own lines, because the original answers them from event programs this build
    /// does not run. Where the shipped words say something this game does not judge, the asking line says so
    /// rather than leaving the party to wonder what else it owes.
    /// </remarks>
    private static string ErrandWords(QuestDefinition definition, HandoffOwner handoff)
    {
        if (handoff == HandoffOwner.ErrandTurnIn)
        {
            return definition.Residue.Length > 0
                ? $"'{definition.Name}? {definition.Residue}'"
                : $"'Well? Is {definition.Name} done?'";
        }

        if (handoff == HandoffOwner.ErrandAccept)
        {
            return $"'Then it is agreed: {definition.Name} is yours to do.'";
        }

        return definition.Note.Length > 0
            ? $"'{definition.Note}'"
            : $"'There is something I would have you do: {definition.Name}.'";
    }

    /// <summary>What one rank's own offer makes of itself right now, read from the party's own state.</summary>
    /// <remarks>
    /// A rank is offered by the person who gives it whether or not anybody can take it, and what the party
    /// brings to it is judged when it is taken: the offer's own answer is the one thing a screen must not
    /// have to work out — whether the party holds the class the rank promotes from, and at the rank it
    /// continues from. Everything else the rank asks for is listed by the refusal, which is where a player
    /// reads it.
    /// </remarks>
    private static ConversationAvailability RankOffer(PromotionRank rank, ConversationContext context)
    {
        if (context.Party is not { } party)
        {
            return ConversationAvailability.Withheld($"the rank of {rank.To} is given to a {rank.From}, and this world holds nobody to give it to");
        }

        List<string> held = [];
        foreach (PartyMember member in party.Members)
        {
            if (!string.Equals(member.Profile.Class.Value, rank.From.Value, StringComparison.Ordinal)) continue;
            if (member.Progression.ClassRank == rank.Rank - 1) return ConversationAvailability.OnOffer;
            held.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"{member.Profile.Name} stands at rank {member.Progression.ClassRank}"));
        }

        return held.Count == 0
            ? ConversationAvailability.Withheld($"nobody in the party is a {rank.From}, and the rank of {rank.To} is given to one")
            : ConversationAvailability.Withheld($"{string.Join(" and ", held)} of the {rank.From} ladder, and the rank of {rank.To} continues from rank {rank.Rank - 1}");
    }

    /// <inheritdoc />
    public ConversationAnswer Take(ConversationTopic topic, ConversationContext context)
    {
        // What the town makes of the party is answered from the party's own standing rather than from a
        // table: the person repeats the band the world has the party in, in this game's words for it, and
        // what is said is recorded on the party the same way any other line is.
        if (string.Equals(topic.Id, StandingTopicId, StringComparison.Ordinal) && context.Party is { } party)
        {
            return new ConversationAnswer(
                MightAndMagic7Standing.Words(party),
                records: [$"{MightAndMagic7Identities.HeardFlagPrefix}{StandingTopicId}"]);
        }

        if (string.Equals(topic.Id, CounterTopicId, StringComparison.Ordinal)
            && Counter(context) is { } counter)
        {
            // The offer is the counter, not what stands on its shelves: the handoff names the service
            // mechanism, which asks its own questions and refuses in its own vocabulary when it is shut.
            return new ConversationAnswer(
                $"'{counter.Name}' — the party steps up to the counter.",
                handoff: new ConversationHandoff(HandoffOwner.Counter, counter.Id.Value));
        }

        // A rank this person gives is handed to the owner that owns ranks rather than answered here, exactly
        // as a counter is handed to the service mechanism: whether the party meets the rank's requirements is
        // judged there, once, and the answer is what both the refusal and the panel read.
        if (_promotions is { } ladder && topic.Id.StartsWith(MightAndMagic7Identities.PromotionTopicPrefix, StringComparison.Ordinal))
        {
            string id = topic.Id[MightAndMagic7Identities.PromotionTopicPrefix.Length..];
            foreach (PromotionRank rank in ladder.Ladder.GivenBy(context.Speaker))
            {
                if (!string.Equals(rank.Id, id, StringComparison.Ordinal)) continue;
                return new ConversationAnswer(
                    rank.Words.Length > 0 ? rank.Words : $"'{rank.To}? Then let us see whether you have what it asks for.'",
                    handoff: new ConversationHandoff(HandoffOwner.Rank, rank.Id));
            }

            return new ConversationAnswer(
                "There is nothing this person grants under that name.",
                $"This game's ladder carries no rank '{id}', so the offer names a rank nothing can give.");
        }

        // An errand this person gives, or the board the counter they keep posts, is handed to the owner that
        // owns quest state exactly as a rank is handed to the owner that owns ranks: whether the party meets
        // what it asks is judged there, once, and what this says is only the line that goes with it.
        if (_quests is { } quests && ErrandTopic(topic.Id, quests) is { } errand)
        {
            return new ConversationAnswer(
                ErrandWords(errand.Definition, errand.Handoff),
                handoff: new ConversationHandoff(errand.Handoff, errand.Definition.Id.Value));
        }

        if (Facts(context.Speaker) is { } person)
        {
            foreach (TopicFacts candidate in person.Topics)
            {
                if (!string.Equals(candidate.Id, topic.Id, StringComparison.Ordinal)) continue;
                return new ConversationAnswer(
                    candidate.Text,
                    candidate.TextCount > 1
                        ? $"The game's table records {candidate.TextCount} versions of this answer and the original chooses between them by the state of its event programs, which this build does not run; this is the first."
                        : string.Empty,
                    records: [$"{MightAndMagic7Identities.HeardFlagPrefix}{candidate.Id}"]);
            }
        }

        return new ConversationAnswer(
            "There is nothing this person says to that.",
            "The topic is not one this game's content carries an answer for, so choosing it said nothing.");
    }

    /// <summary>What a topic's own condition makes of it, read from the owner that holds the state.</summary>
    /// <remarks>
    /// Every kind is answered from something the world actually carries: a party-carried flag is a
    /// party-wide effect, the standing is the party's own reputation, a class or a race is a member's own
    /// profile, the hour is the session's one clock, and an errand is a flag the quest owner will set.
    /// Nothing is satisfied by an invented fact, and a kind whose owner is absent says so.
    /// </remarks>
    private static ConversationAvailability Judge(ConversationCondition condition, ConversationContext context) =>
        Holds(condition, context.Party, context.Clock)
            ? ConversationAvailability.OnOffer
            : ConversationAvailability.Withheld(Reason(condition, context));

    /// <summary>
    /// Whether one stated condition holds, which is the whole meaning of this game's condition vocabulary.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the one reading of what a condition <em>means</em>, and it is deliberately separated from the
    /// sentence a withheld topic gives: a quest states its offer and completion conditions in the same
    /// vocabulary, and it is judged by the same answer through <c>IQuestRule</c>, so a topic and a quest that
    /// wait for the same thing cannot disagree about whether it holds.
    /// </para>
    /// <para>
    /// Nothing is satisfied by an invented fact. A party-carried flag and an errand are effects the party
    /// actually holds, the standing is the party's own reputation, a class or a race is a member's own
    /// profile, and the hour is the session's one clock; a condition whose owner is absent is unmet rather
    /// than assumed.
    /// </para>
    /// </remarks>
    /// <param name="condition">The condition as content or a quest states it.</param>
    /// <param name="party">The party it is read against, or null when the world holds none.</param>
    /// <param name="clock">The session's one clock, or null when its ruleset composed none.</param>
    /// <returns>Whether it holds.</returns>
    internal static bool Holds(ConversationCondition condition, PartyEntity? party, GameClock? clock) =>
        condition.Kind switch
        {
            ConversationConditionKind.Flag or ConversationConditionKind.Errand =>
                party is { } carrier && carrier.Records.Has(condition.Name),
            ConversationConditionKind.Reputation => (party?.Reputation.Reputation ?? 0) >= condition.Amount,
            ConversationConditionKind.Class => Anyone(party, member => string.Equals(member.Profile.Class.Value, condition.Name, StringComparison.Ordinal)),
            ConversationConditionKind.Race => Anyone(party, member => string.Equals(member.Profile.Race.Value, condition.Name, StringComparison.Ordinal)),
            ConversationConditionKind.Hour => clock is { } now && PartOfDay(now) == condition.Name,
            _ => false,
        };

    /// <summary>Why a condition is not met, in the words a person reads beside the withheld subject.</summary>
    private static string Reason(ConversationCondition condition, ConversationContext context) =>
        condition.Kind switch
        {
            ConversationConditionKind.Flag => $"the party does not carry {condition.Label}",
            ConversationConditionKind.Reputation => context.Party is null
                ? "this world holds no party whose standing could be read"
                : string.Create(
                    CultureInfo.InvariantCulture,
                    $"the party's standing is {context.Party.Reputation.Reputation} and this needs {condition.Amount}"),
            ConversationConditionKind.Class => $"nobody in the party is {condition.Label}",
            ConversationConditionKind.Race => $"nobody in the party is {condition.Label}",
            ConversationConditionKind.Hour => context.Clock is null
                ? "nothing in this world keeps the hour"
                : $"the clock stands at {context.Clock.Now.Hour:00}:00",
            ConversationConditionKind.Errand => $"{condition.Label} is not finished",
            _ => $"nothing in this build knows what '{condition.Kind}' asks for",
        };

    /// <summary>Whether a counter the person keeps invites the party in at this hour.</summary>
    /// <remarks>
    /// The window is the counter's own hours, which is the same window the place's schedule reads: a shop
    /// shut for the night does not offer to step inside, and says which hours it keeps rather than leaving
    /// the party to guess. The offer is withheld rather than removed, so the reason is what a player sees.
    /// </remarks>
    private static ConversationAvailability CounterOffer(ServiceDefinition counter, ConversationContext context)
    {
        if (counter.Hours is not { } hours) return ConversationAvailability.OnOffer;
        if (context.Clock is not { } clock)
        {
            return ConversationAvailability.Withheld($"it keeps {hours} and this world keeps no clock");
        }

        return hours.IsOpenAt(clock.Now)
            ? ConversationAvailability.OnOffer
            : ConversationAvailability.Withheld($"it is shut: it keeps {hours} and the clock stands at {clock.Now.Hour:00}:00");
    }

    /// <summary>Whether one of the party's members satisfies something.</summary>
    private static bool Anyone(ConversationContext context, Func<PartyMember, bool> test) => Anyone(context.Party, test);

    /// <summary>Whether one of a party's members satisfies something, or false when there is no party.</summary>
    private static bool Anyone(PartyEntity? party, Func<PartyMember, bool> test)
    {
        if (party is null) return false;
        foreach (PartyMember member in party.Members)
        {
            if (test(member)) return true;
        }

        return false;
    }

    /// <summary>Whether the party carries a named flag as one of its own party-wide effects.</summary>
    private static bool Carries(ConversationContext context, string flag) =>
        context.Party is { } party && party.Records.Has(flag);

    /// <summary>Which part of the day the session's one clock stands in, or empty when it keeps none.</summary>
    private static string PartOfDay(ConversationContext context) =>
        context.Clock is not { } clock ? string.Empty : PartOfDay(clock);

    /// <summary>Which part of the day one clock stands in, as this game's word for it.</summary>
    private static string PartOfDay(GameClock clock) => clock.IsDaylight ? DayWord : NightWord;

    /// <summary>Whether the person has already answered a topic in this conversation.</summary>
    private static bool Said(ConversationContext context, string topic)
    {
        foreach (ConversationLine line in context.Said)
        {
            if (string.Equals(line.Topic, topic, StringComparison.Ordinal)) return true;
        }

        return false;
    }

    /// <summary>The counter a placement keeps, or null when it keeps none.</summary>
    private ServiceDefinition? Counter(ConversationContext context)
    {
        if (_services is null) return null;
        return !string.Equals(context.Placement.Content.Kind, ServicePlacementKind, StringComparison.Ordinal)
            ? null
            : _services.Describe(new ServiceTargetRequest(context.Place, context.Placement));
    }

    /// <summary>What a keeper says when this game has no line of theirs to read.</summary>
    /// <remarks>
    /// The shipped tables carry greetings for the NPCs they place, not for the keepers the building table
    /// names, and the original draws those lines from strings inside its executable rather than from data.
    /// These are therefore this game's own words for what a keeper is, one per kind of building, which is
    /// the same place the kinds' operations and stock are answered from.
    /// </remarks>
    private static string KeeperGreeting(ConversationContext context) =>
        context.Placement.Content.Kind switch
        {
            ServicePlacementKind => $"'Welcome. {CounterGreeting(Kind(context))}'",
            ResidencePlacementKind => "'Yes? What brings you to my door?'",
            _ => "'Well met, travellers.'",
        };

    /// <summary>What kind of building a counter is, as its own placement states it.</summary>
    private static string Kind(ConversationContext context)
    {
        string fixture = context.Placement.Source.GetString("fixture");
        return fixture.Length > 0 ? fixture : context.Placement.Source.GetString("kind");
    }

    /// <summary>What a keeper of one kind of building says when the party walks up.</summary>
    private static string CounterGreeting(string kind) => kind switch
    {
        MightAndMagic7ServiceKinds.Tavern => "A room, a drink, or a game of Arcomage?",
        MightAndMagic7ServiceKinds.Temple => "Do you need healing, or a condition lifted?",
        MightAndMagic7ServiceKinds.Training => "You look like you could use some training.",
        MightAndMagic7ServiceKinds.Bank => "We keep what you would rather not carry.",
        MightAndMagic7ServiceKinds.TownHall => "The hall has work for capable hands.",
        MightAndMagic7ServiceKinds.Stables => "The coach leaves when it is full.",
        MightAndMagic7ServiceKinds.Boats => "The tide turns for no one; book your passage.",
        MightAndMagic7ServiceKinds.Alchemist => "Potions, reagents, and a steady hand.",
        MightAndMagic7ServiceKinds.WeaponShop => "Steel for sale, and coin for your old arms.",
        MightAndMagic7ServiceKinds.ArmorShop => "Nothing turns a blade like good plate.",
        MightAndMagic7ServiceKinds.MagicShop => "Wands, rings, and things best left wrapped.",
        _ => MightAndMagic7ServiceKinds.IsGuild(kind)
            ? "Our doors open wider for members."
            : $"What can I do for you?",
    };

    /// <summary>How a counter's own offer reads in a list of things to bring up.</summary>
    private static string CounterLabel(string kind) => kind switch
    {
        MightAndMagic7ServiceKinds.Tavern => "Ask about a room, a drink, or a game",
        MightAndMagic7ServiceKinds.Temple => "Ask for healing",
        MightAndMagic7ServiceKinds.Training => "Ask to train",
        MightAndMagic7ServiceKinds.Bank => "Ask about the bank",
        MightAndMagic7ServiceKinds.TownHall => "Ask what the hall wants done",
        MightAndMagic7ServiceKinds.Stables => "Ask about the coach",
        MightAndMagic7ServiceKinds.Boats => "Ask about the passage",
        MightAndMagic7ServiceKinds.Alchemist => "Ask to see the reagents",
        MightAndMagic7ServiceKinds.WeaponShop or MightAndMagic7ServiceKinds.ArmorShop or MightAndMagic7ServiceKinds.MagicShop => "Ask to see the wares",
        _ => MightAndMagic7ServiceKinds.IsGuild(kind) ? "Ask about the guild" : "Step up to the counter",
    };

    /// <summary>One person's own facts by identity, or null when nobody present has it.</summary>
    private PersonFacts? Facts(string person) => _people.TryGetValue(person, out PersonFacts? facts) ? facts : null;

    /// <summary>Reads every placement that names the people standing there.</summary>
    private static Dictionary<(string, string), IReadOnlyList<string>> ReadPlacements(
        ContentCatalog catalog,
        Dictionary<string, PersonFacts> people,
        List<ContentValidationIssue> issues)
    {
        Dictionary<(string, string), IReadOnlyList<string>> present = [];
        foreach ((LoadedPack pack, ContentDocument document, ContentEntry entry) in catalog.Entries(PlaceGraphLoader.PlaceDefinitionKind))
        {
            foreach (System.Text.Json.JsonElement placement in entry.GetArray(PlacePopulationContent.PlacementsField))
            {
                string kind = ContentEntry.ReadString(placement, PlacePopulationContent.KindField);
                if (!string.Equals(kind, PersonPlacementKind, StringComparison.Ordinal)
                    && !string.Equals(kind, ServicePlacementKind, StringComparison.Ordinal)
                    && !string.Equals(kind, ResidencePlacementKind, StringComparison.Ordinal))
                {
                    continue;
                }

                string id = ContentEntry.ReadId(placement, PlacePopulationContent.IdField);
                List<string> ids = [];
                foreach (string named in PeopleNamed(placement))
                {
                    if (!people.ContainsKey(named))
                    {
                        issues.Add(new ContentValidationIssue(
                            "person-placement-unknown",
                            $"place '{entry.Id}' places a person named '{named}', whom no person entry describes.",
                            pack.PackId,
                            document.DocumentId));
                        continue;
                    }

                    ids.Add(named);
                }

                if (ids.Count == 0 && string.Equals(kind, PersonPlacementKind, StringComparison.Ordinal))
                {
                    issues.Add(new ContentValidationIssue(
                        "person-placement-empty",
                        $"place '{entry.Id}' places somebody at '{id}' and names nobody, so the party would face a person with nothing to say.",
                        pack.PackId,
                        document.DocumentId));
                    continue;
                }

                if (ids.Count > 0) present[(entry.Id, id)] = ids;
            }
        }

        return present;
    }

    /// <summary>The conditions a topic states, which are the state it waits for.</summary>
    /// <remarks>
    /// The vocabulary is this game's own and is read from the pack by name, so content written by hand and
    /// content an import wrote state what a topic waits for in the same words. A kind nothing reads is a
    /// defect of the pack rather than a topic that quietly never appears.
    /// </remarks>
    private static IReadOnlyList<System.Text.Json.JsonElement> Conditions(System.Text.Json.JsonElement topic) =>
        topic.ValueKind == System.Text.Json.JsonValueKind.Object &&
        topic.TryGetProperty("conditions", out System.Text.Json.JsonElement value) &&
        value.ValueKind == System.Text.Json.JsonValueKind.Array
            ? [.. value.EnumerateArray()]
            : [];

    /// <summary>The kind of state a condition's own word names, or null when nothing reads it.</summary>
    /// <summary>The kind of state a content word names, as an authored quest's own conditions read it.</summary>
    /// <param name="kind">The word content wrote.</param>
    /// <returns>The kind, or null when this game reads no state of that name.</returns>
    internal static ConversationConditionKind? ConditionKind(string kind) => ReadKind(kind);

    private static ConversationConditionKind? ReadKind(string kind) => kind.ToLowerInvariant() switch
    {
        "flag" => ConversationConditionKind.Flag,
        "reputation" => ConversationConditionKind.Reputation,
        "class" => ConversationConditionKind.Class,
        "race" => ConversationConditionKind.Race,
        "hour" => ConversationConditionKind.Hour,
        "errand" => ConversationConditionKind.Errand,
        _ => null,
    };

    /// <summary>The people a placement names, whether content wrote them as identities or as objects.</summary>
    /// <remarks>
    /// An identity is an identity: a pack may write the people standing in a place as bare strings or as
    /// objects carrying an id, and a reader that understood only one of the two would report the other as a
    /// placement with nobody in it.
    /// </remarks>
    private static IReadOnlyList<string> PeopleNamed(System.Text.Json.JsonElement placement)
    {
        if (placement.ValueKind != System.Text.Json.JsonValueKind.Object ||
            !placement.TryGetProperty(PeopleField, out System.Text.Json.JsonElement value) ||
            value.ValueKind != System.Text.Json.JsonValueKind.Array)
        {
            return [];
        }

        List<string> ids = [];
        foreach (System.Text.Json.JsonElement item in value.EnumerateArray())
        {
            string id = item.ValueKind switch
            {
                System.Text.Json.JsonValueKind.String => item.GetString() ?? string.Empty,
                System.Text.Json.JsonValueKind.Object => ContentEntry.ReadId(item, "id"),
                _ => string.Empty,
            };
            if (id.Length > 0) ids.Add(id);
        }

        return ids;
    }

    private static int ReadInt(ContentEntry entry, string property, int fallback = 0) =>
        entry.GetDouble(property) is { } value && value >= int.MinValue && value <= int.MaxValue ? (int)value : fallback;

    private static int ReadInt(System.Text.Json.JsonElement element, string property, int fallback = 0)
    {
        if (element.ValueKind != System.Text.Json.JsonValueKind.Object) return fallback;
        if (!element.TryGetProperty(property, out System.Text.Json.JsonElement value)) return fallback;
        return value.ValueKind == System.Text.Json.JsonValueKind.Number && value.TryGetInt32(out int number) ? number : fallback;
    }

    /// <summary>One person's facts, as this game reads them from content.</summary>
    private sealed record PersonFacts(
        string Id,
        string Name,
        string Portrait,
        string Greeting,
        string GreetingAgain,
        int House,
        int DialogueEvents,
        IReadOnlyList<TopicFacts> Topics)
    {
        /// <summary>How the person reads in a conversation.</summary>
        public ConversationPerson Who => new(Id, Name, Portrait);
    }

    /// <summary>One thing a person can be asked about, as this game reads it from content.</summary>
    private sealed record TopicFacts(
        string Id,
        string Label,
        string Text,
        int TextCount,
        IReadOnlyList<ConversationCondition> Conditions)
    {
        /// <summary>The topic as the conversation mechanism holds it.</summary>
        public ConversationTopic Topic => new(Id, Label, Conditions);
    }
}
