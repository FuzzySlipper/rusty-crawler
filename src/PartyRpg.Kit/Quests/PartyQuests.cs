using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Conversation;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Progression;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;

namespace PartyRpg.Kit.Quests;

/// <summary>
/// The one owner of what a party has been offered, what it has taken, and what it has finished.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every quest fact is written here and nowhere else.</b> An errand is offered, taken, progressed, and
/// turned in through this owner; a quest's stage and the progress recorded against its objectives are
/// reachable only from inside it, so a second writer is a compile error rather than a review finding. What
/// another owner already reports — an item carried, a record held, a person met — is read from that owner at
/// the moment it is asked about and never copied, which is what keeps one fact in one place.
/// </para>
/// <para>
/// <b>The ruleset supplies the policy; this supplies the mechanism.</b> Which quests exist, what a creature's
/// death counts for, and what a stated condition means are <see cref="IQuestRule"/>'s answers. Which owner
/// each reward reaches is not policy at all: experience arrives at the progression owner's one award entry,
/// coin through the party's one ledger, items through the party's own acquisition path, and records on the
/// party's effects — the same four owners every other source of those things already reaches.
/// </para>
/// <para>
/// <b>A turn-in is judged whole before anything moves.</b> The giver, the objectives, the completion
/// conditions, and the presence of every owner a reward would reach are settled first, so a turn-in that is
/// refused leaves the party — and the quest — exactly where they stood, and a refusal names the objective
/// that is unmet rather than only that something was.
/// </para>
/// <para>
/// <b>An instance belongs to the party and outlives the place.</b> Nothing here is keyed by where the party
/// stands, so a quest taken in one town is the same instance when it is finished in another, and the only
/// place an instance records is the one the offer was taken in — provenance for a journal and a check at
/// load, never a lookup.
/// </para>
/// </remarks>
public sealed class PartyQuests : IItemRetentionRule
{
    /// <summary>What this names as the source of an award a finished quest earned.</summary>
    public const string QuestSource = "quest";

    private readonly IQuestRule _rule;
    private readonly PartyEntity _party;
    private readonly PartyResourceLedger? _ledger;
    private readonly PartyProgression? _progression;
    private readonly GameClock? _clock;
    private readonly List<QuestInstance> _instances = [];

    /// <summary>Creates the owner over the party whose journal it keeps.</summary>
    /// <param name="rule">This game's quests and what its objectives and conditions mean.</param>
    /// <param name="party">The party whose instances these are and whose owners a reward reaches.</param>
    /// <param name="ledger">The party's one settlement path, which a quest's coin is credited through.</param>
    /// <param name="progression">
    /// The owner every award of experience arrives at. Without one a quest that pays experience refuses the
    /// turn-in by name, because paying it anywhere else would be a second award entry.
    /// </param>
    /// <param name="clock">
    /// The session's one clock, which a condition about the hour is judged against. Without one a condition
    /// about the time of day cannot be known to hold, and the rule says so.
    /// </param>
    /// <param name="save">What a save recorded, or null for a party that has taken no quests.</param>
    /// <exception cref="ArgumentNullException">The rule or the party is missing.</exception>
    public PartyQuests(
        IQuestRule rule,
        PartyEntity party,
        PartyResourceLedger? ledger = null,
        PartyProgression? progression = null,
        GameClock? clock = null,
        QuestSave? save = null)
    {
        _rule = rule ?? throw new ArgumentNullException(nameof(rule));
        _party = party ?? throw new ArgumentNullException(nameof(party));
        _ledger = ledger;
        _progression = progression;
        _clock = clock;
        _party.RetainItemsWith(this);

        if (save is null) return;
        foreach (QuestInstanceSave recorded in save.Instances)
        {
            _instances.Add(QuestInstanceSave.Read(recorded));
        }
    }

    /// <summary>This game's quests and the meanings of their objectives and conditions.</summary>
    public IQuestRule Rule => _rule;

    /// <summary>The party whose journal this keeps.</summary>
    public PartyEntity Party => _party;

