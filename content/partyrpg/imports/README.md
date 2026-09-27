# Imported content packs

Packs written by the offline importer land here. They are generated from the operator's own game
data, so they are not committed: run

```bash
dotnet src/MightAndMagic7.Import.Tool/bin/Release/net10.0/mm7import.dll write --install /path/to/mm7 --output content/partyrpg/imports
```

To load them, add their pack ids to the [`partyrpg-default` bundle](../bundles/partyrpg-default/bundle.json).
The bundle is the one place that decides what plays: a pack it does not name is still read and validated,
but it contributes no definitions, no placements, and no scenario start. A bundle that names a pack which
is not present stops the product with the missing pack named, so a bundle is only edited after the packs
exist.

Every pack here must record the game and the build it was taken from; the loader refuses an imported
pack that does not say where it came from.
