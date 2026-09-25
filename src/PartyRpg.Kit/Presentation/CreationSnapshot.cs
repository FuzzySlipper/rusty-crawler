using PartyRpg.Kit.Party;

namespace PartyRpg.Kit.Presentation;

/// <summary>One member of the party being created, as the creation screen shows it.</summary>
/// <remarks>
/// Every value is read from the flow's own answer for that member — the step it stands on, what it has
/// chosen so far, and what the pool still holds — so the screen draws the draft rather than a second copy
/// of it. A step not yet reached reads as absent: no portrait, no race, no class, no name.
/// </remarks>
/// <param name="Index">Which member this is, counted from zero in the order the party stands in.</param>
/// <param name="Step">Where this member stands, as the wire spells it.</param>
/// <param name="Name">The name given, empty while none has been.</param>
/// <param name="Race">The race the portrait decided, empty while no portrait has been chosen.</param>
/// <param name="Class">The class chosen, empty while none has been.</param>
/// <param name="Portrait">The portrait chosen, empty while none has been.</param>
/// <param name="PoolRemaining">How many attribute points this member has still to spend.</param>
public readonly record struct CreationMemberSnapshot(
    int Index,
    string Step,
    string Name,
    string Race,
    string Class,
    string Portrait,
    int PoolRemaining);

/// <summary>One member of the party a session accepted, as the screen shows it once it is playing.</summary>
/// <remarks>
/// These are the created party's own members, read from the party the session holds rather than from the
/// flow that described it, which is what makes the accepted state a fact about the party being played.
/// </remarks>
/// <param name="Index">Which member this is, counted from zero in the order the party stands in.</param>
/// <param name="Name">The character's name.</param>
/// <param name="Race">The character's race.</param>
/// <param name="Class">The character's class.</param>
/// <param name="Portrait">The portrait the character was created with, empty when it carries none.</param>
public readonly record struct CreationPartyMemberSnapshot(
    int Index,
    string Name,
    string Race,
    string Class,
    string Portrait);

/// <summary>One portrait creation offers, with whether the member being created has chosen it.</summary>
/// <param name="Id">The portrait's identity, which is what choosing it sends back.</param>
/// <param name="Name">What the portrait is called.</param>
/// <param name="Race">The race the portrait is drawn as, which is what choosing it decides.</param>
/// <param name="Selected">Whether this is the portrait the member being created carries.</param>
public readonly record struct CreationPortraitSnapshot(string Id, string Name, string Race, bool Selected);

/// <summary>One class creation offers, with whether the member being created has chosen it.</summary>
/// <param name="Id">The class's identity, which is what choosing it sends back.</param>
/// <param name="Name">What the class is called.</param>
/// <param name="Selected">Whether this is the class the member being created belongs to.</param>
public readonly record struct CreationClassSnapshot(string Id, string Name, bool Selected);

/// <summary>One skill of the member being created, and where it stands.</summary>
/// <remarks>
/// The class decides which skills are on offer, so an attribute-only view would leave a player guessing.
/// Each entry carries the flow's own answer as one word: a skill the class fixes, one the player has
/// already chosen, or one still available to choose. The screen renders that word rather than working out
/// which list an id belongs to.
/// </remarks>
/// <param name="Id">The skill's identity, which is what choosing or removing it sends back.</param>
/// <param name="Name">The skill's display name, which is its identity when the choices carry no other.</param>
/// <param name="State">Where the skill stands: the wire word for fixed, chosen, or available.</param>
public readonly record struct CreationSkillSnapshot(string Id, string Name, string State);

/// <summary>
/// One attribute of the member being created, with the range its race allows and what the pool can do.
/// </summary>
/// <remarks>
/// The bounds and the two flags are the race's own range read through its own methods, so a screen can
/// offer only the moves that are legal without re-deriving a single rule: what the flow would refuse and
/// what this says is impossible are the same answer, computed once where the range lives.
/// </remarks>
/// <param name="Id">The attribute's identity, which is what raising or lowering it sends back.</param>
/// <param name="Name">What the attribute is called.</param>
/// <param name="Value">The score the member being created holds now.</param>
/// <param name="Minimum">The lowest creation may lower it to for this race.</param>
/// <param name="Maximum">The highest creation may raise it to for this race.</param>
/// <param name="CanRaise">Whether one raise still fits under the ceiling.</param>
/// <param name="CanLower">Whether one lowering still fits above the floor.</param>
public readonly record struct CreationAttributeSnapshot(
    string Id,
    string Name,
    int Value,
    int Minimum,
    int Maximum,
    bool CanRaise,
    bool CanLower);

