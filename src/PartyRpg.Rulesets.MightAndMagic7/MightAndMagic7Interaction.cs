using System.Text.Json;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// This game's answers about using what the world holds: what a placement offers the party, what a
/// requirement means here, what still guards a target, and what a granted use makes of a door, a fixture,
/// or a container.
/// </summary>
/// <remarks>
/// <para>
/// <b>What the imported data supports.</b> A place's doors arrive as placements carrying the delta's own
/// door state and attributes, its containers as placements carrying the delta's chest records, its loose
/// items as the sprite objects holding them, and an interior's decorations as the event each one raises.
/// This ruleset turns the first into a door the party opens, the second and third into containers it
/// searches, and the last into a fixture whose use is refused with the event named, because nothing in
/// this build executes map events. Everything else a place holds — spawn points, lights, decorations that
/// raise nothing, sprite objects holding nothing — is not a target, which is the same filter the original
/// applies when it picks what the interaction key can reach (OpenEnroth
/// <c>src/Engine/Graphics/Vis.cpp:31-34</c>, the door and event-decoration filters).
/// </para>
/// <para>
/// <b>What content adds.</b> A placement may state what using it requires — an item, a skill, a flag, a part
/// of the day — in a <c>requires</c> array, and that is where a lock comes from. The imported packs state
/// none, because the original's locked doors are map events rather than door records; an authored pack, and
/// the tests, are what exercise the vocabulary until the map-event interpreter can read the original's own
/// locks.
/// </para>
/// <para>
/// <b>A place's hours lock its doors.</b> When the place the door stands in keeps hours — its counters'
/// hours, or hours its own entry states — the door is given one more requirement: the hours themselves,
/// judged against the one clock on every use. A shut door is therefore an unmet requirement with the
/// sentence naming the hours, never a menu entry that politely disappears, and it opens again by itself
/// when the clock reaches the hour its place opens at, because nothing about it was ever remembered.
/// </para>
/// <para>
/// <b>A body is a container the fight made.</b> A creature the party brought down is described as the same
/// kind of target a chest is, searched through the same workflow, and transferred into the same shared pack:
/// the corpse answers come from <see cref="MightAndMagic7Corpses"/>, which reads what the fight reported, and
/// this rule hands them the same way it hands a chest's. That is what makes a kill yield loot without a
/// second mechanism for looting.
/// </para>
/// <para>
/// <b>What this game cannot deliver yet, stated rather than hidden.</b> Opening a door records its state and
/// reports it, and leaves the door's polygons standing as collision, because door geometry does not move in
/// this build; that is the residue the outcome carries. A fixture's use raises an event nothing executes, so
/// it is a refusal with the event named rather than a success that did nothing.
/// </para>
/// </remarks>
internal sealed class MightAndMagic7Interaction : IInteractionRule, ICorpseSource
{
    /// <summary>The placement kind an interior's door slot is imported as.</summary>
    internal const string DoorPlacementKind = "door";

    /// <summary>The placement kind an interior's decorations are imported as.</summary>
    internal const string DecorationPlacementKind = "decoration";

    /// <summary>The target kind a door is.</summary>
    internal const string DoorTargetKind = "door";

    /// <summary>The target kind a decoration raising an event is.</summary>
    internal const string FixtureTargetKind = "fixture";

    /// <summary>The state word a door the party has opened holds.</summary>
    internal const string OpenState = "open";

    /// <summary>The state word a door whose lock the party has turned holds, before it is opened.</summary>
    internal const string UnlockedState = "unlocked";

    /// <summary>The state word a door standing in the way holds.</summary>
    internal const string ClosedState = "closed";

    /// <summary>The placement field that names the event a decoration raises.</summary>
    internal const string EventField = "eventId";

    /// <summary>The placement field that names what using a placement requires.</summary>
    internal const string RequiresField = "requires";

    /// <summary>The placement field that states what a door's delta stored: <c>0</c> at rest, <c>2</c> moved.</summary>
    internal const string DoorStateField = "state";

