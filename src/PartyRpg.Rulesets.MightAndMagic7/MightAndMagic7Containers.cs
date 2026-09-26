using System.Globalization;
using System.Text.Json;
using PartyRpg.Kit.Content;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Loot;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.World;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// This game's containers: what a chest or a pile offers, what guards it, and what searching it yields.
/// </summary>
/// <remarks>
/// <para>
/// <b>One target, several sources.</b> A chest comes from a delta's container array, placed by the map
/// face whose event opens it; a pile comes from a sprite object that holds an item; a corpse would come
/// from an actor's body. They are the same target here — the same kind, the same verb, the same transfer,
/// the same state words — because what differs between them is where their contents were recorded, and
/// that is a fact about the source and not about searching.
/// </para>
/// <para>
/// <b>What the shipped data holds.</b> A container record stores its items as identifiers, and a shipped
/// one mostly stores the donor's negative references, which ask for a random item of a treasure level
/// (<c>src/Engine/Objects/ItemEnums.h:956-963</c>). Those references are answered here, by the loot owner
/// that turns a level into things, through the place's own danger level and the donor's own shape for one
/// finding (<c>src/Engine/Objects/Chest.cpp:323-365</c>). A reference is therefore resolved once, under a
/// key that names the container and the slot it stood in, so a search refused for want of room and tried
/// again finds exactly what the first attempt would have given.
/// </para>
/// <para>
/// <b>Traps.</b> A record's own flag word says whether it is trapped (<c>Trapped = 0x1</c>, which both
/// donors agree on: OpenEnroth <c>src/Engine/Objects/ChestEnums.h:7</c> and MMExtension
/// <c>Scripts/Core/ConstAndBits.lua:116-120</c>). Its difficulty and its damage are the place's own row
/// of the per-map table, which the importer copies onto the container. The check is the donor's: a
/// character's disarm skill is tested against twice the map's disarm difficulty
/// (<c>src/Engine/Objects/Chest.cpp:64-65</c>), and a trap that goes off does its damage to the party
/// (<c>src/Engine/Objects/SpriteObject.cpp:512-546</c>, five points plus the map's dice of twenty-sided
/// damage). The two donors disagree about the record's remaining bit — 0x4 is <c>CHEST_OPENED</c> to
/// OpenEnroth and <c>Identified</c> to MMExtension — so nothing here reads it.
/// </para>
/// <para>
/// <b>An adaptation, stated.</b> The donor never tests perception against a container: it discovers a trap
/// only by setting it off. The manual gives perception a part in traps — "chests and drawers may be
/// trapped (Disarm Trap, with Perception reducing the damage of a triggered trap)"
/// (<c>docs/research/mm7-manual-outline.md</c>, p.27) — and this product makes it the check that notices
/// the trap before it goes off, which is the act a player can decide about. That is our reading rather
/// than the donor's, and it is why noticing and defeating are two separate answers here.
/// </para>
/// <para>
/// <b>The trap's dice are taken at their mean.</b> The donor rolls them; this build prices a trap by the
/// same number every time, because a trap's harm is applied by the interaction mechanism's own workflow
/// rather than by a generator with a key to draw under. The loot this game gives a container is drawn, and
/// the trap's dice are the one place the two readings differ — a difference stated here rather than hidden
/// behind one number that looks rolled.
/// </para>
/// </remarks>
internal static class MightAndMagic7Containers
{
    /// <summary>The placement kind a placed container record is imported as.</summary>
    internal const string ContainerPlacementKind = "container";

    /// <summary>The placement kind an item-carrying sprite object is imported as.</summary>
    internal const string PilePlacementKind = "sprite";

    /// <summary>The target kind a chest, a pile, and a corpse are: something the party searches.</summary>
    internal const string TargetKind = "container";

    /// <summary>The placement field that carries what a container holds.</summary>
    internal const string ContentsField = "contents";

    /// <summary>The contents field that carries one reference's item identifier.</summary>
    internal const string ItemField = "item";

    /// <summary>The contents field that carries the slot the item was stored in.</summary>
    internal const string SlotField = "slot";

    /// <summary>The placement field that carries a container record's own flag word.</summary>
    internal const string FlagsField = "flags";

    /// <summary>The placement field that carries the place's trap difficulty.</summary>
    internal const string TrapDifficultyField = "trapDifficulty";

    /// <summary>The placement field that carries the place's trap damage, in twenty-sided dice.</summary>
    internal const string TrapDamageDiceField = "trapDamageDice";

    /// <summary>The placement field that carries the item a sprite object holds.</summary>
    internal const string ContainingItemField = "containingItem";