    /// <summary>
    /// The change stamp this owner took when the errands it holds or the last outcome it reports last changed, or
    /// when it was made: a reader that kept what it built beside it reads the owner again only when it has moved
    /// (<see cref="ChangeStamp"/>).
    /// </summary>
    public long Stamp { get; private set; } = ChangeStamp.Next();

    /// <summary>What the last operation did, or null before any has been asked for.</summary>
    public QuestResult? Last { get; private set; }

    /// <summary>Every instance this party holds, in the order they were first recorded.</summary>
    public IReadOnlyList<QuestInstance> Instances => _instances;

    /// <summary>
    /// Every quest this party stands with, in the order the instances were first recorded.
    /// </summary>
    /// <remarks>
    /// This is the journal: what the party has been offered, what it has taken, and what it has finished,
    /// each read against the owners that report its progress at the moment it is asked for. A quest this
    /// game no longer states is left out rather than guessed at, and a load is where that is refused by name.
    /// </remarks>
    public IReadOnlyList<QuestReading> Journal
    {
        get
        {
            List<QuestReading> journal = [];
            foreach (QuestInstance instance in _instances)
            {
                if (Read(instance.Quest) is { } reading) journal.Add(reading);
            }

            return journal;
        }
    }

    /// <summary>One instance by the quest it is of, or null when this party holds none.</summary>
    /// <param name="quest">The quest's identity.</param>
    /// <returns>The instance, or null.</returns>
    public QuestInstance? Instance(QuestId quest)
    {
        foreach (QuestInstance instance in _instances)
        {
            if (instance.Quest == quest) return instance;
        }

        return null;
    }

    /// <summary>One quest as this party stands with it, or null when it holds no instance of one.</summary>
    /// <remarks>
    /// A definition the game no longer states reads as null even when an instance names it, because a reading
    /// is composed of both halves and half a reading is not one. What a load does about such an instance is
    /// stated where the document is judged: it is refused by name rather than dropped silently.
    /// </remarks>
    /// <param name="quest">The quest's identity.</param>
    /// <returns>The reading, or null.</returns>
    public QuestReading? Read(QuestId quest)
    {
        if (Instance(quest) is not { } instance) return null;
        if (_rule.Definition(quest) is not { } definition) return null;
        return Read(definition, instance);
    }

    /// <summary>Records that a person stated an errand to the party.</summary>
    /// <remarks>
    /// <para>
    /// The offer is a fact about the party rather than about the conversation, which is why it is recorded
    /// here: a journal that showed only what the party agreed to would forget every errand it heard and
    /// walked away from. Nothing about the reward is judged here — an offer is not a payment — but the
    /// stated offer conditions are, so an errand only the right party hears is not recorded for the wrong one.
    /// </para>
    /// <para>
    /// The person is judged rather than trusted: an offer recorded from somebody the quest does not name as
    /// its giver is refused by name, exactly as a rank offered by the wrong person is, so the journal cannot
    /// claim an errand came from somebody who never gave it.
    /// </para>
    /// </remarks>
    /// <param name="quest">The quest the person stated.</param>
    /// <param name="person">The identity of the person stating it.</param>
    /// <param name="place">The place it was stated in, or an unset one when nowhere in particular.</param>
    /// <returns>What the offer did.</returns>
    public QuestResult Offer(QuestId quest, string person, PlaceId place = default)
    {
        if (_rule.Definition(quest) is not { } definition) return Refuse(QuestAction.Offer, quest, Unknown(quest));

        if (Instance(quest) is { } held)
        {
            return Refuse(QuestAction.Offer, quest, new Refusal(
                QuestCodes.QuestAlreadyKnown,
                $"The party already stands with '{definition.Name}' at the stage '{held.Stage}', so it was not offered again."));
        }

        if (!string.Equals(definition.Giver, person ?? string.Empty, StringComparison.Ordinal))
        {
            return Refuse(QuestAction.Offer, quest, new Refusal(
                QuestCodes.QuestNotTheGiver,
                $"'{definition.Name}' is given by {definition.Giver} and the party was told of it by {(string.IsNullOrEmpty(person) ? "nobody" : person)}, so the offer was not recorded."));
        }

        foreach (ConversationCondition condition in definition.OfferConditions)
        {
            if (_rule.Holds(new QuestConditionRequest(definition, condition, _party, _clock))) continue;
            return Refuse(QuestAction.Offer, quest, new Refusal(
                QuestCodes.QuestOfferConditionUnmet,
                $"'{definition.Name}' is offered only where {condition.Describe()} holds, and it does not for this party."));
        }

        _instances.Add(new QuestInstance(quest, QuestStage.Offered, definition.Giver, place.Value));
        Stamp = ChangeStamp.Next();
        return Record(QuestResult.Applied(QuestAction.Offer, quest, QuestStage.Offered));
    }

