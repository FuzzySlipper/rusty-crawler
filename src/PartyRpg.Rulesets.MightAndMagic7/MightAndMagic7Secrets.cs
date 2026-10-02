using PartyRpg.Kit;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Knowledge;
using PartyRpg.Kit.Party;

namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>This game's explicit-use adaptation of secret-face highlighting.</summary>
/// <remarks>
/// The donor highlights FACE_IsSecret when an acting member's Perception reaches twice the map difficulty
/// (OpenEnroth Party.cpp:168-177, Character.cpp:599-610, Engine.cpp:135 and OpenGLRenderer.cpp:2514).
/// Here a first ordinary use discovers it, and a second uses the same door/event. The discovery is a place
/// value in the existing interaction ledger, rather than a rendering effect or another target scene.
/// </remarks>
internal static class MightAndMagic7Secrets
{
    internal const string SecretField = "secret";
    internal const string DifficultyField = "perceptionDifficulty";
    internal const string DiscoveryPrefix = "secret-discovered:";
    private static readonly SkillId Perception = new("Perception");

    internal static InteractionOutcome? Discover(InteractionTargetDefinition target, InteractionContext context)
    {
        if (context.Raised.Length > 0 || context.Placement.Source.GetBoolean(SecretField) != true) return null;
        string key = DiscoveryPrefix + context.Placement.Content;
        if (context.PlaceValues.GetValueOrDefault(key) != 0) return null;
        int required = checked(2 * context.Placement.Source.GetInt32(DifficultyField)!.Value);
        int best = 0;
        foreach (PartyMember member in context.Party?.Members ?? [])
        {
            if (!MightAndMagic7Conditions.CanAct(member)) continue;
            int tier = member.Skills.TierOf(Perception).Value;
            int value = tier >= 4 ? 10000 : member.Skills.LevelOf(Perception) * tier;
            best = Math.Max(best, value);
        }

        if (best < required)
        {
            return InteractionOutcome.Refused(new Refusal(MightAndMagic7Codes.SecretNotDiscovered,
                $"The party cannot discover this secret surface: Perception {required} is needed; its best acting member has {best}."));
        }

        return InteractionOutcome.Applied(
            target.Kind.Value == MightAndMagic7Interaction.DoorTargetKind ? target.State : "discovered",
            $"The party discovered a secret surface with Perception {best}. Use it again to {target.Verb.ToString().ToLowerInvariant()} it.",
            kept: new Dictionary<string, long> { [key] = 1 },
            learned: [new KnowledgeReport(KnowledgeKind.Clue, "Perception", context.Placement.Content.Id, "a secret surface", context.Place.Value)]);
    }

    /// <summary>A current save may remember only a real secret placement, discovered once.</summary>
    internal static string? Judge(string key, long value, IReadOnlyList<PartyRpg.Kit.World.PlacementDefinition> targets)
    {
        if (value != 1) return "a discovered secret surface must carry 1";
        return targets.Any(target => target.Content.ToString() == key[DiscoveryPrefix.Length..] &&
            target.Source.GetBoolean(SecretField) == true)
            ? null : "the discovery names no secret surface in this place";
    }
}
