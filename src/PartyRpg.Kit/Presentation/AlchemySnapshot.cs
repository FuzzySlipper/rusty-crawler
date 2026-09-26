using PartyRpg.Kit.Alchemy;
using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Presentation;

/// <summary>One thing in the pack that some mixture the game states takes part in, as the panel shows it.</summary>
/// <remarks>
/// The row names the <em>instance</em> rather than the kind, because two potions of one kind are two things at
/// two strengths and the mixture a player asks for names which of them is spent. The strength is published
/// because it is what the pack actually holds: a potion mixed from a dragon's eye and one mixed from a berry
/// are the same drink at fifty and at one, and a panel showing only names would leave them looking alike.
/// </remarks>
/// <param name="Item">The instance's durable identity, which a mixture echoes back.</param>
/// <param name="Definition">The content definition the instance is a copy of.</param>
/// <param name="Name">What a person reads for it.</param>
/// <param name="Kind">How the game reads the row: a potion, a reagent, the bottle, or the catalyst.</param>
/// <param name="Potency">How strong the instance is, zero when nothing has stated a strength for it.</param>
/// <param name="Count">How many of the definition the instance carries.</param>
public readonly record struct AlchemyItemSnapshot(
    string Item,
    string Definition,
    string Name,
    string Kind,
    int Potency,
    int Count);

/// <summary>One mixture the pack currently offers, as the panel shows it.</summary>
/// <remarks>
/// <para>
/// A row exists for every pair of things in the pack that the game's own table states a mixture for, because
/// that is what a mixing screen can offer: the two things a player has, and nothing else. It deliberately says
/// nothing about what the pair will do — a result, a burst, or nothing at all — because the game's own table
/// is what a player is meant to learn, and the discovery a mixture records belongs to whatever keeps what the
/// party knows rather than to the screen that offered it.
/// </para>
/// <para>
/// The rung is not published either, for the same reason: a pair whose result asks for a mastery the character
/// has not reached is refused by name where the attempt is made, which is where a player is told what would
/// raise it.
/// </para>
/// </remarks>
/// <param name="First">One ingredient, as the pack holds it.</param>
/// <param name="Second">The other ingredient, as the pack holds it.</param>
/// <param name="FirstName">What a person reads for the first.</param>
/// <param name="SecondName">What a person reads for the second.</param>
public readonly record struct AlchemyMixtureSnapshot(
    string First,
    string Second,
    string FirstName,
    string SecondName);

/// <summary>What the last mixture did, or why nothing was mixed, as the panel shows it.</summary>
/// <remarks>
/// Every field is a reading of the workflow's own answer rather than a sentence the panel parses: the outcome
/// is one of the three the game's table can state, the result and its strength are what the pack now holds,
/// and a burst carries what it took. A refusal carries its code and its sentence, so a screen can show what
/// blocked the mixture — a mastery, a pack with no room, an ingredient that is not there — without knowing
/// which of them it was.
/// </remarks>
/// <param name="Member">The mixing character's place in the party, counted from zero.</param>
/// <param name="Mixer">What the mixing character is called, empty when the party has no such member.</param>
/// <param name="Outcome">What came of the attempt: <c>potion</c>, <c>burst</c>, <c>nothing</c>, or <c>refused</c>.</param>
/// <param name="Result">The definition of what the mixture made, empty when it made nothing.</param>
/// <param name="ResultName">What the mixture made is called, empty when it made nothing.</param>
/// <param name="Power">The strength the result came out at, zero when nothing was made.</param>
/// <param name="Burst">How strong a burst the mixture was, zero when it did not burst.</param>
/// <param name="Harm">What the burst took from the mixing character, zero when it took nothing.</param>
/// <param name="Condition">The condition the burst left, empty when it left none.</param>
/// <param name="Note">The discovery the mixture records, zero when the game states none.</param>
/// <param name="Code">The outcome's own code, which a screen may key on.</param>
/// <param name="Message">What happened, in a sentence a person reads.</param>
public readonly record struct AlchemyOutcomeSnapshot(
    int Member,
    string Mixer,
    string Outcome,
    string Result,
    string ResultName,
    int Power,
    int Burst,
    int Harm,
    string Condition,
    int Note,
    string Code,
    string Message);

/// <summary>One character, as the pack screen's own chooser names them.</summary>
/// <remarks>
/// Who mixes is a character's own fact — the mastery the result asks for is theirs — so the screen has to be
/// able to name one. The rows are the party's own order, which is the order a mixture's member index is
/// counted in, so a screen sends back exactly the index it drew.
/// </remarks>
/// <param name="Index">The character's place in the party, counted from zero.</param>
/// <param name="Member">The character's durable identity.</param>
/// <param name="Name">What the character is called.</param>
/// <param name="Alchemy">How far up the mixing skill's ladder they stand, as this game counts rungs.</param>
public readonly record struct AlchemyMemberSnapshot(int Index, string Member, string Name, int Alchemy);

