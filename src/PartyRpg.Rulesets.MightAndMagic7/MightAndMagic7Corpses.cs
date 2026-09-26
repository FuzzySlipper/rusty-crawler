using System.Globalization;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Loot;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.World;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>
/// This game's corpses: what a creature the party brought down leaves, what that death held, and what
/// searching it yields.
/// </summary>
/// <remarks>
/// <para>
/// <b>A body is searched through the container mechanism.</b> It is described as the same kind of target a
/// chest is, with the same verb, the same reach, the same requirement and trap vocabulary, and the same
/// transfer into the party's one shared pack: what differs is only where its contents came from, which is a
/// death rather than a map record. A second search workflow for bodies would be a second place for the
/// party's inventory to be written, which is exactly what the container mechanism exists to prevent.
/// </para>
/// <para>
/// <b>What a death left is decided when it falls.</b> The fight reports what it read as down, and this
/// generates each new death's loot once, under a key that names the place, the creature, and which death it
/// was, and hands it to the body that holds it. Searching a body therefore hands over what it holds rather
/// than rolling anything: a search the party's pack could not take leaves the body exactly as full as it
/// was, and the retry finds the same lot. That is the donor's own reading — an actor's coin and item are
/// settled when it dies (<c>src/Engine/Objects/Actor.cpp:192-212</c>) — and it is what makes "a corpse's
/// loot is not generated twice" a fact rather than a promise.
/// </para>
/// <para>
/// <b>A body is named for what it was.</b> The name comes from the fight's own naming of the creature, so a
/// body reads as the thing the party killed rather than as a row id, and two bodies of two creatures are two
/// named things.
/// </para>
/// </remarks>
internal sealed class MightAndMagic7Corpses : IFallenCreatureObserver, ICorpseSource
{
    private readonly CorpseGround _ground;
    private readonly MightAndMagic7Loot _loot;

    /// <summary>Creates this game's corpse answers over the ground the fight reports to.</summary>
    /// <param name="ground">What the fight's readings are kept in, which is also what holds what a death left.</param>
    /// <param name="loot">This game's loot, which generation is asked of.</param>
    /// <exception cref="ArgumentNullException">No ground or no loot was supplied.</exception>
    internal MightAndMagic7Corpses(CorpseGround ground, MightAndMagic7Loot loot)
    {
        _ground = ground ?? throw new ArgumentNullException(nameof(ground));
        _loot = loot ?? throw new ArgumentNullException(nameof(loot));
    }

    /// <summary>The ground this game keeps its bodies in, which the panel and a suite read.</summary>
    internal CorpseGround Ground => _ground;

    /// <inheritdoc />
    public IReadOnlyList<Corpse> Observe(PlaceId place, IReadOnlyList<FallenCreature> fallen)
    {
        IReadOnlyList<Corpse> bodies = _ground.Observe(place, fallen);
        foreach (Corpse body in bodies)
        {
            // A body that already holds something keeps it: this reading happens every update, and a death
            // that was generated for once must not be rolled again on the next one.
            if (_ground.Held(body) is not null) continue;

            // Without a random service nothing can be drawn, and the body holds nothing: that is a product
            // that cannot generate loot rather than a body whose loot is a promise to roll later.
            if (_loot.RollsFor(Key(body)) is not { } rolls) continue;
            _ground.Hold(body, _loot.Death(body.Body, rolls));
        }

        return bodies;
    }

    /// <inheritdoc />
    public IReadOnlyList<PlacementDefinition> CorpsesOf(PlaceId place) =>
        [.. _ground.In(place).Select(body => body.Body)];

    /// <summary>What a placement offers when something is lying at it, or null when nothing is.</summary>
    /// <remarks>
    /// The state word travels with the answer rather than being recomputed: a body the party has already
    /// searched reads as a container that has been emptied, through the same word a chest uses, so the refusal
    /// a second search gets is the container mechanism's own.
    /// </remarks>
    /// <param name="request">The placement, its place, and what the party has already done to it.</param>
    /// <returns>The body as a container, or null when the placement is not a body.</returns>
    internal InteractionTargetDefinition? Describe(InteractionTargetRequest request) =>
        _ground.At(request.Place, request.Placement.Content) is { } body
            ? new InteractionTargetDefinition(
                new InteractionTargetKind(MightAndMagic7Containers.TargetKind),
                $"The body of {body.Name}",
                InteractionVerb.Search,
                MightAndMagic7Interaction.Reach,
                request.State)
            : null;

    /// <summary>What searching a body yields, given that nothing guards the dead.</summary>
    /// <param name="target">The body as the ruleset described it.</param>
    /// <param name="context">The body, its state, and the party.</param>
    /// <returns>What the search gave, or why it gave nothing.</returns>
    internal InteractionOutcome Search(InteractionTargetDefinition target, InteractionContext context)
    {
        if (_ground.At(context.Place, context.Placement.Content) is not { } body)
        {
            // The mechanism re-validates what it faces before a use reaches here, so a body that is gone is a
            // creature standing up again between the two reads: a refusal, not a defect.
            return InteractionOutcome.Refused(
                "corpse-gone",
                $"{target.Name} is not lying there any more: what the party was searching has got up or gone.");
        }

        if (string.Equals(target.State, MightAndMagic7Containers.SearchedState, StringComparison.Ordinal))
        {
            return InteractionOutcome.Refused("container-emptied", $"{target.Name} has already been emptied.");
        }

        LootYield? held = _ground.Held(body);
        if (held is null || held.IsEmpty)
        {
            return InteractionOutcome.Applied(
                MightAndMagic7Containers.SearchedState,
                $"{target.Name} holds nothing{(held is null && !_loot.CanGenerate ? ", and this build cannot draw for it" : string.Empty)}.");
        }

        // Identical findings are one yield of that many, which is what a stack is: nothing about a death
        // tells two copies of one item apart, so two of them are two of that item.
        List<InteractionItemYield> yields = [];
        foreach (IGrouping<ItemDefinitionId, LootItem> group in held.Items.GroupBy(item => item.Definition))
        {
            yields.Add(new InteractionItemYield(group.Key, group.Sum(item => item.Count)));
        }

        string found = string.Join(
            " and ",
            held.Items
                .GroupBy(item => item.Definition)
                .Select(group => group.Sum(item => item.Count) is var count and > 1
                    ? string.Create(CultureInfo.InvariantCulture, $"{count} × {_loot.NameOf(group.Key)}")
                    : _loot.NameOf(group.Key))
                .Concat(held.Coins > 0 ? [string.Create(CultureInfo.InvariantCulture, $"{held.Coins} gold")] : Array.Empty<string>()));
        return InteractionOutcome.Applied(
            MightAndMagic7Containers.SearchedState,
            $"{target.Name} holds {found}.",
            items: yields,
            gain: held.Coins > 0 ? PartyCost.OfGold(held.Coins) : PartyCost.Free);
    }

    /// <summary>What one death's generation is keyed on: the place, the creature, and which death it was.</summary>
    /// <remarks>
    /// The serial is what makes two deaths at one placement two lots of loot rather than one: a creature the
    /// party kills in one visit and kills again in the next is a different body, and the key says so.
    /// </remarks>
    private static string Key(Corpse body) =>
        string.Create(CultureInfo.InvariantCulture, $"corpse/{body.Place}/{body.Content}/{body.Serial}");
}