    /// <summary>The decoration field that carries the decoration's internal name.</summary>
    internal const string DecorationNameField = "name";

    /// <summary>
    /// How far from a target the party may stand and still use it, in place units.
    /// </summary>
    /// <remarks>
    /// The donor's keyboard interaction depth: "Maximum range for item pickup / opening chests / activating
    /// levers / etc with a keyboard" (OpenEnroth <c>src/Application/GameConfig.h:180</c>,
    /// <c>keyboard_interaction_depth</c> default 512). It is one number for every kind of target because the
    /// donor has one, and a range a game tunes belongs here rather than in the kit.
    /// </remarks>
    internal const double Reach = 512;

    /// <summary>The half-angle a target is picked up within, in radians.</summary>
    /// <remarks>
    /// The donor casts a single ray through the reticle and takes the topmost thing it meets
    /// (<c>src/Application/Game.cpp:1511</c> <c>onPressSpace</c> and <c>src/Engine/Graphics/Vis.cpp:659</c>
    /// <c>PickKeyboard</c>), which a mouse aims precisely and a keyboard cannot. This product's aim is a cone
    /// instead, and the cone is a deliberate adaptation: it is wide enough to find a door the party is
    /// facing and narrow enough that a door behind the party is not what it uses.
    /// </remarks>
    internal const double AcquisitionAngleRadians = 0.20;

    /// <summary>The half-angle a picked-up target is kept within, in radians.</summary>
    internal const double ReleaseAngleRadians = 0.31;

    /// <summary>The aim this game's reticle acquires and releases targets within.</summary>
    internal static InteractionTuning Aim { get; } = new(AcquisitionAngleRadians, ReleaseAngleRadians);

    private readonly PlaceSchedule? _schedule;
    private readonly MightAndMagic7Corpses? _corpses;
    private readonly MightAndMagic7Loot? _loot;

    /// <summary>Creates this game's interaction answers.</summary>
    /// <param name="schedule">
    /// Which places keep hours, when this game's content clocks any: a door in a clocked place is locked
    /// outside them. Without one no door is locked by the hour, and every other answer stands.
    /// </param>
    /// <param name="corpses">
    /// What the fight reported, when this session has corpses to answer about. Without one no placement is a
    /// body, which is the honest state of a session whose fight keeps no record of what it brought down.
    /// </param>
    /// <param name="loot">
    /// This game's loot, which a container's random references are answered by. Without one a container
    /// holding one is refused by name rather than emptied of invented contents.
    /// </param>
    internal MightAndMagic7Interaction(
        PlaceSchedule? schedule = null,
        MightAndMagic7Corpses? corpses = null,
        MightAndMagic7Loot? loot = null)
    {
        _schedule = schedule;
        _corpses = corpses;
        _loot = loot;
    }

    /// <summary>The door state the delta stores for a door at rest, which the donor calls open.</summary>
    /// <remarks>
    /// OpenEnroth <c>src/Engine/Graphics/FaceEnums.h:63-66</c>: <c>DOOR_OPEN = 0</c> is the door mesh at the
    /// offsets it rests at and <c>DOOR_CLOSED = 2</c> is the moved position. The importer stores the number
    /// as it stands rather than interpreting it, which is why the reading is here.
    /// </remarks>
    private const int DoorRestState = 0;