    /// <summary>The placement field that carries the place's own map treasure level.</summary>
    /// <remarks>
    /// The donor reads it from the place's row of the per-map table and uses it to remap every random
    /// reference a container holds (OpenEnroth <c>src/Engine/Tables/MapTable.cpp:76</c>,
    /// <c>src/Engine/Objects/Chest.cpp:331</c>), so the number travels with the container the way its trap
    /// numbers do rather than widening every reader of the map table.
    /// </remarks>
    internal const string MapTreasureLevelField = "mapTreasureLevel";

    /// <summary>The record flag that says a chest is trapped, which both donors agree on.</summary>
    internal const int TrappedFlag = 0x1;

    /// <summary>The definition kind the item table's rows are imported under.</summary>
    internal const string ItemDefinitionKind = "item";

    /// <summary>The lowest random reference a container item may carry, which asks for treasure level 1.</summary>
    internal const int FirstRandomReference = -7;

    /// <summary>The highest random reference a container item may carry, which asks for treasure level 7.</summary>
    internal const int LastRandomReference = -1;

    /// <summary>The sense a party notices a trap with, as the skill table names it.</summary>
    internal const string PerceptionSkill = "Perception";

    /// <summary>The skill a party defeats a trap with, as the skill table names it.</summary>
    internal const string DisarmTrapsSkill = "Disarm Traps";

    /// <summary>The word a container reads as once the party has noticed its trap.</summary>
    internal const string TrappedState = "trapped";

    /// <summary>The word a container reads as once its trap is defeated.</summary>
    internal const string DisarmedState = "disarmed";

    /// <summary>The word a container reads as once its trap has gone off.</summary>
    internal const string SprungState = "sprung";

    /// <summary>The word a container reads as once the party has taken what it held.</summary>
    internal const string SearchedState = "searched";

    /// <summary>What a sprung trap takes from every member before its dice are counted.</summary>
    private const int TrapBaseDamage = 5;

    /// <summary>How many sides the trap dice have.</summary>
    private const int TrapDieSides = 20;

    /// <summary>The name a person reads for a chest, and for a pile of items.</summary>
    internal static string NameOf(PlacementDefinition placement) =>
        string.Equals(placement.Content.Kind, PilePlacementKind, StringComparison.Ordinal)
            ? "A pile of items"
            : "A chest";

    /// <summary>What a container offers the party, or null when the placement is not one.</summary>
    /// <remarks>
    /// A pile that holds nothing is not a target: there is nothing to reach for and nothing to say, and a
    /// refusal would be a sentence about an object nobody was ever going to use. Every other container is
    /// offered, and which act it offers follows from its state: a lock that has not been turned is turned
    /// first, a trap the party already knows about is what the next use is spent on, and everything else is
    /// the search itself.
    /// </remarks>
    /// <param name="placement">The placement content declared.</param>
    /// <param name="state">What the party has already done to it, as this game's own word.</param>
    /// <param name="requires">What the use requires, which is where a lock on a container comes from.</param>
    /// <param name="reach">How far from a target the party may stand and still use it.</param>
    internal static InteractionTargetDefinition? Describe(
        PlacementDefinition placement,
        string state,
        IReadOnlyList<InteractionRequirement> requires,
        double reach)
    {
        string kind = placement.Content.Kind;
        bool isPile = string.Equals(kind, PilePlacementKind, StringComparison.Ordinal);
        if (!isPile && !string.Equals(kind, ContainerPlacementKind, StringComparison.Ordinal)) return null;
        if (isPile && (placement.Source.GetInt32(ContainingItemField) ?? 0) == 0) return null;

        bool locked = requires.Count > 0 && !string.Equals(state, MightAndMagic7Interaction.UnlockedState, StringComparison.Ordinal);
        bool knownTrap = string.Equals(state, TrappedState, StringComparison.Ordinal);
        InteractionVerb verb = locked
            ? InteractionVerb.Unlock
            : knownTrap ? InteractionVerb.Disarm : InteractionVerb.Search;
        return new InteractionTargetDefinition(
            new InteractionTargetKind(TargetKind),
            NameOf(placement),
            verb,
            reach,
            state,
            requires);
    }

    /// <summary>What guards a container right now, or null when nothing does.</summary>
    /// <remarks>
    /// The trap is answered on every use rather than travelling with the target's description because what
    /// the party brings to it is read from the party, and a description is an answer about content that a
    /// world without a party still gives. A trap the party has already defeated or already set off is not
    /// answered at all: it is spent, and what is left is the search.
    /// </remarks>
    /// <param name="target">The definition the container was given.</param>
    /// <param name="context">The container, its state, and the party the check is about.</param>
    internal static InteractionTrap? Trap(InteractionTargetDefinition target, InteractionContext context)
    {
        if (!string.Equals(target.Kind.Value, TargetKind, StringComparison.Ordinal)) return null;
        if (context.Placement.Source.GetInt32(FlagsField) is not { } flags || (flags & TrappedFlag) == 0) return null;
        if (string.Equals(target.State, DisarmedState, StringComparison.Ordinal) ||
            string.Equals(target.State, SprungState, StringComparison.Ordinal))
        {
            return null;
        }

        int difficulty = context.Placement.Source.GetInt32(TrapDifficultyField) ?? 0;
        int dice = context.Placement.Source.GetInt32(TrapDamageDiceField) ?? 0;
        return new InteractionTrap(
            "a trap",
            new InteractionChallenge("Perception", BestSkill(context.Party, PerceptionSkill), difficulty),
            new InteractionChallenge("Disarm Traps", BestSkill(context.Party, DisarmTrapsSkill), 2 * difficulty),
            new InteractionHarm(TrapDamage(dice)),
            string.Equals(target.State, TrappedState, StringComparison.Ordinal),
            TrappedState,
            DisarmedState,
            SprungState);
    }