    /// <summary>Takes an errand the party was offered.</summary>
    /// <param name="quest">The quest to take.</param>
    /// <returns>What taking it did.</returns>
    public QuestResult Accept(QuestId quest)
    {
        if (Instance(quest) is not { } instance) return Refuse(QuestAction.Accept, quest, NotOffered(quest));
        if (instance.Stage != QuestStage.Offered)
        {
            return Refuse(QuestAction.Accept, quest, new Refusal(
                QuestCodes.QuestAlreadyTaken,
                $"{Name(quest)} stands at the stage '{instance.Stage}', so it was not taken again."));
        }

        _instances[_instances.IndexOf(instance)] = instance with { Stage = QuestStage.Accepted };
        Stamp = ChangeStamp.Next();
        return Record(QuestResult.Applied(QuestAction.Accept, quest, QuestStage.Accepted));
    }

    /// <summary>Finishes an errand and pays what it promised, or refuses whole and names what is missing.</summary>
    /// <remarks>
    /// <para>
    /// Everything is judged before anything moves: the quest must be one the party took, the person must be
    /// the giver the definition names, every objective must be met, every completion condition must hold, and
    /// every owner a reward would reach must exist. A refusal therefore names the objective that is unmet —
    /// in the quest's own words — rather than only that the turn-in failed, and leaves the party and the quest
    /// exactly where they stood.
    /// </para>
    /// <para>
    /// What the turn-in then does is one pass over the owners: experience to the award entry, records onto
    /// the party's effects, coin through the ledger, the items a delivery objective asked for handed over,
    /// and the quest's own record left last — the mark that it is finished, written after everything it
    /// promised arrived, so no reader can see the mark without the payment.
    /// </para>
    /// </remarks>
    /// <param name="quest">The quest to finish.</param>
    /// <param name="person">The identity of the person it is handed to.</param>
    /// <returns>What the turn-in paid, or why nothing happened.</returns>
    public QuestResult TurnIn(QuestId quest, string person)
    {
        if (Instance(quest) is not { } instance) return Refuse(QuestAction.TurnIn, quest, NotTaken(quest));
        if (_rule.Definition(quest) is not { } definition) return Refuse(QuestAction.TurnIn, quest, Unknown(quest));

        if (instance.Stage == QuestStage.TurnedIn)
        {
            return Refuse(QuestAction.TurnIn, quest, new Refusal(
                QuestCodes.QuestAlreadyFinished,
                $"'{definition.Name}' was already finished, so it was not paid a second time."));
        }

        if (instance.Stage == QuestStage.Offered)
        {
            return Refuse(QuestAction.TurnIn, quest, new Refusal(
                QuestCodes.QuestNotAccepted,
                $"'{definition.Name}' was offered and never taken, so there is nothing to finish."));
        }

        if (!string.Equals(definition.Giver, person ?? string.Empty, StringComparison.Ordinal))
        {
            return Refuse(QuestAction.TurnIn, quest, new Refusal(
                QuestCodes.QuestNotTheGiver,
                $"'{definition.Name}' is finished with {definition.Giver} and the party is speaking with {(string.IsNullOrEmpty(person) ? "nobody" : person)}, so nothing was paid."));
        }

        QuestReading reading = Read(definition, instance);
        if (reading.Unmet.Count > 0)
        {
            return Refuse(QuestAction.TurnIn, quest, new Refusal(
                QuestCodes.QuestObjectivesUnmet,
                $"'{definition.Name}' is not finished: {string.Join("; ", reading.Unmet)}."));
        }

        QuestRewards rewards = definition.Rewards;
        if (rewards.Experience > 0 && _progression is null)
        {
            return Refuse(QuestAction.TurnIn, quest, new Refusal(
                QuestCodes.QuestNoAwardOwner,
                $"'{definition.Name}' pays {rewards.Experience} experience and this session holds no owner every award arrives at, so nothing was paid."));
        }

        if (rewards.Coins > 0 && _ledger is null)
        {
            return Refuse(QuestAction.TurnIn, quest, new Refusal(
                QuestCodes.QuestNoLedger,
                $"'{definition.Name}' pays {rewards.Coins} coin and this session holds no settlement path to credit it through, so nothing was paid."));
        }

        // Judge the complete declared record payment before experience, coin or custody changes.
        Dictionary<string, int> counts = new(StringComparer.Ordinal);
        foreach (QuestRewardRecord record in rewards.Records)
        {
            long count = record.Accumulate
                ? (long)counts.GetValueOrDefault(record.Record, _party.Records.CountOf(record.Record)) + record.Amount
                : record.Amount;
            if (count > int.MaxValue)
            {
                return Refuse(QuestAction.TurnIn, quest, new Refusal(QuestCodes.QuestRecordCapacity,
                    $"'{definition.Name}' would exceed the count held for '{record.Record}', so nothing was paid."));
            }
            counts[record.Record] = (int)count;
        }

        ProgressionAwardResult? award = rewards.Experience > 0
            ? _progression!.Award(new PartyExperienceAward(QuestSource, rewards.Experience))
            : null;

        foreach (QuestRewardRecord record in rewards.Records)
        {
            if (record.Accumulate) _party.Records.Increment(record.Record, record.Amount);
            else if (record.Amount > 0) _party.Records.Set(record.Record, record.Amount);
        }

        if (rewards.Coins > 0) _ledger!.Credit(PartyCost.OfGold(rewards.Coins));

        List<QuestRewardItem> delivered = [];
        foreach (QuestObjectiveReading objective in reading.Objectives)
        {
            if (objective.Objective.Kind != QuestObjectiveKind.Deliver || !objective.IsMet) continue;
            Deliver(objective.Objective, delivered);
        }

        // Every reward was judged together a moment ago, so none is refused here; what is reported as taken is
        // what the pack actually took all the same.
        List<QuestRewardItem> taken = [];
        foreach (QuestRewardItem item in rewards.Items)
        {
            if (_party.AcquireItem(new ItemDefinitionId(item.Item), item.Count).Admitted) taken.Add(item);
        }

        // The record the quest leaves is written last, so what a later rank or topic reads as "finished" is
        // never true before everything the errand promised has arrived.
        if (definition.Record.Length > 0)
        {
            _party.Records.Set(definition.Record, 1);
        }

        _instances[_instances.IndexOf(instance)] = instance with { Stage = QuestStage.TurnedIn };
        Stamp = ChangeStamp.Next();
        return Record(QuestResult.Applied(
            QuestAction.TurnIn,
            quest,
            QuestStage.TurnedIn,
            new QuestPayment(award, rewards.Coins, taken, rewards.Records, delivered)));
    }

