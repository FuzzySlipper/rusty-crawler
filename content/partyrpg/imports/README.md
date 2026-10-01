# Imported content packs

Packs written by the offline importer land here. They are generated from the operator's own game
data, so they are not committed: run

```bash
dotnet src/MightAndMagic7.Import.Tool/bin/Release/net10.0/mm7import.dll write --install /path/to/mm7 --output content/partyrpg/imports
```

To load them, add their pack ids to the [`partyrpg-default` bundle](../bundles/partyrpg-default/bundle.json).
The bundle is the one place that decides what plays: a pack it does not name is still read and validated,
but it contributes no definitions, no placements, and no scenario start; a broken pack it does not name
still stops the product, with a refusal that says the pack is not selected and where it was read from, so
the fix is to repair it or move it out of the root. A bundle that names a pack which
is not present stops the product with the missing pack named, so a bundle is only edited after the packs
exist.

Every pack here must record the game and the build it was taken from; the loader refuses an imported
pack that does not say where it came from.

A pack's directory name is its id, and one id belongs to one pack: a second directory claiming an id
already in the root — the same name under both `content-packs/` and `imports/`, say — is refused by name
rather than one of the two loading. The same holds inside a pack: an entry id or a document id declared
twice anywhere in the root stops the product, which is what lets a reader look an entry up by id and
find the only one there is.
