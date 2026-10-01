using PartyRpg.Kit.Content;
using PartyRpg.Kit.Input;
using PartyRpg.Kit.Party;
using Rusty.Engine;


namespace PartyRpg.Kit.Sessions;

/// <summary>
/// The creation a session holds while a party is being made: the flow that owns the choices, the factory
/// that builds the accepted party, and the world that party walks into.
/// </summary>
/// <remarks>
/// <para>
/// Both factories are supplied by whoever owns the rules the party obeys, because both are policy: a party
/// is built through the product's one party factory, so the created party and the restored party are the
/// same durable shape, and the world is composed once there is a party whose accounts it charges. That is
/// why a session which creates composes neither until the party is accepted: a world built before its party
/// would have no larder to take a road's provisions from, and a party built twice would be two bands for
/// one expedition.
/// </para>
/// <para>
/// Nothing here mints an identity, advances a clock, or steps anything. The flow refuses what is illegal,
/// the factory builds what the flow finished, and the world is handed the party that was just created.
/// </para>
/// </remarks>
public sealed class SessionCreation
{
    /// <summary>States the creation a session holds.</summary>
    /// <param name="flow">The flow that owns the party being assembled and refuses every illegal choice.</param>
    /// <param name="buildParty">Builds the party a finished flow describes, through the product's one factory.</param>
    /// <param name="play">
    /// Composes what the accepted party plays: the world it walks into and the accounts it settles through. It is
    /// asked once, with the created party, and it is meant to be the same composition a session handed its party
    /// runs — the answer is the very <see cref="SessionParty.Playing"/> such a session starts from, and the session
    /// takes it through the one entry both paths share (<see cref="SessionOwners"/>), so a created party can never
    /// end up with fewer owners than a scenario's. Its world may be null, which is what content that declares no
    /// places gets; its party must be the one it was handed.
    /// </param>
    /// <exception cref="ArgumentNullException">The flow or one of the factories is missing.</exception>
    public SessionCreation(
        PartyCreationFlow flow,
        Func<PartyCreation, PartyEntity> buildParty,
        Func<PartyEntity, SessionParty.Playing> play)
    {
        ArgumentNullException.ThrowIfNull(flow);
        ArgumentNullException.ThrowIfNull(buildParty);
        ArgumentNullException.ThrowIfNull(play);
        Flow = flow;
        BuildParty = buildParty;
        Play = play;
    }

    /// <summary>The flow that owns the party being assembled.</summary>
    public PartyCreationFlow Flow { get; }

    /// <summary>Builds the party a finished flow describes.</summary>
    public Func<PartyCreation, PartyEntity> BuildParty { get; }

    /// <summary>Composes what the accepted party plays: its world and its accounts.</summary>
    public Func<PartyEntity, SessionParty.Playing> Play { get; }
}

/// <summary>
/// Drives a creation from the commands each admitted update carries, until the party is accepted.
/// </summary>
/// <remarks>
/// Every command goes through the flow's own operation and its refusal is kept rather than thrown, so an illegal
/// choice is an answer the screen shows and creation stays exactly where it was. An acceptance is the last
/// command an update acts on: once the party exists there is no flow left to drive, and the commands behind it
/// belonged to a screen that has just gone away.
/// </remarks>
internal sealed class CreationDriver(SessionCreation creation, CreationIntentNames names, SessionDiagnostics diagnostics)
{
    private readonly CreationInput _input = new(names);

    /// <summary>The flow being driven.</summary>
    public PartyCreationFlow Flow => creation.Flow;

    /// <summary>The last choice the flow refused, or null when the last choice was accepted.</summary>
    public Refusal? Refusal { get; private set; }

    /// <summary>Applies this update's commands, and returns what the accepted party plays once one is accepted.</summary>
    public SessionParty.Playing? Drive(ActionInbox input)
    {
        foreach (CreationCommand command in _input.Read(input))
        {
            if (command.Kind != CreationCommandKind.Accept)
            {
                Refusal = Apply(creation.Flow, command);
                continue;
            }

            if (Accept() is { } accepted) return accepted;
        }

        return null;
    }