    /// <summary>
    /// Records that the party stands in a place, which is what a reach objective reads.
    /// </summary>
    /// <remarks>
    /// A place is reported by the world as a state rather than as an event — the party is in it for as long
    /// as it is — so what is recorded here is that the errand's own place was stood in while the errand was
    /// taken, and it stays recorded after the party walks out.
    /// </remarks>
    /// <param name="place">The place the party stands in.</param>
    public void Observe(PlaceId place)
    {
        if (string.IsNullOrEmpty(place.Value)) return;

        // The list is walked by position because recording progress replaces the instance in place: an
        // enumerator over it would be invalidated by the very write this method exists to make.
        for (int index = 0; index < _instances.Count; index++)
        {
            QuestInstance instance = _instances[index];
            if (instance.Stage != QuestStage.Accepted) continue;
            if (_rule.Definition(instance.Quest) is not { } definition) continue;

            QuestInstance current = instance;
            foreach (QuestObjective objective in definition.Objectives)
            {
                if (objective.Kind != QuestObjectiveKind.Reach) continue;
                if (!string.Equals(objective.Target, place.Value, StringComparison.Ordinal)) continue;
                current = current.Record(objective.Id, objective.Count);
            }

            if (ReferenceEquals(current, instance)) continue;
            _instances[index] = current;
            Stamp = ChangeStamp.Next();
        }
    }

