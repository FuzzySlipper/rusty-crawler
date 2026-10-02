using PartyRpg.Kit.Alchemy;
using PartyRpg.Kit.Combat;
using PartyRpg.Kit.Conversation;
using PartyRpg.Kit.Input;
using PartyRpg.Kit.Interaction;
using PartyRpg.Kit.Knowledge;
using PartyRpg.Kit.Magic;
using PartyRpg.Kit.Party;
using PartyRpg.Kit.Progression;
using PartyRpg.Kit.Services;
using PartyRpg.Kit.Time;
using PartyRpg.Kit.World;
using Rusty.Engine;


namespace PartyRpg.Kit.Sessions;

/// <summary>
/// The acts a player asks for inside one admitted update, each applied through the one owner it belongs to.
/// </summary>
/// <remarks>
/// <para>
/// An act is an instant rather than an interval: a use, a stop, a purchase, a raise, a casting, and a mixture
/// are each read from the update that carries them and applied whole in it. Every one goes through its owner's
/// own entry and its refusal is reported rather than thrown, so a control the panel offers and the product
/// refuses says why instead of looking like a control that did nothing.
/// </para>
/// <para>
/// Which of them may be applied in an update is the session's decision — a screen that owns the controls, a
/// held session, a fight waiting for a turn — and none is made here: each method applies what it is handed.
/// </para>
/// </remarks>
internal sealed class SessionActs(SessionOwners owners, SessionControls controls)
{
    private readonly InteractionUseInput? _use = controls.Use is { } use ? new InteractionUseInput(use) : null;
    private readonly RestInput? _rest = controls.Rest is { } rest ? new RestInput(rest) : null;
    private readonly ServiceInput? _service = controls.Service is { } service ? new ServiceInput(service) : null;
    private readonly ConversationInput? _conversation = controls.Conversation is { } talk ? new ConversationInput(talk) : null;
    private readonly SkillRaiseInput? _raise = controls.Skills is { } skills ? new SkillRaiseInput(skills) : null;
    private readonly CastInput? _cast = controls.Cast is { } cast ? new CastInput(cast) : null;
    private readonly MixInput? _mix = controls.Mix is { } mix ? new MixInput(mix) : null;
    private readonly EquipInput? _equip = controls.Equip is { } equip ? new EquipInput(equip) : null;
    private readonly ConversationHandoffRouter _router = new(owners);

    private SessionDiagnostics Report => owners.Diagnostics;

    /// <summary>
    /// Refreshes what the party faces and applies a use the player asked for, to what the step just put in
    /// front of it.
    /// </summary>
    /// <remarks>
    /// A use that lands on somebody opens the conversation with them, which is the one way a person is reached:
    /// what stands behind them — a counter, a household, an errand — is offered from inside that conversation.
    /// What a use taught is handed to the knowledge owner, which decides whether it is news.
    /// </remarks>
    public void Interact(ActionInbox input)
    {
        if (owners.World is not { } world) return;
        InteractionResult? result = world.Interact(_use is not null && _use.Read(input));

        if (result is { IsApplied: true, Learned.Count: > 0 } taught && owners.Knowledge is { } knowledge)
        {
            foreach (KnowledgeReport report in taught.Learned) knowledge.Record(report);
        }

        if (result is { IsApplied: true, Target: { } target } && owners.Conversations is { } conversations)
        {
            // A use that hands the party to somebody — a fixture whose event calls a person over — opens the
            // conversation with them; a use that led the party away, or a door that kept it outside, opens none; every
            // other use opens one only when somebody stands at the placement.
            if (result.Speaks is { } subject) conversations.Open(target.Id.Place, target.Placement, subject);
            else if (result.Travels is null && !result.KeptOut) conversations.OpenTarget(target.Id.Place, target.Placement);
        }
    }

    /// <summary>Applies the conversation commands this update carried while somebody is being spoken with.</summary>
    public void Converse(ActionInbox input)
    {
        if (owners.Conversations is not { } conversations || _conversation is null) return;
        foreach (ConversationCommand command in _conversation.Read(input))
        {
            if (command.Kind == ConversationCommandKind.Steal)
            {
                StealFrom(conversations, command.Member);
                continue;
            }

            ConversationResult result = command.Kind switch
            {
                ConversationCommandKind.Topic => conversations.Choose(command.Target),
                ConversationCommandKind.Person => conversations.Turn(command.Target),
                _ => conversations.Close(),
            };

            if (result is { IsApplied: true, Handoff: { } handoff }) _router.Route(handoff, conversations);
        }
    }