    /// <summary>
    /// Builds the finished party through the ruleset's one factory and composes the world it walks into.
    /// </summary>
    /// <remarks>
    /// A refusal leaves creation exactly as it was: an unfinished member is named, and a factory or a world that
    /// refuses what the flow produced is reported as what it is — a party the rules will not build, or content
    /// the world refuses to be composed over — rather than leaving a half-created party behind or an exception
    /// inside an admitted update.
    /// </remarks>
    private SessionParty.Playing? Accept()
    {
        if (!creation.Flow.IsComplete)
        {
            Refusal = new Refusal(
                CreationCodes.CreationIncomplete,
                $"The party cannot be accepted while creation is unfinished: {Unfinished(creation.Flow)}.");
            return null;
        }

        PartyEntity? party = null;
        SessionWorld? world = null;
        SessionParty.Playing playing;
        try
        {
            party = creation.BuildParty(creation.Flow.ToCreation());
            playing = creation.Play(party);
            world = playing.World;
            if (!ReferenceEquals(playing.Party, party) || playing.Resumed is not null)
            {
                throw new ArgumentException(
                    "What a created party plays must be composed over that party, as a new session: a different party, or one resumed from a save, would be a second band for one expedition.");
            }
        }
        catch (Exception error) when (error is ArgumentException or ContentValidationException)
        {
            // An accepted party that is not played would be a second party, so both are released here.
            world?.Dispose();
            party?.Dispose();
            Refusal = new Refusal(CreationCodes.CreationRefused, $"The finished party was refused: {error.Message}");
            diagnostics.Refused("creation", CreationCodes.CreationRefused, Refusal.Message);
            return null;
        }

        Refusal = null;
        return playing;
    }

    /// <summary>Makes one creation command on the flow and returns the rule it broke, when it broke one.</summary>
    private static Refusal? Apply(PartyCreationFlow flow, CreationCommand command) => command.Kind switch
    {
        CreationCommandKind.SelectMember => flow.SelectMember(command.Member),
        CreationCommandKind.SelectPortrait => Missing(command, "portrait") ?? flow.SelectPortrait(new PortraitId(command.Value)),
        CreationCommandKind.SelectClass => Missing(command, "class") ?? flow.SelectClass(new ClassId(command.Value)),
        CreationCommandKind.SetName => flow.SetName(command.Value),
        CreationCommandKind.RaiseAttribute => Missing(command, "attribute") ?? flow.RaiseAttribute(new AttributeId(command.Value)),
        CreationCommandKind.LowerAttribute => Missing(command, "attribute") ?? flow.LowerAttribute(new AttributeId(command.Value)),
        CreationCommandKind.ChooseSkill => Missing(command, "skill") ?? flow.ChooseSkill(new SkillId(command.Value)),
        CreationCommandKind.RemoveSkill => Missing(command, "skill") ?? flow.RemoveSkill(new SkillId(command.Value)),
        CreationCommandKind.Advance => flow.Advance(),
        _ => null,
    };

    /// <summary>
    /// Refuses a choice command that arrived without the choice it names: a control that silently does nothing
    /// looks exactly like a control that worked and changed nothing.
    /// </summary>
    private static Refusal? Missing(CreationCommand command, string choice) =>
        string.IsNullOrWhiteSpace(command.Value)
            ? new Refusal(
                CreationCodes.CreationChoiceMissing,
                $"A {choice} choice arrived naming no {choice}, so there was nothing to choose; a {choice} is named by the id creation offers it under.")
            : null;

    /// <summary>Names every member still unfinished, which is why a party cannot be accepted yet.</summary>
    private static string Unfinished(PartyCreationFlow flow)
    {
        List<string> pending = [];
        for (int index = 0; index < flow.MemberCount; index++)
        {
            CreationMember member = flow.Member(index);
            if (member.IsComplete) continue;
            pending.Add($"member {index + 1} is at the {member.Step} step");
        }

        return pending.Count > 0 ? string.Join("; ", pending) : "no member is finished";
    }
}