/// <summary>The alchemy the pack screen shows: what can be mixed, and what the last mixture did.</summary>
/// <remarks>
/// <para>
/// The rows are read from the parties' own pack and the game's own mixture table, so a screen never keeps a
/// list of recipes of its own: a pair appears because the table states it and because both things are lying in
/// the pack, and it disappears the moment one of them is spent. A session whose ruleset answered no mixtures
/// publishes <see cref="None"/> and the panel says so rather than offering a control nothing could carry out.
/// </para>
/// <para>
/// Nothing here is the panel's own arithmetic: the pair's components are the pack's own instances, the names
/// are the game's own words for their definitions, and the outcome is the workflow's answer read straight
/// through.
/// </para>
/// </remarks>
/// <param name="Available">Whether the session holds a mixing workflow at all.</param>
/// <param name="Members">The party's own characters, which is who a mixture may be asked of.</param>
/// <param name="Items">The pack's own things that take part in a mixture, in the order the pack holds them.</param>
/// <param name="Mixtures">The pairs the pack currently offers, in pack order.</param>
/// <param name="Outcome">What the last mixture did, or null when nothing has been mixed.</param>
public readonly record struct AlchemySnapshot(
    bool Available,
    IReadOnlyList<AlchemyMemberSnapshot> Members,
    IReadOnlyList<AlchemyItemSnapshot> Items,
    IReadOnlyList<AlchemyMixtureSnapshot> Mixtures,
    AlchemyOutcomeSnapshot? Outcome)
{
    /// <summary>The alchemy of a session that holds no mixing workflow.</summary>
    public static AlchemySnapshot None => new(false, [], [], [], null);

    /// <summary>Reads the pack screen's alchemy out of the mixing workflow, or nothing when it holds none.</summary>
    /// <param name="mixing">The session's mixing workflow, or null when it composes none.</param>
    /// <returns>The alchemy the pack screen shows.</returns>
    public static AlchemySnapshot From(PotionMixing? mixing)
    {
        if (mixing is not { } owner) return None;

        AlchemyCatalog catalog = owner.Catalog;

        // Who may mix is a character's own mastery, so the rows are the party's own order and the rung each
        // character stands at: a screen that showed only names would leave a player unable to tell which of
        // the band can make the potion they are looking at.
        List<AlchemyMemberSnapshot> members = [];
        for (int index = 0; index < owner.Party.Members.Count; index++)
        {
            PartyMember member = owner.Party.Members[index];
            members.Add(new AlchemyMemberSnapshot(index, member.Id.ToString(), member.Profile.Name, member.Skills.TierOf(owner.Rule.Skill).Value));
        }

        List<AlchemyItemSnapshot> items = [];
        foreach (ItemInstance item in owner.Party.Items)
        {
            // Only what some stated mixture takes part in is published: a list of everything the party carries
            // would be the pack screen, which the party's own snapshot already owns.
            if (catalog.With(item.Definition).Count == 0) continue;
            items.Add(new AlchemyItemSnapshot(
                item.Id.ToString(),
                item.Definition.Value,
                owner.Rule.NameOf(item.Definition),
                KindOf(owner.Rule, item.Definition),
                item.State.Potency,
                item.StackCount));
        }

        List<AlchemyMixtureSnapshot> mixtures = [];
        for (int first = 0; first < items.Count; first++)
        {
            for (int second = first + 1; second < items.Count; second++)
            {
                if (catalog.Find(new ItemDefinitionId(items[first].Definition), new ItemDefinitionId(items[second].Definition)) is null) continue;
                mixtures.Add(new AlchemyMixtureSnapshot(
                    items[first].Item,
                    items[second].Item,
                    items[first].Name,
                    items[second].Name));
            }
        }

        AlchemyOutcomeSnapshot? outcome = owner.Last is { } last
            ? new AlchemyOutcomeSnapshot(
                last.Member,
                last.Mixer,
                last.Outcome,
                last.Result,
                last.ResultName,
                last.Power,
                last.Burst,
                last.Harm,
                last.Condition,
                last.Note,
                last.Code,
                last.Message)
            : null;

        return new AlchemySnapshot(true, members, items, mixtures, outcome);
    }

    /// <summary>
    /// What kind of row a definition is, as the panel's wire spells it.
    /// </summary>
    /// <remarks>
    /// The kit's own mixing workflow states nothing about kinds — a mixture is a pair of definitions and that
    /// is all it needs — so the kind is the game's own answer through <see cref="IAlchemyKinds"/>, which is
    /// where a screen learns that this thing is a bottle and that one is a reagent without either of them
    /// being a kit concept. A game that answers no kinds leaves the cell empty rather than inventing a word.
    /// </remarks>
    private static string KindOf(IAlchemyRule rule, ItemDefinitionId definition) =>
        rule is IAlchemyKinds kinds ? kinds.KindOf(definition) : string.Empty;
}

/// <summary>What kind of thing a definition is, for a screen that names the rows it draws.</summary>
/// <remarks>
/// This is deliberately small and optional: the mixing workflow needs nothing but the pair, and a ruleset that
/// reads no kinds answers nothing and the panel shows an empty cell rather than an invented word.
/// </remarks>
public interface IAlchemyKinds
{
    /// <summary>How this game reads a definition's row: a potion, a reagent, a bottle, or a catalyst.</summary>
    /// <param name="definition">The definition to read.</param>
    /// <returns>The kind's word, empty when this game states none.</returns>
    string KindOf(ItemDefinitionId definition);
}