    /// <summary>
    /// Records one creature's death against every kill objective an instance still wants.
    /// </summary>
    /// <remarks>
    /// A death is an event and not a state — the body lies there and then is gone — so what a kill objective
    /// counts is recorded here rather than read later, and it is reported once, so it is counted once. Which
    /// deaths count is the ruleset's answer, because what a creature is, is content's; this adds up what it is
    /// told and never guesses from a name.
    /// </remarks>
    /// <param name="death">The death.</param>
    /// <exception cref="ArgumentNullException">No death was supplied.</exception>
    public void ObserveDeath(CreatureDeath death)
    {
        ArgumentNullException.ThrowIfNull(death);

        // Walked by position for the same reason a place is: what a death does is record progress, which
        // replaces the instance being read.
        for (int index = 0; index < _instances.Count; index++)
        {
            QuestInstance instance = _instances[index];
            if (instance.Stage != QuestStage.Accepted) continue;
            if (_rule.Definition(instance.Quest) is not { } definition) continue;

            QuestInstance current = instance;
            foreach (QuestObjective objective in definition.Objectives)
            {
                if (objective.Kind != QuestObjectiveKind.Kill) continue;
                if (objective.Place.Length > 0 && !string.Equals(objective.Place, death.Place.Value, StringComparison.Ordinal)) continue;
                int counted = _rule.Counts(new QuestKillRequest(definition, objective, death.Place, death.Placement, death.Name));
                if (counted > 0) current = current.Record(objective.Id, Math.Min(objective.Count, current.Recorded(objective.Id) + counted));
            }

            if (ReferenceEquals(current, instance)) continue;
            _instances[index] = current;
            Stamp = ChangeStamp.Next();
        }
    }

    /// <summary>
    /// The quest that needs one item right now, or null when nothing does.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the answer a refusal names. It is asked of an errand the party has <em>taken</em> and not yet
    /// finished: an errand the party only heard about asks for nothing, and one that has been turned in asks
    /// for nothing more.
    /// </para>
    /// <para>
    /// <b>An objective that is met still needs what it names.</b> What a retrieve or delivery objective is
    /// judged against is the party carrying the thing at the moment the errand is finished, so the thing is
    /// needed until then — a party that sold it would simply stop having met the objective it had met. That
    /// is why the reading asks what the objective names rather than whether it currently holds.
    /// </para>
    /// </remarks>
    /// <param name="item">The item definition whose need is being asked about.</param>
    /// <returns>The quest and objective that need it, or null.</returns>
    public QuestNeed? Needs(ItemDefinitionId item)
    {
        foreach (QuestInstance instance in _instances)
        {
            if (instance.Stage != QuestStage.Accepted) continue;
            if (_rule.Definition(instance.Quest) is not { } definition) continue;
            QuestReading reading = Read(definition, instance);
            foreach (QuestObjectiveReading objective in reading.Objectives)
            {
                if (objective.Objective.Kind is not (QuestObjectiveKind.Retrieve or QuestObjectiveKind.Deliver)) continue;
                if (!string.Equals(objective.Objective.Target, item.Value, StringComparison.Ordinal)) continue;
                return new QuestNeed(definition.Id, definition.Name, objective.Objective.Label);
            }
        }

        return null;
    }

    /// <summary>The unfinished objective that keeps an instance in the party's custody, using the same need read by turn-in.</summary>
    public Refusal? Retains(ItemDefinitionId item) => Needs(item) is { } needed
        ? new Refusal(QuestCodes.QuestItemNeeded, $"{needed.Statement} is not done yet, so {item} stays with the party until that errand is finished.")
        : null;

