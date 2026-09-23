# Agent review workflow

Status: active convention. This file records the review model this repository
expects; a Den document may take ownership of the policy later, and then Den
wins where the two disagree. There are no packet files yet — the lanes below are
questions, not files, and the first implementation task that needs them adds its
own packet.

Reviewers are persistent, not one-shot. An identified issue is re-checked by the
same reviewer in the same session, so a revision round verifies the fix instead
of rediscovering the problem from a blank context.

Review is not an approval gate and not an interactive ceremony. Reviewers report
source-backed findings; the root agent reconciles them and decides.

## Lane roster

Three lanes run on **every** task:

| Lane | The question |
| --- | --- |
| Engine reuse | Did this reinvent something the installed `Rusty.Engine` SDK already guarantees? |
| Existing product reuse | Did this reinvent something `PartyRpg.Kit`, the ruleset, or the importer already owns, or put a mechanism in the wrong layer? |
| Runtime trust | Did this add validation, hashing, revision guards, snapshots, or rollback that a trusted single-player runtime path does not need? |

The two reuse lanes are always on because agents skip capabilities that already
exist. Runtime trust is always on for the opposite failure: import-side
validation gravity (bounds checks, digests, provenance) leaking into gameplay
paths.

Optional lanes. In DSH, pick to a total of three or four reviewers, and pick
lanes whose questions can disagree with each other:

| Lane | Use when |
| --- | --- |
| Ownership and values | the change crosses an ownership seam, adds tuning, or lets ruleset vocabulary into the kit |
| Behavior and interoperability | the task specifies behavior with real callers, persistence, or content contracts |
| Requirement and acceptance | the task carries explicit acceptance criteria |
| Error and boundary paths | the change adds parsing, input handling, or failure paths |
| Test claims | the change adds or edits tests, or claims verification |

Do not open a lane that repeats another lane's question in different words, and
do not run the full roster to be safe.

## Choosing the reviewer tool

| Tool | Context | Use for |
| --- | --- | --- |
| `subagent` | fresh; sees only its prompt | adversarial and requirement lanes, where anchoring on the root agent's reasoning would weaken the check |
| `subagent_fork` | inherits the root agent's completed turns | lanes that need the change's rationale — reuse, ownership, interoperability, runtime trust |

Open every reviewer for a round in one message and keep working while they run;
their reports arrive as settlement notices. Do not wait idly and do not poll.

Give a fresh reviewer everything it needs: repository path, the exact artifact
under review, and the command that demonstrates the behavior.

Revision rounds: `send_message` the same reviewer. State what changed, what was
deliberately left alone and why, and which finding ids to re-check. Never open a
new reviewer for work an existing reviewer has already seen — that discards the
continuity that makes the second pass worth reading. Open a fresh reviewer only
when the revision is large enough that the old reviewer's accumulated position
is itself a bias, and say so when you do.

## Authority on disagreement

Findings are claims to verify, not instructions to apply.

- A factual dispute is settled with evidence and a re-check in the reviewer's own
  session.
- A scope dispute is not the reviewer's call. The task's stated contract and the
  user decide; the root records the disposition and the reason.
- A reviewer's verdict never amends user intent, and an unresolved finding is
  never silently dropped — it is deferred explicitly, or declined with a reason.

## What reviewers must not report

Stylistic preferences, new scope, broad redesign proposals, invented acceptance
criteria, or interactive gates. A finding that is not backed by a file and line,
command output, or a command that reproduces it does not belong in the report.
