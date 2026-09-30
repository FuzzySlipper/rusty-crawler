using System.Text;
using PartyRpg.Kit.Input;
using PartyRpg.Kit.Services;
using Rusty.Engine;

namespace PartyRpg.Kit.Sessions;

/// <summary>The service controls a product declares, by the names a player's commands arrive on.</summary>
/// <remarks>
/// <para>
/// Names are data rather than vocabulary, exactly as the movement, creation, save, and use controls are:
/// the kit claims what a product declares and invents no key of its own. Two names are needed because a
/// product offers two ways to act at a counter — a digital intent for a key that leaves the service, and
/// the payload contract the screen sends its commands on — and both are read inside the one admitted
/// update, so a key and a button ask for exactly the same thing.
/// </para>
/// <para>
/// Entering a service is deliberately not one of these controls. A party enters by using the person the
/// interaction mechanism reached, on the use control the product already declares, so there is one way to
/// walk up to somebody rather than two.
/// </para>
/// </remarks>
public sealed record ServiceIntentNames
{
    /// <summary>Creates the declared service control names.</summary>
    /// <param name="leave">The intent a request to leave the counter arrives on.</param>
    /// <param name="actionContract">The payload contract the screen's service commands arrive on.</param>
    /// <exception cref="ArgumentException">A name is missing, so no event could ever be claimed for it.</exception>
    public ServiceIntentNames(string leave, string actionContract)
    {
        Leave = Require(leave, nameof(leave));
        ActionContract = Require(actionContract, nameof(actionContract));
    }

    /// <summary>The intent that leaves the counter the party stands at.</summary>
    public string Leave { get; }

    /// <summary>The payload contract the screen's service commands arrive on.</summary>
    public string ActionContract { get; }

    private static string Require(string name, string parameterName) =>
        !string.IsNullOrWhiteSpace(name)
            ? name
            : throw new ArgumentException(
                $"The service control '{parameterName}' declares no name, so no event could ever be claimed for it.",
                parameterName);
}

/// <summary>
/// The actions a service screen sends, on the payload contract the product declares.
/// </summary>
/// <remarks>
/// These are wire names, not rules: the screen reports what the player asked the counter for, the session
/// reads it into a command, and the mechanism decides whether it is legal and what it costs. A name here
/// that the session never reads is a control that does nothing, which is why every one of them is exercised
/// by the suite.
/// </remarks>
public static class ServiceActions
{
    /// <summary>Takes a line of the shelves, carrying the lot and how many.</summary>
    public const string Buy = "service.buy";

    /// <summary>Gives the counter one of the party's items, carrying the instance.</summary>
    public const string Sell = "service.sell";

    /// <summary>Pays to learn what an item is, carrying the instance.</summary>
    public const string Identify = "service.identify";

    /// <summary>Pays to have an item repaired, carrying the instance.</summary>
    public const string Repair = "service.repair";

    /// <summary>Pays for a lesson, carrying its subject, its rung, and the member it goes to.</summary>
    /// <remarks>
    /// The rung is carried because a counter can teach one skill at more than one rung: the row a player
    /// pressed is the row the command names, and a command that leaves it out means the first.
    /// </remarks>
    public const string Teach = "service.teach";

    /// <summary>
    /// Pays for a passage a counter sells, carrying the place the passage reaches.
    /// </summary>
    /// <remarks>
    /// A passage is bought by naming where it goes rather than by naming the counter: a stable sells several
    /// journeys and the destination is what tells them apart. What the counter does with the request is its
    /// own — it settles the fare, writes the passage on the party, and hands the party to the road — and this
    /// action only names the journey, so a screen decides nothing about either the price or the travel.
    /// </remarks>
    public const string Fare = "service.fare";

    /// <summary>Pays a counter to train one member a level, carrying the member it goes to.</summary>
    /// <remarks>
    /// A training step names a member rather than a target: what is bought is the member's next level at the
    /// counter the party stands at, and the hall's own ceiling and fee are the ruleset's answers about this
    /// member. Without this action the one mechanism could train nobody from the screen, which is what left
    /// the shipped halls unusable before it.
    /// </remarks>
    public const string Train = "service.train";

    /// <summary>Leaves the counter, ending the visit.</summary>
    public const string Leave = "service.leave";
}

/// <summary>The service commands a player gave, read from the admitted input of each update, in the order they arrived.</summary>
public sealed class ServiceInput
{
    private static readonly HashSet<string> Known = new(StringComparer.Ordinal)
    {
        ServiceActions.Buy, ServiceActions.Sell, ServiceActions.Identify, ServiceActions.Repair,
        ServiceActions.Teach, ServiceActions.Train, ServiceActions.Fare, ServiceActions.Leave,
    };

    private readonly byte[] _leave;
    private readonly string _actionContract;

    /// <summary>Creates the reader for the declared service controls.</summary>
    /// <param name="names">The service controls the host declared.</param>
    public ServiceInput(ServiceIntentNames names)
    {
        ArgumentNullException.ThrowIfNull(names);
        _leave = Encoding.UTF8.GetBytes(names.Leave);
        _actionContract = names.ActionContract;
    }

    /// <summary>The service commands this update carried.</summary>
    /// <param name="inbox">The update's input.</param>
    public IReadOnlyList<ServiceCommand> Read(ActionInbox inbox)
    {
        ArgumentNullException.ThrowIfNull(inbox);
        List<ServiceCommand> commands = [];
        if (inbox.Activated(_leave)) commands.Add(ServiceCommand.Of(ServiceCommandKind.Leave));
        foreach (UiAction action in inbox.Take(_actionContract, Known.Contains))
        {
            if (Command(action) is { } command) commands.Add(command);
        }

        return commands;
    }

    private static ServiceCommand? Command(UiAction action)
    {
        string target = action.Text("target");
        return action.Name switch
        {
            ServiceActions.Buy => new ServiceCommand(ServiceCommandKind.Buy, target, Count: action.Int("count") ?? 1),
            ServiceActions.Sell => new ServiceCommand(ServiceCommandKind.Sell, target),
            ServiceActions.Identify => new ServiceCommand(ServiceCommandKind.Identify, target),
            ServiceActions.Repair => new ServiceCommand(ServiceCommandKind.Repair, target),
            ServiceActions.Teach => new ServiceCommand(
                ServiceCommandKind.Teach,
                target,
                action.Int("member") ?? 0,
                Tier: action.Int("tier") is { } tier && tier > 0 ? tier : 1),
            ServiceActions.Train => new ServiceCommand(ServiceCommandKind.Train, Member: action.Int("member") ?? 0),
            ServiceActions.Fare => new ServiceCommand(ServiceCommandKind.Fare, target),
            ServiceActions.Leave => ServiceCommand.Of(ServiceCommandKind.Leave),
            _ => null,
        };
    }
}
