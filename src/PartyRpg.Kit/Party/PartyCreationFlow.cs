namespace PartyRpg.Kit.Party;

/// <summary>
/// The party-creation flow: the session's creation mode, driven one step at a time, refusing every illegal
/// choice as it is made.
/// </summary>
/// <remarks>
/// <para>
/// Creation owns no world. It consumes no admitted update, steps nothing, and advances no clock: a product
/// drives it while the world stands still, and the mode it belongs to is the one the design gives no world
/// stepping at all. What it owns instead is the party being assembled — the members' unfinished choices and
/// the points not yet spent — and the answers a ruleset supplied in
/// <see cref="PartyCreationOptions"/>.
/// </para>
/// <para>
/// <b>Every choice is checked when it is made.</b> A portrait that creation does not offer, a skill the
/// chosen class may not learn, an attribute pushed past its ceiling or bought with points the pool does not
/// have, a name that is blank or too long — each is refused where it happens, with a message naming the rule
/// it broke, rather than being carried to the end and discovered when the party is built. The two rules that
/// can only be judged at the end of a step — the pool spent exactly and the right number of skills chosen —
/// are judged when that step is confirmed.
/// </para>
/// <para>
/// The result is the shape the rest of the product already speaks: a finished flow hands
/// <see cref="ToCreation"/> to <see cref="PartyEntityFactory.Create"/>, which mints the durable member
/// identities, and the party it builds is what a save captures. Creation itself mints nothing.
/// </para>
/// </remarks>
public sealed class PartyCreationFlow
{
    private readonly PartyCreationOptions _options;
    private readonly PartyCreationDefaults? _defaults;
    private readonly MemberDraft[] _members;
    private int _memberIndex;

    /// <summary>Starts creation over the choices a ruleset offers.</summary>
    /// <param name="options">The races, classes, portraits, budgets and starting values creation offers.</param>
    /// <param name="defaults">
    /// The default party to start from, when the ruleset offers one. It is applied through the same steps and
    /// the same validation as a player's own choices, so a ruleset whose default breaks a rule is refused
    /// here, where the defect is, rather than producing a party nobody checked.
    /// </param>
    /// <exception cref="ArgumentNullException">The options are null.</exception>
    /// <exception cref="ArgumentException">The default party does not fit the options, or breaks creation's own rules.</exception>
    public PartyCreationFlow(PartyCreationOptions options, PartyCreationDefaults? defaults = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _members = new MemberDraft[options.MemberCount];
        for (int index = 0; index < _members.Length; index++) _members[index] = new MemberDraft();

        if (defaults is null) return;
        if (defaults.Members.Count != options.MemberCount)
        {
            throw new ArgumentException(
                $"The default party has {defaults.Members.Count} member{(defaults.Members.Count == 1 ? string.Empty : "s")} "
                + $"and creation makes {options.MemberCount}; a starting point that does not fit is not one a player could accept.",
                nameof(defaults));
        }

        _defaults = defaults;
        if (ApplyDefault() is { } refused)
        {
            throw new ArgumentException(
                $"The default party breaks creation's own rules: {refused.Message} ({refused.Code}).",
                nameof(defaults));
        }
    }

    /// <summary>The choices creation offers.</summary>
    public PartyCreationOptions Options => _options;

    /// <summary>Which member creation is on, counted from zero.</summary>
    public int MemberIndex => _memberIndex;

    /// <summary>How many members the party is created with.</summary>
    public int MemberCount => _members.Length;

    /// <summary>Where the member being created stands in creation's step sequence.</summary>
    public CreationStep Step => _members[_memberIndex].Step;

    /// <summary>How many attribute points the member being created has still to spend.</summary>
    public int PoolRemaining => PoolRemainingOf(_members[_memberIndex]);

    /// <summary>Whether every member is finished and confirmed, so the party may be created.</summary>
    public bool IsComplete
    {
        get
        {
            foreach (MemberDraft member in _members)
            {
                if (member.Step != CreationStep.Complete) return false;
            }

            return true;
        }
    }

