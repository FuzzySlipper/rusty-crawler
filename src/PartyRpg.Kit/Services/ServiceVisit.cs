namespace PartyRpg.Kit.Services;

/// <summary>One service the party is standing at, with the shelves it is looking over.</summary>
/// <remarks>
/// A visit is the open half of the mechanism's lifecycle: the party has entered a counter and stays there
/// until it leaves. It is deliberately not a mode of the session — it is gameplay state under the running
/// session, and the world keeps its clock and its places while it is open — so what lives here is only
/// which service is being visited and the shelves that belong to it.
/// </remarks>
public sealed class ServiceVisit
{
    internal ServiceVisit(ServiceDefinition service, ServiceShelf shelf)
    {
        Service = service;
        Shelf = shelf;
    }

    /// <summary>The service being visited.</summary>
    public ServiceDefinition Service { get; }

    /// <summary>The amount being quoted for this visit's holding operations; choosing it moves no resources.</summary>
    public int Amount { get; internal set; } = 1;

    /// <summary>Whether this visit has settled a training step, reset by leaving and returning.</summary>
    internal bool HasTrained { get; set; }

    /// <summary>The shelves this service keeps, which are the service's own rather than the visit's.</summary>
    internal ServiceShelf Shelf { get; }

    /// <inheritdoc />
    public override string ToString() => Service.Describe();
}
