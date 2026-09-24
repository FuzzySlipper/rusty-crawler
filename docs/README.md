# docs

Durable repository documents for Rusty Crawler. Start here.

| Document | Owns |
| --- | --- |
| [`../AGENTS.md`](../AGENTS.md) | The working contract: direction, fidelity stance, ownership, boundary rules, donor posture, git and documentation conventions. Read it first. |
| [`../README.md`](../README.md) | What the repository is, its current state, and how to develop and verify it. |
| [`gameplay-design.md`](gameplay-design.md) | The shape of the game: the loop, every system with a fidelity verdict, the foundations-first building order, non-goals, and the decisions that are expensive to reverse. |
| [`code-organization.md`](code-organization.md) | How the repository expresses that shape: layering, where new code goes, the Kit and ruleset owner maps, content and import shapes, the UI contract, session modes, and persistence. |
| [`research/`](research/) | Donor surveys, the manual-cited experience outline, the extracted data inventory, and the byte-verified format specs the importer's remaining work is written against. |
| [`agent-review/`](agent-review/) | The review lane model and the packets handed to reviewers. |

Sequencing lives in Den, not here: the nine foundation stones are campaigns
`rusty-crawler#8454`–`#8462`, each with child tasks carrying outcome, scope,
acceptance criteria, and expected evidence. Den owns task status, dependencies,
and scheduling — repository documents deliberately do not mirror the task list,
because a copied list goes stale and is later read as current. Durable shape and
evidence stay here: the design documents above and
[`research/`](research/).

A point-in-time feature map may still be written here when a campaign needs one.

Documentation posture: durable documents state ownership, boundaries, and
current behavior. They do not pin engine versions or commit revisions — those
live in machine-checked configuration such as `Directory.Build.props` and are
moved by scripts, never by editing prose. Do not restate a version here.
