using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Alchemy;

/// <summary>What a failed mixture does to the character who attempted it, as the game states it.</summary>
/// <remarks>
/// <para>
/// A mixture that bursts takes something from whoever was mixing it, and the game's own table says what: a
/// strength, and the harm that strength is worth. The number is stated rather than derived because the donor
/// states one per strength and rolls it — a burst of the first strength costs a handful of hit points while
/// the deepest strength eradicates the character outright — so what this carries is the game's answer with
/// whatever it rolled already in it.
/// </para>
/// <para>
/// <b>The condition is the deeper consequence.</b> A burst that leaves a condition rather than a wound is
/// stated here as that condition, because a game whose deepest failure is not damage at all should not have
/// to express it as a large number of hit points.
/// </para>
/// </remarks>
/// <param name="Harm">How much harm the burst does, which may be zero when the consequence is a condition.</param>
/// <param name="Label">What the harm is called, in the game's own word for it.</param>
/// <param name="Condition">The condition the burst leaves, or null when it leaves none.</param>
public sealed record MixtureBackfire(int Harm, string Label, ConditionId? Condition = null)
{
    /// <summary>What the burst does, in a sentence a person reads.</summary>
    /// <returns>The sentence.</returns>
    public string Describe() => Condition is { } condition
        ? $"{Harm} {Label} and {condition}"
        : $"{Harm} {Label}";
}

/// <summary>
/// What this game answers about mixing: which skill judges a mixture, what a mixture's result is worth, and
/// what a mixture that bursts costs.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the seam, not the mixture table.</b> Which pairs combine and what each pair does is content and
/// arrives through <see cref="AlchemyCatalog"/>; everything here is the game's policy over those rows — the
/// skill whose mastery a row's tier is judged against, the arithmetic a result's strength comes from, the
/// harm a burst is worth, and the words a refusal and a report are written in. A game with a different ladder
/// answers different words and changes nothing about the workflow.
/// </para>
/// <para>
/// <b>Strength is the game's arithmetic, not a field.</b> Rows state what a pair makes; what that thing is
/// worth when it comes out of the mixture is this game's own reading — the donor adds the mixer's skill to the
/// power a reagent's row states, averages the strengths of two things, and lets one ingredient's own strength
/// replace the mixture's. All of those need the ingredients themselves and not only their definitions, which
/// is why the two instances travel here rather than a number the workflow decided.
/// </para>
/// </remarks>
public interface IAlchemyRule
{
    /// <summary>The skill whose mastery a mixture's tier is judged against.</summary>
    SkillId Skill { get; }

    /// <summary>What one rung of that skill's ladder is called, in the game's own words.</summary>
    /// <param name="tier">The rung to name.</param>
    /// <returns>The rung's name.</returns>
    string RungName(SkillTier tier);

    /// <summary>
    /// What raises a character's mastery of the mixing skill, said the way the game says it.
    /// </summary>
    /// <remarks>
    /// A refusal that only names the rung a mixture asks for leaves a player knowing what blocked them and not
    /// what would unblock them. The kit states the refusal and this supplies the sentence, exactly as the
    /// ruleset supplies the words for every other refusal in this game.
    /// </remarks>
    /// <param name="skill">The skill whose mastery is short.</param>
    /// <returns>What raises it, as a clause a refusal appends.</returns>
    string MasteryRaisedBy(SkillId skill);

    /// <summary>What a person reads for a definition, which a refusal and a report name it by.</summary>
    /// <param name="definition">The definition to name.</param>
    /// <returns>The name, or the definition's own identity when the game states none.</returns>
    string NameOf(ItemDefinitionId definition);

    /// <summary>Whether this character may mix at all, or why they may not.</summary>
    /// <remarks>
    /// A game asks this before it lets anybody handle an item at all: a character asleep, paralyzed, laid out,
    /// dead, petrified, or eradicated is in no condition to mix anything. Which of a game's own conditions
    /// that is, is the game's answer.
    /// </remarks>
    /// <param name="mixer">The character who would mix.</param>
    /// <returns>The refusal, or null when they may.</returns>
    PartyRefusal? MayMix(PartyMember mixer);

    /// <summary>The strength the mixture's result comes out at, as this game's own arithmetic reads it.</summary>
    /// <param name="mixer">The character mixing.</param>
    /// <param name="mixture">The mixture being attempted.</param>
    /// <param name="first">One ingredient instance, with its own state.</param>
    /// <param name="second">The other ingredient instance, with its own state.</param>
    /// <returns>The strength, which must be at least one when the mixture produces anything.</returns>
    int Strength(PartyMember mixer, PotionMixture mixture, ItemInstance first, ItemInstance second);

    /// <summary>What a burst of a stated strength does to the character who attempted the mixture.</summary>
    /// <param name="strength">How strong the burst is, as the mixture row states it.</param>
    /// <returns>The burst's own answer, with any roll the game makes already in it.</returns>
    MixtureBackfire Backfire(int strength);
}