    /// <summary>Reads this party's quest state into the product's one current save schema.</summary>
    /// <remarks>
    /// The capture is a snapshot of values rather than a view of live state: the stages and the recorded
    /// progress are what a save carries, and the definitions are deliberately left out because they are read
    /// back from the game's own content when the save is loaded.
    /// </remarks>
    /// <returns>The quests as a save records them.</returns>
    public QuestSave Capture() => new([.. _instances.Select(QuestInstanceSave.Record)]);

    /// <summary>Reads one quest as this party stands with it, over an instance it already holds.</summary>
    private QuestReading Read(QuestDefinition definition, QuestInstance instance)
    {
        List<QuestObjectiveReading> objectives = [];
        List<string> unmet = [];
        foreach (QuestObjective objective in definition.Objectives)
        {
            int count = Math.Min(objective.Count, Progress(objective, instance));
            bool met = count >= objective.Count;
            objectives.Add(new QuestObjectiveReading(objective, count, met));
            if (!met) unmet.Add(objectives[^1].Statement);
        }

        foreach (ConversationCondition condition in definition.CompletionConditions)
        {
            if (_rule.Holds(new QuestConditionRequest(definition, condition, _party, _clock))) continue;
            unmet.Add(condition.Describe());
        }

        return new QuestReading(definition, instance, objectives, unmet);
    }

    /// <summary>
    /// How much of one objective the party has done, read from whichever owner reports it.
    /// </summary>
    /// <remarks>
    /// Two kinds are recorded and four are read. A death and a place are moments this owner is told about and
    /// keeps; something carried, a person met, and a record held are states another owner already holds, so
    /// they are read from it — which is why a party that loses the thing an errand named stops having that
    /// objective met without anything having to notice.
    /// </remarks>
    private int Progress(QuestObjective objective, QuestInstance instance) => objective.Kind switch
    {
        QuestObjectiveKind.Kill or QuestObjectiveKind.Reach => instance.Recorded(objective.Id),
        QuestObjectiveKind.Retrieve => _party.Inventory.TotalOf(new ItemDefinitionId(objective.Target)),
        QuestObjectiveKind.Talk => _party.Records.Has(objective.Target) ? 1 : 0,
        QuestObjectiveKind.Deliver => Delivered(objective),
        QuestObjectiveKind.Flag => _party.Records.CountOf(objective.Target),
        _ => 0,
    };

    /// <summary>Whether a delivery has both halves: the thing carried and the person it goes to.</summary>
    private int Delivered(QuestObjective objective) =>
        _party.Records.Has(objective.Person)
            ? _party.Inventory.TotalOf(new ItemDefinitionId(objective.Target))
            : 0;

    /// <summary>Hands over what one delivery objective asked for, instance by instance.</summary>
    private void Deliver(QuestObjective objective, List<QuestRewardItem> delivered)
    {
        int handed = 0;
        for (int index = 0; index < objective.Count; index++)
        {
            if (_party.Inventory.Find(new ItemDefinitionId(objective.Target)) is not { } item) break;
            if (_party.DeliverItem(item.Id) is null) break;
            handed++;
        }

        if (handed > 0) delivered.Add(new QuestRewardItem(objective.Target, handed));
    }

    private string Name(QuestId quest) => _rule.Definition(quest) is { } definition ? $"'{definition.Name}'" : $"'{quest}'";

    private Refusal Unknown(QuestId quest) => new(
        QuestCodes.QuestUnknown,
        $"This game states no quest '{quest}', so there is nothing to offer, take, or finish.");

    private Refusal NotOffered(QuestId quest) => new(
        QuestCodes.QuestNotOffered,
        $"{Name(quest)} was never offered to this party, so there is nothing to take.");

    private Refusal NotTaken(QuestId quest) => new(
        QuestCodes.QuestNotTaken,
        $"{Name(quest)} is not in the party's journal, so there is nothing to finish.");

    private QuestResult Refuse(QuestAction action, QuestId quest, Refusal refusal) =>
        Record(QuestResult.Refused(action, quest, refusal));

    private QuestResult Record(QuestResult result)
    {
        Last = result;
        Stamp = ChangeStamp.Next();
        return result;
    }
}