    /// <summary>Whether the ruleset offered a default party to start from.</summary>
    public bool HasDefault => _defaults is not null;

    /// <summary>Reads one member as it stands.</summary>
    /// <param name="index">Which member to read, counted from zero.</param>
    /// <exception cref="ArgumentOutOfRangeException">Creation makes no member with that index.</exception>
    public CreationMember Member(int index)
    {
        if (index < 0 || index >= _members.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(index),
                index,
                $"Creation makes {_members.Length} member{(_members.Length == 1 ? string.Empty : "s")}, so there is no member {index} to read.");
        }

        MemberDraft member = _members[index];
        CreationClass? characterClass = member.Class is { } classId ? _options.FindClass(classId) : null;
        return new CreationMember(
            index,
            member.Step,
            member.Portrait,
            member.Race,
            member.Class,
            member.Name,
            [.. member.Attributes],
            characterClass?.FixedSkills ?? [],
            [.. member.ChosenSkills],
            PoolRemainingOf(member));
    }

    /// <summary>Moves creation onto a member, reopening a confirmed one so its choices can be changed.</summary>
    /// <remarks>
    /// Reopening is what customizing the default party does: the member's answers stay until they are changed,
    /// but its steps begin again, so a change to the portrait or the class re-derives what depends on it.
    /// </remarks>
    /// <param name="index">Which member to create next, counted from zero.</param>
    /// <returns>A refusal when creation makes no member with that index, otherwise null.</returns>
    public PartyRefusal? SelectMember(int index)
    {
        if (index < 0 || index >= _members.Length)
        {
            return new PartyRefusal(
                "member-unknown",
                $"Creation makes {_members.Length} member{(_members.Length == 1 ? string.Empty : "s")}, so there is no member {index} to create.");
        }

        _memberIndex = index;
        if (_members[index].Step == CreationStep.Complete) _members[index].Step = CreationStep.Portrait;
        return null;
    }

    /// <summary>Chooses the portrait, which is what decides the character's race.</summary>
    /// <remarks>
    /// The race's attribute table is re-derived here, so the scores go back to the race's starting values and
    /// any points already spent on the previous race are returned to the pool: a player who changes the face
    /// has changed the character. The chosen skills are left alone because they follow from the class, which
    /// no portrait decides.
    /// </remarks>
    /// <param name="portrait">The portrait to create the character with.</param>
    /// <returns>A refusal when the choice is out of step or the portrait is not one creation offers, otherwise null.</returns>
    public PartyRefusal? SelectPortrait(PortraitId portrait)
    {
        if (RequireStep(CreationStep.Portrait, "Choosing a portrait") is { } wrongStep) return wrongStep;
        if (_options.FindPortrait(portrait) is not { } chosen)
        {
            return new PartyRefusal(
                "portrait-unknown",
                $"'{portrait}' is not a portrait creation offers, so no race is drawn as it; a portrait is chosen from the ones the ruleset lists.");
        }

        CreationRace? race = _options.FindRace(chosen.Race);
        if (race is null)
        {
            // The options refuse this when they are assembled, so reaching it means they were changed behind
            // creation's back; refusing is still better than creating a character with no attribute table.
            return new PartyRefusal(
                "portrait-race-unknown",
                $"Portrait '{chosen.Name}' is drawn as race '{chosen.Race}', which creation does not offer.");
        }

        MemberDraft member = _members[_memberIndex];
        member.Portrait = chosen.Id;
        member.Race = chosen.Race;
        member.Attributes.Clear();
        foreach (AttributeCreationRange range in race.Attributes)
        {
            member.Attributes.Add(new AttributeScore(range.Attribute, range.Start));
        }

        return null;
    }

    /// <summary>Chooses the class, which is what decides the skills a player may pick from.</summary>
    /// <remarks>
    /// The skills already picked for another class are cleared: legality is the class's answer, and keeping a
    /// pick that the new class may not learn would smuggle an illegal skill past the rule that refuses it.
    /// </remarks>
    /// <param name="characterClass">The class to create the character in.</param>
    /// <returns>A refusal when the choice is out of step or the class is not one creation offers, otherwise null.</returns>
    public PartyRefusal? SelectClass(ClassId characterClass)
    {
        if (RequireStep(CreationStep.Class, "Choosing a class") is { } wrongStep) return wrongStep;
        CreationClass? chosen = _options.FindClass(characterClass);
        if (chosen is null)
        {
            return new PartyRefusal(
                "class-unknown",
                $"'{characterClass}' is not a class creation offers; a class is chosen from the ones the ruleset lists.");
        }

        MemberDraft member = _members[_memberIndex];
        member.Class = chosen.Id;
        member.ChosenSkills.Clear();
        return null;
    }

    /// <summary>Names the character.</summary>
    /// <param name="name">The name to give, trimmed of surrounding whitespace.</param>
    /// <returns>A refusal when the choice is out of step or the name is not one a character may carry, otherwise null.</returns>
    public PartyRefusal? SetName(string name)
    {
        if (RequireStep(CreationStep.Name, "Naming the character") is { } wrongStep) return wrongStep;
        string trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return new PartyRefusal(
                "name-blank",
                "A character's name cannot be blank; every member of the party is named before the game starts.");
        }

        if (trimmed.Length > _options.NameMaximumLength)
        {
            return new PartyRefusal(
                "name-too-long",
                $"A name holds at most {_options.NameMaximumLength} characters and '{trimmed}' holds {trimmed.Length}.");
        }

        foreach (char letter in trimmed)
        {
            if (!char.IsControl(letter)) continue;
            return new PartyRefusal(
                "name-invalid",
                $"'{trimmed}' carries a control character, which a name a player reads cannot carry.");
        }

        _members[_memberIndex].Name = trimmed;
        return null;
    }

    /// <summary>Spends points on one attribute, raising it by one adjustment.</summary>
    /// <param name="attribute">Which attribute to raise.</param>
    /// <returns>A refusal when the choice is out of step, the attribute is unknown, the ceiling is reached, or the pool is short, otherwise null.</returns>
    public PartyRefusal? RaiseAttribute(AttributeId attribute)
    {
        if (RequireStep(CreationStep.Attributes, "Spending attribute points") is { } wrongStep) return wrongStep;
        if (RequireRange(attribute) is not { } range) return UnknownAttribute(attribute);

        int value = ValueOf(attribute);
        if (!range.CanRaise(value))
        {
            return new PartyRefusal(
                "attribute-ceiling",
                $"{range.Name} is already {value} and creation raises it at most to {range.Maximum} for this race.");
        }

        int cost = range.RaiseCost(value);
        int remaining = PoolRemainingOf(_members[_memberIndex]);
        if (cost > remaining)
        {
            return new PartyRefusal(
                "attribute-pool-short",
                $"Raising {range.Name} by {range.RaiseSize(value)} costs {cost} of the {remaining} attribute "
                + $"point{(remaining == 1 ? string.Empty : "s")} left; the pool of {_options.AttributePool} is spent exactly, never overdrawn.");
        }

        SetValue(attribute, range.Raised(value));
        return null;
    }

    /// <summary>Lowers one attribute by one adjustment, returning what it cost to the pool.</summary>
    /// <param name="attribute">Which attribute to lower.</param>
    /// <returns>A refusal when the choice is out of step, the attribute is unknown, or the floor is reached, otherwise null.</returns>
    public PartyRefusal? LowerAttribute(AttributeId attribute)
    {
        if (RequireStep(CreationStep.Attributes, "Spending attribute points") is { } wrongStep) return wrongStep;
        if (RequireRange(attribute) is not { } range) return UnknownAttribute(attribute);

        int value = ValueOf(attribute);
        if (!range.CanLower(value))
        {
            return new PartyRefusal(
                "attribute-floor",
                $"{range.Name} is already {value} and creation lowers it at most to {range.Minimum} for this race.");
        }

        SetValue(attribute, range.Lowered(value));
        return null;
    }

    /// <summary>Chooses one of the skills the class does not fix.</summary>
    /// <param name="skill">Which skill to choose.</param>
    /// <returns>A refusal when the choice is out of step, the class may not learn the skill, it is already fixed or chosen, or enough skills are chosen, otherwise null.</returns>
    public PartyRefusal? ChooseSkill(SkillId skill)
    {
        if (RequireStep(CreationStep.Skills, "Choosing a starting skill") is { } wrongStep) return wrongStep;
        MemberDraft member = _members[_memberIndex];
        CreationClass? characterClass = member.Class is { } classId ? _options.FindClass(classId) : null;
        if (characterClass is null)
        {
            return new PartyRefusal(
                "class-unchosen",
                "A skill is chosen for a class, and this character has no class yet.");
        }

        if (characterClass.FixedSkills.Contains(skill))
        {
            return new PartyRefusal(
                "skill-fixed",
                $"'{skill}' is one of the skills the {characterClass.Name} class starts with, so it is not a choice; the two chosen skills are picked from the rest.");
        }

        if (!characterClass.ChoosableSkills.Contains(skill))
        {
            return new PartyRefusal(
                "skill-not-legal",
                $"'{skill}' is not a skill the {characterClass.Name} class may learn at creation; the class decides which skills may be chosen.");
        }

        if (member.ChosenSkills.Contains(skill))
        {
            return new PartyRefusal(
                "skill-already-chosen",
                $"'{skill}' is already one of this character's chosen skills, and a skill is not learned twice.");
        }

        if (member.ChosenSkills.Count >= _options.ChosenSkillCount)
        {
            return new PartyRefusal(
                "skills-complete",
                $"This character already has its {_options.ChosenSkillCount} chosen skill{(_options.ChosenSkillCount == 1 ? string.Empty : "s")}; "
                + "remove one before choosing another.");
        }

        member.ChosenSkills.Add(skill);
        return null;
    }

    /// <summary>Takes back one of the chosen skills.</summary>
    /// <param name="skill">Which chosen skill to remove.</param>
    /// <returns>A refusal when the choice is out of step or the skill was not chosen, otherwise null.</returns>
    public PartyRefusal? RemoveSkill(SkillId skill)
    {
        if (RequireStep(CreationStep.Skills, "Choosing a starting skill") is { } wrongStep) return wrongStep;
        MemberDraft member = _members[_memberIndex];
        if (!member.ChosenSkills.Remove(skill))
        {
            return new PartyRefusal(
                "skill-not-chosen",
                $"'{skill}' is not one of this character's chosen skills, so there is nothing to take back.");
        }

        return null;
    }

    /// <summary>Confirms the step the member is on and moves creation to the next one.</summary>
    /// <remarks>
    /// This is where the two rules that judge a whole step are enforced: the attribute pool must be spent
    /// exactly, and the character must carry the number of chosen skills creation promises. Confirming the
    /// last step finishes the member and moves on to the next one that is not finished.
    /// </remarks>
    /// <returns>A refusal when the step being confirmed is unfinished, otherwise null.</returns>
    public PartyRefusal? Advance()
    {
        MemberDraft member = _members[_memberIndex];
        switch (member.Step)
        {
            case CreationStep.Portrait:
                if (member.Portrait is null)
                {
                    return new PartyRefusal(
                        "portrait-unchosen",
                        "A character is created with a portrait; choosing one is what decides its race.");
                }

                member.Step = CreationStep.Class;
                return null;

            case CreationStep.Class:
                if (member.Class is null)
                {
                    return new PartyRefusal(
                        "class-unchosen",
                        "A character is created in a class; choosing one is what decides which skills may be chosen.");
                }

                member.Step = CreationStep.Name;
                return null;

            case CreationStep.Name:
                if (member.Name.Length == 0)
                {
                    return new PartyRefusal(
                        "name-blank",
                        "A character's name cannot be blank; every member of the party is named before the game starts.");
                }

                member.Step = CreationStep.Attributes;
                return null;

            case CreationStep.Attributes:
                int remaining = PoolRemainingOf(member);
                if (remaining != 0)
                {
                    return new PartyRefusal(
                        "attribute-pool-unspent",
                        remaining > 0
                            ? $"The pool of {_options.AttributePool} attribute points must be spent exactly and {remaining} remain{(remaining == 1 ? "s" : string.Empty)} unspent."
                            : $"The pool of {_options.AttributePool} attribute points must be spent exactly and this character is {-remaining} past it.");
                }

                member.Step = CreationStep.Skills;
                return null;

            case CreationStep.Skills:
                if (member.ChosenSkills.Count != _options.ChosenSkillCount)
                {
                    int missing = _options.ChosenSkillCount - member.ChosenSkills.Count;
                    return new PartyRefusal(
                        "skills-unchosen",
                        missing > 0
                            ? $"A character starts with {_options.ChosenSkillCount} chosen skill{(_options.ChosenSkillCount == 1 ? string.Empty : "s")} and {missing} remain{(missing == 1 ? "s" : string.Empty)} unchosen."
                            : $"A character starts with {_options.ChosenSkillCount} chosen skill{(_options.ChosenSkillCount == 1 ? string.Empty : "s")} and this one carries {member.ChosenSkills.Count}.");
                }

                member.Step = CreationStep.Complete;
                FocusNextUnfinished();
                return null;

            case CreationStep.Complete:
            default:
                // Confirming a member that is already finished changes nothing: creation is already past it.
                return null;
        }
    }

    /// <summary>Applies the ruleset's default party through the same steps and the same validation.</summary>
    /// <remarks>
    /// The default is offered as a starting point, not as a shortcut: every one of its answers goes through
    /// the operation a player's answer goes through, so applying it can be refused for exactly the reasons a
    /// player's choice can, and the refusal is reported with the member it belongs to.
    /// </remarks>
    /// <returns>A refusal when the ruleset offers no default or the default breaks a creation rule, otherwise null.</returns>
    public PartyRefusal? ApplyDefault()
    {
        if (_defaults is null)
        {
            return new PartyRefusal(
                "no-default",
                "This ruleset offers no default party, so there is none to start from.");
        }

        for (int index = 0; index < _members.Length; index++)
        {
            CreationMemberDefaults start = _defaults.Members[index];
            _memberIndex = index;
            _members[index].Reset();
            PartyRefusal? refused =
                SelectPortrait(start.Portrait)
                ?? Advance()
                ?? SelectClass(start.Class)
                ?? Advance()
                ?? SetName(start.Name)
                ?? Advance()
                ?? ApplyAttributes(start.Attributes)
                ?? Advance()
                ?? ApplySkills(start.ChosenSkills)
                ?? Advance();
            if (refused is null) continue;
            return new PartyRefusal(
                refused.Code,
                $"The default party's member {index + 1} broke a creation rule: {refused.Message}");
        }

        // Creation starts on the first member once the default party has been applied, whether that was at
        // construction or when a player asked for the default back.
        _memberIndex = 0;
        return null;
    }

    /// <summary>Turns the finished creation into the party description the factory builds from.</summary>
    /// <remarks>
    /// Nothing here mints an identity: the members arrive without one and the factory mints them as it
    /// creates the party, which is what keeps identity minting in one place and makes the created party and
    /// the restored party the same durable shape.
    /// </remarks>
    /// <returns>The members creation decided and what the party starts with.</returns>
    /// <exception cref="InvalidOperationException">A member is not finished, so there is no whole party to create.</exception>
    public PartyCreation ToCreation()
    {
        List<string> unfinished = [];
        for (int index = 0; index < _members.Length; index++)
        {
            if (_members[index].Step == CreationStep.Complete) continue;
            unfinished.Add($"member {index + 1} is at the {_members[index].Step} step");
        }

        if (unfinished.Count > 0)
        {
            throw new InvalidOperationException(
                $"The party cannot be created while creation is unfinished: {string.Join("; ", unfinished)}. "
                + "Every member must be finished and confirmed first.");
        }

        List<MemberCreation> members = [];
        foreach (MemberDraft member in _members)
        {
            // The portrait is as much a part of a finished member as the race and the class are: it is what
            // the race was decided from, and it is the face the save carries. A finished member without one
            // is a defect in the flow rather than a character missing a picture.
            if (member.Portrait is not { } portrait ||
                member.Race is not { } race ||
                member.Class is not { } classId ||
                _options.FindClass(classId) is not { } characterClass)
            {
                throw new InvalidOperationException(
                    "A finished member must carry the portrait it was created with, a race, and a class creation knows; creation reached a member that does not.");
            }

            List<SkillEntry> skills = [];
            foreach (SkillId skill in characterClass.FixedSkills) skills.Add(new SkillEntry(skill, 1, _options.StartingSkillTier, 0));
            foreach (SkillId skill in member.ChosenSkills) skills.Add(new SkillEntry(skill, 1, _options.StartingSkillTier, 0));

            PartyMemberSeed seed = new(
                member.Name,
                race,
                characterClass.Id,
                [.. member.Attributes],
                skills,
                // A created caster knows no spells: the shipped data carries no starting-spell table and this
                // game teaches magic from learning books, so a spellbook is filled in play, not at creation.
                [],
                experience: 0,
                level: _options.StartingLevel,
                skillPoints: 0,
                classRank: characterClass.StartingRank,
                conditions: [],
                hitPoints: ResourcePool.Full(characterClass.StartingHitPoints),
                spellPoints: ResourcePool.Full(characterClass.StartingSpellPoints),
                portrait: portrait);
            members.Add(new MemberCreation(seed));
        }

        return new PartyCreation(
            members,
            _options.StartingCoins,
            _options.StartingFoodPortions,
            _options.StartingFoodUnit,
            _options.StartingReputation,
            _options.StartingFame);
    }

    /// <summary>Spends the pool until every attribute stands at the value a default asked for.</summary>
    /// <remarks>
    /// The default states values, not a sequence of clicks, so creation walks each one there with the same
    /// adjustments a player makes — and refuses when a value cannot be reached in whole steps, which is a
    /// default the ruleset stated wrongly rather than a player's mistake.
    /// </remarks>
    private PartyRefusal? ApplyAttributes(IReadOnlyList<AttributeScore> targets)
    {
        foreach (AttributeScore target in targets)
        {
            if (RequireRange(target.Attribute) is not { } range) return UnknownAttribute(target.Attribute);

            int value = ValueOf(target.Attribute);
            int distance = target.Value - value;
            int size = distance < 0 ? range.LowerSize(value) : range.RaiseSize(value);
            if (distance % size != 0)
            {
                return new PartyRefusal(
                    "attribute-unreachable",
                    $"{range.Name} moves {size} at a time from {value}, so a default that asks for {target.Value} asks for a value creation cannot reach.");
            }

            while (ValueOf(target.Attribute) != target.Value)
            {
                bool raising = ValueOf(target.Attribute) < target.Value;
                PartyRefusal? refused = raising ? RaiseAttribute(target.Attribute) : LowerAttribute(target.Attribute);
                if (refused is not null) return refused;
            }
        }

        return null;
    }

    /// <summary>Chooses the skills a default asked for, through the same choice a player makes.</summary>
    private PartyRefusal? ApplySkills(IReadOnlyList<SkillId> skills)
    {
        foreach (SkillId skill in skills)
        {
            if (ChooseSkill(skill) is { } refused) return refused;
        }

        return null;
    }

    /// <summary>Reads the range of one attribute for the race this member was created with.</summary>
    private AttributeCreationRange? RequireRange(AttributeId attribute)
    {
        MemberDraft member = _members[_memberIndex];
        if (member.Race is not { } raceId || _options.FindRace(raceId) is not { } race) return null;
        return race.TryAttribute(attribute, out AttributeCreationRange? range) ? range : null;
    }

    /// <summary>Names an attribute the chosen race does not have.</summary>
    private PartyRefusal UnknownAttribute(AttributeId attribute)
    {
        MemberDraft member = _members[_memberIndex];
        string race = member.Race is { } raceId && _options.FindRace(raceId) is { } found ? found.Name : "no race";
        return new PartyRefusal(
            "attribute-unknown",
            $"'{attribute}' is not an attribute a {race} has, so there are no points to spend on it.");
    }

    /// <summary>Reads the current value of one attribute of the member being created.</summary>
    private int ValueOf(AttributeId attribute)
    {
        foreach (AttributeScore score in _members[_memberIndex].Attributes)
        {
            if (score.Attribute == attribute) return score.Value;
        }

        return 0;
    }

    /// <summary>Records a new value for one attribute of the member being created.</summary>
    private void SetValue(AttributeId attribute, int value)
    {
        List<AttributeScore> scores = _members[_memberIndex].Attributes;
        for (int index = 0; index < scores.Count; index++)
        {
            if (scores[index].Attribute != attribute) continue;
            scores[index] = new AttributeScore(attribute, value);
            return;
        }
    }

    /// <summary>How much of the pool one member has spent or refunded so far.</summary>
    private int PoolRemainingOf(MemberDraft member)
    {
        int remaining = _options.AttributePool;
        if (member.Race is not { } raceId || _options.FindRace(raceId) is not { } race) return remaining;
        foreach (AttributeScore score in member.Attributes)
        {
            if (!race.TryAttribute(score.Attribute, out AttributeCreationRange? range) || range is null) continue;
            remaining -= range.PoolPoints(score.Value);
        }

        return remaining;
    }

    /// <summary>Refuses an action that belongs to a step creation has not reached.</summary>
    private PartyRefusal? RequireStep(CreationStep expected, string action) =>
        _members[_memberIndex].Step == expected
            ? null
            : new PartyRefusal(
                "creation-step",
                $"{action} happens at the {expected} step, and member {_memberIndex + 1} is at the {_members[_memberIndex].Step} step; creation's steps are taken in order.");

    /// <summary>Moves onto the next member that is not finished, leaving the current one when all are.</summary>
    private void FocusNextUnfinished()
    {
        for (int offset = 1; offset <= _members.Length; offset++)
        {
            int index = (_memberIndex + offset) % _members.Length;
            if (_members[index].Step == CreationStep.Complete) continue;
            _memberIndex = index;
            return;
        }
    }

    /// <summary>One member's answers, owned by the flow so that only a validated step can change them.</summary>
    private sealed class MemberDraft
    {
        /// <summary>Where this member stands; every change goes through the step that owns it.</summary>
        internal CreationStep Step { get; set; } = CreationStep.Portrait;

        /// <summary>The portrait chosen, or null while none has been.</summary>
        internal PortraitId? Portrait { get; set; }

        /// <summary>The race the portrait decided, or null while no portrait has been chosen.</summary>
        internal RaceId? Race { get; set; }

        /// <summary>The class chosen, or null while none has been.</summary>
        internal ClassId? Class { get; set; }

        /// <summary>The name given, or blank while none has been.</summary>
        internal string Name { get; set; } = string.Empty;

        /// <summary>The attribute scores, in the race's own order.</summary>
        internal List<AttributeScore> Attributes { get; } = [];

        /// <summary>The chosen skills, in the order they were chosen.</summary>
        internal List<SkillId> ChosenSkills { get; } = [];

        /// <summary>Clears every answer, which is what applying the default party to a member does.</summary>
        internal void Reset()
        {
            Step = CreationStep.Portrait;
            Portrait = null;
            Race = null;
            Class = null;
            Name = string.Empty;
            Attributes.Clear();
            ChosenSkills.Clear();
        }
    }
}