    /// <summary>
    /// Reads what a placement requires and what a container holds, failing while the world is built on
    /// content that states either of them in a way nothing can resolve.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Requirements and contents are both read inside an admitted update, where a content defect cannot be
    /// reported without stopping the session, so they are checked once here: a requirement that names no
    /// kind, no identity, or a kind this game does not know is a defect in the pack that declared it, and a
    /// container item reference that names an item the catalog does not carry is a defect too, because a
    /// party that searched such a container would be handed an identity nothing resolves. Every defect is
    /// named at once, not the first.
    /// </para>
    /// <para>
    /// A reference that asks for a random item is not a defect: the map data records a request the item
    /// generator answers elsewhere, and the search refuses by name until that owner exists.
    /// </para>
    /// </remarks>
    /// <param name="catalog">The content the world is being built from, when any loaded.</param>
    /// <exception cref="ContentValidationException">A placement states a requirement or a container item that cannot be resolved.</exception>
    internal static void Validate(ContentCatalog? catalog)
    {
        if (catalog is null) return;
        HashSet<string> items = [.. catalog.Entries(MightAndMagic7Containers.ItemDefinitionKind).Select(entry => entry.Entry.Id)];
        List<ContentValidationIssue> issues = [];
        foreach ((LoadedPack pack, ContentDocument document, ContentEntry entry) in catalog.Entries(PlaceGraphLoader.PlaceDefinitionKind))
        {
            foreach (JsonElement placement in entry.GetArray(PlacePopulationContent.PlacementsField))
            {
                foreach (JsonElement requirement in MightAndMagic7Containers.ReadArray(placement, RequiresField))
                {
                    if (ReadKind(ContentEntry.ReadString(requirement, "kind")) is null)
                    {
                        issues.Add(new ContentValidationIssue(
                            "interaction-requirement-kind-unknown",
                            $"place '{entry.Id}' declares a requirement of kind '{ContentEntry.ReadString(requirement, "kind")}', which is not an item, a skill, a flag, or a time of day.",
                            pack.PackId,
                            document.DocumentId));
                    }
                }

                string kind = ContentEntry.ReadString(placement, PlacePopulationContent.KindField);
                foreach (int reference in MightAndMagic7Containers.DeclaredReferences(placement, kind))
                {
                    if (MightAndMagic7Containers.Resolves(reference, items)) continue;
                    issues.Add(new ContentValidationIssue(
                        "interaction-container-item-unknown",
                        $"place '{entry.Id}' declares a container holding item '{reference}', which is neither an item the catalog carries nor one of the seven treasure levels a random item is asked for by; a party that searched it would be handed an identity nothing resolves.",
                        pack.PackId,
                        document.DocumentId));
                }
            }
        }

        if (issues.Count > 0)
        {
            throw new ContentValidationException(
                $"The world's interaction requirements and container contents cannot be read: {issues[0].Message}",
                issues);
        }
    }

    /// <inheritdoc />
    public InteractionTargetDefinition? Describe(InteractionTargetRequest request)
    {
        PlacementDefinition placement = request.Placement;
        IReadOnlyList<InteractionRequirement> requires = ReadRequirements(placement);

        // What is lying at a placement is asked first, because a creature's own placement is where its body
        // lies: a door is never a body, and a creature the fight has not read as down is not a target at all.
        if (_corpses?.Describe(request) is { } body) return body;

        if (string.Equals(placement.Content.Kind, DoorPlacementKind, StringComparison.Ordinal))
        {
            // A door whose lock is stated and not yet turned offers the use that turns it; the same door
            // afterwards, and every door that states no lock, offers the use that opens it. The two are
            // different verbs because they are different acts, and the state word is what tells them apart.
            // What the place's hours add is a gate rather than a lock, so it is read after the lock is: a
            // door the party could simply open is not made into a two-step lock by the hour of the day.
            string state = DoorState(placement, request.State);
            bool locked = requires.Count > 0 && !string.Equals(state, UnlockedState, StringComparison.Ordinal);
            return new InteractionTargetDefinition(
                new InteractionTargetKind(DoorTargetKind),
                "A door",
                locked ? InteractionVerb.Unlock : InteractionVerb.Open,
                Reach,
                state,
                Hours(request.Place, requires));
        }

        // A container and a pile are one target: what they hold came from different records, and what the
        // party does with them is the same act, so the reading lives in one place rather than two.
        if (MightAndMagic7Containers.Describe(placement, request.State, requires, Reach) is { } container)
        {
            return container;
        }

        if (string.Equals(placement.Content.Kind, DecorationPlacementKind, StringComparison.Ordinal) &&
            placement.Source.GetInt32(EventField) is { } eventId && eventId != 0)
        {
            string name = placement.Source.GetString(DecorationNameField);
            return new InteractionTargetDefinition(
                new InteractionTargetKind(FixtureTargetKind),
                name.Length == 0 ? "A fixture" : $"A fixture ({name})",
                InteractionVerb.Pull,
                Reach);
        }

        return null;
    }