    /// <summary>
    /// Has one member try to lift what the person the party is speaking with carries, through the service
    /// mechanism's theft, and answers a hand that was seen.
    /// </summary>
    /// <remarks>
    /// A person who caught a hand in their purse does not go on talking: the conversation ends, and the person is put
    /// into the fight as an attack on them would put them there, so what the party did is what the world now answers.
    /// A theft that could not be tried at all — nobody here to rob, no member who could — leaves the conversation as
    /// it was, and the refusal is the service mechanism's last result.
    /// </remarks>
    private void StealFrom(PartyConversations conversations, int member)
    {
        if (!conversations.IsOpen || conversations.Placement is not { } person) return;
        if (owners.Services is not { } services)
        {
            Report.Refused(
                "service",
                "theft-unowned",
                "A theft from a person was asked for, and this session composes no service mechanism to carry it out.");
            return;
        }

        PlaceId place = conversations.Place;
        ServiceResult result = services.StealFrom(place, person, member);
        Report.Report(
            result.IsApplied,
            "service",
            result.IsApplied ? "theft-applied" : "theft-refused",
            result.IsApplied
                ? $"A member tried to lift from '{person.Content}' in place '{place}': {result.Message}"
                : $"A theft from '{person.Content}' in place '{place}' was refused ({result.Code}): {result.Message}");
        if (!result.IsApplied || services.LastTheft is not { Caught: true }) return;

        conversations.Close();
        if (owners.Combat is { } fight)
        {
            foreach (Combatant combatant in fight.Combatants)
            {
                if (combatant.Subject.Placement is { } standing && standing.Content == person.Content) fight.Provoke(combatant.Id);
            }
        }
    }

    /// <summary>
    /// Applies the stops this update carried: each is applied whole, so there is nothing to resume and no screen
    /// that could be drawn while the clock is halfway through the night.
    /// </summary>
    public void Stop(ActionInbox input)
    {
        if (owners.Rest is not { Available: true } rest || _rest is null) return;
        foreach (RestKind kind in _rest.Read(input))
        {
            RestResult result = rest.Perform(kind);
            string place = owners.World?.Place.ToString() ?? string.Empty;
            Report.Report(
                result.IsApplied,
                "rest",
                result.IsApplied ? "rest-applied" : "rest-refused",
                result.IsApplied
                    ? $"The party stopped ({result.Kind}) from {result.From} to {result.To} in place '{place}': {result.Message}"
                    : $"The party's stop ({result.Kind}) in place '{place}' was refused ({result.Code}): {result.Message}");
        }
    }

    /// <summary>Applies the service commands this update carried while a counter is open.</summary>
    /// <remarks>
    /// A passage is the one thing a counter sells that is not simply carried away: the counter settles the fare
    /// and writes the passage on the party, and the journey belongs to the world. Asking for a journey the party
    /// already holds a passage to boards it, which is how a boarding refused once can be tried again.
    /// </remarks>
    public void Serve(ActionInbox input)
    {
        if (owners.Services is not { IsOpen: true } services || _service is null) return;
        foreach (ServiceRequest request in _service.Read(input))
        {
            if (request.Amount is { } amount)
            {
                services.ChooseAmount(amount);
                continue;
            }
            ServiceCommand command = request.Command!;
            ServiceResult result = services.Transact(command);
            if (command.Kind != ServiceOperationKind.Fare) continue;
            string journey = result.Subject.Length > 0 ? result.Subject : command.Target;
            if (journey.Length > 0 && owners.Party is { } party && party.Passages.Holds(new PlaceId(journey)))
            {
                Board(services, journey);
            }
        }
    }

    /// <summary>
    /// Boards the passage the party holds, and ends the visit it was bought at — quietly, because the counter is
    /// behind the party now. A boarding the world refuses leaves the visit open.
    /// </summary>
    private void Board(PartyServices services, string destination)
    {
        if (owners.World is not { } world)
        {
            Report.Refused(
                "travel",
                "fare-no-world",
                $"A passage to {destination} was bought and this session holds no world to travel through, so nothing was boarded.");
            return;
        }

        if (world.Board(new PlaceId(destination)).Arrived) services.Abandon();
    }

