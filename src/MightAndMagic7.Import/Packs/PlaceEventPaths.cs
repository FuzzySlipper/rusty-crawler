using System.Globalization;
using MightAndMagic7.Import.Events;

namespace MightAndMagic7.Import.Packs;

/// <summary>How a run of one event reaches one of its steps: the conditions on the first way there.</summary>
/// <param name="Step">The step.</param>
/// <param name="Conditions">
/// What must hold, in the order the run meets it, on the shortest way from the event's first step; empty when every
/// run reaches the step.
/// </param>
/// <param name="Ways">How many different ways through the event's branches reach the step.</param>
public sealed record PlaceEventPath(int Step, IReadOnlyList<string> Conditions, int Ways)
{
    /// <summary>Whether every run of the event reaches the step.</summary>
    public bool IsUnconditional => Conditions.Count == 0;

    /// <summary>The conditions as one phrase, empty for a step every run reaches.</summary>
    public string Condition => string.Join(" and ", Conditions);
}

/// <summary>
/// Reads which steps of a map event a run reaches, and under what conditions, from the event's own branches.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why it is read here.</b> A travel link is one move instruction of an event, and an event can hold several:
/// a barrow's door compares a map variable and moves the party to one barrow or another, a shrine compares a quest
/// bit and either moves the party or prints a line and stops. Which move a run takes is the ruleset's to decide in
/// play — it interprets the event when the party uses or treads on what raises it — and what this reading gives is
/// the account of it: for every move, the condition under which a run reaches it, so a report can state each link's
/// condition rather than "first move wins".
/// </para>
/// <para>
/// <b>The donor's control flow.</b> A run starts at step zero and goes to the next step unless the instruction
/// says otherwise (OpenEnroth <c>src/Engine/Evt/EvtInterpreter.cpp:140-146</c>, the first instruction with a step
/// number): a comparison, a skill check, a kill check and a season check jump to their target when they hold
/// (<c>:317-357</c>, <c>:517-524</c>, <c>:546-549</c>); a plain jump always jumps; a random jump picks one of its
/// non-zero targets (<c>src/Engine/Evt/EvtInstruction.cpp:1036-1050</c>); an exit and a trigger end the run
/// (<c>EvtInterpreter.cpp:425-448</c>). A hint is not a step (<c>EvtInstruction.cpp:919-924</c>), so it is skipped.
/// A move to another map ends the walk here, because the ruleset's run ends there: the party leaves.
/// </para>
/// </remarks>
public static class PlaceEventPaths
{
    /// <summary>Every step a run of the event reaches, with the conditions on the first way there.</summary>
    /// <param name="instructions">The event's instructions, in program order.</param>
    /// <returns>The paths, by step; a step no run reaches is absent.</returns>
    /// <exception cref="ArgumentNullException">The instructions are null.</exception>
    public static IReadOnlyDictionary<int, PlaceEventPath> Of(IReadOnlyList<EvtInstruction> instructions)
    {
        ArgumentNullException.ThrowIfNull(instructions);
        Dictionary<int, EvtInstruction> byStep = [];
        foreach (EvtInstruction instruction in instructions)
        {
            if (instruction.Opcode == EvtOpcodes.MouseOver) continue;
            byStep.TryAdd(instruction.Step, instruction);
        }

        Dictionary<int, List<string>> first = [];
        Dictionary<int, int> ways = [];
        Queue<(int Step, List<string> Conditions, HashSet<int> Seen)> queue = new();
        queue.Enqueue((0, [], []));
        while (queue.Count > 0)
        {
            (int step, List<string> conditions, HashSet<int> seen) = queue.Dequeue();

            // A loop does not add a way: a way that comes back to a step it already passed is the same way.
            if (!byStep.TryGetValue(step, out EvtInstruction instruction) || !seen.Add(step)) continue;
            ways[step] = ways.GetValueOrDefault(step) + 1;
            if (first.ContainsKey(step))
            {
                // The shortest way is the one reported; later ways are counted and walked on only far enough to
                // count what they reach, which a bounded number of steps keeps finite.
                if (ways[step] > 16) continue;
            }
            else
            {
                first[step] = conditions;
            }

            foreach ((int next, string? condition) in Next(instruction, byStep))
            {
                List<string> after = condition is null ? conditions : [.. conditions, condition];
                queue.Enqueue((next, after, [.. seen]));
            }
        }

        return first.ToDictionary(pair => pair.Key, pair => new PlaceEventPath(pair.Key, pair.Value, ways[pair.Key]));
    }