    /// <inheritdoc />
    public InteractionTrap? Trap(InteractionTargetDefinition target, InteractionContext context) =>
        MightAndMagic7Containers.Trap(target, context);

    /// <inheritdoc />
    public InteractionRequirementVerdict Judge(InteractionRequirement requirement, InteractionContext context) => requirement.Kind switch
    {
        InteractionRequirementKind.Item => JudgeItem(requirement, context),
        InteractionRequirementKind.Skill => JudgeSkill(requirement, context),
        InteractionRequirementKind.TimeOfDay => JudgeTime(requirement, context),
        _ => InteractionRequirementVerdict.Unsatisfied(
            $"What '{requirement.Name}' asks for is something this game does not record yet, so it cannot be met."),
    };

    /// <inheritdoc />
    /// <remarks>
    /// A body and a chest are the same kind of target, so the first question is whether anything is lying at
    /// the placement: the answer decides whether the search reads a death's contents or a record's, and
    /// everything else about the use is the same.
    /// </remarks>
    public InteractionOutcome Apply(InteractionTargetDefinition target, InteractionContext context)
    {
        if (string.Equals(target.Kind.Value, MightAndMagic7Containers.TargetKind, StringComparison.Ordinal))
        {
            return _corpses is not null && _corpses.Describe(new InteractionTargetRequest(context.Place, context.Placement, target.State)) is not null
                ? _corpses.Search(target, context)
                : MightAndMagic7Containers.Search(target, context, _loot);
        }

        if (string.Equals(target.Kind.Value, FixtureTargetKind, StringComparison.Ordinal))
        {
            int eventId = context.Placement.Source.GetInt32(EventField) ?? 0;
            return InteractionOutcome.Refused(
                "interaction-event-not-executed",
                $"{target.Name} raises map event {eventId} of place '{context.Place}', and nothing in this build executes map events: the event interpreter that will is not built.");
        }

        return Door(target, context);
    }

    /// <summary>What is lying in one place, which is what the interaction mechanism merges beside content.</summary>
    /// <remarks>
    /// The bodies the fight reported travel to the mechanism from here because this rule is the one object
    /// both halves of a session are composed over: the fight is handed this game's corpse owner to report to,
    /// and the mechanism is handed this game's answers. A world-composed source would be a second owner of
    /// one fact.
    /// </remarks>
    public IReadOnlyList<PlacementDefinition> CorpsesOf(PlaceId place) =>
        _corpses?.CorpsesOf(place) ?? [];

    /// <summary>What using a door makes of it, given its state and what it requires.</summary>
    /// <remarks>
    /// A locked door is turned first and opened second, which is why the state word rather than the lock
    /// alone decides the verb. The passage the party cannot walk is stated as the outcome's residue: this
    /// build admits a door's polygons as collision wherever they stand, so a door that is open in state is
    /// still a door the party cannot walk through, and a report that said only "it opens" would be claiming a
    /// way through that is not there. Whether a lock stands in the way is read from the verb the definition
    /// offers rather than from the requirements alone, so the hours a place keeps are a gate at the door and
    /// not a second lock inside it.
    /// </remarks>
    private static InteractionOutcome Door(InteractionTargetDefinition target, InteractionContext context)
    {
        if (string.Equals(target.State, OpenState, StringComparison.Ordinal))
        {
            return InteractionOutcome.Refused("door-already-open", $"{target.Name} already stands open.");
        }

        if (target.Verb == InteractionVerb.Unlock)
        {
            return InteractionOutcome.Applied(
                UnlockedState,
                $"What {target.Name} was locked with is to hand, and the lock falls open.");
        }

        return InteractionOutcome.Applied(
            OpenState,
            $"{target.Name} swings open.",
            "Doors do not move in this build: its polygons are still admitted where they stood, so the doorway cannot be walked through yet.");
    }