    /// <summary>
    /// Applies the skill raises this update carried through the progression owner's one spend path, so a raise
    /// past the ceiling or the pool is refused with the limit or the shortfall named and nothing moves.
    /// </summary>
    public void Raise(ActionInbox input)
    {
        if (_raise is null || owners.Progression is not { } progression) return;
        foreach (SkillRaiseRequest raise in _raise.Read(input))
        {
            if (raise.Member < 0 || raise.Member >= progression.Party.Members.Count)
            {
                Report.Refused(
                    "progression",
                    "skill-member-unknown",
                    $"A raise named member {raise.Member + 1}, and the party has {progression.Party.Members.Count}.");
                continue;
            }

            progression.RaiseSkill(progression.Party.Members[raise.Member].Id, raise.Skill, raise.Levels);
        }
    }

    /// <summary>Applies the quick-slot choices and castings this update carried.</summary>
    /// <param name="input">The update's admitted input.</param>
    /// <param name="allowed">
    /// Whether a casting may be applied: a screen that owns the controls owns casting too, while the quick slot
    /// is a character's own state and is read whatever is open.
    /// </param>
    /// <returns>The member whose casting was applied last this update, which a paced fight spends a turn for.</returns>
    public CombatantId? Cast(ActionInbox input, bool allowed)
    {
        if (_cast is null || owners.Casting is not { } casting) return null;

        foreach (QuickSpellRequest choice in _cast.ReadQuick(input))
        {
            if (choice.Member < 0 || choice.Member >= casting.Party.Members.Count)
            {
                Report.Refused(
                    "magic",
                    "spell-member-unknown",
                    $"A quick spell named member {choice.Member + 1}, and the party has {casting.Party.Members.Count}.");
                continue;
            }

            try
            {
                casting.Party.Members[choice.Member].Spells.SetQuickSpell(choice.Spell);
            }
            catch (ArgumentException refused)
            {
                // The slot holds a spell its owner can cast, so one the character never learned is refused here.
                Report.Refused("magic", "spell-quick-refused", refused.Message);
            }
        }

        CombatantId? caster = null;
        foreach (CastRequest request in _cast.Read(input))
        {
            if (!allowed)
            {
                Report.Refused(
                    "magic",
                    "spell-screen-open",
                    "A casting arrived while a screen owned the player's controls, so nothing was cast and no spell point was spent.");
                continue;
            }

            SpellCastResult result = casting.Cast(new SpellCastRequest(request.Member, request.Spell, request.Target, request.Item));
            if (result.IsCast) caster = CombatantId.Of(casting.Party.Members[result.Member].Id);
            else Report.Refused("magic", result.Code, result.Message);
        }

        return caster;
    }

    /// <summary>Applies the mixtures this update carried through the one mixing workflow.</summary>
    /// <param name="input">The update's admitted input.</param>
    /// <param name="allowed">Whether a mixture may be applied, which a screen owning the controls forbids.</param>
    public void Mix(ActionInbox input, bool allowed)
    {
        if (_mix is null || owners.Mixing is not { } mixing) return;
        foreach (MixRequest request in _mix.Read(input))
        {
            if (!allowed)
            {
                Report.Refused(
                    "alchemy",
                    "mixture-screen-open",
                    "A mixture arrived while a screen owned the player's controls, so nothing was mixed and both ingredients are still where they were.");
                continue;
            }

            MixingResult result = mixing.Mix(new MixingRequest(request.Member, request.First, request.Second));
            Report.Report(result.IsMixed, "alchemy", result.Code, result.Message);
        }
    }

    /// <summary>Applies the equipment changes this update carried through the party's own equip and unequip.</summary>
    /// <param name="input">The update's admitted input.</param>
    /// <param name="allowed">Whether a change may be applied, which a screen owning the controls forbids.</param>
    /// <remarks>
    /// A change is an instant: the party moves the item and the game's use rule judges it, and what a member then
    /// wears is what the fight reads at that member's next blow. A refusal leaves the figure exactly as it was.
    /// </remarks>
    public void Equip(ActionInbox input, bool allowed)
    {
        if (_equip is null || owners.Outfitting is not { } outfitting) return;
        foreach (EquipRequest request in _equip.Read(input))
        {
            if (!allowed)
            {
                Report.Refused(
                    "equipment",
                    "equipment-screen-open",
                    "A change of equipment arrived while a screen owned the player's controls, so nothing was put on or taken off.");
                continue;
            }

            OutfittingResult result = request.Unequip
                ? outfitting.Unequip(request.Member, request.Slot!.Value)
                : outfitting.Equip(request.Member, request.Item!.Value, request.Slot);
            Report.Report(result.Changed, "equipment", result.Code, result.Message);
        }
    }
}
