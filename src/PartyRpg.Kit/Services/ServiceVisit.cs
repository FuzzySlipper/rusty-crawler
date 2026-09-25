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

    /// <summary>The shelves this service keeps, which are the service's own rather than the visit's.</summary>
    internal ServiceShelf Shelf { get; }

    /// <inheritdoc />
    public override string ToString() => Service.Describe();
}