    /// <summary>
    /// What a use of a door requires: the placement's own requirements, and the hours its place keeps.
    /// </summary>
    /// <remarks>
    /// The hours are appended rather than put first so a door that needs a key still says so before it says
    /// the shop is shut: the first unmet requirement is the one a refusal names, and what the party carries
    /// is what it can do something about. A place that keeps no hours adds nothing, because nothing has shut
    /// its doors.
    /// </remarks>
    private IReadOnlyList<InteractionRequirement> Hours(PlaceId place, IReadOnlyList<InteractionRequirement> stated)
    {
        if (_schedule?.HoursOf(place) is not { } hours) return stated;
        List<InteractionRequirement> all = [.. stated];
        all.Add(new InteractionRequirement(
            InteractionRequirementKind.TimeOfDay,
            MightAndMagic7Schedules.OpenRequirementName,
            1,
            $"the hours {hours}"));
        return all;
    }

    /// <summary>
    /// What a door currently reads as: the word the party's own use left it in, or the position the
    /// delta stored when nothing has happened to it.
    /// </summary>
    /// <remarks>
    /// The stored number is the donor's own door state: a door at rest is the one the donor calls open, and
    /// every other stored position is one the door has moved to, which is a door that stands closed. The
    /// reading is done here rather than by the importer because the number is map runtime state and not an
    /// interpretation of the level, and a pack that carried the word instead would be a pack a different
    /// game could not read.
    /// </remarks>
    private static string DoorState(PlacementDefinition placement, string recorded) =>
        recorded.Length > 0
            ? recorded
            : placement.Source.GetInt32(DoorStateField) == DoorRestState ? OpenState : ClosedState;

    /// <summary>Whether the party carries what an item requirement names.</summary>
    /// <remarks>
    /// An item requirement is a key when a lock states one: a key is an item the party carries, and the kit
    /// has one kind for both rather than two names for one check. It is read from the party's one shared pack
    /// and what its members wear, which is every item the party holds.
    /// </remarks>
    private static InteractionRequirementVerdict JudgeItem(InteractionRequirement requirement, InteractionContext context)
    {
        if (context.Party is not { } party)
        {
            return InteractionRequirementVerdict.Unsatisfied(
                $"It requires {requirement.Describe()}, and the party that would carry it does not exist in this session.");
        }

        ItemDefinitionId definition = new(requirement.Name);
        int carried = party.Inventory.TotalOf(definition);
        return carried >= requirement.Amount
            ? InteractionRequirementVerdict.Satisfied
            : InteractionRequirementVerdict.Unsatisfied(
                $"It requires {requirement.Describe()} and the party carries {carried} of it.");
    }

    /// <summary>Whether any member has a skill to the level a requirement names.</summary>
    /// <remarks>
    /// The best level in the party is what answers, because a party acts as one band: the strongest member's
    /// skill is the party's, which is the same reading a locked door in the original takes when it asks the
    /// party whether anybody can pick it.
    /// </remarks>
    private static InteractionRequirementVerdict JudgeSkill(InteractionRequirement requirement, InteractionContext context)
    {
        if (context.Party is not { } party)
        {
            return InteractionRequirementVerdict.Unsatisfied(
                $"It requires {requirement.Describe()}, and the party that would know it does not exist in this session.");
        }

        SkillId skill = new(requirement.Name);
        int best = 0;
        foreach (PartyMember member in party.Members) best = Math.Max(best, member.Skills.LevelOf(skill));
        return best >= requirement.Amount
            ? InteractionRequirementVerdict.Satisfied
            : InteractionRequirementVerdict.Unsatisfied(
                $"It requires {requirement.Describe()} and the party's best is {best}.");
    }

