# Companion HUD clicks

Recorded on 5 October 2026 for Den task #9441 (provenance only).

The UI suite passed 105 cases. The companion regression holds a pointer press across five detached
projections with changing frame/time and purse values, checks that the connected button retains its
identity, then releases and activates it. It also checks changed labels/portraits, disabled talk and
departure/reappearance. jsdom does not synthesize a physical click; the following live check covers that.

Owned browser session `c2617636-2d78-40e9-9f6b-6bff02bfcdf8`, slot 3, ran a private CoreCLR host.
The staged content added two explicitly authored free hireable people 180 units in front of Emerald
Island's Party Start. No runtime gameplay or TypeScript state was added for the check. The observer
used New Game, default party creation, ordinary Use (G), conversation hire and Take your leave.

DOM inspection located the two visible HUD rectangles. Activation used physical pointer input:
point `(1167,349)` then an 80 ms press/release for the guide; point `(1167,394)` then a 150 ms press/release
for the witness. Each was repeated after leaving the preceding conversation while the session ran.

| Capture | Observation |
| --- | --- |
| `f9a36d04-1cf8-4c8c-a7bc-828b23bb41fc` | Emerald Island adventure HUD before hiring. |
| `5d5754d0-cf40-439e-8057-704d9ae58284` | 80 ms guide-row click opened the guide's conversation with Leave the party. |
| `d138cc4f-b16e-4815-a76c-74b84ca84b7d` | 150 ms witness-row click opened the witness's conversation. |
| `fe4d53d0-38e6-478c-bb3c-f2d0740aef89` | Repeated guide click opened the guide's conversation. |
| `06bdbe1a-d6bd-4103-95ed-32f0c1ede3df` | Repeated witness click opened the witness's conversation. |

The parent inspected these original images. This proves bounded ordinary click behavior with stable
companion content, not every possible pointer duration or viewport. An earlier launch failed because
the isolated checkout lacked UI dependencies; it performed no gameplay actions. Both owned sessions
were stopped. The successful session reported `released: true` and `browser_closed: true`, and its host
port refused connections afterward.