    /// <summary>What searching a container makes of it, given that nothing guards it any more.</summary>
    /// <remarks>
    /// <para>
    /// Every reference is answered the same way whichever it is: a positive one names an item the catalog
    /// carries, and a negative one asks the loot owner for a treasure level. The place's own danger level
    /// travels with the container, so a reference in a dangerous place yields better than the same reference
    /// in a quiet one, which is the donor's own reading of a container's contents.
    /// </para>
    /// <para>
    /// Nothing is rolled twice. Each reference is drawn under a key that names the container and the slot it
    /// stood in, so a search the party's pack could not take is refused whole and the retry finds the same
    /// things — which is what makes the refusal a refusal rather than a reroll.
    /// </para>
    /// </remarks>
    /// <param name="target">The definition the container was given.</param>
    /// <param name="context">The container, its state, and the party.</param>
    /// <param name="loot">This game's loot, or null when this ruleset cannot generate any.</param>
    internal static InteractionOutcome Search(
        InteractionTargetDefinition target,
        InteractionContext context,
        MightAndMagic7Loot? loot)
    {
        if (target.Verb == InteractionVerb.Unlock)
        {
            return InteractionOutcome.Applied(
                MightAndMagic7Interaction.UnlockedState,
                $"What {target.Name} was locked with is to hand, and the lock falls open.");
        }

        if (string.Equals(target.State, SearchedState, StringComparison.Ordinal))
        {
            return InteractionOutcome.Refused("container-emptied", $"{target.Name} has already been emptied.");
        }

        IReadOnlyList<int> contents = References(context.Placement);
        if (contents.Count == 0)
        {
            return InteractionOutcome.Applied(SearchedState, $"{target.Name} is empty.");
        }

        int placeLevel = context.Placement.Source.GetInt32(MapTreasureLevelField) ?? 0;
        Dictionary<int, int> named = [];
        List<LootItem> found = [];
        int coins = 0;
        for (int slot = 0; slot < contents.Count; slot++)
        {
            int reference = contents[slot];
            if (reference > 0)
            {
                named[reference] = named.GetValueOrDefault(reference) + 1;
                continue;
            }

            // A reference is a request and this is the owner that answers it. Without one, or without a
            // random service to draw through, the request stays unanswered and the search says which
            // reference it could not answer rather than handing over a level's worth of nothing.
            if (loot?.RollsFor(Key(context, slot)) is not { } rolls)
            {
                return InteractionOutcome.Refused(
                    "container-contents-unresolved",
                    $"{target.Name} holds treasure level {-reference} at slot {slot}, and this build has no loot generator to answer it: the request the map recorded is recorded and not answered.");
            }

            LootYield yielded = loot.Reference(-reference, placeLevel, rolls);
            found.AddRange(yielded.Items);
            coins += yielded.Coins;
        }

        List<InteractionItemYield> yields = [];
        List<string> words = [];
        foreach ((int id, int count) in named)
        {
            ItemDefinitionId definition = new(id.ToString(CultureInfo.InvariantCulture));
            yields.Add(new InteractionItemYield(definition, count));
            words.Add(Word(loot, definition, count));
        }

        foreach (IGrouping<ItemDefinitionId, LootItem> group in found.GroupBy(item => item.Definition))
        {
            int count = group.Sum(item => item.Count);
            yields.Add(new InteractionItemYield(group.Key, count));
            words.Add(Word(loot, group.Key, count));
        }

        if (coins > 0) words.Add(string.Create(CultureInfo.InvariantCulture, $"{coins} gold"));
        if (words.Count == 0) return InteractionOutcome.Applied(SearchedState, $"{target.Name} holds nothing after all.");
        return InteractionOutcome.Applied(
            SearchedState,
            $"{target.Name} holds {string.Join(" and ", words)}.",
            items: yields,
            gain: coins > 0 ? PartyCost.OfGold(coins) : PartyCost.Free);
    }