/// <summary>
/// The creation a session is in the middle of, as the screen shows it: where the flow stands, what the
/// member being created has chosen, what it may still choose, and the rule the last illegal choice broke.
/// </summary>
/// <remarks>
/// <para>
/// The whole screen is one value because the screen holds nothing: every list here is the flow's own
/// options and the member's own answers, so a projection that carried only the current step would leave the
/// screen deciding which skills a class offers — which is a rule, and rules are not the screen's.
/// </para>
/// <para>
/// <see cref="Active"/> and <see cref="Accepted"/> are separate facts: a session can be creating a party,
/// have accepted one and be playing it, or neither — a resumed session creates nothing and accepted
/// nothing. The empty value is the third case, and a screen that shows creation must not confuse it with
/// a party that is still being made.
/// </para>
/// </remarks>
/// <param name="Active">Whether the session is creating a party right now.</param>
/// <param name="Accepted">Whether this session accepted a party and is playing it.</param>
/// <param name="HasDefault">Whether the ruleset offered a default party to start from.</param>
/// <param name="MemberIndex">Which member creation is on, counted from zero.</param>
/// <param name="MemberCount">How many members the party is created with.</param>
/// <param name="Step">Where the member being created stands, as the wire spells it.</param>
/// <param name="PoolRemaining">How many attribute points the member being created has still to spend.</param>
/// <param name="RefusalCode">The stable code of the last choice the flow refused, empty when the last one was accepted.</param>
/// <param name="RefusalMessage">Why the last choice was refused, in terms a person can act on; empty when none was.</param>
/// <param name="Roster">The members as they stand while creating, empty once creation is over.</param>
/// <param name="Portraits">The portraits creation offers the member being created, empty when none is being created.</param>
/// <param name="Classes">The classes creation offers the member being created, empty when none is being created.</param>
/// <param name="Skills">The skills the chosen class fixes or offers, with where each one stands.</param>
/// <param name="Attributes">The attributes the chosen race brings, with what the pool may do to each.</param>
/// <param name="Party">The party this session accepted, once it is playing it; empty otherwise.</param>
public readonly record struct CreationSnapshot(
    bool Active,
    bool Accepted,
    bool HasDefault,
    int MemberIndex,
    int MemberCount,
    string Step,
    int PoolRemaining,
    string RefusalCode,
    string RefusalMessage,
    IReadOnlyList<CreationMemberSnapshot> Roster,
    IReadOnlyList<CreationPortraitSnapshot> Portraits,
    IReadOnlyList<CreationClassSnapshot> Classes,
    IReadOnlyList<CreationSkillSnapshot> Skills,
    IReadOnlyList<CreationAttributeSnapshot> Attributes,
    IReadOnlyList<CreationPartyMemberSnapshot> Party)
{
    /// <summary>The creation of a session that is neither creating nor playing an accepted party.</summary>
    public static CreationSnapshot None => new(
        Active: false,
        Accepted: false,
        HasDefault: false,
        MemberIndex: 0,
        MemberCount: 0,
        Step: string.Empty,
        PoolRemaining: 0,
        RefusalCode: string.Empty,
        RefusalMessage: string.Empty,
        Roster: [],
        Portraits: [],
        Classes: [],
        Skills: [],
        Attributes: [],
        Party: []);

    /// <summary>Reads the flow as the creation screen needs it.</summary>
    /// <param name="flow">The flow the session is holding.</param>
    /// <param name="refusal">The last choice the flow refused, or null when the last one was accepted.</param>
    /// <returns>Where creation stands and what it offers.</returns>
    /// <exception cref="ArgumentNullException">The flow is null.</exception>
    public static CreationSnapshot From(PartyCreationFlow flow, PartyRefusal? refusal)
    {
        ArgumentNullException.ThrowIfNull(flow);
        CreationMember current = flow.Member(flow.MemberIndex);
        CreationRace? race = current.Race is { } raceId ? flow.Options.FindRace(raceId) : null;
        CreationClass? characterClass = current.Class is { } classId ? flow.Options.FindClass(classId) : null;

        List<CreationMemberSnapshot> roster = [];
        for (int index = 0; index < flow.MemberCount; index++)
        {
            CreationMember member = flow.Member(index);
            roster.Add(new CreationMemberSnapshot(
                member.Index,
                SessionProjection.WireName(member.Step),
                member.Name,
                member.Race?.Value ?? string.Empty,
                member.Class?.Value ?? string.Empty,
                member.Portrait?.Value ?? string.Empty,
                member.PoolRemaining));
        }

        List<CreationPortraitSnapshot> portraits = [];
        foreach (CreationPortrait portrait in flow.Options.Portraits)
        {
            portraits.Add(new CreationPortraitSnapshot(
                portrait.Id.Value,
                portrait.Name,
                portrait.Race.Value,
                portrait.Id == current.Portrait));
        }

        List<CreationClassSnapshot> classes = [];
        foreach (CreationClass option in flow.Options.Classes)
        {
            classes.Add(new CreationClassSnapshot(option.Id.Value, option.Name, option.Id == current.Class));
        }

        return new CreationSnapshot(
            Active: true,
            Accepted: false,
            HasDefault: flow.HasDefault,
            MemberIndex: flow.MemberIndex,
            MemberCount: flow.MemberCount,
            Step: SessionProjection.WireName(flow.Step),
            PoolRemaining: flow.PoolRemaining,
            RefusalCode: refusal?.Code ?? string.Empty,
            RefusalMessage: refusal?.Message ?? string.Empty,
            Roster: roster,
            Portraits: portraits,
            Classes: classes,
            Skills: SkillChoices(current, characterClass),
            Attributes: AttributeChoices(current, race),
            Party: []);
    }

    /// <summary>Reads the party a session accepted as the creation screen needs it once it is playing.</summary>
    /// <param name="party">The party the session holds, or null when it holds none.</param>
    /// <returns>The accepted party's members, or the empty value when there is no party to show.</returns>
    public static CreationSnapshot OfParty(PartyEntity? party)
    {
        if (party is null) return None;

        List<CreationPartyMemberSnapshot> members = [];
        int index = 0;
        foreach (PartyMember member in party.Members)
        {
            members.Add(new CreationPartyMemberSnapshot(
                index++,
                member.Profile.Name,
                member.Profile.Race.Value,
                member.Profile.Class.Value,
                member.Profile.Portrait?.Value ?? string.Empty));
        }

        return None with { Accepted = true, Party = members };
    }

    /// <summary>
    /// Reads the skills the chosen class fixes and offers, each with where it stands for this member.
    /// </summary>
    /// <remarks>
    /// A member with no class has nothing to show: legality is the class's answer, and inventing a list
    /// before one is chosen would offer skills nobody may learn yet.
    /// </remarks>
    private static List<CreationSkillSnapshot> SkillChoices(CreationMember member, CreationClass? characterClass)
    {
        List<CreationSkillSnapshot> skills = [];
        if (characterClass is null) return skills;

        foreach (SkillId skill in characterClass.FixedSkills)
        {
            skills.Add(new CreationSkillSnapshot(skill.Value, skill.Value, CreationWording.FixedSkill));
        }

        foreach (SkillId skill in characterClass.ChoosableSkills)
        {
            skills.Add(new CreationSkillSnapshot(
                skill.Value,
                skill.Value,
                member.ChosenSkills.Contains(skill) ? CreationWording.ChosenSkill : CreationWording.AvailableSkill));
        }

        return skills;
    }

    /// <summary>Reads the attribute ranges the chosen race brings, with this member's scores in them.</summary>
    private static List<CreationAttributeSnapshot> AttributeChoices(CreationMember member, CreationRace? race)
    {
        List<CreationAttributeSnapshot> attributes = [];
        if (race is null) return attributes;

        foreach (AttributeCreationRange range in race.Attributes)
        {
            int value = 0;
            foreach (AttributeScore score in member.Attributes)
            {
                if (score.Attribute == range.Attribute) value = score.Value;
            }

            attributes.Add(new CreationAttributeSnapshot(
                range.Attribute.Value,
                range.Name,
                value,
                range.Minimum,
                range.Maximum,
                range.CanRaise(value),
                range.CanLower(value)));
        }

        return attributes;
    }
}

/// <summary>
/// The words the creation projection spells its states with.
/// </summary>
/// <remarks>
/// One place for the wire vocabulary, so a screen that renders a word and the projection that publishes it
/// cannot disagree: a state with no word here is a state the screen cannot show, and the projection suite
/// asserts every one of them.
/// </remarks>
public static class CreationWording
{
    /// <summary>A skill the chosen class grants, which is therefore not a choice.</summary>
    public const string FixedSkill = "fixed";

    /// <summary>A skill the player has already chosen for this character.</summary>
    public const string ChosenSkill = "chosen";

    /// <summary>A skill the chosen class offers and this character has not chosen.</summary>
    public const string AvailableSkill = "available";
}