    /// <summary>The steps an instruction hands the run on to, each with the condition of going there.</summary>
    private static IEnumerable<(int Step, string? Condition)> Next(EvtInstruction instruction, Dictionary<int, EvtInstruction> byStep)
    {
        int next = instruction.Step + 1;
        if (instruction.Opcode == EvtOpcodes.Exit || EvtOpcodes.IsTrigger(instruction.Opcode)) yield break;
        if (instruction.TryReadMoveToMap(out MoveToMapInstruction move) && !move.IsWithinMap) yield break;
        if (instruction.TryReadJump(out int jump))
        {
            yield return (jump, null);
            yield break;
        }

        if (instruction.TryReadRandomGoTo(out IReadOnlyList<int> targets))
        {
            int count = targets.Count;
            foreach (IGrouping<int, int> target in targets.GroupBy(target => target))
            {
                yield return (target.Key, string.Create(CultureInfo.InvariantCulture, $"a random pick lands on step {target.Key} ({target.Count()} in {count})"));
            }

            yield break;
        }

        if (Branch(instruction, byStep) is { } branch)
        {
            yield return (branch.Target, branch.Holds);
            yield return (next, branch.Fails);
            yield break;
        }

        if (byStep.ContainsKey(next)) yield return (next, null);
    }

    /// <summary>The target and the two phrases of a conditional jump, or null when the instruction is not one.</summary>
    private static (int Target, string Holds, string Fails)? Branch(EvtInstruction instruction, Dictionary<int, EvtInstruction> byStep)
    {
        if (instruction.Opcode == EvtOpcodes.Compare && instruction.TryReadVariable(out VariableInstruction variable) && variable.Target is { } target)
        {
            (string holds, string fails) = Compared(EvtVariables.Name(variable.Variable), variable.Value, Chosen(instruction, byStep));
            return (target, holds, fails);
        }

        if (instruction.TryReadCheckSkill(out CheckSkillInstruction skill))
        {
            string what = string.Create(
                CultureInfo.InvariantCulture,
                $"{EvtVariables.Skill(skill.Skill)} at rank {skill.Rank} {EvtVariables.Mastery(skill.Mastery)}");
            return (skill.Target, $"a chosen member has {what}", $"no chosen member has {what}");
        }

        if (instruction.TryReadIsActorKilled(out ActorKilledInstruction killed))
        {
            string what = string.Create(
                CultureInfo.InvariantCulture,
                $"{EvtVariables.KillPolicy(killed.Policy)} {killed.Parameter}{(killed.Count > 0 ? $" ({killed.Count} of them)" : string.Empty)}");
            return (killed.Target, $"the place's {what} are down", $"the place's {what} are not all down");
        }

        if (instruction.TryReadCheckSeason(out int season, out int seasonTarget))
        {
            string what = EvtVariables.Season(season);
            return (seasonTarget, $"it is {what}", $"it is not {what}");
        }

        return null;
    }

    /// <summary>
    /// Who a comparison of a character's variable reads, as the choice the step before it made; a run starts on the
    /// active character (OpenEnroth <c>src/Engine/Evt/EvtInterpreter.cpp:101-115</c>, <c>:623</c>).
    /// </summary>
    private static string Chosen(EvtInstruction instruction, Dictionary<int, EvtInstruction> byStep)
    {
        if (!byStep.TryGetValue(instruction.Step - 1, out EvtInstruction before) || !before.TryReadForPartyMember(out byte who)) return "the chosen member";
        (string word, int? member) = EvtVariables.Who(who);
        return word switch
        {
            "member" => string.Create(CultureInfo.InvariantCulture, $"member {member + 1}"),
            "party" => "a member of the party",
            "random" => "a member chosen at random",
            "active" => "the active member",
            _ => "the chosen member",
        };
    }

    /// <summary>What holding and failing a comparison of a variable with a value read as.</summary>
    private static (string Holds, string Fails) Compared(EvtVariableName name, int value, string chosen)
    {
        string number = value.ToString(CultureInfo.InvariantCulture);
        string slot = name.Index is { } index ? $" {index.ToString(CultureInfo.InvariantCulture)}" : string.Empty;
        string which = name.Which.Length > 0 ? $" {name.Which}" : string.Empty;
        return name.Word switch
        {
            "quest-bit" => ($"quest bit {number} is set", $"quest bit {number} is not set"),
            "member-bit" => ($"party bit {number} is set", $"party bit {number} is not set"),
            "autonote" => ($"note {number} is known", $"note {number} is not known"),
            "item" => ($"the party carries item {number}", $"the party does not carry item {number}"),
            "item-equipped" => ($"{chosen} wears item {number}", $"{chosen} does not wear item {number}"),
            _ => ($"{name.Word}{which}{slot} is at least {number}", $"{name.Word}{which}{slot} is below {number}"),
        };
    }
}
