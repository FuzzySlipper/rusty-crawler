using System.Text.Json.Serialization;

namespace PartyRpg.Kit.Party;

/// <summary>One property an item bears, with its strength and optional deadline on the game's clock.</summary>
/// <remarks>The property is the ruleset's identity. This value interprets no stat, damage kind or stacking rule.</remarks>
public readonly record struct ItemEnchantment
{
    /// <summary>Creates a property carried by an item.</summary>
    /// <param name="property">The ruleset's property identity.</param>
    /// <param name="strength">The positive strength of the property.</param>
    /// <param name="dueElapsedMilliseconds">Its deadline on the one game clock, or null for a permanent property.</param>
    [JsonConstructor]
    public ItemEnchantment(string property, int strength, long? dueElapsedMilliseconds = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(property);
        ArgumentOutOfRangeException.ThrowIfLessThan(strength, 1);
        if (dueElapsedMilliseconds is { } due) ArgumentOutOfRangeException.ThrowIfNegative(due);
        Property = property;
        Strength = strength;
        DueElapsedMilliseconds = dueElapsedMilliseconds;
    }

    /// <summary>The ruleset's property identity.</summary>
    public string Property { get; }
    /// <summary>The strength the earning rule stated.</summary>
    public int Strength { get; }
    /// <summary>The deadline on the one game clock, or null when this does not expire.</summary>
    public long? DueElapsedMilliseconds { get; }
}
