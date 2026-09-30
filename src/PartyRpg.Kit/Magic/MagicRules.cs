using PartyRpg.Kit.Sessions;

namespace PartyRpg.Kit.Magic;

/// <summary>A game's answers about spells, and each capability of the path their effects take, named.</summary>
/// <remarks>
/// A casting and its panel are composed over the capabilities a game states and over nothing they would have to
/// discover by asking the effect path what else it is: a capability left out is one they do without, and a
/// wrapper around a rule cannot silently drop one it did not forward. One object may answer several of these —
/// a game's effect path usually keeps the running effects, the aims, and the sight together — and is then
/// handed over under each name.
/// </remarks>
/// <param name="Spells">What each spell costs, requires, and aims at.</param>
/// <param name="Effects">What a spell that lands does, or null for a game whose spells change nothing yet.</param>
/// <param name="Running">The effects spells leave running on the one clock, which the panel lists.</param>
/// <param name="Time">The part of the effect path that hears the one clock, so a ward ends when its time does.</param>
/// <param name="Aim">What a spell whose aim names no actor may be pointed at.</param>
/// <param name="Members">The effects spells leave on single characters rather than on the party.</param>
/// <param name="Sight">How far the party sees in the dark, which a light spell changes.</param>
/// <param name="Items">Which items hold a spell, and how a scroll or a wand spends it.</param>
public sealed record MagicRules(
    ISpellRule Spells,
    ISpellEffectRule? Effects = null,
    IRunningSpellEffects? Running = null,
    IGameTimeObserver? Time = null,
    ISpellAimRule? Aim = null,
    IMemberSpellEffects? Members = null,
    IPartySightRule? Sight = null,
    ISpellItemRule? Items = null);
