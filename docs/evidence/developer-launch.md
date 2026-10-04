# Supported developer launch

Reading of rusty-crawler#9227 on 2026-10-03, through a crew-services browser session with its own ordinary
SDK host at 1280×720. Detailed logs, original captures and cleanup receipts are indexed on the Den task.

`scripts/developer-launch.sh prepare-content --install <operator-install> --output <isolated-output>`
built the current importer, passed all 91 inventory checks, decoded all 76 maps and wrote deterministic
packs. The source was the operator's own installation. Existing imports and saves were preserved.
The wrapper also passed its doctor checks with the normal PATH, a sparse service PATH and a symlinked
`dotnet` executable; it resolved the installed SDK root rather than the symlink directory.

The ordinary `.den-serve.json` entry used that wrapper and the pinned Engine pair. **New Game** and
**Accept party** entered the imported Emerald Island world: road, gate, trees, buildings, people, fire
and the four-member adventure HUD. **W**, **Q** and **E** visibly moved and turned the party. This was
session `a559b460-25bc-401f-bd78-0731875d81b0`; the initial original capture was
`dda038d4-3ad0-479e-8f46-9da2261f262d.png`.

Starts were sequential and each test session owned its host. The stop receipt reported the browser
closed and the slot released; the observer also verified that the owned process exited and its port
closed. No broker lease timeout occurred in this run. This proves the supported local CoreCLR launch
and cleanup path, without making a claim about concurrent startup, Windows, NativeAOT or audio.
