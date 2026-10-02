using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Sessions;

namespace PartyRpg.Kit.World;

/// <summary>Hands the fight's completed death notification to the world's place memory.</summary>
public sealed class WorldDeaths(Func<SessionWorld?> world) : ICreatureDeathObserver
{
    /// <inheritdoc />
    public void Died(CreatureDeath death) => world()?.Died(death);
}
