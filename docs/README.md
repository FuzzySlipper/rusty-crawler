# docs

Durable repository documents for Rusty Crawler. Start here.

| Document | Owns |
| --- | --- |
| [`../AGENTS.md`](../AGENTS.md) | The working contract: direction, fidelity stance, ownership, boundary rules, donor posture, git and documentation conventions. Read it first. |
| [`../README.md`](../README.md) | What the repository is, its current state, and how to develop and verify it. |
| [`gameplay-design.md`](gameplay-design.md) | The shape of the game: the loop, every system with a fidelity verdict, the first coherent slice, non-goals, and the decisions that are expensive to reverse. |
| [`code-organization.md`](code-organization.md) | How the repository expresses that shape: layering, where new code goes, the Kit and ruleset owner maps, content and import shapes, the UI contract, session modes, and persistence. |
| [`research/`](research/) | Donor surveys and the manual-cited experience outline: what the reference reimplementation, the extension projects, and the shipped manual say about the game. |
| [`agent-review/`](agent-review/) | The review lane model and the packets handed to reviewers. |

Planned, not yet written:

- A coverage plan and task index: what behavior is in scope, how it is sequenced,
  and which donor artifact or manual page documents it.
- A feature map: the point-in-time donor and data inventory this repository plans
  against.

Documentation posture: durable documents state ownership, boundaries, and
current behavior. They do not pin engine versions or commit revisions — those
live in machine-checked configuration such as `Directory.Build.props` and are
moved by scripts, never by editing prose. Do not restate a version here.
