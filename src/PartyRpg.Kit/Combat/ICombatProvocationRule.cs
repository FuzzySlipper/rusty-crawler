namespace PartyRpg.Kit.Combat;

/// <summary>
/// A ruleset's answer about who else an act against one creature turns against the party.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why it is its own seam.</b> The fight remembers what the party has done to each creature: an attack, a spell
/// that is an act against it, or being caught stealing from it puts that creature into the fight for as long as it
/// stands there. A game may let that act reach further — those who stand with the one wronged and saw it take its
/// part — and which of them do is a question about two actors at once that no answer about one actor's own nature
/// can state. A game that answers nothing here provokes only the creature acted against.
/// </para>
/// <para>
/// <b>The fight asks it wherever it provokes.</b> Every way the fight remembers a provocation asks this about every
/// other standing actor that is not on the party's side, and remembers each one the answer names exactly as it
/// remembers the creature acted against, so a creature provoked this way is the party's enemy for as long as it
/// stands there and a spell that makes it stand with the party still outranks it.
/// </para>
/// </remarks>
public interface ICombatProvocationRule
{
    /// <summary>Whether an act against one creature also turns another against the party.</summary>
    /// <param name="provoked">The creature the party acted against.</param>
    /// <param name="bystander">Another standing actor of the place, not on the party's side.</param>
    /// <returns>Whether the bystander is provoked with it.</returns>
    bool ProvokedWith(CombatSubject provoked, CombatSubject bystander);
}
