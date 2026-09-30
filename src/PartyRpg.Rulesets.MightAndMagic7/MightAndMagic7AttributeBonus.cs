namespace PartyRpg.Rulesets.MightAndMagic7;

/// <summary>What an attribute score is worth, as one table prices every attribute.</summary>
/// <remarks>
/// OpenEnroth <c>src/Engine/Objects/Character.cpp:234-240</c> (<c>param_to_bonus_table</c> and
/// <c>parameter_to_bonus_value</c>), read by <c>GetParameterBonus</c> (<c>:2041-2049</c>): the first floor a
/// score reaches is its bonus, from thirty at 500 down to minus six, and a score below every floor — a score
/// under three, or one a curse drove below zero — is worth minus six too. Faithful: the values are the donor's.
/// Everything this game derives from an attribute — a recovery, a hit chance, a damage bonus, a saving throw,
/// a death threshold, a spell pool — reads it here.
/// </remarks>
internal static class MightAndMagic7AttributeBonus
{
    private static readonly (int Floor, int Bonus)[] Steps =
    [
        (500, 30), (400, 25), (350, 20), (300, 19), (275, 18), (250, 17), (225, 16), (200, 15),
        (175, 14), (150, 13), (125, 12), (100, 11), (75, 10), (50, 9), (40, 8), (35, 7), (30, 6),
        (25, 5), (21, 4), (19, 3), (17, 2), (15, 1), (13, 0), (11, -1), (9, -2), (7, -3), (5, -4),
        (3, -5), (0, -6),
    ];

    /// <summary>What one attribute score is worth.</summary>
    /// <param name="score">The attribute score.</param>
    internal static int Of(int score)
    {
        foreach ((int floor, int bonus) in Steps)
        {
            if (score >= floor) return bonus;
        }

        return Steps[^1].Bonus;
    }
}
