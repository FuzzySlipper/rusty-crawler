# docs

Durable repository documents for Rusty Crawler. Start here.

| Document | Owns |
| --- | --- |
| [`../AGENTS.md`](../AGENTS.md) | The working contract: direction, ownership, boundary rules, git and documentation posture. Read it first. |
| [`../README.md`](../README.md) | What the repository is, its current state, and how to develop and verify it. |
| [`research/`](research/) | Donor surveys: what the reference reimplementation and the extension projects know about the games, and what this repository may reuse from them. |
| [`agent-review/`](agent-review/) | The review lane model and the packets handed to reviewers. |

Planned, not yet written:

- A gameplay design document naming the concrete owners of party, combat, magic,
  world, quest, and persistence state once implementation starts.
- A coverage plan and task index for the game family: what behavior is in scope
  for MM6/MM7/MM8, how it is sequenced, and which donor artifact documents it.
- A feature map: the point-in-time donor inventory this repository plans against.

Documentation posture: durable documents state ownership, boundaries, and
current behavior. They do not pin engine versions or commit revisions — those
live in machine-checked configuration such as `Directory.Build.props` and are
moved by scripts, never by editing prose. Do not restate a version here.