    /// <summary>Whether the clock stands in the part of the day a requirement names.</summary>
    /// <remarks>
    /// Two readings of one requirement kind: <c>day</c> and <c>night</c> are the clock's own halves, which
    /// the daylight window answers, and the schedule's own word is the place's hours, which the schedule
    /// answers. Both are read from the live clock at the moment of the use, so a door that was shut at
    /// midnight is open at six without anything having changed but the hour.
    /// </remarks>
    private InteractionRequirementVerdict JudgeTime(InteractionRequirement requirement, InteractionContext context)
    {
        if (context.Clock is not { } clock)
        {
            return InteractionRequirementVerdict.Unsatisfied(
                $"It can only be used at {requirement.Name} and this session keeps no clock, so no time of day is known.");
        }

        if (string.Equals(requirement.Name, MightAndMagic7Schedules.OpenRequirementName, StringComparison.OrdinalIgnoreCase))
        {
            return JudgeOpenHours(context, clock);
        }

        bool wantsDay = string.Equals(requirement.Name, "day", StringComparison.OrdinalIgnoreCase);
        bool isDay = clock.IsDaylight;
        return wantsDay == isDay
            ? InteractionRequirementVerdict.Satisfied
            : InteractionRequirementVerdict.Unsatisfied(
                $"It can only be used at {requirement.Name} and it is {(isDay ? "day" : "night")}.");
    }

    /// <summary>
    /// Whether the place the door stands in is open at the hour the clock stands on.
    /// </summary>
    /// <remarks>
    /// This is the whole of a scheduled lock: a clock read, taken when the door is used. A shut door says
    /// what it keeps, what the clock reads, and when it opens again, so a player who finds a shop locked at
    /// midnight knows how long the wait is instead of being told only that it is closed. A place whose hours
    /// this ruleset did not read is open, because nothing has shut it.
    /// </remarks>
    private InteractionRequirementVerdict JudgeOpenHours(InteractionContext context, GameClock clock)
    {
        if (_schedule?.HoursOf(context.Place) is not { } hours) return InteractionRequirementVerdict.Satisfied;
        GameDate now = clock.Now;
        if (hours.IsOpenAt(now)) return InteractionRequirementVerdict.Satisfied;

        string opens = hours.NextChangeAfter(clock.Calendar, now) is { } next
            ? string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $" and it opens again at {next.Year:0000}-{next.Month:00}-{next.Day:00} {next.Hour:00}:{next.Minute:00}")
            : string.Empty;
        return InteractionRequirementVerdict.Unsatisfied(
            string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"It keeps {hours} and the clock stands at {now.Hour:00}:{now.Minute:00}{opens}."));
    }

    /// <summary>Reads what a placement requires, in the order it states them.</summary>
    private static IReadOnlyList<InteractionRequirement> ReadRequirements(PlacementDefinition placement)
    {
        List<InteractionRequirement> requires = [];
        foreach (JsonElement element in MightAndMagic7Containers.ReadArray(placement.Source.Payload, RequiresField))
        {
            string kind = ContentEntry.ReadString(element, "kind");
            string name = ContentEntry.ReadId(element, "id");
            if (ReadKind(kind) is not { } requirementKind || name.Length == 0) continue;

            double? amount = ContentEntry.ReadDouble(element, "amount");
            requires.Add(new InteractionRequirement(
                requirementKind,
                name,
                amount is { } value && value >= 1 && value <= int.MaxValue ? (int)value : 1,
                ContentEntry.ReadString(element, "label")));
        }

        return requires;
    }

    /// <summary>The requirement kind a content word names, or null when this game has none for it.</summary>
    private static InteractionRequirementKind? ReadKind(string kind) => kind switch
    {
        "item" => InteractionRequirementKind.Item,
        "skill" => InteractionRequirementKind.Skill,
        "flag" => InteractionRequirementKind.Flag,
        "time" => InteractionRequirementKind.TimeOfDay,
        _ => null,
    };
}