    /// <summary>How one finding reads: the item's name, counted when there is more than one.</summary>
    private static string Word(MightAndMagic7Loot? loot, ItemDefinitionId definition, int count)
    {
        string name = loot is null ? definition.Value : loot.NameOf(definition);
        return count > 1 ? string.Create(CultureInfo.InvariantCulture, $"{count} × {name}") : name;
    }

    /// <summary>What one reference's draw is keyed on: the container, and which slot the reference stood in.</summary>
    /// <remarks>
    /// The slot is what separates two identical references in one container, so a chest holding two requests
    /// for the same level yields two lots rather than two copies of one.
    /// </remarks>
    private static string Key(InteractionContext context, int slot) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"container/{context.Place}/{context.Placement.Content}/{slot}");

    /// <summary>
    /// Everything a container placement's contents are read as: the item references, in slot order.
    /// </summary>
    /// <remarks>
    /// A chest record's identifiers are read from its own contents array; a pile holds the one item its
    /// sprite object carries. Both are read here so the search and the world-build check answer about one
    /// shape rather than about where the items came from.
    /// </remarks>
    internal static IReadOnlyList<int> References(PlacementDefinition placement) =>
        DeclaredReferences(placement.Source.Payload, placement.Content.Kind);

    /// <summary>Every item reference a placement declares, whatever the placement is.</summary>
    /// <param name="placement">The placement element, exactly as content wrote it.</param>
    /// <param name="kind">The placement's own kind, which decides which field carries its contents.</param>
    internal static IReadOnlyList<int> DeclaredReferences(JsonElement placement, string kind)
    {
        if (string.Equals(kind, PilePlacementKind, StringComparison.Ordinal))
        {
            return ReadInt32(placement, ContainingItemField) is { } carried and not 0 ? [carried] : [];
        }

        if (!string.Equals(kind, ContainerPlacementKind, StringComparison.Ordinal)) return [];

        List<int> references = [];
        foreach (JsonElement entry in ReadArray(placement, ContentsField))
        {
            if (ContentEntry.ReadDouble(entry, ItemField) is { } item && item >= int.MinValue && item <= int.MaxValue)
            {
                references.Add((int)item);
            }
        }

        return references;
    }

    /// <summary>
    /// Whether a reference is one the item table can answer, which is what the world build checks.
    /// </summary>
    /// <remarks>
    /// A positive reference must name an item the catalog carries; a negative one must ask for one of the
    /// donor's seven treasure levels, because a reference outside that range is a request nothing can
    /// answer rather than a random item (OpenEnroth <c>src/Engine/Objects/ItemEnums.h:956-963</c>).
    /// </remarks>
    /// <param name="reference">The identifier the map recorded.</param>
    /// <param name="items">The item identities the catalog carries.</param>
    internal static bool Resolves(int reference, IReadOnlySet<string> items) => reference > 0
        ? items.Contains(reference.ToString(System.Globalization.CultureInfo.InvariantCulture))
        : reference is >= FirstRandomReference and <= LastRandomReference;

    /// <summary>Reads a numeric property of a placement element, or null when it is absent or not one.</summary>
    private static int? ReadInt32(JsonElement element, string property) =>
        ContentEntry.ReadDouble(element, property) is { } value && value >= int.MinValue && value <= int.MaxValue
            ? (int)value
            : null;

    /// <summary>
    /// Reads an array property of a placement element, or nothing when it carries none.
    /// </summary>
    /// <remarks>
    /// A placement's nested arrays — what a use requires and what a container holds — are read the same way,
    /// so both the lock policy and the container policy see the same shape rather than each having its own
    /// reading of a malformed element. A property that is not an array is nothing, and the caller's own
    /// validation is what names a content defect.
    /// </remarks>
    internal static IReadOnlyList<JsonElement> ReadArray(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(property, out JsonElement value) &&
        value.ValueKind == JsonValueKind.Array
            ? [.. value.EnumerateArray()]
            : [];

    /// <summary>What a trap of the place's own damage takes from every member.</summary>
    /// <remarks>
    /// The donor's amount is five points plus as many twenty-sided dice as the map's row declares
    /// (<c>src/Engine/Objects/SpriteObject.cpp:518-520</c>). With no owner of randomness in this build the
    /// dice are taken at their mean — ten and a half per die, rounded down — so the same trap always costs
    /// the same, and a test can state what a sprung trap costs.
    /// </remarks>
    private static int TrapDamage(int dice) => TrapBaseDamage + ((dice * (TrapDieSides + 1)) / 2);

    /// <summary>The best level of a skill in the party, which is what a band brings as one.</summary>
    private static int BestSkill(PartyEntity? party, string skill)
    {
        if (party is null) return 0;
        SkillId id = new(skill);
        int best = 0;
        foreach (PartyMember member in party.Members) best = Math.Max(best, member.Skills.LevelOf(id));
        return best;
    }
}
